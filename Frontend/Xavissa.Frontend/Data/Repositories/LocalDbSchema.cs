public static class LocalDbSchema
{
    public const int CurrentSchemaVersion = 6;

    public const string ConfigureSqlitePragmasSql =
        @"
    -- Desktop offline-first workload: enforce relational integrity, keep readers
    -- responsive during writes, and wait briefly instead of failing on busy files.
    PRAGMA foreign_keys = ON;
    PRAGMA journal_mode = WAL;
    PRAGMA synchronous = NORMAL;
    PRAGMA busy_timeout = 5000;
    ";

    public const string CreateLocalSchemaInfoTableSql =
        @"
    CREATE TABLE IF NOT EXISTS LocalSchemaInfo (
        Id INTEGER PRIMARY KEY CHECK (Id = 1),
        Version INTEGER NOT NULL,
        UpdatedAt TEXT NOT NULL
    );
    ";

    public const string CreateStoresTableSql =
        @"
    CREATE TABLE IF NOT EXISTS Stores (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        Name TEXT NOT NULL,
        Code TEXT NOT NULL DEFAULT '',
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT,
        IsActive INTEGER NOT NULL DEFAULT 1
    );
    ";

    public const string CreateCategoriesTableSql =
        @"
    CREATE TABLE IF NOT EXISTS Categories (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        Name TEXT NOT NULL,
        IsActive INTEGER NOT NULL DEFAULT 1,
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT,
        ProductCount INTEGER NOT NULL DEFAULT 0
    );
    ";

    public const string CreateProductsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS Products (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        VariantId INTEGER NOT NULL DEFAULT 0,
        AssignmentId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        CategoryId INTEGER,
        Name TEXT NOT NULL,
        Barcode TEXT,
        Category TEXT,
        Brand TEXT,
        Label TEXT,
        AttributesJson TEXT,
        Code TEXT,
        Color TEXT,
        Size TEXT,
        SKU TEXT,
        Description TEXT,
        ImageUrl TEXT,
        CreatedAt TEXT,
        DeletedAt TEXT,
        Price REAL NOT NULL,
        StockQuantity INTEGER NOT NULL,
        IsActive INTEGER NOT NULL DEFAULT 1,
        VariantCount INTEGER NOT NULL DEFAULT 0,
        UpdatedAt TEXT
    );
    ";

    public const string CreateProductStoreAssignmentsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS ProductStoreAssignments (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        ProductId INTEGER NOT NULL,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        StoreName TEXT NOT NULL DEFAULT '',
        Price REAL NOT NULL DEFAULT 0,
        StockQuantity INTEGER NOT NULL DEFAULT 0,
        IsActive INTEGER NOT NULL DEFAULT 1,
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT,
        VariantCount INTEGER NOT NULL DEFAULT 0
    );
    ";

    public const string CreateProductVariantsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS ProductVariants (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        ProductId INTEGER NOT NULL,
        AssignmentId INTEGER NOT NULL DEFAULT 0,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        StoreName TEXT NOT NULL DEFAULT '',
        Name TEXT NOT NULL DEFAULT '',
        Description TEXT,
        Label TEXT NOT NULL DEFAULT '',
        SKU TEXT NOT NULL DEFAULT '',
        Barcode TEXT NOT NULL DEFAULT '',
        Price REAL NOT NULL DEFAULT 0,
        CostPrice REAL,
        AttributesJson TEXT,
        StockQuantity INTEGER NOT NULL DEFAULT 0,
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT,
        IsActive INTEGER NOT NULL DEFAULT 1
    );
    ";

    public const string CreateSellableVariantsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS SellableVariants (
        VariantId INTEGER PRIMARY KEY,
        StoreProductId INTEGER NOT NULL,
        ProductId INTEGER NOT NULL,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        ProductName TEXT NOT NULL DEFAULT '',
        VariantLabel TEXT NOT NULL DEFAULT '',
        Barcode TEXT NOT NULL DEFAULT '',
        SKU TEXT NOT NULL DEFAULT '',
        Price REAL NOT NULL DEFAULT 0,
        QuantityOnHand INTEGER NOT NULL DEFAULT 0,
        IsSellable INTEGER NOT NULL DEFAULT 1,
        UpdatedAt TEXT NOT NULL
    );
    CREATE INDEX IF NOT EXISTS IX_SellableVariants_StoreId_Barcode ON SellableVariants(StoreId, Barcode);
    CREATE INDEX IF NOT EXISTS IX_SellableVariants_StoreId_ProductName ON SellableVariants(StoreId, ProductName);
    ";

    public const string CreateSalesTableSql =
        @"
    CREATE TABLE IF NOT EXISTS Sales (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        SaleDate TEXT NOT NULL,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        TotalAmount REAL NOT NULL,
        Discount REAL,
        TotalPaid REAL NOT NULL DEFAULT 0,
        PaymentSummary TEXT NOT NULL DEFAULT '',
        PaymentStatus TEXT NOT NULL DEFAULT 'Paid',
        ChangeGiven REAL,
        ReceiptNumber TEXT,
        IsRefunded INTEGER NOT NULL DEFAULT 0,
        RefundReason TEXT,
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT,
        Synced INTEGER NOT NULL DEFAULT 0,
        SyncFailed INTEGER NOT NULL DEFAULT 0,
        CONSTRAINT CK_Sales_Discount_NotGreaterThanTotalAmount
            CHECK (Discount IS NULL OR Discount <= TotalAmount)
    );
    ";

    public const string CreateSaleItemsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS SaleItems (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        SaleId INTEGER NOT NULL,
        ProductId INTEGER NOT NULL,
        VariantId INTEGER NOT NULL DEFAULT 0,
        ProductName TEXT,
        ProductCategory TEXT,
        Quantity INTEGER NOT NULL,
        UnitPrice REAL NOT NULL,
        Subtotal REAL NOT NULL DEFAULT 0,
        IsRefunded INTEGER NOT NULL DEFAULT 0,
        RefundedQuantity INTEGER NOT NULL DEFAULT 0,
        RefundReason TEXT,
        RefundedAt TEXT,
        RefundedByUserId INTEGER,
        UpdatedBy INTEGER,
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT,
        FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE
    );
    ";

    public const string CreateSalePaymentsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS SalePayments (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        SaleId INTEGER NOT NULL,
        PaymentMethod TEXT NOT NULL DEFAULT 'Cash',
        Amount REAL NOT NULL DEFAULT 0,
        ReferenceNumber TEXT,
        Notes TEXT,
        CreatedAt TEXT NOT NULL DEFAULT '',
        UpdatedAt TEXT,
        DeletedAt TEXT,
        FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE
    );
    ";

    public const string CreateOperationalIndexesSql =
        @"
    CREATE INDEX IF NOT EXISTS IX_Stores_TenantId_IsActive ON Stores(TenantId, IsActive);
    CREATE INDEX IF NOT EXISTS IX_Stores_UpdatedAt ON Stores(UpdatedAt);
    CREATE INDEX IF NOT EXISTS IX_Categories_TenantId_IsActive ON Categories(TenantId, IsActive);
    CREATE INDEX IF NOT EXISTS IX_Categories_UpdatedAt ON Categories(UpdatedAt);
    CREATE INDEX IF NOT EXISTS IX_Products_TenantId_IsActive ON Products(TenantId, IsActive);
    CREATE INDEX IF NOT EXISTS IX_Products_UpdatedAt ON Products(UpdatedAt);
    CREATE INDEX IF NOT EXISTS IX_Products_Barcode ON Products(Barcode);
    CREATE INDEX IF NOT EXISTS IX_ProductStoreAssignments_StoreId_ProductId ON ProductStoreAssignments(StoreId, ProductId);
    CREATE INDEX IF NOT EXISTS IX_ProductStoreAssignments_UpdatedAt ON ProductStoreAssignments(UpdatedAt);
    CREATE INDEX IF NOT EXISTS IX_ProductVariants_StoreId_Barcode ON ProductVariants(StoreId, Barcode);
    CREATE INDEX IF NOT EXISTS IX_ProductVariants_ProductId ON ProductVariants(ProductId);
    CREATE INDEX IF NOT EXISTS IX_ProductVariants_UpdatedAt ON ProductVariants(UpdatedAt);
    CREATE INDEX IF NOT EXISTS IX_Sales_StoreId_SaleDate ON Sales(StoreId, SaleDate DESC);
    CREATE INDEX IF NOT EXISTS IX_Sales_SaleDate ON Sales(SaleDate DESC);
    CREATE INDEX IF NOT EXISTS IX_Sales_ReceiptNumber ON Sales(ReceiptNumber);
    CREATE INDEX IF NOT EXISTS IX_Sales_LastSyncedAt ON Sales(LastSyncedAt);
    CREATE INDEX IF NOT EXISTS IX_SaleItems_SaleId ON SaleItems(SaleId);
    CREATE INDEX IF NOT EXISTS IX_SaleItems_VariantId ON SaleItems(VariantId);
    CREATE INDEX IF NOT EXISTS IX_SalePayments_SaleId ON SalePayments(SaleId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_Categories_SyncId ON Categories(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_Products_SyncId ON Products(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_ProductStoreAssignments_SyncId ON ProductStoreAssignments(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_ProductVariants_SyncId ON ProductVariants(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_Sales_SyncId ON Sales(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_SaleItems_SyncId ON SaleItems(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_SalePayments_SyncId ON SalePayments(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_StockLevels_SyncId ON StockLevels(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_StockMovements_SyncId ON StockMovements(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_CashRegisterSessions_SyncId ON CashRegisterSessions(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_CashRegisterCashMovements_SyncId ON CashRegisterCashMovements(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_StockAdjustments_SyncId ON StockAdjustments(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_StockAdjustmentItems_SyncId ON StockAdjustmentItems(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_StockTransfers_SyncId ON StockTransfers(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_StockTransferItems_SyncId ON StockTransferItems(SyncId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_ProductStoreAssignments_TenantStoreProduct ON ProductStoreAssignments(TenantId, StoreId, ProductId);
    CREATE UNIQUE INDEX IF NOT EXISTS UX_StockLevels_TenantStoreVariant ON StockLevels(TenantId, StoreId, VariantId);
    CREATE INDEX IF NOT EXISTS IX_ProductVariants_AssignmentId ON ProductVariants(AssignmentId);
    CREATE INDEX IF NOT EXISTS IX_StockLevels_StoreId_VariantId ON StockLevels(StoreId, VariantId);
    CREATE INDEX IF NOT EXISTS IX_StockLevels_LastSyncedAt ON StockLevels(LastSyncedAt);
    CREATE INDEX IF NOT EXISTS IX_StockMovements_TenantStoreVariant ON StockMovements(TenantId, StoreId, VariantId);
    CREATE INDEX IF NOT EXISTS IX_StockMovements_LastSyncedAt ON StockMovements(LastSyncedAt);
    CREATE INDEX IF NOT EXISTS IX_CashRegisterCashMovements_SessionId ON CashRegisterCashMovements(CashRegisterSessionId);
    CREATE INDEX IF NOT EXISTS IX_StockAdjustmentItems_AdjustmentId ON StockAdjustmentItems(StockAdjustmentId);
    CREATE INDEX IF NOT EXISTS IX_StockTransferItems_TransferId ON StockTransferItems(StockTransferId);
    CREATE INDEX IF NOT EXISTS IX_SyncLogs_Synced_Timestamp ON SyncLogs(Synced, Timestamp);
    ";

    public const string CreateStockLevelsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS StockLevels (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        VariantId INTEGER NOT NULL DEFAULT 0,
        QuantityOnHand INTEGER NOT NULL DEFAULT 0,
        ReorderLevel INTEGER,
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT
    );
    ";

    public const string CreateStockMovementsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS StockMovements (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        VariantId INTEGER NOT NULL DEFAULT 0,
        Quantity INTEGER NOT NULL DEFAULT 0,
        MovementType TEXT NOT NULL DEFAULT '',
        ReferenceType TEXT,
        ReferenceId INTEGER,
        Notes TEXT,
        CreatedAt TEXT,
        UpdatedAt TEXT
    );
    ";

    public const string CreateCashRegisterSessionsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS CashRegisterSessions (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        OpenedByUserId INTEGER NOT NULL DEFAULT 0,
        ClosedByUserId INTEGER,
        OpenedAt TEXT NOT NULL DEFAULT '',
        ClosedAt TEXT,
        OpeningCashAmount REAL NOT NULL DEFAULT 0,
        ExpectedCashAmount REAL,
        CountedCashAmount REAL,
        DifferenceAmount REAL,
        Status TEXT NOT NULL DEFAULT 'Open',
        Notes TEXT,
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT
    );
    ";

    public const string CreateCashRegisterCashMovementsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS CashRegisterCashMovements (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        CashRegisterSessionId INTEGER NOT NULL,
        MovementType TEXT NOT NULL DEFAULT '',
        Amount REAL NOT NULL DEFAULT 0,
        Reason TEXT,
        CreatedAt TEXT NOT NULL DEFAULT '',
        CreatedBy INTEGER NOT NULL DEFAULT 0,
        FOREIGN KEY (CashRegisterSessionId) REFERENCES CashRegisterSessions(Id) ON DELETE CASCADE
    );
    ";

    public const string CreateStockAdjustmentsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS StockAdjustments (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        StoreId INTEGER NOT NULL DEFAULT 0,
        AdjustmentNumber TEXT NOT NULL DEFAULT '',
        Reason TEXT NOT NULL DEFAULT '',
        Status TEXT NOT NULL DEFAULT 'Draft',
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT
    );
    ";

    public const string CreateStockAdjustmentItemsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS StockAdjustmentItems (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        StockAdjustmentId INTEGER NOT NULL,
        VariantId INTEGER NOT NULL DEFAULT 0,
        OldQuantity INTEGER NOT NULL DEFAULT 0,
        NewQuantity INTEGER NOT NULL DEFAULT 0,
        DifferenceQuantity INTEGER NOT NULL DEFAULT 0,
        Reason TEXT,
        Notes TEXT,
        FOREIGN KEY (StockAdjustmentId) REFERENCES StockAdjustments(Id) ON DELETE CASCADE
    );
    ";

    public const string CreateStockTransfersTableSql =
        @"
    CREATE TABLE IF NOT EXISTS StockTransfers (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        TenantId INTEGER NOT NULL DEFAULT 0,
        FromStoreId INTEGER NOT NULL DEFAULT 0,
        ToStoreId INTEGER NOT NULL DEFAULT 0,
        TransferNumber TEXT NOT NULL DEFAULT '',
        Status TEXT NOT NULL DEFAULT 'Requested',
        RequestedAt TEXT NOT NULL DEFAULT '',
        CreatedAt TEXT,
        UpdatedAt TEXT,
        DeletedAt TEXT,
        CONSTRAINT CK_StockTransfers_DifferentStores CHECK (FromStoreId <> ToStoreId)
    );
    ";

    public const string CreateStockTransferItemsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS StockTransferItems (
        Id INTEGER PRIMARY KEY,
        OnlineId INTEGER NOT NULL DEFAULT 0,
        SyncId TEXT NOT NULL DEFAULT '',
        SourceDeviceId TEXT,
        ClientCreatedAt TEXT,
        ClientUpdatedAt TEXT,
        LastSyncedAt TEXT,
        StockTransferId INTEGER NOT NULL,
        VariantId INTEGER NOT NULL DEFAULT 0,
        QuantityRequested INTEGER NOT NULL DEFAULT 0,
        QuantityApproved INTEGER,
        QuantitySent INTEGER,
        QuantityReceived INTEGER,
        Notes TEXT,
        FOREIGN KEY (StockTransferId) REFERENCES StockTransfers(Id) ON DELETE CASCADE
    );
    ";

    public const string CreateOfflineIdentitiesTableSql =
        @"
    CREATE TABLE IF NOT EXISTS OfflineIdentities (
        Id INTEGER PRIMARY KEY,
        OnlineUserId INTEGER NOT NULL DEFAULT 0,
        Username TEXT NOT NULL UNIQUE,
        PasswordHash TEXT NOT NULL,
        ApiToken TEXT NOT NULL DEFAULT '',
        Role TEXT NOT NULL DEFAULT 'User',
        PlatformRoleId INTEGER,
        PlatformRoleCode TEXT NOT NULL DEFAULT '',
        PlatformRole TEXT NOT NULL,
        ActingRole TEXT NOT NULL,
        AllowedTenantsJson TEXT NOT NULL DEFAULT '[]',
        AllowedStoresJson TEXT NOT NULL DEFAULT '[]',
        SelectedTenantId INTEGER,
        SelectedStoreId INTEGER,
        IsActive INTEGER NOT NULL DEFAULT 1,
        LastOnlineLogin TEXT
    );
    ";

    public const string CreateLocalDeviceIdentityTableSql =
        @"
    CREATE TABLE IF NOT EXISTS LocalDeviceIdentity (
        Id INTEGER PRIMARY KEY CHECK (Id = 1),
        LocalDeviceId TEXT NOT NULL,
        DeviceFingerprint TEXT NOT NULL,
        CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL
    );
    ";

    public const string CreateLocalLicenseSnapshotsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS LocalLicenseSnapshots (
        Id INTEGER PRIMARY KEY CHECK (Id = 1),
        TenantId INTEGER,
        TenantCode TEXT NOT NULL DEFAULT '',
        TenantName TEXT NOT NULL DEFAULT '',
        LicenseId INTEGER,
        LicensePublicCode TEXT NOT NULL DEFAULT '',
        LicensePlanId INTEGER,
        PlanCode TEXT NOT NULL DEFAULT '',
        PlanName TEXT NOT NULL DEFAULT '',
        ActivationId INTEGER,
        DeviceFingerprint TEXT NOT NULL DEFAULT '',
        Status TEXT NOT NULL DEFAULT '',
        IsDemo INTEGER NOT NULL DEFAULT 0,
        IsTrial INTEGER NOT NULL DEFAULT 0,
        LicenseType TEXT NOT NULL DEFAULT '',
        PurchaseType TEXT NOT NULL DEFAULT '',
        MaxStores INTEGER,
        MaxUsers INTEGER,
        MaxDevices INTEGER,
        MaxOfflineDays INTEGER NOT NULL DEFAULT 7,
        AllowsMultiStore INTEGER NOT NULL DEFAULT 0,
        AllowsAdvancedReports INTEGER NOT NULL DEFAULT 0,
        AllowsCloudSync INTEGER NOT NULL DEFAULT 0,
        AllowsBarcodePrinting INTEGER NOT NULL DEFAULT 0,
        AllowsCustomReceipt INTEGER NOT NULL DEFAULT 0,
        AllowsDemoMode INTEGER NOT NULL DEFAULT 0,
        IssuedAt TEXT,
        ActivatedAt TEXT,
        ExpiresAt TEXT,
        LastValidatedAt TEXT,
        GracePeriodEndsAt TEXT,
        SnapshotIssuedAt TEXT NOT NULL,
        SnapshotExpiresAt TEXT NOT NULL,
        Signature TEXT NOT NULL
    );
    CREATE INDEX IF NOT EXISTS IX_LocalLicenseSnapshots_TenantId ON LocalLicenseSnapshots(TenantId);
    ";

    public const string CreateSyncLogsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS SyncLogs (
        Id INTEGER PRIMARY KEY,
        TableName TEXT NOT NULL,
        Operation TEXT NOT NULL,
        Payload TEXT NOT NULL,
        Timestamp TEXT NOT NULL,
        Synced INTEGER NOT NULL DEFAULT 0
    );
    ";

    public const string CreateSyncCursorsTableSql =
        @"
    CREATE TABLE IF NOT EXISTS SyncCursors (
        Key TEXT PRIMARY KEY,
        Value TEXT NULL
    );
    ";
}
