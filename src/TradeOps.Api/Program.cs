using System.Text.Json.Serialization;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Application.Services;
using TradeOps.Infrastructure.Exchange;
using TradeOps.Infrastructure.Risk;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSingleton<IExchangeClient, MockExchangeClient>();
builder.Services.AddSingleton<IRiskState, InMemoryRiskState>();
builder.Services.AddSingleton(new RiskSettings());
builder.Services.AddScoped<IRiskEngine, RiskEngine>();
builder.Services.AddScoped<IOrderManager, OrderManager>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
