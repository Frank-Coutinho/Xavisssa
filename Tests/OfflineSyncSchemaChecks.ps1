param(
    [string]$Root = (Resolve-Path "$PSScriptRoot\..").Path
)

$ErrorActionPreference = "Stop"

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Message
    )

    $fullPath = Join-Path $Root $Path
    if (!(Test-Path -LiteralPath $fullPath)) {
        throw "Missing file: $Path"
    }

    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

Assert-FileContains "Database\Xavissa.Database\Models\EntityContracts.cs" "interface IOfflineSyncEntity" "Remote sync contract is missing."
Assert-FileContains "Database\Xavissa.Database\XavissaDbContect.cs" "ConfigureSyncMetadata<\s*Sale\s*>" "Sale sync metadata mapping is missing."
Assert-FileContains "Database\Xavissa.Database\XavissaDbContect.cs" "ApplySyncInfo" "Remote SaveChanges sync metadata generation is missing."
Assert-FileContains "Database\Xavissa.Database\Migrations\20260513090000_AddOfflineSyncMetadata.cs" "gen_random_uuid" "Remote sync metadata migration does not backfill UUIDs."
Assert-FileContains "Frontend\Xavissa.Frontend\Infrastructure\LocalDatabase\Data\Repositories\LocalDbSchema.cs" "CurrentSchemaVersion = 7" "Local SQLite schema version was not bumped."
Assert-FileContains "Frontend\Xavissa.Frontend\Infrastructure\LocalDatabase\Data\LocalBdContext.cs" "BackfillOfflineSyncMetadataAsync" "Local SQLite SyncId backfill is missing."
Assert-FileContains "Frontend\Xavissa.Frontend\Modules\Sales\Infrastructure\Repositories\SaleRepository.cs" "SyncId = sale\.SyncId" "Sale upload does not include SyncId."
Assert-FileContains "Backend\Xavissa.Backend\Modules\Synchronization\Application\SyncService.cs" "FirstOrDefaultAsync\(\s*sale =>\s*sale\.SyncId == dto\.SyncId" "Sale upload is not idempotent by SyncId."
Assert-FileContains "Backend\Xavissa.Backend\Infrastructure\Authentication\Security\RlsContextService.cs" "set_config\('app\.current_user_id'" "Backend RLS context service is missing scoped app context setup."

Write-Host "Offline sync schema checks passed."
