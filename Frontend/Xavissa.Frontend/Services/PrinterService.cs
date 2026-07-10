using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;

namespace Xavissa.Frontend.Services
{
    public class PrinterService : IPrinterService
    {
        private static readonly string[] PreferredBarcodePrinterKeywords =
        [
            "ZEBRA",
            "ZDESIGNER",
            "XPRINTER",
            "X-PRINTER",
            "TSC",
            "BIXOLON",
            "CITIZEN",
            "GODEX",
            "ARGOX",
            "DATAMAX",
            "BROTHER",
            "DYMO",
            "SATO",
            "TOSHIBA TEC",
            "EPSON TM",
        ];

        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Xavissa",
            "printerconfig.json"
        );

        // Singleton instance
        private static readonly Lazy<PrinterService> _instance = new(() => new PrinterService());
        public static PrinterService Instance => _instance.Value;

        // Core printer settings
        public string PrinterName { get; set; }
        public string LabelPrinterName { get; set; } = "";
        public int PaperWidth { get; set; } = 80;
        public double PaperHeight { get; set; } = 150;
        public int FontSize { get; set; } = 10;
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

        // UI and customization options
        public string HeaderText { get; set; } = "Loja Exemplo";
        public string FooterText { get; set; } = "Obrigado pela preferência!";
        public bool ShowDateTime { get; set; } = true;
        public string HeaderImagePath { get; set; }
        public bool UseHeaderImage { get; set; } = false;
        public bool PrintImageOnReceipt { get; set; } = false;
        public bool IsDarkMode { get; set; }
        public string LanguageCode { get; set; } = "en-US";

        public string ImagePosition { get; set; } = "Centro";

        // Debug/testing
        public bool SimulatePrint { get; set; } = false;

        // Available printers
        public List<string> AvailablePrinters { get; private set; } = new();

        public PrinterService(bool skipLoad = false)
        {
            if (!skipLoad)
            {
                Console.WriteLine($"[PrinterService] Using config path: {ConfigFilePath}");
                LoadConfig();
                LoadAvailablePrinters();
            }
        }

        #region Config Management

        public static List<string> GetAvailablePrinters()
        {
            return PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        }

        private void LoadAvailablePrinters()
        {
            AvailablePrinters = GetAvailablePrinters();
        }

