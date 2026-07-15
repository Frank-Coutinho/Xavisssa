using System.Collections.Generic;

namespace Xavissa.Frontend.Services
{
    public interface IPrinterService
    {
        // Printing
        void PrintOrSaveReceipt(string receiptContent, string? receiptNumber = null);
        void PrintBarcodeLabel(string title, string subtitle, string sku, decimal price, string barcode, string imagePath, int quantity = 1);

        // Printer config
        string LabelPrinterName { get; set; }
        string PrinterName { get; set; }
        int PaperWidth { get; set; }
        double PaperHeight { get; set; }
        int FontSize { get; set; }
        int MarginLeft { get; set; }
        int MarginRight { get; set; }
        double LabelWidthMm { get; set; }
        double LabelHeightMm { get; set; }
        double LabelGapMm { get; set; }
        double LabelHorizontalPaddingMm { get; set; }
        double LabelVerticalPaddingMm { get; set; }
        double LabelBarcodeWidthMm { get; set; }
        double LabelBarcodeHeightMm { get; set; }
        int LabelFontSize { get; set; }

        string HeaderText { get; set; }
        string FooterText { get; set; }
        bool ShowDateTime { get; set; }

        bool UseHeaderImage { get; set; }
        string HeaderImagePath { get; set; }

        bool PrintImageOnReceipt { get; set; }
        string ImagePosition { get; set; }
        public bool IsDarkMode { get; set; }
        string LanguageCode { get; set; }

        // Printer detection
        List<string> AvailablePrinters { get; }

        // Save to disk
        void SaveConfiguration();
    }
}
