using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Application.Services;
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

builder.Services.AddSingleton<IExchangeClient, MockExchangeClient>();
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddSingleton<IClientOrderIdGenerator, ClientOrderIdGenerator>();
builder.Services.AddSingleton<IRiskState, InMemoryRiskState>();
builder.Services.AddSingleton(new RiskSettings());
builder.Services.AddScoped<IRiskEngine, RiskEngine>();
builder.Services.AddScoped<IOrderManager, OrderManager>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
