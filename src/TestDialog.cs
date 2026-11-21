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
        private string mode = "cpu";
        private RoundButton modeFps, modeRend, modeCpu;
        private readonly Card box;
        private Func<string, string> RN = r => r == "gl" ? "OpenGL" : r == "vlcn" ? "Vulkan" : r == "dx" ? "DirectX" : r;

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

            // ---- original settings ----
            if (emu != null)
                foreach (var k in new[] { "graphics_renderer", "fb_width", "fb_height", "dpi", "max_fps", "cpus" })
                    original[k] = BsControl.Get(emu, inst, k) ?? "";

            modeCpu = new RoundButton { Text = "CPU TEST", ChamferShape = true, Font = Theme.Semi(8.5F), Location = new Point(298, 14), Size = new Size(88, 34) };
            modeFps = new RoundButton { Text = "FPS TEST", ChamferShape = true, Font = Theme.Semi(8.5F), Location = new Point(392, 14), Size = new Size(88, 34) };
            modeRend = new RoundButton { Text = "RENDERER", ChamferShape = true, Font = Theme.Semi(8.5F), Location = new Point(486, 14), Size = new Size(92, 34) };
            modeCpu.Click += (s, e) => { if (cur < 0) { mode = "cpu"; BuildRounds(); } };
            Controls.Add(modeCpu);
            modeFps.Click += (s, e) => { if (cur < 0) { mode = "fps"; BuildRounds(); } };
            modeRend.Click += (s, e) => { if (cur < 0) { mode = "rend"; BuildRounds(); } };
            Controls.Add(modeFps); Controls.Add(modeRend);
            int y = 84 + 3 * 66;
            box = new Card { Location = new Point(22, y + 6), Size = new Size(556, 214), Neon = true, ChamferSize = 12, Fill = Theme.Card };
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

            BuildRounds();
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
                    "3. Match me ghuste hi F9 dabao (recording shuru), fight karo.\n" +
                    "4. 2-4 min ke baad phir F9 (match khatam). Ye window band mat karna.\n\n" +
                    "Har round me yahi. Aakhir me app sabse kam jhatke wali setting khud laga degi.\n" +
                    "Graphics / resolution kam nahi hogi. Upar chuno: FPS TEST ya RENDERER TEST.\nHar round me Free Fire ki graphics same rakhna (taaki muqabla sahi ho).";

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

        // ---- F9: game ke andar se hi recording shuru / khatam (Alt+Tab ki zaroorat nahi) ----
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h, int id);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool MessageBeep(uint t);
        private bool hotkey;
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); try { hotkey = RegisterHotKey(Handle, 9, 0x4000 /*NOREPEAT*/, 0x78 /*F9*/); } catch { } }
        protected override void OnHandleDestroyed(EventArgs e) { try { UnregisterHotKey(Handle, 9); } catch { } base.OnHandleDestroyed(e); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 9)   // WM_HOTKEY
            {
                if (!working && (phase == 1 || (phase == 2 && (DateTime.Now - recStart).TotalSeconds >= 90)))
                {
                    try { Console.Beep(phase == 1 ? 1200 : 700, 180); } catch { }
                    Next();
                }
                else try { MessageBeep(0x10); } catch { }
            }
            base.WndProc(ref m);
        }

        private void BuildRounds()
        {
            foreach (var r in rounds) { Controls.Remove(r.Row); r.Row.Dispose(); }
            rounds.Clear();
            modeFps.Fill = mode == "fps" ? Theme.Accent : Theme.Row; modeFps.Fill2 = mode == "fps" ? Theme.AccentDark : Theme.Row;
            modeRend.Fill = mode == "rend" ? Theme.Accent : Theme.Row; modeRend.Fill2 = mode == "rend" ? Theme.AccentDark : Theme.Row;
            modeFps.Invalidate(); modeRend.Invalidate();
            modeCpu.Fill = mode == "cpu" ? Theme.Accent : Theme.Row; modeCpu.Fill2 = mode == "cpu" ? Theme.AccentDark : Theme.Row; modeCpu.Invalidate();
            if (emu == null) return;
            string rend; original.TryGetValue("graphics_renderer", out rend); if (string.IsNullOrEmpty(rend)) rend = "vlcn";
            string res = string.IsNullOrEmpty(original["fb_width"]) ? "default res" : original["fb_width"] + "x" + original["fb_height"];
            int f; if (!int.TryParse(original["max_fps"], out f) || f < 30) f = 100;

            int c; if (!int.TryParse(original["cpus"], out c) || c < 1) c = 4;
            if (mode == "cpu")
            {
                int threads = Environment.ProcessorCount;
                var list = new List<int>();
                if (8 <= threads - 2) list.Add(8);          // pehle 8 core
                if (!list.Contains(c)) list.Add(c);
                if (4 <= threads - 2 && !list.Contains(4)) list.Add(4);
                int n = 1;
                foreach (var v in list)
                {
                    var r = new Round { Name = "Round " + n + ":  " + v + " CPU core" + (v == c ? "  (abhi wali)" : ""), Desc = RN(rend) + "  •  " + f + " FPS  •  " + v + " / " + threads + " thread BlueStacks ko" };
                    if (v != c) r.Values["cpus"] = v.ToString();
                    rounds.Add(r); n++;
                }
            }
            else if (mode == "rend")
            {
                string other = rend == "gl" ? "vlcn" : "gl";
                rounds.Add(new Round { Name = "Round 1:  Abhi wali", Desc = RN(rend) + "  •  " + res + "  •  " + f + " FPS" });
                var r2 = new Round { Name = "Round 2:  " + RN(other), Desc = RN(other) + "  •  " + res + "  •  " + f + " FPS" };
                r2.Values["graphics_renderer"] = other; rounds.Add(r2);
            }
            else
            {
                var list = new List<int> { f };
                int lower = Math.Max(60, f - 10), higher = f < 120 ? 120 : f + 24;
                if (!list.Contains(lower)) list.Add(lower);
                if (!list.Contains(higher)) list.Add(higher);
                int n = 1;
                foreach (var v in list)
                {
                    var r = new Round
                    {
                        Name = "Round " + n + ":  " + v + " FPS" + (n == 1 ? "  (abhi wali)" : ""),
                        Desc = RN(rend) + "  •  " + res + (v < f ? "  •  CPU pe kam bojh, zyada stable" : v > f ? "  •  zyada FPS" : "")
                    };
                    if (v != f) r.Values["max_fps"] = v.ToString();
                    rounds.Add(r); n++;
                }
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
            modeFps.Enabled = false; modeRend.Enabled = false; modeCpu.Enabled = false;
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
                BsControl.AiHighlightsOff(emu);
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
                    "3. Match shuru karo. Match me ghuste hi keyboard pe  F9  dabao (game ke andar se hi).\n" +
                    "   Beep aayegi = recording shuru.\n\n" +
                    "Ye window band MAT karna - band karne se test ruk jata hai aur purani setting wapas lagti hai.";
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
                            "Kam se kam 1:30, achha ho 2-4 min. Match khatam hone par phir se  F9  dabao (ya yahan MATCH KHATAM).";
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
            var r1 = rounds.FirstOrDefault(x => x.Values.Count == 0) ?? rounds[0];   // abhi wali setting = muqabla
            var winner = rounds.OrderByDescending(x => x.Score).First();
            var best = new Dictionary<string, string>(original);
            string why;
            if (winner != r1 && winner.Score > r1.Score * 1.03)
            {
                foreach (var kv in winner.Values) best[kv.Key] = kv.Value;
                why = winner.Name.Substring(winner.Name.IndexOf(':') + 1).Trim() + " sabse smooth nikla";
                string fps; int fv; if (winner.Values.TryGetValue("max_fps", out fps) && int.TryParse(fps, out fv)) Settings.FpsLimit = fv;
                string cp; int cv; if (winner.Values.TryGetValue("cpus", out cp) && int.TryParse(cp, out cv)) { Settings.SetInt("EmuCpus", cv); PcProfile.Reset(); }
            }
            else { winner = r1; why = "abhi wali setting hi sabse achhi hai"; }
            // CPU test: jeeta hua core yaad rakho (BOOST dabane par bhi wahi rahe)
            if (mode == "cpu")
            {
                string cp; int cv;
                if (!winner.Values.TryGetValue("cpus", out cp)) original.TryGetValue("cpus", out cp);
                if (int.TryParse(cp, out cv) && cv >= 2) { Settings.SetInt("EmuCpus", cv); PcProfile.Reset(); }
            }
            foreach (var r in rounds) if (r == winner) { r.Row.Border = Theme.Green; r.Row.Invalidate(); r.Status.Text = "BEST  •  " + r.Status.Text; }

            stepText.Text = "RESULT\n\n" + why + ".\n\nBlueStacks best setting ke saath dobara khulega.\nGraphics quality wahi rahegi jo aapne rakhi hai.";
            phase = 3; mainBtn.Text = "THEEK HAI";
            if (mainLog != null) mainLog("Setting Test poora: " + why);
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
                BsControl.AiHighlightsOff(e);
                if (launch) BsControl.Launch(e, i);
            });
            if (mainLog != null) mainLog(what + " lag rahi hai (BlueStacks restart).");
        }
    }
}
