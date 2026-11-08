using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace NabaGameBooster
{
    /// <summary>v1.2 ke naye boost: BlueStacks Turbo, Timer 0.5ms, RAM Cleaner.</summary>
    internal static class Booster
    {
        // ---------------- BlueStacks / MSI Turbo ----------------
        private static List<string> Players()
        {
            return Emulators.Installed().Select(e => e.Player).Where(File.Exists).ToList();
        }

        private const string Ifeo = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\HD-Player.exe\PerfOptions";
        private const string GpuPref = @"Software\Microsoft\DirectX\UserGpuPreferences";
        private const string Layers = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        public static bool TurboPriorityOn
        {
            get { return Convert.ToInt32(Status.Reg(Registry.LocalMachine, Ifeo, "CpuPriorityClass") ?? 0) == 3; }
        }

        public static FeatureState TurboState(out string detail)
        {
            var players = Players();
            if (players.Count == 0) { detail = "BlueStacks / MSI install nahi hai"; return FeatureState.NotAvailable; }
            int need = 1 + players.Count * 2, got = TurboPriorityOn ? 1 : 0;
            foreach (var exe in players)
            {
                if (((Status.Reg(Registry.CurrentUser, GpuPref, exe) as string) ?? "").Contains("GpuPreference=2")) got++;
                if (((Status.Reg(Registry.CurrentUser, Layers, exe) as string) ?? "").Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE")) got++;
            }
            detail = "High priority, best GPU, fullscreen fix";
            if (got == need) return FeatureState.On;
            if (got == 0) return FeatureState.Off;
            detail = "Kuch hissa laga hai - switch dabao poora lagane ke liye";
            return FeatureState.Partial;
        }

        public static void TurboOn(Action<string> log)
        {
            var players = Players();
            if (players.Count == 0) { log("BlueStacks / MSI nahi mila."); return; }
            using (var k = Registry.LocalMachine.CreateSubKey(Ifeo)) k.SetValue("CpuPriorityClass", 3, RegistryValueKind.DWord);
            log("[OK] Emulator hamesha HIGH priority pe chalega");
            foreach (var exe in players)
            {
                using (var k = Registry.CurrentUser.CreateSubKey(GpuPref)) k.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String);
                using (var k = Registry.CurrentUser.CreateSubKey(Layers)) k.SetValue(exe, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE", RegistryValueKind.String);
            }
            log("[OK] High performance GPU + Fullscreen optimization OFF");
            log("Emulator ek baar band karke dobara kholna - tab lagega.");
        }

        public static void TurboOff(Action<string> log)
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\HD-Player.exe", false); } catch { }
            foreach (var exe in Players())
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(GpuPref, true)) if (k != null) k.DeleteValue(exe, false); } catch { }
                try { using (var k = Registry.CurrentUser.OpenSubKey(Layers, true)) if (k != null) k.DeleteValue(exe, false); } catch { }
            }
            log("[OK] Turbo hata diya (Windows default)");
        }

        // ---------------- Timer 0.5ms ----------------
        private const string KernelKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";

        public static FeatureState TimerState(out string detail)
        {
            bool reg = Convert.ToInt32(Status.Reg(Registry.LocalMachine, KernelKey, "GlobalTimerResolutionRequests") ?? 0) == 1;
            bool on = Settings.TimerRes;
            double cur = Native.CurrentTimerMs();
            bool win10 = Environment.OSVersion.Version.Build < 22000;
            detail = (win10 ? "Windows 10 pe asar kam  •  " : "") + (on ? string.Format("Game Mode me 0.5ms  •  abhi {0:0.###} ms", cur) : string.Format("Windows default  •  abhi {0:0.###} ms", cur));
            if (on && reg) return FeatureState.On;
            if (on || reg) return FeatureState.Partial;
            return FeatureState.Off;
        }

        public static void TimerOn(Action<string> log)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(KernelKey)) k.SetValue("GlobalTimerResolutionRequests", 1, RegistryValueKind.DWord);
            Settings.TimerRes = true;
            log("[OK] Timer 0.5ms ON - Game Mode START pe lagega.");
            log("     Windows 11 pe poora asar ek baar PC restart ke baad.");
        }

        public static void TimerOff(Action<string> log)
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(KernelKey, true)) if (k != null) k.DeleteValue("GlobalTimerResolutionRequests", false); } catch { }
            Settings.TimerRes = false;
            Native.SetTimerResolution(false);
            log("[OK] Timer Windows default pe wapas");
        }

        // ---------------- RAM Cleaner ----------------
        public static FeatureState RamCleanerState(out string detail)
        {
            bool on = Settings.RamCleaner;
            detail = on ? "Game Mode me har 5 min cache RAM saaf" : "Band - switch dabao";
            return on ? FeatureState.On : FeatureState.Off;
        }

        public static void RamCleanerOn(Action<string> log)
        {
            Settings.RamCleaner = true;
            log("[OK] RAM Cleaner ON - Game Mode START pe aur har 5 minute chalega.");
        }

        public static void RamCleanerOff(Action<string> log)
        {
            Settings.RamCleaner = false;
            log("[OK] RAM Cleaner band.");
        }

        /// <summary>Abhi RAM saaf karo. Kitni GB khali hui wo batata hai.</summary>
        public static double CleanNow()
        {
            double before = Native.FreeRamGB();
            Native.PurgeStandbyList();
            System.Threading.Thread.Sleep(400);
            return Native.FreeRamGB() - before;
        }
    }
}
