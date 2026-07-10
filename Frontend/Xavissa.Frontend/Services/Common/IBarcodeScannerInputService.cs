using System;
using Avalonia.Input;

namespace Xavissa.Frontend.Services
{
    public interface IBarcodeScannerInputService
    {
        void Reset();
        void NotifyManualFocus();
        bool ProcessTextInput(TextInputEventArgs e, DateTimeOffset timestamp);
        bool TryCompleteScan(KeyEventArgs e, DateTimeOffset timestamp, out string barcode);
    }
}
