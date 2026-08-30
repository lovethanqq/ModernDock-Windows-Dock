using System;
using System.Collections.Generic;
using System.IO;

namespace MyCustomDock
{
    public enum FixedMatchPriority
    {
        None = 0,
        ProcessNameWindowClassFallback = 1,
        LimitedProcessFamily = 2,
        PathMatch = 3,
        ExactNormalizedTargetPath = 4,
        ShellExplicitIdentity = 5
    }

    public sealed class FixedItemMatch
    {
        public FixedItemMatch(DockItem item, FixedMatchPriority priority, int specificity, string reason, bool isAmbiguous, IList<DockItem> candidates)
        {
            Item = item;
            Priority = priority;
            Specificity = specificity;
            Reason = reason ?? string.Empty;
            IsAmbiguous = isAmbiguous;
            Candidates = candidates ?? new List<DockItem>();
        }

        public DockItem Item { get; private set; }
        public FixedMatchPriority Priority { get; private set; }
        public int Specificity { get; private set; }
        public string Reason { get; private set; }
        public bool IsAmbiguous { get; private set; }
        public IList<DockItem> Candidates { get; private set; }
    }

    public static class FixedItemMatcher
    {
        private sealed class Candidate
        {
            public DockItem Item;
            public FixedMatchPriority Priority;
            public int Specificity;
            public string Reason;
        }

        private sealed class PreparedRule
        {
            public DockItem Item;
            public bool IsRecycle;
            public bool IsExplorer;
            public bool IsSettings;
            public bool IsPowerShell;
            public string TargetPath;
            public bool TargetIsRooted;
            public bool TargetIsGeneric;
            public bool HasPathRule;
            public string PathMatch;
            public string ItemFamily;
            public string[] ProcessNames;
            public string WindowClass;
        }

        public sealed class MatchContext
        {
            private readonly List<PreparedRule> rules;

            internal MatchContext(IList<DockItem> items)
            {
                rules = new List<PreparedRule>();
                if (items == null) return;

                foreach (var item in items)
                {
                    if (item != null && item.IsFixed)
                    {
                        rules.Add(PrepareRule(item));
                    }
                }
            }

            public FixedItemMatch Resolve(WindowSnapshot window)
            {
                var candidates = new List<Candidate>();
                if (window == null) return EmptyMatch();

                string shellIdentity = (window.ShellIdentity ?? string.Empty).Trim();
                string windowPath = ApplicationIdentityResolver.NormalizeExecutablePath(window.ProcessPath);
                bool pathAvailable = !string.IsNullOrEmpty(windowPath);
                string windowFamily = ApplicationIdentityResolver.GetProcessFamilyKey(windowPath, window.ProcessName);

                foreach (var rule in rules)
                {
                    Candidate candidate;
                    if (TryScorePrepared(rule, window, shellIdentity, windowPath, pathAvailable, windowFamily, out candidate))
                    {
                        candidates.Add(candidate);
                    }
                }

                return SelectBest(candidates, window);
            }
        }

        public static MatchContext CreateContext(IList<DockItem> items)
        {
            return new MatchContext(items);
        }

        public static FixedItemMatch Resolve(IList<DockItem> items, WindowSnapshot window)
        {
            return CreateContext(items).Resolve(window);
        }

        private static FixedItemMatch EmptyMatch()
        {
            return new FixedItemMatch(null, FixedMatchPriority.None, 0, string.Empty, false, new List<DockItem>());
        }

