using Microsoft.Extensions.Options;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;

namespace TradeOps.Worker;

public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IExchangeConnectionManager connectionManager,
    IAlertService alertService,
    IOptions<WorkerOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private bool _wasDisconnected;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromSeconds(Math.Max(5, settings.ReconciliationIntervalSeconds));

        logger.LogInformation(
            "TradeOps recovery worker started. Reconciliation interval: {IntervalSeconds}s.",
            interval.TotalSeconds);

        await RunRecoveryCycleWithRetryAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunRecoveryCycleWithRetryAsync(stoppingToken);
        }
    }

    private async Task RunRecoveryCycleWithRetryAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var retryDelaySeconds = Math.Max(1, settings.InitialRetryDelaySeconds);
        var maxRetryDelaySeconds = Math.Max(retryDelaySeconds, settings.MaxRetryDelaySeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await connectionManager.EnsureConnectedAsync(stoppingToken);

                if (_wasDisconnected)
                {
                    logger.LogInformation("Exchange connection recovered.");

                    await alertService.SendAsync(
                        new AlertMessage(
                            "ApiReconnected",
                            "TradeOps exchange connection recovered.",
                            AlertSeverity.Info),
                        stoppingToken);

                    _wasDisconnected = false;
                }

                using var scope = scopeFactory.CreateScope();
                var reconciliationService = scope.ServiceProvider
                    .GetRequiredService<IOrderReconciliationService>();

                var summary = await reconciliationService.ReconcileAsync(stoppingToken);

                logger.LogInformation(
                    "Recovery cycle completed. Connected={Connected}, Scanned={Scanned}, Updated={Updated}, Issues={IssueCount}.",
                    connectionManager.IsConnected,
                    summary.Scanned,
                    summary.Updated,
                    summary.Issues.Count);

                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Recovery cycle failed. Retrying in {RetryDelaySeconds}s.",
                    retryDelaySeconds);

                if (!_wasDisconnected)
                {
                    await alertService.SendAsync(
                        new AlertMessage(
                            "ApiDisconnected",
                            $"TradeOps recovery cycle failed: {exception.Message}",
                            AlertSeverity.Error),
                        stoppingToken);
                }

                _wasDisconnected = true;

                await Task.Delay(
                    TimeSpan.FromSeconds(retryDelaySeconds),
                    stoppingToken);

                retryDelaySeconds = Math.Min(
                    retryDelaySeconds * 2,
                    maxRetryDelaySeconds);
            }
        }
    }
}
