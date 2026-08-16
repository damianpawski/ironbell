# Azure provisioning (M0)

Run once, by hand. Provisioning is rare and occasionally destructive, so it is deliberately not in
the pipeline — CI only ever applies migrations and updates the container image.

Everything below assumes **North Europe** and the Azure SQL **free offer**, which is free for the
lifetime of the subscription rather than for twelve months. See ADR 0001 in
`ironbell-plan-v3-azure.md`.

## Prerequisites

- An Azure subscription
- Azure CLI (`az`) signed in: `az login`

## 1. Resource group

```bash
az group create --name ironbell --location northeurope
```

## 2. Deploy the infrastructure

Choose a SQL administrator password and keep it somewhere durable — it is needed again for the
`SQL_CONNECTION_STRING` secret below, and there is no way to recover it from Azure.

```bash
az deployment group create \
  --resource-group ironbell \
  --template-file infra/main.bicep \
  --parameters sqlAdminLogin=ironbelladmin sqlAdminPassword='<a-strong-password>'
```

The deployment prints `applicationUrl`, `containerAppName`, `sqlServerName` and `sqlServerFqdn`.
Keep all four.

> The app will return 500 until migrations run. That is expected and correct: the schema is applied
> by the pipeline, never by the application starting up.

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
