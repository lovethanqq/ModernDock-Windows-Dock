namespace MyCustomDock
{
    internal static class NativeConstants
    {
        // SetWindowPos flags. SWP_NOACTIVATE is a SetWindowPos flag, not an
        // extended window style.
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const uint DockTopmostNoActivateFlags =
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE;

        // Extended window style. This is intentionally separate from the
        // SetWindowPos flag above.
        internal const int WS_EX_NOACTIVATE = unchecked((int)0x08000000);
    }
}
