# Ironbell — Architecture Plan (v1)

> Kettlebell training PWA. This is the original architecture document.
> The delivery-sequencing section here (§11) is **superseded by `ironbell-plan-v2.md`**,
> which reorganises the work from layers into testable milestones. Everything else in
> this document still stands.

---

## 1. What Ironbell is

A genuinely polished, motivating kettlebell training tool — not a generic sets-and-reps
logger. Strong opinions baked in from the start, both in domain modelling and in product
design philosophy. Offline-first: you must be able to train an entire session with no
network.

---

## 2. Stack

- **Client:** Blazor WebAssembly, **standalone** (not a Blazor Web App Server/Auto host).
  Standalone WASM is chosen specifically to support offline training mode — the client is a
  static PWA that keeps working with the network cut.
- **Backend:** ASP.NET Core **Minimal API**.
- **Database:** PostgreSQL 16+.
- **Data access:** EF Core 10 for writes and normal reads; **Dapper** for analytical read
  models (tonnage, trends, PR queries). *(Superseded by `ironbell-plan-v3-azure.md`: Dapper
  is dropped — **EF Core only** — to keep the layer provider-portable between Azure SQL now
  and PostgreSQL later.)*
- **Logging:** Serilog, both client and server, with a correlation id flowing
  client → API → log.
- **Architecture:** Vertical Slice Architecture (VSA).
- **Runtime:** .NET 10.

### Mediator

MediatR changed to a commercial licence in 2025, so it's replaced with a **hand-rolled
`IHandler<,>`** and a small pipeline-behaviour chain. This keeps the request/handler
ergonomics of VSA without the licensing dependency.

---

## 3. Vertical Slice Architecture

- Each feature is a self-contained slice: request, handler, validation, endpoint, and the
  read/write it needs, colocated.
- **Strict rule: no cross-slice handler references.** A slice never calls another slice's
  handler. Shared logic lives in the domain project, not in a neighbouring slice.
- This rule is **enforced by a NetArchTest architecture test** that fails the build on any
  cross-slice handler reference — it isn't a convention people are trusted to remember.

### Projects

- `Ironbell.Api` — Minimal API host; feature slices as folders.
- `Ironbell.Client` — Blazor WASM standalone PWA.
- `Ironbell.Domain` — pure C#: timeline resolver, tonnage, PR rules. No framework
  dependencies. This is where the bugs actually live, so it's the most heavily tested.
- `Ironbell.Infrastructure` — EF Core, Npgsql, Dapper read models.

---

## 4. Domain model

The domain is modelled around **kettlebell-specific block types**, not a generic
sets-and-reps abstraction. Nine block types:

1. **Straight** — fixed sets × reps.
2. **Circuit** — ordered exercises, rounds.
3. **EMOM** — every minute on the minute.
4. **AMRAP** — as many reps/rounds as possible in a window.
5. **Ladder** — ascending/descending rep schemes.
6. **Complex** — multiple movements, same bell, no rest between.
7. **Chain** — sequenced movements across a set.
8. **Interval** — work/rest timed intervals.
9. **ForTime** — fixed work, race the clock.

### `plan_snapshot`

Sessions carry a **`plan_snapshot` jsonb column**. When a plan is edited, previously logged
sessions must not retroactively change — the snapshot freezes exactly what the plan looked
like at the moment the session was trained. This prevents retroactive data corruption of
history.

---

## 5. Auth & tokens

- ASP.NET Core **Identity**.
- **JWT** access tokens.
- **Rotating opaque refresh tokens** with **family reuse detection**: presenting a rotated
  (already-used) refresh token revokes the entire token family.
- Ownership authorisation baked into the handler template so it's the default, not a
  remembered step.
- Rate limiting on auth endpoints.
- A test asserts that Serilog output for `/auth/login` contains **no password or token
  substring**.

---

## 6. Timer engine

- **JavaScript-owned**, not driven from .NET.
- **Anchored to absolute wall-clock time**, so it doesn't drift when the screen sleeps or
  the tab is backgrounded — the timer computes "where should I be now" from a fixed start
  instant rather than accumulating intervals.
- **Wake Lock** held during a session to keep the screen awake.
- Accuracy gate: training-mode timing must stay within **±250 ms** of wall clock **with the
  screen locked**.

---

## 7. Audio cues

- **Web Audio API**.
- Cues are **synthesised via `OscillatorNode`** rather than played from audio files. This
  deliberately eliminates the file/format/decode/hosting questions entirely — there are no
  audio assets to ship or decode.
