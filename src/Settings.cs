using System;
using Microsoft.Win32;

namespace NabaGameBooster
{
    /// <summary>App ki apni settings (HKCU\Software\NabaGameBooster).</summary>
    internal static class Settings
    {
        private const string Key = @"Software\NabaGameBooster";

        public static bool Get(string name, bool def)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Key))
                {
                    var v = k == null ? null : k.GetValue(name);
                    return v == null ? def : Convert.ToInt32(v) == 1;
                }
            }
            catch { return def; }
        }

        public static void Set(string name, bool value)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord); }
            catch { }
        }

        public static int GetInt(string name, int def)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Key))
                {
                    var v = k == null ? null : k.GetValue(name);
                    return v == null ? def : Convert.ToInt32(v);
                }
            }
            catch { return def; }
        }

        public static void SetInt(string name, int value)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue(name, value, RegistryValueKind.DWord); }
            catch { }
        }

        public static bool AutoUpdate { get { return Get("AutoUpdate", true); } set { Set("AutoUpdate", value); } }
        public static bool PauseAiApps { get { return Get("PauseAiApps", true); } set { Set("PauseAiApps", value); } }
        public static bool RamCleaner { get { return Get("RamCleaner", false); } set { Set("RamCleaner", value); } }
        public static bool PauseServices { get { return Get("PauseServices", true); } set { Set("PauseServices", value); } }
        /// <summary>BlueStacks/MSI FPS limit (0 = abhi chuna nahi).</summary>
        public static int FpsLimit { get { return GetInt("FpsLimit", 0); } set { SetInt("FpsLimit", value); } }
        public static bool TimerRes { get { return Get("TimerRes", false); } set { Set("TimerRes", value); } }
    }
}
