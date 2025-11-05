using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
