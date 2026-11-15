using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace NabaGameBooster
{
    /// <summary>BlueStacks ko band / chalu karna aur uski conf ki ek-ek setting padhna / likhna (Setting Test ke liye).</summary>
    internal static class BsControl
    {
        public static Emu Emu
        {
            get
            {
                var all = Emulators.Installed();
                return all.FirstOrDefault(e => e.Key == "BlueStacks_nxt") ?? all.FirstOrDefault();
            }
        }

        private static string AppCache(Emu e, string inst) { return Path.Combine(e.DataDir, "Engine", inst, "AppCache"); }

        /// <summary>Free Fire wala instance (jaise Pie64).</summary>
        public static string Instance(Emu e)
        {
            var names = new List<string>();
            try
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(e.Conf), @"bst\.instance\.([A-Za-z0-9_]+)\.graphics_renderer="))
                    if (!names.Contains(m.Groups[1].Value)) names.Add(m.Groups[1].Value);
            }
            catch { }
            foreach (var n in names)
                if (Package(e, n) != null) return n;
            if (names.Contains("Pie64")) return "Pie64";
            return names.FirstOrDefault() ?? "Pie64";
        }

        public static string Package(Emu e, string inst)
        {
            foreach (var p in new[] { "com.dts.freefiremax", "com.dts.freefireth" })
                if (File.Exists(Path.Combine(AppCache(e, inst), p + ".ico"))) return p;
            return null;
        }

        public static string Get(Emu e, string inst, string key)
        {
            try
            {
                var m = Regex.Match(File.ReadAllText(e.Conf), @"bst\.instance\." + Regex.Escape(inst) + @"\." + Regex.Escape(key) + @"=""([^""]*)""");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch { return null; }
        }

        /// <summary>Sirf tab likho jab BlueStacks band ho (warna band hote waqt woh apni purani setting likh deta hai).</summary>
        public static bool Set(Emu e, string inst, Dictionary<string, string> values)
        {
            if (values == null || values.Count == 0) return true;
            if (Running) return false;
            try
            {
                string text = File.ReadAllText(e.Conf);
                string nl = text.Contains("\r\n") ? "\r\n" : "\n";
                foreach (var kv in values)
                {
                    var rx = new Regex(@"(bst\.instance\." + Regex.Escape(inst) + @"\." + Regex.Escape(kv.Key) + @"=)""[^""]*""");
                    if (rx.IsMatch(text)) text = rx.Replace(text, "${1}\"" + kv.Value + "\"", 1);
                    else text = text.TrimEnd('\r', '\n') + nl + "bst.instance." + inst + "." + kv.Key + "=\"" + kv.Value + "\"" + nl;
                }
                File.WriteAllText(e.Conf, text, new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        public static bool Running { get { return Process.GetProcessesByName("HD-Player").Length > 0; } }

        /// <summary>BlueStacks ko aaram se band karo, 15 sec me na ho to zabardasti.</summary>
        public static void Close()
        {
            foreach (var p in Process.GetProcessesByName("HD-Player")) { try { p.CloseMainWindow(); } catch { } }
            for (int i = 0; i < 30 && Running; i++) Thread.Sleep(500);
            foreach (var p in Process.GetProcessesByName("HD-Player")) { try { p.Kill(); } catch { } }
            for (int i = 0; i < 10 && Running; i++) Thread.Sleep(500);
            Thread.Sleep(2500);   // conf likhna khatam hone do
        }

        public static void Launch(Emu e, string inst)
        {
            string pkg = Package(e, inst);
            string args = "--instance " + inst + (pkg != null ? " --cmd launchApp --package \"" + pkg + "\"" : "");
            try { Process.Start(new ProcessStartInfo(e.Player, args) { UseShellExecute = true, WorkingDirectory = e.ProgDir }); } catch { }
        }
    }
}
