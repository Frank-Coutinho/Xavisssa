using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class AnalyticsViewModel : ViewModelBase
    {
        private readonly IAnalyticsRepository _analyticsRepo;
        private readonly IAuthService _auth;
        private readonly INotificationService _notify;
        private readonly ILocalizationService _localization;

        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private string _scopeTitle = string.Empty;
        private int _storeCount;
        private int _productCount;
        private int _totalSalesCount;
        private decimal _totalRevenue;
        private decimal _averageSaleValue;
        private string _lineChartPath = string.Empty;
        private string _lineAreaPath = string.Empty;
        private bool _hasLoaded;

        public ObservableCollection<StoreAnalyticsRow> Stores { get; } = new();
        public ObservableCollection<AnalyticsPieSegment> PieSegments { get; } = new();
        public ObservableCollection<AnalyticsLineAxisLabel> LineAxisLabels { get; } = new();
        public ObservableCollection<AnalyticsLinePoint> LinePoints { get; } = new();
        public ObservableCollection<AnalyticsLineGuide> LineGuides { get; } = new();

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                this.RaiseAndSetIfChanged(ref _isLoading, value);
                RaiseVisualState();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        public string ScopeTitle
        {
            get => _scopeTitle;
            set => this.RaiseAndSetIfChanged(ref _scopeTitle, value);
        }

        public int StoreCount
        {
            get => _storeCount;
            set => this.RaiseAndSetIfChanged(ref _storeCount, value);
        }

        public int ProductCount
        {
            get => _productCount;
            set => this.RaiseAndSetIfChanged(ref _productCount, value);
        }

        public int TotalSalesCount
        {
            get => _totalSalesCount;
            set => this.RaiseAndSetIfChanged(ref _totalSalesCount, value);
        }

        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => this.RaiseAndSetIfChanged(ref _totalRevenue, value);
        }

        public decimal AverageSaleValue
        {
            get => _averageSaleValue;
            set => this.RaiseAndSetIfChanged(ref _averageSaleValue, value);
        }

        public string LineChartPath
        {
            get => _lineChartPath;
            set => this.RaiseAndSetIfChanged(ref _lineChartPath, value);
        }

        public string LineAreaPath
        {
            get => _lineAreaPath;
            set => this.RaiseAndSetIfChanged(ref _lineAreaPath, value);
        }

        public ReactiveCommand<Unit, Unit> LoadCommand { get; }
        public bool ShowAnalyticsSkeleton => IsLoading && Stores.Count == 0;
        public bool ShowAnalyticsEmptyState => !IsLoading && Stores.Count == 0 && string.IsNullOrWhiteSpace(ErrorMessage);
        public bool ShowAnalyticsErrorState => !IsLoading && !string.IsNullOrWhiteSpace(ErrorMessage);
        public bool IsDemoReport => string.Equals(_auth.Username, "demo-admin", StringComparison.OrdinalIgnoreCase);
        public string DemoReportLabel => "Demo Report - Sample Data";
        public string AnalyticsEmptyTitle => _localization.GetString("Loc.NoAnalyticsData");
        public string AnalyticsEmptyMessage => _localization.GetString("Loc.AnalyticsEmptyMessage");

        public AnalyticsViewModel(
            IAnalyticsRepository analyticsRepo,
            IAuthService auth,
            INotificationService notify,
            ILocalizationService localization)
        {
            _analyticsRepo = analyticsRepo;
            _auth = auth;
            _notify = notify;
            _localization = localization;

            LoadCommand = ReactiveCommand.CreateFromTask(LoadAnalyticsAsync);
            _auth.UserChanged += OnUserChanged;
            _localization.LanguageChanged += OnLanguageChanged;
        }

        public async Task EnsureLoadedAsync()
        {
            if (_hasLoaded)
                return;

            await LoadAnalyticsAsync();
        }

        public async Task LoadAnalyticsAsync()
        {
            if (IsLoading)
                return;

            _hasLoaded = true;
            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                if (_auth.CanViewTenantAnalytics && _auth.SelectedTenantId.HasValue)
                {
                    var tenantAnalytics = await _analyticsRepo.GetTenantAnalyticsAsync(_auth.SelectedTenantId.Value);
                    ScopeTitle = _localization.GetString("Loc.TenantAnalytics");
                    StoreCount = tenantAnalytics.StoreCount;
                    ProductCount = tenantAnalytics.ProductCount;
                    TotalSalesCount = tenantAnalytics.TotalSalesCount;
                    TotalRevenue = tenantAnalytics.TotalRevenue;
                    AverageSaleValue = tenantAnalytics.AverageSaleValue;

                    Stores.Clear();
                    foreach (var store in tenantAnalytics.Stores.OrderByDescending(s => s.TotalRevenue))
                    {
                        Stores.Add(new StoreAnalyticsRow
                        {
                            StoreId = store.StoreId,
                            StoreName = store.StoreName,
                            StoreCode = store.StoreCode,
                            IsActive = store.IsActive,
                            TotalSalesCount = store.TotalSalesCount,
                            TotalRevenue = store.TotalRevenue,
                            AverageSaleValue = store.AverageSaleValue,
                            LastSaleDate = store.LastSaleDate,
                            StatusLabel = store.IsActive ? _localization.GetString("Loc.ActiveStatus") : _localization.GetString("Loc.InactiveStatus"),
                            LastSaleLabel = store.LastSaleDate.HasValue ? store.LastSaleDate.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) : _localization.GetString("Loc.NoSalesYet"),
                            AverageSaleLabel = string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.AverageSaleFormat"), store.AverageSaleValue),
                        });
                    }
                    RaiseVisualState();
                }
                else if (_auth.CanViewStoreAnalytics)
                {
                    var storeAnalytics = await _analyticsRepo.GetStoreAnalyticsAsync();
                    var selectedStore = _auth.AllowedStores.FirstOrDefault(s => s.Id == _auth.SelectedStoreId);

                    ScopeTitle = _localization.GetString("Loc.StoreAnalytics");
                    StoreCount = 1;
                    ProductCount = 0;
                    TotalSalesCount = storeAnalytics.TotalSalesCount;
                    TotalRevenue = storeAnalytics.TotalRevenue;
                    AverageSaleValue = storeAnalytics.AverageSaleValue;

                    Stores.Clear();
                    Stores.Add(new StoreAnalyticsRow
                    {
                        StoreId = storeAnalytics.StoreId,
                        StoreName = selectedStore?.Name ?? string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.StoreNumber"), storeAnalytics.StoreId),
                        StoreCode = string.Empty,
                        IsActive = true,
                        TotalSalesCount = storeAnalytics.TotalSalesCount,
                        TotalRevenue = storeAnalytics.TotalRevenue,
                        AverageSaleValue = storeAnalytics.AverageSaleValue,
                        LastSaleDate = null,
                        StatusLabel = _localization.GetString("Loc.ActiveStatus"),
                        LastSaleLabel = _localization.GetString("Loc.NoSalesYet"),
                        AverageSaleLabel = string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.AverageSaleFormat"), storeAnalytics.AverageSaleValue),
                    });
                    RaiseVisualState();
                }
                else
                {
                    ErrorMessage = _localization.GetString("Loc.AnalyticsPermissionDenied");
                }

                RebuildCharts();
                RaiseVisualState();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _notify.Show(string.Format(CultureInfo.CurrentCulture, _localization.GetString("Loc.AnalyticsLoadFailed"), ex.Message), NotificationType.Error);
                Stores.Clear();
                PieSegments.Clear();
                LineAxisLabels.Clear();
                LinePoints.Clear();
                LineGuides.Clear();
                LineChartPath = string.Empty;
                LineAreaPath = string.Empty;
                RaiseVisualState();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ResetForStoreChange()
        {
            Stores.Clear();
            PieSegments.Clear();
            LineAxisLabels.Clear();
            LinePoints.Clear();
            LineGuides.Clear();
            StoreCount = 0;
            ProductCount = 0;
            TotalSalesCount = 0;
            TotalRevenue = 0;
            AverageSaleValue = 0;
            LineChartPath = string.Empty;
            LineAreaPath = string.Empty;
            ErrorMessage = string.Empty;
            RaiseVisualState();
        }

        private void RaiseVisualState()
        {
            this.RaisePropertyChanged(nameof(ShowAnalyticsSkeleton));
            this.RaisePropertyChanged(nameof(ShowAnalyticsEmptyState));
            this.RaisePropertyChanged(nameof(ShowAnalyticsErrorState));
            this.RaisePropertyChanged(nameof(AnalyticsEmptyTitle));
            this.RaisePropertyChanged(nameof(AnalyticsEmptyMessage));
        }

        private void RebuildCharts()
        {
            PieSegments.Clear();
            LineAxisLabels.Clear();
            LinePoints.Clear();
            LineGuides.Clear();

            var chartStores = Stores
                .Where(x => x.TotalRevenue > 0)
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            if (chartStores.Count == 0 || TotalRevenue <= 0)
            {
                LineChartPath = string.Empty;
                LineAreaPath = string.Empty;
                return;
            }

            var palette = new[]
            {
                "#0F766E",
                "#F59E0B",
                "#DC2626",
                "#2563EB",
                "#059669",
                "#7C3AED",
            };

            BuildPieChart(chartStores, palette);
            BuildLineChart(chartStores);
        }

        private void BuildPieChart(IReadOnlyList<StoreAnalyticsRow> chartStores, IReadOnlyList<string> palette)
        {
            const double centerX = 110;
            const double centerY = 110;
            const double radius = 82;

            double startAngle = -90;
            for (var i = 0; i < chartStores.Count; i++)
            {
                var store = chartStores[i];
                var sweep = TotalRevenue == 0 ? 0 : (double)(store.TotalRevenue / TotalRevenue) * 360d;
                var endAngle = startAngle + sweep;
                var color = palette[i % palette.Count];

                PieSegments.Add(new AnalyticsPieSegment
                {
                    StoreName = store.StoreName,
                    Revenue = store.TotalRevenue,
                    PercentageLabel = $"{Math.Round((store.TotalRevenue / TotalRevenue) * 100m, 1):0.#}%",
                    Fill = color,
                    PathData = BuildPieSlicePath(centerX, centerY, radius, startAngle, endAngle),
                });

                startAngle = endAngle;
            }
        }

        private void BuildLineChart(IReadOnlyList<StoreAnalyticsRow> chartStores)
        {
            const double width = 520;
            const double height = 220;
            const double leftPadding = 24;
            const double rightPadding = 24;
            const double topPadding = 18;
            const double bottomPadding = 30;
            const int guideCount = 4;

            LinePoints.Clear();
            LineGuides.Clear();

            var maxRevenue = chartStores.Max(x => x.TotalRevenue);
            var usableWidth = width - leftPadding - rightPadding;
            var usableHeight = height - topPadding - bottomPadding;
            var baselineY = height - bottomPadding;
            var stepX = chartStores.Count == 1 ? 0 : usableWidth / (chartStores.Count - 1);

            for (var guideIndex = 0; guideIndex < guideCount; guideIndex++)
            {
                var normalizedGuide = guideCount == 1 ? 0d : guideIndex / (double)(guideCount - 1);
                var y = topPadding + (normalizedGuide * usableHeight);
                var guideValue = maxRevenue * (decimal)(1d - normalizedGuide);
                LineGuides.Add(new AnalyticsLineGuide
                {
                    Y = y,
                    Label = string.Format(CultureInfo.CurrentCulture, "MZN {0:N0}", guideValue),
                });
            }

            var pathPoints = new List<(double X, double Y)>();

            for (var i = 0; i < chartStores.Count; i++)
            {
                var store = chartStores[i];
                var x = chartStores.Count == 1 ? width / 2d : leftPadding + (stepX * i);
                var normalized = maxRevenue == 0 ? 0 : (double)(store.TotalRevenue / maxRevenue);
                var y = topPadding + ((1d - normalized) * usableHeight);
                pathPoints.Add((x, y));
                LinePoints.Add(new AnalyticsLinePoint
                {
                    X = x,
                    Y = y,
                    Label = store.ShortLabel,
                    RevenueLabel = string.Format(CultureInfo.CurrentCulture, "MZN {0:N2}", store.TotalRevenue),
                });

                LineAxisLabels.Add(new AnalyticsLineAxisLabel
                {
                    Label = store.ShortLabel,
                    X = x - 24,
                });
            }

            LineChartPath = BuildLinePath(pathPoints);
            LineAreaPath = BuildAreaPath(pathPoints, baselineY);
        }

        private static string BuildLinePath(IReadOnlyList<(double X, double Y)> points)
        {
            if (points.Count == 0)
            {
                return string.Empty;
            }

            return "M " + string.Join(" L ", points.Select(p => $"{p.X:0.##},{p.Y:0.##}"));
        }

        private static string BuildAreaPath(IReadOnlyList<(double X, double Y)> points, double baselineY)
        {
            if (points.Count == 0)
            {
                return string.Empty;
            }

            var first = points[0];
            var last = points[^1];
            var lineSegments = string.Join(" L ", points.Select(p => $"{p.X:0.##},{p.Y:0.##}"));
            return $"M {first.X:0.##},{baselineY:0.##} L {lineSegments} L {last.X:0.##},{baselineY:0.##} Z";
        }

        private static string BuildPieSlicePath(double cx, double cy, double radius, double startAngle, double endAngle)
        {
            var sweep = endAngle - startAngle;
            if (sweep >= 359.99)
            {
                return $"M {cx},{cy} m {-radius},0 a {radius},{radius} 0 1,0 {radius * 2},0 a {radius},{radius} 0 1,0 {-radius * 2},0";
            }

            var startRadians = Math.PI * startAngle / 180d;
            var endRadians = Math.PI * endAngle / 180d;
            var startX = cx + (radius * Math.Cos(startRadians));
            var startY = cy + (radius * Math.Sin(startRadians));
            var endX = cx + (radius * Math.Cos(endRadians));
            var endY = cy + (radius * Math.Sin(endRadians));
            var largeArc = sweep > 180 ? 1 : 0;

            return $"M {cx:0.##},{cy:0.##} L {startX:0.##},{startY:0.##} A {radius:0.##},{radius:0.##} 0 {largeArc},1 {endX:0.##},{endY:0.##} Z";
        }

        private void OnUserChanged()
        {
            if (_hasLoaded)
                _ = LoadAnalyticsAsync();
        }

        private void OnLanguageChanged()
        {
            this.RaisePropertyChanged(nameof(ScopeTitle));
            this.RaisePropertyChanged(nameof(ErrorMessage));
            this.RaisePropertyChanged(nameof(Stores));
            this.RaisePropertyChanged(nameof(PieSegments));
            this.RaisePropertyChanged(nameof(LineAxisLabels));
            if (_hasLoaded)
                _ = LoadAnalyticsAsync();
        }
    }

    public class StoreAnalyticsRow
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageSaleValue { get; set; }
        public DateTime? LastSaleDate { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string LastSaleLabel { get; set; } = string.Empty;
        public string AverageSaleLabel { get; set; } = string.Empty;
        public string ShortLabel => string.IsNullOrWhiteSpace(StoreCode)
            ? StoreName.Length <= 8 ? StoreName : StoreName[..8]
            : StoreCode;
    }

    public class AnalyticsPieSegment
    {
        public string StoreName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public string PercentageLabel { get; set; } = string.Empty;
        public string Fill { get; set; } = string.Empty;
        public string PathData { get; set; } = string.Empty;
    }

    public class AnalyticsLineAxisLabel
    {
        public string Label { get; set; } = string.Empty;
        public double X { get; set; }
    }

    public class AnalyticsLinePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; } = string.Empty;
        public string RevenueLabel { get; set; } = string.Empty;
    }

    public class AnalyticsLineGuide
    {
        public double Y { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
