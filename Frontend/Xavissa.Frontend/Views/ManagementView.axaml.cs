using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class ManagementView : UserControl
    {
        public ManagementView()
        {
            InitializeComponent();
            SizeChanged += (_, e) => ResponsiveLayoutHelper.UpdateWidthClasses(this, e.NewSize.Width, 1180, 880);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private ManagementViewModel Vm =>
            DataContext as ManagementViewModel
            ?? throw new InvalidOperationException("DataContext must be ManagementViewModel.");

        private void CreateUserOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Vm.CloseCreateUserPopupCommand.Execute().Subscribe();
        }

        private void UserEditorOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Vm.CloseUserEditorCommand.Execute().Subscribe();
        }

        private void CreateStoreOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Vm.CloseCreateStorePopupCommand.Execute().Subscribe();
        }

        private void CreateProductOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Vm.CloseCreateProductPopupCommand.Execute().Subscribe();
        }

        private void EditProductOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Vm.CancelEditProductCommand.Execute().Subscribe();
        }

        private void BarcodePrintOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Vm.CloseBarcodePrintPopupCommand.Execute().Subscribe();
        }
    }
}
