$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Assert-FileContains {
    param(
        [string] $Path,
        [string] $Pattern
    )

    $fullPath = Join-Path $root $Path
    if (-not (Test-Path $fullPath)) {
        throw "Expected file '$Path' to exist."
    }

    if (-not (Select-String -Path $fullPath -Pattern $Pattern -Quiet)) {
        throw "Expected '$Path' to contain pattern '$Pattern'."
    }
}

function Assert-FileDoesNotContain {
    param(
        [string] $Path,
        [string] $Pattern
    )

    $fullPath = Join-Path $root $Path
    if ((Test-Path $fullPath) -and (Select-String -Path $fullPath -Pattern $Pattern -Quiet)) {
        throw "Expected '$Path' not to contain pattern '$Pattern'."
    }
}

function Assert-FileMissing {
    param([string] $Path)

    $fullPath = Join-Path $root $Path
    if (Test-Path $fullPath) {
        throw "Expected file '$Path' to be removed."
    }
}

Assert-FileMissing "Backend/Xavissa.Backend/Controllers/LicensingController.cs"
Assert-FileMissing "Backend/Xavissa.Backend/Services/LicenseService.cs"
Assert-FileMissing "Backend/Xavissa.Backend/Services/LicenseKeyService.cs"
Assert-FileMissing "Backend/Xavissa.Backend/DTOs/LicensingDtos.cs"
Assert-FileMissing "Database/Xavissa.Database/Models/License.cs"
Assert-FileMissing "Database/Xavissa.Database/Models/LicensePlan.cs"
Assert-FileMissing "Database/Xavissa.Database/Models/LicenseActivation.cs"
Assert-FileMissing "Database/Xavissa.Database/Models/LicensePayment.cs"
Assert-FileMissing "Database/Xavissa.Database/Models/LicenseUpgradeHistory.cs"
Assert-FileMissing "Database/Xavissa.Database/Models/LicenseValidationLog.cs"

Assert-FileDoesNotContain "Frontend/Xavissa.Frontend/appsettings.json" "HashSecret"
Assert-FileDoesNotContain "Frontend/Xavissa.Frontend/appsettings.json" "CacheSigningSecret"
Assert-FileDoesNotContain "Backend/Xavissa.Backend/appsettings.json" "HashSecret"
Assert-FileDoesNotContain "Backend/Xavissa.Backend/appsettings.json" "CacheSigningSecret"
Assert-FileDoesNotContain "Backend/Xavissa.Backend/Controllers/SalesController.cs" "No active license"
Assert-FileDoesNotContain "Backend/Xavissa.Backend/Controllers/StoresController.cs" "CanCreateStoreAsync"
Assert-FileDoesNotContain "Backend/Xavissa.Backend/Controllers/UserManagementController.cs" "CanCreateTenantUserAsync"

Write-Host "POS no-licensing architecture checks passed."
