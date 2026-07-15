using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly ISyncService _sync;
        private readonly ISaleRepository _saleRepo;
        private readonly IProductRepository _productRepo;
        private readonly IStoreAdminRepository _storeAdminRepo;
        private readonly INotificationService _notify;
        private readonly IConnectivityService _net;
        private readonly IAuthService _auth;
        private readonly ILocalizationService _localization;

        private string _error = string.Empty;
        private bool _isLoading;
        private FilterOptionItem? _selectedFilter;
        private DateTimeOffset? _customDate;
        private bool _isCustomDateEnabled;
        private ObservableCollection<HistorySaleRow> _filtered = new();
        private decimal _total;
        private decimal _average;
        private bool _isSaleDetailsOpen;
        private HistorySaleRow? _selectedSale;
        private bool _isDeleteConfirmationOpen;
        private string _deleteConfirmationTitle = string.Empty;
        private string _deleteConfirmationMessage = string.Empty;
        private HistorySaleRow? _pendingSaleDeletion;
        private bool _isRefundDialogOpen;
        private string _refundReason = string.Empty;
        private HistorySaleLine? _pendingRefundItem;
        private HistorySaleRow? _pendingRefundSale;
        private int _refundQuantity = 1;
        private HistoryStoreFilterOption? _selectedStoreFilter;
        private int _currentOffset;
        private bool _hasMoreSales;
        private Dictionary<int, string>? _storeNameCache;
        private bool _hasLoaded;
        private bool _suppressReloads;
        private const int HistoryPageSize = 100;

        public ObservableCollection<HistorySaleRow> Sales { get; } = new();
        public ObservableCollection<HistoryStoreFilterOption> AvailableStoreFilters { get; } = new();

        public ObservableCollection<HistorySaleRow> FilteredSales
        {
            get => _filtered;
            private set
            {
                this.RaiseAndSetIfChanged(ref _filtered, value);
                RaiseListState();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                this.RaiseAndSetIfChanged(ref _isLoading, value);
                this.RaisePropertyChanged(nameof(ShowHistoryLoading));
                this.RaisePropertyChanged(nameof(ShowHistoryEmptyState));
            }
        }

        public string ErrorMessage
        {
            get => _error;
            set => this.RaiseAndSetIfChanged(ref _error, value);
        }

        public decimal Total
        {
            get => _total;
            private set => this.RaiseAndSetIfChanged(ref _total, value);
        }

        public decimal AverageSaleAmount
        {
            get => _average;
            private set => this.RaiseAndSetIfChanged(ref _average, value);
        }

        public bool CanManageHistoryDeletions => _auth.IsTenantAdmin || _auth.IsStoreManager;
        public bool ShowStoreFilter => _auth.IsTenantAdmin;
        public bool ShowStoreColumn => _auth.IsTenantAdmin;
        public bool IsOnline => _net.IsOnline();
        public bool ShowHistoryLoading => IsLoading && Sales.Count == 0;
        public bool ShowHistoryEmptyState => !IsLoading && FilteredSales.Count == 0 && string.IsNullOrWhiteSpace(ErrorMessage);
        public string HistoryEmptyTitle => (SelectedFilterOption?.Value ?? FilterOption.All) == FilterOption.All
            ? _localization.GetString("Loc.NoSalesYet")
            : _localization.GetString("Loc.NoSalesForSelectedFilter");
        public string HistoryEmptyMessage => _localization.GetString("Loc.HistoryEmptyMessage");
        public string OnlineOnlyActionMessage => IsOnline
            ? string.Empty
            : _localization.GetString("Loc.OnlineRequiredForRefundDelete");

        public bool HasMoreSales
        {
            get => _hasMoreSales;
            private set => this.RaiseAndSetIfChanged(ref _hasMoreSales, value);
        }

        public bool IsSaleDetailsOpen
        {
            get => _isSaleDetailsOpen;
            set => this.RaiseAndSetIfChanged(ref _isSaleDetailsOpen, value);
        }

        public HistorySaleRow? SelectedSale
        {
            get => _selectedSale;
            set => this.RaiseAndSetIfChanged(ref _selectedSale, value);
        }

        public bool IsDeleteConfirmationOpen
        {
            get => _isDeleteConfirmationOpen;
            set => this.RaiseAndSetIfChanged(ref _isDeleteConfirmationOpen, value);
        }

        public string DeleteConfirmationTitle
        {
            get => _deleteConfirmationTitle;
            set => this.RaiseAndSetIfChanged(ref _deleteConfirmationTitle, value);
        }

        public string DeleteConfirmationMessage
        {
            get => _deleteConfirmationMessage;
            set => this.RaiseAndSetIfChanged(ref _deleteConfirmationMessage, value);
        }

        public bool IsRefundDialogOpen
        {
            get => _isRefundDialogOpen;
            set => this.RaiseAndSetIfChanged(ref _isRefundDialogOpen, value);
        }

        public string RefundReason
        {
            get => _refundReason;
            set => this.RaiseAndSetIfChanged(ref _refundReason, value);
        }

        public HistorySaleLine? PendingRefundItem
        {
            get => _pendingRefundItem;
            set
            {
                this.RaiseAndSetIfChanged(ref _pendingRefundItem, value);
                this.RaisePropertyChanged(nameof(IsRefundingItem));
                this.RaisePropertyChanged(nameof(IsRefundingSale));
            }
        }

        public HistorySaleRow? PendingRefundSale
        {
            get => _pendingRefundSale;
            set
            {
                this.RaiseAndSetIfChanged(ref _pendingRefundSale, value);
                this.RaisePropertyChanged(nameof(IsRefundingItem));
                this.RaisePropertyChanged(nameof(IsRefundingSale));
            }
        }

        public int RefundQuantity
        {
            get => _refundQuantity;
            set => this.RaiseAndSetIfChanged(ref _refundQuantity, value);
        }

        public bool IsRefundingItem => PendingRefundItem != null;
        public bool IsRefundingSale => PendingRefundSale != null;

        public HistoryStoreFilterOption? SelectedStoreFilter
        {
            get => _selectedStoreFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedStoreFilter, value);
                if (!_suppressReloads && _hasLoaded)
                    _ = LoadSalesAsync();
            }
        }

        public DateTimeOffset? CustomDate
        {
            get => _customDate;
            set
            {
                this.RaiseAndSetIfChanged(ref _customDate, value);
                if (!_suppressReloads && _hasLoaded && SelectedFilterOption?.Value == FilterOption.Custom)
                    _ = LoadSalesAsync();
            }
        }

        public FilterOptionItem? SelectedFilterOption
        {
            get => _selectedFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedFilter, value);
                var selectedValue = value?.Value ?? FilterOption.All;
                IsCustomDateEnabled = selectedValue == FilterOption.Custom;

                if (selectedValue == FilterOption.Custom && !CustomDate.HasValue)
                    CustomDate = DateTimeOffset.Now;

                if (!_suppressReloads && _hasLoaded)
                    _ = LoadSalesAsync();
            }
        }

        public bool IsCustomDateEnabled
        {
            get => _isCustomDateEnabled;
            private set => this.RaiseAndSetIfChanged(ref _isCustomDateEnabled, value);
        }

        public ObservableCollection<FilterOptionItem> FilterOptions { get; } = new();

        public ReactiveCommand<Unit, Unit> ReloadCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadMoreCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportToCsvCommand { get; }
        public ReactiveCommand<HistorySaleRow, Unit> OpenSaleDetailsCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseSaleDetailsCommand { get; }
        public ReactiveCommand<HistorySaleRow, Unit> RequestDeleteSaleCommand { get; }
        public ReactiveCommand<HistorySaleRow, Unit> RequestRefundSaleCommand { get; }
        public ReactiveCommand<HistorySaleLine, Unit> RequestRefundItemCommand { get; }
        public ReactiveCommand<Unit, Unit> ConfirmDeleteCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelDeleteCommand { get; }
        public ReactiveCommand<Unit, Unit> ConfirmRefundCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelRefundCommand { get; }

        public HistoryViewModel(
            ISyncService syncService,
            ISaleRepository saleRepo,
            IProductRepository productRepo,
            IStoreAdminRepository storeAdminRepo,
            INotificationService notify,
            IConnectivityService net,
            IAuthService auth,
            ILocalizationService localization)
        {
            _sync = syncService;
            _saleRepo = saleRepo;
            _productRepo = productRepo;
            _storeAdminRepo = storeAdminRepo;
            _notify = notify;
            _net = net;
            _auth = auth;
            _localization = localization;

            ReloadCommand = ReactiveCommand.CreateFromTask(LoadSalesAsync);
            LoadMoreCommand = ReactiveCommand.CreateFromTask(LoadMoreSalesAsync);
            ExportToCsvCommand = ReactiveCommand.CreateFromTask(ExportToCsvAsync);
            OpenSaleDetailsCommand = ReactiveCommand.Create<HistorySaleRow>(OpenSaleDetails);
            CloseSaleDetailsCommand = ReactiveCommand.Create(CloseSaleDetails);
            RequestDeleteSaleCommand = ReactiveCommand.Create<HistorySaleRow>(RequestDeleteSale);
            RequestRefundSaleCommand = ReactiveCommand.Create<HistorySaleRow>(RequestRefundSale);
            RequestRefundItemCommand = ReactiveCommand.Create<HistorySaleLine>(RequestRefundItem);
            ConfirmDeleteCommand = ReactiveCommand.CreateFromTask(ConfirmDeleteAsync);
            CancelDeleteCommand = ReactiveCommand.Create(CancelDelete);
            ConfirmRefundCommand = ReactiveCommand.CreateFromTask(ConfirmRefundAsync);
            CancelRefundCommand = ReactiveCommand.Create(CancelRefund);
            _saleRepo.SalesChanged += async () =>
            {
                if (_hasLoaded)
                    await LoadSalesAsync();
            };
            _auth.UserChanged += OnUserChanged;
            _localization.LanguageChanged += OnLanguageChanged;
            RebuildStoreFilters();

            _suppressReloads = true;
            RebuildFilterOptions();
            SelectedFilterOption = FilterOptions.FirstOrDefault(option => option.Value == FilterOption.All);
            _suppressReloads = false;
        }

        public async Task EnsureLoadedAsync()
        {
            if (_hasLoaded)
                return;

            await LoadSalesAsync();
        }

        private async Task LoadSalesAsync()
        {
            _hasLoaded = true;
            _currentOffset = 0;
            await LoadSalesPageAsync(append: false);
        }

        private async Task LoadMoreSalesAsync()
        {
            if (!HasMoreSales)
                return;

            await LoadSalesPageAsync(append: true);
        }

        private async Task LoadSalesPageAsync(bool append)
        {
            if (IsLoading)
                return;

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var sales = await _saleRepo.GetHistoryPageAsync(BuildHistoryQuery(_currentOffset));
                HasMoreSales = sales.Count >= HistoryPageSize;

                var rows = sales
                    .Where(s => s.Items != null && s.Items.Count > 0)
                    .Select(BuildSaleRow)
                    .ToList();

                var storeNames = await GetStoreNamesAsync();

                foreach (var row in rows)
                    row.StoreName = storeNames.TryGetValue(row.StoreId, out var storeName)
                        ? storeName
                        : string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.StoreNumber"), row.StoreId);

                ApplyLocalizedTextToRows(rows);

                if (!append)
                    Sales.Clear();
                foreach (var row in rows)
                    Sales.Add(row);

                FilteredSales = new ObservableCollection<HistorySaleRow>(Sales);
                RecalculateTotals();
                RaiseListState();
                _currentOffset += sales.Count;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                if (!append)
                {
                    Sales.Clear();
                    FilteredSales.Clear();
                    _currentOffset = 0;
                    HasMoreSales = false;
                    RaiseListState();
                }
            }
            finally
            {
                IsLoading = false;
                RaiseListState();
            }
        }

        private async Task<Dictionary<int, string>> GetStoreNamesAsync()
        {
            if (_storeNameCache != null)
                return _storeNameCache;

            var storeNames = _auth.AllowedStores.ToDictionary(store => store.Id, store => store.Name);
            try
            {
                foreach (var store in await _storeAdminRepo.GetStoresAsync())
                    storeNames[store.Id] = store.Name;
            }
            catch
            {
                // Keep auth-scoped names as fallback if store lookup is unavailable.
            }

            _storeNameCache = storeNames;
            return _storeNameCache;
        }

        private void RaiseListState()
        {
            this.RaisePropertyChanged(nameof(IsOnline));
            this.RaisePropertyChanged(nameof(ShowHistoryLoading));
            this.RaisePropertyChanged(nameof(ShowHistoryEmptyState));
            this.RaisePropertyChanged(nameof(HistoryEmptyTitle));
            this.RaisePropertyChanged(nameof(HistoryEmptyMessage));
            this.RaisePropertyChanged(nameof(OnlineOnlyActionMessage));
        }

        public void ResetForStoreChange()
        {
            Sales.Clear();
            FilteredSales.Clear();
            AvailableStoreFilters.Clear();
            _storeNameCache = null;
            SelectedSale = null;
            PendingRefundSale = null;
            PendingRefundItem = null;
            IsSaleDetailsOpen = false;
            IsDeleteConfirmationOpen = false;
            IsRefundDialogOpen = false;
            DeleteConfirmationTitle = string.Empty;
            DeleteConfirmationMessage = string.Empty;
            RefundReason = string.Empty;
            RefundQuantity = 1;
            Total = 0;
            AverageSaleAmount = 0;
            _currentOffset = 0;
            HasMoreSales = false;
            RebuildStoreFilters();
        }

        private HistorySaleRow BuildSaleRow(
            Sale sale)
        {
            var lines = sale.Items.Select(item =>
            {
                var grossLineTotal = item.UnitPrice * item.Quantity;
                var lineDiscount = 0m;
                var netLineTotal = item.Subtotal > 0 ? item.Subtotal : grossLineTotal;

                return new HistorySaleLine
                {
                    SaleId = sale.Id,
                    SaleItemId = item.Id,
                    ItemName = string.IsNullOrWhiteSpace(item.ProductName)
                        ? _localization.GetString("Loc.UnknownProduct")
                        : item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Discount = lineDiscount,
                    Total = netLineTotal,
                    IsRefunded = item.IsRefunded,
                    RefundedQuantity = item.RefundedQuantity,
                    RefundableQuantity = item.RefundableQuantity,
                    RefundReason = item.RefundReason,
                };
            }).ToList();

            var resolvedStoreId = ResolveSaleStoreId(sale);

            var grossSubtotal = lines.Sum(i => i.Total + i.Discount);
            var saleDiscount = sale.Discount ?? lines.Sum(i => i.Discount);
            var discountedTotal = Math.Max(grossSubtotal - saleDiscount, 0);
            var paidTotal = sale.TotalPaid > 0 ? sale.TotalPaid : discountedTotal;

            return new HistorySaleRow
            {
                SaleId = sale.Id,
                StoreId = resolvedStoreId,
                StoreName = string.Empty,
                ReceiptNumber = sale.ReceiptNumber,
                Date = sale.Timestamp.ToLocalTime(),
                PaymentSummary = sale.PaymentSummary,
                ItemCount = lines.Sum(line => line.Quantity),
                Subtotal = grossSubtotal,
                Discount = saleDiscount,
                FinalTotal = paidTotal,
                TotalPaid = paidTotal,
                IsRefunded = sale.IsRefunded,
                HasRefundedItems = lines.Any(line => line.RefundedQuantity > 0),
                RefundReason = sale.RefundReason,
                Items = new ObservableCollection<HistorySaleLine>(lines),
            };
        }

        private void ApplyFilter()
        {
            FilteredSales = new ObservableCollection<HistorySaleRow>(Sales);
            RecalculateTotals();
        }

        private SaleHistoryQuery BuildHistoryQuery(int offset)
        {
            var (fromLocal, toLocal) = ResolveSelectedDateRange();
            return new SaleHistoryQuery
            {
                StoreId = ResolveEffectiveStoreFilterId(),
                FromUtc = fromLocal?.ToUniversalTime(),
                ToUtc = toLocal?.ToUniversalTime(),
                Offset = offset,
                Limit = HistoryPageSize,
            };
        }

        private (DateTime? From, DateTime? To) ResolveSelectedDateRange()
        {
            var today = DateTime.Today;
            switch (SelectedFilterOption?.Value ?? FilterOption.All)
            {
                case FilterOption.Today:
                    return (today, today.AddDays(1));
                case FilterOption.ThisWeek:
                    var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var start = today.AddDays(-diff);
                    return (start, start.AddDays(7));
                case FilterOption.ThisMonth:
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    return (monthStart, monthStart.AddMonths(1));
                case FilterOption.Custom:
                    var date = CustomDate?.Date;
                    return date.HasValue
                        ? (date.Value, date.Value.AddDays(1))
                        : (null, null);
                case FilterOption.All:
                default:
                    return (null, null);
            }
        }

        private int ResolveSaleStoreId(Sale sale)
        {
            if (sale.StoreId > 0)
                return sale.StoreId;

            var itemStoreId = sale.Items
                .Select(item => item.StoreId)
                .FirstOrDefault(storeId => storeId > 0);
            if (itemStoreId > 0)
                return itemStoreId;

            return ResolveEffectiveStoreFilterId() ?? 0;
        }

        private int? ResolveEffectiveStoreFilterId()
        {
            if (_auth.IsTenantAdmin)
                return SelectedStoreFilter?.StoreId;

            if (_auth.SelectedStoreId.HasValue)
                return _auth.SelectedStoreId.Value;

            if (_auth.AllowedStores.Count == 1)
                return _auth.AllowedStores[0].Id;

            return SelectedStoreFilter?.StoreId;
        }

        private void RecalculateTotals()
        {
            Total = FilteredSales.Sum(s => s.TotalPaid);
            AverageSaleAmount = FilteredSales.Count > 0
                ? Math.Round(Total / FilteredSales.Count, 2)
                : 0;
        }

        private async Task ExportToCsvAsync()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    "SaleId,ReceiptNumber,Date,PaymentSummary,ItemCount,Subtotal,Discount,FinalTotal,TotalPaid");

                foreach (var sale in FilteredSales)
                {
                    sb.AppendLine(
                        $"{sale.SaleId},{Escape(sale.ReceiptNumber)},{sale.Date:yyyy-MM-dd HH:mm:ss},{Escape(sale.PaymentSummary)},{sale.ItemCount},{sale.Subtotal},{sale.Discount},{sale.FinalTotal},{sale.TotalPaid}");
                }

                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var file = $"sales_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var path = System.IO.Path.Combine(docs, file);

                await System.IO.File.WriteAllTextAsync(path, sb.ToString());
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.ExportedTo"), path));
            }
            catch (Exception ex)
            {
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.ExportFailed"), ex.Message));
            }
        }

        private void RequestDeleteSale(HistorySaleRow? sale)
        {
            if (!CanManageHistoryDeletions || sale == null)
                return;

            _pendingSaleDeletion = sale;
            DeleteConfirmationTitle = _localization.GetString("Loc.DeleteSale");
            DeleteConfirmationMessage = string.Format(
                CultureInfo.CurrentCulture,
                _localization.GetString("Loc.DeleteReceiptConfirmation"),
                sale.ReceiptLabel);
            IsDeleteConfirmationOpen = true;
        }

        private void OpenSaleDetails(HistorySaleRow? sale)
        {
            if (sale == null)
                return;

            SelectedSale = sale;
            IsSaleDetailsOpen = true;
        }

        private async Task ConfirmDeleteAsync()
        {
            if (!_net.IsOnline())
            {
                _notify.Show(_localization.GetString("Loc.InternetRequired"), NotificationType.Warning);
                return;
            }

            try
            {
                if (_pendingSaleDeletion != null)
                {
                    await _saleRepo.SoftDeleteSaleAsync(_pendingSaleDeletion.SaleId, string.Empty);
                    _notify.Show(_localization.GetString("Loc.SaleDeleted"), NotificationType.Success);
                }

                CancelDelete();
            }
            catch (Exception ex)
            {
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.DeleteFailed"), ex.Message), NotificationType.Error);
            }
        }

        private void RequestRefundItem(HistorySaleLine? item)
        {
            if (item == null || item.RefundableQuantity <= 0)
                return;

            PendingRefundSale = null;
            PendingRefundItem = item;
            RefundQuantity = 1;
            RefundReason = string.Empty;
            IsRefundDialogOpen = true;
        }

        private void RequestRefundSale(HistorySaleRow? sale)
        {
            if (sale == null || !sale.CanRefund)
                return;

            PendingRefundItem = null;
            PendingRefundSale = sale;
            RefundQuantity = 1;
            RefundReason = string.Empty;
            IsRefundDialogOpen = true;
        }

        private async Task ConfirmRefundAsync()
        {
            if (PendingRefundItem == null && PendingRefundSale == null)
                return;

            if (!_net.IsOnline())
            {
                _notify.Show(_localization.GetString("Loc.InternetRequired"), NotificationType.Warning);
                return;
            }

            try
            {
                if (PendingRefundItem != null)
                {
                    await _saleRepo.RefundSaleItemAsync(
                        PendingRefundItem.SaleId,
                        PendingRefundItem.SaleItemId,
                        RefundQuantity,
                        RefundReason);
                    _notify.Show(_localization.GetString("Loc.ItemRefunded"), NotificationType.Success);
                }
                else if (PendingRefundSale != null)
                {
                    await _saleRepo.RefundSaleAsync(
                        PendingRefundSale.SaleId,
                        RefundReason);
                    _notify.Show(_localization.GetString("Loc.SaleRefunded"), NotificationType.Success);
                }

                CancelRefund();
            }
            catch (Exception ex)
            {
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.RefundFailed"), ex.Message), NotificationType.Error);
            }
        }

        private void CancelDelete()
        {
            _pendingSaleDeletion = null;
            DeleteConfirmationTitle = string.Empty;
            DeleteConfirmationMessage = string.Empty;
            IsDeleteConfirmationOpen = false;
        }

        private void CancelRefund()
        {
            PendingRefundSale = null;
            PendingRefundItem = null;
            RefundQuantity = 1;
            RefundReason = string.Empty;
            IsRefundDialogOpen = false;
        }

        private void CloseSaleDetails()
        {
            SelectedSale = null;
            IsSaleDetailsOpen = false;
        }

        private void OnUserChanged()
        {
            this.RaisePropertyChanged(nameof(CanManageHistoryDeletions));
            this.RaisePropertyChanged(nameof(ShowStoreFilter));
            this.RaisePropertyChanged(nameof(ShowStoreColumn));
            _storeNameCache = null;
            RebuildStoreFilters();
            if (_hasLoaded)
                _ = LoadSalesAsync();
        }

        private void RebuildStoreFilters()
        {
            AvailableStoreFilters.Clear();
            if (_auth.IsTenantAdmin)
                AvailableStoreFilters.Add(new HistoryStoreFilterOption { StoreId = null, Name = _localization.GetString("Loc.AllStores") });

            foreach (var store in _auth.AllowedStores.OrderBy(store => store.Name))
                AvailableStoreFilters.Add(new HistoryStoreFilterOption { StoreId = store.Id, Name = store.Name });

            if (_auth.IsTenantAdmin)
            {
                SelectedStoreFilter = AvailableStoreFilters.FirstOrDefault(filter => filter.StoreId == SelectedStoreFilter?.StoreId)
                    ?? AvailableStoreFilters.FirstOrDefault();
                return;
            }

            var effectiveStoreId = _auth.SelectedStoreId
                ?? (_auth.AllowedStores.Count == 1 ? _auth.AllowedStores[0].Id : (int?)null);

            SelectedStoreFilter = AvailableStoreFilters.FirstOrDefault(filter => filter.StoreId == effectiveStoreId)
                ?? AvailableStoreFilters.FirstOrDefault();
        }

        private static string Escape(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;
            if (s.Contains(',') || s.Contains('"'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        private void OnLanguageChanged()
        {
            _suppressReloads = true;
            RebuildFilterOptions();
            _suppressReloads = false;
            RebuildStoreFilters();
            ApplyLocalizedTextToRows(Sales);
            ApplyFilter();
        }

        private void ApplyLocalizedTextToRows(IEnumerable<HistorySaleRow> rows)
        {
            foreach (var row in rows)
            {
                row.StatusLabel = row.IsRefunded
                    ? _localization.GetString("Loc.Refunded")
                    : row.HasRefundedItems
                        ? _localization.GetString("Loc.PartiallyRefunded")
                        : _localization.GetString("Loc.Completed");
                row.ReceiptLabel = string.IsNullOrWhiteSpace(row.ReceiptNumber)
                    ? _localization.GetString("Loc.ReceiptPending")
                    : row.ReceiptNumber;

                foreach (var item in row.Items)
                {
                    item.RefundStatusLabel = item.IsRefunded
                        ? string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.RefundedQuantityFormat"), item.RefundedQuantity, item.Quantity)
                        : item.RefundedQuantity > 0
                            ? string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.PartiallyRefundedQuantityFormat"), item.RefundedQuantity, item.Quantity)
                            : _localization.GetString("Loc.NotRefunded");
                }
            }
        }

        private void RebuildFilterOptions()
        {
            var selectedValue = SelectedFilterOption?.Value ?? FilterOption.All;

            FilterOptions.Clear();
            FilterOptions.Add(new FilterOptionItem(FilterOption.All, _localization.GetString("Loc.All")));
            FilterOptions.Add(new FilterOptionItem(FilterOption.Today, _localization.GetString("Loc.Today")));
            FilterOptions.Add(new FilterOptionItem(FilterOption.ThisWeek, _localization.GetString("Loc.ThisWeek")));
            FilterOptions.Add(new FilterOptionItem(FilterOption.ThisMonth, _localization.GetString("Loc.ThisMonth")));
            FilterOptions.Add(new FilterOptionItem(FilterOption.Custom, _localization.GetString("Loc.Custom")));

            SelectedFilterOption = FilterOptions.FirstOrDefault(option => option.Value == selectedValue)
                ?? FilterOptions.FirstOrDefault();
        }

        public enum FilterOption
        {
            All,
            Today,
            ThisWeek,
            ThisMonth,
            Custom,
        }
    }

    public class HistorySaleRow : ViewModelBase
    {
        public int SaleId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string PaymentSummary { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal FinalTotal { get; set; }
        public decimal TotalPaid { get; set; }
        public bool IsRefunded { get; set; }
        public bool HasRefundedItems { get; set; }
        public string? RefundReason { get; set; }
        public ObservableCollection<HistorySaleLine> Items { get; set; } = new();
        public bool CanRefund => !IsRefunded;
        public string StatusLabel { get; set; } = string.Empty;
        public string ReceiptLabel { get; set; } = string.Empty;
        public string SecondaryLabel => string.IsNullOrWhiteSpace(StoreName)
            ? string.Empty
            : StoreName;
    }

    public class HistoryStoreFilterOption
    {
        public int? StoreId { get; set; }
        public string Name { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public class FilterOptionItem
    {
        public FilterOptionItem(HistoryViewModel.FilterOption value, string label)
        {
            Value = value;
            Label = label;
        }

        public HistoryViewModel.FilterOption Value { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }

    public class HistorySaleLine
    {
        public int SaleId { get; set; }
        public int SaleItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public bool IsRefunded { get; set; }
        public int RefundedQuantity { get; set; }
        public int RefundableQuantity { get; set; }
        public string? RefundReason { get; set; }
        public IReadOnlyList<int> AvailableRefundQuantities => Enumerable.Range(1, Math.Max(RefundableQuantity, 0)).ToList();
        public bool HasRefundReason => !string.IsNullOrWhiteSpace(RefundReason);
        public bool CanRefund => RefundableQuantity > 0;
        public string RefundStatusLabel { get; set; } = string.Empty;
    }
}
