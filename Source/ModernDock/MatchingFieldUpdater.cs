using System;
using System.IO;

namespace MyCustomDock
{
    // Keeps automatically derived matching rules in sync with an edited target
    // while leaving explicit user rules untouched.
    public static class MatchingFieldUpdater
    {
        public static bool Update(DockItem item, string originalTargetPath)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.TargetPath)) return false;

            string oldTarget = NormalizeTarget(originalTargetPath);
            string newTarget = NormalizeTarget(item.TargetPath);
            if (string.Equals(oldTarget, newTarget, StringComparison.OrdinalIgnoreCase)) return false;
            if (ApplicationIdentityResolver.IsGenericLauncherOrHostPath(newTarget)) return false;
            if (!ApplicationIdentityResolver.IsFullyQualifiedPath(newTarget)) return false;

            string newDirectory;
            string newProcessName;
            try
            {
                newDirectory = Path.GetDirectoryName(newTarget) ?? string.Empty;
                newProcessName = Path.GetFileNameWithoutExtension(newTarget) ?? string.Empty;
            }
            catch
            {
                return false;
            }

            bool changed = false;
            if (string.IsNullOrEmpty(item.PathMatch) || IsDerivedPathMatch(item.PathMatch, oldTarget))
            {
                if (!string.Equals(item.PathMatch ?? string.Empty, newDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    item.PathMatch = newDirectory;
                    changed = true;
                }
            }

            if (string.IsNullOrEmpty(item.ProcessNameMatch) || IsDerivedProcessName(item.ProcessNameMatch, oldTarget))
            {
                if (!string.Equals(item.ProcessNameMatch ?? string.Empty, newProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    item.ProcessNameMatch = newProcessName;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsDerivedPathMatch(string configuredPath, string originalTarget)
        {
            if (string.IsNullOrWhiteSpace(originalTarget) ||
                !ApplicationIdentityResolver.IsFullyQualifiedPath(originalTarget)) return false;

            string oldDirectory;
            try
            {
                oldDirectory = Path.GetDirectoryName(originalTarget) ?? string.Empty;
            }
            catch
            {
                return false;
            }

            string configured = NormalizeFragment(configuredPath);
            string old = NormalizeTarget(oldDirectory);
            if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(old)) return false;
            if (string.Equals(configured, old, StringComparison.OrdinalIgnoreCase)) return true;

            // A generated rule in older configs may have been stored as a
            // directory fragment. Require at least one separator and a complete
            // directory suffix so a human rule such as "foo" is not rewritten.
            if (configured.IndexOf('\\') < 0) return false;
            return old.EndsWith("\\" + configured.Trim('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDerivedProcessName(string configuredProcessName, string originalTarget)
        {
            if (string.IsNullOrWhiteSpace(originalTarget) ||
                !ApplicationIdentityResolver.IsFullyQualifiedPath(originalTarget)) return false;

            string oldProcessName;
            try
            {
                oldProcessName = Path.GetFileNameWithoutExtension(originalTarget) ?? string.Empty;
            }
            catch
            {
                return false;
            }

            return !string.IsNullOrEmpty(oldProcessName) &&
                   string.Equals(configuredProcessName.Trim(), oldProcessName, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTarget(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return ApplicationIdentityResolver.NormalizeExecutablePath(path);
        }

        private static string NormalizeFragment(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string value = path.Trim().Trim('"').Replace('/', '\\');
            if (ApplicationIdentityResolver.IsFullyQualifiedPath(value)) return NormalizeTarget(value);
            return value.Trim('\\').ToLowerInvariant();
        }
    }
}
