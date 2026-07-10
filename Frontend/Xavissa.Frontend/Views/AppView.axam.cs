using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Xavissa.Frontend.Views
{
    public partial class AppView : UserControl
    {
        public AppView()
        {
            InitializeComponent();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
        }
    }
}
