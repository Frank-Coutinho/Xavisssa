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

Assert-FileContains "Frontend/Xavissa.Frontend/Modules/Licensing/Services/IDemoApiClient.cs" "StartDemoSessionAsync"
Assert-FileContains "Frontend/Xavissa.Frontend/Modules/Licensing/Services/IDemoStateService.cs" "DemoSessionState"
Assert-FileContains "Frontend/Xavissa.Frontend/Modules/Licensing/Services/DemoCleanupService.cs" "Workspaces.*Demo"
Assert-FileContains "Frontend/Xavissa.Frontend/Modules/Licensing/Services/DemoWorkspaceSeeder.cs" "Loja Demo Xavissa"
Assert-FileContains "Frontend/Xavissa.Frontend/Modules/Licensing/Services/DemoWorkspaceSeeder.cs" "Arroz 5kg"
Assert-FileContains "Frontend/Xavissa.Frontend/appsettings.json" '"DemoEnabled": false'
Assert-FileContains "Backend/Xavissa.Backend/appsettings.json" '"EnableDemos": false'
Assert-FileContains "Backend/Xavissa.Backend/Modules/Demo/Endpoints/DemoController.cs" "Demo mode is currently disabled"
Assert-FileDoesNotContain "Frontend/Xavissa.Frontend/Modules/Licensing/Views/LicenseActivationView.axaml" "Try Demo"
Assert-FileDoesNotContain "Frontend/Xavissa.Frontend/Modules/Identity/Views/LoginView.axaml" "TryDemoCommand"
Assert-FileDoesNotContain "Frontend/Xavissa.Frontend/Modules/Identity/Views/LoginView.axaml" "ActivateLicenseCommand"
Assert-FileContains "Frontend/Xavissa.Frontend/Infrastructure/Printing/ReceiptBuilder.cs" "DEMO RECEIPT"
Assert-FileContains "Backend/Xavissa.Backend/Modules/Demo/Endpoints/DemoController.cs" "HttpPost\(""validate""\)"
Assert-FileContains "Backend/Xavissa.Backend/Modules/Demo/Application/DemoService.cs" "AddMinutes\(60\)"

Write-Host "Dormant demo mode architecture checks passed."
