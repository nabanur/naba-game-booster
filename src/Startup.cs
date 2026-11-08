using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace NabaGameBooster
{
    /// <summary>
    /// PC chalu hote hi khulne wale apps. Band/chalu karne ka tarika wahi hai jo Task Manager ka
    /// "Startup apps" tab use karta hai (StartupApproved) - isliye poori tarah reversible.
    /// </summary>
    internal class StartupItem
    {
        public string Name;
        public string Command;
        public string Source;
        public RegistryKey ApprovedRoot;
        public string ApprovedSub;   // Run / Run32 / StartupFolder
        public string ValueName;
        public bool Enabled;
        public bool Protected;
    }

    internal static class StartupApps
    {
        private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        private static readonly string[] ProtectedNames = { "SecurityHealth", "Windows Security", "WindowsDefender" };

        public static List<StartupItem> List()
        {
            var list = new List<StartupItem>();
            ReadRun(list, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run", "User");
            ReadRun(list, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run", "Sab users");
            ReadRun(list, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32", "Sab users");
            ReadFolder(list, Registry.CurrentUser, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder");
            ReadFolder(list, Registry.LocalMachine, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (sab)");
            return list.OrderBy(i => i.Protected).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void ReadRun(List<StartupItem> list, RegistryKey root, string path, string approvedSub, string source)
        {
            try
            {
                using (var k = root.OpenSubKey(path))
                {
                    if (k == null) return;
                    foreach (var name in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        var cmd = k.GetValue(name) as string ?? "";
                        list.Add(Make(name, cmd, source, root, approvedSub, name));
                    }
                }
            }
            catch { }
        }

        private static void ReadFolder(List<StartupItem> list, RegistryKey root, string folder, string source)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
                foreach (var f in Directory.GetFiles(folder))
                {
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    list.Add(Make(Path.GetFileNameWithoutExtension(f), f, source, root, "StartupFolder", fn));
                }
            }
            catch { }
        }

        private static StartupItem Make(string name, string cmd, string source, RegistryKey root, string sub, string valueName)
        {
            var item = new StartupItem
            {
                Name = name, Command = cmd, Source = source, ApprovedRoot = root, ApprovedSub = sub, ValueName = valueName,
                Protected = ProtectedNames.Any(p => name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
            };
            item.Enabled = true;
            try
            {
                using (var k = root.OpenSubKey(ApprovedBase + sub))
                {
                    var b = k == null ? null : k.GetValue(valueName) as byte[];
                    if (b != null && b.Length > 0) item.Enabled = (b[0] & 1) == 0;
                }
            }
            catch { }
            return item;
        }

        public static bool SetEnabled(StartupItem item, bool enabled)
        {
            if (item.Protected) return false;
            try
            {
                var data = new byte[12];
                data[0] = enabled ? (byte)2 : (byte)3;
                if (!enabled)
                {
                    var ft = BitConverter.GetBytes(DateTime.Now.ToFileTime());
                    Array.Copy(ft, 0, data, 4, 8);
                }
                using (var k = item.ApprovedRoot.CreateSubKey(ApprovedBase + item.ApprovedSub))
                    k.SetValue(item.ValueName, data, RegistryValueKind.Binary);
                item.Enabled = enabled;
                return true;
            }
            catch { return false; }
        }
    }
}
