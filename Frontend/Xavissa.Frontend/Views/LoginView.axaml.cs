using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            // InitializeViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
