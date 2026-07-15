using System;
using System.Text;
using Avalonia.Input;

namespace Xavissa.Frontend.Services
{
    public class BarcodeScannerInputService : IBarcodeScannerInputService
    {
        private const int MaxInterKeyDelayMs = 45;
        private const int MinBarcodeLength = 6;
        private const int MaxBarcodeLength = 32;

        private readonly StringBuilder _buffer = new();
        private DateTimeOffset? _lastInputAt;

        public void Reset()
        {
            _buffer.Clear();
            _lastInputAt = null;
        }

        public void NotifyManualFocus()
        {
            Reset();
        }

        public bool ProcessTextInput(TextInputEventArgs e, DateTimeOffset timestamp)
        {
            var text = e.Text;
            if (string.IsNullOrEmpty(text) || text.Length != 1)
            {
                Reset();
                return false;
            }

            var character = text[0];
            if (!IsValidBarcodeCharacter(character))
            {
                Reset();
                return false;
            }

            if (_lastInputAt.HasValue && (timestamp - _lastInputAt.Value).TotalMilliseconds > MaxInterKeyDelayMs)
                Reset();

            if (_buffer.Length >= MaxBarcodeLength)
            {
                Reset();
                return false;
            }

            _buffer.Append(character);
            _lastInputAt = timestamp;
            return true;
        }

        public bool TryCompleteScan(KeyEventArgs e, DateTimeOffset timestamp, out string barcode)
        {
            barcode = string.Empty;

            if (HasModifierKeys(e.KeyModifiers))
            {
                Reset();
                return false;
            }

            if (!IsSubmitKey(e.Key))
                return false;

            if (!_lastInputAt.HasValue || (timestamp - _lastInputAt.Value).TotalMilliseconds > MaxInterKeyDelayMs)
            {
                Reset();
                return false;
            }

            var candidate = _buffer.ToString();
            Reset();

            if (!IsPlausibleBarcode(candidate))
                return false;

            barcode = candidate;
            return true;
        }

        private static bool IsSubmitKey(Key key) =>
            key is Key.Enter or Key.Return;

        private static bool HasModifierKeys(KeyModifiers modifiers) =>
            (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != KeyModifiers.None;

        private static bool IsPlausibleBarcode(string candidate)
        {
            if (candidate.Length < MinBarcodeLength || candidate.Length > MaxBarcodeLength)
                return false;

            foreach (var character in candidate)
            {
                if (!IsValidBarcodeCharacter(character))
                    return false;
            }

            return true;
        }

        private static bool IsValidBarcodeCharacter(char character) =>
            char.IsLetterOrDigit(character) || character is '-' or '.' or '_' or '/';
    }
}
