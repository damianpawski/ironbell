# Ironbell — Gap Triage

> Companion to `ironbell-plan-v2.md`. Every item from the original plan sorted **by retrofit
> cost, not by importance** — the question is "how expensive is this to add *later*", because
> that's what determines whether it has to be in the foundation or can wait.

## The tiers

- **Tier A — blocking.** Expensive or impossible to retrofit once real users and installed
  PWAs exist. Must be right in the foundation (M0–M1 territory). Deferring a Tier-A item
  requires writing down why (it's item 5 of the definition of done).
- **Tier B — pre-launch.** Real work, but cheap enough to slot in before M9 without
  unwinding earlier decisions.
- **Tier C — deferred.** Can be added any time, or may never be needed. No foundation cost.

---

## Tier A — blocking (get right in the foundation)

- **Hosting on a real HTTPS domain from M0.** Service-worker scope, push subscriptions,
  IndexedDB, and the installed home-screen app are all bound to the origin. Migrating origins
  after users exist is expensive, so the domain and host are decided at M0, not staged
  through free options. *(Settled: Azure — see v2 M0.)*
- **Same-origin client + API** (or a deliberately-designed cross-origin cookie story).
  Getting this wrong bakes CORS/cookie pain into everything.
- **Auth token model** — JWT + rotating refresh with family reuse detection, ownership
  authorisation in the handler template. Retrofitting ownership checks after slices exist
  means auditing every slice.
- **`plan_snapshot` on sessions.** If history isn't snapshotted from the first logged
  session, editing a plan silently corrupts past data and there's no clean recovery.
- **VSA boundaries enforced by NetArchTest.** Cheap on day one; a large refactor once
  cross-slice coupling has spread.
- **Migration bundles applied as a pipeline step, never on app startup.** Changing this later
  means rebuilding the deploy process.
- **CSP + correlation-id logging from day one.** Bolting CSP on later breaks things in ways
  that are hard to find; threading a correlation id through after the fact means touching
  every log call.
- **Absolute-time timer anchoring.** The whole training-mode design depends on it; it's not
  something you retrofit under a drifting timer.

---

## Tier B — pre-launch (slot in before M9)

- Exercise library + seed data
- Plan builder for all nine block types
- Calendar, density grid, Bell Stack, tonnage, PR tracking
- RRULE scheduling + Web Push + VAPID
- `.ics` export
- Accessibility pass (contrast, focus rings, reduced motion, VoiceOver on training mode)
- Backup/restore drill
- Old-device testing (old Android, iOS)

---

## Tier C — deferred (any time, or never)

- Passkeys
- Coach role and shared programmes
- Health Connect / HealthKit export
- Wearable heart rate
- Plan marketplace

---

## Cut outright (decided *not* to build)

These were in scope somewhere and were deliberately removed. Filed so the decision has a
paper trail rather than living only in chat history:

1. **Feature flags** — solo dev, no need to dark-launch to segments.
2. **Load testing** — traffic profile is one user; the bottleneck won't be load.
3. **Autoscaling** — Container Apps scale-to-zero is enough; nothing to tune.
4. **Coverage targets** — a number to game, not a quality signal. Test the domain and the
   slices where bugs live instead.
5. **Data archival / retention tiers** — dataset is tiny; premature.
6. **Speculative coach role** (as infrastructure) — the *feature* is Tier C; building
   role/permission scaffolding for it now is cut.
7. **Monitoring dashboards** — Serilog + the platform's own logs cover a solo app; a
   dashboard is ceremony at this scale.

---

## Missing from the original plan (added by the triage)

Two things the first plan didn't account for:

1. **Client/server version skew.** A standalone WASM client can be running an old build
   against a newer API. Handled with an **`X-Client-Version` header**; the API returns
   **`426 Upgrade Required`** when the client is too old, and the app prompts to reload.
   (Lands in M5.)
2. **Audio synthesis instead of audio files.** Synthesising cues via **`OscillatorNode`**
   eliminates the file / format / decode / hosting questions entirely — there are no audio
   assets to ship, decode, or cache. (Folds into M4.)
