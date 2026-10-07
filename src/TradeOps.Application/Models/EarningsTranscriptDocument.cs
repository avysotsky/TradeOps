namespace TradeOps.Application.Models;

public enum TranscriptParticipantRole
{
    Unknown = 0,
    Executive = 1,
    Analyst = 2,
    Operator = 3
}

public sealed record TranscriptParticipant(
    string ParticipantId,
    string DisplayName,
    TranscriptParticipantRole Role,
    string? Organization = null);

public sealed record TranscriptSegment(
    string SegmentId,
    int Sequence,
    string ParticipantId,
    string Text);

public sealed record EarningsTranscriptDocument(
    string DocumentId,
    InstrumentReference Instrument,
    string FiscalPeriod,
    string Title,
    DateTimeOffset PublishedAt,
    ResearchSourceProvenance Provenance,
    IReadOnlyList<TranscriptParticipant> Participants,
    IReadOnlyList<TranscriptSegment> Segments,
    string Fingerprint);

public sealed record RawTranscriptParticipant(
    string ParticipantId,
    string DisplayName,
    TranscriptParticipantRole Role = TranscriptParticipantRole.Unknown,
    string? Organization = null);

public sealed record RawTranscriptSegment(
    int Sequence,
    string ParticipantId,
    string Text);

public sealed record RawEarningsTranscriptInput(
    string Provider,
    Uri SourceUri,
    string SourceDocumentId,
    string? IssuerId,
    InstrumentReference Instrument,
    string FiscalPeriod,
    string Title,
    DateTimeOffset SourceTimestamp,
    DateTimeOffset PublishedAt,
    DateTimeOffset RetrievedAt,
    IReadOnlyList<RawTranscriptParticipant> Participants,
    IReadOnlyList<RawTranscriptSegment> Segments,
    string ExtractionMethod = "transcript-intake-v1");
