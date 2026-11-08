using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NabaGameBooster
{
    /// <summary>
    /// Game Mode ON  = kaam wale apps PAUSE (andar ka kaam jaisa ka taisa) + unki RAM khali,
    ///                 sync apps (Google Drive / OneDrive / Epic) BAND, BlueStacks HIGH priority.
    /// Game Mode OFF = paused apps wahin se CHALU, band hue apps wapas khulte hain.
    /// </summary>
    internal static class GameMode
    {
        // PAUSE honge - project, tabs, video, chat sab waise hi rahenge.
        private static readonly HashSet<string> PauseList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Editing
            "Adobe Premiere Pro", "Animate", "Photoshop", "Illustrator", "AfterFX", "Audition", "Adobe Media Encoder",
            "CEPHtmlEngine", "Adobe CEF Helper", "Creative Cloud UI Helper", "CCXProcess", "CCLibrary",
            // Browsers
            "chrome", "brave", "msedge", "firefox", "opera", "opera_gx", "vivaldi",
            // Baaki apps
            "Telegram", "WhatsApp", "Codex", "Spotify", "Notepad++", "Audacity", "Copilot", "Grok", "ms-teams", "Teams", "fdm"
        };

        // BAND honge (inme user ka koi kaam nahi hota). Inhe kabhi PAUSE nahi karna -
        // Google Drive / OneDrive pause hone par poora Windows atak sakta hai.
        private static readonly HashSet<string> CloseList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GoogleDriveFS", "OneDrive", "EpicGamesLauncher", "EpicWebHelper"
        };

        // AI desktop apps - setting ON ho tabhi pause
        private static readonly HashSet<string> AiApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Claude", "ChatGPT"
        };

        private static readonly HashSet<string> NoReopen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EpicWebHelper"
        };

        // Game ke time rukne wali Windows services (STOP pe wapas chalu)
        private static readonly string[] BgServices = { "wuauserv", "UsoSvc", "BITS", "DoSvc", "WSearch", "SysMain", "DiagTrack" };

        private static string StateFile
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NabaGameBooster");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "gamemode.txt");
            }
        }

        public static bool IsOn { get { return File.Exists(StateFile); } }

        public static int PausedCount
        {
            get
            {
                try { return IsOn ? File.ReadAllLines(StateFile).Count(l => l.StartsWith("P|")) : 0; }
                catch { return 0; }
            }
        }

        // State file format (har line):
        //   P|pid|startTicks|naam     -> pause hua process
        //   L|launchId               -> band hua app, OFF pe kholna hai

        public static void On(Action<string> log)
        {
            if (IsOn) { log("Game Mode pehle se ON hai. Pehle OFF karo."); return; }

            double before = Native.FreeRamGB();
            int me = Process.GetCurrentProcess().Id;
            var lines = new List<string>();
            var procs = Process.GetProcesses();

            // 1) Sync / launcher apps band karo (pehle yaad rakho kaise kholna hai)
            var toClose = procs.Where(p => CloseList.Contains(p.ProcessName)).ToList();
            foreach (var g in toClose.GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                if (NoReopen.Contains(g.Key)) continue;
                string path = g.Select(Native.ProcessPath).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                if (path != null) lines.Add("L|" + path);
            }

            // 2) Kaam wale apps PAUSE + RAM khali
            bool ai = Settings.PauseAiApps;
            var toPause = procs.Where(p => p.Id != me && (PauseList.Contains(p.ProcessName) || (ai && AiApps.Contains(p.ProcessName)))).ToList();
            var pausedNames = new SortedDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in toPause)
            {
                long ticks;
                try { ticks = p.StartTime.ToUniversalTime().Ticks; } catch { continue; }
                long ws = 0;
                try { ws = p.WorkingSet64; } catch { }
                if (Native.Suspend(p.Id))
                {
                    Native.TrimMemory(p.Id);
                    lines.Add("P|" + p.Id + "|" + ticks + "|" + p.ProcessName);
                    double mb; pausedNames.TryGetValue(p.ProcessName, out mb);
                    pausedNames[p.ProcessName] = mb + ws / 1048576.0;
                }
            }

            // State pehle likho, phir band karo - taaki kuch bhi ho, OFF hamesha chal sake
            var stopServices = new List<string>();
            if (Settings.PauseServices)
            {
                foreach (var name in BgServices)
                {
                    try
                    {
                        using (var sc = new System.ServiceProcess.ServiceController(name))
                            if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running && sc.CanStop) { stopServices.Add(name); lines.Add("S|" + name); }
                    }
                    catch { }
                }
            }
            File.WriteAllLines(StateFile, lines);

            var stopped = new List<string>();
            foreach (var name in stopServices)
            {
                try
                {
                    using (var sc = new System.ServiceProcess.ServiceController(name))
                    {
                        sc.Stop();
                        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(8));
                        stopped.Add(ServiceLabel(name));
                    }
                }
                catch { }
            }
            if (stopped.Count > 0) log("RUKI Windows services: " + string.Join(", ", stopped));

            foreach (var p in toClose)
            {
                try { p.CloseMainWindow(); } catch { }
            }
            Thread.Sleep(1500);
            foreach (var p in Process.GetProcesses().Where(p => CloseList.Contains(p.ProcessName)))
            {
                try { p.Kill(); } catch { }
            }

            if (pausedNames.Count > 0)
            {
                log("PAUSE hue (kaam jaisa ka taisa rahega):");
                foreach (var kv in pausedNames) log(string.Format("   {0,-26} {1,6:N0} MB", kv.Key, kv.Value));
            }
            var closedNames = toClose.Select(p => p.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (closedNames.Count > 0) log("BAND hue (OFF pe wapas khulenge): " + string.Join(", ", closedNames));
            if (pausedNames.Count == 0 && closedNames.Count == 0) log("Koi background app khula nahi tha - RAM pehle se free hai.");

            SetBlueStacksPriority(ProcessPriorityClass.High);

            if (Settings.TimerRes)
            {
                double ms = Native.SetTimerResolution(true);
                log(string.Format("Timer: {0:0.###} ms", ms));
            }
            if (Settings.RamCleaner)
            {
                double got = Booster.CleanNow();
                log(string.Format("RAM Cleaner: cache saaf (+{0:N1} GB)", Math.Max(0, got)));
            }

            Thread.Sleep(1000);
            log(string.Format("GAME MODE ON!  Khali RAM: {0:N1} GB -> {1:N1} GB.  BlueStacks = HIGH priority.", before, Native.FreeRamGB()));
        }

        public static void Off(Action<string> log)
        {
            // BlueStacks Turbo ON ho to High hi rehne do
            SetBlueStacksPriority(Booster.TurboPriorityOn ? ProcessPriorityClass.High : ProcessPriorityClass.Normal);
            Native.SetTimerResolution(false);
            if (!IsOn) { log("Game Mode ON nahi tha."); return; }

            var lines = File.ReadAllLines(StateFile);
            int resumed = 0;
            var resumedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) Paused apps chalu
            foreach (var l in lines.Where(x => x.StartsWith("P|")))
            {
                var f = l.Split('|');
                int pid; long ticks;
                if (f.Length < 4 || !int.TryParse(f[1], out pid) || !long.TryParse(f[2], out ticks)) continue;
                if (!SameProcess(pid, ticks)) continue;   // process khatam ho chuka / PID kisi aur ka
                if (Native.Resume(pid)) { resumed++; resumedNames.Add(f[3]); }
            }
            if (resumed > 0) log("CHALU hue (wahin se jahan ruke the): " + string.Join(", ", resumedNames.OrderBy(x => x)));

            // 1b) Windows services wapas chalu
            var started = new List<string>();
            foreach (var l in lines.Where(x => x.StartsWith("S|")))
            {
                string name = l.Substring(2);
                try
                {
                    using (var sc = new System.ServiceProcess.ServiceController(name))
                    {
                        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped) { sc.Start(); started.Add(ServiceLabel(name)); }
                    }
                }
                catch { }
            }
            if (started.Count > 0) log("Windows services wapas chalu: " + string.Join(", ", started));

            // 2) Band hue apps wapas kholo (normal user mode me, explorer ke through)
            var running = new HashSet<string>(Process.GetProcesses().Select(p => p.ProcessName), StringComparer.OrdinalIgnoreCase);
            foreach (var l in lines.Where(x => x.StartsWith("L|")))
            {
                string path = l.Substring(2);
                if (!File.Exists(path)) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (running.Contains(name)) continue;
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = false });
                    log("   Wapas khula: " + name);
                }
                catch (Exception ex) { log("   " + name + " nahi khula: " + ex.Message); }
                Thread.Sleep(600);
            }

            try { File.Delete(StateFile); } catch { }
            log("GAME MODE OFF!  Sab apps wapas chalu.");
        }

        /// <summary>App band ho rahi ho ya crash ke baad khule - paused apps kabhi atke na rahen.</summary>
        public static void SafetyResume(Action<string> log)
        {
            if (!IsOn) return;
            Off(log);
        }

        /// <summary>Restart ke baad purani state bekaar hai (services khud chalu, paused apps khatam) - file hata do.</summary>
        public static void CleanStaleState()
        {
            if (!IsOn) return;
            try
            {
                var boot = DateTime.Now - TimeSpan.FromMilliseconds((uint)Environment.TickCount);
                if (File.GetLastWriteTime(StateFile) < boot) File.Delete(StateFile);
            }
            catch { }
        }

        private static string ServiceLabel(string n)
        {
            switch (n.ToLowerInvariant())
            {
                case "wuauserv": return "Windows Update";
                case "usosvc": return "Update Orchestrator";
                case "bits": return "BITS";
                case "dosvc": return "Delivery Optimization";
                case "wsearch": return "Windows Search";
                case "sysmain": return "SysMain";
                case "diagtrack": return "Telemetry";
                default: return n;
            }
        }

        private static bool SameProcess(int pid, long startTicksUtc)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                return Math.Abs(p.StartTime.ToUniversalTime().Ticks - startTicksUtc) < TimeSpan.TicksPerSecond;
            }
            catch { return false; }
        }

        private static void SetBlueStacksPriority(ProcessPriorityClass pc)
        {
            foreach (var p in Process.GetProcesses())
            {
                if (!p.ProcessName.StartsWith("HD-Player", StringComparison.OrdinalIgnoreCase)) continue;
                try { p.PriorityClass = pc; } catch { }
            }
        }
    }
}
