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
    /// Game Mode ON  = background apps band, list yaad rakho, BlueStacks HIGH priority.
    /// Game Mode OFF = wahi apps wapas kholo (normal user mode me), priority normal.
    /// </summary>
    internal static class GameMode
    {
        // Editing apps: sirf "pyaar se" band (save ka poochenge). Kabhi zabardasti nahi.
        private static readonly HashSet<string> Editors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Adobe Premiere Pro", "Animate", "Photoshop", "Illustrator", "AfterFX", "Audition", "Adobe Media Encoder"
        };

        // Baaki background apps: pehle pyaar se, na maane to zabardasti band.
        private static readonly HashSet<string> CloseList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Browsers
            "chrome", "brave", "msedge", "firefox", "opera", "opera_gx", "vivaldi",
            // Apps
            "Telegram", "WhatsApp", "Codex", "OneDrive", "GoogleDriveFS", "EpicGamesLauncher", "EpicWebHelper",
            "fdm", "Copilot", "Notepad++", "Audacity", "ms-teams", "Teams", "Grok", "Spotify",
            // Adobe background
            "Creative Cloud", "Creative Cloud Helper", "Creative Cloud UI Helper", "CCXProcess", "CCLibrary", "CoreSync",
            "Adobe Desktop Service", "AdobeIPCBroker", "AdobeCollabSync", "CEPHtmlEngine", "AdobeNotificationClient", "Adobe CEF Helper"
        };

        // OFF pe inhe dobara nahi kholna (Adobe app khulte hi yeh khud chalu hote hain).
        private static readonly HashSet<string> NoReopen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Creative Cloud", "Creative Cloud Helper", "Creative Cloud UI Helper", "CCXProcess", "CCLibrary", "CoreSync",
            "Adobe Desktop Service", "AdobeIPCBroker", "AdobeCollabSync", "CEPHtmlEngine", "AdobeNotificationClient",
            "Adobe CEF Helper", "EpicWebHelper"
        };

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

        public static void On(Action<string> log)
        {
            double before = Native.FreeRamGB();
            var targets = Process.GetProcesses()
                .Where(p => CloseList.Contains(p.ProcessName) || Editors.Contains(p.ProcessName))
                .ToList();

            if (targets.Count == 0)
            {
                log("Koi background app khula nahi tha - RAM pehle se free hai.");
            }
            else
            {
                // 1) Yaad rakho kya-kya khula tha
                var ids = new List<string>();
                if (File.Exists(StateFile))
                    ids.AddRange(File.ReadAllLines(StateFile).Where(l => !string.IsNullOrWhiteSpace(l)));

                log("Yeh apps band ho rahe hain:");
                foreach (var g in targets.GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase))
                {
                    long mb = 0;
                    foreach (var p in g) { try { mb += p.WorkingSet64; } catch { } }
                    log(string.Format("   {0,-26} {1,6:N0} MB", g.Key, mb / 1048576.0));

                    if (NoReopen.Contains(g.Key)) continue;
                    var main = g.OrderBy(p => SafeHandle(p) == IntPtr.Zero ? 1 : 0).First();
                    string id = LaunchId(main);
                    if (id != null && !ids.Contains(id, StringComparer.OrdinalIgnoreCase)) ids.Add(id);
                }
                File.WriteAllLines(StateFile, ids);

                // 2) Editing apps ko pyaar se band karo
                foreach (var p in Process.GetProcesses().Where(p => Editors.Contains(p.ProcessName)))
                    TryCloseWindow(p);

                // 3) Baaki apps: 3 baar pyaar se, phir zabardasti
                for (int i = 0; i < 3; i++)
                {
                    foreach (var p in Process.GetProcesses().Where(p => CloseList.Contains(p.ProcessName)))
                        if (SafeHandle(p) != IntPtr.Zero) TryCloseWindow(p);
                    Thread.Sleep(2000);
                }
                foreach (var p in Process.GetProcesses().Where(p => CloseList.Contains(p.ProcessName)))
                {
                    try { p.Kill(); } catch { }
                }

                // 4) Editing apps ko 20 second (save wala sawaal ho to jawab de do)
                for (int t = 0; t < 20 && Process.GetProcesses().Any(p => Editors.Contains(p.ProcessName)); t += 2)
                    Thread.Sleep(2000);
                var left = Process.GetProcesses().Where(p => Editors.Contains(p.ProcessName))
                    .Select(p => p.ProcessName).Distinct().ToList();
                if (left.Count > 0)
                    log("Yeh band nahi hue (unsaved kaam ho sakta hai): " + string.Join(", ", left) + " - save karke khud band karo.");

                // State file hamesha bane, taaki OFF chal sake
                if (!File.Exists(StateFile)) File.WriteAllText(StateFile, "");
            }

            SetBlueStacksPriority(ProcessPriorityClass.High);
            if (!File.Exists(StateFile)) File.WriteAllText(StateFile, "");
            Thread.Sleep(1500);
            log(string.Format("GAME MODE ON!  Khali RAM: {0:N1} GB -> {1:N1} GB.  BlueStacks = HIGH priority.", before, Native.FreeRamGB()));
        }

        public static void Off(Action<string> log)
        {
            SetBlueStacksPriority(ProcessPriorityClass.Normal);
            if (!File.Exists(StateFile))
            {
                log("Game Mode ON nahi tha - kholne ke liye kuch nahi.");
                return;
            }

            var ids = File.ReadAllLines(StateFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var runningNames = new HashSet<string>(Process.GetProcesses().Select(p => p.ProcessName), StringComparer.OrdinalIgnoreCase);
            if (ids.Count > 0) log("Yeh apps wapas khul rahe hain:");
            foreach (var id in ids)
            {
                string label;
                if (id.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    label = Regex.Replace(id, @"^shell:AppsFolder\\", "");
                    label = label.Split('_')[0];
                }
                else
                {
                    if (!File.Exists(id)) continue;
                    label = Path.GetFileNameWithoutExtension(id);
                    if (runningNames.Contains(label)) continue;
                }
                // explorer se kholo taaki app normal (bina admin) mode me chale
                try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + id + "\"") { UseShellExecute = false }); log("   " + label); }
                catch (Exception ex) { log("   " + label + " nahi khula: " + ex.Message); }
                Thread.Sleep(800);
            }
            try { File.Delete(StateFile); } catch { }
            log("GAME MODE OFF!  Sab apps wapas khul gaye.");
        }

        private static void SetBlueStacksPriority(ProcessPriorityClass pc)
        {
            foreach (var p in Process.GetProcesses())
            {
                if (!p.ProcessName.StartsWith("HD-Player", StringComparison.OrdinalIgnoreCase)) continue;
                try { p.PriorityClass = pc; } catch { }
            }
        }

        private static IntPtr SafeHandle(Process p)
        {
            try { return p.MainWindowHandle; } catch { return IntPtr.Zero; }
        }

        private static void TryCloseWindow(Process p)
        {
            try { p.CloseMainWindow(); } catch { }
        }

        /// <summary>App ko dobara kholne ka tarika: exe ka path, ya Store app ke liye shell:AppsFolder ID.</summary>
        private static string LaunchId(Process p)
        {
            string path = Native.ProcessPath(p);
            if (string.IsNullOrEmpty(path)) return null;

            int wi = path.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);
            if (wi >= 0)
            {
                // ...\WindowsApps\Name_Version_Arch__PublisherId\...
                string rest = path.Substring(wi + @"\WindowsApps\".Length);
                string folder = rest.Split('\\')[0];
                string[] parts = folder.Split('_');
                if (parts.Length < 2) return null;
                string pfn = parts[0] + "_" + parts[parts.Length - 1];
                string appId = "App";
                try
                {
                    string manifest = Path.Combine(path.Substring(0, wi + @"\WindowsApps\".Length) + folder, "AppxManifest.xml");
                    if (File.Exists(manifest))
                    {
                        var m = Regex.Match(File.ReadAllText(manifest), @"<Application\b[^>]*?\sId=""([^""]+)""");
                        if (m.Success) appId = m.Groups[1].Value;
                    }
                }
                catch { }
                return @"shell:AppsFolder\" + pfn + "!" + appId;
            }

            // Codex ke andar ke chhote helper (codex.exe) dobara nahi kholne
            if (Path.GetFileName(path).Equals("codex.exe", StringComparison.OrdinalIgnoreCase)) return null;
            return path;
        }
    }
}
