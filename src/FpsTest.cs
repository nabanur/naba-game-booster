using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NabaGameBooster
{
    internal class FpsResult
    {
        public double Avg, Low1, Min;
        public int Frames;
        public double Seconds;
    }

    /// <summary>
    /// Asli FPS naapta hai - Intel PresentMon (MIT license) se, emulator ki window (HD-Player.exe) ke
    /// har frame ka time ginke. Koi nakli number nahi.
    /// </summary>
    internal static class FpsTest
    {
        public static string ToolPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"tools\PresentMon\PresentMon.exe"); }
        }

        public static bool ToolAvailable { get { return File.Exists(ToolPath); } }

        public static FpsResult Measure(int seconds, Action<string> log)
        {
            if (!ToolAvailable) throw new Exception("FPS test tool app ke saath nahi mila - app dobara install karo.");
            if (!Emulators.AnyRunning) throw new Exception("BlueStacks / MSI chal nahi raha - pehle Free Fire kholo.");

            string args = "--process_name HD-Player.exe --output_stdout --timed " + seconds +
                          " --terminate_after_timed --stop_existing_session --session_name NabaFpsTest --v1_metrics";
            var psi = new ProcessStartInfo(ToolPath, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };

            var rows = new List<string>();
            string header = null;
            string errText = "";
            using (var p = Process.Start(psi))
            {
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) errText += e.Data + "\n"; };
                p.BeginErrorReadLine();
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    if (header == null)
                    {
                        if (line.IndexOf("Application", StringComparison.OrdinalIgnoreCase) >= 0 && line.Contains(",")) header = line;
                        continue;
                    }
                    rows.Add(line);
                }
                if (!p.WaitForExit((seconds + 30) * 1000)) { try { p.Kill(); } catch { } }
            }

            if (header == null || rows.Count < 10)
                throw new Exception("Frames nahi mile. Game window saamne rakho aur dobara try karo." +
                                    (string.IsNullOrWhiteSpace(errText) ? "" : " (" + errText.Trim().Split('\n')[0] + ")"));

            return Analyze(header, rows);
        }

        /// <summary>PresentMon CSV se FPS nikalta hai (v1 aur v2 dono column naam samajhta hai).</summary>
        public static FpsResult Analyze(string header, List<string> rows)
        {
            var cols = header.Split(',').Select(c => c.Trim()).ToList();
            int iFrame = FindCol(cols, "MsBetweenPresents", "FrameTime", "MsBetweenAppStart");
            int iTime = FindCol(cols, "TimeInSeconds", "CPUStartTime", "CPUStartQPCTime");
            int iSwap = FindCol(cols, "SwapChainAddress");

            // Sabse zyada frames wali swapchain (asli game window) chuno
            var bySwap = new Dictionary<string, List<string[]>>();
            foreach (var r in rows)
            {
                var f = r.Split(',');
                string key = iSwap >= 0 && iSwap < f.Length ? f[iSwap] : "x";
                List<string[]> l;
                if (!bySwap.TryGetValue(key, out l)) { l = new List<string[]>(); bySwap[key] = l; }
                l.Add(f);
            }
            var main = bySwap.Values.OrderByDescending(l => l.Count).First();

            var ft = new List<double>();
            if (iFrame >= 0)
            {
                foreach (var f in main)
                {
                    double v;
                    if (iFrame < f.Length && double.TryParse(f[iFrame], NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0.05 && v < 1000) ft.Add(v);
                }
            }
            else if (iTime >= 0)
            {
                bool secs = cols[iTime].Equals("TimeInSeconds", StringComparison.OrdinalIgnoreCase);
                double prev = double.NaN;
                foreach (var f in main)
                {
                    double v;
                    if (iTime >= f.Length || !double.TryParse(f[iTime], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                    if (!double.IsNaN(prev)) { double d = (v - prev) * (secs ? 1000 : 1); if (d > 0.05 && d < 1000) ft.Add(d); }
                    prev = v;
                }
            }
            if (ft.Count < 10) throw new Exception("FPS gin nahi paya (frames kam mile).");

            double total = ft.Sum();
            var sorted = ft.OrderByDescending(x => x).ToList();   // sabse dheeme frame pehle
            int n1 = Math.Max(1, ft.Count / 100);
            double worst1 = sorted.Take(n1).Average();
            return new FpsResult
            {
                Frames = ft.Count,
                Seconds = total / 1000.0,
                Avg = ft.Count * 1000.0 / total,
                Low1 = 1000.0 / worst1,
                Min = 1000.0 / sorted[0]
            };
        }

        private static int FindCol(List<string> cols, params string[] names)
        {
            foreach (var n in names)
            {
                int i = cols.FindIndex(c => c.Equals(n, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) return i;
            }
            return -1;
        }

        // ---------------- result save / load ----------------
        public static void Save(string which, FpsResult r)
        {
            Settings.SetInt("Fps" + which + "Avg", (int)Math.Round(r.Avg * 10));
            Settings.SetInt("Fps" + which + "Low", (int)Math.Round(r.Low1 * 10));
        }

        public static bool Load(string which, out double avg, out double low)
        {
            int a = Settings.GetInt("Fps" + which + "Avg", -1), l = Settings.GetInt("Fps" + which + "Low", -1);
            avg = a / 10.0; low = l / 10.0;
            return a > 0;
        }

        public static string CpuName()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    return ((k == null ? null : k.GetValue("ProcessorNameString") as string) ?? "").Trim();
            }
            catch { return ""; }
        }

        /// <summary>Share karne layak result card (PNG) Desktop pe banata hai.</summary>
        public static string SaveCard(string gpu)
        {
            double ba, bl, aa, al;
            if (!Load("Before", out ba, out bl) || !Load("After", out aa, out al)) return null;
            int W = 900, H = 470;
            using (var bmp = new Bitmap(W, H))
            using (var g = Graphics.FromImage(bmp))
            {
                Theme.Smooth(g);
                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(14, 16, 22), Color.FromArgb(26, 20, 12), 30f)) g.FillRectangle(bg, 0, 0, W, H);

                // logo
                var lr = new RectangleF(40, 34, 56, 56);
                using (var p = Theme.Round(lr, 14)) using (var b = new LinearGradientBrush(lr, Theme.Accent, Theme.AccentDark, 45f)) g.FillPath(b, p);
                float s = 56 / 44f;
                var bolt = new[] { new PointF(40 + 25 * s, 34 + 7 * s), new PointF(40 + 12 * s, 34 + 25 * s), new PointF(40 + 21 * s, 34 + 25 * s), new PointF(40 + 18 * s, 34 + 38 * s), new PointF(40 + 32 * s, 34 + 18 * s), new PointF(40 + 23 * s, 34 + 18 * s), new PointF(40 + 28 * s, 34 + 7 * s) };
                using (var b = new SolidBrush(Color.FromArgb(20, 20, 26))) g.FillPolygon(b, bolt);
                using (var f = Theme.Semi(24F)) g.DrawString("Naba Game Booster", f, Brushes.White, 112, 36);
                using (var f = Theme.F(11F)) using (var b = new SolidBrush(Theme.SubText)) g.DrawString("Asli FPS test  •  " + DateTime.Now.ToString("dd MMM yyyy"), f, b, 115, 72);

                DrawBox(g, new RectangleF(40, 130, 380, 200), "PEHLE (bina boost)", ba, bl, Color.FromArgb(120, 126, 142));
                DrawBox(g, new RectangleF(480, 130, 380, 200), "BOOST KE SAATH", aa, al, Theme.Green);
                using (var f = Theme.Semi(28F)) using (var b = new SolidBrush(Theme.Accent))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("→", f, b, new RectangleF(420, 130, 60, 200), sf);
                }

                double diff = aa - ba, pct = ba > 0 ? diff / ba * 100 : 0;
                string verdict = diff >= 0 ? string.Format("+{0:N0} FPS  ({1:N0}% zyada)", diff, pct) : string.Format("{0:N0} FPS", diff);
                using (var f = Theme.Semi(22F)) using (var b = new SolidBrush(diff >= 0 ? Theme.Green : Theme.Red))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(verdict, f, b, new RectangleF(0, 352, W, 40), sf);
                }
                using (var f = Theme.F(10F)) using (var b = new SolidBrush(Theme.SubText))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center };
                    var specs = new List<string>();
                    if (!string.IsNullOrEmpty(CpuName())) specs.Add(CpuName());
                    if (!string.IsNullOrEmpty(gpu)) specs.Add(gpu);
                    if (Native.TotalRamGB() > 0.5) specs.Add(Native.TotalRamGB().ToString("N0") + " GB RAM");
                    g.DrawString(string.Join("   •   ", specs), f, b, new RectangleF(0, 404, W, 24), sf);
                    g.DrawString("github.com/nabanur/naba-game-booster", f, b, new RectangleF(0, 430, W, 24), sf);
                }
                string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Naba FPS Result " + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".png");
                bmp.Save(file, ImageFormat.Png);
                return file;
            }
        }

        private static void DrawBox(Graphics g, RectangleF r, string title, double avg, double low, Color c)
        {
            using (var p = Theme.Round(r, 18))
            using (var b = new SolidBrush(Color.FromArgb(30, 34, 44)))
            using (var pen = new Pen(Color.FromArgb(120, c), 2f))
            {
                g.FillPath(b, p); g.DrawPath(pen, p);
            }
            var sf = new StringFormat { Alignment = StringAlignment.Center };
            using (var f = Theme.Semi(11F)) using (var b = new SolidBrush(c)) g.DrawString(title, f, b, new RectangleF(r.X, r.Y + 18, r.Width, 24), sf);
            using (var f = Theme.Semi(54F)) g.DrawString(avg.ToString("N0"), f, Brushes.White, new RectangleF(r.X, r.Y + 44, r.Width, 90), sf);
            using (var f = Theme.F(11F)) using (var b = new SolidBrush(Theme.SubText))
                g.DrawString("Average FPS   •   1% low " + low.ToString("N0"), f, b, new RectangleF(r.X, r.Y + 150, r.Width, 24), sf);
        }
    }
}
