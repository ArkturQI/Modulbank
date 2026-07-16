using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                                // Case-insensitive deserialization prevents failures if provider uses PascalCase or snake_case
                                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                var providerData = JsonSerializer.Deserialize<ProviderResponse>(responseContent, options);

                                if (providerData != null && !string.IsNullOrEmpty(providerData.ProviderPaymentId))
                                {
                                    // Create a new scope for the update to ensure fresh DbContext tracking
                                    using var updateScope = _serviceProvider.CreateScope();
                                    var updateRepo = updateScope.ServiceProvider.GetRequiredService<IOperationRepository>();

                                    var opToUpdate = await updateRepo.GetByIdAsync(operation.OperationId, stoppingToken);
                                    if (opToUpdate != null && opToUpdate.TrySetProviderPaymentId(providerData.ProviderPaymentId))
                                    {
                                        await updateRepo.SaveChangesAsync(stoppingToken);
                                        _logger.LogInformation("Operation {Id} linked with ProviderPaymentId {ProviderId}",
                                            operation.OperationId, providerData.ProviderPaymentId);
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

// Helper record for robust, case-insensitive JSON deserialization
internal record ProviderResponse(string? ProviderPaymentId);