using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform.Storage;
using ReactiveUI;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class ConfigViewModel : ViewModelBase, IDisposable
    {
        private readonly IPrinterService _printer;
        private readonly INotificationService _notify;
        private readonly IThemeService _theme;
        private readonly ILocalizationService _localization;
        private readonly IAuthService _auth;
        private readonly ILicenseCacheService _licenseCache;
        private readonly ILicenseClient _licenseClient;
        private readonly IConfirmationDialogService _confirmations;
        private readonly ILicenseFeatureGate _featureGate;
        private readonly CompositeDisposable _disposables = new();
        private bool _suppressPreferenceAutoSave;

        public ObservableCollection<int> PopularPaperWidths { get; } = new() { 48, 57, 58, 70, 76, 80 };
        public ObservableCollection<string> AvailableLanguages { get; } = new() { "English", "Portug\u00eas" };
        public ObservableCollection<string> AvailablePrinters { get; } = new();
        public ObservableCollection<string> ImagePositions { get; } = new() { "Center", "Left", "Right" };

        public bool CanManageTenantPrinting => _auth.CanEditTenantPrinting;
        public bool CanManageStorePrinting => _auth.CanEditStorePrinting;
        public bool ShowPrintingSettings => CanManageTenantPrinting || CanManageStorePrinting;
        public bool ShowLicenseUsage => false;
        public bool HasAvailablePrinters => AvailablePrinters.Count > 0;
        public bool HasReceiptPrinterSelected => !string.IsNullOrWhiteSpace(SelectedReceiptPrinter);
        public bool HasLabelPrinterSelected => !string.IsNullOrWhiteSpace(SelectedLabelPrinter);
        public string PrinterStatusText => HasAvailablePrinters
            ? _localization.GetString("Loc.PrinterReady")
            : _localization.GetString("Loc.NoPrintersAvailable");

        public string ConfigScopeTitle => CanManageTenantPrinting
            ? _localization.GetString("Loc.TenantSettings")
            : _localization.GetString("Loc.StoreSettings");
        public string ConfigScopeSubtitle => CanManageTenantPrinting
            ? _localization.GetString("Loc.TenantPrintingHint")
            : _localization.GetString("Loc.StorePrintingHint");
        public string PrintingScopeLabel => CanManageTenantPrinting
            ? _localization.GetString("Loc.TenantPrintingDefaults")
            : _localization.GetString("Loc.StorePrintingDefaults");
        public string PrintingScopeHint => CanManageTenantPrinting
            ? _localization.GetString("Loc.TenantPrintingHint")
            : _localization.GetString("Loc.StorePrintingHint");

        private string _currentDateTime = DateTime.Now.ToString("dd MMM yyyy - HH:mm:ss");
        public string CurrentDateTime
        {
            get => _currentDateTime;
            set => this.RaiseAndSetIfChanged(ref _currentDateTime, value);
        }

        private string _storeName = string.Empty;
        public string StoreName
        {
            get => _storeName;
            set => this.RaiseAndSetIfChanged(ref _storeName, value);
        }

        public bool HasHeaderImage => !string.IsNullOrWhiteSpace(HeaderImagePath);

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode == value)
                    return;

                this.RaiseAndSetIfChanged(ref _isDarkMode, value);
                PersistPreferenceSettings();
            }
        }

        private string _selectedLanguage = "English";
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (string.Equals(_selectedLanguage, value, StringComparison.Ordinal))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
                _printer.LanguageCode = MapLanguageNameToCode(value);
                _localization.SetLanguage(_printer.LanguageCode);
                PersistPreferenceSettings();
            }
        }

        private string _selectedPrinter = string.Empty;
        public string SelectedReceiptPrinter
        {
            get => _selectedPrinter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedPrinter, value);
                RaisePrinterStatus();
            }
        }

        private string _selectedLabelPrinter = string.Empty;
        public string SelectedLabelPrinter
        {
            get => _selectedLabelPrinter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedLabelPrinter, value);
                RaisePrinterStatus();
            }
        }

        public int PaperWidth
        {
            get => _printer.PaperWidth;
            set
            {
                _printer.PaperWidth = value;
                this.RaisePropertyChanged();
            }
        }

        public double PaperHeight
        {
            get => _printer.PaperHeight;
            set
            {
                _printer.PaperHeight = value;
                this.RaisePropertyChanged();
            }
        }

        public int FontSize
        {
            get => _printer.FontSize;
            set
            {
                _printer.FontSize = value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LabelWidthMm
        {
            get => (decimal)_printer.LabelWidthMm;
            set
            {
                _printer.LabelWidthMm = (double)value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LabelHeightMm
        {
            get => (decimal)_printer.LabelHeightMm;
            set
            {
                _printer.LabelHeightMm = (double)value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LabelGapMm
        {
            get => (decimal)_printer.LabelGapMm;
            set
            {
                _printer.LabelGapMm = (double)value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LabelHorizontalPaddingMm
        {
            get => (decimal)_printer.LabelHorizontalPaddingMm;
            set
            {
                _printer.LabelHorizontalPaddingMm = (double)value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LabelVerticalPaddingMm
        {
            get => (decimal)_printer.LabelVerticalPaddingMm;
            set
            {
                _printer.LabelVerticalPaddingMm = (double)value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LabelBarcodeWidthMm
        {
            get => (decimal)_printer.LabelBarcodeWidthMm;
            set
            {
                _printer.LabelBarcodeWidthMm = (double)value;
                this.RaisePropertyChanged();
            }
        }

        public decimal LabelBarcodeHeightMm
        {
            get => (decimal)_printer.LabelBarcodeHeightMm;
            set
            {
                _printer.LabelBarcodeHeightMm = (double)value;
                this.RaisePropertyChanged();
            }
        }

        public int LabelFontSize
        {
            get => _printer.LabelFontSize;
            set
            {
                _printer.LabelFontSize = value;
                this.RaisePropertyChanged();
            }
        }

        public bool ShowDateTime
        {
            get => _printer.ShowDateTime;
            set
            {
                _printer.ShowDateTime = value;
                this.RaisePropertyChanged();
            }
        }

        public string HeaderText
        {
            get => _printer.HeaderText;
            set
            {
                _printer.HeaderText = value;
                this.RaisePropertyChanged();
            }
        }

        public string FooterText
        {
            get => _printer.FooterText;
            set
            {
                _printer.FooterText = value;
                this.RaisePropertyChanged();
            }
        }

        public string HeaderImagePath
        {
            get => _printer.HeaderImagePath;
            set
            {
                _printer.HeaderImagePath = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(HasHeaderImage));
            }
        }

        public bool UseHeaderImage
        {
            get => _printer.UseHeaderImage;
            set
            {
                _printer.UseHeaderImage = value;
                this.RaisePropertyChanged();
            }
        }

        public bool PrintImageOnReceipt
        {
            get => _printer.PrintImageOnReceipt;
            set
            {
                _printer.PrintImageOnReceipt = value;
                this.RaisePropertyChanged();
            }
        }

        private string _selectedImagePosition = "Center";
        public string SelectedImagePosition
        {
            get => _selectedImagePosition;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedImagePosition, value);
                _printer.ImagePosition = value;
            }
        }

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> ReloadPrintersCommand { get; }
        public ReactiveCommand<Unit, Unit> PickLogoCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveLogoCommand { get; }
        public ReactiveCommand<Unit, Unit> TestPrintCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyLabelDefaultsCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshLicenseUsageCommand { get; }

        private string _licensePlanName = "Not activated";
        public string LicensePlanName
        {
            get => _licensePlanName;
            set => this.RaiseAndSetIfChanged(ref _licensePlanName, value);
        }

        private string _licenseStatus = "Unknown";
        public string LicenseStatus
        {
            get => _licenseStatus;
            set => this.RaiseAndSetIfChanged(ref _licenseStatus, value);
        }

        private string _storesUsage = "-";
        public string StoresUsage
        {
            get => _storesUsage;
            set => this.RaiseAndSetIfChanged(ref _storesUsage, value);
        }

        private string _usersUsage = "-";
        public string UsersUsage
        {
            get => _usersUsage;
            set => this.RaiseAndSetIfChanged(ref _usersUsage, value);
        }

        private string _devicesUsage = "-";
        public string DevicesUsage
        {
            get => _devicesUsage;
            set => this.RaiseAndSetIfChanged(ref _devicesUsage, value);
        }

        private string _lastValidated = "-";
        public string LastValidated
        {
            get => _lastValidated;
            set => this.RaiseAndSetIfChanged(ref _lastValidated, value);
        }

        private string _offlineGraceEnds = "-";
        public string OfflineGraceEnds
        {
            get => _offlineGraceEnds;
            set => this.RaiseAndSetIfChanged(ref _offlineGraceEnds, value);
        }

        private string _licenseUpgradeMessage = string.Empty;
        private bool _hasLoadedLicenseUsage;
        public string LicenseUpgradeMessage
        {
            get => _licenseUpgradeMessage;
            set => this.RaiseAndSetIfChanged(ref _licenseUpgradeMessage, value);
        }

        public ConfigViewModel(
            IPrinterService printer,
            INotificationService notify,
            IThemeService theme,
            ILocalizationService localization,
            IAuthService auth,
            ILicenseCacheService licenseCache,
            ILicenseClient licenseClient,
            IConfirmationDialogService confirmations,
            ILicenseFeatureGate featureGate)
        {
            _printer = printer ?? throw new ArgumentNullException(nameof(printer));
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _licenseCache = licenseCache ?? throw new ArgumentNullException(nameof(licenseCache));
            _licenseClient = licenseClient ?? throw new ArgumentNullException(nameof(licenseClient));
            _confirmations = confirmations ?? throw new ArgumentNullException(nameof(confirmations));
            _featureGate = featureGate ?? throw new ArgumentNullException(nameof(featureGate));

            LoadPrinterConfiguration();
            LoadAvailablePrinters();
            RefreshScopeLabel();

            _localization.LanguageChanged += OnLanguageChanged;

            var clockSub = Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ => CurrentDateTime = DateTime.Now.ToString("dd MMM yyyy - HH:mm:ss"));
            _disposables.Add(clockSub);

            var themeSub = this.WhenAnyValue(x => x.IsDarkMode)
                .Subscribe(mode =>
                {
                    if (mode)
                        _theme.SetDark();
                    else
                        _theme.SetLight();
                });
            _disposables.Add(themeSub);

            _auth.UserChanged += RefreshScopeLabel;

            SaveCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
            ReloadPrintersCommand = ReactiveCommand.Create(LoadAvailablePrinters);
            PickLogoCommand = ReactiveCommand.CreateFromTask(PickLogoAsync);
            RemoveLogoCommand = ReactiveCommand.CreateFromTask(RemoveLogoAsync);
            TestPrintCommand = ReactiveCommand.Create(TestPrint);
            ApplyLabelDefaultsCommand = ReactiveCommand.Create(ApplyXpP203ADefaults);
            RefreshLicenseUsageCommand = ReactiveCommand.CreateFromTask(LoadLicenseUsageAsync);
        }

        public async Task EnsureLoadedAsync()
        {
            if (_hasLoadedLicenseUsage)
                return;

            await LoadLicenseUsageAsync();
        }

        private void RefreshScopeLabel()
        {
            StoreName = CanManageTenantPrinting
                ? _localization.GetString("Loc.TenantDefaultPrinting")
                : _localization.GetString("Loc.SelectedStorePrinting");
            this.RaisePropertyChanged(nameof(CanManageTenantPrinting));
            this.RaisePropertyChanged(nameof(CanManageStorePrinting));
            this.RaisePropertyChanged(nameof(ShowPrintingSettings));
            this.RaisePropertyChanged(nameof(ShowLicenseUsage));
            this.RaisePropertyChanged(nameof(ConfigScopeTitle));
            this.RaisePropertyChanged(nameof(ConfigScopeSubtitle));
            this.RaisePropertyChanged(nameof(PrintingScopeLabel));
            this.RaisePropertyChanged(nameof(PrintingScopeHint));
        }

        private async Task LoadLicenseUsageAsync()
        {
            _hasLoadedLicenseUsage = true;
            try
            {
                var cache = await _licenseCache.LoadAsync();
                if (cache != null)
                {
                    LicensePlanName = cache.PlanCode;
                    LicenseStatus = _licenseCache.IsLimitedMode(cache) ? "Limited / read-only" : "Active";
                    StoresUsage = $"- / {FormatLimit(cache.MaxStores)}";
                    UsersUsage = $"- / {FormatLimit(cache.MaxUsers)}";
                    DevicesUsage = $"- / {FormatLimit(cache.MaxDevices)}";
                    LastValidated = FormatDate(cache.LastValidatedAt);
                    OfflineGraceEnds = FormatDate(cache.GracePeriodEndsAt);
                }

                if (_auth.SelectedTenantId.HasValue)
                {
                    var usage = await _licenseClient.GetUsageAsync(_auth.SelectedTenantId.Value);
                    if (usage != null)
                    {
                        LicensePlanName = usage.PlanName;
                        LicenseStatus = usage.Status;
                        StoresUsage = $"{usage.StoresUsed} / {FormatLimit(usage.MaxStores)}";
                        UsersUsage = $"{usage.UsersUsed} / {FormatLimit(usage.MaxUsers)}";
                        DevicesUsage = $"{usage.DevicesUsed} / {FormatLimit(usage.MaxDevices)}";
                        LastValidated = FormatDate(usage.LastValidatedAt);
                        OfflineGraceEnds = FormatDate(usage.GracePeriodEndsAt);
                        LicenseUpgradeMessage = usage.MaxStores.HasValue && usage.StoresUsed >= usage.MaxStores.Value
                            ? $"You reached the {usage.MaxStores.Value}-store limit. Upgrade to Business Lifetime to add more stores."
                            : string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"License usage load failed: {ex.Message}");
            }
        }

        private static string FormatDate(DateTime? date) =>
            date.HasValue ? date.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm") : "-";

        private static string FormatLimit(int? value) =>
            value.HasValue ? value.Value.ToString() : "Unlimited";

        private void LoadPrinterConfiguration()
        {
            _suppressPreferenceAutoSave = true;

            try
            {
                SelectedReceiptPrinter = _printer.PrinterName;
                SelectedLabelPrinter = _printer.LabelPrinterName;
                PaperWidth = _printer.PaperWidth;
                PaperHeight = _printer.PaperHeight;
                FontSize = _printer.FontSize;
                LabelWidthMm = (decimal)_printer.LabelWidthMm;
                LabelHeightMm = (decimal)_printer.LabelHeightMm;
                LabelGapMm = (decimal)_printer.LabelGapMm;
                LabelHorizontalPaddingMm = (decimal)_printer.LabelHorizontalPaddingMm;
                LabelVerticalPaddingMm = (decimal)_printer.LabelVerticalPaddingMm;
                LabelBarcodeWidthMm = (decimal)_printer.LabelBarcodeWidthMm;
                LabelBarcodeHeightMm = (decimal)_printer.LabelBarcodeHeightMm;
                LabelFontSize = _printer.LabelFontSize;
                HeaderText = _printer.HeaderText;
                FooterText = _printer.FooterText;
                HeaderImagePath = _printer.HeaderImagePath;
                UseHeaderImage = _printer.UseHeaderImage;
                PrintImageOnReceipt = _printer.PrintImageOnReceipt;
                SelectedImagePosition = NormalizeImagePosition(_printer.ImagePosition);
                IsDarkMode = _printer.IsDarkMode;
                SelectedLanguage = MapCodeToLanguageName(_printer.LanguageCode);
            }
            finally
            {
                _suppressPreferenceAutoSave = false;
            }
        }

        private static string MapLanguageNameToCode(string languageName) =>
            string.Equals(languageName, "Portug\u00eas", StringComparison.OrdinalIgnoreCase)
            || string.Equals(languageName, "Portugues", StringComparison.OrdinalIgnoreCase)
                ? "pt-PT"
                : "en-US";

        private static string MapCodeToLanguageName(string languageCode) =>
            string.Equals(languageCode, "pt-PT", StringComparison.OrdinalIgnoreCase) ? "Portug\u00eas" : "English";

        private void LoadAvailablePrinters()
        {
            AvailablePrinters.Clear();

            try
            {
                foreach (var printer in _printer.AvailablePrinters)
                    AvailablePrinters.Add(printer);

                if (!AvailablePrinters.Contains(SelectedReceiptPrinter))
                    SelectedReceiptPrinter = AvailablePrinters.FirstOrDefault() ?? string.Empty;

                if (!AvailablePrinters.Contains(SelectedLabelPrinter))
                    SelectedLabelPrinter = AvailablePrinters.FirstOrDefault() ?? string.Empty;

                RaisePrinterStatus();
            }
            catch (Exception ex)
            {
                _notify.Show(_localization.GetString("Loc.PrinterConfigLoadFailed") + $" {ex.Message}");
            }
        }

        private async Task PickLogoAsync()
        {
            var top = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (top == null)
                return;

            var result = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _localization.GetString("Loc.SelectImage"),
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(_localization.GetString("Loc.Images"))
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff" },
                    },
                    FilePickerFileTypes.All,
                },
            });

            if (result != null && result.Count > 0)
            {
                HeaderImagePath = result[0].Path.LocalPath;
                _notify.Show(_localization.GetString("Loc.PrinterConfigLoaded"));
            }
        }

        private async Task RemoveLogoAsync()
        {
            if (!await _confirmations.ConfirmActionAsync(
                    "Remove receipt logo?",
                    "This removes the configured receipt logo from future printed receipts.",
                    "Remove",
                    true))
                return;

            HeaderImagePath = string.Empty;
            _notify.Show(_localization.GetString("Loc.PrinterConfigRemoved"));
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                if (CanManageTenantPrinting)
                {
                    var feature = await _featureGate.EnsureFeatureAsync(LicenseFeature.CustomReceipt, _auth.SelectedTenantId);
                    if (!feature.Allowed)
                    {
                        _notify.Show(feature.Message ?? "Custom receipt settings are not included in the current license.", NotificationType.Warning);
                        return;
                    }
                }

                _printer.PrinterName = SelectedReceiptPrinter;
                _printer.LabelPrinterName = SelectedLabelPrinter;
                _printer.PaperWidth = PaperWidth;
                _printer.PaperHeight = PaperHeight;
                _printer.FontSize = FontSize;
                _printer.LabelWidthMm = (double)LabelWidthMm;
                _printer.LabelHeightMm = (double)LabelHeightMm;
                _printer.LabelGapMm = (double)LabelGapMm;
                _printer.LabelHorizontalPaddingMm = (double)LabelHorizontalPaddingMm;
                _printer.LabelVerticalPaddingMm = (double)LabelVerticalPaddingMm;
                _printer.LabelBarcodeWidthMm = (double)LabelBarcodeWidthMm;
                _printer.LabelBarcodeHeightMm = (double)LabelBarcodeHeightMm;
                _printer.LabelFontSize = LabelFontSize;
                _printer.HeaderText = HeaderText;
                _printer.FooterText = FooterText;
                _printer.ShowDateTime = ShowDateTime;
                _printer.HeaderImagePath = HeaderImagePath;
                _printer.UseHeaderImage = UseHeaderImage;
                _printer.PrintImageOnReceipt = PrintImageOnReceipt;
                _printer.ImagePosition = SelectedImagePosition;
                _printer.IsDarkMode = IsDarkMode;
                _printer.LanguageCode = MapLanguageNameToCode(SelectedLanguage);
                _printer.SaveConfiguration();
                _notify.Show(_localization.GetString("Loc.PrinterConfigSaved"));
            }
            catch (Exception ex)
            {
                _notify.Show(_localization.GetString("Loc.PrinterConfigSaveFailed"));
                Console.WriteLine($"Error saving settings: {ex}");
            }
        }

        private void PersistPreferenceSettings()
        {
            if (_suppressPreferenceAutoSave)
                return;

            try
            {
                _printer.IsDarkMode = IsDarkMode;
                _printer.LanguageCode = MapLanguageNameToCode(SelectedLanguage);
                _printer.SaveConfiguration();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error auto-saving preference settings: {ex}");
            }
        }

        private void TestPrint()
        {
            var text =
                "=== PRINT TEST ===\n"
                + $"Scope: {StoreName}\n"
                + $"Date: {DateTime.Now}\n\n";

            _printer.PrintOrSaveReceipt(text, "TEST");
        }

        private void ApplyXpP203ADefaults()
        {
            LabelWidthMm = 40m;
            LabelHeightMm = 30m;
            LabelGapMm = 2m;
            LabelHorizontalPaddingMm = 2m;
            LabelVerticalPaddingMm = 1.5m;
            LabelBarcodeWidthMm = 34m;
            LabelBarcodeHeightMm = 11m;
            LabelFontSize = 8;
        }

        public void Dispose()
        {
            _auth.UserChanged -= RefreshScopeLabel;
            _localization.LanguageChanged -= OnLanguageChanged;
            _disposables.Dispose();
            GC.SuppressFinalize(this);
        }

        private void OnLanguageChanged()
        {
            this.RaisePropertyChanged(nameof(ConfigScopeTitle));
            this.RaisePropertyChanged(nameof(ConfigScopeSubtitle));
            this.RaisePropertyChanged(nameof(PrintingScopeLabel));
            this.RaisePropertyChanged(nameof(PrintingScopeHint));
            RefreshScopeLabel();
            RaisePrinterStatus();
        }

        private void RaisePrinterStatus()
        {
            this.RaisePropertyChanged(nameof(HasAvailablePrinters));
            this.RaisePropertyChanged(nameof(HasReceiptPrinterSelected));
            this.RaisePropertyChanged(nameof(HasLabelPrinterSelected));
            this.RaisePropertyChanged(nameof(PrinterStatusText));
        }

        private static string NormalizeImagePosition(string value) =>
            value.Equals("Centro", StringComparison.OrdinalIgnoreCase) ? "Center"
            : value.Equals("Esquerda", StringComparison.OrdinalIgnoreCase) ? "Left"
            : value.Equals("Direita", StringComparison.OrdinalIgnoreCase) ? "Right"
            : value;
    }
}
