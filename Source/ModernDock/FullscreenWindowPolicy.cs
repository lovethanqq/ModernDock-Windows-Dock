using System;

namespace MyCustomDock
{
    // Plain data used by the product's native foreground-window adapter and
    // by the controlled fullscreen regression probe.
    public sealed class FullscreenWindowInfo
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int MonitorLeft;
        public int MonitorTop;
        public int MonitorRight;
        public int MonitorBottom;
        public int WorkLeft;
        public int WorkTop;
        public int WorkRight;
        public int WorkBottom;
        public bool IsVisible;
        public bool IsMinimized;
        public bool IsZoomed;
        public int WindowStyle;
        public string ProcessName;
        public string WindowClass;
        public bool IsDock;
    }

    public static class FullscreenWindowPolicy
    {
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int EdgeTolerance = 2;

        public static bool IsFullscreenCandidate(FullscreenWindowInfo window)
        {
            if (window == null || !window.IsVisible || window.IsMinimized || window.IsDock) return false;
            if (IsExcludedProcess(window.ProcessName) || IsExcludedClass(window.WindowClass)) return false;

            bool coversMonitor = Covers(window.Left, window.Top, window.Right, window.Bottom,
                window.MonitorLeft, window.MonitorTop, window.MonitorRight, window.MonitorBottom);
            bool coversWorkArea = Covers(window.Left, window.Top, window.Right, window.Bottom,
                window.WorkLeft, window.WorkTop, window.WorkRight, window.WorkBottom);
            if (!coversMonitor && !coversWorkArea) return false;

            // A maximized window and a normal framed window which only fills
            // the work area are not fullscreen. Borderless windows that cover
            // the monitor are the intended game/video/browser-F11 case.
            if (window.IsZoomed) return false;
            bool hasNormalFrame = (window.WindowStyle & WS_CAPTION) != 0 ||
                                  (window.WindowStyle & WS_THICKFRAME) != 0;
            if (hasNormalFrame) return false;

            return true;
        }

        private static bool Covers(int left, int top, int right, int bottom,
            int boundaryLeft, int boundaryTop, int boundaryRight, int boundaryBottom)
        {
            return right > left && bottom > top &&
                   left <= boundaryLeft + EdgeTolerance &&
                   top <= boundaryTop + EdgeTolerance &&
                   right >= boundaryRight - EdgeTolerance &&
                   bottom >= boundaryBottom - EdgeTolerance;
        }

        private static bool IsExcludedProcess(string processName)
        {
            string value = (processName ?? string.Empty).Trim();
            if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 4);
            }

            return string.Equals(value, "moderndock", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "explorer", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "shellexperiencehost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "searchhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "searchapp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "startmenuexperiencehost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "applicationframehost", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExcludedClass(string windowClass)
        {
            string value = (windowClass ?? string.Empty).Trim();
            return string.Equals(value, "Progman", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "ModernDock", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "TopLevelWindowForOverflowList", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase);
        }
    }
}
