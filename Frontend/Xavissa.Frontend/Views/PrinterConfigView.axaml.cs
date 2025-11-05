using Avalonia.Controls;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class PrinterConfigView : UserControl
    {
        public PrinterConfigView()
        {
            InitializeComponent();
            DataContext = new PrinterConfigViewModel();
        }
    }
}
