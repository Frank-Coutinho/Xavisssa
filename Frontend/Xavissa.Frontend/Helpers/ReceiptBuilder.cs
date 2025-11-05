using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Helpers
{
    public class ReceiptBuilder
    {
        private readonly HomeViewModel _sale;
        private readonly PrinterService _printer;

        public ReceiptBuilder(HomeViewModel sale, PrinterService printer)
        {
            _sale = sale ?? throw new ArgumentNullException(nameof(sale));
            _printer = printer ?? throw new ArgumentNullException(nameof(printer));
        }

        /// <summary>
        /// Build the receipt as raw text (for cross-platform printing)
        /// </summary>
        public string BuildTextReceipt()
        {
            var sb = new StringBuilder();

            // HEADER IMAGE (optional)
            if (
                _printer.UseHeaderImage
                && _printer.PrintImageOnReceipt
                && !string.IsNullOrWhiteSpace(_printer.HeaderImagePath)
            )
            {
                sb.AppendLine($"[IMAGE: {_printer.HeaderImagePath} - {_printer.ImagePosition}]");
                sb.AppendLine();
            }

            // HEADER TEXT
            if (!string.IsNullOrWhiteSpace(_printer.HeaderText))
            {
                sb.AppendLine(_printer.HeaderText);
                sb.AppendLine(new string('-', _printer.PaperWidth / 8));
            }

            // DATE/TIME
            if (_printer.ShowDateTime)
            {
                sb.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm}");
                sb.AppendLine();
            }

            // ITEMS
            sb.AppendLine("Item                Qty   Total");
            sb.AppendLine(new string('-', _printer.PaperWidth / 8));
            foreach (var item in _sale.CartItems)
            {
                string name =
                    item.Product.Name.Length > 16
                        ? item.Product.Name[..16]
                        : item.Product.Name.PadRight(16);
                string qty = item.Quantity.ToString().PadLeft(3);
                string total = item.Total.ToString("0.00").PadLeft(7);
                sb.AppendLine($"{name} {qty} {total}");
            }

            sb.AppendLine(new string('-', _printer.PaperWidth / 8));

            // TOTALS
            sb.AppendLine($"Subtotal: {_sale.Subtotal:0.00}");
            if (_sale.Discount > 0)
                sb.AppendLine($"Discount: {_sale.Discount:0.00}");
            sb.AppendLine($"Total: {_sale.FinalTotal:0.00}");
            sb.AppendLine($"Paid: {_sale.AmountPaid:0.00}");
            sb.AppendLine($"Change: {_sale.ChangeAmount:0.00}");
            sb.AppendLine($"Payment: {_sale.SelectedPaymentMethod}");

            // FOOTER
            sb.AppendLine(new string('-', _printer.PaperWidth / 8));
            if (!string.IsNullOrWhiteSpace(_printer.FooterText))
                sb.AppendLine(_printer.FooterText);

            return sb.ToString();
        }

        /// <summary>
        /// Send the receipt to the printer
        /// </summary>
        public void PrintReceipt()
        {
            var receipt = BuildTextReceipt();
            _printer.PrintOrSaveReceipt(receipt);
        }
    }
}
