using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using TradeOps.Api.Contracts;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Application.Services;
using TradeOps.Infrastructure.Alerts;
using TradeOps.Infrastructure.Exchange;
using TradeOps.Infrastructure.Persistence;
using TradeOps.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TradeOps API",
        Version = "v1",
        Description = "Execution and operator API. Exchange routes reflect the configured exchange adapter; operator audit routes reflect local PostgreSQL state."
    });
});

var connectionString = builder.Configuration.GetConnectionString("TradeOpsDb")
    ?? throw new InvalidOperationException("Connection string 'TradeOpsDb' is not configured.");

builder.Services.AddDbContext<TradeOpsDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient("telegram");
builder.Services.AddSingleton<IAlertService, TelegramAlertService>();
builder.Services.AddTradeOpsExchange(builder.Configuration);

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IOrderCancellationCandidateRepository, EfOrderCancellationCandidateRepository>();
builder.Services.AddScoped<IFillRepository, EfFillRepository>();
builder.Services.AddScoped<IPositionSnapshotRepository, EfPositionSnapshotRepository>();
builder.Services.AddScoped<IRiskEventRepository, EfRiskEventRepository>();
builder.Services.AddScoped<IOperationalRiskStateRepository, EfOperationalRiskStateRepository>();
builder.Services.AddScoped<IOperationalRunStatusRepository, EfOperationalRunStatusRepository>();
builder.Services.AddScoped<IOperationalRunHistoryRepository, EfOperationalRunHistoryRepository>();
builder.Services.AddScoped<ITradingSignalRepository, EfTradingSignalRepository>();
builder.Services.AddScoped<ITradingSignalOutcomeHistoryRepository, EfTradingSignalOutcomeHistoryRepository>();
builder.Services.AddScoped<IOperatorReadRepository, EfOperatorReadRepository>();
builder.Services.AddScoped<ILocalOrderAuditRepository, EfLocalOrderAuditRepository>();
builder.Services.AddScoped<IExecutionMetricsRepository, EfExecutionMetricsRepository>();
builder.Services.AddScoped<IExecutionMetricsBySymbolRepository, EfExecutionMetricsBySymbolRepository>();
builder.Services.AddScoped<ISignalTransitionMetricsRepository, EfSignalTransitionMetricsRepository>();
builder.Services.AddSingleton<IClientOrderIdGenerator, ClientOrderIdGenerator>();
builder.Services.AddSingleton<IOrderStateMachine, OrderStateMachine>();
builder.Services.AddSingleton(new RiskSettings());
builder.Services.AddSingleton(new AccountingSettings
{
    SettlementCurrency = builder.Configuration["Accounting:SettlementCurrency"]?.Trim().ToUpperInvariant() ?? "USDT"
});
builder.Services.AddScoped<IRiskControlService, RiskControlService>();
builder.Services.AddScoped<IRiskEngine, RiskEngine>();
builder.Services.AddScoped<IOrderManager, OrderManager>();
builder.Services.AddScoped<IOrderCancellationService, OrderCancellationService>();
builder.Services.AddScoped<IOrderBulkCancellationService, OrderBulkCancellationService>();
builder.Services.AddScoped<IEmergencyStopService, EmergencyStopService>();
builder.Services.AddScoped<ISignalExecutionService, SignalExecutionService>();
builder.Services.AddScoped<IOrderReconciliationService, OrderReconciliationService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IPositionReconciliationService, PositionReconciliationService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TradeOpsDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "TradeOps API v1"));

app.MapGet("/health", () => Results.Ok(new HealthResponse("live"))).ExcludeFromDescription();
app.MapGet("/health/live", () => Results.Ok(new HealthResponse("live")))
    .WithName("GetLiveness")
    .WithTags("Health")
    .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapGet("/health/ready", async (
        TradeOpsDbContext dbContext,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
    {
        var dependencies = new Dictionary<string, string>();
        bool postgresReady;

        try
        {
            postgresReady = await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            postgresReady = false;
        }

        dependencies["postgres"] = postgresReady ? "ready" : "unavailable";
        if (!postgresReady)
        {
            return Results.Json(
                new HealthResponse("not-ready", dependencies),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var exchangeClient = serviceProvider.GetService<IExchangeClient>();
            if (exchangeClient is null)
            {
                dependencies["exchangeClient"] = "unavailable";
                return Results.Json(
                    new HealthResponse("not-ready", dependencies),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            dependencies["exchangeClient"] = exchangeClient.GetType().Name;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            dependencies["exchangeClient"] = "unavailable";
            return Results.Json(
                new HealthResponse("not-ready", dependencies),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Json(
            new HealthResponse("ready", dependencies),
            statusCode: StatusCodes.Status200OK);
    })
    .WithName("GetReadiness")
    .WithTags("Health")
    .Produces<HealthResponse>(StatusCodes.Status200OK)
    .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

app.MapControllers();
app.Run();

public partial class Program
{
}
