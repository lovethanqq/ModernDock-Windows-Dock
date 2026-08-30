using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace MyCustomDock
{
    // Parses only user-dropped application files. Persistence and insertion
    // remain in DockWindow so the existing seven-column config transaction is
    // used for every successful drop.
    public static class DockDropService
    {
        public static bool IsSupportedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string extension = Path.GetExtension(path.Trim().Trim('"'));
            return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryCreateDockItem(string droppedPath, out DockItem item, out string error)
        {
            item = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(droppedPath))
            {
                error = "Dropped path is empty.";
                return false;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(droppedPath.Trim().Trim('"'));
            }
            catch (Exception ex)
            {
                error = "Dropped path is invalid: " + ex.Message;
                return false;
            }

            if (!File.Exists(fullPath))
            {
                error = "Dropped file does not exist.";
                return false;
            }

            string extension = Path.GetExtension(fullPath);
            if (!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only .exe and .lnk files can be pinned.";
                return false;
            }

            string targetPath = fullPath;
            string arguments = string.Empty;
            string shortcutIconPath = string.Empty;
            if (string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadShortcut(fullPath, out targetPath, out arguments, out shortcutIconPath, out error)) return false;
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                error = "Shortcut target is empty.";
                return false;
            }

            try
            {
                targetPath = Path.GetFullPath(targetPath.Trim().Trim('"'));
            }
            catch (Exception ex)
            {
                error = "Shortcut target is invalid: " + ex.Message;
                return false;
            }

            if (!File.Exists(targetPath))
            {
                error = "Shortcut target does not exist.";
                return false;
            }

            string title = GetDisplayName(targetPath);
            string directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            string processName = Path.GetFileNameWithoutExtension(targetPath) ?? string.Empty;
            ImageSource icon = LoadIcon(shortcutIconPath, targetPath);
            if (icon == null)
            {
                error = "No readable icon was found for the dropped application.";
                return false;
            }

            item = new DockItem
            {
                Title = title,
                TargetPath = targetPath,
                Arguments = arguments ?? string.Empty,
                PathMatch = directory,
                ProcessNameMatch = processName,
                WindowClassMatch = string.Empty,
                IconSource = icon,
                IconFile = string.Empty,
                ShortcutSource = string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase) ? fullPath : string.Empty,
                AutoDerivedPathMatch = true,
                AutoDerivedProcessNameMatch = true,
                IsFixed = true
            };
            return true;
        }

        private static ImageSource LoadIcon(string shortcutIconPath, string targetPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(shortcutIconPath) && File.Exists(shortcutIconPath))
                {
                    string extension = Path.GetExtension(shortcutIconPath);
                    ImageSource shortcutIcon = string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase)
                        ? IconService.LoadImage(shortcutIconPath)
                        : IconService.ExtractBest(shortcutIconPath);
                    if (shortcutIcon != null) return shortcutIcon;
                }

                return IconService.ExtractBest(targetPath);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("drop.icon target=" + targetPath, ex);
                return null;
            }
        }

        private static string GetDisplayName(string targetPath)
        {
            try
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(targetPath);
                if (!string.IsNullOrWhiteSpace(version.ProductName)) return version.ProductName.Trim();
                if (!string.IsNullOrWhiteSpace(version.FileDescription)) return version.FileDescription.Trim();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("drop.file_version target=" + targetPath, ex);
            }

            string name = Path.GetFileNameWithoutExtension(targetPath);
            return string.IsNullOrWhiteSpace(name) ? "Dropped Application" : name;
        }

        public static bool TryReadShortcut(string shortcutPath, out string targetPath,
            out string arguments, out string iconPath, out string error)
        {
            targetPath = string.Empty;
            arguments = string.Empty;
            iconPath = string.Empty;
            error = string.Empty;
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    error = "Windows shortcut support is unavailable.";
                    return false;
                }

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                targetPath = Convert.ToString(shortcutType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                arguments = Convert.ToString(shortcutType.InvokeMember("Arguments", BindingFlags.GetProperty, null, shortcut, null));
                string iconLocation = Convert.ToString(shortcutType.InvokeMember("IconLocation", BindingFlags.GetProperty, null, shortcut, null));
                iconPath = ExtractIconPath(iconLocation);
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    error = "Shortcut target is empty.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Shortcut could not be read: " + ex.Message;
                EntryPoint.LogException("drop.shortcut path=" + shortcutPath, ex);
                return false;
            }
            finally
            {
                ReleaseCom(shortcut);
                ReleaseCom(shell);
            }
        }

        private static string ExtractIconPath(string iconLocation)
        {
            if (string.IsNullOrWhiteSpace(iconLocation)) return string.Empty;
            string value = iconLocation.Trim().Trim('"');
            int comma = value.LastIndexOf(',');
            if (comma > 0) value = value.Substring(0, comma).Trim().Trim('"');
            return value;
        }

        private static void ReleaseCom(object value)
        {
            try
            {
                if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("drop.release_com", ex);
            }
        }
    }
}
