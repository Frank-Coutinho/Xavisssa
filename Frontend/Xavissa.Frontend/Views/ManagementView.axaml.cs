using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Xavissa.Frontend.Views
{
    public partial class ManagementView : UserControl
    {
        public ManagementView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
