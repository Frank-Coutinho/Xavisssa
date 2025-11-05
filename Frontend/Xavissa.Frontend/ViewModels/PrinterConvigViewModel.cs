using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class PrinterConfigViewModel : ViewModelBase
    {
        private string _printerName;
        private int _paperWidth;
        private string _headerText;
        private string _footerText;
        private bool _showDateTime;
        private string _headerImagePath;
        private bool _useHeaderImage;
        private bool _printImageOnReceipt;
        private string _imagePosition;

        private readonly PrinterService _printerService;

        public PrinterConfigViewModel()
        {
            // Initialize collections
            AvailablePrinters = new ObservableCollection<string>(
                PrinterService.GetAvailablePrinters()
            );
            PopularPaperWidths = new ObservableCollection<int> { 58, 80, 112, 210 };
            ImagePositions = new ObservableCollection<string> { "Centro", "Esquerda", "Direita" };

            // Load saved configuration
            _printerService = new PrinterService();
            PrinterName = _printerService.PrinterName;
            PaperWidth = _printerService.PaperWidth;
            HeaderText = _printerService.HeaderText;
            FooterText = _printerService.FooterText;
            ShowDateTime = _printerService.ShowDateTime;
            HeaderImagePath = _printerService.HeaderImagePath;
            UseHeaderImage = _printerService.UseHeaderImage;
            PrintImageOnReceipt = _printerService.PrintImageOnReceipt;
            ImagePosition = _printerService.ImagePosition;

            // Commands
            SaveConfigCommand = ReactiveCommand.Create(SaveConfig);
            BrowseImageCommand = ReactiveCommand.CreateFromTask<Window>(async window =>
            {
                if (window == null)
                    return;

                var result = await window.StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        Title = "Selecione a imagem do cabeçalho",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Imagens")
                            {
                                Patterns = new[] { "*.png", "*.jpg", "*.jpeg" },
                            },
                        },
                    }
                );

                if (result?.Count > 0)
                    HeaderImagePath = result[0].Path.LocalPath;
            });
        }

        #region Properties

        public ObservableCollection<string> AvailablePrinters { get; }
        public ObservableCollection<int> PopularPaperWidths { get; }
        public ObservableCollection<string> ImagePositions { get; }

        public string PrinterName
        {
            get => _printerName;
            set => this.RaiseAndSetIfChanged(ref _printerName, value);
        }

        public int PaperWidth
        {
            get => _paperWidth;
            set => this.RaiseAndSetIfChanged(ref _paperWidth, value);
        }

        public string HeaderText
        {
            get => _headerText;
            set => this.RaiseAndSetIfChanged(ref _headerText, value);
        }

        public string FooterText
        {
            get => _footerText;
            set => this.RaiseAndSetIfChanged(ref _footerText, value);
        }

        public bool ShowDateTime
        {
            get => _showDateTime;
            set => this.RaiseAndSetIfChanged(ref _showDateTime, value);
        }

        public string HeaderImagePath
        {
            get => _headerImagePath;
            set => this.RaiseAndSetIfChanged(ref _headerImagePath, value);
        }

        public bool UseHeaderImage
        {
            get => _useHeaderImage;
            set => this.RaiseAndSetIfChanged(ref _useHeaderImage, value);
        }

        public bool PrintImageOnReceipt
        {
            get => _printImageOnReceipt;
            set => this.RaiseAndSetIfChanged(ref _printImageOnReceipt, value);
        }

        public string ImagePosition
        {
            get => _imagePosition;
            set => this.RaiseAndSetIfChanged(ref _imagePosition, value);
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> SaveConfigCommand { get; }
        public ReactiveCommand<Window, Unit> BrowseImageCommand { get; }

        #endregion

        #region Methods

        private void SaveConfig()
        {
            _printerService.PrinterName = PrinterName;
            _printerService.PaperWidth = PaperWidth;
            _printerService.HeaderText = HeaderText;
            _printerService.FooterText = FooterText;
            _printerService.ShowDateTime = ShowDateTime;
            _printerService.HeaderImagePath = HeaderImagePath;
            _printerService.UseHeaderImage = UseHeaderImage;
            _printerService.PrintImageOnReceipt = PrintImageOnReceipt;
            _printerService.ImagePosition = ImagePosition;

            _printerService.SaveConfiguration();
        }

        #endregion
    }
}
