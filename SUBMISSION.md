# SUBMISSION.md — AI Diff Review Service

## Architecture (overview)

ASP.NET Core 10 Web API, all state in-memory (no database — the service is ephemeral for the
grading window). Request flow:

1. `POST /v1/reviews` reads the raw body (manual read, so we control 400/413 and can hash the body),
   validates (413 / 400 / 422), applies `Idempotency-Key` and result-cache checks, creates a `Job`,
   enqueues the job id on an unbounded `Channel`, and returns `202 queued`.
2. A `BackgroundService` (`JobProcessor`) runs **4 worker loops** consuming the channel — this gives
   bounded concurrency (≥4 concurrent) and lets a queued 5th job wait rather than fail.
3. Each worker chunks the diff on file boundaries (≤64 KiB), runs the selected provider per chunk,
   merges findings, and **normalizes** them globally: dedup by `id`, order by `path → line → ruleId`
   (Ordinal, so it's deterministic across machines), truncate to `maxFindings`.
4. Results and an ordered SSE event log are written back to the in-memory job.
5. `GET /v1/reviews/{id}` polls status; `GET /v1/reviews/{id}/stream` replays the event log then tails.

Cross-cutting: static bearer-token middleware guards all `/v1/*` routes; a single error-envelope
type is emitted from both middleware and controllers; a fixed-window rate limiter (30/min) applies to
`POST` only and returns `429 + Retry-After`; `/spec` limits come from one `ServiceLimits` constants
class that every enforcement point also reads, so declared limits cannot drift from actual behavior.

## Provider design

`IReviewProvider { Name; ReviewAsync(chunk, ct) }`, resolved by name via DI.

- **mock** — deterministic implementation of the rule table. Single-line rules are data-driven
  (metadata + a predicate); the empty-catch rule (`MOCK-004`) scans a small multi-line window.
  Findings carry `id = ruleId:path:line`. Prompt-injection content is matched as inert text and can
  never alter behavior. Global ordering/dedup/truncation happens in the pipeline, not the provider,
  which is exactly why chunked output is identical to an unchunked scan.
- **llm** — calls an OpenAI-compatible endpoint (configured server-side via `Llm:Endpoint`,
  `Llm:ApiKey`, `Llm:Model`; verified working against Groq). The diff is passed as untrusted data and
  the prompt instructs the model to ignore instructions inside it. Any failure (unconfigured,
  unreachable, bad response) throws, and the worker marks the job `failed` with a clear error —
  it never crashes the service.

## How the cross-cutting behaviors were verified

- **Chunking** — xUnit test asserts `Normalize(scan(wholeDiff)) == Normalize(merge(scan(each chunk)))`
  over a >64 KiB multi-file diff (identical ids, identical order); plus chunk-size-limit and
  single-oversized-file tests.
- **Ordering / dedup / truncation** — `FindingSet` tests for dedup by id, `path → line → ruleId`
  ordering (including the ruleId tiebreak and Ordinal casing), and truncation after ordering.
- **Diff parsing** — tests that removed lines don't shift new-file line numbers and that the `+++`
  header is never emitted as a finding.
- **Mock rules** — every rule has a positive test plus negative near-miss tests (e.g. `=== null` must
  not fire `MOCK-005`; short credential must not fire `MOCK-002`).
- **Idempotency / caching** — manual: same key + same body → same jobId; same key + different body →
  409; identical body resubmitted → `cacheHit: true` with identical findings.
- **SSE replay** — manual: streaming a finished job replays `status` → `finding` → `done` identically.
- 24 automated tests passing.

## AI tools used

- **Claude (Cowork mode)** as a pair-programmer / tutor throughout — I wrote and reviewed every
  component and can explain each design decision.
- **Groq (Llama 3.1)** as the runtime model behind the `llm` provider.

## An AI suggestion I rejected, and why

An assistant proposed an `ISystemMonitorService` computing uptime from `Environment.TickCount64`.
I rejected it: `TickCount64` measures **OS** uptime, not the **service** process uptime the contract
wants, and the suggested response shape didn't match `/health`. I captured a boot `DateTimeOffset` at
startup instead. (Separately, one of my own negative tests caught a real bug — `Contains("== null")`
also matched `=== null` — which I fixed with a lookaround regex.)

## What I'd do next with more time

- Persist jobs (e.g. Redis) so a host restart doesn't lose in-flight work.
- Per-client rate limiting (partition by token) instead of a single global window.
- Stream findings from the provider as they're discovered rather than after the full scan.
- Stricter LLM output schema validation and retry/backoff on transient model errors.