        private static FixedItemMatch SelectBest(List<Candidate> candidates, WindowSnapshot window)
        {
            if (candidates == null || candidates.Count == 0) return EmptyMatch();

            FixedMatchPriority bestPriority = FixedMatchPriority.None;
            int bestSpecificity = 0;
            foreach (var candidate in candidates)
            {
                if (candidate.Priority > bestPriority ||
                    (candidate.Priority == bestPriority && candidate.Specificity > bestSpecificity))
                {
                    bestPriority = candidate.Priority;
                    bestSpecificity = candidate.Specificity;
                }
            }

            var best = new List<Candidate>();
            foreach (var candidate in candidates)
            {
                if (candidate.Priority == bestPriority && candidate.Specificity == bestSpecificity)
                {
                    best.Add(candidate);
                }
            }

            if (best.Count > 1)
            {
                var ambiguousItems = new List<DockItem>();
                foreach (var candidate in best) ambiguousItems.Add(candidate.Item);
                EntryPoint.Log("matching.ambiguity hwnd=0x" + window.Handle.ToInt64().ToString("X") +
                    " priority=" + bestPriority + " specificity=" + bestSpecificity +
                    " candidates=" + DescribeCandidates(ambiguousItems));
                return new FixedItemMatch(null, bestPriority, bestSpecificity, "ambiguous", true, ambiguousItems);
            }

            Candidate winner = best[0];
            return new FixedItemMatch(winner.Item, winner.Priority, winner.Specificity, winner.Reason, false, new List<DockItem> { winner.Item });
        }

        private static PreparedRule PrepareRule(DockItem item)
        {
            string targetValue = (item.TargetPath ?? string.Empty).Trim().Trim('"').Replace('/', '\\');
            string pathMatch = NormalizeConfiguredPath(item.PathMatch);
            string processMatch = (item.ProcessNameMatch ?? string.Empty).Trim();
            return new PreparedRule
            {
                Item = item,
                IsRecycle = IsRecycleItem(item),
                IsExplorer = IsExplorerItem(item),
                IsSettings = IsSettingsItem(item),
                IsPowerShell = IsPowerShellItem(item),
                TargetIsRooted = ApplicationIdentityResolver.IsFullyQualifiedPath(targetValue),
                TargetPath = ApplicationIdentityResolver.NormalizeExecutablePath(targetValue),
                TargetIsGeneric = ApplicationIdentityResolver.IsGenericLauncherOrHostPath(targetValue),
                HasPathRule = !string.IsNullOrEmpty(pathMatch),
                PathMatch = pathMatch,
                ItemFamily = GetItemProcessFamily(item),
                ProcessNames = processMatch.Length == 0 ? new string[0] : processMatch.Split(';'),
                WindowClass = (item.WindowClassMatch ?? string.Empty).Trim()
            };
        }

        private static bool TryScorePrepared(PreparedRule rule, WindowSnapshot window, string shellIdentity,
            string windowPath, bool pathAvailable, string windowFamily, out Candidate candidate)
        {
            candidate = null;
            if (rule == null || window == null) return false;

            if (rule.IsRecycle && string.Equals(shellIdentity, ShellIdentityResolver.RecycleBinIdentity, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.ShellExplicitIdentity, 100, "shell-recycle-bin");
                return true;
            }

            if (rule.IsSettings && !MatchesSettingsWindow(window)) return false;
            if (rule.IsSettings && MatchesSettingsWindow(window))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.ProcessNameWindowClassFallback, 80, "settings-explicit-identity");
                return true;
            }
            if (rule.IsExplorer && string.Equals(shellIdentity, ShellIdentityResolver.ExplorerIdentity, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.ShellExplicitIdentity, 90, "shell-explorer");
                return true;
            }

            if (rule.TargetIsRooted && pathAvailable && !rule.TargetIsGeneric &&
                string.Equals(rule.TargetPath, windowPath, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.ExactNormalizedTargetPath, rule.TargetPath.Length, "exact-target-path");
                return true;
            }

