using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace MyCustomDock
{
    public enum LaunchResolutionStatus
    {
        NotFound,
        Found,
        Ambiguous
    }

    public sealed class LaunchResolutionResult
    {
        public LaunchResolutionResult(LaunchResolutionStatus status, string path, IList<string> candidates, string reason)
        {
            Status = status;
            ResolvedTargetPath = path ?? string.Empty;
            Candidates = candidates ?? new List<string>();
            Reason = reason ?? string.Empty;
        }

        public LaunchResolutionStatus Status { get; private set; }
        public string ResolvedTargetPath { get; private set; }
        public IList<string> Candidates { get; private set; }
        public string Reason { get; private set; }
        public bool IsFound { get { return Status == LaunchResolutionStatus.Found; } }
    }

    // Bounded, user-initiated repair for a stale fixed-item target. This class
    // never scans a drive and never runs in the 250ms refresh path.
    public static class LaunchTargetResolver
    {
        private const string AppPathsSubKey = @"Software\Microsoft\Windows\CurrentVersion\App Paths\";

        public static LaunchResolutionResult Resolve(DockItem item, IList<WindowSnapshot> windows)
        {
            if (item == null) return NotFound("invalid-item");
            string oldTarget = Normalize(item.TargetPath);
            if (string.IsNullOrEmpty(oldTarget)) return NotFound("missing-target");
            if (IsShellActivation(item)) return NotFound("shell-activation-is-not-a-file-repair");

            LaunchResolutionResult result = ResolveFromRunningWindows(item, oldTarget, windows);
            if (result.Status != LaunchResolutionStatus.NotFound) return result;

            result = ResolveFromShortcut(item, oldTarget);
            if (result.Status != LaunchResolutionStatus.NotFound) return result;

            result = ResolveFromVersionedDirectories(item, oldTarget);
            if (result.Status != LaunchResolutionStatus.NotFound) return result;

            result = ResolveFromPathMatch(item, oldTarget);
            if (result.Status != LaunchResolutionStatus.NotFound) return result;

            result = ResolveFromProcessName(item, oldTarget);
            if (result.Status != LaunchResolutionStatus.NotFound) return result;

            result = ResolveFromAppPaths(item, oldTarget);
            if (result.Status != LaunchResolutionStatus.NotFound) return result;

            return NotFound("no-bounded-candidate");
        }

        private static LaunchResolutionResult ResolveFromRunningWindows(DockItem item, string oldTarget, IList<WindowSnapshot> windows)
        {
            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (windows == null) return NotFound("no-window-snapshot");

            foreach (WindowSnapshot window in windows)
            {
                if (window == null || string.IsNullOrWhiteSpace(window.ProcessPath)) continue;
                string processPath = Normalize(window.ProcessPath);
                if (string.IsNullOrEmpty(processPath) || string.Equals(processPath, oldTarget, StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(processPath) || !LooksLikeItemProcess(item, window, processPath)) continue;
                AddCandidate(candidates, processPath);
            }
            return FromCandidates(candidates, "running-process-path");
        }

        private static LaunchResolutionResult ResolveFromShortcut(DockItem item, string oldTarget)
        {
            if (string.IsNullOrWhiteSpace(item.ShortcutSource) || !File.Exists(item.ShortcutSource))
            {
                return NotFound("shortcut-source-unavailable");
            }

            string target;
            string arguments;
            string iconPath;
            string error;
            if (!DockDropService.TryReadShortcut(item.ShortcutSource, out target, out arguments, out iconPath, out error))
            {
                return NotFound("shortcut-reparse-failed");
            }

            string normalized = Normalize(target);
            if (string.IsNullOrEmpty(normalized) || string.Equals(normalized, oldTarget, StringComparison.OrdinalIgnoreCase) || !File.Exists(normalized))
            {
                return NotFound("shortcut-target-unavailable");
            }

            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddCandidate(candidates, normalized);
            return FromCandidates(candidates, "shortcut-source");
        }

        private static LaunchResolutionResult ResolveFromVersionedDirectories(DockItem item, string oldTarget)
        {
            string oldDirectory = SafeDirectoryName(oldTarget);
            string fileName = SafeFileName(oldTarget);
            if (string.IsNullOrEmpty(oldDirectory) || string.IsNullOrEmpty(fileName)) return NotFound("target-directory-unavailable");

            var roots = GetNearbyRoots(oldDirectory, fileName);
            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                ScanImmediateDirectoryForFile(root, fileName, oldTarget, candidates);
                string[] childDirectories = GetDirectories(root);
                foreach (string child in childDirectories)
                {
                    if (!LooksVersionedDirectory(child) && !IsNamedLikeExecutableRoot(root, fileName)) continue;
                    ScanImmediateDirectoryForFile(child, fileName, oldTarget, candidates);
                }
            }
            return FromCandidates(candidates, "nearby-versioned-directory");
        }

        private static LaunchResolutionResult ResolveFromPathMatch(DockItem item, string oldTarget)
        {
            string pathMatch = (item.PathMatch ?? string.Empty).Trim().Trim('"');
            if (!ApplicationIdentityResolver.IsFullyQualifiedPath(pathMatch)) return NotFound("path-match-not-rooted");

            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string fileName = SafeFileName(oldTarget);
            if (!string.IsNullOrEmpty(fileName))
            {
                ScanImmediateDirectoryForFile(pathMatch, fileName, oldTarget, candidates);
                foreach (string child in GetDirectories(pathMatch))
                {
                    ScanImmediateDirectoryForFile(child, fileName, oldTarget, candidates);
                }
            }
            return FromCandidates(candidates, "path-match-directory");
        }

        private static LaunchResolutionResult ResolveFromProcessName(DockItem item, string oldTarget)
        {
            string fileName = SafeFileName(oldTarget);
            if (string.IsNullOrEmpty(fileName)) return NotFound("process-name-unavailable");

            string oldDirectory = SafeDirectoryName(oldTarget);
            var roots = GetNearbyRoots(oldDirectory, fileName);
            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                ScanImmediateDirectoryForFile(root, fileName, oldTarget, candidates);
                foreach (string child in GetDirectories(root))
                {
                    if (LooksVersionedDirectory(child)) ScanImmediateDirectoryForFile(child, fileName, oldTarget, candidates);
                }
            }
            return FromCandidates(candidates, "bounded-process-name");
        }

        private static LaunchResolutionResult ResolveFromAppPaths(DockItem item, string oldTarget)
        {
            string fileName = SafeFileName(oldTarget);
            if (string.IsNullOrEmpty(fileName)) return NotFound("app-path-name-unavailable");

            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ReadAppPath(Registry.CurrentUser, fileName, oldTarget, candidates);
            ReadAppPath(Registry.LocalMachine, fileName, oldTarget, candidates);
            return FromCandidates(candidates, "app-paths-registry");
        }

        private static bool LooksLikeItemProcess(DockItem item, WindowSnapshot window, string processPath)
        {
            string processName = (window.ProcessName ?? string.Empty).Trim();
            string configuredNames = item.ProcessNameMatch ?? string.Empty;
            foreach (string name in configuredNames.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(name) && string.Equals(name.Trim(), processName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            if (!string.IsNullOrWhiteSpace(item.PathMatch) && ApplicationIdentityResolver.PathContainsDirectory(processPath, item.PathMatch)) return true;

            string itemFamily = ApplicationIdentityResolver.GetProcessFamilyKey(item.TargetPath, processName);
            string windowFamily = ApplicationIdentityResolver.GetProcessFamilyKey(processPath, processName);
            if (!string.IsNullOrEmpty(itemFamily) && string.Equals(itemFamily, windowFamily, StringComparison.OrdinalIgnoreCase)) return true;

            string targetName = SafeFileName(item.TargetPath);
            return !string.IsNullOrEmpty(targetName) && string.Equals(targetName, SafeFileName(processPath), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsShellActivation(DockItem item)
        {
            string target = (item.TargetPath ?? string.Empty).Trim().Trim('"');
            return string.Equals(target, "explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(target, "applicationframehost.exe", StringComparison.OrdinalIgnoreCase) ||
                   (item.Arguments ?? string.Empty).IndexOf("shell:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IList<string> GetNearbyRoots(string oldDirectory, string fileName)
        {
            var roots = new List<string>();
            AddRoot(roots, oldDirectory);
            string parent = SafeDirectoryName(oldDirectory);
            if (!string.IsNullOrEmpty(parent) && IsNamedLikeExecutableRoot(oldDirectory, fileName)) AddRoot(roots, oldDirectory);
            if (!string.IsNullOrEmpty(parent)) AddRoot(roots, parent);
            return roots;
        }

        private static bool IsNamedLikeExecutableRoot(string directory, string fileName)
        {
            string directoryName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string executableName = Path.GetFileNameWithoutExtension(fileName);
            return !string.IsNullOrEmpty(directoryName) && !string.IsNullOrEmpty(executableName) &&
                   string.Equals(directoryName, executableName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksVersionedDirectory(string directory)
        {
            string name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("v", StringComparison.OrdinalIgnoreCase) && name.Length > 1 && char.IsDigit(name[1])) return true;
            bool hasDigit = false;
            bool hasDot = false;
            foreach (char value in name)
            {
                if (char.IsDigit(value)) hasDigit = true;
                if (value == '.') hasDot = true;
            }
            return hasDigit && hasDot;
        }

        private static void ScanImmediateDirectoryForFile(string directory, string fileName, string oldTarget, IDictionary<string, string> candidates)
        {
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)) return;
            try
            {
                string path = Normalize(Path.Combine(directory, fileName));
                if (!string.Equals(path, oldTarget, StringComparison.OrdinalIgnoreCase) && File.Exists(path)) AddCandidate(candidates, path);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("launch.resolve.directory=" + directory, ex);
            }
        }

        private static string[] GetDirectories(string directory)
        {
            try { return Directory.Exists(directory) ? Directory.GetDirectories(directory) : new string[0]; }
            catch (Exception ex) { EntryPoint.LogException("launch.resolve.enumerate=" + directory, ex); return new string[0]; }
        }

        private static void ReadAppPath(RegistryKey root, string fileName, string oldTarget, IDictionary<string, string> candidates)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(AppPathsSubKey + fileName, false))
                {
                    if (key == null) return;
                    string value = Convert.ToString(key.GetValue(string.Empty, string.Empty));
                    string path = Normalize(value);
                    if (!string.Equals(path, oldTarget, StringComparison.OrdinalIgnoreCase) && File.Exists(path)) AddCandidate(candidates, path);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("launch.resolve.app_paths=" + fileName, ex);
            }
        }

        private static void AddRoot(IList<string> roots, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string normalized = Normalize(path);
            foreach (string existing in roots)
            {
                if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)) return;
            }
            roots.Add(normalized);
        }

        private static void AddCandidate(IDictionary<string, string> candidates, string path)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(path)) return;
            string normalized = Normalize(path);
            if (!string.IsNullOrEmpty(normalized)) candidates[normalized] = normalized;
        }

        private static LaunchResolutionResult FromCandidates(IDictionary<string, string> candidates, string reason)
        {
            var paths = new List<string>();
            if (candidates != null)
            {
                foreach (string path in candidates.Values) paths.Add(path);
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            if (paths.Count == 1) return new LaunchResolutionResult(LaunchResolutionStatus.Found, paths[0], paths, reason);
            if (paths.Count > 1) return new LaunchResolutionResult(LaunchResolutionStatus.Ambiguous, string.Empty, paths, reason);
            return NotFound(reason);
        }

        private static LaunchResolutionResult NotFound(string reason)
        {
            return new LaunchResolutionResult(LaunchResolutionStatus.NotFound, string.Empty, new List<string>(), reason);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetFullPath(path.Trim().Trim('"')).TrimEnd('\\').ToLowerInvariant(); }
            catch { return path.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\').ToLowerInvariant(); }
        }

        private static string SafeDirectoryName(string path)
        {
            try { return Path.GetDirectoryName(path) ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeFileName(string path)
        {
            try { return Path.GetFileName(path) ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
