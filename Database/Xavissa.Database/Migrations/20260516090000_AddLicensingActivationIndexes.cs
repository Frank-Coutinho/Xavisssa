using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Database.Migrations
{
    public partial class AddLicensingActivationIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ux_license_activations_active_device
                ON public.""LicenseActivations"" (""LicenseId"", ""DeviceFingerprint"")
                WHERE ""IsActive"" = true;

                CREATE INDEX IF NOT EXISTS ix_license_activations_license_active
                ON public.""LicenseActivations"" (""LicenseId"", ""IsActive"");

                CREATE INDEX IF NOT EXISTS ix_license_activations_tenant_active
                ON public.""LicenseActivations"" (""TenantId"", ""IsActive"");

                CREATE INDEX IF NOT EXISTS ix_license_validation_logs_tenant_created
                ON public.""LicenseValidationLogs"" (""TenantId"", ""CreatedAt"" DESC);

                CREATE INDEX IF NOT EXISTS ix_licenses_key_prefix
                ON public.""Licenses"" (""LicenseKeyPrefix"");

                CREATE INDEX IF NOT EXISTS ix_licenses_tenant_status
                ON public.""Licenses"" (""TenantId"", ""Status"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS public.ix_licenses_tenant_status;
                DROP INDEX IF EXISTS public.ix_licenses_key_prefix;
                DROP INDEX IF EXISTS public.ix_license_validation_logs_tenant_created;
                DROP INDEX IF EXISTS public.ix_license_activations_tenant_active;
                DROP INDEX IF EXISTS public.ix_license_activations_license_active;
                DROP INDEX IF EXISTS public.ux_license_activations_active_device;
            ");
        }
    }
}
