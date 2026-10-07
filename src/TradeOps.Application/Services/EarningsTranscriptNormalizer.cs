using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradeOps.Application.Models;

namespace TradeOps.Application.Services;

public static class EarningsTranscriptNormalizer
{
    public const string DocumentIdPrefix = "transcript:";
    public const string SegmentIdPrefix = "segment:";

    public static EarningsTranscriptDocument Normalize(
        RawEarningsTranscriptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var provider =
            NormalizeRequired(
                input.Provider,
                nameof(input.Provider));
        var sourceDocumentId =
            NormalizeRequired(
                input.SourceDocumentId,
                nameof(input.SourceDocumentId));
        var issuerId =
            NormalizeOptional(
                input.IssuerId);
        var fiscalPeriod =
            NormalizeRequired(
                input.FiscalPeriod,
                nameof(input.FiscalPeriod));
        var title =
            NormalizeRequired(
                input.Title,
                nameof(input.Title));
        var extractionMethod =
            NormalizeRequired(
                input.ExtractionMethod,
                nameof(input.ExtractionMethod));
        var sourceUri =
            ValidateSourceUri(
                input.SourceUri);
        var instrument =
            NormalizeInstrument(
                input.Instrument);

        var sourceTimestamp =
            NormalizeTimestamp(
                input.SourceTimestamp,
                nameof(input.SourceTimestamp));
        var publishedAt =
            NormalizeTimestamp(
                input.PublishedAt,
                nameof(input.PublishedAt));
        var retrievedAt =
            NormalizeTimestamp(
                input.RetrievedAt,
                nameof(input.RetrievedAt));

        ValidateTimestampOrder(
            sourceTimestamp,
            publishedAt,
            retrievedAt);

        var participants =
            NormalizeParticipants(
                input.Participants);
        var documentId =
            ComputeDocumentId(
                provider,
                sourceDocumentId,
                issuerId,
                instrument,
                fiscalPeriod);
        var segments =
            NormalizeSegments(
                documentId,
                input.Segments,
                participants);

        var provenance =
            new ResearchSourceProvenance(
                provider,
                sourceUri,
                sourceTimestamp,
                retrievedAt,
                extractionMethod,
                sourceDocumentId,
                issuerId);

        var document =
            new EarningsTranscriptDocument(
                documentId,
                instrument,
                fiscalPeriod,
                title,
                publishedAt,
                provenance,
                participants,
                segments,
                string.Empty);

        return document with
        {
            Fingerprint =
                ComputeFingerprint(
                    document)
        };
    }

    public static void ValidateNormalizedDocument(
        EarningsTranscriptDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        if (document.Instrument is null)
        {
            throw new ArgumentException(
                "Instrument is required.",
                nameof(document));
        }

        if (document.Provenance is null)
        {
            throw new ArgumentException(
                "Provenance is required.",
                nameof(document));
        }

        if (document.Participants is null)
        {
            throw new ArgumentException(
                "Participants are required.",
                nameof(document));
        }

        if (document.Segments is null)
        {
            throw new ArgumentException(
                "Segments are required.",
                nameof(document));
        }

        var provider =
            NormalizeRequired(
                document.Provenance.Provider,
                nameof(document.Provenance.Provider));
        var sourceDocumentId =
            NormalizeRequired(
                document.Provenance.SourceDocumentId,
                nameof(document.Provenance.SourceDocumentId));
        var issuerId =
            NormalizeOptional(
                document.Provenance.IssuerId);
        var fiscalPeriod =
            NormalizeRequired(
                document.FiscalPeriod,
                nameof(document.FiscalPeriod));
        var title =
            NormalizeRequired(
                document.Title,
                nameof(document.Title));
        var extractionMethod =
            NormalizeRequired(
                document.Provenance.ExtractionMethod,
                nameof(document.Provenance.ExtractionMethod));
        var sourceUri =
            ValidateSourceUri(
                document.Provenance.SourceUri);
        var instrument =
            NormalizeInstrument(
                document.Instrument);
        var sourceTimestamp =
            NormalizeTimestamp(
                document.Provenance.SourceTimestamp,
                nameof(document.Provenance.SourceTimestamp));
        var publishedAt =
            NormalizeTimestamp(
                document.PublishedAt,
                nameof(document.PublishedAt));
        var retrievedAt =
            NormalizeTimestamp(
                document.Provenance.RetrievedAt,
                nameof(document.Provenance.RetrievedAt));

        ValidateTimestampOrder(
            sourceTimestamp,
            publishedAt,
            retrievedAt);

        if (document.Instrument != instrument ||
            document.FiscalPeriod != fiscalPeriod ||
            document.Title != title ||
            document.PublishedAt.Offset != TimeSpan.Zero ||
            document.Provenance.Provider != provider ||
            document.Provenance.SourceDocumentId != sourceDocumentId ||
            document.Provenance.IssuerId != issuerId ||
            document.Provenance.ExtractionMethod != extractionMethod ||
            document.Provenance.SourceUri != sourceUri ||
            document.Provenance.SourceTimestamp.Offset !=
                TimeSpan.Zero ||
            document.Provenance.RetrievedAt.Offset !=
                TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Normalized transcript document contains non-canonical metadata.",
                nameof(document));
        }

