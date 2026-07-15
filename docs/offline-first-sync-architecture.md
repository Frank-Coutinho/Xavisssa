# Offline-first sales and stock architecture

## Verified baseline

The desktop client is an Avalonia application backed by a per-workspace SQLite database through Entity Framework Core. The ASP.NET Core backend owns the central PostgreSQL database. Before this change, reads were mostly local-first, but a connected checkout attempted the backend first and the local sale and stock decrement were separate commits. Periodic pull sync and server-side stock concurrency checks already existed.

## Current write path

Sales now always enter through `SaleRepository` and commit to SQLite first. One SQLite transaction persists the sale, items, payments and stock movements and performs guarded decrements of the sellable-variant and product caches. A failure rolls back the whole checkout. After commit, the UI queues `SaleCompleted` on the serialized background sync worker.

Stock adjustments enter through `IStockAdjustmentService.ApplyLocalAsync`. One SQLite transaction persists the applied adjustment and movement and updates local variant stock. It then queues `StockAdjusted`. The current desktop UI does not yet expose an adjustment screen; this service is the required entry point for one when it is added.

## Sync path

The singleton background worker serializes event, reconnect, manual and periodic requests. The interval is configured by `OfflineFirst:BackgroundSyncIntervalSeconds` and defaults to 180 seconds. It uploads pending local writes before pulling central sales, sellable variants and stock levels. Successful periodic pulls refresh active views so staff see current warnings.

Sale and adjustment uploads are idempotent by `SyncId`. The backend applies each sale inside a PostgreSQL transaction and uses a conditional stock update (`quantity_on_hand >= requested`) to prevent negative central stock. Stock adjustment sync uses a dedicated idempotent apply endpoint and merges the offline quantity difference as a stock movement instead of overwriting newer central sales.

## Critical-stock rule

`OfflineFirst:LowStockThreshold` defaults to 5. If a cart line would leave stock at or below that threshold, checkout requires an authenticated live stock check. The client uses the lower of local and server availability so a server response cannot erase stock reserved by an unsynced local sale. If the server cannot be reached, the scarce-item sale is blocked; ordinary stock remains fully offline-capable.

Low-stock and out-of-stock warnings appear on catalog cards/list rows and in the low-stock metric.

## Cross-device conflict policy

The policy is `AlertStaff`. If two devices sell the last unit, the first server transaction wins. The second upload is rejected and recorded by the backend as a sync conflict. The client persists `SyncConflictId` and `SyncError`, stops automatically retrying that rejected sale, displays a long error notification and marks the history row `SYNC CONFLICT - STAFF ACTION REQUIRED`. Staff must stop fulfilment and issue/refund the payment according to store procedure. Automatic payment refunds are intentionally not attempted because no payment-provider reversal contract exists.

## PowerSync-ready stopping point

Domain writes are now isolated from HTTP transport: sales and stock adjustments commit locally, while background services own upload/pull behavior. `SyncId`, source-device timestamps, cursors and local conflict state are retained. A future PowerSync adapter can replace the current upload/pull implementations without changing checkout or stock-adjustment commands. PowerSync itself is not added in this phase.

## Relevant stack

- Desktop: .NET 8, Avalonia 11.3.8, ReactiveUI, Microsoft.Extensions.Hosting/DI/Http, EF Core SQLite, Microsoft.Data.Sqlite.
- Backend: .NET 9 ASP.NET Core, EF Core, Npgsql/PostgreSQL, JWT bearer authentication, Serilog, Swagger/OpenAPI.
- Sync: local SQLite metadata/outbox records, HTTP JSON DTOs, cursor-based delta pulls, idempotent UUID writes and a channel-based hosted background worker.
