using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TradeOps.Infrastructure.Persistence;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class TradingViewWebhookAdapterApiIntegrationTests
{
    private const string GatewayHeader =
        "X-TradeOps-TradingView-Gateway-Key";
    private const string GatewayKey =
        "tradingview-integration-gateway-key-32-plus";

    [Fact]
    public async Task Adapter_Authenticates_Normalizes_AndMakesRedeliveryIdempotent()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            "TRADEOPS_TEST_POSTGRES_ADMIN");
        var isGitHubActions = string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(adminConnectionString)
            && !isGitHubActions)
        {
            return;
        }

        adminConnectionString ??=
            "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=postgres";

        var databaseName =
            $"tradeops_tradingview_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(
            adminConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        var testBuilder = new NpgsqlConnectionStringBuilder(
            adminBuilder.ConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        await using var adminConnection = new NpgsqlConnection(
            adminBuilder.ConnectionString);
        await adminConnection.OpenAsync();

        await using (var createDatabase = new NpgsqlCommand(
                         $"CREATE DATABASE \"{databaseName}\"",
                         adminConnection))
        {
            await createDatabase.ExecuteNonQueryAsync();
        }

        try
        {
            using var factory = CreateFactory(
                testBuilder.ConnectionString);
            using var client = factory.CreateClient();

            var payload = new
            {
                eventId = "BTCUSDT|1h|2026-10-04T15:00:00Z|long-entry",
                symbol = "btcusdt",
                action = "buy",
                quantity = 0.001m,
                riskPercent = 1.0m
            };

            using (var unauthenticated = await client.PostAsJsonAsync(
                       "/api/integrations/tradingview",
                       payload))
            {
                Assert.Equal(
                    HttpStatusCode.Unauthorized,
                    unauthenticated.StatusCode);
            }

            await AssertCountsAsync(
                factory.Services,
                expectedSignals: 0,
                expectedOrders: 0);

            using var firstRequest = CreateGatewayRequest(payload);
            using var firstResponse = await client.SendAsync(firstRequest);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            var first = await firstResponse.Content
                .ReadFromJsonAsync<TradingViewResponse>();
            Assert.NotNull(first);
            Assert.True(first.Accepted);
            Assert.NotEqual(Guid.Empty, first.SignalId);
            Assert.NotNull(first.Order);
            Assert.StartsWith(
                "trd-",
                first.Order.ClientOrderId);

            await AssertCountsAsync(
                factory.Services,
                expectedSignals: 1,
                expectedOrders: 1);

            using var retryRequest = CreateGatewayRequest(payload);
            using var retryResponse = await client.SendAsync(retryRequest);
            Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

            var retry = await retryResponse.Content
                .ReadFromJsonAsync<TradingViewResponse>();
            Assert.NotNull(retry);
            Assert.Equal(first.SignalId, retry.SignalId);
            Assert.Equal(
                first.Order!.ClientOrderId,
                retry.Order!.ClientOrderId);

            await AssertCountsAsync(
                factory.Services,
                expectedSignals: 1,
                expectedOrders: 1);

            var conflictingPayload = new
            {
                eventId = payload.eventId,
                symbol = "BTCUSDT",
                action = "buy",
                quantity = 0.002m,
                riskPercent = 1.0m
            };

            using var conflictRequest =
                CreateGatewayRequest(conflictingPayload);
            using var conflictResponse =
                await client.SendAsync(conflictRequest);

            Assert.Equal(
                HttpStatusCode.Conflict,
                conflictResponse.StatusCode);

            await AssertCountsAsync(
                factory.Services,
                expectedSignals: 1,
                expectedOrders: 1);

            using var signalAudit = await client.GetAsync(
                $"/api/signals/{first.SignalId:D}");
            Assert.Equal(
                HttpStatusCode.OK,
                signalAudit.StatusCode);

            var auditJson = await signalAudit.Content
                .ReadFromJsonAsync<TradingSignalAudit>();
            Assert.NotNull(auditJson);
            Assert.Equal("BTCUSDT", auditJson.Symbol);
            Assert.Equal("TradingView", auditJson.SignalType);
            Assert.Equal("TradingView", auditJson.Source);
        }
        finally
        {
            await using var dropDatabase = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                adminConnection);
            await dropDatabase.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Adapter_RejectsInvalidActionBeforeExecution()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            "TRADEOPS_TEST_POSTGRES_ADMIN");
        var isGitHubActions = string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(adminConnectionString)
            && !isGitHubActions)
        {
            return;
        }

        adminConnectionString ??=
            "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=postgres";

        var databaseName =
            $"tradeops_tradingview_invalid_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(
            adminConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        var testBuilder = new NpgsqlConnectionStringBuilder(
            adminBuilder.ConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        await using var adminConnection = new NpgsqlConnection(
            adminBuilder.ConnectionString);
        await adminConnection.OpenAsync();

        await using (var createDatabase = new NpgsqlCommand(
                         $"CREATE DATABASE \"{databaseName}\"",
                         adminConnection))
        {
            await createDatabase.ExecuteNonQueryAsync();
        }

        try
        {
            using var factory = CreateFactory(
                testBuilder.ConnectionString);
            using var client = factory.CreateClient();

            var invalid = new
            {
                eventId = "invalid-action-event",
                symbol = "BTCUSDT",
                action = "hold",
                quantity = 0.001m
            };

            using var request = CreateGatewayRequest(invalid);
            using var response = await client.SendAsync(request);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);

            await AssertCountsAsync(
                factory.Services,
                expectedSignals: 0,
                expectedOrders: 0);
        }
        finally
        {
            await using var dropDatabase = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                adminConnection);
            await dropDatabase.ExecuteNonQueryAsync();
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:TradeOpsDb",
                    connectionString);
                builder.UseSetting(
                    "Exchange:Provider",
                    "Mock");
                builder.UseSetting(
                    "Telegram:Enabled",
                    "false");
                builder.UseSetting(
                    "Integrations:TradingView:Enabled",
                    "true");
                builder.UseSetting(
                    "Integrations:TradingView:GatewayHeaderName",
                    GatewayHeader);
                builder.UseSetting(
                    "Integrations:TradingView:GatewayKey",
                    GatewayKey);
                builder.UseSetting(
                    "Integrations:TradingView:RequireGatewayIpAllowlist",
                    "false");
            });
    }

    private static HttpRequestMessage CreateGatewayRequest(
        object payload)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/integrations/tradingview");

        request.Headers.Add(
            GatewayHeader,
            GatewayKey);
        request.Content = JsonContent.Create(payload);
        return request;
    }

    private static async Task AssertCountsAsync(
        IServiceProvider services,
        int expectedSignals,
        int expectedOrders)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<TradeOpsDbContext>();

        Assert.Equal(
            expectedSignals,
            await dbContext.TradingSignals.CountAsync());
        Assert.Equal(
            expectedOrders,
            await dbContext.Orders.CountAsync());
    }

    private sealed record TradingViewResponse(
        string EventId,
        Guid SignalId,
        bool Accepted,
        IReadOnlyCollection<string> RiskReasons,
        OrderResponse? Order);

    private sealed record OrderResponse(
        string? ExchangeOrderId,
        string ClientOrderId,
        string Status,
        decimal FilledQuantity,
        decimal? AverageFillPrice);

    private sealed record TradingSignalAudit(
        Guid SignalId,
        string Symbol,
        string? SignalType,
        string? Source);
}
