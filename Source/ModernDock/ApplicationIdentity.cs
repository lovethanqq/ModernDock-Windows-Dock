using System;
using System.Collections.Generic;

namespace MyCustomDock
{
    public sealed class ApplicationIdentity
    {
        public ApplicationIdentity(string key, string normalizedPath, string processFamily, string displayName)
        {
            Key = key ?? string.Empty;
            NormalizedPath = normalizedPath ?? string.Empty;
            ProcessFamily = processFamily ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public string Key { get; private set; }
        public string NormalizedPath { get; private set; }
        public string ProcessFamily { get; private set; }
        public string DisplayName { get; private set; }
    }

    public sealed class ApplicationGroup
    {
        private readonly List<WindowSnapshot> windows = new List<WindowSnapshot>();

        public ApplicationGroup(ApplicationIdentity identity)
        {
            Identity = identity;
        }

        public ApplicationIdentity Identity { get; private set; }
        public IList<WindowSnapshot> Windows { get { return windows.AsReadOnly(); } }
        public WindowSnapshot Representative { get; private set; }

        public void Add(WindowSnapshot window)
        {
            if (window == null) return;
            windows.Add(window);

            if (Representative == null || IsBetterRepresentative(window, Representative))
            {
                Representative = window;
            }
        }

        private static bool IsBetterRepresentative(WindowSnapshot candidate, WindowSnapshot current)
        {
            if (candidate.IsMinimized != current.IsMinimized)
            {
                return !candidate.IsMinimized;
            }

            bool candidateHasTitle = !string.IsNullOrEmpty(candidate.WindowTitle);
            bool currentHasTitle = !string.IsNullOrEmpty(current.WindowTitle);
            return candidateHasTitle && !currentHasTitle;
        }
    }
}
