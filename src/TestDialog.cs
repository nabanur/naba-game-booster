using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NabaGameBooster
{
    /// <summary>
    /// SETTING TEST: BlueStacks ki alag-alag setting pe ek-ek match khelwa ke asli frame data se
    /// sabse kam jhatke wali setting chunta hai aur laga deta hai.
    /// </summary>
    internal class TestDialog : Form
    {
        private class Round
        {
            public string Name, Desc;
            public Dictionary<string, string> Values = new Dictionary<string, string>();
            public FrameMon.Result Res; public double Thread, Gpu;
            public Label Status; public Card Row;
            public double Score { get { return Res == null || Res.Frames == 0 ? double.MinValue : Res.Low1 + Res.Avg * 0.2 - Res.HitchPerMin * 6; } }
        }

        private readonly Action<string> mainLog;
        private readonly Emu emu; private readonly string inst;
        private readonly List<Round> rounds = new List<Round>();
        private readonly Dictionary<string, string> original = new Dictionary<string, string>();
        private readonly Label stepText;
        private readonly RoundButton mainBtn, cancelBtn;
        private int cur = -1;       // kaunsa round
        private int phase = 0;      // 0 = shuru karna hai, 1 = BlueStacks khul raha / match ka intezar, 2 = recording, 3 = sab khatam
        private DateTime recStart;
        private bool working, applied;
        private readonly Timer clock = new Timer { Interval = 1000 };

        public TestDialog(Action<string> logger)
        {
            mainLog = logger;
            emu = BsControl.Emu;
            inst = emu != null ? BsControl.Instance(emu) : "Pie64";

            Text = "Setting Test";
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(600, 640);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true; ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterParent; TopMost = false;
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5F);

            Controls.Add(new IconBox { IconName = "layers", IconColor = Theme.Accent, Location = new Point(22, 20), Size = new Size(28, 28) });
            Controls.Add(new Label { Text = "SETTING TEST", Font = Theme.Heavy(16F), ForeColor = Theme.Accent, AutoSize = true, Location = new Point(56, 16) });
            Controls.Add(new Label { Text = "Har setting pe ek match - app frame-by-frame naap ke best setting khud lagayegi", Font = Theme.F(9F), ForeColor = Theme.SubText, AutoSize = true, Location = new Point(58, 50) });

            // ---- rounds ----
            string rend = emu != null ? (BsControl.Get(emu, inst, "graphics_renderer") ?? "vlcn") : "vlcn";
            string other = rend == "gl" ? "vlcn" : "gl";
            string w = emu != null ? (BsControl.Get(emu, inst, "fb_width") ?? "1920") : "1920";
            string h = emu != null ? (BsControl.Get(emu, inst, "fb_height") ?? "1080") : "1080";
            string dpi = emu != null ? (BsControl.Get(emu, inst, "dpi") ?? "320") : "320";
            original["graphics_renderer"] = rend; original["fb_width"] = w; original["fb_height"] = h; original["dpi"] = dpi;
            Func<string, string> RN = r => r == "gl" ? "OpenGL" : r == "vlcn" ? "Vulkan" : r == "dx" ? "DirectX" : r;

            rounds.Add(new Round { Name = "Round 1:  Abhi wali", Desc = RN(rend) + "  •  " + w + "x" + h });
            var r2 = new Round { Name = "Round 2:  " + RN(other), Desc = RN(other) + "  •  " + w + "x" + h };
            r2.Values["graphics_renderer"] = other; rounds.Add(r2);
            bool big = int.Parse(w) > 1600;
            if (big)
            {
                var r3 = new Round { Name = "Round 3:  Halki resolution", Desc = RN(rend) + "  •  1600x900  (GPU pe kam bojh)" };
                r3.Values["fb_width"] = "1600"; r3.Values["fb_height"] = "900"; r3.Values["dpi"] = "240"; rounds.Add(r3);
            }

            int y = 84;
            foreach (var r in rounds)
            {
                r.Row = new Card { Location = new Point(22, y), Size = new Size(556, 58), Fill = Theme.Row, Border = Theme.RowBorder, Radius = 9 };
                r.Row.Controls.Add(new Label { Text = r.Name, Font = Theme.Semi(10.5F), ForeColor = Theme.Text, Location = new Point(14, 7), AutoSize = true, BackColor = Color.Transparent });
                r.Row.Controls.Add(new Label { Text = r.Desc, Font = Theme.F(8.5F), ForeColor = Theme.SubText, Location = new Point(15, 32), AutoSize = true, BackColor = Color.Transparent });
                r.Status = new Label { Text = "baaki", Font = Theme.Semi(9F), ForeColor = Theme.SubText, Location = new Point(300, 8), Size = new Size(244, 42), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent };
                r.Row.Controls.Add(r.Status);
                Controls.Add(r.Row);
                y += 66;
            }

            var box = new Card { Location = new Point(22, y + 6), Size = new Size(556, 214), Neon = true, ChamferSize = 12, Fill = Theme.Card };
            stepText = new Label { Font = Theme.F(10F), ForeColor = Theme.Text, Location = new Point(22, 18), Size = new Size(512, 180), BackColor = Color.Transparent };
            box.Controls.Add(stepText);
            Controls.Add(box);

            mainBtn = new RoundButton { Text = "TEST SHURU", ChamferShape = true, GlowColor = Theme.Accent, Fill = Color.FromArgb(255, 176, 40), Fill2 = Theme.AccentDark, ForeColor = Color.FromArgb(30, 14, 4), Font = Theme.Heavy(12F), Location = new Point(300, 572), Size = new Size(278, 54) };
            mainBtn.Click += (s, e) => Next();
            cancelBtn = new RoundButton { Text = "Band karo", Outline = true, OutlineColor = Theme.Purple, ChamferShape = true, Font = Theme.Semi(10F), Location = new Point(22, 578), Size = new Size(170, 42) };
            cancelBtn.Click += (s, e) => Close();
            Controls.Add(mainBtn); Controls.Add(cancelBtn);

            clock.Tick += (s, e) =>
            {
                if (phase == 2 && cur >= 0)
                {
                    var t = DateTime.Now - recStart;
                    rounds[cur].Status.Text = "REC  " + (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
                    rounds[cur].Status.ForeColor = Theme.Red;
                    mainBtn.Enabled = t.TotalSeconds >= 90;
                    mainBtn.Text = t.TotalSeconds >= 90 ? "MATCH KHATAM" : "kam se kam 1:30 khelo";
                }
            };
            clock.Start();

            if (emu == null)
            {
                stepText.Text = "BlueStacks / MSI App Player nahi mila.";
                mainBtn.Enabled = false;
            }
            else
                stepText.Text =
                    "Kaise hoga:\n" +
                    "1. App BlueStacks band karke setting badlegi aur Free Fire khud kholegi.\n" +
                    "2. Aap ek match shuru karo (Clash Squad / BR - jisme fight ho).\n" +
                    "3. Match shuru hote hi \"RECORDING SHURU\" dabao, fight karo.\n" +
                    "4. 2-4 min ke baad \"MATCH KHATAM\" dabao.\n\n" +
                    "Har round me yahi. Aakhir me app sabse kam jhatke wali setting khud laga degi.\n" +
                    "Har round me Free Fire ki graphics same rakhna (taaki muqabla sahi ho).";

            FormClosing += (s, e) =>
            {
                if (applied || working) { if (working) e.Cancel = true; return; }
                if (cur < 0) return;
                if (MessageBox.Show(this, "Test beech me band karein? BlueStacks ki purani setting wapas laga dunga.", "Setting Test", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { e.Cancel = true; return; }
                try { Jhatka.Stop(); } catch { }
                Jhatka.Label = "";
                ApplyAndLaunch(original, "Purani setting wapas", false);
            };
        }

        private void Next()
        {
            if (working) return;
            if (phase == 0) StartRound(cur + 1);
            else if (phase == 1) StartRecording();
            else if (phase == 2) StopRound();
            else if (phase == 3) Close();
        }

        private void Busy(bool b) { working = b; mainBtn.Enabled = !b; cancelBtn.Enabled = !b; Cursor = b ? Cursors.WaitCursor : Cursors.Default; }

        private void StartRound(int i)
        {
            cur = i; var r = rounds[i];
            var vals = new Dictionary<string, string>(original);
            foreach (var kv in r.Values) vals[kv.Key] = kv.Value;
            r.Status.Text = "BlueStacks khul raha..."; r.Status.ForeColor = Theme.Accent;
            r.Row.Border = Theme.Accent; r.Row.Invalidate();
            stepText.Text = r.Name + "  (" + r.Desc + ")\n\nBlueStacks band ho raha hai, setting lag rahi hai, phir Free Fire khulega...";
            Busy(true);
            try { Jhatka.Stop(); } catch { }
            Task.Factory.StartNew(() =>
            {
                if (!GameMode.IsOn) { try { GameMode.On(m => { }); } catch { } }
                BsControl.Close();
                bool ok = BsControl.Set(emu, inst, vals);
                BsControl.Launch(emu, inst);
                return ok;
            }).ContinueWith(t => BeginInvoke(new Action(() =>
            {
                Busy(false);
                phase = 1;
                if (mainLog != null) mainLog("Setting Test - " + r.Name + " (" + r.Desc + ")" + (t.Result ? "" : "  [setting likh nahi paya]"));
                r.Status.Text = "match ka intezar";
                stepText.Text = r.Name + "  (" + r.Desc + ")\n\n" +
                    "1. Free Fire khul raha hai - load hone do.\n" +
                    "2. F11 se full screen karo.\n" +
                    "3. Match shuru karo. Jaise hi match me ghuso, Alt+Tab karke neeche button dabao:\n\n" +
                    "            RECORDING SHURU";
                mainBtn.Text = "RECORDING SHURU";
            })));
        }

        private void StartRecording()
        {
            var r = rounds[cur];
            GameMode.SetBlueStacksPriority(System.Diagnostics.ProcessPriorityClass.High);
            Jhatka.Label = r.Name + " (" + r.Desc + ")";
            Jhatka.Start();
            recStart = DateTime.Now; phase = 2;
            stepText.Text = r.Name + "  (" + r.Desc + ")\n\nRECORDING chal rahi hai. Wapas game me jao aur fight karo.\n\n" +
                            "Kam se kam 1:30, achha ho 2-4 min. Match khatam hone par Alt+Tab karke \"MATCH KHATAM\" dabao.";
            WindowState = FormWindowState.Minimized;
        }

        private void StopRound()
        {
            var r = rounds[cur];
            Busy(true);
            Task.Factory.StartNew(() => { try { Jhatka.Stop(); } catch { } }).ContinueWith(t => BeginInvoke(new Action(() =>
            {
                Busy(false);
                r.Res = Jhatka.LastFrames; r.Thread = Jhatka.LastThreadAvg; r.Gpu = Jhatka.LastGpuAvg;
                Jhatka.Label = "";
                r.Row.Border = Theme.RowBorder; r.Row.Invalidate();
                if (r.Res == null || r.Res.Frames < 100)
                {
                    r.Status.Text = "frames nahi mile - dobara"; r.Status.ForeColor = Theme.Red;
                    stepText.Text = "Is round me FPS record nahi hua (game window saamne thi?). Ye round dobara karte hain.";
                    cur--; phase = 0; mainBtn.Text = "ROUND DOBARA";
                    return;
                }
                r.Status.Text = string.Format("FPS {0:0}  •  1% low {1:0}\n{2:0.0} jhatke/min", r.Res.Avg, r.Res.Low1, r.Res.HitchPerMin);
                r.Status.ForeColor = Theme.Green;
                if (mainLog != null) mainLog(string.Format("  {0}: FPS {1:0}, 1% low {2:0}, {3:0.0} jhatke/min, main thread {4:0}%, GPU {5:0}%", r.Name, r.Res.Avg, r.Res.Low1, r.Res.HitchPerMin, r.Thread, r.Gpu));
                WindowState = FormWindowState.Normal; Activate();

                if (cur + 1 < rounds.Count)
                {
                    phase = 0; mainBtn.Text = "AGLA ROUND";
                    stepText.Text = r.Name + " ho gaya.\n\nAgla: " + rounds[cur + 1].Name + "  (" + rounds[cur + 1].Desc + ")\n\"AGLA ROUND\" dabao - BlueStacks dobara khulega.";
                }
                else Finish();
            })));
        }

        private void Finish()
        {
            var r1 = rounds[0];
            var best = new Dictionary<string, string>(original);
            var why = new List<string>();
            var r2 = rounds[1];
            if (r2.Score > r1.Score * 1.03) { best["graphics_renderer"] = r2.Values["graphics_renderer"]; why.Add(r2.Desc.Split(' ')[0] + " renderer behtar nikla"); }
            if (rounds.Count > 2)
            {
                var r3 = rounds[2];
                if (r3.Score > r1.Score * 1.03) { foreach (var kv in r3.Values) best[kv.Key] = kv.Value; why.Add("1600x900 se jhatke kam hue"); }
            }
            var winner = rounds.OrderByDescending(x => x.Score).First();
            foreach (var r in rounds) if (r == winner) { r.Row.Border = Theme.Green; r.Row.Invalidate(); r.Status.Text = "BEST  •  " + r.Status.Text; }

            stepText.Text = "RESULT\n\n" + (why.Count == 0 ? "Abhi wali setting hi sabse achhi hai - kuch nahi badla." : "Lag rahi hai: " + string.Join(", ", why) + ".") +
                            "\n\nBlueStacks best setting ke saath dobara khulega.\n\n" +
                            "Tip: Free Fire graphics Smooth + High FPS ON rakhna.";
            phase = 3; mainBtn.Text = "THEEK HAI";
            if (mainLog != null) mainLog("Setting Test poora: " + (why.Count == 0 ? "abhi wali setting best" : string.Join(", ", why)));
            ApplyAndLaunch(best, "Best setting", true);
        }

        private void ApplyAndLaunch(Dictionary<string, string> vals, string what, bool launch)
        {
            applied = true;
            var e = emu; var i = inst;
            Task.Factory.StartNew(() =>
            {
                BsControl.Close();
                BsControl.Set(e, i, vals);
                if (launch) BsControl.Launch(e, i);
            });
            if (mainLog != null) mainLog(what + " lag rahi hai (BlueStacks restart).");
        }
    }
}
