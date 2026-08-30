using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MyCustomDock
{
    public sealed class RefreshPhaseProfiler
    {
        private sealed class PhaseStats
        {
            public int Count;
            public double TotalMilliseconds;
            public double MaxMilliseconds;
        }

        private readonly Dictionary<string, PhaseStats> stats = new Dictionary<string, PhaseStats>(StringComparer.OrdinalIgnoreCase);

        public void Record(string phase, double elapsedMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(phase)) return;
            PhaseStats phaseStats;
            if (!stats.TryGetValue(phase, out phaseStats))
            {
                phaseStats = new PhaseStats();
                stats.Add(phase, phaseStats);
            }

            phaseStats.Count++;
            phaseStats.TotalMilliseconds += Math.Max(0.0, elapsedMilliseconds);
            phaseStats.MaxMilliseconds = Math.Max(phaseStats.MaxMilliseconds, elapsedMilliseconds);
        }

        public string SnapshotAndReset()
        {
            var output = new StringBuilder();
            foreach (var entry in stats)
            {
                if (output.Length > 0) output.Append(';');
                PhaseStats phase = entry.Value;
                double average = phase.Count == 0 ? 0.0 : phase.TotalMilliseconds / phase.Count;
                output.Append(entry.Key);
                output.Append("_count=");
                output.Append(phase.Count.ToString(CultureInfo.InvariantCulture));
                output.Append("_avg_ms=");
                output.Append(average.ToString("F3", CultureInfo.InvariantCulture));
                output.Append("_max_ms=");
                output.Append(phase.MaxMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            }
            stats.Clear();
            return output.ToString();
        }
    }
}
