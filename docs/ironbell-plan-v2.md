# Ironbell — Delivery Plan (v2, milestone-based)

> Supersedes §11 of `ironbell-plan.md`. Incorporates the triage in `ironbell-gap-triage.md`.
> Architecture decisions live in `ironbell-plan.md`; this document is about **sequence and
> proof**.
>
> ⚠️ **Hosting and data layer are superseded by `ironbell-plan-v3-azure.md`.** v3 changes the
> database to **Azure SQL (free offer)**, drops Dapper (**EF Core only**), moves compute to
> **Azure Container Apps + GHCR**, serves the client **from the API container**, and
> redesigns reminders (**§M7**) onto a **daily materialiser + Storage Queue**. Read the M0
> and M7 sections below through that lens — the *milestone shape and checks still stand*, but
> the specific tech in those two sections is now v3's.

## What changed from v1 and why

The first plan was organised by **layer** — foundation, then content, then training, then
history. That structure has a failure mode: nothing is installed on a phone until week 9,
and the riskiest component (the timer) is validated last, when there's no schedule left to
react with.

Version 2 inverts two things:

1. **A real phone is the target from week one, not week nine.** Milestone 0 ends with the
   app installed on your home screen from a real HTTPS domain, updating automatically on
   push to `main`. Everything after that is verified where it will actually be used.
2. **The domain logic is proven headless before any UI exists.** The timeline resolver,
   tonnage, and PR rules are pure functions with exhaustive tests at M2 — before a single
   training screen is built. They're the cheapest thing to get wrong early and the most
   expensive to get wrong late.

Every milestone ends with a **check you can perform in under five minutes**, most of them
on the phone. If you can't demonstrate it, the milestone isn't done.

---

## Milestones at a glance

| # | Milestone | Length | Ends with |
|---|---|---|---|
| M0 | Walking skeleton, deployed | 1 wk | App on your home screen, auto-deploying |
| M1 | Identity | 1 wk | Stay signed in for a week; revoke from another device |
| M2 | Domain core (headless) | 1 wk | Golden-file test expands a 6-week plan exactly |
| M3 | Exercises & plan builder | 2 wk | Build Rite of Passage on the phone in 5 min |
| M4 | **Training mode** | 3 wk | 10-min EMOM accurate to ±250 ms with screen locked |
| M5 | Offline & sync | 1 wk | Force-quit mid-session in airplane mode, resume, sync once |
| M6 | History & progress | 2 wk | Numbers match a hand-computed spreadsheet |
| M7 | Scheduling & reminders | 1 wk | Push fires at 05:30, survives the clock change |
| M8 | Launch hardening | 1 wk | Restore from backup; VoiceOver completes a session |
| M9 | Train on it | 2 wk | Two weeks of real use, fixes only |

**15 weeks to launch, plus 2 weeks of living with it.** Longer than v1's 13 — that estimate
assumed training mode would take three weeks with nothing going wrong.

---

## Definition of done — applies to every milestone

No milestone is complete until all five are true:

1. Merged to `main` and auto-deployed to production.
2. The manual check below passes **on a physical phone**, not a simulator.
3. All tests green, including architecture tests.
4. WASM bundle under budget (the build fails otherwise).
5. No new Tier-A item deferred without writing down why.

---

## M0 — Walking skeleton, deployed to your phone

**Goal:** eliminate every "how do we deploy this" question before any feature exists.

**Decide hosting now.** PWA install, service workers, and Web Push all require HTTPS on a
real domain, so this cannot be deferred past week one.

> **Hosting decision → see `ironbell-plan-v3-azure.md`.** Azure was chosen over a
> self-managed VPS, while keeping the identical Docker-image and CI/CD story. v3 pins the
> specifics: **Azure SQL Database (free offer)** for the DB, **Container Apps** (Consumption,
> scale-to-zero) for the API with the **client served from the same container**, and
> **GHCR** for the image. Not Postgres Flexible Server — the SQL free offer is free
> indefinitely rather than for 12 months. Consider a small **Bicep** file so the resources
> are infra-as-code rather than clicked up by hand.

**Build:**
- Solution skeleton, VSA scaffolding, the hand-rolled `IHandler<,>` and pipeline behaviours
- One trivial end-to-end slice (`GET /api/health/ping` → rendered on screen)
- Postgres + EF Core, `UseXminAsConcurrencyToken`, `EnableRetryOnFailure`
- CI: build, test, **migration bundle applied as a pipeline step** (never on app startup),
  deploy
