using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 1120, 860);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
