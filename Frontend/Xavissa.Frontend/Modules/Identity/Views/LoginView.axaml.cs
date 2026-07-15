using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class LoginView : UserControl
    {
        private TextBox? _usernameTextBox;
        private TextBox? _passwordTextBox;
        private TextBox? _visiblePasswordTextBox;
        private Button? _loginButton;

        public LoginView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 960, 720);

            this.AttachedToVisualTree += (_, _) =>
            {
                _usernameTextBox?.Focus();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _usernameTextBox = this.FindControl<TextBox>("UsernameTextBox");
            _passwordTextBox = this.FindControl<TextBox>("PasswordTextBox");
            _visiblePasswordTextBox = this.FindControl<TextBox>("VisiblePasswordTextBox");
            _loginButton = this.FindControl<Button>("LoginButton");
        }

        private void OnUsernameKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _passwordTextBox?.Focus();
                e.Handled = true;
            }
        }

        private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_loginButton?.Command != null && _loginButton.Command.CanExecute(null))
                {
                    _loginButton.Command.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_passwordTextBox != null)
                    _passwordTextBox.Text = string.Empty;

                if (_visiblePasswordTextBox != null)
                    _visiblePasswordTextBox.Text = string.Empty;
            }
        }

        private void OnTogglePasswordVisibilityClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel vm)
                return;

            if (vm.IsPasswordVisible)
                _visiblePasswordTextBox?.Focus();
            else
                _passwordTextBox?.Focus();
        }
    }
}
