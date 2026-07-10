using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Database.Migrations;

public partial class CleanupFinalArchitecture : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP VIEW IF EXISTS "vw_store_sellable_variants";
            DROP VIEW IF EXISTS "vw_cash_register_session_summary";
            DROP VIEW IF EXISTS "vw_tenant_license_usage";
            DROP VIEW IF EXISTS "vw_active_tenant_license";

            UPDATE "Products"
            SET "UpdatedAt" = "LastModified"
            WHERE "UpdatedAt" IS NULL AND "LastModified" IS NOT NULL;

            UPDATE "ProductVariants"
            SET "UpdatedAt" = "LastModified"
            WHERE "UpdatedAt" IS NULL AND "LastModified" IS NOT NULL;

            UPDATE "Sales"
            SET "UpdatedAt" = "LastModified"
            WHERE "UpdatedAt" IS NULL AND "LastModified" IS NOT NULL;

            UPDATE "SaleItems"
            SET "UpdatedAt" = "LastModified"
            WHERE "UpdatedAt" IS NULL AND "LastModified" IS NOT NULL;

            UPDATE "StockLevels"
            SET "UpdatedAt" = "LastUpdatedAt"
            WHERE "UpdatedAt" IS NULL AND "LastUpdatedAt" IS NOT NULL;

            ALTER TABLE "Tenants"
                DROP COLUMN IF EXISTS "Status",
                DROP COLUMN IF EXISTS "TrialStartsAt",
                DROP COLUMN IF EXISTS "TrialEndsAt",
                DROP COLUMN IF EXISTS "SubscriptionEndsAt",
                DROP COLUMN IF EXISTS "LicenseStatus";

            ALTER TABLE "Users" DROP COLUMN IF EXISTS "IsDemoUser";
            ALTER TABLE "Stores" DROP COLUMN IF EXISTS "IsDemoStore";

            ALTER TABLE "Products" DROP COLUMN IF EXISTS "LastModified";
            ALTER TABLE "ProductVariants" DROP COLUMN IF EXISTS "LastModified";
            ALTER TABLE "Sales"
                DROP COLUMN IF EXISTS "LastModified",
                DROP COLUMN IF EXISTS "PaymentMethod",
                DROP COLUMN IF EXISTS "AmountPaid",
                DROP COLUMN IF EXISTS "SoldByUserId";
            ALTER TABLE "SaleItems"
                DROP COLUMN IF EXISTS "LastModified",
                DROP COLUMN IF EXISTS "Discount";
            ALTER TABLE "StockLevels" DROP COLUMN IF EXISTS "LastUpdatedAt";
            ALTER TABLE "StockMovements"
                DROP COLUMN IF EXISTS "UpdatedAt",
                DROP COLUMN IF EXISTS "UpdatedBy";

            ALTER TABLE "CashRegisterSessions"
                DROP COLUMN IF EXISTS "CashSalesTotal",
                DROP COLUMN IF EXISTS "NonCashSalesTotal",
                DROP COLUMN IF EXISTS "TotalSalesAmount",
                DROP COLUMN IF EXISTS "TotalRefundAmount";

            DROP TABLE IF EXISTS "TenantSubscriptions" CASCADE;
            DROP TABLE IF EXISTS "DeletedSaleItems" CASCADE;
            DROP TABLE IF EXISTS "DeletedSales" CASCADE;
            DROP TABLE IF EXISTS "StoreCategoryMappings" CASCADE;

            ALTER TABLE "Products" ALTER COLUMN "IsActive" SET DEFAULT true;
            ALTER TABLE "ProductVariants" ALTER COLUMN "IsActive" SET DEFAULT true;
            UPDATE "ProductVariants" SET "IsActive" = true WHERE "IsActive" IS NULL;
            ALTER TABLE "ProductVariants" ALTER COLUMN "IsActive" SET NOT NULL;
            ALTER TABLE "Sales" ALTER COLUMN "PaymentStatus" SET DEFAULT 'Paid';
            ALTER TABLE "Stores" ALTER COLUMN "IsActive" SET DEFAULT true;
            ALTER TABLE "Categories" ALTER COLUMN "IsActive" SET DEFAULT true;
            ALTER TABLE "StoreProducts" ALTER COLUMN "IsActive" SET DEFAULT true;

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Tenants_Code"
                ON "Tenants" ("Code");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Stores_TenantId_Code"
                ON "Stores" ("TenantId", "Code");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_TenantUsers_TenantId_UserId"
                ON "TenantUsers" ("TenantId", "UserId");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_UserStoreRoles_TenantId_StoreId_UserId"
                ON "UserStoreRoles" ("TenantId", "StoreId", "UserId");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Categories_TenantId_Code"
                ON "Categories" ("TenantId", "Code")
                WHERE "Code" IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Products_TenantId_Code"
                ON "Products" ("TenantId", "Code");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_StoreProducts_TenantId_StoreId_ProductId"
                ON "StoreProducts" ("TenantId", "StoreId", "ProductId");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProductVariants_TenantId_Barcode"
                ON "ProductVariants" ("TenantId", "Barcode")
                WHERE "Barcode" IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProductVariants_TenantId_SKU"
                ON "ProductVariants" ("TenantId", "SKU")
                WHERE "SKU" IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_StockLevels_TenantId_StoreId_VariantId"
                ON "StockLevels" ("TenantId", "StoreId", "VariantId");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_LicensePlans_Code"
                ON "LicensePlans" ("Code");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Licenses_LicensePublicCode"
                ON "Licenses" ("LicensePublicCode");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Licenses_LicenseKeyHash"
                ON "Licenses" ("LicenseKeyHash");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_DemoSessions_DemoTokenHash"
                ON "DemoSessions" ("DemoTokenHash");

            CREATE OR REPLACE VIEW "vw_cash_register_session_summary" AS
            SELECT
                crs."Id" AS "CashRegisterSessionId",
                crs."TenantId",
                crs."StoreId",
                crs."OpenedByUserId",
                crs."OpenedAt",
                crs."ClosedAt",
                crs."OpeningCashAmount",
                crs."Status",
                COALESCE(SUM(sp."Amount") FILTER (WHERE lower(sp."PaymentMethod") = 'cash'), 0) AS "CashPaymentsTotal",
                COALESCE(SUM(sp."Amount") FILTER (WHERE lower(sp."PaymentMethod") <> 'cash'), 0) AS "NonCashPaymentsTotal",
                COALESCE(SUM(sp."Amount"), 0) AS "AllPaymentsTotal",
                COALESCE(cash_movements."CashInTotal", 0) AS "CashInTotal",
                COALESCE(cash_movements."CashOutTotal", 0) AS "CashOutTotal",
                crs."OpeningCashAmount"
                    + COALESCE(SUM(sp."Amount") FILTER (WHERE lower(sp."PaymentMethod") = 'cash'), 0)
                    + COALESCE(cash_movements."CashInTotal", 0)
                    - COALESCE(cash_movements."CashOutTotal", 0) AS "CalculatedExpectedCashAmount"
            FROM "CashRegisterSessions" crs
            LEFT JOIN "SalePayments" sp ON sp."CashRegisterSessionId" = crs."Id"
            LEFT JOIN (
                SELECT
                    "CashRegisterSessionId",
                    COALESCE(SUM("Amount") FILTER (WHERE "MovementType" = 'CashIn'), 0) AS "CashInTotal",
                    COALESCE(SUM("Amount") FILTER (WHERE "MovementType" = 'CashOut'), 0) AS "CashOutTotal"
                FROM "CashRegisterCashMovements"
                GROUP BY "CashRegisterSessionId"
            ) cash_movements ON cash_movements."CashRegisterSessionId" = crs."Id"
            GROUP BY
                crs."Id",
                cash_movements."CashInTotal",
                cash_movements."CashOutTotal";

            CREATE OR REPLACE VIEW "vw_active_tenant_license" AS
            SELECT DISTINCT ON (l."TenantId")
                l."Id" AS "LicenseId",
                l."TenantId",
                t."Name" AS "TenantName",
                t."Code" AS "TenantCode",
                l."LicensePlanId",
                lp."Code" AS "PlanCode",
                lp."Name" AS "PlanName",
                COALESCE(l."MaxStoresOverride", lp."MaxStores") AS "MaxStores",
                COALESCE(l."MaxUsersOverride", lp."MaxUsers") AS "MaxUsers",
                COALESCE(l."MaxDevicesOverride", lp."MaxDevices") AS "MaxDevices",
                COALESCE(l."MaxOfflineDaysOverride", lp."MaxOfflineDays") AS "MaxOfflineDays",
                lp."AllowsMultiStore",
                lp."AllowsAdvancedReports",
                lp."AllowsCloudSync",
                lp."AllowsBarcodePrinting",
                lp."AllowsCustomReceipt",
                lp."AllowsDemoMode",
                l."LicenseType",
                l."PurchaseType",
                l."Status",
                l."IsDemo",
                l."IsTrial",
                l."IssuedAt",
                l."ActivatedAt",
                l."ExpiresAt",
                l."LastValidatedAt",
                l."GracePeriodEndsAt"
            FROM "Licenses" l
            JOIN "LicensePlans" lp ON lp."Id" = l."LicensePlanId"
            JOIN "Tenants" t ON t."Id" = l."TenantId"
            WHERE l."Status" = 'Active'
            ORDER BY l."TenantId", l."IsDemo" ASC, COALESCE(l."ActivatedAt", l."IssuedAt", l."CreatedAt") DESC;

            CREATE OR REPLACE VIEW "vw_tenant_license_usage" AS
            SELECT
                active."TenantId",
                active."TenantName",
                active."PlanCode",
                active."PlanName",
                active."LicenseId",
                active."MaxStores",
                COALESCE(stores."UsedStores", 0)::integer AS "UsedStores",
                active."MaxUsers",
                COALESCE(users."UsedUsers", 0)::integer AS "UsedUsers",
                active."MaxDevices",
                COALESCE(devices."UsedDevices", 0)::integer AS "UsedDevices",
                active."MaxOfflineDays",
                active."AllowsMultiStore",
                active."AllowsAdvancedReports",
                active."AllowsCloudSync",
                active."AllowsBarcodePrinting",
                active."AllowsCustomReceipt",
                active."AllowsDemoMode",
                active."LicenseType",
                active."PurchaseType",
                active."Status",
                active."IsDemo",
                active."IsTrial"
            FROM "vw_active_tenant_license" active
            LEFT JOIN (
                SELECT "TenantId", COUNT(*) AS "UsedStores"
                FROM "Stores"
                WHERE "IsActive" = true
                GROUP BY "TenantId"
            ) stores ON stores."TenantId" = active."TenantId"
            LEFT JOIN (
                SELECT tu."TenantId", COUNT(*) AS "UsedUsers"
                FROM "TenantUsers" tu
                JOIN "Users" u ON u."Id" = tu."UserId"
                WHERE tu."IsActive" = true
                  AND u."IsActive" = true
                  AND u."PlatformRole" = 'User'
                GROUP BY tu."TenantId"
            ) users ON users."TenantId" = active."TenantId"
            LEFT JOIN (
                SELECT "LicenseId", COUNT(*) AS "UsedDevices"
                FROM "LicenseActivations"
                WHERE "IsActive" = true
                GROUP BY "LicenseId"
            ) devices ON devices."LicenseId" = active."LicenseId";

            CREATE OR REPLACE VIEW "vw_store_sellable_variants" AS
            SELECT
                pv."Id" AS "VariantId",
                sp."Id" AS "StoreProductId",
                p."Id" AS "ProductId",
                p."TenantId",
                sp."StoreId",
                p."Name" AS "ProductName",
                pv."Label" AS "VariantLabel",
                pv."Barcode",
                pv."SKU",
                pv."Price",
                sl."QuantityOnHand",
                (
                    p."IsActive" = true
                    AND sp."IsActive" = true
                    AND COALESCE(pv."IsActive", true) = true
                    AND p."DeletedAt" IS NULL
                    AND sp."DeletedAt" IS NULL
                    AND pv."DeletedAt" IS NULL
                ) AS "IsSellable",
                GREATEST(
                    COALESCE(p."UpdatedAt", p."CreatedAt", now()),
                    COALESCE(sp."UpdatedAt", sp."CreatedAt", now()),
                    COALESCE(pv."UpdatedAt", pv."CreatedAt", now()),
                    COALESCE(sl."UpdatedAt", now())
                ) AS "UpdatedAt"
            FROM "ProductVariants" pv
            JOIN "StoreProducts" sp ON sp."Id" = pv."StoreProductId"
            JOIN "Products" p ON p."Id" = sp."ProductId"
            LEFT JOIN "StockLevels" sl
                ON sl."VariantId" = pv."Id"
               AND sl."StoreId" = sp."StoreId"
               AND sl."TenantId" = p."TenantId";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP VIEW IF EXISTS "vw_store_sellable_variants";
            DROP VIEW IF EXISTS "vw_cash_register_session_summary";
            DROP VIEW IF EXISTS "vw_tenant_license_usage";
            DROP VIEW IF EXISTS "vw_active_tenant_license";
            """);
    }
}
