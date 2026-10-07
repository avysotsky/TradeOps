using TradeOps.Application.Models;
using TradeOps.Application.Services;
using TradeOps.Domain.Enums;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class EarningsTranscriptNormalizerTests
{
    [Fact]
    public void Valid_raw_transcript_normalizes_and_reuses_existing_contracts()
    {
        var document =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());

        Assert.IsType<InstrumentReference>(
            document.Instrument);
        Assert.IsType<ResearchSourceProvenance>(
            document.Provenance);
        Assert.Equal(
            "SAMP",
            document.Instrument.Symbol);
        Assert.Equal(
            "USD",
            document.Instrument.Currency);
        Assert.Equal(
            "FY2026-Q3",
            document.FiscalPeriod);
        Assert.Equal(
            SourceTimestamp,
            document.Provenance.SourceTimestamp);
        Assert.Equal(
            PublishedAt,
            document.PublishedAt);
        Assert.Equal(
            RetrievedAt,
            document.Provenance.RetrievedAt);
        Assert.StartsWith(
            EarningsTranscriptNormalizer.DocumentIdPrefix,
            document.DocumentId);
        Assert.All(
            document.Segments,
            item =>
                Assert.StartsWith(
                    EarningsTranscriptNormalizer.SegmentIdPrefix,
                    item.SegmentId));
        Assert.Matches(
            "^[0-9a-f]{64}$",
            document.Fingerprint);
    }

    [Fact]
    public void Valid_time_order_is_preserved()
    {
        var document =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());

        Assert.True(
            document.Provenance.SourceTimestamp <=
            document.PublishedAt);
        Assert.True(
            document.PublishedAt <=
            document.Provenance.RetrievedAt);
    }

    [Fact]
    public void Published_before_source_timestamp_is_rejected()
    {
        var input =
            CreateInput() with
            {
                PublishedAt =
                    SourceTimestamp.AddSeconds(-1)
            };

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                EarningsTranscriptNormalizer.Normalize(
                    input));
    }

    [Fact]
    public void Retrieval_before_publication_is_rejected()
    {
        var input =
            CreateInput() with
            {
                RetrievedAt =
                    PublishedAt.AddSeconds(-1)
            };

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                EarningsTranscriptNormalizer.Normalize(
                    input));
    }

    [Fact]
    public void Empty_transcript_is_rejected()
    {
        var input =
            CreateInput() with
            {
                Segments =
                    Array.Empty<RawTranscriptSegment>()
            };

        Assert.Throws<ArgumentException>(
            () =>
                EarningsTranscriptNormalizer.Normalize(
                    input));
    }

    [Fact]
    public void Empty_meaningful_segment_is_rejected()
    {
        var input =
            CreateInput() with
            {
                Segments =
                [
                    new RawTranscriptSegment(
                        1,
                        "p-ceo",
                        " \r\n \t ")
                ]
            };

        Assert.Throws<ArgumentException>(
            () =>
                EarningsTranscriptNormalizer.Normalize(
                    input));
    }

    [Fact]
    public void Duplicate_sequence_is_rejected()
    {
        var input =
            CreateInput() with
            {
                Segments =
                [
                    new RawTranscriptSegment(
                        1,
                        "p-ceo",
                        "First."),
                    new RawTranscriptSegment(
                        1,
                        "p-cfo",
                        "Second.")
                ]
            };

        Assert.Throws<ArgumentException>(
            () =>
                EarningsTranscriptNormalizer.Normalize(
                    input));
    }

    [Fact]
    public void Segment_order_is_deterministic_by_explicit_sequence()
    {
        var input =
            CreateInput() with
            {
                Segments =
                [
                    new RawTranscriptSegment(
                        2,
                        "p-cfo",
                        "Second."),
                    new RawTranscriptSegment(
                        1,
                        "p-ceo",
                        "First.")
                ]
            };

        var document =
            EarningsTranscriptNormalizer.Normalize(
                input);

        Assert.Equal(
            [1, 2],
            document.Segments
                .Select(
                    item =>
                        item.Sequence)
                .ToArray());
        Assert.Equal(
            ["First.", "Second."],
            document.Segments
                .Select(
                    item =>
                        item.Text)
                .ToArray());
    }

    [Fact]
    public void Same_logical_input_has_same_document_segment_ids_and_fingerprint()
    {
        var first =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());
        var second =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());

        Assert.Equal(
            first.DocumentId,
            second.DocumentId);
        Assert.Equal(
            first.Segments
                .Select(
                    item =>
                        item.SegmentId)
                .ToArray(),
            second.Segments
                .Select(
                    item =>
                        item.SegmentId)
                .ToArray());
        Assert.Equal(
            first.Fingerprint,
            second.Fingerprint);
    }

    [Fact]
    public void CrLf_and_lf_are_fingerprint_equivalent()
    {
        var crlf =
            CreateInput() with
            {
                Segments =
                [
                    new RawTranscriptSegment(
                        1,
                        "p-ceo",
                        "Revenue improved.\r\nMargin remained stable.")
                ]
            };
        var lf =
            crlf with
            {
                Segments =
                [
                    new RawTranscriptSegment(
                        1,
                        "p-ceo",
                        "Revenue improved.\nMargin remained stable.")
                ]
            };

        var first =
            EarningsTranscriptNormalizer.Normalize(
                crlf);
        var second =
            EarningsTranscriptNormalizer.Normalize(
                lf);

        Assert.Equal(
            first.Segments[0].SegmentId,
            second.Segments[0].SegmentId);
        Assert.Equal(
            first.Fingerprint,
            second.Fingerprint);
    }

    [Fact]
    public void Surrounding_and_trailing_whitespace_are_fingerprint_equivalent()
    {
        var padded =
            CreateInput() with
            {
                Provider =
                    " SyntheticProvider ",
                FiscalPeriod =
                    " FY2026-Q3 ",
                Title =
                    " Synthetic Earnings Call ",
                Instrument =
                    new InstrumentReference(
                        " samp ",
                        AssetClass.Stock,
                        " usd "),
                Participants =
                [
                    new RawTranscriptParticipant(
                        "p-ceo",
                        " CEO ",
                        TranscriptParticipantRole.Executive,
                        " SAMP Industries "),
                    new RawTranscriptParticipant(
                        "p-cfo",
                        " CFO ",
                        TranscriptParticipantRole.Executive,
                        " SAMP Industries ")
                ],
                Segments =
                [
                    new RawTranscriptSegment(
                        1,
                        "p-ceo",
                        "  Revenue improved.   \n  Margin remained stable.   "),
                    new RawTranscriptSegment(
                        2,
                        "p-cfo",
                        " Cash remained strong.   ")
                ]
            };
        var clean =
            CreateInput();

        var first =
            EarningsTranscriptNormalizer.Normalize(
                padded);
        var second =
            EarningsTranscriptNormalizer.Normalize(
                clean);

        Assert.Equal(
            first.DocumentId,
            second.DocumentId);
        Assert.Equal(
            first.Fingerprint,
            second.Fingerprint);
    }

    [Fact]
    public void Meaningful_text_change_changes_segment_identity_and_fingerprint()
    {
        var original =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());
        var changed =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput() with
                {
                    Segments =
                    [
                        new RawTranscriptSegment(
                            1,
                            "p-ceo",
                            "Revenue declined."),
                        new RawTranscriptSegment(
                            2,
                            "p-cfo",
                            "Cash remained strong.")
                    ]
                });

        Assert.NotEqual(
            original.Segments[0].SegmentId,
            changed.Segments[0].SegmentId);
        Assert.NotEqual(
            original.Fingerprint,
            changed.Fingerprint);
    }

    [Fact]
    public void Segment_order_change_changes_fingerprint()
    {
        var first =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());
        var second =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput() with
                {
                    Segments =
                    [
                        new RawTranscriptSegment(
                            1,
                            "p-cfo",
                            "Cash remained strong."),
                        new RawTranscriptSegment(
                            2,
                            "p-ceo",
                            "Revenue improved.\nMargin remained stable.")
                    ]
                });

        Assert.NotEqual(
            first.Fingerprint,
            second.Fingerprint);
    }

    [Fact]
    public void Normalized_serialization_round_trip_preserves_meaning_and_fingerprint()
    {
        var document =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());

        var json =
            EarningsTranscriptJson.SerializeNormalized(
                document);
        var roundTrip =
            EarningsTranscriptJson.DeserializeNormalized(
                json);

        Assert.Equal(
            document.DocumentId,
            roundTrip.DocumentId);
        Assert.Equal(
            document.Instrument,
            roundTrip.Instrument);
        Assert.Equal(
            document.FiscalPeriod,
            roundTrip.FiscalPeriod);
        Assert.Equal(
            document.Title,
            roundTrip.Title);
        Assert.Equal(
            document.PublishedAt,
            roundTrip.PublishedAt);
        Assert.Equal(
            document.Provenance,
            roundTrip.Provenance);
        Assert.Equal(
            document.Participants.ToArray(),
            roundTrip.Participants.ToArray());
        Assert.Equal(
            document.Segments.ToArray(),
            roundTrip.Segments.ToArray());
        Assert.Equal(
            document.Fingerprint,
            roundTrip.Fingerprint);
    }

    [Fact]
    public void Normalized_json_with_tampered_fingerprint_fails_closed()
    {
        var document =
            EarningsTranscriptNormalizer.Normalize(
                CreateInput());
        var json =
            EarningsTranscriptJson
                .SerializeNormalized(
                    document)
                .Replace(
                    document.Fingerprint,
                    new string(
                        '0',
                        64),
                    StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(
            () =>
                EarningsTranscriptJson
                    .DeserializeNormalized(
                        json));
    }

    [Fact]
    public void Duplicate_display_names_do_not_merge_distinct_explicit_participants()
    {
        var input =
            CreateInput() with
            {
                Participants =
                [
                    new RawTranscriptParticipant(
                        "p-one",
                        "Alex",
                        TranscriptParticipantRole.Analyst),
                    new RawTranscriptParticipant(
                        "p-two",
                        "Alex",
                        TranscriptParticipantRole.Executive)
                ],
                Segments =
                [
                    new RawTranscriptSegment(
                        1,
                        "p-one",
                        "Question."),
                    new RawTranscriptSegment(
                        2,
                        "p-two",
                        "Answer.")
                ]
            };

        var document =
            EarningsTranscriptNormalizer.Normalize(
                input);

        Assert.Equal(
            2,
            document.Participants.Count);
        Assert.Contains(
            document.Participants,
            item =>
                item.ParticipantId == "p-one" &&
                item.DisplayName == "Alex");
        Assert.Contains(
            document.Participants,
            item =>
                item.ParticipantId == "p-two" &&
                item.DisplayName == "Alex");
        Assert.Equal(
            "p-one",
            document.Segments[0].ParticipantId);
        Assert.Equal(
            "p-two",
            document.Segments[1].ParticipantId);
    }

    [Fact]
    public void Synthetic_sample_fixture_normalizes_successfully()
    {
        var fixturePath =
            FindRepositoryFile(
                "samples/research/earnings-transcript.sample.json");
        var raw =
            EarningsTranscriptJson.DeserializeRaw(
                File.ReadAllText(
                    fixturePath));

        var document =
            EarningsTranscriptNormalizer.Normalize(
                raw);

        Assert.Equal(
            "SAMP",
            document.Instrument.Symbol);
        Assert.Equal(
            4,
            document.Participants.Count);
        Assert.Equal(
            5,
            document.Segments.Count);
        Assert.Matches(
            "^[0-9a-f]{64}$",
            document.Fingerprint);
    }

    private static RawEarningsTranscriptInput
        CreateInput() =>
        new(
            Provider:
                "SyntheticProvider",
            SourceUri:
                new Uri(
                    "https://example.invalid/transcripts/samp-fy2026-q3"),
            SourceDocumentId:
                "samp-fy2026-q3-call",
            IssuerId:
                "SAMP-ISSUER-001",
            Instrument:
                new InstrumentReference(
                    "SAMP",
                    AssetClass.Stock,
                    "USD"),
            FiscalPeriod:
                "FY2026-Q3",
            Title:
                "Synthetic Earnings Call",
            SourceTimestamp:
                SourceTimestamp,
            PublishedAt:
                PublishedAt,
            RetrievedAt:
                RetrievedAt,
            Participants:
            [
                new RawTranscriptParticipant(
                    "p-ceo",
                    "CEO",
                    TranscriptParticipantRole.Executive,
                    "SAMP Industries"),
                new RawTranscriptParticipant(
                    "p-cfo",
                    "CFO",
                    TranscriptParticipantRole.Executive,
                    "SAMP Industries")
            ],
            Segments:
            [
                new RawTranscriptSegment(
                    1,
                    "p-ceo",
                    "Revenue improved.\nMargin remained stable."),
                new RawTranscriptSegment(
                    2,
                    "p-cfo",
                    "Cash remained strong.")
            ],
            ExtractionMethod:
                "provider-json-transcript-v1");

    private static string FindRepositoryFile(
        string relativePath)
    {
        var current =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate =
                Path.Combine(
                    current.FullName,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));

            if (File.Exists(
                    candidate))
            {
                return candidate;
            }

            current =
                current.Parent;
        }

        throw new FileNotFoundException(
            $"Repository fixture not found: {relativePath}");
    }

    private static readonly DateTimeOffset
        SourceTimestamp =
            new(
                2026,
                10,
                1,
                20,
                0,
                0,
                TimeSpan.Zero);

    private static readonly DateTimeOffset
        PublishedAt =
            new(
                2026,
                10,
                1,
                20,
                5,
                0,
                TimeSpan.Zero);

    private static readonly DateTimeOffset
        RetrievedAt =
            new(
                2026,
                10,
                1,
                20,
                10,
                0,
                TimeSpan.Zero);
}
