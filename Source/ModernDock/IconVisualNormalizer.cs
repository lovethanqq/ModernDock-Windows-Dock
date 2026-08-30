using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyCustomDock
{
    // Describes the native bitmap and its actual visible alpha content. The
    // native dimensions are deliberately kept separate from the normalized
    // render size so a 32px source cannot be reported as a high-resolution
    // asset merely because it is drawn on a 256px canvas.
    public sealed class IconVisualMetrics
    {
        internal IconVisualMetrics()
        {
            AlphaBoundingBox = Int32Rect.Empty;
        }

        public int SourcePixelWidth { get; internal set; }
        public int SourcePixelHeight { get; internal set; }
        public Int32Rect AlphaBoundingBox { get; internal set; }
        public int VisibleWidth { get; internal set; }
        public int VisibleHeight { get; internal set; }
        public double VisibleMaxDimensionRatio { get; internal set; }
        public double AlphaCoverage { get; internal set; }
        public double AspectRatio { get; internal set; }
        public double EffectiveVisualSizeDip { get; internal set; }
        public bool HasVisibleContent { get; internal set; }
        public bool IsOpaque { get; internal set; }
        public bool LikelyPadded { get; internal set; }
    }

    // Optical normalization is intentionally a load/render concern. It never
    // writes over the source PNG/ICO and is shared by fixed and dynamic Dock
    // items through CreateItemContainer.
    public static class IconVisualNormalizer
    {
        public const int NormalizedCanvasPixels = 256;
        public const int TargetVisibleMaxPixels = 220;
        public const byte AlphaThreshold = 12;

        private const int CacheLimit = 128;
        private const long MaxAnalyzedPixels = 64L * 1024L * 1024L;

        private sealed class CacheEntry
        {
            public BitmapSource Source;
            public BitmapSource Normalized;
            public IconVisualMetrics SourceMetrics;
        }

        private static readonly object CacheLock = new object();
        private static readonly List<CacheEntry> Cache = new List<CacheEntry>();

        public static int GetCacheEntryCount()
        {
            lock (CacheLock)
            {
                return Cache.Count;
            }
        }

        public static IconVisualMetrics Analyze(BitmapSource source)
        {
            if (source == null) return CreateEmptyMetrics();
            return AnalyzePixels(source);
        }

        public static BitmapSource Normalize(BitmapSource source)
        {
            if (source == null) return null;

            lock (CacheLock)
            {
                CacheEntry cached = FindEntry(source);
                if (cached != null) return cached.Normalized;
            }

            IconVisualMetrics metrics = AnalyzePixels(source);
            BitmapSource normalized = RenderNormalized(source, metrics);
            if (normalized == null) normalized = source;

            lock (CacheLock)
            {
                CacheEntry existing = FindEntry(source);
                if (existing != null) return existing.Normalized;

                Cache.Add(new CacheEntry
                {
                    Source = source,
                    Normalized = normalized,
                    SourceMetrics = metrics
                });
                while (Cache.Count > CacheLimit) Cache.RemoveAt(0);
            }

            return normalized;
        }

        private static IconVisualMetrics AnalyzePixels(BitmapSource source)
        {
            var metrics = new IconVisualMetrics
            {
                SourcePixelWidth = source.PixelWidth,
                SourcePixelHeight = source.PixelHeight,
                IsOpaque = false,
                HasVisibleContent = false,
                LikelyPadded = false,
                VisibleMaxDimensionRatio = 0.0,
                AlphaCoverage = 0.0,
                AspectRatio = 0.0,
                EffectiveVisualSizeDip = 0.0
            };

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            if (width <= 0 || height <= 0 || (long)width * height > MaxAnalyzedPixels) return metrics;

            try
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = source;
                converted.DestinationFormat = PixelFormats.Bgra32;
                converted.EndInit();
                converted.Freeze();

                int stride = checked(width * 4);
                byte[] pixels = new byte[checked(stride * height)];
                converted.CopyPixels(pixels, stride, 0);

                int left = width;
                int top = height;
                int right = -1;
                int bottom = -1;
                long visibleCount = 0;
                bool opaque = true;

                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte alpha = pixels[row + x * 4 + 3];
                        if (alpha != 255) opaque = false;
                        if (alpha <= AlphaThreshold) continue;

                        visibleCount++;
                        if (x < left) left = x;
                        if (y < top) top = y;
                        if (x > right) right = x;
                        if (y > bottom) bottom = y;
                    }
                }

                metrics.IsOpaque = opaque;
                if (right < left || bottom < top) return metrics;

                int visibleWidth = right - left + 1;
                int visibleHeight = bottom - top + 1;
                int sourceMax = Math.Max(width, height);
                int visibleMax = Math.Max(visibleWidth, visibleHeight);
                double visibleRatio = sourceMax == 0 ? 0.0 : (double)visibleMax / sourceMax;
                double coverage = (double)visibleCount / ((long)width * height);

                metrics.AlphaBoundingBox = new Int32Rect(left, top, visibleWidth, visibleHeight);
                metrics.VisibleWidth = visibleWidth;
                metrics.VisibleHeight = visibleHeight;
                metrics.VisibleMaxDimensionRatio = visibleRatio;
                metrics.AlphaCoverage = coverage;
                metrics.AspectRatio = visibleHeight == 0 ? 0.0 : (double)visibleWidth / visibleHeight;
                metrics.EffectiveVisualSizeDip = visibleRatio * 32.0;
                metrics.HasVisibleContent = true;
                metrics.LikelyPadded = !opaque &&
                    (left > 0 || top > 0 || right < width - 1 || bottom < height - 1) &&
                    (visibleRatio < 0.94 || coverage < 0.75);
                return metrics;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.visual_metrics", ex);
                return metrics;
            }
        }

        private static BitmapSource RenderNormalized(BitmapSource source, IconVisualMetrics metrics)
        {
            if (source == null || metrics == null || !metrics.HasVisibleContent) return source;

            try
            {
                BitmapSource sourceToDraw = source;
                if (!metrics.IsOpaque && metrics.AlphaBoundingBox.Width > 0 && metrics.AlphaBoundingBox.Height > 0)
                {
                    var cropped = new CroppedBitmap(source, metrics.AlphaBoundingBox);
                    cropped.Freeze();
                    sourceToDraw = cropped;
                }

                int sourceWidth = Math.Max(1, sourceToDraw.PixelWidth);
                int sourceHeight = Math.Max(1, sourceToDraw.PixelHeight);
                int sourceMax = Math.Max(sourceWidth, sourceHeight);
                double scale = (double)TargetVisibleMaxPixels / sourceMax;
                double drawWidth = Math.Max(1.0, sourceWidth * scale);
                double drawHeight = Math.Max(1.0, sourceHeight * scale);
                double x = (NormalizedCanvasPixels - drawWidth) / 2.0;
                double y = (NormalizedCanvasPixels - drawHeight) / 2.0;

                var visual = new DrawingVisual();
                RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
                using (DrawingContext context = visual.RenderOpen())
                {
                    context.DrawRectangle(Brushes.Transparent, null,
                        new Rect(0, 0, NormalizedCanvasPixels, NormalizedCanvasPixels));
                    context.DrawImage(sourceToDraw, new Rect(x, y, drawWidth, drawHeight));
                }

                var bitmap = new RenderTargetBitmap(
                    NormalizedCanvasPixels,
                    NormalizedCanvasPixels,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(visual);
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.visual_normalize", ex);
                return source;
            }
        }

        private static CacheEntry FindEntry(BitmapSource source)
        {
            for (int i = 0; i < Cache.Count; i++)
            {
                CacheEntry entry = Cache[i];
                if (!Object.ReferenceEquals(entry.Source, source)) continue;
                if (i != Cache.Count - 1)
                {
                    Cache.RemoveAt(i);
                    Cache.Add(entry);
                }
                return entry;
            }
            return null;
        }

        private static IconVisualMetrics CreateEmptyMetrics()
        {
            return new IconVisualMetrics
            {
                SourcePixelWidth = 0,
                SourcePixelHeight = 0,
                IsOpaque = false,
                HasVisibleContent = false,
                LikelyPadded = false,
                VisibleMaxDimensionRatio = 0.0,
                AlphaCoverage = 0.0,
                AspectRatio = 0.0,
                EffectiveVisualSizeDip = 0.0
            };
        }
    }
}
