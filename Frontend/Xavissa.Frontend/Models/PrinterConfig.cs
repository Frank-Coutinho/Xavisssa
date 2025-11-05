using System.Text.Json.Serialization;

public class PrinterConfig
{
    public string PrinterName { get; set; } = "";
    public int PaperWidth { get; set; } = 280;
    public string HeaderText { get; set; } = "RECIBO";
    public string FooterText { get; set; } = "Obrigado pela sua compra!";
    public bool ShowDateTime { get; set; } = true;
    public string HeaderImagePath { get; set; } = "pack://application:,,,/Resources/logo.png";
    public bool UseHeaderImage { get; set; } = true;
    public bool PrintImageOnReceipt { get; set; } = true;
    public string ImagePosition { get; set; } = "Centro";
}
