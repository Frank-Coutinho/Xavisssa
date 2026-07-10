using System.Text.Json.Serialization;

public class PrinterConfig
{
    public string PrinterName { get; set; } = "";
    public string LabelPrinterName { get; set; } = "";
    public int PaperWidth { get; set; } = 280;
    public double LabelWidthMm { get; set; } = 40;
    public double LabelHeightMm { get; set; } = 30;
    public double LabelGapMm { get; set; } = 2;
    public double LabelHorizontalPaddingMm { get; set; } = 2;
    public double LabelVerticalPaddingMm { get; set; } = 1.5;
    public double LabelBarcodeWidthMm { get; set; } = 34;
    public double LabelBarcodeHeightMm { get; set; } = 11;
    public int LabelFontSize { get; set; } = 8;
    public string HeaderText { get; set; } = "RECIBO";
    public string FooterText { get; set; } = "Obrigado pela sua compra!";
    public bool ShowDateTime { get; set; } = true;
    public string HeaderImagePath { get; set; } = "pack://application:,,,/Resources/logo.png";
    public bool UseHeaderImage { get; set; } = true;
    public bool PrintImageOnReceipt { get; set; } = true;
    public string ImagePosition { get; set; } = "Centro";
}