        var normalizedParticipants =
            NormalizeParticipants(
                document.Participants
                    .Select(
                        item =>
                            new RawTranscriptParticipant(
                                item.ParticipantId,
                                item.DisplayName,
                                item.Role,
                                item.Organization))
                    .ToArray());

        if (!document.Participants.SequenceEqual(
                normalizedParticipants))
        {
            throw new ArgumentException(
                "Normalized transcript participants are not canonical.",
                nameof(document));
        }

        var expectedDocumentId =
            ComputeDocumentId(
                provider,
                sourceDocumentId,
                issuerId,
                instrument,
                fiscalPeriod);

        if (!string.Equals(
                document.DocumentId,
                expectedDocumentId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "DocumentId does not match normalized document identity.",
                nameof(document));
        }

        ValidateNormalizedSegments(
            document,
            normalizedParticipants);

        var expectedFingerprint =
            ComputeFingerprint(
                document);

        if (!IsLowercaseSha256(
                document.Fingerprint) ||
            !string.Equals(
                document.Fingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Fingerprint does not match normalized transcript content.",
                nameof(document));
        }
    }

    public static string ComputeFingerprint(
        EarningsTranscriptDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var builder =
            new StringBuilder();

        AppendCanonical(
            builder,
            document.DocumentId);
        AppendInstrument(
            builder,
            document.Instrument);
        AppendCanonical(
            builder,
            document.FiscalPeriod);
        AppendCanonical(
            builder,
            document.Title);
        AppendCanonical(
            builder,
            FormatTimestamp(
                document.PublishedAt));
        AppendCanonical(
            builder,
            document.Provenance.Provider);
        AppendCanonical(
            builder,
            document.Provenance.SourceUri.AbsoluteUri);
        AppendCanonical(
            builder,
            FormatTimestamp(
                document.Provenance.SourceTimestamp));
        AppendCanonical(
            builder,
            FormatTimestamp(
                document.Provenance.RetrievedAt));
        AppendCanonical(
            builder,
            document.Provenance.ExtractionMethod);
        AppendCanonical(
            builder,
            document.Provenance.SourceDocumentId);
        AppendCanonical(
            builder,
            document.Provenance.IssuerId);

        foreach (var participant in
                 document.Participants)
        {
            AppendCanonical(
                builder,
                participant.ParticipantId);
            AppendCanonical(
                builder,
                participant.DisplayName);
            AppendCanonical(
                builder,
                ((int)participant.Role)
                    .ToString(
                        CultureInfo.InvariantCulture));
            AppendCanonical(
                builder,
                participant.Organization);
        }

        foreach (var segment in
                 document.Segments)
        {
            AppendCanonical(
                builder,
                segment.SegmentId);
            AppendCanonical(
                builder,
                segment.Sequence
                    .ToString(
                        CultureInfo.InvariantCulture));
            AppendCanonical(
                builder,
                segment.ParticipantId);
            AppendCanonical(
                builder,
                segment.Text);
        }

        return Sha256(
            builder.ToString());
    }

    private static InstrumentReference NormalizeInstrument(
        InstrumentReference instrument)
    {
        if (instrument is null)
        {
            throw new ArgumentException(
                "Instrument is required.",
                nameof(instrument));
        }

        var symbol =
            NormalizeRequired(
                instrument.Symbol,
                nameof(instrument.Symbol))
                .ToUpperInvariant();
        var currency =
            NormalizeOptional(
                instrument.Currency)
                ?.ToUpperInvariant();
        var venueInstrumentId =
            NormalizeOptional(
                instrument.VenueInstrumentId);
        var exchange =
            NormalizeOptional(
                instrument.Exchange);

        return instrument with
        {
            Symbol = symbol,
            Currency = currency,
            VenueInstrumentId =
                venueInstrumentId,
            Exchange = exchange
        };
    }

    private static IReadOnlyList<TranscriptParticipant>
        NormalizeParticipants(
            IReadOnlyList<RawTranscriptParticipant>
                participants)
    {
        if (participants is null ||
            participants.Count == 0)
        {
            throw new ArgumentException(
                "At least one transcript participant is required.",
                nameof(participants));
        }

        var normalized =
            new List<TranscriptParticipant>(
                participants.Count);
        var ids =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var participant in
                 participants)
        {
            if (participant is null)
            {
                throw new ArgumentException(
                    "Transcript participant cannot be null.",
                    nameof(participants));
            }

            var participantId =
                NormalizeRequired(
                    participant.ParticipantId,
                    nameof(participant.ParticipantId));
            var displayName =
                NormalizeRequired(
                    participant.DisplayName,
                    nameof(participant.DisplayName));
            var organization =
                NormalizeOptional(
                    participant.Organization);

            if (!Enum.IsDefined(
                    participant.Role))
            {
                throw new ArgumentException(
                    "Transcript participant role is invalid.",
                    nameof(participants));
            }

            if (!ids.Add(
                    participantId))
            {
                throw new ArgumentException(
                    $"Duplicate transcript participant id '{participantId}'.",
                    nameof(participants));
            }

            normalized.Add(
                new TranscriptParticipant(
                    participantId,
                    displayName,
                    participant.Role,
                    organization));
        }

        return normalized
            .OrderBy(
                item =>
                    item.ParticipantId,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<TranscriptSegment>
        NormalizeSegments(
            string documentId,
            IReadOnlyList<RawTranscriptSegment>
                segments,
            IReadOnlyList<TranscriptParticipant>
                participants)
    {
        if (segments is null ||
            segments.Count == 0)
        {
            throw new ArgumentException(
                "Transcript must contain at least one segment.",
                nameof(segments));
        }

        var participantIds =
            participants
                .Select(
                    item =>
                        item.ParticipantId)
                .ToHashSet(
                    StringComparer.Ordinal);
        var sequences =
            new HashSet<int>();
        var normalized =
            new List<TranscriptSegment>(
                segments.Count);

        foreach (var segment in
                 segments)
        {
            if (segment is null)
            {
                throw new ArgumentException(
                    "Transcript segment cannot be null.",
                    nameof(segments));
            }

            if (segment.Sequence <= 0)
            {
                throw new ArgumentException(
                    "Transcript segment sequence must be greater than zero.",
                    nameof(segments));
            }

            if (!sequences.Add(
                    segment.Sequence))
            {
                throw new ArgumentException(
                    $"Duplicate transcript segment sequence '{segment.Sequence}'.",
                    nameof(segments));
            }

            var participantId =
                NormalizeRequired(
                    segment.ParticipantId,
                    nameof(segment.ParticipantId));

            if (!participantIds.Contains(
                    participantId))
            {
                throw new ArgumentException(
                    $"Transcript segment sequence {segment.Sequence} references unknown participant '{participantId}'.",
                    nameof(segments));
            }

            var text =
                NormalizeTranscriptText(
                    segment.Text);

            if (string.IsNullOrWhiteSpace(
                    text))
            {
                throw new ArgumentException(
                    $"Transcript segment sequence {segment.Sequence} must contain meaningful text.",
                    nameof(segments));
            }

            normalized.Add(
                new TranscriptSegment(
                    ComputeSegmentId(
                        documentId,
                        segment.Sequence,
                        participantId,
                        text),
                    segment.Sequence,
                    participantId,
                    text));
        }

        return normalized
            .OrderBy(
                item =>
                    item.Sequence)
            .ToArray();
    }

    private static void ValidateNormalizedSegments(
        EarningsTranscriptDocument document,
        IReadOnlyList<TranscriptParticipant>
            participants)
    {
        if (document.Segments.Count == 0)
        {
            throw new ArgumentException(
                "Transcript must contain at least one segment.",
                nameof(document));
        }

        var participantIds =
            participants
                .Select(
                    item =>
                        item.ParticipantId)
                .ToHashSet(
                    StringComparer.Ordinal);
        var sequences =
            new HashSet<int>();
        var priorSequence = 0;

        foreach (var segment in
                 document.Segments)
        {
            if (segment is null ||
                segment.Sequence <= 0 ||
                !sequences.Add(
                    segment.Sequence))
            {
                throw new ArgumentException(
                    "Normalized transcript contains an invalid or duplicate segment sequence.",
                    nameof(document));
            }

            if (segment.Sequence <=
                priorSequence)
            {
                throw new ArgumentException(
                    "Normalized transcript segments must be ordered by sequence.",
                    nameof(document));
            }

            priorSequence =
                segment.Sequence;

            var participantId =
                NormalizeRequired(
                    segment.ParticipantId,
                    nameof(segment.ParticipantId));

            if (!participantIds.Contains(
                    participantId) ||
                participantId !=
                    segment.ParticipantId)
            {
                throw new ArgumentException(
                    "Normalized transcript segment references an invalid participant.",
                    nameof(document));
            }

            var text =
                NormalizeTranscriptText(
                    segment.Text);

            if (string.IsNullOrWhiteSpace(
                    text) ||
                text !=
                    segment.Text)
            {
                throw new ArgumentException(
                    "Normalized transcript segment text is not canonical.",
                    nameof(document));
            }

            var expectedSegmentId =
                ComputeSegmentId(
                    document.DocumentId,
                    segment.Sequence,
                    participantId,
                    text);

            if (!string.Equals(
                    segment.SegmentId,
                    expectedSegmentId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "SegmentId does not match normalized segment content.",
                    nameof(document));
            }
        }
    }

    private static string ComputeDocumentId(
        string provider,
        string sourceDocumentId,
        string? issuerId,
        InstrumentReference instrument,
        string fiscalPeriod)
    {
        var builder =
            new StringBuilder();

        AppendCanonical(
            builder,
            provider);
        AppendCanonical(
            builder,
            sourceDocumentId);
        AppendCanonical(
            builder,
            issuerId);
        AppendInstrument(
            builder,
            instrument);
        AppendCanonical(
            builder,
            fiscalPeriod);

        return DocumentIdPrefix +
            Sha256(
                builder.ToString());
    }

    private static string ComputeSegmentId(
        string documentId,
        int sequence,
        string participantId,
        string text)
    {
        var builder =
            new StringBuilder();

        AppendCanonical(
            builder,
            documentId);
        AppendCanonical(
            builder,
            sequence
                .ToString(
                    CultureInfo.InvariantCulture));
        AppendCanonical(
            builder,
            participantId);
        AppendCanonical(
            builder,
            Sha256(
                text));

        return SegmentIdPrefix +
            Sha256(
                builder.ToString());
    }

    private static Uri ValidateSourceUri(
        Uri sourceUri)
    {
        if (sourceUri is null ||
            !sourceUri.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "SourceUri must be an absolute URI.",
                nameof(sourceUri));
        }

        return sourceUri;
    }

    private static DateTimeOffset NormalizeTimestamp(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException(
                $"{parameterName} is required.",
                parameterName);
        }

        return value.ToUniversalTime();
    }

    private static void ValidateTimestampOrder(
        DateTimeOffset sourceTimestamp,
        DateTimeOffset publishedAt,
        DateTimeOffset retrievedAt)
    {
        if (publishedAt <
            sourceTimestamp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publishedAt),
                "PublishedAt must be greater than or equal to SourceTimestamp.");
        }

        if (retrievedAt <
            publishedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retrievedAt),
                "RetrievedAt must be greater than or equal to PublishedAt.");
        }
    }

    private static string NormalizeRequired(
        string? value,
        string parameterName)
    {
        var normalized =
            NormalizeOptional(
                value);

        if (normalized is null)
        {
            throw new ArgumentException(
                $"{parameterName} must be non-empty.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeTranscriptText(
        string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var lines =
            value
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    '\r',
                    '\n')
                .Split(
                    '\n')
                .Select(
                    line =>
                        line.Trim())
                .ToArray();

        var first = 0;
        var last =
            lines.Length - 1;

        while (first <= last &&
               lines[first].Length == 0)
        {
            first++;
        }

        while (last >= first &&
               lines[last].Length == 0)
        {
            last--;
        }

        return first > last
            ? string.Empty
            : string.Join(
                '\n',
                lines[first..(last + 1)]);
    }

    private static void AppendInstrument(
        StringBuilder builder,
        InstrumentReference instrument)
    {
        AppendCanonical(
            builder,
            instrument.Symbol);
        AppendCanonical(
            builder,
            ((int)instrument.AssetClass)
                .ToString(
                    CultureInfo.InvariantCulture));
        AppendCanonical(
            builder,
            instrument.Currency);
        AppendCanonical(
            builder,
            instrument.VenueInstrumentId);
        AppendCanonical(
            builder,
            instrument.Exchange);
    }

    private static void AppendCanonical(
        StringBuilder builder,
        string? value)
    {
        if (value is null)
        {
            builder.Append(
                "-1:|");
            return;
        }

        builder
            .Append(
                value.Length
                    .ToString(
                        CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('|');
    }

    private static string FormatTimestamp(
        DateTimeOffset value) =>
        value
            .ToUniversalTime()
            .ToString(
                "O",
                CultureInfo.InvariantCulture);

    private static string Sha256(
        string value)
    {
        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    value));

        return Convert
            .ToHexString(
                hash)
            .ToLowerInvariant();
    }

    private static bool IsLowercaseSha256(
        string? value) =>
        value is { Length: 64 } &&
        value.All(
            character =>
                character is
                    >= '0' and <= '9'
                    or >= 'a' and <= 'f');
}

