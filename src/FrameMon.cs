using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace NabaGameBooster
{
    /// <summary>
    /// Intel PresentMon (MIT) se BlueStacks (HD-Player.exe) ka har ek frame naapta hai - asli FPS aur jhatke.
    /// Har frame ke saath wall-clock time bhi rakhta hai taaki jhatke ke pal CPU / GPU / ping se mila sakein.
    /// </summary>
    internal class FrameMon
    {
        public static string ToolPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"tools\PresentMon\PresentMon.exe"); } }
        public static bool Available { get { return File.Exists(ToolPath); } }
        private const string Session = "NabaFrameMon";

        public class Result
        {
            public int Frames; public double Seconds, Avg, Low1, Low01;
            public List<DateTime> Hitches = new List<DateTime>();   // jhatke ke pal
            public List<double> HitchMs = new List<double>();
            public Dictionary<string, int> PerSecond = new Dictionary<string, int>();   // "HH:mm:ss" -> frames
            public double HitchPerMin { get { return Seconds > 0 ? Hitches.Count / (Seconds / 60.0) : 0; } }
        }

        private Process proc;
        private Thread reader;
        private readonly object gate = new object();
        private readonly Dictionary<string, List<KeyValuePair<DateTime, double>>> bySwap = new Dictionary<string, List<KeyValuePair<DateTime, double>>>();
        public string Error;

        public bool Start()
        {
            if (!Available) { Error = "PresentMon nahi mila"; return false; }
            try
            {
                StopSession();
                var psi = new ProcessStartInfo(ToolPath, "--process_name HD-Player.exe --output_stdout --stop_existing_session --session_name " + Session)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                proc = Process.Start(psi);
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null && Error == null && e.Data.Trim().Length > 0) Error = e.Data.Trim(); };
                proc.BeginErrorReadLine();
                reader = new Thread(Read) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
                reader.Start();
                return true;
            }
            catch (Exception ex) { Error = ex.Message; return false; }
        }

        private void Read()
        {
            try
            {
                string header = null; int iFrame = -1, iSwap = -1, iTime = -1; bool secs = false; double prevT = double.NaN;
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    if (header == null)
                    {
                        if (line.IndexOf("Application", StringComparison.OrdinalIgnoreCase) >= 0 && line.Contains(","))
                        {
                            header = line;
                            var cols = header.Split(',').Select(c => c.Trim()).ToList();
                            iFrame = Find(cols, "MsBetweenPresents", "FrameTime", "msBetweenPresents", "MsBetweenAppStart");
                            iSwap = Find(cols, "SwapChainAddress");
                            iTime = Find(cols, "TimeInSeconds", "CPUStartTime");
                            secs = iTime >= 0 && cols[iTime].Equals("TimeInSeconds", StringComparison.OrdinalIgnoreCase);
                        }
                        continue;
                    }
                    var f = line.Split(',');
                    double ms = double.NaN, v;
                    if (iFrame >= 0 && iFrame < f.Length && double.TryParse(f[iFrame], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) ms = v;
                    else if (iTime >= 0 && iTime < f.Length && double.TryParse(f[iTime], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    { if (!double.IsNaN(prevT)) ms = (v - prevT) * (secs ? 1000 : 1); prevT = v; }
                    if (double.IsNaN(ms) || ms <= 0.05 || ms > 2000) continue;
                    string key = iSwap >= 0 && iSwap < f.Length ? f[iSwap] : "x";
                    lock (gate)
                    {
                        List<KeyValuePair<DateTime, double>> l;
                        if (!bySwap.TryGetValue(key, out l)) { l = new List<KeyValuePair<DateTime, double>>(); bySwap[key] = l; }
                        if (l.Count < 400000) l.Add(new KeyValuePair<DateTime, double>(DateTime.Now, ms));
                    }
                }
            }
            catch { }
        }

        public Result Stop()
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
            StopSession();
            try { if (reader != null) reader.Join(2000); } catch { }
            lock (gate) return Analyze(bySwap.Values.OrderByDescending(x => x.Count).FirstOrDefault());
        }

        private static void StopSession()
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo("logman.exe", "stop " + Session + " -ets") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                    p.WaitForExit(5000);
            }
            catch { }
        }

        private static Result Analyze(List<KeyValuePair<DateTime, double>> fr)
        {
            var r = new Result();
            if (fr == null || fr.Count < 30) return r;
            // shuru ke 20 sec (game / BlueStacks load ho raha) gino mat - loading ke lambe frame FPS ko galat dikhate hain
            if ((fr[fr.Count - 1].Key - fr[0].Key).TotalSeconds > 60)
            {
                var t0 = fr[0].Key.AddSeconds(20);
                var trimmed = fr.Where(x => x.Key >= t0).ToList();
                if (trimmed.Count >= 30) fr = trimmed;
            }
            var ft = fr.Select(x => x.Value).ToList();
            double total = ft.Sum();
            var sorted = ft.OrderByDescending(x => x).ToList();
            r.Frames = ft.Count; r.Seconds = total / 1000.0;
            r.Avg = ft.Count * 1000.0 / total;
            r.Low1 = 1000.0 / sorted.Take(Math.Max(1, ft.Count / 100)).Average();
            r.Low01 = 1000.0 / sorted.Take(Math.Max(1, ft.Count / 1000)).Average();
            double median = ft.OrderBy(x => x).ElementAt(ft.Count / 2);
            double limit = Math.Max(34, median * 3);
            DateTime lastHitch = DateTime.MinValue;
            foreach (var x in fr)
            {
                string k = x.Key.ToString("HH:mm:ss"); int c; r.PerSecond.TryGetValue(k, out c); r.PerSecond[k] = c + 1;
                if (x.Value > limit && (x.Key - lastHitch).TotalMilliseconds > 400) { r.Hitches.Add(x.Key); r.HitchMs.Add(x.Value); lastHitch = x.Key; }
            }
            return r;
        }

        private static int Find(List<string> cols, params string[] names)
        {
            foreach (var n in names) { int i = cols.FindIndex(c => c.Equals(n, StringComparison.OrdinalIgnoreCase)); if (i >= 0) return i; }
            return -1;
        }
    }
}