            if (rule.TargetIsRooted && pathAvailable && string.Equals(rule.TargetPath, windowPath, StringComparison.OrdinalIgnoreCase) &&
                (rule.IsExplorer || rule.IsRecycle))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.ExactNormalizedTargetPath, rule.TargetPath.Length, "explicit-shell-target-path");
                return true;
            }

            if (rule.HasPathRule && pathAvailable && PathMatchesNormalized(windowPath, rule.PathMatch))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.PathMatch, rule.PathMatch.Length, "normalized-path-match");
                return true;
            }

            if (!string.IsNullOrEmpty(rule.ItemFamily) && string.Equals(rule.ItemFamily, windowFamily, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.LimitedProcessFamily, rule.ItemFamily.Length, "limited-process-family");
                return true;
            }

            if (rule.IsPowerShell && MatchesPowerShellHost(window))
            {
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.ProcessNameWindowClassFallback, 40, "powershell-host-fallback");
                return true;
            }

            if (pathAvailable && (rule.TargetIsRooted || rule.HasPathRule)) return false;

            bool processNameMatched = ProcessNameMatchesPrepared(rule.ProcessNames, window.ProcessName);
            bool classMatched = !string.IsNullOrEmpty(rule.WindowClass) &&
                                string.Equals(rule.WindowClass, window.WindowClass ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (processNameMatched || classMatched)
            {
                int specificity = Math.Max(string.Join(";", rule.ProcessNames).Length, rule.WindowClass.Length);
                candidate = CreateCandidate(rule.Item, FixedMatchPriority.ProcessNameWindowClassFallback, specificity, classMatched ? "window-class-fallback" : "process-name-fallback");
                return true;
            }

            return false;
        }

        private static string NormalizeConfiguredPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string normalized = value.Trim().Trim('"').Replace('/', '\\');
            if (ApplicationIdentityResolver.IsFullyQualifiedPath(normalized))
            {
                return ApplicationIdentityResolver.NormalizeExecutablePath(normalized);
            }
            return normalized.Trim('\\').ToLowerInvariant();
        }

        private static bool PathMatchesNormalized(string normalizedProcessPath, string normalizedConfiguredPath)
        {
            if (string.IsNullOrEmpty(normalizedProcessPath) || string.IsNullOrEmpty(normalizedConfiguredPath)) return false;
            if (ApplicationIdentityResolver.IsFullyQualifiedPath(normalizedConfiguredPath))
            {
                return string.Equals(normalizedProcessPath, normalizedConfiguredPath, StringComparison.OrdinalIgnoreCase) ||
                       normalizedProcessPath.StartsWith(normalizedConfiguredPath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
            }

            if (normalizedConfiguredPath.IndexOf('\\') >= 0)
            {
                string wrappedPath = "\\" + normalizedProcessPath.Trim('\\') + "\\";
                string wrappedMatch = "\\" + normalizedConfiguredPath.Trim('\\') + "\\";
                return wrappedPath.IndexOf(wrappedMatch, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            int offset = 0;
            while (offset < normalizedProcessPath.Length)
            {
                int found = normalizedProcessPath.IndexOf(normalizedConfiguredPath, offset, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return false;
                bool leftBoundary = found == 0 || !IsPathTokenCharacter(normalizedProcessPath[found - 1]);
                int end = found + normalizedConfiguredPath.Length;
                bool rightBoundary = end >= normalizedProcessPath.Length || !IsPathTokenCharacter(normalizedProcessPath[end]);
                if (leftBoundary && rightBoundary) return true;
                offset = found + 1;
            }
            return false;
        }

        private static bool ProcessNameMatchesPrepared(string[] configuredNames, string processName)
        {
            if (configuredNames == null || configuredNames.Length == 0 || string.IsNullOrEmpty(processName)) return false;
            foreach (var name in configuredNames)
            {
                if (string.Equals(name.Trim(), processName.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsPathTokenCharacter(char value)
        {
            return char.IsLetterOrDigit(value);
        }

        private static bool TryScore(DockItem item, WindowSnapshot window, out Candidate candidate)
        {
            candidate = null;
            if (item == null || window == null || !item.IsFixed) return false;

            string shellIdentity = (window.ShellIdentity ?? string.Empty).Trim();
            if (IsRecycleItem(item) && string.Equals(shellIdentity, ShellIdentityResolver.RecycleBinIdentity, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(item, FixedMatchPriority.ShellExplicitIdentity, 100, "shell-recycle-bin");
                return true;
            }

            if (IsSettingsItem(item) && !MatchesSettingsWindow(window))
            {
                return false;
            }

            if (IsSettingsItem(item) && MatchesSettingsWindow(window))
            {
                candidate = CreateCandidate(item, FixedMatchPriority.ProcessNameWindowClassFallback, 80, "settings-explicit-identity");
                return true;
            }
            if (IsExplorerItem(item) && string.Equals(shellIdentity, ShellIdentityResolver.ExplorerIdentity, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(item, FixedMatchPriority.ShellExplicitIdentity, 90, "shell-explorer");
                return true;
            }

            string windowPath = ApplicationIdentityResolver.NormalizeExecutablePath(window.ProcessPath);
            string targetValue = (item.TargetPath ?? string.Empty).Trim().Trim('"').Replace('/', '\\');
            bool targetIsRooted = ApplicationIdentityResolver.IsFullyQualifiedPath(targetValue);
            string targetPath = ApplicationIdentityResolver.NormalizeExecutablePath(targetValue);
            if (targetIsRooted && !string.IsNullOrEmpty(windowPath) &&
                !ApplicationIdentityResolver.IsGenericLauncherOrHostPath(targetPath) &&
                string.Equals(targetPath, windowPath, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(item, FixedMatchPriority.ExactNormalizedTargetPath, targetPath.Length, "exact-target-path");
                return true;
            }

            if (targetIsRooted && !string.IsNullOrEmpty(windowPath) &&
                string.Equals(targetPath, windowPath, StringComparison.OrdinalIgnoreCase) &&
                (IsExplorerItem(item) || IsRecycleItem(item)))
            {
                candidate = CreateCandidate(item, FixedMatchPriority.ExactNormalizedTargetPath, targetPath.Length, "explicit-shell-target-path");
                return true;
            }

            bool hasPathRule = !string.IsNullOrEmpty(item.PathMatch);
            bool pathAvailable = !string.IsNullOrEmpty(windowPath);
            if (hasPathRule && pathAvailable && PathMatches(windowPath, item.PathMatch))
            {
                string normalizedMatch = (item.PathMatch ?? string.Empty).Trim().Trim('"').Replace('/', '\\').Trim('\\').ToLowerInvariant();
                candidate = CreateCandidate(item, FixedMatchPriority.PathMatch, normalizedMatch.Length, "normalized-path-match");
                return true;
            }

            string itemFamily = GetItemProcessFamily(item);
            string windowFamily = ApplicationIdentityResolver.GetProcessFamilyKey(window);
            if (!string.IsNullOrEmpty(itemFamily) && string.Equals(itemFamily, windowFamily, StringComparison.OrdinalIgnoreCase))
            {
                candidate = CreateCandidate(item, FixedMatchPriority.LimitedProcessFamily, itemFamily.Length, "limited-process-family");
                return true;
            }

            // PowerShell's Windows Terminal host is the one intentionally retained
            // special rule: its visible window is not owned by pwsh.exe.
            if (IsPowerShellItem(item) && MatchesPowerShellHost(window))
            {
                candidate = CreateCandidate(item, FixedMatchPriority.ProcessNameWindowClassFallback, 40, "powershell-host-fallback");
                return true;
            }

            // Once a process path is known, a strong path rule that failed must not
            // fall back to a generic process name. This prevents chrome.exe apps
            // installed in different directories from crossing fixed identities.
            if (pathAvailable && (targetIsRooted || hasPathRule)) return false;

            bool processNameMatched = ProcessNameMatches(item.ProcessNameMatch, window.ProcessName);
            bool classMatched = !string.IsNullOrEmpty(item.WindowClassMatch) &&
                                string.Equals(item.WindowClassMatch.Trim(), window.WindowClass ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (processNameMatched || classMatched)
            {
                int specificity = Math.Max((item.ProcessNameMatch ?? string.Empty).Length, (item.WindowClassMatch ?? string.Empty).Length);
                candidate = CreateCandidate(item, FixedMatchPriority.ProcessNameWindowClassFallback, specificity, classMatched ? "window-class-fallback" : "process-name-fallback");
                return true;
            }

            return false;
        }

        private static Candidate CreateCandidate(DockItem item, FixedMatchPriority priority, int specificity, string reason)
        {
            return new Candidate { Item = item, Priority = priority, Specificity = specificity, Reason = reason };
        }

        private static bool PathMatches(string processPath, string configuredPath)
        {
            string value = (configuredPath ?? string.Empty).Trim().Trim('"').Replace('/', '\\');
            if (string.IsNullOrEmpty(value)) return false;
            if (ApplicationIdentityResolver.IsFullyQualifiedPath(value)) return ApplicationIdentityResolver.PathContainsDirectory(processPath, value);
            if (value.IndexOf('\\') >= 0) return ApplicationIdentityResolver.PathContainsDirectory(processPath, value);
            return ApplicationIdentityResolver.PathContainsToken(processPath, value);
        }

        private static bool ProcessNameMatches(string configuredNames, string processName)
        {
            if (string.IsNullOrEmpty(configuredNames) || string.IsNullOrEmpty(processName)) return false;
            foreach (var name in configuredNames.Split(';'))
            {
                if (string.Equals(name.Trim(), processName.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string GetItemProcessFamily(DockItem item)
        {
            string family = ApplicationIdentityResolver.GetProcessFamilyKey(item.TargetPath, string.Empty);
            if (!string.IsNullOrEmpty(family)) return family;

            foreach (var name in (item.ProcessNameMatch ?? string.Empty).Split(';'))
            {
                family = ApplicationIdentityResolver.GetProcessFamilyKey(string.Empty, name.Trim());
                if (!string.IsNullOrEmpty(family)) return family;
            }
            return string.Empty;
        }

        private static bool IsRecycleItem(DockItem item)
        {
            return item.Arguments != null && item.Arguments.IndexOf("shell:RecycleBinFolder", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsExplorerItem(DockItem item)
        {
            return string.Equals(item.Title, "文件资源管理器", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(item.Title, "File Explorer", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPowerShellItem(DockItem item)
        {
            return (item.Title ?? string.Empty).IndexOf("PowerShell", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSettingsItem(DockItem item)
        {
            string title = item == null ? string.Empty : (item.Title ?? string.Empty);
            return title.IndexOf("设置", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesSettingsWindow(WindowSnapshot window)
        {
            string title = window == null ? string.Empty : (window.WindowTitle ?? string.Empty);
            if (title.IndexOf("设置", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string processName = window == null ? string.Empty : (window.ProcessName ?? string.Empty);
            string processPath = window == null ? string.Empty : (window.ProcessPath ?? string.Empty);
            return processName.IndexOf("SystemSettings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   processPath.IndexOf("SystemSettings", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesPowerShellHost(WindowSnapshot window)
        {
            bool terminal = string.Equals(window.ProcessName, "WindowsTerminal", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(window.WindowClass, "CASCADIA_HOSTING_WINDOW_CLASS", StringComparison.OrdinalIgnoreCase);
            bool title = (window.WindowTitle ?? string.Empty).IndexOf("PowerShell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (window.WindowTitle ?? string.Empty).IndexOf("pwsh", StringComparison.OrdinalIgnoreCase) >= 0;
            return terminal && title;
        }

        private static string DescribeCandidates(IList<DockItem> items)
        {
            var names = new List<string>();
            foreach (var item in items)
            {
                names.Add(item == null ? "<null>" : (item.Title ?? "<untitled>"));
            }
            return string.Join(",", names.ToArray());
        }
    }
}
