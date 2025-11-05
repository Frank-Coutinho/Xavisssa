using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace Xavissa.Frontend.Services
{
    public class PrinterService
    {
        private string _printerName;
        private int _paperWidth;
        private string _headerText = "RECIBO";
        private string _footerText = "Obrigado pela sua compra!";
        private bool _showDateTime = true;

        // Image support (Windows only)
        private string _headerImagePath;
        private bool _useHeaderImage = true;
        private bool _printImageOnReceipt = true;
        private string _imagePosition = "Centro";

        private const string CONFIG_FILE = "printer_config.json";

        public PrinterService(string printerName = "", int paperWidth = 280, bool loadConfig = true)
        {
            _printerName = printerName;
            _paperWidth = paperWidth;

            if (loadConfig)
            {
                LoadConfiguration();
            }
        }

        #region Properties


        public string PrinterName
        {
            get => _printerName;
            set => _printerName = value;
        }
        public int PaperWidth
        {
            get => _paperWidth;
            set => _paperWidth = value;
        }
        public string HeaderText
        {
            get => _headerText;
            set => _headerText = value;
        }
        public string FooterText
        {
            get => _footerText;
            set => _footerText = value;
        }
        public bool ShowDateTime
        {
            get => _showDateTime;
            set => _showDateTime = value;
        }

        public string HeaderImagePath
        {
            get => _headerImagePath;
            set => _headerImagePath = value;
        }
        public bool UseHeaderImage
        {
            get => _useHeaderImage;
            set => _useHeaderImage = value;
        }
        public bool PrintImageOnReceipt
        {
            get => _printImageOnReceipt;
            set => _printImageOnReceipt = value;
        }
        public string ImagePosition
        {
            get => _imagePosition;
            set => _imagePosition = value ?? "Centro";
        }

        #endregion

        #region Configuration


        public void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    new
                    {
                        PrinterName = _printerName,
                        PaperWidth = _paperWidth,
                        HeaderText = _headerText,
                        FooterText = _footerText,
                        ShowDateTime = _showDateTime,
                        HeaderImagePath = _headerImagePath,
                        UseHeaderImage = _useHeaderImage,
                        PrintImageOnReceipt = _printImageOnReceipt,
                        ImagePosition = _imagePosition,
                    },
                    new JsonSerializerOptions { WriteIndented = true }
                );

                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }

        public void LoadConfiguration()
        {
            try
            {
                if (!File.Exists(CONFIG_FILE))
                    return;

                string json = File.ReadAllText(CONFIG_FILE);
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (config == null)
                    return;

                if (config.TryGetValue("PrinterName", out var p))
                    _printerName = p.GetString() ?? "";
                if (config.TryGetValue("PaperWidth", out var w))
                    _paperWidth = w.GetInt32();
                if (config.TryGetValue("HeaderText", out var ht))
                    _headerText = ht.GetString() ?? "";
                if (config.TryGetValue("FooterText", out var ft))
                    _footerText = ft.GetString() ?? "";
                if (config.TryGetValue("ShowDateTime", out var dt))
                    _showDateTime = dt.GetBoolean();
                if (config.TryGetValue("HeaderImagePath", out var hip))
                    _headerImagePath = hip.GetString() ?? "";
                if (config.TryGetValue("UseHeaderImage", out var uhi))
                    _useHeaderImage = uhi.GetBoolean();
                if (config.TryGetValue("PrintImageOnReceipt", out var pi))
                    _printImageOnReceipt = pi.GetBoolean();
                if (config.TryGetValue("ImagePosition", out var ip))
                    _imagePosition = ip.GetString() ?? "Centro";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
            }
        }

        #endregion

        #region Printing

        /// <summary>
        /// Prints receipt as raw text (cross-platform)
        /// </summary>
        public void PrintOrSaveReceipt(string receiptContent)
        {
            if (
                !string.IsNullOrWhiteSpace(_printerName)
                && GetAvailablePrinters().Contains(_printerName)
            )
            {
                PrintOrSaveReceipt(receiptContent);
            }
            else
            {
                string pdfPath = Path.Combine(Path.GetTempPath(), "receipt.pdf");
                SaveReceiptAsPdf(receiptContent, pdfPath);
                Console.WriteLine($"Printer not available. PDF saved at {pdfPath}");
                // Optional: open PDF automatically
                if (OperatingSystem.IsWindows())
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true }
                    );
            }
        }

        public void SaveReceiptAsPdf(string receiptContent, string filePath)
        {
            Document
                .Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(20);
                        page.Size(PageSizes.A4);
                        page.Content()
                            .Column(col =>
                            {
                                if (!string.IsNullOrWhiteSpace(HeaderText))
                                    col.Item().Text(HeaderText).Bold().FontSize(18).AlignCenter();

                                if (ShowDateTime)
                                    col.Item()
                                        .Text($"Date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                                        .FontSize(12)
                                        .AlignCenter();

                                col.Item().Text(receiptContent).FontSize(12);

                                if (!string.IsNullOrWhiteSpace(FooterText))
                                    col.Item().Text(FooterText).Bold().FontSize(14).AlignCenter();
                            });
                    });
                })
                .GeneratePdf(filePath);
        }

        private string BuildReceipt(string content)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(HeaderText))
            {
                sb.AppendLine(HeaderText);
                sb.AppendLine(new string('-', _paperWidth / 8)); // approximate
            }

            if (ShowDateTime)
            {
                sb.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine();
            }

            sb.AppendLine(content);

            if (!string.IsNullOrWhiteSpace(FooterText))
            {
                sb.AppendLine(new string('-', _paperWidth / 8));
                sb.AppendLine(FooterText);
            }

            return sb.ToString();
        }

        #endregion

        #region Platform-specific printing

        private void PrintWindows(string text)
        {
            // Use Notepad or "lpr" on Windows for simplicity
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "receipt.txt");
                File.WriteAllText(tempFile, text, Encoding.UTF8);

                // Use default printer if none specified
                string printer = string.IsNullOrWhiteSpace(_printerName)
                    ? ""
                    : $"/D:\"{_printerName}\"";

                var psi = new System.Diagnostics.ProcessStartInfo("notepad.exe")
                {
                    Arguments = $"{tempFile} {printer}",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows print error: {ex.Message}");
            }
        }

        private void PrintUnix(string text)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "receipt.txt");
                File.WriteAllText(tempFile, text, Encoding.UTF8);

                string printerArg = string.IsNullOrWhiteSpace(_printerName)
                    ? ""
                    : $"-P {_printerName}";

                var psi = new System.Diagnostics.ProcessStartInfo("lp")
                {
                    Arguments = $"{printerArg} {tempFile}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unix print error: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        public static List<string> GetAvailablePrinters()
        {
            var printers = new List<string>();

            if (OperatingSystem.IsWindows())
            {
                // Requires System.Drawing.Common
                foreach (
                    string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters
                )
                {
                    printers.Add(printer);
                }
            }
            else
            {
                // Linux/macOS: use 'lpstat -a'
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("lpstat")
                    {
                        Arguments = "-a",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    var process = System.Diagnostics.Process.Start(psi);
                    string output = process?.StandardOutput.ReadToEnd() ?? "";
                    process?.WaitForExit();

                    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        printers.Add(line.Split(' ')[0]);
                }
                catch
                {
                    // fail silently
                }
            }

            return printers;
        }

        #endregion
    }
}
