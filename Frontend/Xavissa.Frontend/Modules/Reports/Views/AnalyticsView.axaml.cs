using Avalonia.Controls;
using Xavissa.Frontend.Helpers;

namespace Xavissa.Frontend.Views
{
    public partial class AnalyticsView : UserControl
    {
        public AnalyticsView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 1120, 860);
        }
    }
}
