# Azure provisioning (M0)

Run once, by hand. Provisioning is rare and occasionally destructive, so it is deliberately not in
the pipeline — CI only ever applies migrations and updates the container image.

Everything below assumes the Azure SQL **free offer**, which is free for the
lifetime of the subscription rather than for twelve months. See ADR 0001 in
`ironbell-plan-v3-azure.md`.

> **Region.** North Europe was the intended home, but this subscription is refused SQL server
> creation in both North Europe and West Europe. **`uksouth` works** — pass `location=uksouth`. If
> a region ever refuses, see *When provisioning fails* below.

## Prerequisites

- An Azure subscription
- Azure CLI (`az`) signed in: `az login`

## 1. Resource group

```bash
az group create --name ironbell --location uksouth
```

## 2. Deploy the infrastructure

Choose a SQL administrator password and keep it somewhere durable — it is needed again for the
`SQL_CONNECTION_STRING` secret below, and there is no way to recover it from Azure.

```bash
az deployment group create \
  --resource-group ironbell \
  --template-file infra/main.bicep \
  --parameters location=uksouth sqlAdminLogin=ironbelladmin sqlAdminPassword='<a-strong-password>'
```

The deployment prints `applicationUrl`, `containerAppName`, `sqlServerName` and `sqlServerFqdn`.
Keep all four.

> The app will return 500 until migrations run. That is expected and correct: the schema is applied
> by the pipeline, never by the application starting up.

## 2b. Confirm it works, without touching CI

The pipeline is not needed to prove the infrastructure is sound. One script reads the deployment
outputs, checks both health endpoints, and optionally applies the schema:

```powershell
# Read-only: confirms the container serves and that readiness correctly refuses.
./scripts/verify-azure.ps1

# Also applies migrations, opening a firewall rule for this machine and removing it afterwards.
./scripts/verify-azure.ps1 -SqlPassword 'the-password-you-chose'
```

What it asserts, in order:

| Check | Before migrations | After |
|---|---|---|
| `GET /api/health/live` | **200** — needs no database | 200 |
| `GET /api/health/ping` | **500** — no schema yet | 200, `schemaVersion: m0` |

That pairing is the point. Liveness answering while readiness refuses is what stops a paused
database from restarting healthy containers, and it is worth seeing it behave that way once.

Then open the printed URL in a browser: the walking-skeleton screen, reading `m0` out of Azure SQL.
That is M0's check, short of the phone.

> **The first request after idle is slow.** The database pauses after 60 minutes and the app scales
> to zero, so a cold start waits for both — expect 30 seconds or more. The script allows for it. It
> is not a fault; it is the price of the free tier, and `minReplicas` goes to 1 at M7.

Watch it work with:

```powershell
az containerapp logs show --name ironbell-api --resource-group ironbell --follow
```

## 3. Service principal for the deploy job

```bash
az ad sp create-for-rbac \
  --name ironbell-deploy \
  --role contributor \
  --scopes /subscriptions/<subscription-id>/resourceGroups/ironbell \
  --sdk-auth
```

Copy the whole JSON object it prints — it is shown once and never again.

Scoped to the resource group rather than the subscription so the credential cannot touch anything
else. **It expires**; when a deploy starts failing to authenticate months from now, this is why.

## 4. GitHub configuration

The deploy job stays inert until `DEPLOY_ENABLED` is `true`, so the workflow can be merged and
tested well before any of this exists.

Repository **secrets**:

| Name | Value |
|---|---|
| `AZURE_CREDENTIALS` | the JSON from step 3 |
| `SQL_CONNECTION_STRING` | `Server=tcp:<sqlServerFqdn>,1433;Initial Catalog=ironbell;User ID=ironbelladmin;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;` |

Repository **variables**:

| Name | Value |
|---|---|
| `DEPLOY_ENABLED` | `true` |
| `AZURE_RESOURCE_GROUP` | `ironbell` |
| `AZURE_SQL_SERVER` | `sqlServerName` from step 2 |
| `AZURE_CONTAINER_APP` | `ironbell-api` |

```bash
gh secret set AZURE_CREDENTIALS < credentials.json
gh secret set SQL_CONNECTION_STRING
gh variable set DEPLOY_ENABLED --body true
gh variable set AZURE_RESOURCE_GROUP --body ironbell
gh variable set AZURE_SQL_SERVER --body '<sqlServerName>'
gh variable set AZURE_CONTAINER_APP --body ironbell-api
```

Delete `credentials.json` afterwards.

## 5. First deploy

Push to `main`, or re-run the latest workflow. The deploy job applies migrations and then rolls the
image — in that order, so the schema is in place before any container serving the new code exists.

## Tearing it all down

Everything lives in one resource group, so removing the group removes the lot.

```bash
pwsh ./scripts/teardown-azure.ps1
```

It lists what will go and makes you type the group name first. `-Force` skips that, `-Wait` blocks
until deletion finishes rather than returning as soon as it starts. The raw equivalent is:

