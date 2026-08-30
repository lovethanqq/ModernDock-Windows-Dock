using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyCustomDock
{
    public static class IconService
    {
        private const int CacheLimit = 128;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL2 = 2;
        private const int GCL_HICON = -14;
        private const int GCL_HICONSM = -34;
        private const uint SHGSI_ICON = 0x00000100;
        private const uint SHGSI_SHELLICONSIZE = 0x00000004;
        private const uint SIID_RECYCLER = 31;

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> CacheOrder = new LinkedList<string>();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr HIcon;
            public int IIcon;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string TypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            out SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint ExtractIconEx(
            string szFileName,
            int nIconIndex,
            IntPtr[] phiconLarge,
            IntPtr[] phiconSmall,
            uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint PrivateExtractIcons(
            string szFileName,
            int nIconIndex,
            int cxIcon,
            int cyIcon,
            IntPtr[] phicon,
            uint[] piconid,
            uint nIcons,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
        private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetClassLongW", SetLastError = true)]
        private static extern uint GetClassLong32(IntPtr hWnd, int index);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public uint CbSize;
            public IntPtr HIcon;
            public int ISysImageIndex;
            public int IIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string SzPath;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetStockIconInfo(
            uint siid,
            uint uFlags,
            ref SHSTOCKICONINFO psii);

        public static int GetCacheEntryCount()
        {
            lock (CacheLock)
            {
                return Cache.Count;
            }
        }

        // Used only when a generic Windows protocol action has no executable
        // resource of its own. This is a neutral system glyph, not a bundled
        // third-party application logo.
        public static ImageSource GetGenericApplicationIcon()
        {
            try
            {
                using (Icon icon = (Icon)SystemIcons.Application.Clone())
                {
                    if (icon == null || icon.Handle == IntPtr.Zero) return null;
                    return CopyHIcon(icon.Handle);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.generic_application", ex);
                return null;
            }
        }

        public static void Invalidate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string key = "file:" + NormalizePath(path);
            lock (CacheLock)
            {
                Cache.Remove(key);
                CacheOrder.Remove(key);
            }
        }

        public static void InvalidateExecutable(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string key = "exe:" + NormalizePath(path);
            lock (CacheLock)
            {
                Cache.Remove(key);
                CacheOrder.Remove(key);
            }
        }

        public static ImageSource LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string key = "file:" + NormalizePath(path);
            ImageSource cached = GetCached(key);
            if (cached != null) return cached;

            try
            {
                if (!File.Exists(path)) return null;
                using (var stream = File.OpenRead(path))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);
                    BitmapFrame frame = SelectLargest(decoder.Frames);
                    if (frame == null) return null;
                    frame.Freeze();
                    return PutCached(key, frame);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.load path=" + path, ex);
                return null;
            }
        }

        public static ImageSource ExtractBest(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath)) return null;
            string normalized = NormalizePath(executablePath);
            string key = "exe:" + normalized;
            ImageSource cached = GetCached(key);
            if (cached != null) return cached;

            try
            {
                if (!File.Exists(executablePath)) return null;

                ImageSource source = ExtractLargestExecutableIcon(executablePath);
                if (source == null) source = ExtractShellLargeIcon(executablePath);
                if (source == null) source = ExtractLargeExecutableIcon(executablePath);
                if (source == null) source = ExtractAssociatedIcon(executablePath);
                return source == null ? null : PutCached(key, source);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.extract path=" + executablePath, ex);
                return null;
            }
        }

        // Reads an icon owned by a live window without taking ownership of the
        // returned HICON. The window/class owns these handles; CopyHIcon makes
        // a frozen WPF bitmap and therefore no DestroyIcon is performed here.
        public static bool TryExtractWindowIcon(IntPtr hWnd, out BitmapSource source)
        {
            source = null;
            if (hWnd == IntPtr.Zero) return false;

            try
            {
                IntPtr icon = SendMessage(hWnd, WM_GETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
                if (icon == IntPtr.Zero) icon = SendMessage(hWnd, WM_GETICON, new IntPtr(ICON_SMALL2), IntPtr.Zero);
                if (icon == IntPtr.Zero) icon = SendMessage(hWnd, WM_GETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
                if (icon == IntPtr.Zero) icon = GetClassIcon(hWnd, GCL_HICON);
                if (icon == IntPtr.Zero) icon = GetClassIcon(hWnd, GCL_HICONSM);
                if (icon == IntPtr.Zero) return false;

                source = CopyHIcon(icon) as BitmapSource;
                return source != null;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.window path=0x" + hWnd.ToInt64().ToString("X"), ex);
                source = null;
                return false;
            }
        }

        // Uses the documented Windows stock-icon API. If the current Windows
        // build only supplies a small stock icon, callers intentionally keep
        // the existing persisted resource rather than using a guessed
        // imageres.dll index.
        public static bool TryExtractRecycleBinStockIcon(out BitmapSource source)
        {
            source = null;
            try
            {
                var info = new SHSTOCKICONINFO
                {
                    CbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO))
                };
                int result = SHGetStockIconInfo(SIID_RECYCLER, SHGSI_ICON | SHGSI_SHELLICONSIZE, ref info);
                if (result != 0 || info.HIcon == IntPtr.Zero) return false;

                try
                {
                    source = CopyHIcon(info.HIcon) as BitmapSource;
                    return source != null;
                }
                finally
                {
                    DestroyIcon(info.HIcon);
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.recycle_bin_stock", ex);
                source = null;
                return false;
            }
        }

        public static bool SavePng(BitmapSource source, string destinationPath)
        {
            if (source == null || string.IsNullOrWhiteSpace(destinationPath)) return false;

            string fullPath = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory)) return false;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.Save(stream);
                    stream.Flush(true);
                }

                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null);
                else File.Move(temporaryPath, fullPath);
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("icon.save path=" + fullPath, ex);
                return false;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) { EntryPoint.LogException("icon.save_cleanup path=" + temporaryPath, ex); }
                }
            }
        }

        private static BitmapFrame SelectLargest(IList<BitmapFrame> frames)
        {
            BitmapFrame largest = null;
            if (frames == null) return null;
            foreach (BitmapFrame frame in frames)
            {
                if (frame == null) continue;
                if (largest == null || frame.PixelWidth * frame.PixelHeight > largest.PixelWidth * largest.PixelHeight)
                {
                    largest = frame;
                }
            }
            return largest;
        }

        private static ImageSource ExtractShellLargeIcon(string path)
        {
            SHFILEINFO info;
            IntPtr result = SHGetFileInfo(path, 0, out info, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_ICON | SHGFI_LARGEICON);
            if (result == IntPtr.Zero || info.HIcon == IntPtr.Zero) return null;
            try
            {
                return CopyHIcon(info.HIcon);
            }
            finally
            {
                DestroyIcon(info.HIcon);
            }
        }

        private static ImageSource ExtractLargestExecutableIcon(string path)
        {
            // Ask Windows for the largest resource available before accepting
            // the normal 32px Shell large icon. Some EXEs only contain a 32px
            // resource; those simply fall through to the existing fallbacks.
            int[] sizes = { 256, 128, 64, 48 };
            foreach (int size in sizes)
            {
                IntPtr[] icons = new IntPtr[1];
                uint[] iconIds = new uint[1];
                try
                {
                    uint count = PrivateExtractIcons(path, 0, size, size, icons, iconIds, 1, 0);
                    if (count > 0 && icons[0] != IntPtr.Zero)
                    {
                        return CopyHIcon(icons[0]);
                    }
                }
                finally
                {
                    if (icons[0] != IntPtr.Zero) DestroyIcon(icons[0]);
                }
            }

            return null;
        }

        private static ImageSource ExtractLargeExecutableIcon(string path)
        {
            IntPtr[] large = new IntPtr[1];
            IntPtr[] small = new IntPtr[1];
            try
            {
                uint count = ExtractIconEx(path, 0, large, small, 1);
                IntPtr handle = count > 0 && large[0] != IntPtr.Zero ? large[0] : small[0];
                return handle == IntPtr.Zero ? null : CopyHIcon(handle);
            }
            finally
            {
                if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                if (small[0] != IntPtr.Zero && small[0] != large[0]) DestroyIcon(small[0]);
            }
        }

        private static ImageSource ExtractAssociatedIcon(string path)
        {
            using (Icon icon = Icon.ExtractAssociatedIcon(path))
            {
                if (icon == null) return null;
                IntPtr handle = icon.Handle;
                if (handle == IntPtr.Zero) return null;
                return CopyHIcon(handle);
            }
        }

        private static ImageSource CopyHIcon(IntPtr handle)
        {
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }

        private static IntPtr GetClassIcon(IntPtr hWnd, int index)
        {
            if (IntPtr.Size == 8) return GetClassLongPtr64(hWnd, index);
            return new IntPtr(unchecked((int)GetClassLong32(hWnd, index)));
        }

        private static ImageSource GetCached(string key)
        {
            lock (CacheLock)
            {
                ImageSource source;
                if (!Cache.TryGetValue(key, out source)) return null;
                CacheOrder.Remove(key);
                CacheOrder.AddLast(key);
                return source;
            }
        }

        private static ImageSource PutCached(string key, ImageSource source)
        {
            if (source == null) return null;
            lock (CacheLock)
            {
                Cache[key] = source;
                CacheOrder.Remove(key);
                CacheOrder.AddLast(key);
                while (Cache.Count > CacheLimit)
                {
                    LinkedListNode<string> first = CacheOrder.First;
                    if (first == null) break;
                    CacheOrder.RemoveFirst();
                    Cache.Remove(first.Value);
                }
            }
            return source;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                return Path.GetFullPath(path).TrimEnd('\\').ToLowerInvariant();
            }
            catch
            {
                return path.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
            }
        }
    }
}
