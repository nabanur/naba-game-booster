using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NabaGameBooster
{
    /// <summary>
    /// Game Mode ke time har 3 second background programs ka CPU dekhta hai. Koi achanak CPU khaane lage
    /// to uski priority "Below normal" kar deta hai (band nahi karta). Game Mode STOP pe sab wapas.
    /// </summary>
    internal static class Watchdog
    {
        private static System.Threading.Timer timer;
        private static Action<string> log;
        private static readonly Dictionary<int, TimeSpan> lastCpu = new Dictionary<int, TimeSpan>();
        private static readonly Dictionary<int, Tuple<DateTime, ProcessPriorityClass, string>> lowered = new Dictionary<int, Tuple<DateTime, ProcessPriorityClass, string>>();
        private static DateTime lastTick;
        private static readonly object gate = new object();

        // Inhe kabhi nahi chhoona (Windows, game, sound, recording, antivirus)
        private static readonly HashSet<string> Exempt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost", "dwm",
            "audiodg", "explorer", "fontdrvhost", "ctfmon", "conhost", "sihost", "taskhostw", "RuntimeBroker",
            "MsMpEng", "NisSrv", "SecurityHealthService", "MpDefenderCoreService",
            "NabaGameBooster", "obs64", "obs32", "Discord", "nvcontainer", "NVDisplay.Container", "nvsphelper64"
        };

        public static bool Running { get { return timer != null; } }

        public static void Start(Action<string> logger)
        {
            lock (gate)
            {
                if (timer != null) return;
                log = logger;
                lastCpu.Clear();
                lastTick = DateTime.UtcNow;
                Sample(false);
                timer = new System.Threading.Timer(_ => { try { Sample(true); } catch { } }, null, 3000, 3000);
            }
            logger("Game Watchdog ON - CPU khaane wale background programs pe nazar.");
        }

        public static void Stop(Action<string> logger)
        {
            lock (gate)
            {
                if (timer != null) { timer.Dispose(); timer = null; }
                int restored = 0;
                foreach (var kv in lowered)
                {
                    try
                    {
                        var p = Process.GetProcessById(kv.Key);
                        if (Math.Abs((p.StartTime - kv.Value.Item1).TotalSeconds) < 1) { p.PriorityClass = kv.Value.Item2; restored++; }
                    }
                    catch { }
                }
                lowered.Clear();
                lastCpu.Clear();
                if (restored > 0 && logger != null) logger("Watchdog: " + restored + " program ki priority wapas normal.");
            }
        }

        private static bool IsExempt(Process p)
        {
            string n = p.ProcessName;
            if (Exempt.Contains(n)) return true;
            if (n.StartsWith("HD-", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Bstk", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("BlueStacks", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Sample(bool act)
        {
            lock (gate)
            {
                if (timer == null && act) return;
                var now = DateTime.UtcNow;
                double wall = (now - lastTick).TotalMilliseconds * Environment.ProcessorCount;
                lastTick = now;
                var seen = new HashSet<int>();
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.Id == 0 || p.Id == 4 || IsExempt(p)) continue;
                        TimeSpan cpu;
                        try { cpu = p.TotalProcessorTime; } catch { continue; }
                        seen.Add(p.Id);
                        TimeSpan prev;
                        if (lastCpu.TryGetValue(p.Id, out prev) && act && wall > 0 && !lowered.ContainsKey(p.Id))
                        {
                            double pct = (cpu - prev).TotalMilliseconds / wall * 100.0;
                            if (pct >= 8.0)
                            {
                                var old = p.PriorityClass;
                                if (old == ProcessPriorityClass.Normal || old == ProcessPriorityClass.AboveNormal || old == ProcessPriorityClass.High)
                                {
                                    p.PriorityClass = ProcessPriorityClass.BelowNormal;
                                    lowered[p.Id] = Tuple.Create(p.StartTime, old, p.ProcessName);
                                    if (log != null) log(string.Format("Watchdog: {0} CPU {1:N0}% kha raha tha - priority kam ki", p.ProcessName, pct));
                                }
                            }
                        }
                        lastCpu[p.Id] = cpu;
                    }
                    catch { }
                }
                foreach (var dead in lastCpu.Keys.Where(k => !seen.Contains(k)).ToList()) lastCpu.Remove(dead);
            }
        }
    }
}
