using System;
using Avalonia;
using Avalonia.Controls;
#if DEBUG
using Avalonia.Diagnostics;
#endif
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.Views;

namespace Xavissa.Frontend
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
