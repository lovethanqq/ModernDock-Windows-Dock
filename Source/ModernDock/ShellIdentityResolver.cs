using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MyCustomDock
{
    public static class ShellIdentityResolver
    {
        public const string RecycleBinIdentity = "shell:recycle-bin";
        public const string ExplorerIdentity = "shell:explorer";

        private const string RecycleBinGuid = "645FF040-5081-101B-9F08-00AA002F954E";

        public static string GetIdentity(IntPtr hwnd)
        {
            IDictionary<IntPtr, string> snapshot = GetIdentitySnapshot();
            string identity;
            return snapshot.TryGetValue(hwnd, out identity) ? identity : string.Empty;
        }

        public static IDictionary<IntPtr, string> GetIdentitySnapshot()
        {
            var identities = new Dictionary<IntPtr, string>();
            object shell = null;
            object windows = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return identities;

                shell = Activator.CreateInstance(shellType);
                windows = shellType.InvokeMember("Windows", BindingFlags.InvokeMethod, null, shell, null);
                int count = Convert.ToInt32(windows.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, windows, null));
                for (int i = 0; i < count; i++)
                {
                    object window = null;
                    object document = null;
                    object folder = null;
                    object self = null;
                    try
                    {
                        window = windows.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, windows, new object[] { i });
                        int windowHwnd = Convert.ToInt32(window.GetType().InvokeMember("HWND", BindingFlags.GetProperty, null, window, null));

                        document = window.GetType().InvokeMember("Document", BindingFlags.GetProperty, null, window, null);
                        folder = document.GetType().InvokeMember("Folder", BindingFlags.GetProperty, null, document, null);
                        self = folder.GetType().InvokeMember("Self", BindingFlags.GetProperty, null, folder, null);
                        string path = Convert.ToString(self.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, self, null));
                        if (!string.IsNullOrEmpty(path) && path.IndexOf(RecycleBinGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            identities[(IntPtr)windowHwnd] = RecycleBinIdentity;
                        }
                        else
                        {
                            identities[(IntPtr)windowHwnd] = ExplorerIdentity;
                        }
                    }
                    catch (Exception ex)
                    {
                        // A shell window can disappear while it is being inspected.
                        EntryPoint.LogException("shell.identity_snapshot_window index=" + i, ex);
                    }
                    finally
                    {
                        ReleaseCom(self);
                        ReleaseCom(folder);
                        ReleaseCom(document);
                        ReleaseCom(window);
                    }
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("shell.identity_snapshot", ex);
            }
            finally
            {
                ReleaseCom(windows);
                ReleaseCom(shell);
            }
            return identities;
        }

        private static void ReleaseCom(object value)
        {
            try
            {
                if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("shell.release_com", ex);
            }
        }
    }
}
