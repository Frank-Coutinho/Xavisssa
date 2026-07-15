using Avalonia.Controls;

namespace Xavissa.Frontend.Helpers
{
    internal static class ResponsiveLayoutHelper
    {
        public static void UpdateWidthClasses(Control control, double width, double compactBreakpoint = 1200, double narrowBreakpoint = 860)
        {
            control.Classes.Set("compact", width <= compactBreakpoint);
            control.Classes.Set("narrow", width <= narrowBreakpoint);
        }
    }
}
