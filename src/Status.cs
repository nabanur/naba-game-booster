using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NabaGameBooster
{
    internal enum FeatureState { Off, On, Partial, Pending, NotAvailable }

    /// <summary>Har setting abhi PC pe lagi hai ya nahi - yeh pata karta hai.</summary>
    internal static class Status
    {
        public static string Run(string exe, string args, int timeoutMs = 30000)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return o;
                }
            }
            catch { return ""; }
        }

        public static object Reg(RegistryKey root, string path, string name)
        {
            try { using (var k = root.OpenSubKey(path)) { return k == null ? null : k.GetValue(name); } }
            catch { return null; }
        }

        private static int? RegInt(RegistryKey root, string path, string name)
        {
            var v = Reg(root, path, name);
            if (v == null) return null;
            try { return Convert.ToInt32(v); } catch { return null; }
        }

        private static string RegStr(RegistryKey root, string path, string name)
        {
            var v = Reg(root, path, name);
            return v == null ? null : v.ToString();
        }

        // ---------------- Power plan ----------------
        public static string ActivePowerPlan(out string guid)
        {
            guid = null;
            var m = Regex.Match(Run("powercfg", "/getactivescheme"), @"([0-9a-fA-F\-]{36})\s+\((.+?)\)");
            if (!m.Success) return "?";
            guid = m.Groups[1].Value.ToLowerInvariant();
            return m.Groups[2].Value.Trim();
        }

        private static bool IsPerformancePlan(string guid, string name)
        {
            if (guid == null) return false;
            string saved = RegStr(Registry.CurrentUser, @"Software\NabaGameBooster", "UltimatePlan");
            return guid == "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
                || guid == "e9a42b02-d5df-448d-aa00-03f14749eb61"
                || (saved != null && guid.Equals(saved, StringComparison.OrdinalIgnoreCase))
                || name.IndexOf("Ultimate", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("High performance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---------------- Features ----------------
        public static FeatureState GamingOptimize(out string detail)
        {
            string guid; string plan = ActivePowerPlan(out guid);
            bool power = IsPerformancePlan(guid, plan);
            int? gm = RegInt(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled");
            bool gameMode = gm == null || gm == 1;
            bool dvrOff = RegInt(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled") == 0;
            bool hags = RegInt(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode") == 2;
            int n = (power ? 1 : 0) + (gameMode ? 1 : 0) + (dvrOff ? 1 : 0) + (hags ? 1 : 0);
            detail = "Power plan: " + plan;
            if (n == 4) return FeatureState.On;
            if (n <= 1 && !power) return FeatureState.Off;
            var miss = new System.Collections.Generic.List<string>();
            if (!power) miss.Add("power plan");
            if (!gameMode) miss.Add("Windows Game Mode");
            if (!dvrOff) miss.Add("Xbox recording");
            if (!hags) miss.Add("GPU scheduling");
            detail = "Baaki: " + string.Join(", ", miss);
            return FeatureState.Partial;
        }

        public static FeatureState BlueStacks(out string detail)
        {
            detail = "";
            if (!File.Exists(Tweaks.BlueStacksConf)) { detail = "BlueStacks 5 install nahi hai"; return FeatureState.NotAvailable; }
            int ram, cpus, pcRam, threads;
            Tweaks.BlueStacksPlan(out ram, out cpus, out pcRam, out threads);
            string t;
            try { t = File.ReadAllText(Tweaks.BlueStacksConf); } catch { detail = "Setting padh nahi paya"; return FeatureState.Off; }
            var r = Regex.Match(t, @"bst\.instance\.[^.\r\n]+\.ram=""(\d+)""");
            var f = Regex.Match(t, @"bst\.instance\.[^.\r\n]+\.max_fps=""(\d+)""");
            var v = Regex.Match(t, @"bst\.instance\.[^.\r\n]+\.enable_vsync=""(\d)""");
            string curRam = r.Success ? r.Groups[1].Value : "?";
            string curFps = f.Success ? f.Groups[1].Value : "?";
            detail = string.Format("Abhi: RAM {0} MB, FPS {1}", curRam, curFps);
            bool ok = curRam == ram.ToString() && curFps == "120" && (!v.Success || v.Groups[1].Value == "0");
            return ok ? FeatureState.On : FeatureState.Off;
        }

        public static FeatureState Defender(out string detail)
        {
            detail = "";
            if (!Directory.Exists(@"C:\ProgramData\BlueStacks_nxt")) { detail = "BlueStacks 5 install nahi hai"; return FeatureState.NotAvailable; }
            string o = Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-MpPreference).ExclusionPath -join '|'\"", 20000);
            bool has = o.IndexOf(@"BlueStacks_nxt", StringComparison.OrdinalIgnoreCase) >= 0;
            detail = has ? "BlueStacks scan se bahar hai" : "Defender BlueStacks ko scan karta hai";
            return has ? FeatureState.On : FeatureState.Off;
        }

        public static FeatureState Nvidia(string gpuName, out string detail)
        {
            if (gpuName == null) { detail = "NVIDIA card nahi mila"; return FeatureState.NotAvailable; }
            bool applied = RegInt(Registry.CurrentUser, @"Software\NabaGameBooster", "NvidiaApplied") == 1;
            detail = gpuName;
            return applied ? FeatureState.On : FeatureState.Off;
        }

        public static FeatureState Mouse(out string detail)
        {
            const string k = @"Control Panel\Mouse";
            bool raw = RegStr(Registry.CurrentUser, k, "MouseSpeed") == "0"
                    && RegStr(Registry.CurrentUser, k, "MouseThreshold1") == "0"
                    && RegStr(Registry.CurrentUser, k, "MouseThreshold2") == "0";
            string sens = RegStr(Registry.CurrentUser, k, "MouseSensitivity") ?? "10";
            detail = raw ? "Acceleration OFF, speed " + sens : "Windows acceleration chalu hai";
            return raw ? FeatureState.On : FeatureState.Off;
        }

        /// <summary>Feature "Memory Integrity band" ka state: On = band hai (gaming ke liye achha).</summary>
        public static FeatureState MemoryIntegrityDisabled(out string detail)
        {
            int? reg = RegInt(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
            bool running = HvciRunning();
            bool wantOff = reg == 0;
            bool wantOn = reg == 1;
            if (!running && (reg == null || wantOff)) { detail = "Memory Integrity band hai"; return FeatureState.On; }
            if (running && wantOff) { detail = "PC restart ke baad band hoga"; return FeatureState.Pending; }
            if (!running && wantOn) { detail = "PC restart ke baad chalu hoga"; return FeatureState.Pending; }
            detail = "Memory Integrity chalu hai (security)";
            return FeatureState.Off;
        }

        private static bool HvciRunning()
        {
            try
            {
                using (var s = new ManagementObjectSearcher(@"root\Microsoft\Windows\DeviceGuard", "SELECT SecurityServicesRunning FROM Win32_DeviceGuard"))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        var arr = mo["SecurityServicesRunning"] as uint[];
                        if (arr != null && arr.Contains(2u)) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool BlueStacksRunning()
        {
            return Process.GetProcesses().Any(p => p.ProcessName.StartsWith("HD-Player", StringComparison.OrdinalIgnoreCase));
        }
    }
}