```bash
az group delete --name ironbell --yes --no-wait
```

**This deletes the database and everything in it.** While the only row is the seeded `app_info`
that is exactly what you want. Once real training history exists it is not, and this stops being a
convenience.

### Repeating the create-and-delete cycle

- **Wait for the delete to finish before recreating.** The SQL server name is derived from the
  resource group id, so recreating the same group in the same subscription produces the same server
  name. That keeps things predictable, but a delete still in flight can collide with the next
  create. Wait for `az group exists --name ironbell` to return `false`.

Billing stops when deletion **starts**, so `--no-wait` costs nothing extra.

## Logs, and why there is no Log Analytics workspace by default

Container Apps does not need one. The platform streams the container's stdout, which is exactly
where Serilog writes, so this works whether or not a workspace exists:

```bash
az containerapp logs show --name ironbell-api --resource-group ironbell --follow
az containerapp logs show --name ironbell-api --resource-group ironbell --tail 200
```

What you give up is **queryable history**: logs are live only, so nothing is available for a crash
that happened overnight. The gap triage already cut monitoring dashboards as ceremony at this
scale, and correlation ids make a live tail genuinely usable.

Ingestion for a single-user app that scales to zero would cost very little, but it is the only
resource in the template without a hard free guarantee, and it soft-deletes for fourteen days —
which makes repeated create-and-delete cycles behave strangely.

Turn it on when history actually matters:

```bash
az deployment group create \
  --resource-group ironbell \
  --template-file infra/main.bicep \
  --parameters sqlAdminLogin=ironbelladmin sqlAdminPassword='<password>' enableLogAnalytics=true
```

That adds a workspace with 30-day retention and a hard 0.1 GB/day ingestion cap, so a runaway
logging bug stops rather than bills. Note the soft delete: recreating a workspace with the same
name inside fourteen days recovers the old one, logs and all.

**If you want durable logs without Azure Monitor**, a Serilog sink to an external collector is the
alternative — Seq, Axiom and Better Stack all have free tiers. That trades an Azure cost for an
external dependency and one more secret, so it is worth doing when there is a reason to look at
logs after the fact, not before.
## When provisioning fails

### `RegionDoesNotAllowProvisioning`

> Location 'North Europe' is not accepting creation of new Windows Azure SQL Database servers at
> this time.

Not a template problem. Azure restricts SQL server creation per region and per subscription, and
new subscriptions are commonly blocked in popular regions. There is no reliable way to pre-check
it; the practical answer is to try a nearby region.

The location is a parameter, so nothing needs editing:

```bash
az deployment group create \
  --resource-group ironbell \
  --template-file infra/main.bicep \
  --parameters location=westeurope sqlAdminLogin=ironbelladmin sqlAdminPassword='<password>'
```

Reasonable alternatives in order of proximity to Ireland: `westeurope`, `uksouth`,
`francecentral`, `swedencentral`.

**Tear down before retrying in a different region.** A failed SQL server can be left behind in a
`Failed` state, and the server name is derived from the resource group id, so a redeploy tries to
reuse the same name — which cannot change region. Start clean:

```bash
pwsh ./scripts/teardown-azure.ps1 -Wait
az group create --name ironbell --location westeurope
```

If every region refuses, the restriction is on the subscription rather than the region, and lifting
it needs a support request. That is worth knowing before trying six regions in turn.

### `AppLogsConfiguration.Destination is invalid`

Fixed. `destination: 'none'` is not accepted; the API's "or none" means send no configuration at
all. The template now omits `appLogsConfiguration` entirely when `enableLogAnalytics` is false.

### Checking what actually failed

The top-level message is never the reason. This lists the individual operations:

```bash
az deployment operation group list \
  --resource-group ironbell --name main \
  --query "[?properties.provisioningState=='Failed'].{resource:properties.targetResource.resourceType, code:properties.statusMessage.error.code, message:properties.statusMessage.error.message}" \
  --output table
```

## Things worth knowing

**Cold starts are stacked.** The database pauses after 60 minutes idle and the app scales to zero,
so the first request after a quiet period waits for both. This is the accepted cost of €0 through
M6; `minReplicas` goes to 1 at M7.

**Exhausting the free allowance takes the database offline** rather than starting to bill. That is
`freeLimitExhaustionBehavior: 'AutoPause'`, chosen because an outage is recoverable and a surprise
invoice is not. Set an alert well before the 100,000 vCore-second limit, and flip the behaviour
deliberately if availability ever matters more than the bill.

**The firewall holds only the Azure-services rule.** Container Apps has no stable outbound IP on
the consumption plan. The deploy job opens a rule for the runner's address, applies migrations, and
removes it again even if the migration fails.

**SQL authentication, not managed identity.** Managed identity would remove the password entirely
and is the better end state, but it also has to work for the migration bundle, which is a larger
change than M0 needs. Worth doing before there is real user data.
grep -n "Log Analytics soft-deletes" docs/azure-provisioning.md
