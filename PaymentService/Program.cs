using Application.Interfaces;
using Application.Services;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using PaymentService.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Add controllers and swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Register DB (PostgreSQL)
builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=db;Port=5432;Database=payments;Username=postgres;Password=140302" 
    ));

// 3. Linking interfaces to implementations (DI)
builder.Services.AddScoped<IOperationRepository, OperationRepository>();
builder.Services.AddScoped<IOperationService, OperationService>();

// 4. Registering HttpClient to communicate with the provider simulator
builder.Services.AddHttpClient("ProviderClient", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ProviderSettings:BaseUrl"]
        ?? "http://localhost:8081"
    );
});

var app = builder.Build();

// Register exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Apply migrations on startup with retry logic
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

    for (int i = 0; i < 10; i++)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("✅ Migrations applied successfully");
            break;
        }
        catch (Exception ex) when (i < 9)
        {
            Console.WriteLine($"⏳ Attempt {i + 1} failed: {ex.Message}. Retrying in 3s...");
            await Task.Delay(3000);
        }
    }
}

// Swagger in development mode only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();