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

        [DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr hProcess);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private const uint PROCESS_SUSPEND_RESUME = 0x0800;
        private const uint PROCESS_SET_QUOTA = 0x0100;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        /// <summary>Process ko pause karta hai (andar ka kaam jaisa ka taisa).</summary>
        public static bool Suspend(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
            if (h == IntPtr.Zero) return false;
            try { return NtSuspendProcess(h) == 0; } finally { CloseHandle(h); }
        }

        /// <summary>Paused process ko wahin se chalu karta hai.</summary>
        public static bool Resume(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
            if (h == IntPtr.Zero) return false;
            try { return NtResumeProcess(h) == 0; } finally { CloseHandle(h); }
        }

        /// <summary>Process ki RAM khali karwata hai (data page file me chala jaata hai, resume pe wapas).</summary>
        public static void TrimMemory(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return;
            try { EmptyWorkingSet(h); } finally { CloseHandle(h); }
        }

        // ---------------- RAM cleaner (standby list) ----------------
        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string system, string name, out long luid);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attr; }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, int len, IntPtr prev, IntPtr retLen);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        private static bool EnablePrivilege(string name)
        {
            IntPtr tok;
            if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out tok)) return false;
            try
            {
                long luid;
                if (!LookupPrivilegeValue(null, name, out luid)) return false;
                var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attr = 2 /* SE_PRIVILEGE_ENABLED */ };
                return AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) && Marshal.GetLastWin32Error() == 0;
            }
            finally { CloseHandle(tok); }
        }

        /// <summary>Windows ki "standby" (cache) RAM khali karta hai. Admin chahiye.</summary>
        public static bool PurgeStandbyList()
        {
            try
            {
                EnablePrivilege("SeProfileSingleProcessPrivilege");
                EnablePrivilege("SeIncreaseQuotaPrivilege");
                int cmd = 3; // MemoryFlushModifiedList
                NtSetSystemInformation(80 /* SystemMemoryListInformation */, ref cmd, sizeof(int));
                cmd = 4;     // MemoryPurgeStandbyList
                return NtSetSystemInformation(80, ref cmd, sizeof(int)) == 0;
            }
            catch { return false; }
        }

        // ---------------- Timer resolution ----------------
        [DllImport("ntdll.dll")]
        private static extern int NtSetTimerResolution(uint desired, bool set, out uint current);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryTimerResolution(out uint min, out uint max, out uint current);

        /// <summary>0.5ms timer maangta hai (set=true) ya chhodta hai (set=false). Value 100ns units me.</summary>
        public static double SetTimerResolution(bool set)
        {
            try
            {
                uint min, max, cur;
                NtQueryTimerResolution(out min, out max, out cur);
                uint want = Math.Max(max, 5000u); // max = sabse chhota possible (aksar 5000 = 0.5ms)
                NtSetTimerResolution(want, set, out cur);
                return cur / 10000.0;
            }
            catch { return 0; }
        }

        public static double CurrentTimerMs()
        {
            try { uint min, max, cur; NtQueryTimerResolution(out min, out max, out cur); return cur / 10000.0; }
            catch { return 0; }
        }

        private const uint SPI_SETMOUSE = 0x0004;
        private const uint SPI_SETMOUSESPEED = 0x0071;
        private const uint SPIF_UPDATE_AND_SEND = 0x01 | 0x02;

        public static double TotalRamGB()
        {
            try { var m = new MEMORYSTATUSEX(); return GlobalMemoryStatusEx(m) ? m.ullTotalPhys / 1073741824.0 : 0; }
            catch { return 0; }
        }

        public static double FreeRamGB()
        {
            try { var m = new MEMORYSTATUSEX(); return GlobalMemoryStatusEx(m) ? m.ullAvailPhys / 1073741824.0 : 0; }
            catch { return 0; }
        }

        public static int RamLoadPercent()
        {
            try { var m = new MEMORYSTATUSEX(); return GlobalMemoryStatusEx(m) ? (int)m.dwMemoryLoad : 0; }
            catch { return 0; }
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
