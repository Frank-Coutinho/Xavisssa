# Local SQLite Offline Sync Upgrade

The Avalonia app upgrades existing local databases through `LocalDbContext.EnsureLocalSchemaAsync()` at startup. The upgrade is additive: it creates any missing operational tables, adds offline sync columns, and does not delete existing local rows.

## What Is Added

The local schema version is now `4`. Synced/offline-created tables get:

- `OnlineId`
- `SyncId`
- `SourceDeviceId`
- `ClientCreatedAt`
- `ClientUpdatedAt`
- `LastSyncedAt`
- audit/scope fields such as `TenantId`, `StoreId`, `CreatedAt`, `UpdatedAt`, `DeletedAt`, and `IsActive` where applicable

New local operational tables are also created for stock levels/movements, cash register sessions/movements, stock adjustments/items, and stock transfers/items.

## Backfill Rules

For existing rows:

- missing `SyncId` values are backfilled with a generated UUID per row
- missing `SourceDeviceId` is set to the local machine device marker
- missing client timestamps are derived from existing `CreatedAt`/`UpdatedAt` values, falling back to the current timestamp

The migration avoids destructive table rebuilds except for the existing sales discount constraint repair path that was already present. No unsynced local sales or catalog rows are intentionally removed.

## Sync Identity

The app should continue treating local SQLite `Id` as a local key. Remote identity is stored in `OnlineId`; cross-device/server reconciliation uses `SyncId`.
