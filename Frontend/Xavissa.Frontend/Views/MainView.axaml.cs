using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        // Example: Logout handler placeholder
        // You can implement a confirmation popup here
        private async void LogoutButton_Click(
            object? sender,
            Avalonia.Interactivity.RoutedEventArgs e
        )
        {
            if (VisualRoot is Window owner && DataContext is MainViewModel vm)
            {
                // Create the message box
                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                    "Confirm Logout",
                    "Are you sure you want to logout?",
                    ButtonEnum.YesNo,
                    Icon.Question
                );

                // Show it asynchronously
                var result = await messageBox.ShowAsync();

                // Only logout if user clicked Yes
                if (result == ButtonResult.Yes)
                {
                    vm.LogoutCommand.Execute().Subscribe();
                }
            }
        }
    }
}
