using System;
using System.Collections.Generic;
using System.IO;

namespace MyCustomDock
{
    public static class ApplicationIdentityResolver
    {
        public static string NormalizeExecutablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string value = path.Trim().Trim('"').Replace('/', '\\');
            if (!IsFullyQualifiedPath(value))
            {
                return value.TrimEnd('\\').ToLowerInvariant();
            }

            try
            {
                value = Path.GetFullPath(value);
            }
            catch
            {
                // Keep the normalized separators when a process path is transient
                // or no longer resolves to a valid local path.
            }

            return value.TrimEnd('\\').ToLowerInvariant();
        }

        public static bool PathContainsDirectory(string fullPath, string pathMatch)
        {
            string normalizedPath = NormalizeExecutablePath(fullPath);
            string normalizedMatch = NormalizePathFragment(pathMatch);
            if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(normalizedMatch)) return false;

            if (IsFullyQualifiedPath(normalizedMatch))
            {
                return string.Equals(normalizedPath, normalizedMatch, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.StartsWith(normalizedMatch + "\\", StringComparison.OrdinalIgnoreCase);
            }

            string wrappedPath = "\\" + normalizedPath.Trim('\\') + "\\";
            string wrappedMatch = "\\" + normalizedMatch.Trim('\\') + "\\";
            return wrappedPath.IndexOf(wrappedMatch, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool PathContainsToken(string fullPath, string token)
        {
            string normalizedPath = NormalizeExecutablePath(fullPath);
            string normalizedToken = (token ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(normalizedToken)) return false;

            int offset = 0;
            while (offset < normalizedPath.Length)
            {
                int found = normalizedPath.IndexOf(normalizedToken, offset, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return false;

                bool leftBoundary = found == 0 || !IsPathTokenCharacter(normalizedPath[found - 1]);
                int end = found + normalizedToken.Length;
                bool rightBoundary = end >= normalizedPath.Length || !IsPathTokenCharacter(normalizedPath[end]);
                if (leftBoundary && rightBoundary) return true;
                offset = found + 1;
            }

            return false;
        }

        public static string GetFixedIdentityKey(DockItem item)
        {
            if (item == null) return string.Empty;

            string arguments = item.Arguments ?? string.Empty;
            if (arguments.IndexOf("shell:RecycleBinFolder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "shell:recyclebin";
            }

            if (string.Equals(item.Title, "开始菜单", StringComparison.OrdinalIgnoreCase))
            {
                return "shell:start-menu";
            }

            string targetPath = NormalizeExecutablePath(item.TargetPath);
            if (!string.IsNullOrEmpty(targetPath) && Path.IsPathRooted(targetPath) && !IsGenericLauncherOrHostPath(targetPath))
            {
                return "target:" + targetPath;
            }

            string pathMatch = NormalizePathFragment(item.PathMatch);
            if (!string.IsNullOrEmpty(pathMatch))
            {
                return "pathmatch:" + pathMatch;
            }

            string processMatch = (item.ProcessNameMatch ?? string.Empty).Trim().ToLowerInvariant();
            string classMatch = (item.WindowClassMatch ?? string.Empty).Trim().ToLowerInvariant();
            return "fallback:" + (item.Title ?? string.Empty).Trim().ToLowerInvariant() + "|" + processMatch + "|" + classMatch;
        }

        public static bool IsGenericLauncherOrHostPath(string path)
        {
            string normalized = NormalizeExecutablePath(path);
            if (string.IsNullOrEmpty(normalized)) return false;

            string fileName;
            try
            {
                fileName = Path.GetFileName(normalized);
            }
            catch
            {
                fileName = normalized;
            }

            return string.Equals(fileName, "cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "applicationframehost.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "windowsterminal.exe", StringComparison.OrdinalIgnoreCase);
        }

        public static ApplicationIdentity Resolve(WindowSnapshot window)
        {
            if (window == null)
            {
                return new ApplicationIdentity("unknown", string.Empty, string.Empty, string.Empty);
            }

            string normalizedPath = NormalizeExecutablePath(window.ProcessPath);
            string family = GetLimitedProcessFamily(window.ProcessPath, window.ProcessName);
            if (!string.IsNullOrEmpty(family))
            {
                return new ApplicationIdentity(
                    "family:" + family,
                    normalizedPath,
                    family,
                    GetDisplayName(window));
            }

            if (!string.IsNullOrEmpty(normalizedPath))
            {
                return new ApplicationIdentity(
                    "path:" + normalizedPath,
                    normalizedPath,
                    GetLimitedProcessFamily(normalizedPath),
                    GetDisplayName(window));
            }

            string processName = (window.ProcessName ?? string.Empty).Trim().ToLowerInvariant();
            string windowClass = (window.WindowClass ?? string.Empty).Trim().ToLowerInvariant();
            return new ApplicationIdentity(
                "fallback:" + processName + "|" + windowClass,
                string.Empty,
                string.Empty,
                GetDisplayName(window));
        }

        public static string GetDynamicIdentityKey(WindowSnapshot window)
        {
            return Resolve(window).Key;
        }

        public static bool IsFullyQualifiedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string value = path.Trim().Trim('"').Replace('/', '\\');
            return (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '\\') ||
                   value.StartsWith("\\\\", StringComparison.Ordinal);
        }

        public static string GetProcessFamilyKey(WindowSnapshot window)
        {
            if (window == null) return string.Empty;
            return GetProcessFamilyKey(window.ProcessPath, window.ProcessName);
        }

        public static string GetProcessFamilyKey(string processPath, string processName)
        {
            return GetLimitedProcessFamily(processPath, processName);
        }

        public static IList<ApplicationGroup> GroupByIdentity(IList<WindowSnapshot> windows)
        {
            var groups = new List<ApplicationGroup>();
            var byKey = new Dictionary<string, ApplicationGroup>(StringComparer.OrdinalIgnoreCase);
            if (windows == null) return groups;

            foreach (var window in windows)
            {
                if (window == null) continue;

                ApplicationIdentity identity = Resolve(window);
                ApplicationGroup group;
                if (!byKey.TryGetValue(identity.Key, out group))
                {
                    group = new ApplicationGroup(identity);
                    byKey.Add(identity.Key, group);
                    groups.Add(group);
                }
                group.Add(window);
            }

            return groups;
        }

        private static string NormalizePathFragment(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string value = path.Trim().Trim('"').Replace('/', '\\');
            if (IsFullyQualifiedPath(value)) return NormalizeExecutablePath(value);
            return value.Trim('\\').ToLowerInvariant();
        }

        private static bool IsPathTokenCharacter(char value)
        {
            // Package identities commonly continue with '_' or '-' after the
            // configured token (for example OpenAI.Codex_...); only letters and
            // digits make a token boundary unsafe here.
            return char.IsLetterOrDigit(value);
        }

        private static string GetLimitedProcessFamily(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath)) return string.Empty;

            if (PathContainsDirectory(normalizedPath, @"\\Steam\\") ||
                normalizedPath.IndexOf("\\steamwebhelper.exe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "steam";
            }
            if (PathContainsDirectory(normalizedPath, @"\\Tencent\\Weixin\\")) return "weixin";
            if (PathContainsDirectory(normalizedPath, @"\\@opencode-aidesktop\\")) return "opencode";
            if (PathContainsDirectory(normalizedPath, @"\\JianyingPro\\")) return "jianying";
            return string.Empty;
        }

        private static string GetLimitedProcessFamily(string processPath, string processName)
        {
            string normalizedPath = NormalizeExecutablePath(processPath);
            string family = GetLimitedProcessFamily(normalizedPath);
            if (!string.IsNullOrEmpty(family)) return family;

            string name = (processName ?? string.Empty).Trim().ToLowerInvariant();
            if (name == "steam" || name == "steamwebhelper") return "steam";
            if (name == "weixin" || name == "wechat") return "weixin";
            return string.Empty;
        }

        private static string GetDisplayName(WindowSnapshot window)
        {
            if (!string.IsNullOrEmpty(window.WindowTitle)) return window.WindowTitle;
            if (!string.IsNullOrEmpty(window.ProcessName)) return window.ProcessName;
            return window.ProcessPath ?? string.Empty;
        }
    }
}
