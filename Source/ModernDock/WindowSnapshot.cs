using System;

namespace MyCustomDock
{
    // UI-independent description of one currently visible top-level window.
    // A window handle describes an instance; it is not the Dock application identity.
    public sealed class WindowSnapshot
    {
        public IntPtr Handle { get; set; }
        public string ProcessPath { get; set; }
        public string ProcessName { get; set; }
        public string WindowClass { get; set; }
        public string WindowTitle { get; set; }
        public uint Pid { get; set; }
        public bool IsMinimized { get; set; }
        public string ShellIdentity { get; set; }
    }
}
