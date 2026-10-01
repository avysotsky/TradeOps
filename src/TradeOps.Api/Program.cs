using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Application.Services;
using TradeOps.Infrastructure.Alerts;
using TradeOps.Infrastructure.Exchange;
using TradeOps.Infrastructure.Persistence;
using TradeOps.Infrastructure.Persistence.Repositories;
using TradeOps.Infrastructure.Risk;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var connectionString = builder.Configuration.GetConnectionString("TradeOpsDb")
    ?? throw new InvalidOperationException(
        "Connection string 'TradeOpsDb' is not configured.");

builder.Services.AddDbContext<TradeOpsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient("telegram");
builder.Services.AddSingleton<IAlertService, TelegramAlertService>();

builder.Services.AddTradeOpsExchange(builder.Configuration);

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IFillRepository, EfFillRepository>();
builder.Services.AddScoped<IPositionSnapshotRepository, EfPositionSnapshotRepository>();
builder.Services.AddSingleton<IClientOrderIdGenerator, ClientOrderIdGenerator>();
builder.Services.AddSingleton<IOrderStateMachine, OrderStateMachine>();
builder.Services.AddSingleton<IRiskState, InMemoryRiskState>();
builder.Services.AddSingleton(new RiskSettings());
builder.Services.AddScoped<IRiskEngine, RiskEngine>();
builder.Services.AddScoped<IOrderManager, OrderManager>();
builder.Services.AddScoped<IOrderReconciliationService, OrderReconciliationService>();
builder.Services.AddScoped<IPositionService, PositionService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TradeOpsDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
