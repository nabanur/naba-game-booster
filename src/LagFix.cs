using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace NabaGameBooster
{
    /// <summary>
    /// "High FPS par bhi laggy" ki asli wajah dhoondh ke theek karta hai:
    /// monitor Hz, cable / FreeSync, BlueStacks full screen, FPS > Hz, background apps, internet, NVIDIA, Fast Mode.
    /// Jo app khud theek kar sakti hai woh karti hai, baaki "aapko karna hai" list me jata hai.
    /// </summary>
    internal static class LagFix
    {
        /// <summary>Jo cheez user ko khud karni hai (cable lagana, monitor menu...).</summary>
        public static readonly List<string> Todo = new List<string>();
        public static bool SuggestNvcp;

        // ===================== 1. Monitor refresh rate =====================
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Ansi)] private static extern bool EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Ansi)] private static extern int ChangeDisplaySettingsEx(string dev, ref DEVMODE dm, IntPtr hwnd, uint flags, IntPtr lp);
        private const int ENUM_CURRENT = -1;
        private const int DM_BITSPERPEL = 0x40000, DM_PELSWIDTH = 0x80000, DM_PELSHEIGHT = 0x100000, DM_DISPLAYFREQUENCY = 0x400000;

        public static void RefreshRate(Action<string> log)
        {
            bool any = false;
            foreach (var sc in Screen.AllScreens)
            {
                var cur = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (!EnumDisplaySettings(sc.DeviceName, ENUM_CURRENT, ref cur)) continue;
                any = true;
                int best = cur.dmDisplayFrequency; DEVMODE bestMode = cur;
                for (int i = 0; ; i++)
                {
                    var m = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                    if (!EnumDisplaySettings(sc.DeviceName, i, ref m)) break;
                    if (m.dmPelsWidth == cur.dmPelsWidth && m.dmPelsHeight == cur.dmPelsHeight && m.dmBitsPerPel == cur.dmBitsPerPel
                        && m.dmDisplayFrequency > best && m.dmDisplayFrequency <= 500) { best = m.dmDisplayFrequency; bestMode = m; }
                }
                string name = sc.Primary ? "Monitor" : "Dusra monitor";
                if (best <= cur.dmDisplayFrequency) { log("[OK] " + name + " apni full speed pe hai: " + cur.dmDisplayFrequency + " Hz"); continue; }
                bestMode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY;
                if (ChangeDisplaySettingsEx(sc.DeviceName, ref bestMode, IntPtr.Zero, 2 /*CDS_TEST*/, IntPtr.Zero) == 0 &&
                    ChangeDisplaySettingsEx(sc.DeviceName, ref bestMode, IntPtr.Zero, 1 /*CDS_UPDATEREGISTRY*/, IntPtr.Zero) == 0)
                    log("[OK] FIX: " + name + " " + cur.dmDisplayFrequency + " Hz pe chal raha tha -> ab " + best + " Hz");
                else
                {
                    log(name + " " + cur.dmDisplayFrequency + " Hz pe hai, " + best + " Hz nahi laga paya");
                    Todo.Add("Windows Settings > Display > Advanced display me refresh rate " + best + " Hz chuno");
                }
            }
            if (!any) log("[OK] Monitor check");
        }

        // ===================== 2. Cable / FreeSync =====================
        public static void Cable(Action<string> log)
        {
            int tech = -1;
            try
            {
                using (var q = new System.Management.ManagementObjectSearcher(@"root\wmi", "SELECT VideoOutputTechnology, Active FROM WmiMonitorConnectionParams"))
                    foreach (System.Management.ManagementObject mo in q.Get())
                    {
                        if (mo["Active"] != null && !(bool)mo["Active"]) continue;
                        tech = Convert.ToInt32(Convert.ToInt64(mo["VideoOutputTechnology"]) & 0x7FFFFFFF); break;
                    }
            }
            catch { }
            bool nv = Tweaks.NvidiaGpuName() != null;
            string t = tech == 5 ? "HDMI" : (tech == 10 || tech == 11) ? "DisplayPort" : tech == 4 ? "DVI" : tech == 0 ? "VGA" : "?";
            if (t == "DisplayPort")
            {
                log("[OK] Cable: DisplayPort - FreeSync / G-SYNC chal sakta hai");
                if (nv)
                {
                    SuggestNvcp = true;
                    Todo.Add("Monitor ke menu me FreeSync / Adaptive Sync ON karo");
                    Todo.Add("NVIDIA Control Panel > Set up G-SYNC > \"Enable G-SYNC, G-SYNC Compatible\" tick karo, phir FPS LIMIT me \"" + Math.Max(30, Tweaks.MonitorHz() - 3) + " (G-SYNC)\" chuno");
                }
            }
            else if (t == "HDMI" && nv)
            {
                log("Cable: HDMI - NVIDIA pe FreeSync nahi chalega (isliye jhatke / tearing)");
                Todo.Add("Monitor ko DisplayPort (DP) cable se jodo - FreeSync / G-SYNC chalega, jhatke khatam");
            }
            else log("[OK] Cable: " + t);
        }

        // ===================== 3. BlueStacks full screen =====================
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }

        private static bool IsFull(IntPtr h)
        {
            RECT r; if (!GetWindowRect(h, out r)) return false;
            var b = Screen.FromHandle(h).Bounds;
            return r.L <= b.Left + 2 && r.T <= b.Top + 2 && r.R >= b.Right - 2 && r.B >= b.Bottom - 2;
        }

        public static void FullScreen(Action<string> log)
        {
            var players = Process.GetProcessesByName("HD-Player").Where(p => p.MainWindowHandle != IntPtr.Zero).ToList();
            if (players.Count == 0)
            {
                // BlueStacks band hai: uski apni setting "apps full screen me" ON
                int n = 0;
                foreach (var e in Emulators.Installed())
                {
                    try
                    {
                        if (!File.Exists(e.Conf)) continue;
                        string text = File.ReadAllText(e.Conf);
                        string nt = Regex.Replace(text, @"(bst\.instance\.[^.\r\n]+\.enable_fullscreen_all_apps=)""0""", "$1\"1\"");
                        if (nt != text) { File.WriteAllText(e.Conf, nt, new UTF8Encoding(false)); n++; }
                    }
                    catch { }
                }
                log(n > 0 ? "[OK] FIX: BlueStacks me game ab full screen me khulega" : "[OK] BlueStacks full screen setting ON hai");
                Todo.Add("Game hamesha full screen (F11) me khelo - window mode me delay aata hai");
                return;
            }
            foreach (var p in players)
            {
                IntPtr h = p.MainWindowHandle;
                if (IsIconic(h)) ShowWindow(h, 9);
                if (IsFull(h)) { log("[OK] BlueStacks full screen me hai"); continue; }
                SetForegroundWindow(h); Thread.Sleep(250);
                keybd_event(0x7A, 0, 0, UIntPtr.Zero); keybd_event(0x7A, 0, 2, UIntPtr.Zero);   // F11
                Thread.Sleep(900);
                if (IsFull(h)) log("[OK] FIX: BlueStacks window mode me tha -> full screen kar diya");
                else { log("BlueStacks full screen nahi ho paya"); Todo.Add("BlueStacks me F11 dabao (full screen)"); }
            }
        }

        // ===================== 4. FPS > monitor Hz = jhatke =====================
        public static void Fps(Action<string> log)
        {
            int hz = Tweaks.MonitorHz(); int f = Tweaks.TargetFps;
            if (hz < 50) { log("[OK] FPS limit " + f); return; }
            if (f > hz)
            {
                if (Tweaks.BlueStacksRunning)
                {
                    log("FPS " + f + " hai par monitor " + hz + " Hz - isse jhatke aate hain");
                    Todo.Add("BlueStacks band karke LAG FIX dobara chalao - FPS " + hz + " pe set ho jayega");
                    return;
                }
                Settings.FpsLimit = hz;
                SetFpsInConf(hz);
                log("[OK] FIX: FPS " + f + " tha par monitor " + hz + " Hz ka hai - extra frame jhatka dete hain. Ab FPS " + hz);
            }
            else log("[OK] FPS " + f + " - monitor " + hz + " Hz ke saath theek");
        }

        public static int SetFpsInConf(int fps)
        {
            if (Tweaks.BlueStacksRunning) return 0;
            int n = 0;
            foreach (var e in Emulators.Installed())
            {
                try
                {
                    if (!File.Exists(e.Conf)) continue;
                    string text = File.ReadAllText(e.Conf);
                    string nt = Regex.Replace(text, @"(bst\.instance\.[^.\r\n]+\.max_fps=)""\d+""", "$1\"" + fps + "\"");
                    if (nt != text) { File.WriteAllText(e.Conf, nt, new UTF8Encoding(false)); n++; }
                }
                catch { }
            }
            return n;
        }

        // ===================== 5. Background apps =====================
        private static readonly string[] Ignore = { "Idle", "System", "HD-Player", "HD-Agent", "BstkSVC", "BlueStacksServices", "BlueStacksHelper", "dwm", "audiodg", "csrss", "NabaGameBooster", "explorer", "MsMpEng", "Memory Compression", "Registry", "svchost" };

        public static void Background(Action<string> log)
        {
            var before = new Dictionary<int, TimeSpan>();
            foreach (var p in Process.GetProcesses()) { try { before[p.Id] = p.TotalProcessorTime; } catch { } }
            Thread.Sleep(1500);
            var hogs = new Dictionary<string, double>();
            double cores = Environment.ProcessorCount;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    TimeSpan b; if (!before.TryGetValue(p.Id, out b)) continue;
                    if (Ignore.Any(x => p.ProcessName.Equals(x, StringComparison.OrdinalIgnoreCase))) continue;
                    double pct = (p.TotalProcessorTime - b).TotalMilliseconds / 1500.0 / cores * 100;
                    if (pct < 0.5) continue;
                    double v; hogs.TryGetValue(p.ProcessName, out v); hogs[p.ProcessName] = v + pct;
                }
                catch { }
            }
            var top = hogs.Where(k => k.Value >= 6).OrderByDescending(k => k.Value).Take(3).ToList();
            if (top.Count == 0) { log("[OK] Background me koi bhari app nahi"); return; }
            string list = string.Join(", ", top.Select(k => k.Key + " " + (int)k.Value + "%"));
            if (GameMode.IsOn) log("[OK] Bhari apps: " + list + " - Watchdog inhe dheema rakhega");
            else
            {
                log("Background me CPU kha rahe: " + list);
                Todo.Add("Khelte waqt GAME MODE START karo (ye apps pause ho jayengi): " + string.Join(", ", top.Select(k => k.Key)));
            }
        }

        // ===================== 6. Internet =====================
        public static void Network(Action<string> log)
        {
            bool wifi = false, eth = false;
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up) continue;
                    if (!n.GetIPProperties().GatewayAddresses.Any(g => g.Address != null && !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any))) continue;
                    if (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) wifi = true;
                    else if (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet || n.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet) eth = true;
                }
            }
            catch { }

            var times = new List<long>(); int lost = 0;
            using (var ping = new Ping())
                for (int i = 0; i < 12; i++)
                {
                    try { var r = ping.Send("8.8.8.8", 1000); if (r.Status == IPStatus.Success) times.Add(r.RoundtripTime); else lost++; }
                    catch { lost++; }
                    Thread.Sleep(120);
                }
            string conn = eth ? "LAN cable" : wifi ? "Wi-Fi" : "Internet";
            if (times.Count == 0) { log(conn + ": ping nahi mila (internet band ya block)"); return; }
            double avg = times.Average(); double jit = 0;
            for (int i = 1; i < times.Count; i++) jit += Math.Abs(times[i] - times[i - 1]);
            jit = times.Count > 1 ? jit / (times.Count - 1) : 0;
            string res = string.Format("{0}: ping {1:0} ms, jitter {2:0} ms, loss {3}/12", conn, avg, jit, lost);
            bool bad = avg > 90 || jit > 15 || lost > 0;
            log((bad ? "" : "[OK] ") + res + (bad ? " - isse game me lag / teleport hota hai" : " - achha hai"));

            if (wifi && !eth)
            {
                Tweaks.Run("powercfg", "/setacvalueindex SCHEME_CURRENT 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0");
                Tweaks.Run("powercfg", "/setdcvalueindex SCHEME_CURRENT 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0");
                Tweaks.Run("powercfg", "/setactive SCHEME_CURRENT");
                log("[OK] FIX: Wi-Fi power saving band (lag spike kam)");
                if (bad) Todo.Add("Wi-Fi ki jagah LAN cable lagao - ping / jitter sabse zyada isi se sudhrega");
            }
            else if (bad) Todo.Add("Khelte waqt ghar me YouTube / download band rakho - ping " + (int)avg + " ms, jitter " + (int)jit + " ms aa raha hai");
        }

        // ===================== 7. NVIDIA =====================
        public static void Nvidia(Action<string> log)
        {
            string gpu = Tweaks.NvidiaGpuName();
            if (gpu == null) { log("[OK] NVIDIA nahi - skip"); return; }
            string d; var st = Status.Nvidia(gpu, out d);
            if (st == FeatureState.On) { log("[OK] NVIDIA low-latency settings lagi hain"); return; }
            Tweaks.NvidiaApply(log);
        }

        // ===================== 8. Fast Mode =====================
        public static void FastMode(Action<string> log)
        {
            string d; var st = Booster.FastModeState(out d);
            if (st == FeatureState.On) log("[OK] Fast Mode chalu (Hyper-V band)");
            else if (st == FeatureState.Pending) { log("Fast Mode laga hai par PC restart baaki"); Todo.Add("PC restart karo - tabhi Fast Mode lagega"); }
            else if (st == FeatureState.NotAvailable) log("[OK] Fast Mode - is PC pe nahi");
            else { log("Fast Mode band hai - BlueStacks Hyper-V pe dheema chalta hai"); Todo.Add("OPTIMIZE me \"BlueStacks Fast Mode\" ON karke PC restart karo"); }
        }
    }
}
