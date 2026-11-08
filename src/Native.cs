using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NabaGameBooster
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr h);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
        private static extern bool SpiArray(uint action, uint param, int[] vparam, uint winIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
        private static extern bool SpiPtr(uint action, uint param, IntPtr vparam, uint winIni);

        private const uint SPI_SETMOUSE = 0x0004;
        private const uint SPI_SETMOUSESPEED = 0x0071;
        private const uint SPIF_UPDATE_AND_SEND = 0x01 | 0x02;

        public static double TotalRamGB()
        {
            var m = new MEMORYSTATUSEX();
            return GlobalMemoryStatusEx(m) ? m.ullTotalPhys / 1073741824.0 : 0;
        }

        public static double FreeRamGB()
        {
            var m = new MEMORYSTATUSEX();
            return GlobalMemoryStatusEx(m) ? m.ullAvailPhys / 1073741824.0 : 0;
        }

        public static int RamLoadPercent()
        {
            var m = new MEMORYSTATUSEX();
            return GlobalMemoryStatusEx(m) ? (int)m.dwMemoryLoad : 0;
        }

        /// <summary>Process ki exe ka poora path (32/64-bit dono ke liye kaam karta hai).</summary>
        public static string ProcessPath(Process p)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, p.Id);
                if (h == IntPtr.Zero) return null;
                var sb = new StringBuilder(2048);
                int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
            }
            catch { return null; }
            finally { if (h != IntPtr.Zero) CloseHandle(h); }
        }

        /// <summary>Mouse acceleration (threshold1, threshold2, speed) turant laga deta hai.</summary>
        public static bool SetMouseAcceleration(int t1, int t2, int accel)
        {
            return SpiArray(SPI_SETMOUSE, 0, new[] { t1, t2, accel }, SPIF_UPDATE_AND_SEND);
        }

        /// <summary>Windows pointer speed 1-20 (10 = 1:1).</summary>
        public static bool SetMouseSpeed(int speed)
        {
            return SpiPtr(SPI_SETMOUSESPEED, 0, new IntPtr(speed), SPIF_UPDATE_AND_SEND);
        }
    }
}
