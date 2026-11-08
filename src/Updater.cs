using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NabaGameBooster
{
    /// <summary>GitHub Releases se naya version check aur install karta hai.</summary>
    internal static class Updater
    {
        public const string Owner = "nabanur";
        public const string Repo = "naba-game-booster";

        public static Version Current
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public static string CurrentText
        {
            get { var v = Current; return v.Major + "." + v.Minor + "." + v.Build; }
        }

        public class UpdateInfo
        {
            public Version Version;
            public string VersionText;
            public string SetupUrl;
            public string Notes;
        }

        private static WebClient Client()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "NabaGameBooster/" + CurrentText;
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        /// <summary>Naya version ho to info deta hai, warna null.</summary>
        public static UpdateInfo Check()
        {
            using (var wc = Client())
            {
                string json = wc.DownloadString("https://api.github.com/repos/" + Owner + "/" + Repo + "/releases/latest");
                var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+){1,3})\"");
                var url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+Setup[^\"]*\\.exe)\"", RegexOptions.IgnoreCase);
                if (!tag.Success || !url.Success) return null;

                var latest = new Version(tag.Groups[1].Value);
                var cur = new Version(Current.Major, Current.Minor, Math.Max(0, Current.Build));
                var lat = new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build));
                if (lat <= cur) return null;

                var body = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                string notes = body.Success ? Regex.Unescape(body.Groups[1].Value) : "";
                return new UpdateInfo { Version = lat, VersionText = tag.Groups[1].Value, SetupUrl = url.Groups[1].Value, Notes = notes };
            }
        }

        /// <summary>Setup download karke silent install chalata hai. App ko iske baad band hona hai.</summary>
        public static void DownloadAndRun(UpdateInfo info, Action<int> progress)
        {
            string file = Path.Combine(Path.GetTempPath(), "NabaGameBooster-Setup-" + info.VersionText + ".exe");
            using (var wc = Client())
            {
                var done = new System.Threading.ManualResetEvent(false);
                Exception err = null;
                wc.DownloadProgressChanged += (s, e) => { if (progress != null) progress(e.ProgressPercentage); };
                wc.DownloadFileCompleted += (s, e) => { err = e.Error; done.Set(); };
                wc.DownloadFileAsync(new Uri(info.SetupUrl), file);
                done.WaitOne();
                if (err != null) throw err;
            }
            Process.Start(new ProcessStartInfo(file, "/SILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
        }
    }
}