- Audio is blocked by browsers until a user gesture, so it's **unlocked on the Start tap**,
  verified, and the user is warned before the session if the unlock failed.

---

## 8. Visual design

- **No "AI slop" aesthetics** — the design must be genuinely appealing and motivating, not
  generic.
- **Design is data-driven:** the **competition kettlebell weight colour code** is used as a
  functional accent system (weight → colour), not decorative branding.
- **Dark iron base:** `#17171A`.
- **Typefaces:** Archivo (display) and Public Sans (text).
- **Signature "Bell Stack" element** reused consistently across the session view, the
  calendar, and the share image, for visual cohesion.

---

## 9. Anti-gamification philosophy

Discipline over dopamine. No streaks, no badges, no confetti.

- **Tonnage as the headline metric** — total weight moved, blunt and quantitative.
- **A 28-day density grid instead of streaks.** Missing a day dims one cell; nothing
  resets. Streaks punish life events; density rewards returning.
- **Dated personal records** — "Best 32 kg snatch set: 24 reps, 4 March." Beating it is the
  reward; no badge needed.
- **One honest sentence** after each session comparing it to the last comparable one.
  Sometimes that sentence is "Lighter than last week. That's what a deload is."

---

## 10. Testing

- **Testcontainers** (Postgres) + one `WebApplicationFactory`. Slice tests hit the real
  endpoint against a real database — for VSA this is the primary test type, not an
  afterthought.
- **Pure unit tests** for `TimelineResolver`, tonnage calculation, PR detection, RRULE
  expansion across DST boundaries. These are where the bugs actually live.
- **bUnit** for the training-mode component state machine (mock the JS interop).
- **Playwright** E2E: full session with the network cut mid-workout, reconnect, verify no
  duplicate set logs.
- A test that asserts Serilog output for `/auth/login` contains no password or token
  substring.

---

## 11. Delivery phases *(superseded by `ironbell-plan-v2.md`)*

> Kept for history. v2 reorganises this into milestones M0–M9 that each end in a check you
> can perform on a physical phone in under five minutes.

**Phase 1 — Foundation (~2 weeks)**
Solution skeleton, VSA scaffolding + mini-mediator, Postgres + EF Core + migrations,
Serilog wired both sides, Identity + JWT + refresh rotation, WASM PWA shell that installs,
design tokens implemented. *Done when: you can register, log in, install to home screen,
and see a styled empty Today screen.*

**Phase 2 — Content (~2 weeks)**
Exercise library + seed data, plan builder (all block types), plan templates, user profile
and preferences. *Done when: you can build a real 4-week programme.*

**Phase 3 — Training mode (~3 weeks, the hard one)**
Timeline resolver, JS timer engine, Web Audio cue system, wake lock, full-bleed training
UI, set logging, session completion. *Done when: you can train an entire EMOM in airplane
mode and the audio lands on the second.*

**Phase 4 — History & progress (~2 weeks)**
Calendar with chalk marks, session detail, Bell Stack, volume trends, PR tracking, tonnage.
*Done when: the app tells you something true about last month.*

**Phase 5 — Scheduling & reminders (~1.5 weeks)**
RRULE schedules, occurrence materialisation, Web Push + VAPID, reminder preferences, `.ics`
export, install-to-get-notifications prompt on iOS.

**Phase 6 — Offline hardening & polish (~2 weeks)**
IndexedDB queue, background sync, conflict handling, interruption recovery, update prompt,
accessibility pass (contrast, focus rings, reduced motion, screen reader on training mode),
bundle size, real-device testing on old Android and iOS.

**Later:** passkeys, coach role and shared programmes, Health Connect / HealthKit export,
wearable heart rate, plan marketplace.

---

## 12. Known risks

| Risk | Mitigation |
|---|---|
| iOS PWA limits (push needs install, storage eviction, no vibration) | Detect capabilities and degrade gracefully with honest copy. Sync aggressively. |
| Audio blocked until user gesture | Unlock on the Start tap, verify, warn before the session if it failed. |
| Timer drift during screen sleep | JS timer anchored to absolute time; verify against wall clock with screen locked. |
| Retroactive history corruption when plans are edited | `plan_snapshot` jsonb freezes the plan per session. |
| Cross-slice coupling creeping in | NetArchTest fails the build on cross-slice handler references. |
