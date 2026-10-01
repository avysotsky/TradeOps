using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Services;
using TradeOps.Infrastructure.Alerts;
using TradeOps.Infrastructure.Exchange;
using TradeOps.Infrastructure.Persistence;
using TradeOps.Infrastructure.Persistence.Repositories;
using TradeOps.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TradeOpsDb")
    ?? throw new InvalidOperationException("Connection string 'TradeOpsDb' is not configured.");

builder.Services.AddDbContext<TradeOpsDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services.AddHttpClient("telegram");
builder.Services.AddSingleton<IAlertService, TelegramAlertService>();

builder.Services.AddTradeOpsExchange(builder.Configuration);

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IFillRepository, EfFillRepository>();
builder.Services.AddScoped<IPositionSnapshotRepository, EfPositionSnapshotRepository>();
builder.Services.AddScoped<IRiskEventRepository, EfRiskEventRepository>();
builder.Services.AddSingleton<IOrderStateMachine, OrderStateMachine>();
builder.Services.AddScoped<IOrderReconciliationService, OrderReconciliationService>();
builder.Services.AddScoped<IExchangeEventProcessor, ExchangeEventProcessor>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IPositionReconciliationService, PositionReconciliationService>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ExchangeEventWorker>();

var host = builder.Build();
host.Run();
