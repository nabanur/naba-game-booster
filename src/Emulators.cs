using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace NabaGameBooster
{
    /// <summary>BlueStacks engine wale emulator: BlueStacks 5 aur MSI App Player.</summary>
    internal class Emu
    {
        public string Key;      // BlueStacks_nxt / BlueStacks_msi5
        public string Name;     // dikhane ka naam
        public string DataDir;  // C:\ProgramData\BlueStacks_nxt
        public string ProgDir;  // C:\Program Files\BlueStacks_nxt
        public string Conf { get { return Path.Combine(DataDir, "bluestacks.conf"); } }
        public string Player { get { return Path.Combine(ProgDir, "HD-Player.exe"); } }
        public string Short { get { return Key == "BlueStacks_msi5" ? "MSI" : "BlueStacks"; } }
    }

    internal static class Emulators
    {
        private static readonly string[][] Known =
        {
            new[] { "BlueStacks_nxt", "BlueStacks 5" },
            new[] { "BlueStacks_msi5", "MSI App Player" }
        };

        public static List<Emu> Installed()
        {
            var list = new List<Emu>();
            foreach (var k in Known)
            {
                string key = k[0];
                string data = null, prog = null;
                try
                {
                    using (var r = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\" + key))
                    {
                        if (r != null)
                        {
                            var ud = r.GetValue("UserDefinedDir") as string;
                            var dd = r.GetValue("DataDir") as string;
                            var id = r.GetValue("InstallDir") as string;
                            if (!string.IsNullOrEmpty(ud) && File.Exists(Path.Combine(ud, "bluestacks.conf"))) data = ud;
                            else if (!string.IsNullOrEmpty(dd))
                            {
                                var parent = Directory.GetParent(dd.TrimEnd('\\'));
                                if (parent != null && File.Exists(Path.Combine(parent.FullName, "bluestacks.conf"))) data = parent.FullName;
                            }
                            if (!string.IsNullOrEmpty(id)) prog = id.TrimEnd('\\');
                        }
                    }
                }
                catch { }
                if (data == null) data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), key);
                if (prog == null || !File.Exists(Path.Combine(prog, "HD-Player.exe")))
                {
                    string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    string c1 = Path.Combine(pf, key);
                    string c2 = Path.Combine(@"C:\Program Files (x86)", key);
                    prog = File.Exists(Path.Combine(c1, "HD-Player.exe")) ? c1 : File.Exists(Path.Combine(c2, "HD-Player.exe")) ? c2 : c1;
                }
                if (File.Exists(Path.Combine(data, "bluestacks.conf")))
                    list.Add(new Emu { Key = key, Name = k[1], DataDir = data.TrimEnd('\\'), ProgDir = prog });
            }
            return list;
        }

        public static bool AnyInstalled { get { return Installed().Count > 0; } }

        public static bool AnyRunning
        {
            get
            {
                return Process.GetProcesses().Any(p =>
                    p.ProcessName.StartsWith("HD-", StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Equals("BlueStacks", StringComparison.OrdinalIgnoreCase));
            }
        }

        public static string Names(IEnumerable<Emu> emus)
        {
            return string.Join(" + ", emus.Select(e => e.Short));
        }
    }
}
