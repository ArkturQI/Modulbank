using System.Text;
using System.Text.Json;
using Application.Interfaces;

namespace PaymentService.Background;

public class ProviderSubmissionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProviderSubmissionBackgroundService> _logger;

    public ProviderSubmissionBackgroundService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<ProviderSubmissionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Provider Submission Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();

                var pendingOperations = await repository.GetProcessingOperationsAsync(stoppingToken);

                foreach (var operation in pendingOperations)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        var client = _httpClientFactory.CreateClient("ProviderClient");
                        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/payments");

                        // Ensure idempotency on the provider side to prevent duplicate charges on retries
                        requestMessage.Headers.Add("Idempotency-Key", operation.OperationId);
                        requestMessage.Headers.Add("X-Correlation-ID", operation.OperationId);

                        var payload = new
                        {
                            operationId = operation.OperationId,
                            amount = operation.Amount.ToString("F2"),
                            currency = operation.Currency
                        };

                        requestMessage.Content = new StringContent(
                            JsonSerializer.Serialize(payload),
                            Encoding.UTF8,
                            "application/json");

                        _logger.LogInformation("Sending submission for operation {Id}", operation.OperationId);
                        var response = await client.SendAsync(requestMessage, stoppingToken);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync(stoppingToken);

                            try
                            {
                                // Safely extract the provider's payment ID to link the external transaction with our internal operation
                                using var doc = JsonDocument.Parse(responseContent);
                                if (doc.RootElement.TryGetProperty("providerPaymentId", out var providerIdProp))
                                {
                                    var providerPaymentId = providerIdProp.GetString();

                                    if (!string.IsNullOrEmpty(providerPaymentId))
                                    {
                                        // Create a new scope for the update to ensure fresh DbContext tracking
                                        using var updateScope = _serviceProvider.CreateScope();
                                        var updateRepo = updateScope.ServiceProvider.GetRequiredService<IOperationRepository>();

                                        var opToUpdate = await updateRepo.GetByIdAsync(operation.OperationId, stoppingToken);
                                        if (opToUpdate != null && opToUpdate.TrySetProviderPaymentId(providerPaymentId))
                                        {
                                            await updateRepo.SaveChangesAsync(stoppingToken);
                                            _logger.LogInformation("Operation {Id} linked with ProviderPaymentId {ProviderId}",
                                                operation.OperationId, providerPaymentId);
                                        }
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                // Fail gracefully on malformed JSON to avoid crashing the entire background processing loop
                                _logger.LogWarning("Provider response was not valid JSON for operation {Id}", operation.OperationId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Provider returned status {Status} for operation {Id}",
                                response.StatusCode, operation.OperationId);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Isolate failures: log the error and continue with the next operation
                        _logger.LogError(ex, "Failed to submit operation {Id} to provider", operation.OperationId);
                    }
                }

                // Polling interval to balance responsiveness with database and provider load
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background service loop");

                // Backoff delay on critical loop failures to prevent tight failure cycles
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}