- Secrets in the vault with the same key names as local user-secrets
- CSP on from day one (`wasm-unsafe-eval` for Blazor), CORS configured
- Serilog both sides, correlation id flowing client → API → log
- Architecture tests (no cross-slice handler references)
- Bundle size budget that fails the build
- PWA manifest, icons, service worker, OpenAPI

**Done when:** Ironbell is on your home screen, launched from the icon, showing a value that
came from Postgres. You push a one-word change to `main`, and within ten minutes the phone
shows it after a reload prompt.

---

## M1 — Identity

**Build:** register, email confirmation, login, JWT issuance, refresh rotation with family
reuse detection, logout, signed-in devices list with revoke, password reset, rate limiting
on auth endpoints. Ownership authorisation baked into the handler template so it's the
default, not a remembered step.

**Automated gates:**
- Presenting a rotated refresh token revokes the whole family
- Ten parallel 401s trigger exactly one refresh (semaphore test)
- A test asserting the login request's Serilog output contains no password or token
  substring

**Done when:** you sign in on the phone, force-quit, and reopen three days later still
signed in without typing anything. Then you revoke that device from a laptop, and the phone
is signed out at its next refresh.

---

## M2 — Domain core, headless

No UI. No endpoints. Pure C# and tests.

**Build:** `TimelineResolver` (expands any plan into an ordered, timed step list across all
nine block types), tonnage calculation, PR detection, RRULE expansion including DST
boundaries.

**The gate:** a **golden-file test** that expands a full six-week programme into a committed
ordered step list. The committed golden file *is* the spec — any change to expansion output
shows up as a diff you have to consciously accept.

**Done when:** the golden-file test expands a 6-week plan exactly, and the tonnage/PR
numbers match a hand-computed check.

---

## M3 — Exercises & plan builder

**Build:** exercise library + seed data, plan builder covering all nine block types, plan
templates, user profile and preferences.

**Done when:** you can build the Rite of Passage programme on the phone in about five
minutes.

---

## M4 — Training mode *(the hard one, ~3 weeks)*

**Build:** the JS timer engine (absolute-time anchored), Web Audio cue system
(`OscillatorNode`, unlocked on Start), Wake Lock, full-bleed training UI driven by the
timeline resolver, set logging, session completion writing a `plan_snapshot`.

**Done when:** you can train a 10-minute EMOM with the **screen locked**, in airplane mode,
and the audio cues land within **±250 ms** of wall clock the whole way through.

---

## M5 — Offline & sync

**Build:** IndexedDB write queue, background sync, conflict handling, interruption recovery,
client/server version-skew handling (`X-Client-Version` header → `426` when the client is
too old).

**Done when:** you force-quit mid-session in airplane mode, reopen, the session resumes from
where it was, and when the network returns it syncs **exactly once** — no duplicate set
logs.

---

## M6 — History & progress

**Build:** calendar with chalk marks, 28-day density grid, session detail, the Bell Stack
element, volume trends, PR tracking, tonnage headline.

**Done when:** the numbers on screen match a hand-computed spreadsheet for a month of
sessions.

---

## M7 — Scheduling & reminders

> ⚠️ **Superseded by `ironbell-plan-v3-azure.md`.** The reminders mechanism changes from a
> polling design to a **daily RRULE materialiser that enqueues Azure Storage Queue messages
> with visibility timeouts** — a ~60-second DB poller would drain the serverless SQL free
> allowance in ~2.5 days. The milestone *check* below is unchanged.

**Build:** RRULE schedules, occurrence materialisation (daily job → Storage Queue, per v3),
Web Push + VAPID, reminder preferences, `.ics` export, install-to-get-notifications prompt
on iOS.

**Done when:** a push reminder fires at 05:30 and still fires correctly across a
daylight-saving clock change.

---

## M8 — Launch hardening

**Build:** database backup/restore drill, accessibility pass (contrast, focus rings,
reduced motion, screen reader on training mode), update prompt, bundle-size check, old
Android + iOS device testing.

**Done when:** you can restore the database from a backup, and **VoiceOver** can complete a
full training session start to finish.

---

## M9 — Train on it

**Two weeks of real use. Fixes only, no new features.** The point is to find what only shows
up when you actually train on it — not to keep building.

---

## Later (explicitly not now)

Passkeys, coach role and shared programmes, Health Connect / HealthKit export, wearable
heart rate, plan marketplace.
