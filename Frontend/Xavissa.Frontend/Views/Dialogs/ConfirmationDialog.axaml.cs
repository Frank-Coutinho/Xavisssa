using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Xavissa.Frontend.Views.Dialogs
{
    public partial class ConfirmationDialog : Window
    {
        private TextBlock? _titleText;
        private TextBlock? _messageText;
        private Button? _confirmButton;

        public ConfirmationDialog()
        {
            InitializeComponent();
            _titleText = this.FindControl<TextBlock>("TitleText");
            _messageText = this.FindControl<TextBlock>("MessageText");
            _confirmButton = this.FindControl<Button>("ConfirmButton");
            KeyDown += OnKeyDown;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void Configure(string title, string message, string confirmText, bool isDestructive)
        {
            Title = title;

            if (_titleText == null || _messageText == null || _confirmButton == null)
                throw new InvalidOperationException("Confirmation dialog controls could not be loaded.");

            _titleText.Text = title;
            _messageText.Text = message;
            _confirmButton.Content = confirmText;
            _confirmButton.Classes.Clear();
            _confirmButton.Classes.Add(isDestructive ? "danger" : "primary");
        }

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        private void ConfirmButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close(false);
        }
    }
}
