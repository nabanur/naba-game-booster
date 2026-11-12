using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NabaGameBooster
{
    /// <summary>
    /// Jhatka Recorder: Game Mode ON rehte har second CPU / RAM / disk / ping / bhari app note karta hai.
    /// Jhatke ke pal pakadta hai aur STOP par report deta hai: asli wajah + fix.
    /// Raw data: %LOCALAPPDATA%\NabaGameBooster\jhatka\  (CSV + report)
    /// </summary>
    internal static class Jhatka
    {
        [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        private class Ev { public DateTime T; public string Kind; public string Info; }

        private static Thread worker, pinger;
        private static volatile bool running;
        private static readonly object gate = new object();
        private static readonly List<Ev> events = new List<Ev>();
        private static readonly Dictionary<string, int> hogCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<int> pings = new List<int>();
        private static DateTime started;
        private static int seconds, pingLost;
        private static double minRam = 999, maxCpu;
        private static int lastPing = -1;
        private static StreamWriter csv;
        public static string LastReport;
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NabaGameBooster", "jhatka"); } }

        private static readonly string[] Skip = { "Idle", "System", "NabaGameBooster", "Memory Compression", "Registry" };
        private static bool IsEmu(string n)
        {
            return n.StartsWith("HD-", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Bstk", StringComparison.OrdinalIgnoreCase) || n.StartsWith("BlueStacks", StringComparison.OrdinalIgnoreCase);
        }

        public static void Start()
        {
            lock (gate)
            {
                if (running) return;
                events.Clear(); hogCount.Clear(); pings.Clear();
                seconds = 0; pingLost = 0; minRam = 999; maxCpu = 0; lastPing = -1; LastReport = null;
                started = DateTime.Now;
                try
                {
                    Directory.CreateDirectory(Folder);
                    foreach (var old in new DirectoryInfo(Folder).GetFiles().OrderByDescending(f => f.LastWriteTime).Skip(20)) try { old.Delete(); } catch { }
                    csv = new StreamWriter(Path.Combine(Folder, "data-" + started.ToString("yyyyMMdd-HHmmss") + ".csv"), false, new UTF8Encoding(false));
                    csv.WriteLine("time,cpu_total,cpu_bluestacks,ram_free_gb,disk_busy,ping_ms,top_app,top_app_cpu");
                }
                catch { csv = null; }
                running = true;
                worker = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.BelowNormal }; worker.Start();
                pinger = new Thread(PingLoop) { IsBackground = true, Priority = ThreadPriority.BelowNormal }; pinger.Start();
            }
        }

        public static string Stop()
        {
            lock (gate)
            {
                if (!running) return LastReport;
                running = false;
            }
            try { if (worker != null) worker.Join(2500); } catch { }
            try { if (pinger != null) pinger.Join(1500); } catch { }
            lock (gate)
            {
                try { if (csv != null) { csv.Flush(); csv.Dispose(); } } catch { }
                csv = null;
                LastReport = BuildReport();
                try { File.WriteAllText(Path.Combine(Folder, "report-" + started.ToString("yyyyMMdd-HHmmss") + ".txt"), LastReport, new UTF8Encoding(false)); } catch { }
                return LastReport;
            }
        }

        private static void PingLoop()
        {
            try { PingInner(); } catch { }
        }

        private static void PingInner()
        {
            using (var p = new Ping())
                while (running)
                {
                    var t0 = DateTime.UtcNow;
                    try
                    {
                        var r = p.Send("8.8.8.8", 1000);
                        lock (gate) { if (r.Status == IPStatus.Success) { lastPing = (int)r.RoundtripTime; pings.Add(lastPing); } else { lastPing = 9999; pingLost++; } }
                    }
                    catch { lock (gate) { lastPing = -1; } }
                    int wait = 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds;
                    if (wait > 0) Thread.Sleep(wait);
                }
        }

        private static void Loop()
        {
            try { LoopInner(); } catch { }
        }

        private static void LoopInner()
        {
            long pi, pk, pu; GetSystemTimes(out pi, out pk, out pu);
            var lastProc = new Dictionary<int, TimeSpan>();
            DateTime lastProcT = DateTime.UtcNow;
            string topName = ""; double topPct = 0, emuPct = 0;
            PerformanceCounter disk = null;
            try { disk = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total"); disk.NextValue(); } catch { disk = null; }
            double cores = Environment.ProcessorCount;
            int tick = 0;
            var lastSeen = new Dictionary<string, DateTime>();

            while (running)
            {
                Thread.Sleep(1000);
                if (!running) break;
                tick++;
                long i, k, u; GetSystemTimes(out i, out k, out u);
                long tot = (k - pk) + (u - pu), idl = i - pi; pi = i; pk = k; pu = u;
                double cpu = tot > 0 ? Math.Max(0, Math.Min(100, 100.0 * (tot - idl) / tot)) : 0;
                double ram = Native.FreeRamGB();
                double busy = -1;
                try { if (disk != null) busy = Math.Max(0, 100 - disk.NextValue()); } catch { }

                // har 2 second: kaun si app kitna CPU
                if (tick % 2 == 0)
                {
                    var now = DateTime.UtcNow; double el = (now - lastProcT).TotalMilliseconds; lastProcT = now;
                    var cur = new Dictionary<int, TimeSpan>();
                    var by = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    double emu = 0;
                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            var tp = p.TotalProcessorTime; cur[p.Id] = tp;
                            TimeSpan b; if (!lastProc.TryGetValue(p.Id, out b) || el <= 0) continue;
                            double pct = (tp - b).TotalMilliseconds / el / cores * 100;
                            if (pct < 0.3) continue;
                            if (IsEmu(p.ProcessName)) { emu += pct; continue; }
                            if (Skip.Contains(p.ProcessName)) continue;
                            double v; by.TryGetValue(p.ProcessName, out v); by[p.ProcessName] = v + pct;
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                    lastProc = cur; emuPct = emu;
                    if (by.Count > 0) { var top = by.OrderByDescending(x => x.Value).First(); topName = top.Key; topPct = top.Value; }
                    else { topName = ""; topPct = 0; }
                }

                int ping; lock (gate) ping = lastPing;
                lock (gate)
                {
                    seconds++;
                    if (ram > 0.01 && ram < minRam) minRam = ram;
                    if (cpu > maxCpu) maxCpu = cpu;
                    try { if (csv != null) csv.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:HH:mm:ss},{1:0},{2:0},{3:0.0},{4:0},{5},{6},{7:0}", DateTime.Now, cpu, emuPct, ram, busy, ping, topName, topPct)); if (seconds % 15 == 0) csv.Flush(); } catch { }

                    // ---- jhatke ke pal ----
                    var found = new List<Ev>();
                    if (topPct >= 12 && topName != "") { found.Add(new Ev { Kind = "app", Info = topName + " " + (int)topPct + "% CPU" }); int c; hogCount.TryGetValue(topName, out c); hogCount[topName] = c + 1; }
                    if (cpu >= 92) found.Add(new Ev { Kind = "cpu", Info = "CPU " + (int)cpu + "% (BlueStacks " + (int)emuPct + "%" + (topName != "" && topPct >= 5 ? ", " + topName + " " + (int)topPct + "%" : "") + ")" });
                    if (ram > 0.01 && ram < 1.0) found.Add(new Ev { Kind = "ram", Info = string.Format("RAM sirf {0:0.0} GB khali", ram) });
                    if (busy >= 90) found.Add(new Ev { Kind = "disk", Info = "Disk " + (int)busy + "% busy" + (topName != "" ? " (" + topName + ")" : "") });
                    int med = Median();
                    if (pings.Count < 5) { }
                    else if (ping == 9999) found.Add(new Ev { Kind = "net", Info = "Internet packet loss" });
                    else if (ping > 0 && med > 0 && ping > Math.Max(150, med * 3)) found.Add(new Ev { Kind = "net", Info = "Ping " + med + " -> " + ping + " ms" });

                    foreach (var f in found)
                    {
                        f.T = DateTime.Now;
                        // ek hi wajah lagatar ho to har second alag mat likho
                        DateTime prev;
                        bool cont = lastSeen.TryGetValue(f.Kind, out prev) && (f.T - prev).TotalSeconds < 3.5;
                        lastSeen[f.Kind] = f.T;
                        if (cont) continue;
                        if (events.Count < 400) events.Add(f);
                    }
                }
            }
            try { if (disk != null) disk.Dispose(); } catch { }
        }

        private static int Median()
        {
            if (pings.Count < 5) return 0;
            var s = pings.Skip(Math.Max(0, pings.Count - 60)).OrderBy(x => x).ToList();
            return s[s.Count / 2];
        }

        private static string BuildReport()
        {
            var sb = new StringBuilder();
            var dur = TimeSpan.FromSeconds(seconds);
            sb.AppendLine("JHATKA REPORT  •  " + started.ToString("dd MMM, hh:mm tt") + "  •  " + (int)dur.TotalMinutes + " min " + dur.Seconds + " sec");
            sb.AppendLine();
            if (seconds < 20) { sb.AppendLine("Game Mode bahut kam der chala - ek poora match khelke STOP karo."); return sb.ToString(); }

            int cpuN = events.Count(e => e.Kind == "cpu"), ramN = events.Count(e => e.Kind == "ram"), diskN = events.Count(e => e.Kind == "disk"), netN = events.Count(e => e.Kind == "net"), appN = events.Count(e => e.Kind == "app");
            int med = Median(); int worst = pings.Count > 0 ? pings.Max() : 0;
            sb.AppendLine(string.Format("CPU max {0:0}%  •  sabse kam khali RAM {1:0.0} GB  •  ping aam {2} ms, sabse zyada {3} ms, loss {4}", maxCpu, minRam < 999 ? minRam : Native.FreeRamGB(), med, worst, pingLost));
            sb.AppendLine();

            var fixes = new List<string>();
            sb.AppendLine("KYA PAKDA GAYA:");
            bool any = false;
            if (appN > 0)
            {
                any = true;
                var tops = hogCount.OrderByDescending(x => x.Value).Take(4).ToList();
                sb.AppendLine("•  Background app ne CPU khaya: " + string.Join(", ", tops.Select(x => x.Key + " (" + x.Value + " sec)")));
                fixes.Add("Khelte waqt ye apps band rakho: " + string.Join(", ", tops.Select(x => x.Key)) + ". Agar ye roz ki app hai to batao, Game Mode me pause list me daal dunga.");
            }
            if (cpuN > 0)
            {
                any = true;
                sb.AppendLine("•  CPU poora bhara: " + cpuN + " baar");
                if (appN == 0) fixes.Add("CPU khud game/BlueStacks se bhar raha hai (fight me effects). Free Fire graphics Smooth/Standard karo, aur BlueStacks resolution 1600x900 try karo.");
            }
            if (ramN > 0) { any = true; sb.AppendLine("•  RAM khatam hone ke kareeb: " + ramN + " baar"); fixes.Add("RAM kam pad rahi: Chrome / Premiere band karke khelo, ya BlueStacks RAM 4 GB karo."); }
            if (diskN > 0) { any = true; sb.AppendLine("•  Disk 100%: " + diskN + " baar"); fixes.Add("Disk busy ho raha (Windows Update / antivirus scan / download). Game Mode me \"Windows Update / Search band\" ON rakho."); }
            if (pings.Count == 0) sb.AppendLine("•  (Internet ping test nahi ho paya - is network pe ping block hai)");
            if (netN > 0) { any = true; sb.AppendLine("•  Internet spike / loss: " + netN + " baar (ping " + med + " -> " + worst + " ms)"); fixes.Add("Jhatka internet ka hai, PC ka nahi: LAN cable lagao, khelte waqt ghar me YouTube/download band."); }
            if (!any)
            {
                sb.AppendLine("•  PC side kuch nahi pakda: CPU, RAM, disk, internet sab theek rahe.");
                fixes.Add("Jo jhatka laga woh Free Fire ka apna load tha (naye effect / skin pehli baar load). Graphics Smooth karo; pehli 1-2 fight ke baad kam ho jata hai.");
            }

            sb.AppendLine();
            sb.AppendLine("FIX:");
            int n = 1; foreach (var f in fixes) sb.AppendLine(n++ + ". " + f);

            if (events.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("JHATKE KE PAL (time ke saath):");
                foreach (var e in events.Where(x => x.Kind != "app" || events.Count < 40).Take(40)) sb.AppendLine(e.T.ToString("HH:mm:ss") + "  " + e.Info);
                if (events.Count > 40) sb.AppendLine("... aur " + (events.Count - 40) + " (poori list: " + Folder + ")");
            }
            return sb.ToString();
        }
    }
}
