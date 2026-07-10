using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using ReactiveUI;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    /// <summary>
    /// HomeViewModel
    /// - Handles the main POS screen
    /// - Manages product loading, cart, totals, discounts, payment
    /// - Creates Sale + SaleItems (works offline & online)
    /// - Prints receipt via IPrinterService
    /// </summary>
    public class HomeViewModel : ViewModelBase, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; } = new();

        private readonly IProductRepository _productRepo;
        private readonly ISaleRepository _saleRepo;
        private readonly IPrinterService _printer;
        private readonly INotificationService _notify;
        private readonly IConnectivityService _net;
        private readonly IAuthService _auth;
        private readonly IBackgroundSyncService _backgroundSync;
        private readonly IBarcodeScannerInputService _scannerInput;
        private readonly ILocalizationService _localization;
        private readonly IDemoStateService _demoState;

        // ================================
        // DATA COLLECTIONS
        // ================================
        private readonly List<Product> _allProducts = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<CartLine> CartItems { get; } = new();
        public ObservableCollection<string> CategoryFilters { get; } = new();
        private DispatcherTimer? _filterDebounceTimer;

        private bool _isLoadingProducts;
        public bool IsLoadingProducts
        {
            get => _isLoadingProducts;
            private set
            {
                this.RaiseAndSetIfChanged(ref _isLoadingProducts, value);
                this.RaisePropertyChanged(nameof(ShowProductSkeleton));
                this.RaisePropertyChanged(nameof(ShowProductEmptyState));
            }
        }

        public bool ShowProductSkeleton => IsLoadingProducts && Products.Count == 0;
        public bool HasProducts => Products.Count > 0;
        public bool ShowProductEmptyState => !IsLoadingProducts && Products.Count == 0;
        public bool IsCartEmpty => CartItems.Count == 0;
        public int CartItemCount => CartItems.Sum(item => item.Quantity);
        public int LowStockProductCount => Products.Count(product => product.StockQuantity <= 5);
        public string ActiveStoreDisplayName => _auth.AllowedStores.FirstOrDefault(store => store.Id == _auth.SelectedStoreId)?.Name
            ?? _localization.GetString("Loc.SelectStoreBeforeProducts");
        public string LocalDataStatusText => _net.IsOnline()
            ? _localization.GetString("Loc.OnlineAndUpToDate")
            : _localization.GetString("Loc.OfflineCache");
        public bool IsOfflineMode => !_net.IsOnline();
        public bool CanFinalizeSale => CartItems.Count > 0 && _auth.CanUsePOS && _auth.SelectedStoreId.HasValue;
        public string FinalizeDisabledReason => CartItems.Count == 0
            ? _localization.GetString("Loc.AddProductsBeforeFinalizing")
            : !_auth.SelectedStoreId.HasValue
                ? _localization.GetString("Loc.SelectStoreBeforeSelling")
                : !_auth.CanUsePOS
                    ? _localization.GetString("Loc.RoleCannotFinalizeSale")
                    : string.Empty;
        public string ProductEmptyTitle => HasActiveFilters
            ? _localization.GetString("Loc.NoProductsMatchFilters")
            : _localization.GetString("Loc.NoProductsInStore");
        public string ProductEmptyMessage => _auth.SelectedStoreId.HasValue
            ? _localization.GetString("Loc.TryChangingProductFilters")
            : _localization.GetString("Loc.SelectStoreBeforeProducts");

        private string _scannerStatusText = string.Empty;
        public string ScannerStatusText
        {
            get => string.IsNullOrWhiteSpace(_scannerStatusText)
                ? _localization.GetString("Loc.ScanBarcodeOrSearchProduct")
                : _scannerStatusText;
            private set => this.RaiseAndSetIfChanged(ref _scannerStatusText, value);
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchText, value);
                QueueProductFilter();
            }
        }

        private string _selectedCategoryFilter = string.Empty;
        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCategoryFilter, value);
                QueueProductFilter();
            }
        }

        private string _minPriceFilter = "";
        public string MinPriceFilter
        {
            get => _minPriceFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _minPriceFilter, value);
                QueueProductFilter();
            }
        }

        private string _maxPriceFilter = "";
        public string MaxPriceFilter
        {
            get => _maxPriceFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _maxPriceFilter, value);
                QueueProductFilter();
            }
        }

        private string _manualBarcodeInput = string.Empty;
        public string ManualBarcodeInput
        {
            get => _manualBarcodeInput;
            set => this.RaiseAndSetIfChanged(ref _manualBarcodeInput, value);
        }

        private bool _isProductGridView = true;
        public bool IsProductGridView
        {
            get => _isProductGridView;
            set
            {
                if (_isProductGridView == value)
                    return;

                this.RaiseAndSetIfChanged(ref _isProductGridView, value);
                this.RaisePropertyChanged(nameof(IsProductListView));
            }
        }

        public bool IsProductListView => !IsProductGridView;

        // ================================
        // CART SELECTION
        // ================================
        private int _selectedCartIndex = -1;
        public int SelectedCartIndex
        {
            get => _selectedCartIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCartIndex, value);
                SelectedCartLine = value >= 0 && value < CartItems.Count ? CartItems[value] : null;
            }
        }

        private CartLine? _selectedCartLine;
        public CartLine? SelectedCartLine
        {
            get => _selectedCartLine;
            set => this.RaiseAndSetIfChanged(ref _selectedCartLine, value);
        }

        // ================================
        // PAYMENT UI FIELDS
        // ================================
        private decimal _discount;
        public decimal Discount
        {
            get => _discount;
            set
            {
                this.RaiseAndSetIfChanged(ref _discount, value);
                RaiseTotals();
            }
        }

        private decimal _tenderedAmount;
        public decimal TenderedAmount
        {
            get => _tenderedAmount;
            set
            {
                this.RaiseAndSetIfChanged(ref _tenderedAmount, value);
                RaiseTotals();
            }
        }

        public ObservableCollection<PaymentMethodOption> PaymentMethods { get; } = new();

        private PaymentMethodOption? _selectedPaymentMethod;
        public PaymentMethodOption? SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set => this.RaiseAndSetIfChanged(ref _selectedPaymentMethod, value);
        }

        // ================================
        // TOTALS
        // ================================
        public decimal Subtotal => CartItems.Sum(c => c.Total);
        public decimal Total => Subtotal;
        public decimal FinalTotal => Math.Max(Total - Discount, 0);
        public decimal ChangeAmount => Math.Max(TenderedAmount - FinalTotal, 0);

        // ================================
        // COMMANDS
        // ================================
        public ReactiveCommand<Product, Unit> AddToCartCommand { get; }
        public ReactiveCommand<CartLine, Unit> RemoveFromCartCommand { get; }
        public ReactiveCommand<CartLine, Unit> IncreaseQuantityCommand { get; }
        public ReactiveCommand<CartLine, Unit> DecreaseQuantityCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearCartCommand { get; }
        public ReactiveCommand<Unit, Unit> FinalizeSaleCommand { get; }

        public ReactiveCommand<string, Unit> ScanBarcodeCommand { get; }
        public ReactiveCommand<Unit, Unit> SubmitManualBarcodeCommand { get; }
        public ReactiveCommand<string, Unit> AddProductByNumberCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearProductFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> SetProductGridViewCommand { get; }
        public ReactiveCommand<Unit, Unit> SetProductListViewCommand { get; }

        // Keyboard navigation
        public ReactiveCommand<Unit, Unit> MoveSelectionUpCommand { get; }
        public ReactiveCommand<Unit, Unit> MoveSelectionDownCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveSelectedItemCommand { get; }
        public ReactiveCommand<Unit, Unit> IncreaseSelectedQuantityCommand { get; }
        public ReactiveCommand<Unit, Unit> DecreaseSelectedQuantityCommand { get; }

        // ================================
        // CONSTRUCTOR
        // ================================
        public HomeViewModel(
            IProductRepository productRepo,
            ISaleRepository saleRepo,
            IPrinterService printer,
            INotificationService notify,
            IConnectivityService net,
            IAuthService auth,
            IBackgroundSyncService backgroundSync,
            IBarcodeScannerInputService scannerInput,
            ILocalizationService localization,
            IDemoStateService demoState
        )
        {
            _productRepo = productRepo;
            _saleRepo = saleRepo;
            _printer = printer;
            _notify = notify;
            _net = net;
            _auth = auth;
            _backgroundSync = backgroundSync;
            _scannerInput = scannerInput;
            _localization = localization;
            _demoState = demoState;

            _filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _filterDebounceTimer.Tick += (_, _) =>
            {
                _filterDebounceTimer.Stop();
                ApplyProductFilters();
            };

            CartItems.CollectionChanged += (_, _) => RaiseCartState();
            RebuildLocalizedOptions();
            _localization.LanguageChanged += OnLanguageChanged;

            // -----------------------------
            // CART COMMANDS
            // -----------------------------
            AddToCartCommand = ReactiveCommand.Create<Product>(p =>
            {
                AddToCart(p);
            });
            RemoveFromCartCommand = ReactiveCommand.Create<CartLine>(RemoveFromCart);
            IncreaseQuantityCommand = ReactiveCommand.Create<CartLine>(IncreaseQuantity);
            DecreaseQuantityCommand = ReactiveCommand.Create<CartLine>(DecreaseQuantity);

            ClearCartCommand = ReactiveCommand.Create(ClearCart);
            FinalizeSaleCommand = ReactiveCommand.CreateFromTask(FinalizeSaleAsync);

            // -----------------------------
            // PRODUCT SCANNING
            // -----------------------------
            ScanBarcodeCommand = ReactiveCommand.CreateFromTask<string>(ScanBarcodeAsync);
            SubmitManualBarcodeCommand = ReactiveCommand.CreateFromTask(SubmitManualBarcodeAsync);

            AddProductByNumberCommand = ReactiveCommand.Create<string>(param =>
            {
                if (int.TryParse(param, out var idx))
                {
                    if (idx >= 0 && idx < Products.Count)
                        AddToCart(Products[idx]);
                }
            });
            ClearProductFiltersCommand = ReactiveCommand.Create(() =>
            {
                SearchText = "";
                SelectedCategoryFilter = AllCategoryLabel;
                MinPriceFilter = "";
                MaxPriceFilter = "";
                ApplyProductFilters();
            });
            SetProductGridViewCommand = ReactiveCommand.Create(() =>
            {
                IsProductGridView = true;
            });
            SetProductListViewCommand = ReactiveCommand.Create(() =>
            {
                IsProductGridView = false;
            });

            // -----------------------------
            // KEYBOARD CART NAVIGATION
            // -----------------------------
            MoveSelectionUpCommand = ReactiveCommand.Create(() =>
            {
                if (CartItems.Any())
                    SelectedCartIndex = Math.Max(0, SelectedCartIndex - 1);
            });

            MoveSelectionDownCommand = ReactiveCommand.Create(() =>
            {
                if (CartItems.Any())
                    SelectedCartIndex = Math.Min(CartItems.Count - 1, SelectedCartIndex + 1);
            });

            RemoveSelectedItemCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedCartIndex >= 0)
                    RemoveFromCart(CartItems[SelectedCartIndex]);
            });

            IncreaseSelectedQuantityCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedCartLine != null)
                    IncreaseQuantity(SelectedCartLine);
            });

            DecreaseSelectedQuantityCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedCartLine != null)
                {
                    if (SelectedCartLine.Quantity > 1)
                        SelectedCartLine.Quantity--;
                    else
                        RemoveFromCart(SelectedCartLine);
                    RaiseTotals();
                }
            });

            // Auto-load products
            // _ = LoadProductsAsync();
            this.WhenActivated(disposables =>
            {
                LoadProductsAsync().ToObservable().Subscribe().DisposeWith(disposables);
            });
        }

        // ================================
        // LOAD PRODUCTS
        // ================================
        public async Task LoadProductsAsync()
        {
            if (IsLoadingProducts)
                return;

            IsLoadingProducts = true;
            try
            {
                List<Product> list;
                if (_auth.SelectedStoreId.HasValue)
                {
                    list = await _productRepo.GetSellableProductsAsync(_auth.SelectedStoreId.Value);
                }
                else
                {
                    list = new List<Product>();
                }

                _allProducts.Clear();
                _allProducts.AddRange(list);

                BuildCategoryFilters();
                ApplyProductFilters();
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadProductsAsync error: " + ex.Message);
                _notify.Show(_localization.GetString("Loc.FailedToLoadProducts"));
            }
            finally
            {
                IsLoadingProducts = false;
            }
        }

        // ================================
        // BARCODE SCANNING
        // ================================
        public async Task ScanBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return;

            barcode = barcode.Trim();

            Product? product = null;

            try
            {
                product = await _productRepo.GetByBarcodeAsync(barcode);
            }
            catch
            {
            }

            // fallback to local list
            product ??= _allProducts.FirstOrDefault(p => p.Barcode == barcode);

            if (product == null || !IsSellableInCurrentStore(product))
            {
                ScannerStatusText = _localization.GetString("Loc.ProductNotFound");
                _notify.Show(_localization.GetString("Loc.ProductNotFound"));
                return;
            }

            // Delegate stock validation to AddToCart
            var added = AddToCart(product);

            if (added)
            {
                ScannerStatusText = string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.ProductAdded"), product.Name);
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.ProductAdded"), product.Name));
            }
        }

        public async Task<bool> HasBarcodeMatchAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return false;

            barcode = barcode.Trim();

            var localMatch = _allProducts.FirstOrDefault(p => p.Barcode == barcode);
            if (localMatch != null)
                return IsSellableInCurrentStore(localMatch);

            try
            {
                var product = await _productRepo.GetByBarcodeAsync(barcode);
                return product != null && IsSellableInCurrentStore(product);
            }
            catch
            {
                return false;
            }
        }

        public bool ProcessScannerTextInput(TextInputEventArgs e, DateTimeOffset timestamp) =>
            _scannerInput.ProcessTextInput(e, timestamp);

        public async Task<bool> TryProcessScannerTerminatorAsync(KeyEventArgs e, DateTimeOffset timestamp)
        {
            if (!_scannerInput.TryCompleteScan(e, timestamp, out var barcode))
                return false;

            await ScanBarcodeAsync(barcode);
            return true;
        }

        public void ResetScannerInput() => _scannerInput.Reset();

        public void NotifyManualTextEntryFocused() => _scannerInput.NotifyManualFocus();

        public async Task SubmitManualBarcodeAsync()
        {
            var barcode = ManualBarcodeInput?.Trim();
            if (string.IsNullOrWhiteSpace(barcode))
                return;

            await ScanBarcodeAsync(barcode);
            ManualBarcodeInput = string.Empty;
        }

        // ================================
        // CART LOGIC
        // ================================
        private bool AddToCart(Product product)
        {
            if (!_auth.CanUsePOS || !_auth.SelectedStoreId.HasValue)
            {
                _notify.Show("Select a store before selling.", NotificationType.Warning);
                return false;
            }

            var line = CartItems.FirstOrDefault(c => GetCartProductKey(c.Product) == GetCartProductKey(product));

            var quantityInCart = line?.Quantity ?? 0;

            // 🚨 Proper stock validation
            if (quantityInCart >= product.StockQuantity)
            {
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.NotEnoughStockFor"), product.Name));
                return false;
            }

            if (line != null)
                line.Quantity++;
            else
                CartItems.Add(new CartLine(product));

            if (SelectedCartIndex == -1)
                SelectedCartIndex = 0;

            RaiseTotals();
            RaiseCartState();
            return true;
        }

        private void RemoveFromCart(CartLine line)
        {
            CartItems.Remove(line);

            if (CartItems.Count == 0)
                SelectedCartIndex = -1;
            else
                SelectedCartIndex = Math.Min(SelectedCartIndex, CartItems.Count - 1);

            RaiseTotals();
            RaiseCartState();
        }

        private void IncreaseQuantity(CartLine line)
        {
            if (line.Quantity >= line.Product.StockQuantity)
            {
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.NotEnoughStockFor"), line.Product.Name));
                return;
            }

            line.Quantity++;
            RaiseTotals();
            RaiseCartState();
        }

        private void DecreaseQuantity(CartLine line)
        {
            if (line.Quantity > 1)
                line.Quantity--;
            else
                RemoveFromCart(line);
            RaiseTotals();
            RaiseCartState();
        }

        private void ClearCart()
        {
            CartItems.Clear();
            Discount = 0;
            TenderedAmount = 0;
            RaiseTotals();
            RaiseCartState();
        }

        private void RaiseTotals()
        {
            this.RaisePropertyChanged(nameof(Subtotal));
            this.RaisePropertyChanged(nameof(Total));
            this.RaisePropertyChanged(nameof(FinalTotal));
            this.RaisePropertyChanged(nameof(ChangeAmount));
            this.RaisePropertyChanged(nameof(CanFinalizeSale));
            this.RaisePropertyChanged(nameof(FinalizeDisabledReason));
        }

        private void RaiseCartState()
        {
            this.RaisePropertyChanged(nameof(IsCartEmpty));
            this.RaisePropertyChanged(nameof(CartItemCount));
            this.RaisePropertyChanged(nameof(CanFinalizeSale));
            this.RaisePropertyChanged(nameof(FinalizeDisabledReason));
            RaiseTotals();
        }

        private void BuildCategoryFilters()
        {
            var categories = _allProducts
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c.ToString())
                .Select(c => c.ToString());

            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategoryLabel);
            foreach (var c in categories)
                CategoryFilters.Add(c);

            if (!CategoryFilters.Contains(SelectedCategoryFilter))
                SelectedCategoryFilter = AllCategoryLabel;
        }

        private void ApplyProductFilters()
        {
            IEnumerable<Product> query = _allProducts;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim();
                query = query.Where(p =>
                    (!string.IsNullOrWhiteSpace(p.Name))
                        && p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(p.Barcode))
                        && p.Barcode.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(p.Code))
                        && p.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                );
            }

            if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter) && !SelectedCategoryFilter.Equals(AllCategoryLabel, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => string.Equals(p.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (TryParsePrice(MinPriceFilter, out var minPrice))
                query = query.Where(p => p.Price >= minPrice);

            if (TryParsePrice(MaxPriceFilter, out var maxPrice))
                query = query.Where(p => p.Price <= maxPrice);

            Products.Clear();
            foreach (var p in query.OrderBy(p => p.Name))
                Products.Add(p);

            this.RaisePropertyChanged(nameof(HasProducts));
            this.RaisePropertyChanged(nameof(LowStockProductCount));
            this.RaisePropertyChanged(nameof(ShowProductSkeleton));
            this.RaisePropertyChanged(nameof(ShowProductEmptyState));
            this.RaisePropertyChanged(nameof(ProductEmptyTitle));
            this.RaisePropertyChanged(nameof(ProductEmptyMessage));
        }

        private void QueueProductFilter()
        {
            if (_filterDebounceTimer == null)
            {
                ApplyProductFilters();
                return;
            }

            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Start();
        }

        private bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchText)
            || !string.IsNullOrWhiteSpace(MinPriceFilter)
            || !string.IsNullOrWhiteSpace(MaxPriceFilter)
            || (!string.IsNullOrWhiteSpace(SelectedCategoryFilter)
                && !SelectedCategoryFilter.Equals(AllCategoryLabel, StringComparison.OrdinalIgnoreCase));

        private static bool TryParsePrice(string input, out decimal value)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                value = default;
                return false;
            }

            return decimal.TryParse(
                    input.Trim(),
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out value
                )
                || decimal.TryParse(
                    input.Trim(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out value
                );
        }

        // ================================
        // FINALIZE SALE
        // ================================
        private async Task FinalizeSaleAsync()
        {
            if (!await EnsureDemoCanWriteAsync())
                return;

            if (!CartItems.Any())
            {
                _notify.Show(_localization.GetString("Loc.CartIsEmpty"));
                return;
            }

            if (!_auth.CanUsePOS || !_auth.SelectedStoreId.HasValue)
            {
                _notify.Show("Select a store before finalizing a sale.", NotificationType.Warning);
                return;
            }

            if (!await ValidateStockBeforeFinalizeAsync())
                return;

            

            var sale = new Sale
            {
                TenantId = _auth.SelectedTenantId ?? 0,
                StoreId = _auth.SelectedStoreId ?? 0,
                Timestamp = DateTime.UtcNow,
                Discount = Discount,
                TotalAmount = FinalTotal,
                TotalPaid = TenderedAmount,
                PaymentSummary = SelectedPaymentMethod?.Label ?? SelectedPaymentMethod?.Code ?? PaymentMethodCodes.Cash,
                PaymentStatus = TenderedAmount >= FinalTotal ? "Paid" : "Partial",
                ChangeGiven = ChangeAmount,
                UpdatedAt = DateTime.UtcNow,
                Synced = false,
            };

            sale.Payments.Add(new SalePayment
            {
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                PaymentMethod = SelectedPaymentMethod?.Code ?? PaymentMethodCodes.Cash,
                Amount = TenderedAmount > 0 ? TenderedAmount : FinalTotal,
                CreatedAt = sale.Timestamp,
            });

            // 🔑 Attach items FIRST
            foreach (var c in CartItems)
            {
                sale.Items.Add(
                    new SaleItem
                    {
                        TenantId = sale.TenantId,
                        StoreId = sale.StoreId,
                        ProductId = c.Product.Id,
                        VariantId = c.Product.VariantId,
                        ProductName = c.Product.Name,
                        Quantity = c.Quantity,
                        UnitPrice = c.Product.Price,
                    }
                );
            }

            try
            {
                // ✅ ONE call only — offline-first internally
                var created = await _saleRepo.CreateAsync(sale);
                Console.WriteLine(
                    $"🧪 AFTER CREATE | SaleId={created.Id}, Items={created.Items?.Count ?? -1}"
                );

                var sellableStockUpdates = CartItems
                    .Where(line => line.Product.VariantId > 0)
                    .Select(line => (VariantId: line.Product.VariantId, Quantity: line.Quantity))
                    .ToList();
                await _productRepo.DecreaseSellableVariantStockRangeAsync(sellableStockUpdates);

                var baseStockUpdates = CartItems
                    .Where(line => line.Product.Id > 0)
                    .Select(line => (ProductId: line.Product.Id, Quantity: line.Quantity))
                    .ToList();
                await _productRepo.DecreaseStockRangeAsync(baseStockUpdates);

                foreach (var line in CartItems)
                    ApplyLocalStockReduction(line.Product.Id, line.Quantity, line.Product.VariantId);

                var receiptModel = new ReceiptModel
                {
                    Timestamp = sale.Timestamp,
                    Subtotal = sale.Items.Sum(i => i.UnitPrice * i.Quantity),
                    Discount = sale.Discount,
                    FinalTotal = sale.TotalAmount,
                    PaymentMethod = sale.PaymentSummary,
                    IsDemo = _demoState.IsDemoActive,
                    Items = sale
                        .Items.Select(i => new ReceiptItem
                        {
                            ProductName = i.ProductName,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                        })
                        .ToList(),
                };

                ClearCart();
                await _demoState.TrackEventAsync("SaleCreated", "Sale", created.Id.ToString(), "Fake sale created in demo mode.");

                try
                {
                    var builder = new ReceiptBuilder(receiptModel, _printer);
                    builder.Print();
                    _notify.Show(_localization.GetString("Loc.SaleCompleted"));
                }
                catch (Exception printEx)
                {
                    Console.WriteLine("FinalizeSaleAsync print error: " + printEx.Message);
                    _notify.Show(_localization.GetString("Loc.SaleCompletedReceiptFailed"));
                }

                if (_net.IsOnline() && !_demoState.IsDemoActive)
                    _backgroundSync.RequestSync(BackgroundSyncReason.SaleCompleted);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FinalizeSaleAsync error: " + ex.Message);
                _notify.Show(_localization.GetString("Loc.FailedToFinalizeSale"));
            }
        }

        private async Task<bool> EnsureDemoCanWriteAsync()
        {
            await _demoState.CheckExpirationAsync();
            if (!_demoState.IsExpired)
                return true;

            _notify.Show("Demo session expired. Activate a license to use this in production.", NotificationType.Warning, 5000);
            return false;
        }

        public void ResetForStoreChange()
        {
            ClearCart();
            SelectedCartIndex = -1;
            SelectedCartLine = null;
            SearchText = string.Empty;
            SelectedCategoryFilter = AllCategoryLabel;
            MinPriceFilter = string.Empty;
            MaxPriceFilter = string.Empty;
            _allProducts.Clear();
            Products.Clear();
            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategoryLabel);
            RaiseCartState();
        }

        private void ApplyLocalStockReduction(int productId, int quantity, int? variantId = null)
        {
            foreach (var product in _allProducts.Where(p =>
                variantId.HasValue && variantId.Value > 0
                    ? p.VariantId == variantId.Value
                    : p.Id == productId))
                product.StockQuantity = Math.Max(0, product.StockQuantity - quantity);

            foreach (var product in Products.Where(p =>
                variantId.HasValue && variantId.Value > 0
                    ? p.VariantId == variantId.Value
                    : p.Id == productId))
                product.StockQuantity = Math.Max(0, product.StockQuantity - quantity);
        }

        private async Task<bool> ValidateStockBeforeFinalizeAsync()
        {
            var productsByVariantId = _allProducts
                .Where(product => product.VariantId > 0)
                .GroupBy(product => product.VariantId)
                .ToDictionary(group => group.Key, group => group.First());
            var productsById = _allProducts
                .GroupBy(product => product.Id)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var line in CartItems)
            {
                Product? currentProduct = null;
                if (line.Product.VariantId > 0)
                    productsByVariantId.TryGetValue(line.Product.VariantId, out currentProduct);
                else
                    productsById.TryGetValue(line.Product.Id, out currentProduct);

                currentProduct ??= await _productRepo.GetByIdAsync(line.Product.Id);
                if (currentProduct == null)
                {
                    _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.ProductNoLongerExists"), line.Product.Name));
                    return false;
                }

                line.Product.StockQuantity = currentProduct.StockQuantity;

                if (line.Quantity > currentProduct.StockQuantity)
                {
                    _notify.Show(string.Format(
                        CultureInfo.CurrentCulture,
                        _localization.GetString("Loc.InsufficientStockRequestedAvailable"),
                        currentProduct.Name,
                        line.Quantity,
                        currentProduct.StockQuantity));
                    await LoadProductsAsync();
                    return false;
                }
            }

            return true;
        }

        private async Task<List<Product>> BuildSellableProductsAsync(List<Product> baseProducts)
        {
            var selectedStoreId = _auth.SelectedStoreId;
            if (!selectedStoreId.HasValue)
            {
                return baseProducts
                    .Where(product => product.IsActive)
                    .OrderBy(product => product.Name)
                    .ThenBy(product => product.Label)
                    .ToList();
            }

            var assignedProducts = baseProducts
                .Where(product => product.IsActive && product.StoreId == selectedStoreId.Value)
                .ToList();

            var variantTasks = assignedProducts
                .Where(product => product.OnlineId > 0)
                .Select(async product => new
                {
                    Product = product,
                    Variants = await _productRepo.GetVariantsAsync(product.OnlineId, selectedStoreId.Value),
                })
                .ToList();

            var variantResults = await Task.WhenAll(variantTasks);
            var variantsByOnlineId = variantResults.ToDictionary(result => result.Product.OnlineId, result => result.Variants);

            var sellableProducts = new List<Product>(assignedProducts.Count);

            foreach (var baseProduct in assignedProducts)
            {
                if (baseProduct.OnlineId <= 0)
                {
                    if (baseProduct.VariantId > 0)
                        sellableProducts.Add(baseProduct);

                    continue;
                }

                if (!variantsByOnlineId.TryGetValue(baseProduct.OnlineId, out var variants) || variants.Count == 0)
                {
                    if (baseProduct.VariantId > 0)
                        sellableProducts.Add(baseProduct);

                    continue;
                }

                sellableProducts.AddRange(
                    variants
                        .Where(variant => variant.IsActive)
                        .Select(variant => MapSellableVariant(baseProduct, variant)));
            }

            return sellableProducts
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Label)
                .ToList();
        }

        private static Product MapSellableVariant(Product baseProduct, ProductVariantRecord variant)
        {
            return new Product
            {
                Id = baseProduct.Id,
                OnlineId = baseProduct.OnlineId,
                TenantId = baseProduct.TenantId,
                VariantId = variant.Id,
                AssignmentId = variant.AssignmentId,
                StoreId = variant.StoreId,
                CategoryId = baseProduct.CategoryId,
                Code = baseProduct.Code,
                Barcode = variant.Barcode,
                Name = baseProduct.Name,
                SKU = variant.SKU,
                Label = variant.Label,
                Description = baseProduct.Description,
                Category = baseProduct.Category,
                Brand = baseProduct.Brand,
                Price = variant.Price,
                StockQuantity = variant.StockQuantity,
                IsActive = variant.IsActive,
                VariantCount = 1,
                CreatedAt = baseProduct.CreatedAt,
                UpdatedAt = baseProduct.UpdatedAt,
            };
        }

        private static string GetCartProductKey(Product product) =>
            product.VariantId > 0 ? $"variant:{product.VariantId}" : $"product:{product.Id}";

        private bool IsSellableInCurrentStore(Product product)
        {
            if (!product.IsActive || product.VariantId <= 0)
                return false;

            return !_auth.SelectedStoreId.HasValue || product.StoreId == _auth.SelectedStoreId.Value;
        }

        private string AllCategoryLabel => _localization.GetString("Loc.All");

        private void RebuildLocalizedOptions()
        {
            PaymentMethods.Clear();
            PaymentMethods.Add(new PaymentMethodOption(PaymentMethodCodes.Cash, _localization.GetString("Loc.PaymentCash")));
            PaymentMethods.Add(new PaymentMethodOption(PaymentMethodCodes.Card, _localization.GetString("Loc.PaymentCard")));
            PaymentMethods.Add(new PaymentMethodOption(PaymentMethodCodes.MobilePayment, _localization.GetString("Loc.PaymentMobile")));
            PaymentMethods.Add(new PaymentMethodOption(PaymentMethodCodes.Other, _localization.GetString("Loc.PaymentOther")));

            SelectedPaymentMethod = PaymentMethods.FirstOrDefault(option => option.Code == SelectedPaymentMethod?.Code)
                ?? PaymentMethods.FirstOrDefault(option => option.Code == PaymentMethodCodes.Cash)
                ?? PaymentMethods.FirstOrDefault();

            var previousSelection = SelectedCategoryFilter;
            BuildCategoryFilters();
            if (string.IsNullOrWhiteSpace(previousSelection) || previousSelection.Equals(AllCategoryLabel, StringComparison.OrdinalIgnoreCase))
                SelectedCategoryFilter = AllCategoryLabel;
            else if (CategoryFilters.Contains(previousSelection))
                SelectedCategoryFilter = previousSelection;
            this.RaisePropertyChanged(nameof(ScannerStatusText));
            this.RaisePropertyChanged(nameof(ProductEmptyTitle));
            this.RaisePropertyChanged(nameof(ProductEmptyMessage));
            this.RaisePropertyChanged(nameof(FinalizeDisabledReason));
        }

        private void OnLanguageChanged()
        {
            RebuildLocalizedOptions();
            ApplyProductFilters();
        }

        // ================================
        // CART LINE MODEL
        // ================================
        public class CartLine : INotifyPropertyChanged
        {
            public Product Product { get; }
            private int _quantity = 1;

            public CartLine(Product product)
            {
                Product = product ?? throw new ArgumentNullException(nameof(product));
            }

            public int Quantity
            {
                get => _quantity;
                set
                {
                    if (_quantity == value)
                        return;
                    _quantity = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total)));
                }
            }

            public decimal Total => Product.Price * Quantity;

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public sealed class PaymentMethodOption
        {
            public PaymentMethodOption(string code, string label)
            {
                Code = code;
                Label = label;
            }

            public string Code { get; }
            public string Label { get; }
            public override string ToString() => Label;
        }

        private static class PaymentMethodCodes
        {
            public const string Cash = "Cash";
            public const string Card = "Card";
            public const string MobilePayment = "MobilePayment";
            public const string Other = "Other";
        }
    }
}



