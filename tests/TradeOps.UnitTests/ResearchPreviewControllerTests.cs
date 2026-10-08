using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TradeOps.Api.Controllers;
using TradeOps.Application.Models;
using TradeOps.Domain.Enums;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class ResearchPreviewControllerTests
{
    private static ResearchPreviewController Controller(bool enabled = true) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ResearchPreview:Enabled"] = enabled.ToString(),
            ["OperatorApi:Authentication:Enabled"] = "true"
        }).Build());

    private static ResearchDecision Decision() => new(
        "sample-decision", "sample-strategy",
        new InstrumentReference("SAMP", AssetClass.Stock, "USD"),
        ResearchDecisionAction.SetTargetWeight,
        new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
        TargetWeight: 0.30m,
        Confidence: 0.90m);

    private static RebalancePreviewHttpRequest Request() => new(
        Decision(),
        new PortfolioSnapshot("USD", 10000m, 10000m,
            Array.Empty<PortfolioPosition>(),
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)),
        100m, null);

    [Fact]
    public void ResearchValidationUsesProductionValidator()
    {
        var response = Controller().Decision(Decision());
        var result = Assert.IsType<OkObjectResult>(response);
        var validation = Assert.IsType<ResearchDecisionValidationResult>(result.Value);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void RebalanceRouteUsesProductionPlanner()
    {
        var response = Controller().Rebalance(Request());
        var result = Assert.IsType<OkObjectResult>(response);
        var plan = Assert.IsType<RebalancePlan>(result.Value);
        Assert.Equal(RebalancePlanStatus.Ready, plan.Status);
        Assert.Equal(30m, plan.OrderIntent!.Quantity);
        Assert.Equal("sample-decision", plan.DecisionId);
    }

    [Fact]
    public void RebalanceInvalidInputIsRejected()
    {
        var response = Controller().Rebalance(Request() with { ReferencePrice = -1m });
        var result = Assert.IsType<OkObjectResult>(response);
        var plan = Assert.IsType<RebalancePlan>(result.Value);
        Assert.Equal(RebalancePlanStatus.Blocked, plan.Status);
    }

    [Fact]
    public void AllRoutesAreDisabledWithoutExplicitOptIn()
    {
        Assert.IsType<NotFoundResult>(Controller(false).Decision(Decision()));
        Assert.IsType<NotFoundResult>(Controller(false).Rebalance(Request()));
    }
}
