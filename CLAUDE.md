# Ironbell

Offline-first kettlebell training PWA. Opinionated training tool, not a generic
sets-and-reps logger.

## Planning docs (read in this order)

v3 is the current source of truth for hosting and the data layer. Where v1/v2 disagree with
v3, v3 wins.

@docs/ironbell-plan-v3-azure.md
@docs/ironbell-plan-v2.md
@docs/ironbell-gap-triage.md
@docs/ironbell-plan.md

## Stack

- **.NET 10**
- **Client:** Blazor WebAssembly, **standalone** (not a Blazor Web App host) — required for
  offline training mode. PWA, installable, service worker.
- **API:** ASP.NET Core **Minimal API**.
- **Database:** **Azure SQL Database (free offer)** — SQL Server. Free indefinitely, not a
  12-month trial.
- **Data access:** **EF Core only.** No Dapper. The layer is deliberately
  **provider-portable** for a later switch to PostgreSQL.
- **Logging:** Serilog, both sides, correlation id flowing client → API → log.
- **Mediator:** hand-rolled `IHandler<,>` + pipeline behaviours. **Not MediatR** (commercial
  licence since 2025).

## Architecture

- **Vertical Slice Architecture.** Each feature is a self-contained slice.
- **No cross-slice handler references** — a slice never calls another slice's handler.
  Shared logic goes in `Ironbell.Domain`. **Enforced by a NetArchTest** that fails the build.
- Projects: `Ironbell.Api` (host + slices), `Ironbell.Client` (WASM PWA),
  `Ironbell.Domain` (pure C#: timeline resolver, tonnage, PR rules — no framework deps),
  `Ironbell.Infrastructure` (EF Core).

## Data-layer rules (ADR 0001 — keep it portable to PostgreSQL)

- No Dapper / no raw provider-specific SQL.
- No `rowversion` / `xmin` — use an **`int` concurrency token**.
- No array columns — model many-values as child rows.
- JSON stays **opaque** (stored/read as a string; no provider-specific JSON querying).
  `plan_snapshot` is a serialized blob.
- **UTC `DateTime`**, never `DateTimeOffset`.
- **snake_case** table and column naming (PostgreSQL convention, kept deliberately).
- Uniqueness/lookups use **normalised lowercase columns** — do not rely on DB collation
  (SQL Server is case-insensitive, Postgres is case-sensitive).
- **CI runs the full slice suite against a PostgreSQL Testcontainer from M0**, even though
  production is SQL Server. This is what actually keeps portability real.

## Domain

Nine block types, not a generic abstraction: Straight, Circuit, EMOM, AMRAP, Ladder,
Complex, Chain, Interval, ForTime. Sessions carry a **`plan_snapshot`** so editing a plan
never retroactively changes logged history.

## Training mode (highest-risk component)

- Timer is **JavaScript-owned, anchored to absolute wall-clock time** (no drift on sleep).
- **Wake Lock** during a session.
- Audio cues **synthesised via `OscillatorNode`** (Web Audio) — no audio files. Unlock on
  the Start tap.
- Accuracy gate: **±250 ms** vs wall clock **with the screen locked**.

## Deployment

- Image → **GHCR** (public), built + pushed by GitHub Actions on merge to `main`.
- **Azure Container Apps**, Consumption, scale-to-zero through M0–M6.
- **Client served from the API container** (same origin — refresh cookie stays first-party).
- Container Apps **managed certificate** + custom domain.
- Migrations applied **as a pipeline step, never on app startup**.
- Reminders: **daily RRULE materialiser → Azure Storage Queue** with visibility timeouts.
  Never a DB poller (drains the serverless free allowance).

## Product philosophy (anti-gamification)

Discipline over dopamine. Tonnage is the headline metric. 28-day density grid, not streaks.
Dated PRs, not badges. Terse coach-voice copy. Competition kettlebell weight colour code as a
functional accent system. Signature "Bell Stack" element reused across session view,
calendar, and share image. Dark iron base `#17171A`; Archivo + Public Sans. No "AI slop"
aesthetics.

## Current state

**Milestone M0 — walking skeleton, deployed to phone.** See v3/v2 for the M0 build list and
"done when" check.

## Definition of done (every milestone)

1. Merged to `main`, auto-deployed to production.
2. Manual check passes **on a physical phone**, not a simulator.
3. All tests green, including architecture tests.
4. WASM bundle under budget (build fails otherwise).
5. No new Tier-A item deferred without writing down why.
