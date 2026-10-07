# VS-03 — Configurable Research Policy

## State

READY_FOR_INTEGRATION

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs03-configurable-research-policy
```

## Baseline

```text
cfa5c315772465a839b1a095675afd009e7e0dad
```

## Goal

Provide a bounded, deterministic JSON configuration layer for the existing earnings research policy without introducing a strategy engine, scripting DSL, LLM path, broker integration or alternate research/portfolio contracts.

Configured flow:

```text
client-supplied JSON policy
-> strict deterministic validation
-> EarningsResearchPolicyDefinition v1
-> existing EarningsDecisionRuleSettings
-> existing EarningsTargetWeightPolicy
-> existing DeterministicEarningsDecisionRule
-> existing ResearchDecision / rebalance pipeline
```

## Implemented

Added a versioned configuration contract:

```text
EarningsResearchPolicyDefinition
  SchemaVersion
  StrategyId
  Thresholds
    RevenueGrowth
    DilutedEpsGrowth
    OperatingMarginDelta
    MinimumDirectionalSignals
  TargetWeights
    Positive
    Neutral
    Negative
```

The loader uses `System.Text.Json` and is fail closed.

Supported schema:

```text
SchemaVersion = 1
```

Unknown JSON properties are rejected. Property names are case-sensitive. Duplicate properties are rejected. Comments and trailing commas are not accepted.

Invalid input is returned through `EarningsResearchPolicyValidationResult` / `EarningsResearchPolicyValidationError`; raw `JsonException` is not the user-facing contract.

Validation covers:

- supported schema version;
- non-empty StrategyId;
- required thresholds and target weights;
- thresholds >= 0;
- MinimumDirectionalSignals in 1..3;
- target weights in 0..1;
- target-weight decimal scale <= 8;
- malformed JSON;
- unknown and duplicate properties.

No invalid values are silently corrected.

## Existing production types reused

A validated definition converts directly to the existing:

```text
EarningsDecisionRuleSettings
EarningsTargetWeightPolicy
```

No alternate settings or target-weight policy type is used by the production rule.

The existing semantics of `DeterministicEarningsDecisionRule` are unchanged.

## Fingerprint

A validated policy has a deterministic SHA-256 fingerprint.

Fingerprint input is a canonical fixed-field representation using:

- supported schema version;
- trimmed StrategyId;
- all threshold values;
- MinimumDirectionalSignals;
- all target weights;
- invariant decimal formatting with insignificant trailing zeroes removed.

The resulting fingerprint is 64 lowercase hexadecimal characters.

It is independent of JSON whitespace and input property order. Equivalent decimal forms such as `0.40` and `0.4000` normalize to the same fingerprint.

## Sample config

```text
samples/research/earnings-policy.sample.json
```

Sample values demonstrate configuration mechanics only. They are not represented as profitable, optimized or production-recommended strategy parameters.

## Tests

Added deterministic unit coverage for:

- valid JSON -> existing `EarningsDecisionRuleSettings`;
- valid JSON -> existing `EarningsTargetWeightPolicy`;
- unsupported SchemaVersion;
- empty StrategyId;
- negative thresholds;
- MinimumDirectionalSignals outside 1..3;
- target weight below 0;
- target weight above 1;
- excessive target-weight precision;
- malformed JSON;
- unknown property rejection;
- duplicate property rejection;
- equivalent JSON with different whitespace/property order -> same fingerprint;
- serialization round trip;
- composition:
  `JSON policy -> existing settings/policy -> existing DeterministicEarningsDecisionRule -> ResearchDecision(SetTargetWeight)`.

## Scope boundary

This slice does not modify or add:

- SEC HTTP acquisition;
- Alpha Vantage;
- market-data ingestion;
- public-data cache;
- `tools/TradeOps.PublicResearchDemo/**`;
- IBKR or broker APIs;
- order placement;
- backtester implementation;
- portfolio planner implementation;
- transcript ingestion;
- LLM functionality;
- strategy optimization.

## Completion record

State:

```text
READY_FOR_INTEGRATION
```

Baseline:

```text
cfa5c315772465a839b1a095675afd009e7e0dad
```

Exact implementation HEAD before this status-file commit:

```text
51eb45f1932c03933a68721f8167deb6d7329d3c
```

Changed files:

```text
src/TradeOps.Application/Models/EarningsResearchPolicyDefinition.cs
src/TradeOps.Application/Services/EarningsResearchPolicyConfiguration.cs
tests/TradeOps.UnitTests/EarningsResearchPolicyConfigurationTests.cs
samples/research/earnings-policy.sample.json
docs/orchestration/VS03_POLICY_CONFIG.md
```

Public/shared contracts changed:

```text
No.

Frozen contracts unchanged:
InstrumentReference v1
ResearchDecision v1
ResearchDecisionAction v1
ResearchDecisionValidationResult v1

Existing EarningsDecisionRuleSettings and EarningsTargetWeightPolicy semantics unchanged.
```

Tests:

```text
Added VS-03 deterministic unit + composition coverage.
Build: success
Unit tests: success
All existing full-workflow validation stages: success
```

Full CI:

```text
GitHub Actions run: 37594141117
Conclusion: success
Validated branch HEAD at run start: cf4adddd22ecec5ea04dc9f4a186cb998488deb6
```

Sample config path:

```text
samples/research/earnings-policy.sample.json
```

Validation behavior:

```text
strict / deterministic / fail closed
unknown properties rejected
duplicate properties rejected
malformed JSON -> structured validation error
no silent correction
```

Fingerprint behavior:

```text
SHA-256 over normalized logical definition
64 lowercase hex characters
independent of whitespace/property order
```

Blockers:

```text
None in the bounded VS-03 implementation.
The branch baseline is intentionally the assigned cfa5c315772465a839b1a095675afd009e7e0dad; current main has advanced independently, so synchronization/integration remains an Orchestrator decision.
```

Next integration action:

```text
Development Orchestrator should review the bounded diff and current-main compatibility, then integrate if still green.
Do not integrate this configuration into VS-02 tooling from this branch.
```
