using System;
using System.Globalization;

namespace MyCustomDock
{
    public static class SystemInfoFormatter
    {
        public static string FormatClock(DateTime value)
        {
            return value.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        public static string FormatDate(DateTime value)
        {
            return value.ToString("M/d", CultureInfo.InvariantCulture);
        }

        public static int GetVolumeVirtualKey(int direction)
        {
            if (direction > 0) return 0xAF;
            if (direction < 0) return 0xAE;
            return 0xAD;
        }
    }
}
