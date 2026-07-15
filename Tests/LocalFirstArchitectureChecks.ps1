param(
    [string]$Root = (Resolve-Path "$PSScriptRoot\..").Path
)

$ErrorActionPreference = "Stop"

function Assert-FileContains {
    param([string]$Path, [string]$Pattern, [string]$Message)
    $content = Get-Content -LiteralPath (Join-Path $Root $Path) -Raw
    if ($content -notmatch $Pattern) { throw $Message }
}

function Assert-FileDoesNotContain {
    param([string]$Path, [string]$Pattern, [string]$Message)
    $content = Get-Content -LiteralPath (Join-Path $Root $Path) -Raw
    if ($content -match $Pattern) { throw $Message }
}

$saleRepository = "Frontend\Xavissa.Frontend\Modules\Sales\Infrastructure\Repositories\SaleRepository.cs"
$offlineSales = "Frontend\Xavissa.Frontend\Modules\Sales\Infrastructure\Repositories\SaleRepositoryOffline.cs"
$homeViewModel = "Frontend\Xavissa.Frontend\Modules\Sales\ViewModels\HomeViewModel.cs"
$backgroundSync = "Frontend\Xavissa.Frontend\Modules\Synchronization\Services\BackgroundSyncService.cs"
$stockAdjustment = "Frontend\Xavissa.Frontend\Modules\Inventory\Services\StockAdjustmentService.cs"

Assert-FileDoesNotContain $saleRepository "_online\.CreateAsync\(sale\)" "Checkout still contains an online-first sale write."
Assert-FileContains $offlineSales "BeginTransactionAsync" "Sale and stock are not protected by a local SQLite transaction."
Assert-FileContains $offlineSales "UPDATE SellableVariants[\s\S]*QuantityOnHand >= \{0\}" "Local sale stock decrement is not guarded against overselling."
Assert-FileContains $homeViewModel "GetLiveAvailabilityAsync" "Critical-stock live validation is missing."
Assert-FileContains $homeViewModel "Math\.Min\(line\.Product\.StockQuantity, serverQuantity\)" "Live stock can overwrite a more conservative local reservation."
Assert-FileContains $backgroundSync "BackgroundSyncReason\.StockAdjusted" "Stock adjustments do not trigger event-based sync."
Assert-FileContains $backgroundSync "ShouldRefreshLocalViews = true" "Periodic stock pulls do not refresh staff warnings."
Assert-FileContains $stockAdjustment "ApplyLocalAsync" "Local-first stock adjustment entry point is missing."
Assert-FileContains $stockAdjustment "sync-apply" "Pending local stock adjustments are not uploaded."
Assert-FileContains "Backend\Xavissa.Backend\Modules\Inventory\Application\StockAdjustmentService.cs" "offlineDifference = line\.NewQuantity - line\.OldQuantity" "Offline stock adjustments overwrite central sales instead of merging as movements."
Assert-FileContains "Backend\Xavissa.Backend\Modules\Synchronization\Endpoints\SyncController.cs" "stock-check" "Live server stock endpoint is missing."
Assert-FileContains "Backend\Xavissa.Backend\Modules\Sales\Application\SalesService.cs" "QuantityOnHand >= itemDto\.Quantity" "Server sale stock update is not atomic."
Assert-FileContains $saleRepository "HandleSaleConflict" "Client sale conflict policy is not invoked."
Assert-FileContains "Frontend\Xavissa.Frontend\Modules\Sales\ViewModels\HistoryViewModel.cs" "STAFF ACTION REQUIRED" "Rejected sale conflicts are not visible in history."

Write-Host "Local-first architecture checks passed."
