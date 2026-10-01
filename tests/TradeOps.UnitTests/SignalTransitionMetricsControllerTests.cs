using Microsoft.AspNetCore.Mvc;
using TradeOps.Api.Controllers;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class SignalTransitionMetricsControllerTests
{
    [Fact]
    public async Task Window_NormalizesUtcWindowAndSymbol()
    {
        var repository = new CapturingRepository();
        var controller = new SignalTransitionMetricsController(repository);

        var action = await controller.GetWindowAsync(
            DateTimeOffset.Parse("2026-10-01T12:00:00+02:00"),
            DateTimeOffset.Parse("2026-10-01T13:00:00+02:00"),
            " btcusdt ",
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<SignalTransitionMetricsWindowSnapshot>(ok.Value);

        Assert.Equal(DateTimeOffset.Parse("2026-10-01T10:00:00Z"), repository.From);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T11:00:00Z"), repository.To);
        Assert.Equal("BTCUSDT", repository.Symbol);
        Assert.Equal("BTCUSDT", payload.Symbol);
    }

    [Fact]
    public async Task Window_RejectsMissingOrInvalidBoundsWithoutQueryingRepository()
    {
        var repository = new CapturingRepository();
        var controller = new SignalTransitionMetricsController(repository);
        var at = DateTimeOffset.Parse("2026-10-01T10:00:00Z");

        var missing = await controller.GetWindowAsync(
            null,
            at,
            cancellationToken: CancellationToken.None);
        var missingProblem = Assert.IsType<ObjectResult>(missing.Result);
        Assert.Equal(400, missingProblem.StatusCode);

        var reversed = await controller.GetWindowAsync(
            at,
            at,
            cancellationToken: CancellationToken.None);
        var reversedProblem = Assert.IsType<ObjectResult>(reversed.Result);
        Assert.Equal(400, reversedProblem.StatusCode);

        Assert.Equal(0, repository.CallCount);
    }

    private sealed class CapturingRepository : ISignalTransitionMetricsRepository
    {
        public int CallCount { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }
        public string? Symbol { get; private set; }

        public Task<SignalTransitionMetricsWindowSnapshot> GetWindowAsync(
            DateTimeOffset fromInclusive,
            DateTimeOffset toExclusive,
            string? symbol = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            From = fromInclusive;
            To = toExclusive;
            Symbol = symbol;

            return Task.FromResult(new SignalTransitionMetricsWindowSnapshot(
                DateTimeOffset.UtcNow,
                fromInclusive,
                toExclusive,
                symbol,
                0,
                0,
                0));
        }
    }
}
