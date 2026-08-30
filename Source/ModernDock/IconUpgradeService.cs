using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyCustomDock
{
    public sealed class IconCandidate
    {
        public string SourceType { get; internal set; }
        public string SourcePath { get; internal set; }
        public string Resource { get; internal set; }
        public int PixelWidth { get; internal set; }
        public int PixelHeight { get; internal set; }
        public BitmapSource Bitmap { get; internal set; }
        public IconVisualMetrics VisualMetrics { get; internal set; }
        public double VisualScore { get; internal set; }
    }

    // Performs one bounded, opt-in icon upgrade. It is not used by the 250ms
    // refresh loop and never replaces a user custom_* override.
    public static class IconUpgradeService
    {
        public static bool TryFindHighConfidenceCandidate(DockItem item, string currentIconPath, out IconCandidate candidate)
        {
            candidate = null;
            if (item == null || !item.IsFixed || IsCustomOverride(item.IconFile)) return false;

            string targetPath = item.TargetPath ?? string.Empty;

            BitmapSource current = LoadBitmap(currentIconPath);
            double currentScore = ScoreBitmap(current, "persisted_config_png");

            IconCandidate best = null;
            if (IsRecycleItem(item))
            {
                BitmapSource stock;
                if (IconService.TryExtractRecycleBinStockIcon(out stock) &&
                    stock.PixelWidth >= 64 && stock.PixelHeight >= 64)
                {
                    IconCandidate shell = CreateCandidate(
                        "shell_stock_icon",
                        "shell:recycle-bin",
                        "SHGetStockIconInfo(SIID_RECYCLER)",
                        stock);
                    if (IsBetterCandidate(shell, best)) best = shell;
                }
            }

            if (!IsLauncherHostPath(targetPath))
            {
                string targetDirectory = SafeDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory) && Directory.Exists(targetDirectory))
                {
                    string[] files = GetImmediateImageFiles(targetDirectory);
                    foreach (string file in files)
                    {
                        BitmapSource bitmap = LoadBitmap(file);
                        if (bitmap == null) continue;
                        IconCandidate asset = CreateCandidate(
                            "install_directory_asset", file, Path.GetFileName(file), bitmap);
                        if (IsBetterCandidate(asset, best)) best = asset;
                    }
                }

                if (File.Exists(targetPath))
                {
                    ImageSource source = IconService.ExtractBest(targetPath);
                    BitmapSource bitmap = source as BitmapSource;
                    if (bitmap != null)
                    {
                        IconCandidate executable = CreateCandidate(
                            "exe_resource_or_shell_icon", targetPath, "IconService.ExtractBest", bitmap);
                        if (IsBetterCandidate(executable, best)) best = executable;
                    }
                }
            }

            if (best == null || best.VisualScore <= currentScore + 0.01) return false;
            candidate = best;
            return true;
        }

        // A fixed item launched through a generic host should use only a
        // process or window icon from a window already attributed to that item.
        // This keeps launcher/host icons from replacing the user's application
        // identity without tying the behavior to a product name.
        public static bool TryFindWindowIconCandidate(
            DockItem item,
            string currentIconPath,
            IList<WindowSnapshot> windows,
            out IconCandidate candidate)
        {
            candidate = null;
            if (item == null || !item.IsFixed || windows == null || IsCustomOverride(item.IconFile)) return false;

            BitmapSource current = LoadBitmap(currentIconPath);
            double currentScore = ScoreBitmap(current, "persisted_config_png");
            IconCandidate best = null;
            foreach (WindowSnapshot window in windows)
            {
                if (window == null) continue;
                FixedItemMatch match = FixedItemMatcher.Resolve(new[] { item }, window);
                if (match == null || match.IsAmbiguous || match.Item != item) continue;

                BitmapSource processBitmap = null;
                string processPath = window.ProcessPath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath) &&
                    !IsLauncherHostPath(processPath))
                {
                    processBitmap = IconService.ExtractBest(processPath) as BitmapSource;
                    if (processBitmap != null)
                    {
                        IconCandidate processCandidate = CreateCandidate(
                            "window_process_resource", processPath, "IconService.ExtractBest", processBitmap);
                        if (IsBetterCandidate(processCandidate, best)) best = processCandidate;
                    }
                }

                BitmapSource windowBitmap;
                if (IconService.TryExtractWindowIcon(window.Handle, out windowBitmap))
                {
                    IconCandidate windowCandidate = CreateCandidate(
                        "window_icon", "0x" + window.Handle.ToInt64().ToString("X"), "WM_GETICON/class-icon", windowBitmap);
                    if (IsBetterCandidate(windowCandidate, best)) best = windowCandidate;
                }
            }

            if (best == null || best.VisualScore <= currentScore + 0.01) return false;
            candidate = best;
            return true;
        }

        public static bool TryApplyCandidate(DockItem item, IconCandidate candidate, string destinationPath)
        {
            if (item == null || candidate == null || candidate.Bitmap == null ||
                string.IsNullOrWhiteSpace(destinationPath) || IsCustomOverride(item.IconFile) ||
                IsCustomOverride(Path.GetFileName(destinationPath))) return false;

            return IconService.SavePng(candidate.Bitmap, destinationPath);
        }

        public static bool IsCustomOverride(string iconFile)
        {
            return !string.IsNullOrEmpty(iconFile) &&
                   (iconFile.StartsWith("custom_", StringComparison.OrdinalIgnoreCase) ||
                    iconFile.StartsWith("custom-", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsLauncherHostPath(string path)
        {
            return ApplicationIdentityResolver.IsGenericLauncherOrHostPath(path);
        }

        private static bool IsRecycleItem(DockItem item)
        {
            return item != null &&
                (item.Arguments ?? string.Empty).IndexOf("shell:RecycleBinFolder", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] GetImmediateImageFiles(string directory)
        {
            try
            {
                var result = new System.Collections.Generic.List<string>();
                foreach (string file in Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
                {
                    string extension = Path.GetExtension(file);
                    if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase)) continue;

                    string baseName = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
                    if (baseName.StartsWith("logo", StringComparison.OrdinalIgnoreCase) ||
                        baseName.StartsWith("icon", StringComparison.OrdinalIgnoreCase) ||
                        baseName.StartsWith("appicon", StringComparison.OrdinalIgnoreCase) ||
                        baseName.StartsWith("application", StringComparison.OrdinalIgnoreCase) ||
                        baseName.StartsWith("product", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(file);
                    }
                    if (result.Count >= 64) break;
                }
                return result.ToArray();
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.upgrade.enumerate=" + directory, ex);
                return new string[0];
            }
        }

        private static bool IsBetterCandidate(IconCandidate candidate, IconCandidate current)
        {
            if (candidate == null) return false;
            if (current == null) return true;
            if (Math.Abs(candidate.VisualScore - current.VisualScore) > 0.0001)
            {
                return candidate.VisualScore > current.VisualScore;
            }

            int candidatePixels = candidate.PixelWidth * candidate.PixelHeight;
            int currentPixels = current.PixelWidth * current.PixelHeight;
            return candidatePixels > currentPixels;
        }

        private static IconCandidate CreateCandidate(string sourceType, string sourcePath, string resource, BitmapSource bitmap)
        {
            IconVisualMetrics metrics = IconVisualNormalizer.Analyze(bitmap);
            return new IconCandidate
            {
                SourceType = sourceType,
                SourcePath = sourcePath,
                Resource = resource,
                PixelWidth = bitmap == null ? 0 : bitmap.PixelWidth,
                PixelHeight = bitmap == null ? 0 : bitmap.PixelHeight,
                Bitmap = bitmap,
                VisualMetrics = metrics,
                VisualScore = ScoreBitmap(metrics, sourceType)
            };
        }

        private static double ScoreBitmap(BitmapSource bitmap, string sourceType)
        {
            return ScoreBitmap(IconVisualNormalizer.Analyze(bitmap), sourceType);
        }

        private static double ScoreBitmap(IconVisualMetrics metrics, string sourceType)
        {
            if (metrics == null || metrics.SourcePixelWidth <= 0 || metrics.SourcePixelHeight <= 0) return double.MinValue;

            double nativeResolution = Math.Min(1.0,
                (double)Math.Max(metrics.SourcePixelWidth, metrics.SourcePixelHeight) / 256.0);
            double sourceAspect = (double)Math.Min(metrics.SourcePixelWidth, metrics.SourcePixelHeight) /
                Math.Max(metrics.SourcePixelWidth, metrics.SourcePixelHeight);
            double visible = metrics.HasVisibleContent ? metrics.VisibleMaxDimensionRatio : 0.0;
            double coverage = Math.Min(1.0, Math.Max(0.0, metrics.AlphaCoverage));
            double confidence = SourceConfidence(sourceType);
            double score = nativeResolution * 0.42 + sourceAspect * 0.20 + visible * 0.18 +
                coverage * 0.08 + confidence * 0.12;
            if (metrics.LikelyPadded) score -= 0.15;
            return score;
        }

        private static double SourceConfidence(string sourceType)
        {
            if (string.Equals(sourceType, "install_directory_asset", StringComparison.OrdinalIgnoreCase)) return 0.95;
            if (string.Equals(sourceType, "exe_resource_or_shell_icon", StringComparison.OrdinalIgnoreCase)) return 0.90;
            if (string.Equals(sourceType, "window_process_resource", StringComparison.OrdinalIgnoreCase)) return 0.92;
            if (string.Equals(sourceType, "window_icon", StringComparison.OrdinalIgnoreCase)) return 0.88;
            if (string.Equals(sourceType, "shell_stock_icon", StringComparison.OrdinalIgnoreCase)) return 0.86;
            return 0.55;
        }

        private static BitmapSource LoadBitmap(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    BitmapFrame largest = null;
                    foreach (BitmapFrame frame in decoder.Frames)
                    {
                        if (largest == null || frame.PixelWidth * frame.PixelHeight > largest.PixelWidth * largest.PixelHeight) largest = frame;
                    }
                    if (largest == null) return null;
                    largest.Freeze();
                    return largest;
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.upgrade.load=" + path, ex);
                return null;
            }
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
