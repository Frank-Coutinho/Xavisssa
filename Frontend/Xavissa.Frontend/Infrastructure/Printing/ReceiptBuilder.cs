using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Helpers
{
    public class ReceiptBuilder
    {
        private readonly ReceiptModel _model;
        private readonly IPrinterService _printer;

        public ReceiptBuilder(ReceiptModel model, IPrinterService printer)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _printer = printer ?? throw new ArgumentNullException(nameof(printer));
        }

        public string BuildTextReceipt()
        {
            Console.WriteLine("[ReceiptBuilder] Building receipt content...");

            var sb = new StringBuilder();

            // 🔹 Use fixed character width for thermal printers
            int paperChars = _printer.PaperWidth >= 80 ? 32 : 24;

            string CenterText(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return string.Empty;

                if (text.Length >= paperChars)
                    return text;

                int padding = (paperChars - text.Length) / 2;
                return new string(' ', padding) + text;
            }

            int totalWidth = 10;
            int qtyWidth = 4;
            int nameWidth = Math.Max(10, paperChars - qtyWidth - totalWidth);

            string FormatItemLine(string name, string qty, string total)
            {
                var lines = new StringBuilder();

                for (int i = 0; i < name.Length; i += nameWidth)
                {
                    string namePart =
                        i + nameWidth < name.Length
                            ? name.Substring(i, nameWidth)
                            : name.Substring(i);

                    string qtyPart = i == 0 ? qty.PadLeft(qtyWidth) : new string(' ', qtyWidth);

                    string totalPart =
                        i == 0 ? total.PadLeft(totalWidth) : new string(' ', totalWidth);

                    lines.AppendLine(namePart.PadRight(nameWidth) + qtyPart + totalPart);
                }

                return lines.ToString().TrimEnd();
            }

            // ==========================
            // HEADER IMAGE
            // ==========================
            if (_printer.UseHeaderImage && !string.IsNullOrWhiteSpace(_printer.HeaderImagePath))
            {
                sb.AppendLine(
                    $"[HEADER_IMAGE|{_printer.HeaderImagePath}|{_printer.ImagePosition}]"
                );
                sb.AppendLine();
            }

            // ==========================
            // HEADER TEXT
            // ==========================
            if (!string.IsNullOrWhiteSpace(_printer.HeaderText))
            {
                sb.AppendLine(CenterText(_printer.HeaderText));
                sb.AppendLine(new string('-', paperChars));
            }

            if (_model.IsDemo)
            {
                sb.AppendLine(CenterText("DEMO RECEIPT"));
                sb.AppendLine(CenterText("Sample data - not valid for real sale"));
                sb.AppendLine(new string('-', paperChars));
            }

            // ==========================
            // DATE
            // ==========================
            if (_printer.ShowDateTime)
            {
                sb.AppendLine(CenterText($"Data: {_model.Timestamp:dd/MM/yyyy HH:mm}"));
                sb.AppendLine();
            }

            // ==========================
            // ITEMS HEADER
            // ==========================
            sb.AppendLine(FormatItemLine("Item", "Qty", "Total"));
            sb.AppendLine(new string('-', paperChars));

            // ==========================
            // ITEMS
            // ==========================
            foreach (var item in _model.Items)
            {
                sb.AppendLine(
                    FormatItemLine(
                        item.ProductName,
                        item.Quantity.ToString(),
                        item.Total.ToString("0.00")
                    )
                );
            }

            sb.AppendLine(new string('-', paperChars));

            // ==========================
            // TOTALS
            // ==========================
            sb.AppendLine($"[BOLD]{CenterText($"Subtotal: {_model.Subtotal:0.00}")}");

            if (_model.Discount > 0)
                sb.AppendLine($"[BOLD]{CenterText($"Desconto: {_model.Discount:0.00}")}");

            sb.AppendLine($"[BOLD]{CenterText($"Pagamento: {_model.PaymentMethod}")}");
            sb.AppendLine($"[BOLD]{CenterText($"Total: {_model.FinalTotal:0.00}")}");

            // ==========================
            // FOOTER
            // ==========================
            if (!string.IsNullOrWhiteSpace(_printer.FooterText))
            {
                sb.AppendLine();
                sb.AppendLine(CenterText(_printer.FooterText));
            }

            Console.WriteLine("[ReceiptBuilder] Receipt built successfully.");
            return sb.ToString();
        }

        public void Print()
        {
            var receipt = BuildTextReceipt();
            _printer.PrintOrSaveReceipt(receipt);
        }
    }

    // ==========================
    // RECEIPT MODEL (Pure Data)
    // ==========================
    public class ReceiptModel
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<ReceiptItem> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal? Discount { get; set; }
        public decimal FinalTotal { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public bool IsDemo { get; set; }
    }

    public class ReceiptItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal Total => UnitPrice * Quantity;
    }
}
