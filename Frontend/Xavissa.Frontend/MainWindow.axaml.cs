using Avalonia.Controls;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();
        }
    }
}
