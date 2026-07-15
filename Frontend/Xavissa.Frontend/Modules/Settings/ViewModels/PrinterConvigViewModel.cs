using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    /// <summary>
    /// ViewModel controlling printer configuration settings:
    /// paper size, header/footer, optional image/logo, and printer selection.
    /// </summary>
    public class PrinterConfigViewModel : ViewModelBase
    {
        private readonly IPrinterService _printer;
        private readonly INotificationService _notify;
        private readonly ILocalizationService _localization;

        public PrinterConfigViewModel(
            IPrinterService printer,
            INotificationService notify,
            ILocalizationService localization)
        {
            _printer = printer ?? throw new ArgumentNullException(nameof(printer));
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));

            AvailablePrinters = new ObservableCollection<string>(_printer.AvailablePrinters);
            PopularPaperWidths = new ObservableCollection<int> { 58, 80, 112, 210 };
            ImagePositions = new ObservableCollection<string> { "Center", "Left", "Right" };

            LoadFromService();

            SaveConfigCommand = ReactiveCommand.Create(SaveConfig);
            BrowseImageCommand = ReactiveCommand.CreateFromTask<Window>(BrowseImageAsync);
        }

        public ObservableCollection<string> AvailablePrinters { get; }
        public ObservableCollection<int> PopularPaperWidths { get; }
        public ObservableCollection<string> ImagePositions { get; }

        private string _printerName = string.Empty;
        public string ReceiptPrinterName
        {
            get => _printerName;
            set => this.RaiseAndSetIfChanged(ref _printerName, value);
        }

        private string _labelPrinterName = string.Empty;
        public string LabelPrinterName
        {
            get => _labelPrinterName;
            set => this.RaiseAndSetIfChanged(ref _labelPrinterName, value);
        }

        private int _paperWidth;
        public int PaperWidth
        {
            get => _paperWidth;
            set => this.RaiseAndSetIfChanged(ref _paperWidth, value);
        }

        private decimal _labelWidthMm;
        public decimal LabelWidthMm
        {
            get => _labelWidthMm;
            set => this.RaiseAndSetIfChanged(ref _labelWidthMm, value);
        }

        private decimal _labelHeightMm;
        public decimal LabelHeightMm
        {
            get => _labelHeightMm;
            set => this.RaiseAndSetIfChanged(ref _labelHeightMm, value);
        }

        private decimal _labelGapMm;
        public decimal LabelGapMm
        {
            get => _labelGapMm;
            set => this.RaiseAndSetIfChanged(ref _labelGapMm, value);
        }

        private decimal _labelBarcodeWidthMm;
        public decimal LabelBarcodeWidthMm
        {
            get => _labelBarcodeWidthMm;
            set => this.RaiseAndSetIfChanged(ref _labelBarcodeWidthMm, value);
        }

        private decimal _labelBarcodeHeightMm;
        public decimal LabelBarcodeHeightMm
        {
            get => _labelBarcodeHeightMm;
            set => this.RaiseAndSetIfChanged(ref _labelBarcodeHeightMm, value);
        }

        private string _headerText = string.Empty;
        public string HeaderText
        {
            get => _headerText;
            set => this.RaiseAndSetIfChanged(ref _headerText, value);
        }

        private string _footerText = string.Empty;
        public string FooterText
        {
            get => _footerText;
            set => this.RaiseAndSetIfChanged(ref _footerText, value);
        }

        private bool _showDateTime;
        public bool ShowDateTime
        {
            get => _showDateTime;
            set => this.RaiseAndSetIfChanged(ref _showDateTime, value);
        }

        private string _headerImagePath = string.Empty;
        public string HeaderImagePath
        {
            get => _headerImagePath;
            set => this.RaiseAndSetIfChanged(ref _headerImagePath, value);
        }

        private bool _useHeaderImage;
        public bool UseHeaderImage
        {
            get => _useHeaderImage;
            set => this.RaiseAndSetIfChanged(ref _useHeaderImage, value);
        }

        private bool _printImageOnReceipt;
        public bool PrintImageOnReceipt
        {
            get => _printImageOnReceipt;
            set => this.RaiseAndSetIfChanged(ref _printImageOnReceipt, value);
        }

        private string _imagePosition = "Center";
        public string ImagePosition
        {
            get => _imagePosition;
            set => this.RaiseAndSetIfChanged(ref _imagePosition, value);
        }

        public ReactiveCommand<Unit, Unit> SaveConfigCommand { get; }
        public ReactiveCommand<Window, Unit> BrowseImageCommand { get; }

        private void LoadFromService()
        {
            ReceiptPrinterName = _printer.PrinterName;
            LabelPrinterName = _printer.LabelPrinterName;
            PaperWidth = _printer.PaperWidth;
            LabelWidthMm = (decimal)_printer.LabelWidthMm;
            LabelHeightMm = (decimal)_printer.LabelHeightMm;
            LabelGapMm = (decimal)_printer.LabelGapMm;
            LabelBarcodeWidthMm = (decimal)_printer.LabelBarcodeWidthMm;
            LabelBarcodeHeightMm = (decimal)_printer.LabelBarcodeHeightMm;
            HeaderText = _printer.HeaderText;
            FooterText = _printer.FooterText;
            ShowDateTime = _printer.ShowDateTime;

            HeaderImagePath = _printer.HeaderImagePath;
            UseHeaderImage = _printer.UseHeaderImage;
            PrintImageOnReceipt = _printer.PrintImageOnReceipt;
            ImagePosition = NormalizeImagePosition(_printer.ImagePosition);
        }

        private async Task BrowseImageAsync(Window window)
        {
            if (window is null)
                return;

            var options = new FilePickerOpenOptions
            {
                Title = _localization.GetString("Loc.SelectImage"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(_localization.GetString("Loc.Images"))
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff" },
                    },
                    FilePickerFileTypes.All,
                },
            };

            var result = await window.StorageProvider.OpenFilePickerAsync(options);

            if (result != null && result.Count > 0)
            {
                HeaderImagePath = result[0].Path.LocalPath;
                _notify.Show(_localization.GetString("Loc.PrinterConfigLoaded"));
            }
        }

        private void SaveConfig()
        {
            try
            {
                _printer.PrinterName = ReceiptPrinterName;
                _printer.LabelPrinterName = LabelPrinterName;
                _printer.PaperWidth = PaperWidth;
                _printer.LabelWidthMm = (double)LabelWidthMm;
                _printer.LabelHeightMm = (double)LabelHeightMm;
                _printer.LabelGapMm = (double)LabelGapMm;
                _printer.LabelBarcodeWidthMm = (double)LabelBarcodeWidthMm;
                _printer.LabelBarcodeHeightMm = (double)LabelBarcodeHeightMm;
                _printer.HeaderText = HeaderText;
                _printer.FooterText = FooterText;
                _printer.ShowDateTime = ShowDateTime;
                _printer.HeaderImagePath = HeaderImagePath;
                _printer.UseHeaderImage = UseHeaderImage;
                _printer.PrintImageOnReceipt = PrintImageOnReceipt;
                _printer.ImagePosition = ImagePosition;

                _printer.SaveConfiguration();
                _notify.Show(_localization.GetString("Loc.PrinterConfigSaved"));
            }
            catch (Exception ex)
            {
                _notify.Show(_localization.GetString("Loc.PrinterConfigSaveFailed"));
                Console.WriteLine($"SaveConfig error: {ex}");
            }
        }

        private static string NormalizeImagePosition(string value) =>
            value.Equals("Centro", StringComparison.OrdinalIgnoreCase) ? "Center"
            : value.Equals("Esquerda", StringComparison.OrdinalIgnoreCase) ? "Left"
            : value.Equals("Direita", StringComparison.OrdinalIgnoreCase) ? "Right"
            : value;
    }
}
