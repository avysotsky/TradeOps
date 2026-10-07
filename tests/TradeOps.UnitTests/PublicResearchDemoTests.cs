using System.Text.Json;
using TradeOps.Application.Models;
using TradeOps.Application.Services;
using TradeOps.Domain.Enums;
using TradeOps.PublicResearchDemo;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class PublicResearchDemoTests
{
    [Fact]
    public void Sec_submissions_parser_preserves_exact_metadata()
    {
        var filings =
            SecSubmissionParser.Parse(
                SubmissionsJson);

        Assert.Equal(
            2,
            filings.Count);

        var latest =
            filings[^1];

        Assert.Equal(
            "0000051143",
            latest.Cik);
        Assert.Equal(
            "0000051143-26-000020",
            latest.AccessionNumber);
        Assert.Equal(
            "10-Q",
            latest.FormType);
        Assert.Equal(
            new DateTimeOffset(
                2026,
                7,
                22,
                20,
                10,
                0,
                TimeSpan.Zero),
            latest.AcceptedAt);
        Assert.Equal(
            "https://www.sec.gov/Archives/edgar/data/51143/000005114326000020/ibm-20260630.htm",
            latest.SourceUri.AbsoluteUri);
    }

    [Fact]
    public void Companyfacts_parser_uses_exact_accession_and_quarter_interval()
    {
        var filings =
            SecSubmissionParser.Parse(
                SubmissionsJson);

        var structured =
            SecCompanyFactsParser
                .CreateStructuredFilings(
                    CompanyFactsJson,
                    filings,
                    "IBM");

        var latest =
            structured[^1];
        var normalized =
            SecStructuredFilingNormalizer
                .Normalize(latest);

        Assert.Equal(
            "0000051143-26-000020",
            latest.AccessionNumber);
        Assert.Equal(
            new DateOnly(
                2026,
                4,
                1),
            latest.FiscalPeriodStart);
        Assert.Equal(
            new DateOnly(
                2026,
                6,
                30),
            latest.FiscalPeriodEnd);
        Assert.Equal(
            "FY2026-Q2",
            latest.FiscalPeriod);
        Assert.Equal(
            110m,
            normalized.Revenue);
        Assert.Equal(
            2.20m,
            normalized.DilutedEps);
        Assert.DoesNotContain(
            latest.Facts,
            fact =>
                fact.Value ==
                999m);
    }

    [Fact]
    public void Sec_projection_preserves_historical_provenance_without_today_time()
    {
        var filing =
            SecCompanyFactsParser
                .CreateStructuredFilings(
                    CompanyFactsJson,
                    SecSubmissionParser.Parse(
                        SubmissionsJson),
                    "IBM")[^1];

        var facts =
            SecStructuredFilingNormalizer
                .Normalize(filing);
        var earningsEvent =
            SecEarningsEventFactory.Create(
                facts,
                filing.AcceptedAt);

        Assert.Equal(
            filing.AcceptedAt,
            earningsEvent.PublishedAt);
        Assert.Equal(
            filing.AcceptedAt,
            earningsEvent.Provenance.SourceTimestamp);
        Assert.Equal(
            filing.AcceptedAt,
            earningsEvent.Provenance.RetrievedAt);
        Assert.Equal(
            filing.AccessionNumber,
            earningsEvent.Provenance.SourceDocumentId);
        Assert.Equal(
            "CIK:0000051143",
            earningsEvent.Provenance.IssuerId);
        Assert.Equal(
            filing.SourceUri,
            earningsEvent.Provenance.SourceUri);
    }

    [Fact]
    public void Alpha_vantage_daily_parser_uses_New_York_DST()
    {
        const string json =
            """
            {
              "Time Series (Daily)": {
                "2026-01-15": {
                  "1. open": "100",
                  "2. high": "101",
                  "3. low": "99",
                  "4. close": "100.5"
                },
                "2026-07-15": {
                  "1. open": "120",
                  "2. high": "121",
                  "3. low": "119",
                  "4. close": "120.5"
                }
              }
            }
            """;

        var bars =
            AlphaVantageDailyParser.Parse(
                json,
                new(
                    "IBM",
                    AssetClass.Stock,
                    "USD"));

        Assert.Equal(
            new DateTimeOffset(
                2026,
                1,
                15,
                14,
                30,
                0,
                TimeSpan.Zero),
            bars[0].OpenTime);
        Assert.Equal(
            new DateTimeOffset(
                2026,
                1,
                15,
                21,
                0,
                0,
                TimeSpan.Zero),
            bars[0].CloseTime);

        Assert.Equal(
            new DateTimeOffset(
                2026,
                7,
                15,
                13,
                30,
                0,
                TimeSpan.Zero),
            bars[1].OpenTime);
        Assert.Equal(
            new DateTimeOffset(
                2026,
                7,
                15,
                20,
                0,
                0,
                TimeSpan.Zero),
            bars[1].CloseTime);
    }

    [Fact]
    public void Cache_roundtrip_verifies_manifest_and_payload_checksum()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"tradeops-public-demo-{Guid.NewGuid():N}");

        try
        {
            var snapshot =
                new PublicDemoRawSnapshot(
                    new DateTimeOffset(
                        2026,
                        10,
                        7,
                        8,
                        0,
                        0,
                        TimeSpan.Zero),
                    SubmissionsJson,
                    CompanyFactsJson,
                    MarketJson);

            var manifest =
                PublicDemoCache.Save(
                    root,
                    snapshot,
                    new[]
                    {
                        "0000051143-26-000010",
                        "0000051143-26-000020"
                    },
                    new DateOnly(
                        2026,
                        7,
                        21),
                    new DateOnly(
                        2026,
                        10,
                        1));

            var loaded =
                PublicDemoCache.Load(root);

            Assert.Equal(
                snapshot.RetrievedAt,
                loaded.Manifest.RetrievedAt);
            Assert.Equal(
                3,
                manifest.Resources.Count);
            Assert.All(
                manifest.Resources,
                item =>
                    Assert.Equal(
                        64,
                        item.Sha256.Length));

            File.AppendAllText(
                Path.Combine(
                    root,
                    "alpha-vantage-ibm-daily.json"),
                " ");

            Assert.Throws<InvalidOperationException>(
                () =>
                    PublicDemoCache.Load(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void Full_composition_invokes_existing_VS01_pipeline()
    {
        var composition =
            PublicResearchDemoComposer.Compose(
                new PublicDemoRawSnapshot(
                    new DateTimeOffset(
                        2026,
                        10,
                        7,
                        8,
                        0,
                        0,
                        TimeSpan.Zero),
                    SubmissionsJson,
                    CompanyFactsJson,
                    MarketJson));

        var result =
            composition.Result;

        Assert.Equal(
            "IBM",
            result.Instrument.Symbol);
        Assert.Equal(
            "sec:IBM:0000051143-26-000020",
            result.EventId);
        Assert.Equal(
            "FY2026-Q2",
            result.FiscalPeriod);
        Assert.Equal(
            EarningsAssessment.Positive,
            result.Assessment);
        Assert.Equal(
            0.40m,
            result.TargetWeight);
        Assert.Equal(
            ResearchDecisionAction.SetTargetWeight,
            result.LatestDecision.Action);
        Assert.Equal(
            1,
            result.EventCount);
        Assert.Single(
            result.Backtest.Plans);
        Assert.Equal(
            RebalancePlanStatus.Ready,
            result.CurrentRebalancePlan.Status);
        Assert.NotNull(
            result.CurrentRebalancePlan.OrderIntent);
        Assert.True(
            result.CurrentRebalancePlan
                .OrderIntent!
                .RequiresRiskApproval);
    }

    [Fact]
    public void Network_gate_fails_closed_without_explicit_confirmation()
    {
        var oldConfirmation =
            Environment.GetEnvironmentVariable(
                PublicDemoNetworkGate
                    .ConfirmationVariable);
        var oldUserAgent =
            Environment.GetEnvironmentVariable(
                PublicDemoNetworkGate
                    .SecUserAgentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                PublicDemoNetworkGate
                    .ConfirmationVariable,
                null);
            Environment.SetEnvironmentVariable(
                PublicDemoNetworkGate
                    .SecUserAgentVariable,
                null);

            Assert.Throws<InvalidOperationException>(
                PublicDemoNetworkGate
                    .RequireExternalFetch);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PublicDemoNetworkGate
                    .ConfirmationVariable,
                oldConfirmation);
            Environment.SetEnvironmentVariable(
                PublicDemoNetworkGate
                    .SecUserAgentVariable,
                oldUserAgent);
        }
    }

    private const string SubmissionsJson =
        """
        {
          "cik": "51143",
          "tickers": ["IBM"],
          "filings": {
            "recent": {
              "accessionNumber": [
                "0000051143-26-000020",
                "0000051143-26-000010"
              ],
              "form": ["10-Q", "10-Q"],
              "reportDate": ["2026-06-30", "2026-03-31"],
              "filingDate": ["2026-07-22", "2026-04-22"],
              "acceptanceDateTime": [
                "2026-07-22T20:10:00Z",
                "2026-04-22T20:10:00Z"
              ],
              "primaryDocument": [
                "ibm-20260630.htm",
                "ibm-20260331.htm"
              ]
            }
          }
        }
        """;

    private const string CompanyFactsJson =
        """
        {
          "cik": 51143,
          "entityName": "International Business Machines Corporation",
          "facts": {
            "us-gaap": {
              "RevenueFromContractWithCustomerExcludingAssessedTax": {
                "units": {
                  "USD": [
                    {
                      "start": "2026-01-01",
                      "end": "2026-03-31",
                      "val": 100,
                      "accn": "0000051143-26-000010",
                      "fy": 2026,
                      "fp": "Q1",
                      "form": "10-Q"
                    },
                    {
                      "start": "2026-01-01",
                      "end": "2026-06-30",
                      "val": 205,
                      "accn": "0000051143-26-000020",
                      "fy": 2026,
                      "fp": "Q2",
                      "form": "10-Q"
                    },
                    {
                      "start": "2026-04-01",
                      "end": "2026-06-30",
                      "val": 110,
                      "accn": "0000051143-26-000020",
                      "fy": 2026,
                      "fp": "Q2",
                      "form": "10-Q"
                    },
                    {
                      "start": "2026-04-01",
                      "end": "2026-06-30",
                      "val": 999,
                      "accn": "0000051143-26-999999",
                      "fy": 2026,
                      "fp": "Q2",
                      "form": "10-Q"
                    }
                  ]
                }
              },
              "EarningsPerShareDiluted": {
                "units": {
                  "USD/shares": [
                    {
                      "start": "2026-01-01",
                      "end": "2026-03-31",
                      "val": 2.00,
                      "accn": "0000051143-26-000010",
                      "fy": 2026,
                      "fp": "Q1",
                      "form": "10-Q"
                    },
                    {
                      "start": "2026-04-01",
                      "end": "2026-06-30",
                      "val": 2.20,
                      "accn": "0000051143-26-000020",
                      "fy": 2026,
                      "fp": "Q2",
                      "form": "10-Q"
                    }
                  ]
                }
              },
              "NetIncomeLoss": {
                "units": {
                  "USD": [
                    {
                      "start": "2026-01-01",
                      "end": "2026-03-31",
                      "val": 10,
                      "accn": "0000051143-26-000010",
                      "fy": 2026,
                      "fp": "Q1",
                      "form": "10-Q"
                    },
                    {
                      "start": "2026-04-01",
                      "end": "2026-06-30",
                      "val": 12,
                      "accn": "0000051143-26-000020",
                      "fy": 2026,
                      "fp": "Q2",
                      "form": "10-Q"
                    }
                  ]
                }
              },
              "GrossProfit": {
                "units": {
                  "USD": [
                    {
                      "start": "2026-01-01",
                      "end": "2026-03-31",
                      "val": 50,
                      "accn": "0000051143-26-000010",
                      "fy": 2026,
                      "fp": "Q1",
                      "form": "10-Q"
                    },
                    {
                      "start": "2026-04-01",
                      "end": "2026-06-30",
                      "val": 56,
                      "accn": "0000051143-26-000020",
                      "fy": 2026,
                      "fp": "Q2",
                      "form": "10-Q"
                    }
                  ]
                }
              },
              "OperatingIncomeLoss": {
                "units": {
                  "USD": [
                    {
                      "start": "2026-01-01",
                      "end": "2026-03-31",
                      "val": 20,
                      "accn": "0000051143-26-000010",
                      "fy": 2026,
                      "fp": "Q1",
                      "form": "10-Q"
                    },
                    {
                      "start": "2026-04-01",
                      "end": "2026-06-30",
                      "val": 24,
                      "accn": "0000051143-26-000020",
                      "fy": 2026,
                      "fp": "Q2",
                      "form": "10-Q"
                    }
                  ]
                }
              }
            }
          }
        }
        """;

    private const string MarketJson =
        """
        {
          "Meta Data": {
            "2. Symbol": "IBM"
          },
          "Time Series (Daily)": {
            "2026-10-01": {
              "1. open": "129",
              "2. high": "131",
              "3. low": "128",
              "4. close": "130"
            },
            "2026-07-24": {
              "1. open": "124",
              "2. high": "126",
              "3. low": "123",
              "4. close": "125"
            },
            "2026-07-23": {
              "1. open": "120",
              "2. high": "125",
              "3. low": "119",
              "4. close": "124"
            },
            "2026-07-22": {
              "1. open": "118",
              "2. high": "121",
              "3. low": "117",
              "4. close": "120"
            },
            "2026-07-21": {
              "1. open": "116",
              "2. high": "119",
              "3. low": "115",
              "4. close": "118"
            }
          }
        }
        """;
}