public static class EarningsTranscriptJson
{
    private static readonly JsonSerializerOptions
        Options =
            CreateOptions();

    public static string SerializeNormalized(
        EarningsTranscriptDocument document)
    {
        EarningsTranscriptNormalizer
            .ValidateNormalizedDocument(
                document);

        return JsonSerializer.Serialize(
            document,
            Options);
    }

    public static EarningsTranscriptDocument
        DeserializeNormalized(
            string json)
    {
        var document =
            Deserialize<EarningsTranscriptDocument>(
                json,
                "Normalized transcript JSON");

        EarningsTranscriptNormalizer
            .ValidateNormalizedDocument(
                document);

        return document;
    }

    public static RawEarningsTranscriptInput
        DeserializeRaw(
            string json) =>
        Deserialize<RawEarningsTranscriptInput>(
            json,
            "Raw transcript JSON");

    private static T Deserialize<T>(
        string json,
        string description)
    {
        if (string.IsNullOrWhiteSpace(
                json))
        {
            throw new ArgumentException(
                $"{description} is required.",
                nameof(json));
        }

        try
        {
            return JsonSerializer
                .Deserialize<T>(
                    json,
                    Options)
                ?? throw new ArgumentException(
                    $"{description} must contain a JSON object.",
                    nameof(json));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"{description} does not match the expected schema.",
                nameof(json),
                exception);
        }
    }

    private static JsonSerializerOptions
        CreateOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive =
                    false,
                AllowTrailingCommas =
                    false,
                ReadCommentHandling =
                    JsonCommentHandling.Disallow,
                UnmappedMemberHandling =
                    JsonUnmappedMemberHandling.Disallow,
                WriteIndented =
                    true
            };

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }
}
