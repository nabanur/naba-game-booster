using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NabaGameBooster
{
    /// <summary>Ek-baar wali gaming settings. Har function log() me batata hai kya hua.</summary>
    internal static class Tweaks
    {
        // ---------------- helpers ----------------
        private static string Run(string exe, string args, int timeoutMs = 60000)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(timeoutMs);
                return o;
            }
        }

        private static void SetDword(RegistryKey root, string path, string name, int value)
        {
            using (var k = root.CreateSubKey(path)) { k.SetValue(name, value, RegistryValueKind.DWord); }
        }

        private static void SetString(RegistryKey root, string path, string name, string value)
        {
            using (var k = root.CreateSubKey(path)) { k.SetValue(name, value, RegistryValueKind.String); }
        }

        public static bool IsRunning(params string[] names)
        {
            return Process.GetProcesses().Any(p => names.Any(n => p.ProcessName.Equals(n, StringComparison.OrdinalIgnoreCase)));
        }

        // ---------------- Gaming Optimize ----------------
        public static void GamingOptimize(Action<string> log)
        {
            // 1) Power plan: Ultimate Performance (na mile to High Performance)
            string list = Run("powercfg", "/list");
            var m = Regex.Match(list, @"([0-9a-fA-F\-]{36})\s+\(Ultimate Performance\)");
            string guid = m.Success ? m.Groups[1].Value : null;
            if (guid == null)
            {
                // Pehle banaya hua plan yaad hai? (Windows dusri bhasha me ho to naam alag hota hai)
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\NabaGameBooster"))
                {
                    var saved = k.GetValue("UltimatePlan") as string;
                    if (saved != null && list.IndexOf(saved, StringComparison.OrdinalIgnoreCase) >= 0) guid = saved;
                }
            }
            if (guid == null)
            {
                var d = Regex.Match(Run("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61"), @"([0-9a-fA-F\-]{36})");
                if (d.Success)
                {
                    guid = d.Groups[1].Value;
                    SetString(Registry.CurrentUser, @"Software\NabaGameBooster", "UltimatePlan", guid);
                }
            }
            if (guid != null) { Run("powercfg", "-setactive " + guid); log("[OK] Ultimate Performance power plan ON"); }
            else { Run("powercfg", "-setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"); log("[OK] High Performance power plan ON"); }

            // 2) Windows Game Mode ON
            SetDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
            SetDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
            log("[OK] Windows Game Mode ON");

            // 3) Xbox background recording OFF
            SetDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
            SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
            log("[OK] Xbox background recording OFF");

            // 4) Hardware-accelerated GPU scheduling
            SetDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
            log("[OK] GPU scheduling ON (PC restart ke baad lagega)");
            log("Gaming Optimize ho gaya. Ek baar PC restart karna.");
        }

        public static void GamingOptimizeUndo(Action<string> log)
        {
            Run("powercfg", "-setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
            log("[OK] Power plan: Balanced (Windows default)");
            SetDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1);
            log("[OK] Xbox background recording Windows default pe");
            SetDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1);
            log("[OK] GPU scheduling OFF (restart ke baad)");
            log("Windows Game Mode chalu hi rakha hai (Windows ka default).");
        }

        // ---------------- BlueStacks ----------------
        public const string BlueStacksConf = @"C:\ProgramData\BlueStacks_nxt\bluestacks.conf";

        public static bool BlueStacksInstalled { get { return File.Exists(BlueStacksConf); } }

        public static bool BlueStacksRunning
        {
            get { return Process.GetProcesses().Any(p => p.ProcessName.StartsWith("HD-", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("BlueStacks", StringComparison.OrdinalIgnoreCase)); }
        }

        public static void BlueStacksPlan(out int ramMb, out int cpus, out int pcRamGb, out int threads)
        {
            pcRamGb = (int)Math.Round(Native.TotalRamGB());
            threads = Environment.ProcessorCount;
            ramMb = pcRamGb >= 15 ? 6144 : pcRamGb >= 11 ? 4096 : 3072;
            cpus = threads >= 8 ? 4 : 2;
        }

        public static void BlueStacksOptimize(Action<string> log)
        {
            int ram, cpus, pcRam, threads;
            BlueStacksPlan(out ram, out cpus, out pcRam, out threads);

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string bak = Path.Combine(docs, "bluestacks.conf.backup_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            File.Copy(BlueStacksConf, bak, true);

            string t = File.ReadAllText(BlueStacksConf);
            t = Regex.Replace(t, @"(bst\.instance\.[^.\r\n]+\.ram=)""\d+""", "${1}\"" + ram + "\"");
            t = Regex.Replace(t, @"(bst\.instance\.[^.\r\n]+\.cpus=)""\d+""", "${1}\"" + cpus + "\"");
            t = Regex.Replace(t, @"(bst\.instance\.[^.\r\n]+\.max_fps=)""\d+""", "${1}\"120\"");
            t = Regex.Replace(t, @"(bst\.instance\.[^.\r\n]+\.enable_vsync=)""\d""", "${1}\"0\"");
            File.WriteAllText(BlueStacksConf, t, new UTF8Encoding(false));

            log(string.Format("[OK] BlueStacks: RAM {0} GB, CPU {1} cores, Max FPS 120, VSync OFF", ram / 1024, cpus));
            log("     Purani setting ka backup: " + bak);
        }

        public static string LatestBlueStacksBackup()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            try
            {
                return Directory.GetFiles(docs, "bluestacks.conf.backup_*").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
            }
            catch { return null; }
        }

        public static void BlueStacksRestore(Action<string> log)
        {
            string bak = LatestBlueStacksBackup();
            if (bak == null) { log("Purani BlueStacks setting ka backup nahi mila."); return; }
            File.Copy(bak, BlueStacksConf, true);
            log("[OK] BlueStacks ki purani setting wapas lagi: " + Path.GetFileName(bak));
        }

        // ---------------- Defender ----------------
        public static void DefenderExclude(Action<string> log)
        {
            var paths = new[] { @"C:\ProgramData\BlueStacks_nxt", @"C:\Program Files\BlueStacks_nxt" }.Where(Directory.Exists).ToList();
            if (paths.Count == 0) { log("BlueStacks 5 ke folder nahi mile - kuch nahi badla."); return; }
            string cmd = "Add-MpPreference -ExclusionPath " + string.Join(",", paths.Select(p => "'" + p + "'")) +
                         "; Add-MpPreference -ExclusionProcess 'HD-Player.exe'";
            string o = Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + cmd + "\"");
            if (!string.IsNullOrWhiteSpace(o) && o.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                log("Defender ne mana kiya: " + o.Trim());
            else
                log("[OK] Defender exclusion laga: " + string.Join(", ", paths) + ", HD-Player.exe");
        }

        public static void DefenderRemove(Action<string> log)
        {
            string cmd = "Remove-MpPreference -ExclusionPath 'C:\\ProgramData\\BlueStacks_nxt','C:\\Program Files\\BlueStacks_nxt'; Remove-MpPreference -ExclusionProcess 'HD-Player.exe'";
            Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + cmd + "\"");
            log("[OK] Defender exclusion hata diya - BlueStacks ab scan hoga");
        }

        // ---------------- Mouse ----------------
        public static void MouseRaw(Action<string> log)
        {
            const string k = @"Control Panel\Mouse";
            SetString(Registry.CurrentUser, k, "MouseSpeed", "0");
            SetString(Registry.CurrentUser, k, "MouseThreshold1", "0");
            SetString(Registry.CurrentUser, k, "MouseThreshold2", "0");
            SetString(Registry.CurrentUser, k, "MouseSensitivity", "10");
            Native.SetMouseAcceleration(0, 0, 0);
            Native.SetMouseSpeed(10);
            log("[OK] Mouse 1:1 - acceleration OFF, speed 10/20 (turant laga)");
        }

        public static void MouseDefault(Action<string> log)
        {
            const string k = @"Control Panel\Mouse";
            SetString(Registry.CurrentUser, k, "MouseSpeed", "1");
            SetString(Registry.CurrentUser, k, "MouseThreshold1", "6");
            SetString(Registry.CurrentUser, k, "MouseThreshold2", "10");
            SetString(Registry.CurrentUser, k, "MouseSensitivity", "10");
            Native.SetMouseAcceleration(6, 10, 1);
            Native.SetMouseSpeed(10);
            log("[OK] Mouse Windows default pe wapas");
        }

        // ---------------- Memory Integrity ----------------
        public static void MemoryIntegrity(bool on, Action<string> log)
        {
            SetDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", on ? 1 : 0);
            log(on ? "[OK] Memory Integrity ON - PC restart karna." : "[OK] Memory Integrity OFF - PC restart karna.");
        }

        // ---------------- NVIDIA ----------------
        public static string NvidiaGpuName()
        {
            try
            {
                using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (cls == null) return null;
                    foreach (var sub in cls.GetSubKeyNames())
                    {
                        if (!Regex.IsMatch(sub, @"^\d{4}$")) continue;
                        using (var k = cls.OpenSubKey(sub))
                        {
                            var desc = k == null ? null : k.GetValue("DriverDesc") as string;
                            if (desc != null && desc.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) return desc;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static void NvidiaApply(Action<string> log)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string tool = Path.Combine(dir, @"tools\nvidiaProfileInspector\nvidiaProfileInspector.exe");
            string nip = Path.Combine(dir, @"tools\naba.nip");
            if (!File.Exists(tool) || !File.Exists(nip))
            {
                log("NVIDIA tool app ke saath nahi mila. App dobara install karo.");
                return;
            }
            var psi = new ProcessStartInfo(tool, "-silentImport \"" + nip + "\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(tool) };
            using (var p = Process.Start(psi))
            {
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } log("NVIDIA tool time pe khatam nahi hua."); return; }
            }
            SetDword(Registry.CurrentUser, @"Software\NabaGameBooster", "NvidiaApplied", 1);
            log("[OK] NVIDIA: Power management = Prefer maximum performance");
            log("[OK] NVIDIA: Shader Cache Size = Unlimited");
        }
    }
}
