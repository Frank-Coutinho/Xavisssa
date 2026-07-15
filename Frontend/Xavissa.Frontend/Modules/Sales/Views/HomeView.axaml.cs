using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class HomeView : ReactiveUserControl<HomeViewModel>
    {
        private TopLevel? _topLevel;

        public HomeView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 1180, 900);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            AttachScannerHandlers();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DetachScannerHandlers();
            base.OnDetachedFromVisualTree(e);
        }

        private void AttachScannerHandlers()
        {
            if (_topLevel != null)
                return;

            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel == null)
                return;

            _topLevel.AddHandler(InputElement.TextInputEvent, HandleGlobalTextInput, RoutingStrategies.Tunnel, true);
            _topLevel.AddHandler(InputElement.KeyDownEvent, HandleGlobalKeyDown, RoutingStrategies.Tunnel, true);
        }

        private void DetachScannerHandlers()
        {
            if (_topLevel == null)
                return;

            _topLevel.RemoveHandler(InputElement.TextInputEvent, HandleGlobalTextInput);
            _topLevel.RemoveHandler(InputElement.KeyDownEvent, HandleGlobalKeyDown);
            ViewModel?.ResetScannerInput();
            _topLevel = null;
        }

        private async void HandleGlobalKeyDown(object? sender, KeyEventArgs e)
        {
            if (ViewModel == null || IsManualTextEntryFocused())
            {
                ViewModel?.NotifyManualTextEntryFocused();
                return;
            }

            if (await ViewModel.TryProcessScannerTerminatorAsync(e, DateTimeOffset.UtcNow))
                e.Handled = true;
        }

        private void HandleGlobalTextInput(object? sender, TextInputEventArgs e)
        {
            if (ViewModel == null || IsManualTextEntryFocused())
            {
                ViewModel?.NotifyManualTextEntryFocused();
                return;
            }

            ViewModel.ProcessScannerTextInput(e, DateTimeOffset.UtcNow);
        }

        private bool IsManualTextEntryFocused()
        {
            var focusedElement = _topLevel?.FocusManager?.GetFocusedElement();
            return focusedElement is TextBox or MaskedTextBox or NumericUpDown;
        }
    }
}
