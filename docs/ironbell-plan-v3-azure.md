# Ironbell — Plan v3: Azure SQL, GHCR, Azure deployment

> Lands in `docs/` beside `ironbell-plan.md`, `ironbell-plan-v2.md`, and
> `ironbell-gap-triage.md`. **This is the current source of truth for hosting and the data
> layer.**
>
> **Supersedes:** v1 §2 (data model), v1 hosting row, **v2 §M0 and §M7**, and the OVH setup
> guide entirely.
> **Unchanged:** VSA structure, the domain core, training mode, offline design, the M0–M9
> milestone shape, and the GitHub labels/milestones already set up.

---

## What changed from v2

| | v2 | v3 |
|---|---|---|
| Database | PostgreSQL 16 on the VPS | **Azure SQL Database (free offer)** |
| Data access | EF Core + Dapper for read models | **EF Core only** |
| Compute | OVH VPS + Docker Compose + Caddy | **Azure Container Apps** |
| Registry | — | **GHCR** (GitHub Container Registry) |
| Client hosting | Caddy static files | **Served from the API container** (same origin preserved) |
| TLS | Caddy + Let's Encrypt | **Container Apps managed certificate** |
| Reminders | 60-second DB poller | **Daily materialiser + Storage Queue visibility timeouts** |

Two of those are **forced, not chosen** — the reminders redesign and the EF-only constraint.
The rest are deliberate.

---

## ADR 0001 — SQL Server now, PostgreSQL portability preserved

### Context

Azure SQL Database's free offer gives **100,000 vCore-seconds** of serverless compute,
**32 GB data and 32 GB backup per database per month, up to 10 databases per subscription,
for the lifetime of the subscription.** That is genuinely free and genuinely permanent — no
12-month clock, unlike Postgres Flexible Server. The cost is that it is **SQL Server**, and
the plan was built on PostgreSQL. The intent is to switch to PostgreSQL later, so the data
layer must stay portable.

### Decision

Run on Azure SQL (SQL Server) now for the free-forever tier. Keep the EF layer
provider-portable so a later move to PostgreSQL is a configuration change, not a rewrite.

### The prohibitions (this is what "abstracted" actually means)

EF Core gets you ~80% of provider portability for free; the missing 20% dies quietly unless
it's forbidden explicitly:

- **No Dapper.** Raw SQL is provider-specific; it would pin us to SQL Server. (This is why
  v3 drops the Dapper read-model split from v1/v2.)
- **No `rowversion` / `xmin`.** Use an **`int` concurrency token** instead — neither
  provider's native mechanism, so it works on both.
- **No arrays.** SQL Server has no array type; Postgres does. Model many-values as child
  rows.
- **JSON stays opaque.** Store and read it as a string; don't query into it with
  provider-specific JSON operators. (`plan_snapshot` remains a serialized blob, which is
  exactly how it's used anyway.)
- **UTC `DateTime`, never `DateTimeOffset`.** Consistent round-tripping across both
  providers.

### The one thing that actually enforces it

**CI runs the full slice test suite against a PostgreSQL Testcontainer from M0 — even though
production is SQL Server.** A **dual-provider CI matrix** is the only real defence: without
it, "portable" is a claim; with it, the build checks it on every push.

### The trap that catches everyone

**Collation.** SQL Server defaults to **case-insensitive**; PostgreSQL is
**case-sensitive**. A uniqueness test on exercise names passes in production and fails the
day you switch. Defence: **normalised lowercase columns** for uniqueness/lookup rather than
relying on the database's collation.

### Naming

**Keep PostgreSQL naming conventions for tables and columns** (snake_case). It costs nothing
to keep and removes a rename step from the eventual migration.

---

## Reminders — redesigned (forced by the serverless model)

The v2 design was a ~60-second outbox poller. Against a serverless database that is
catastrophic: **polling keeps the DB permanently awake, burning the entire monthly
vCore-second allowance in about 2.5 days** — and the default behaviour on exhaustion is the
database becoming **inaccessible until the 1st of the next month.**

**Replacement:** a **daily job** that expands RRULEs and enqueues **Azure Storage Queue**
messages with **visibility timeouts set to each delivery time**. The database is woken
**once a day**, not every minute.

It's genuinely better architecture — delivery no longer depends on a process that must never
miss a tick — but you're doing it because the free tier requires it. **This supersedes v2
§M7.**

---

## Deployment

- **Image → GHCR** (public package, €0). GitHub Actions builds and pushes on merge to
  `main`.
- **Azure Container Apps** pulls the image. Consumption plan, **scale-to-zero** through
  M0–M6.
- **Client served from the API container** — same origin, so the refresh cookie stays
  first-party and there's no CORS/cookie story to design.
- **Managed certificate + custom domain** on Container Apps (€0), replacing Caddy +
  Let's Encrypt.
- Migration bundle still applied **as a pipeline step, never on app startup** (unchanged
  from v2).

---

## Cost

| | Monthly |
|---|---|
| Azure SQL free offer | €0 |
| Container Apps, M0–M6 (scale to zero) | €0 |
| Container Apps, M7+ (min 1 replica) | ~€6–9 |
| Storage account + queue | ~€0.05 |
| Managed certificate, custom domain | €0 |
| GHCR (public package) | €0 |
| Application Insights (within free grant) | €0 |
| Domain registration | ~€1 |
| **Before M7 / after M7** | **~€1 / ~€8–10** |

Comparable to the OVH VPS once you're running continuously. The free tier buys you the first
six milestones, not the finished product — which is a perfectly good thing to buy. And
unlike Postgres Flexible Server (free for 12 months, then ~€16/mo), the SQL free offer
doesn't have an expiry clock.

---

## Risks this introduces

| Risk | Mitigation |
|---|---|
| Free vCore limit exhausted → app dark until the 1st | Alert at 10k vCore-seconds remaining; queue-based reminders keep daily burn low; billing-flip procedure documented and tested |
| Two stacked cold starts (app + DB) | `minReplicas: 1` from M7; accept it before then |
| Portability rots silently | Dual-provider CI matrix from M0 — the only real defence |
| Local SQL Server on Apple Silicon | Verify before M0; if it fails, this ADR needs reopening |
| Free offer terms change | 32 GB of data is small — a BACPAC export is a complete exit |
