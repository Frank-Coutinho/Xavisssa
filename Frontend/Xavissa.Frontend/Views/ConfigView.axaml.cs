using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.Helpers;

namespace Xavissa.Frontend.Views
{
    public partial class ConfigView : UserControl
    {
        public ConfigView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 1080, 820);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
