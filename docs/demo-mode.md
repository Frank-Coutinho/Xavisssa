# Xavissa Demo Mode

Demo mode is a controlled public walkthrough for prospects. It is not a free edition of Xavissa and it is not trial mode.

## Demo vs Trial

Demo mode uses disposable fake data, starts without account creation or a license key, and expires after exactly 60 minutes. It must not become a production tenant automatically.

Trial mode is real business data with `Licenses.IsTrial = true` and an expiry. Trial tenants can be converted to paid use through the backoffice and are not reset automatically.

## Startup Flow

On first launch, or whenever there is no valid activated local license snapshot, Xavissa shows the activation/demo screen with:

- App name and short POS/stock explanation
- `Activate License`
- `Try Demo - 1 hour`
- Optional `Watch Video` when `Licensing:DemoVideoUrl` is configured
- Support/contact text

The demo button does not ask for a license key, credentials, or account creation.

## Session Behavior

`Try Demo - 1 hour` collects device identity (`DeviceFingerprint`, `DeviceName`, `MachineUserName`, `AppVersion`, `OSVersion`) and calls `POST /api/demo/start` through `IDemoApiClient`.

The expected server behavior is:

- Select an active `DemoTemplates` row.
- Create or assign a demo tenant.
- Set `Tenants.IsDemo = true`.
- Set `Tenants.DemoExpiresAt = StartedAt + 60 minutes`.
- Create a demo license with `Licenses.IsDemo = true`, `Licenses.IsTrial = false`, `Licenses.Status = Active`, and `Licenses.ExpiresAt` matching the session expiry.
- Create `DemoSessions` with `Status = Active`, `IsActive = true`, `ResetOnClose = true`, and `ExpiresAt = StartedAt + 60 minutes`.
- Return a signed demo license snapshot and demo session metadata.

If no demo API is configured, the desktop uses a clearly marked local development fallback. The fallback creates a temporary unsigned demo snapshot and a local seeded `xavissa_demo.db`; it is intended to be replaced by the production demo API.

## API Contract

`POST /api/demo/start`

Request:

- `DeviceFingerprint`
- `DeviceName`
- `MachineUserName`
- `AppVersion`
- `OSVersion`
- `OptionalLeadName`
- `OptionalLeadPhone`
- `OptionalLeadEmail`

Response:

- `Success`
- `FailureCode`
- `FailureMessage`
- `DemoSessionId`
- `TenantId`
- `TenantCode`
- `TenantName`
- `LicenseId`
- `StartedAt`
- `ExpiresAt`
- `ResetOnClose`
- `DemoLicenseSnapshot`
- `DemoModeEnabled`

`POST /api/demo/validate`

Validates `DemoSessionId`, `TenantId`, `LicenseId`, `DeviceFingerprint`, expiry, status, and active state. Expired sessions return `IsExpired = true`.

`POST /api/demo/events`

Tracks demo events and updates `DemoSessions.LastActivityAt`. Event tracking failures must not block the user workflow.

## Local Storage

Demo state is stored in `%LOCALAPPDATA%/Xavissa/demo-session-state.json`.

The demo database is isolated from production data at:

- `%LOCALAPPDATA%/Xavissa/Workspaces/Demo/xavissa_demo.db`

The production workspace remains at:

- `%LOCALAPPDATA%/Xavissa/Workspaces/Real/xavissa.db`

Cleanup code only deletes files under the demo workspace folder and only clears the local license snapshot when the snapshot is a demo snapshot.

## Seed Data

The local fallback seeds Mozambique sample data for:

- Tenant: `Loja Demo Xavissa`
- Stores: `Loja Central`, `Loja Bairro`
- Users/session identity: `demo-admin` plus documented demo roles
- Categories: `Bebidas`, `Mercearia`, `Higiene`, `Electrónicos`
- Products: `Água 500ml`, `Arroz 5kg`, `Açúcar 1kg`, `Óleo 1L`, `Sabão`, `Carregador USB`, `Auscultadores`
- Store assignments, variants with MZN prices, stock levels, stock movements, sales history, and sale payments

All seeded product descriptions and demo reports/receipts are marked as sample data.

## Restrictions

Demo mode allows exploring the app: sales, product lookup, stock changes, reports, receipts, store switching, product search, and barcode search.

Demo mode blocks or disables production behavior:

- Cloud sync
- Expired demo writes
- Real production conversion
- Production report export without sample watermark
- Demo-to-production tenant conversion
- Demo data reuse after expiry or reset

UI checks are not the only guard. Sale, product, stock, store, and sync service paths check demo expiry before writes or remote sync.

## UI Indicators

The main shell shows `DEMO MODE`, remaining minutes, `Activate License`, and `Contact Sales`.

When less than 10 minutes remain, the shell shows `Demo expires in 10 minutes.`

When expired, Xavissa redirects to `DemoExpiredView` with:

- `Demo session expired`
- `Restart Demo`
- `Activate License`
- `Contact Sales / WhatsApp`

## Conversion Flow

Activation from demo opens the normal activation flow. The UI explains that demo data does not become production data automatically. Any migration or real tenant setup must be handled by support/sales.
