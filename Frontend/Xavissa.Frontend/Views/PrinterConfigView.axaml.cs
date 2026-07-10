using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class PrinterConfigView : UserControl
    {
        // Constructor that receives dependencies from DI
        public PrinterConfigView(IPrinterService printer, INotificationService notify, ILocalizationService localization)
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 760, 560);
            DataContext = new PrinterConfigViewModel(printer, notify, localization);
        }

        // Only used by design mode
        public PrinterConfigView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 760, 560);

            if (Design.IsDesignMode)
            {
                // Safe dummy objects for designer
                DataContext = new PrinterConfigViewModel(
                    new PrinterService(),
                    new NotificationService(),
                    new LocalizationService()
                );
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
