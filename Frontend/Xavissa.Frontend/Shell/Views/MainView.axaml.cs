using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.Helpers;

namespace Xavissa.Frontend.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 1080, 780);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