        public void SaveConfiguration()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);

                var config = new AppConfiguration
                {
                    PrinterName = PrinterName,
                    LabelPrinterName = LabelPrinterName,
                    PaperWidth = PaperWidth,
                    PaperHeight = PaperHeight,
                    FontSize = FontSize,
                    LabelWidthMm = LabelWidthMm,
                    LabelHeightMm = LabelHeightMm,
                    LabelGapMm = LabelGapMm,
                    LabelHorizontalPaddingMm = LabelHorizontalPaddingMm,
                    LabelVerticalPaddingMm = LabelVerticalPaddingMm,
                    LabelBarcodeWidthMm = LabelBarcodeWidthMm,
                    LabelBarcodeHeightMm = LabelBarcodeHeightMm,
                    LabelFontSize = LabelFontSize,
                    MarginLeft = MarginLeft,
                    MarginRight = MarginRight,
                    HeaderText = HeaderText,
                    FooterText = FooterText,
                    ShowDateTime = ShowDateTime,
                    HeaderImagePath = HeaderImagePath,
                    UseHeaderImage = UseHeaderImage,
                    PrintImageOnReceipt = PrintImageOnReceipt,
                    ImagePosition = ImagePosition,
                    IsDarkMode = IsDarkMode,
                    LanguageCode = LanguageCode,
                };

                File.WriteAllText(
                    ConfigFilePath,
                    JsonSerializer.Serialize(
                        config,
                        new JsonSerializerOptions { WriteIndented = true }
                    )
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save printer config: {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json);

                    if (config != null)
                        if (config != null)
                        {
                            PrinterName = config.PrinterName;
                            LabelPrinterName = config.LabelPrinterName;
                            PaperWidth = config.PaperWidth;
                            PaperHeight = config.PaperHeight;
                            FontSize = config.FontSize;
                            LabelWidthMm = config.LabelWidthMm <= 0 ? 40 : config.LabelWidthMm;
                            LabelHeightMm = config.LabelHeightMm <= 0 ? 30 : config.LabelHeightMm;
                            LabelGapMm = config.LabelGapMm < 0 ? 0 : config.LabelGapMm;
                            LabelHorizontalPaddingMm = config.LabelHorizontalPaddingMm <= 0 ? 2 : config.LabelHorizontalPaddingMm;
                            LabelVerticalPaddingMm = config.LabelVerticalPaddingMm <= 0 ? 1.5 : config.LabelVerticalPaddingMm;
                            LabelBarcodeWidthMm = config.LabelBarcodeWidthMm <= 0 ? 34 : config.LabelBarcodeWidthMm;
                            LabelBarcodeHeightMm = config.LabelBarcodeHeightMm <= 0 ? 11 : config.LabelBarcodeHeightMm;
                            LabelFontSize = config.LabelFontSize <= 0 ? 8 : config.LabelFontSize;
                            MarginLeft = config.MarginLeft;
                            MarginRight = config.MarginRight;
                            HeaderText = config.HeaderText;
                            FooterText = config.FooterText;
                            ShowDateTime = config.ShowDateTime;
                            HeaderImagePath = config.HeaderImagePath;
                            UseHeaderImage = config.UseHeaderImage;
                            PrintImageOnReceipt = config.PrintImageOnReceipt;
                            ImagePosition = config.ImagePosition;
                            IsDarkMode = config.IsDarkMode;
                            LanguageCode = string.IsNullOrWhiteSpace(config.LanguageCode)
                                ? "en-US"
                                : config.LanguageCode;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load printer config: {ex.Message}");
            }
        }

        #endregion

        #region Printing Logic

        public void PrintOrSaveReceipt(string receiptContent, string receiptNumber = "")
        {
            Console.WriteLine("[PrinterService] PrintOrSaveReceipt called.");

            if (SimulatePrint)
            {
                Console.WriteLine("=== Simulated Print ===");
                Console.WriteLine(receiptContent);
                Console.WriteLine("=======================");
                return;
            }

            bool printerAvailable =
                !string.IsNullOrWhiteSpace(PrinterName)
                && PrinterSettings.InstalledPrinters.Cast<string>().Contains(PrinterName);

            Console.WriteLine(
                $"[PrinterService] Printer available: {printerAvailable} (PrinterName='{PrinterName}')"
            );

            if (printerAvailable)
            {
                try
                {
                    var printDoc = new PrintDocument();
                    printDoc.PrinterSettings.PrinterName = PrinterName;

                    printDoc.PrintPage += (sender, e) =>
                    {
                        Console.WriteLine("[PrinterService] PrintPage event started.");

                        using var font = new Font("Consolas", FontSize);
                        using var boldFont = new Font("Consolas", FontSize, FontStyle.Bold);
                        float y = 10;

                        foreach (var line in receiptContent.Split('\n'))
                        {
                            if (TryDrawHeaderImage(line, e, ref y))
                                continue; // skip the placeholder line

                            Font drawFont = font;
                            string text = line;

                            if (line.StartsWith("[BOLD]"))
                            {
                                drawFont = boldFont;
                                text = line.Substring(6); // remove [BOLD] marker
                            }

                            // Center text
                            SizeF textSize = e.Graphics.MeasureString(text, drawFont);
                            float x =
                                MarginLeft
                                + (e.PageBounds.Width - MarginLeft - MarginRight - textSize.Width)
                                    / 2;

                            // Draw normal text
                            e.Graphics.DrawString(text, drawFont, Brushes.Black, x, y);
                            y += drawFont.Height;
                        }

                        Console.WriteLine("[PrinterService] PrintPage event completed.");
                    };

                    Console.WriteLine("[PrinterService] Sending print job...");
                    printDoc.Print();
                    Console.WriteLine("[PrinterService] Print job completed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PrinterService] ERROR during printing: {ex.Message}");
                }
            }
            else
            {
                SaveReceiptAsPdfFallback(receiptContent);
            }
        }

        public void PrintBarcodeLabel(string title, string subtitle, string sku, decimal price, string barcode, string imagePath, int quantity = 1)
        {
            if (!string.IsNullOrWhiteSpace(TryResolveBarcodePrinterName()))
            {
                try
                {
                    PrintBarcodeLabelToPrinter(title, subtitle, sku, price, barcode, imagePath, quantity);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PrinterService] Barcode print failed, falling back to PDF: {ex.Message}");
                }
            }

            SaveBarcodeLabelAsPdf(title, subtitle, sku, price, barcode, imagePath, quantity);
        }

        private string? TryResolveBarcodePrinterName()
        {
            var installedPrinters = PrinterSettings.InstalledPrinters.Cast<string>().ToList();

            if (!string.IsNullOrWhiteSpace(LabelPrinterName)
                && installedPrinters.Contains(LabelPrinterName, StringComparer.OrdinalIgnoreCase))
                return installedPrinters.First(name => string.Equals(name, LabelPrinterName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(PrinterName)
                && installedPrinters.Contains(PrinterName, StringComparer.OrdinalIgnoreCase))
                return installedPrinters.First(name => string.Equals(name, PrinterName, StringComparison.OrdinalIgnoreCase));

            return installedPrinters.FirstOrDefault(name =>
                PreferredBarcodePrinterKeywords.Any(keyword =>
                    name.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        private void PrintBarcodeLabelToPrinter(string title, string subtitle, string sku, decimal price, string barcode, string imagePath, int quantity)
        {
            var printerName = TryResolveBarcodePrinterName()
                ?? throw new InvalidOperationException("No barcode printer is available.");

            using var barcodeImage = PrepareThermalImage(imagePath, 560);
            var labelFontSize = Math.Max(LabelFontSize, 6);
            using var titleFont = new Font("Consolas", Math.Max(labelFontSize + 1, 7), FontStyle.Bold);
            using var bodyFont = new Font("Consolas", labelFontSize, FontStyle.Regular);

            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = printerName;
            printDoc.PrintController = new StandardPrintController();
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            printDoc.DefaultPageSettings.PaperSize = BuildLabelPaperSize();
            printDoc.OriginAtMargins = false;
            var labelsRemaining = Math.Max(quantity, 1);

            printDoc.PrintPage += (_, e) =>
            {
                e.Graphics.Clear(System.Drawing.Color.White);
                var totalPageWidth = MmToHundredthsInch(LabelWidthMm);
                var contentHeight = MmToHundredthsInch(LabelHeightMm);
                var paddingX = MmToHundredthsInch(LabelHorizontalPaddingMm);
                var paddingY = MmToHundredthsInch(LabelVerticalPaddingMm);
                var contentLeft = paddingX;
                var contentTop = paddingY;
                var contentWidth = Math.Max(20, totalPageWidth - (paddingX * 2));
                var usableHeight = Math.Max(20, contentHeight - (paddingY * 2));
                var y = contentTop;

                y = DrawWrappedCenteredText(
                    e.Graphics,
                    title,
                    titleFont,
                    contentLeft,
                    contentWidth,
                    y,
                    Math.Min(MmToHundredthsInch(6.5), usableHeight / 4),
                    2
                );

                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    y = DrawWrappedCenteredText(
                        e.Graphics,
                        subtitle,
                        bodyFont,
                        contentLeft,
                        contentWidth,
                        y + 2,
                        Math.Min(MmToHundredthsInch(4.5), usableHeight / 6),
                        1
                    );
                }

                y += 2;

                var targetBarcodeWidth = Math.Min(contentWidth, MmToHundredthsInch(LabelBarcodeWidthMm));
                var targetBarcodeHeight = Math.Min(MmToHundredthsInch(LabelBarcodeHeightMm), usableHeight);
                var fitScale = Math.Min(
                    (double)targetBarcodeWidth / barcodeImage.Width,
                    (double)targetBarcodeHeight / barcodeImage.Height
                );
                fitScale = Math.Min(fitScale, 1d);
                var barcodeWidth = Math.Max(20, (int)Math.Round(barcodeImage.Width * fitScale));
                var barcodeHeight = Math.Max(12, (int)Math.Round(barcodeImage.Height * fitScale));
                var barcodeX = contentLeft + (contentWidth - barcodeWidth) / 2;
                e.Graphics.DrawImage(barcodeImage, barcodeX, y, barcodeWidth, barcodeHeight);
                y += barcodeHeight + 2;

                var metadataLineHeight = Math.Max((int)Math.Ceiling(bodyFont.GetHeight(e.Graphics)), 8);
                var remainingHeight = contentTop + usableHeight - y;
                var showPrice = remainingHeight >= metadataLineHeight * 3;
                var showSku = remainingHeight >= metadataLineHeight * 2;

                y = DrawSingleLineCenteredText(e.Graphics, barcode, bodyFont, contentLeft, contentWidth, y);

                if (showSku)
                    y = DrawSingleLineCenteredText(e.Graphics, $"SKU: {sku}", bodyFont, contentLeft, contentWidth, y);

                if (showPrice)
                    DrawSingleLineCenteredText(e.Graphics, $"Price: {price:F2} MZN", bodyFont, contentLeft, contentWidth, y);

                labelsRemaining--;
                e.HasMorePages = labelsRemaining > 0;
            };

            printDoc.Print();
        }

        private static int DrawCenteredText(Graphics graphics, string text, Font font, int left, int width, int y)
        {
            var size = graphics.MeasureString(text, font);
            var x = left + (width - size.Width) / 2f;
            graphics.DrawString(text, font, Brushes.Black, x, y);
            return y + font.Height;
        }

        private static int DrawSingleLineCenteredText(Graphics graphics, string text, Font font, int left, int width, int y)
        {
            using var format = CreateCenteredStringFormat();
            using var brush = new SolidBrush(System.Drawing.Color.Black);
            var bounds = new RectangleF(left, y, width, font.GetHeight(graphics) + 6);
            graphics.DrawString(text, font, brush, bounds, format);
            return y + (int)Math.Ceiling(font.GetHeight(graphics)) + 2;
        }

        private static int DrawWrappedCenteredText(Graphics graphics, string text, Font baseFont, int left, int width, int y, int maxHeight, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text) || maxHeight <= 0)
                return y;

            using var format = CreateCenteredStringFormat();
            using var brush = new SolidBrush(System.Drawing.Color.Black);
            using var layoutFont = FitFontToBounds(graphics, text, baseFont, width, maxHeight, maxLines);
            var bounds = new RectangleF(left, y, width, maxHeight);
            graphics.DrawString(text, layoutFont, brush, bounds, format);
            return y + (int)Math.Ceiling(Math.Min(graphics.MeasureString(text, layoutFont, width, format).Height, maxHeight));
        }

        private static Font FitFontToBounds(Graphics graphics, string text, Font template, int width, int height, int maxLines)
        {
            var size = template.Size;
            using var format = CreateCenteredStringFormat();

            while (size > 6f)
            {
                var candidate = new Font(template.FontFamily, size, template.Style);
                var measured = graphics.MeasureString(text, candidate, width, format);
                var lineHeight = candidate.GetHeight(graphics);
                var estimatedLines = Math.Max(1, (int)Math.Ceiling(measured.Height / Math.Max(lineHeight, 1f)));

                if (measured.Height <= height && estimatedLines <= maxLines)
                    return candidate;

                candidate.Dispose();
                size -= 0.5f;
            }

            return new Font(template.FontFamily, 6f, template.Style);
        }

        private static StringFormat CreateCenteredStringFormat()
        {
            return new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };
        }

        private PaperSize BuildLabelPaperSize()
        {
            var width = Math.Max(MmToHundredthsInch(LabelWidthMm), 100);
            var height = Math.Max(MmToHundredthsInch(LabelHeightMm + LabelGapMm), 80);
            return new PaperSize("XP-P203A 40x30", width, height);
        }

        private static int MmToHundredthsInch(double millimetres)
        {
            return (int)Math.Round(millimetres / 25.4d * 100d);
        }

        private System.Drawing.Image PrepareThermalImage(string path, int maxWidth = 576)
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var original = System.Drawing.Image.FromStream(fileStream, useEmbeddedColorManagement: false, validateImageData: true);

            // Resize if needed
            int newWidth = original.Width > maxWidth ? maxWidth : original.Width;
            int newHeight = (int)((double)newWidth / original.Width * original.Height);

            using var resized = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);

            using (var g = Graphics.FromImage(resized))
            {
                g.Clear(System.Drawing.Color.White);
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            // Convert to monochrome for better thermal-printer compatibility.
            var bw = new Bitmap(newWidth, newHeight);

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    int gray = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                    bw.SetPixel(
                        x,
                        y,
                        gray < 128 ? System.Drawing.Color.Black : System.Drawing.Color.White
                    );
                }
            }

            return bw;
        }

        private bool TryDrawHeaderImage(string line, PrintPageEventArgs e, ref float y)
        {
            var trimmedLine = line.Trim();

            if (!trimmedLine.StartsWith("[HEADER_IMAGE|", StringComparison.Ordinal))
                return false;

            Console.WriteLine("[PrinterService] Found header image placeholder.");

            // Remove prefix and trailing bracket from token like:
            // [HEADER_IMAGE|C:\path\logo.png|Centro]
            string content = trimmedLine.Substring("[HEADER_IMAGE|".Length);
            if (content.EndsWith("]", StringComparison.Ordinal))
                content = content[..^1];

            var parts = content.Split('|');

            if (parts.Length == 0)
                return true;

            string path = parts[0].Trim();
            string pos = parts.Length > 1 ? parts[1].Trim() : "Centro";

            Console.WriteLine($"[PrinterService] Header image path: {path}, position: {pos}");

            if (!File.Exists(path))
            {
                Console.WriteLine($"[PrinterService] ERROR: Image file does not exist: {path}");
                return true; // skip printing the placeholder text
            }

            try
            {
                float printableWidth = Math.Max(1, e.PageBounds.Width - MarginLeft - MarginRight);
                using var img = PrepareThermalImage(path, (int)printableWidth);

                float maxHeight = e.PageBounds.Height * 0.15f;
                float scale = Math.Min(printableWidth / img.Width, maxHeight / img.Height);
                scale = Math.Min(scale, 1f);
                float drawWidth = img.Width * scale;
                float drawHeight = img.Height * scale;

                float x = pos.ToLowerInvariant() switch
                {
                    "center" => MarginLeft + (printableWidth - drawWidth) / 2f,
                    "middle" => MarginLeft + (printableWidth - drawWidth) / 2f,
                    "centro" => MarginLeft + (printableWidth - drawWidth) / 2f,
                    "right" => MarginLeft + printableWidth - drawWidth,
                    "direita" => MarginLeft + printableWidth - drawWidth,
                    "left" => MarginLeft,
                    "esquerda" => MarginLeft,
                    _ => MarginLeft, // Esquerda
                };

                e.Graphics.DrawImage(img, x, y, drawWidth, drawHeight);
                y += drawHeight + 10;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PrinterService] ERROR loading/drawing image: {ex.Message}");
            }

            return true;
        }

        private void SaveReceiptAsPdfFallback(string receiptContent)
        {
            Console.WriteLine("[PrinterService] Printer not available. Saving PDF fallback.");

            string folderPath = Path.Combine(Path.GetTempPath(), "Receipts");
            Directory.CreateDirectory(folderPath);

            string pdfPath = Path.Combine(
                folderPath,
                $"Receipt_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            );

            Console.WriteLine($"[PrinterService] PDF Path: {pdfPath}");
            SaveReceiptAsPdf(receiptContent, pdfPath);
        }

        private void SaveReceiptAsPdf(string receiptContent, string pdfPath)
        {
            try
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.Content()
                            .Text(receiptContent)
                            .FontSize(FontSize)
                            .FontFamily("Consolas");
                    });
                });

                doc.GeneratePdf(pdfPath);
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save PDF: {ex.Message}");
            }
        }

        private void SaveBarcodeLabelAsPdf(string title, string subtitle, string sku, decimal price, string barcode, string imagePath, int quantity)
        {
            try
            {
                var folderPath = Path.Combine(Path.GetTempPath(), "Receipts");
                Directory.CreateDirectory(folderPath);

                var pdfPath = Path.Combine(
                    folderPath,
                    $"BarcodeLabel_{SanitizeFileName(barcode)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

                var barcodeBytes = File.Exists(imagePath) ? File.ReadAllBytes(imagePath) : Array.Empty<byte>();
                var totalLabels = Math.Max(quantity, 1);

                var doc = Document.Create(container =>
                {
                    for (var labelIndex = 0; labelIndex < totalLabels; labelIndex++)
                    {
                        container.Page(page =>
                        {
                            page.Size(80, 60, Unit.Millimetre);
                            page.Margin(6);
                            page.PageColor(Colors.White);
                            page.Content().Column(column =>
                            {
                                column.Spacing(4);
                                column.Item().AlignCenter().Text(title).FontSize(12).SemiBold();

                                if (!string.IsNullOrWhiteSpace(subtitle))
                                    column.Item().AlignCenter().Text(subtitle).FontSize(9);

                                if (barcodeBytes.Length > 0)
                                {
                                    column.Item().AlignCenter().Image(barcodeBytes).FitWidth();
                                }

                                column.Item().AlignCenter().Text(barcode).FontSize(9);
                                column.Item().AlignCenter().Text($"SKU: {sku}").FontSize(9);
                                column.Item().AlignCenter().Text($"Price: {price:F2} MZN").FontSize(9).SemiBold();
                            });
                        });
                    }
                });

                doc.GeneratePdf(pdfPath);
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save barcode label PDF: {ex.Message}");
            }
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        }

        #endregion
    }

    public class AppConfiguration
    {
        public string PrinterName { get; set; } = "";
        public string LabelPrinterName { get; set; } = "";
        public int PaperWidth { get; set; }
        public double PaperHeight { get; set; }
        public int FontSize { get; set; }
        public double LabelWidthMm { get; set; } = 40;
        public double LabelHeightMm { get; set; } = 30;
        public double LabelGapMm { get; set; } = 2;
        public double LabelHorizontalPaddingMm { get; set; } = 2;
        public double LabelVerticalPaddingMm { get; set; } = 1.5;
        public double LabelBarcodeWidthMm { get; set; } = 34;
        public double LabelBarcodeHeightMm { get; set; } = 11;
        public int LabelFontSize { get; set; } = 8;
        public int MarginLeft { get; set; }
        public int MarginRight { get; set; }
        public string HeaderText { get; set; } = "";
        public string FooterText { get; set; } = "";
        public bool ShowDateTime { get; set; }
        public string HeaderImagePath { get; set; } = "";
        public bool UseHeaderImage { get; set; }
        public bool PrintImageOnReceipt { get; set; }
        public string ImagePosition { get; set; } = "Centro";
        public bool IsDarkMode { get; set; }
        public string LanguageCode { get; set; } = "en-US";
    }
}
