using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xavissa.Frontend.Services
{
    public class DummyPrinterService : IPrinterService
    {
        // Basic no-op printer behavior for design mode

        public string PrinterName { get; set; } = "Dummy Printer";
        public string LabelPrinterName { get; set; } = "Dummy Label Printer";

        public int PaperWidth { get; set; } = 58;
        public double PaperHeight { get; set; } = 200;
        public int FontSize { get; set; } = 12;
        public double LabelWidthMm { get; set; } = 40;
        public double LabelHeightMm { get; set; } = 30;
        public double LabelGapMm { get; set; } = 2;
        public double LabelHorizontalPaddingMm { get; set; } = 2;
        public double LabelVerticalPaddingMm { get; set; } = 1.5;
        public double LabelBarcodeWidthMm { get; set; } = 34;
        public double LabelBarcodeHeightMm { get; set; } = 11;
        public int LabelFontSize { get; set; } = 8;

        public int MarginLeft { get; set; } = 5;
        public int MarginRight { get; set; } = 5;

        public string HeaderText { get; set; } = "Dummy Header";
        public string FooterText { get; set; } = "Dummy Footer";
        public bool ShowDateTime { get; set; } = true;

        public bool UseHeaderImage { get; set; } = false;
        public string HeaderImagePath { get; set; } = "";
        public bool PrintImageOnReceipt { get; set; } = false;
        public bool IsDarkMode { get; set; }
        public string LanguageCode { get; set; } = "en-US";
        public string ImagePosition { get; set; } = "Centro";

        public List<string> AvailablePrinters { get; } =
            new() { "Dummy Printer A", "Dummy Printer B" };

        public void PrintOrSaveReceipt(string receiptContent, string? receiptNumber = null)
        {
            // No-op in design mode
            Console.WriteLine("DummyPrinterService.PrintOrSaveReceipt()");
            Console.WriteLine(receiptContent);
        }

        public void PrintBarcodeLabel(string title, string subtitle, string sku, decimal price, string barcode, string imagePath, int quantity = 1)
        {
            Console.WriteLine($"DummyPrinterService.PrintBarcodeLabel(): {title} | {subtitle} | {sku} | {price} | {barcode} | {imagePath} | copies={quantity}");
        }

        public void SaveConfiguration()
        {
            // No-op
        }
    }
}
