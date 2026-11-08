using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NabaGameBooster
{
    /// <summary>One-Click Boost: ek ek step chalta dikhta hai (✓), aakhir me PC Score aur restart.</summary>
    internal class BoostDialog : Form
    {
        private class Step
        {
            public string Title;
            public Func<bool> Applies;               // is PC pe chalna hai?
            public Action<Action<string>> Run;
            public Label Icon, Text, Sub;
            public string State = "wait";            // wait / run / ok / skip / fail
        }

        private readonly List<Step> steps = new List<Step>();
        private readonly PcProfile pc;
        private readonly Action<string> mainLog;
        private readonly Label header, sub;
        private readonly Panel list;
        private readonly Card result;
        private readonly BoostRing ring;
        private readonly RoundButton restartBtn, doneBtn;
        private readonly System.Windows.Forms.Timer spin = new System.Windows.Forms.Timer { Interval = 120 };
        private int spinIdx;
        private static readonly string[] SpinChars = { "◐", "◓", "◑", "◒" };
        public bool Finished;

        public BoostDialog(PcProfile profile, Action<string> mainLogger)
        {
            pc = profile; mainLog = mainLogger;
            Text = "One-Click Boost";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(560, 700);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5F);
            DoubleBuffered = true;

            header = new Label { Text = "BOOST ho raha hai...", Font = Theme.Semi(16F), ForeColor = Theme.Accent, AutoSize = true, Location = new Point(22, 34) };
            sub = new Label
            {
                Text = string.Format("{0} PC  •  {1}  •  {2} GB RAM  •  {3} core", pc.TierName, pc.Gpu, pc.RamGb, pc.PhysicalCores),
                Font = Theme.F(9F), ForeColor = Theme.SubText, Location = new Point(24, 70), Size = new Size(380, 40), AutoEllipsis = true
            };
            ring = new BoostRing { Location = new Point(418, 8) };
            Controls.Add(ring);
            Controls.Add(header); Controls.Add(sub);

            list = new Panel { Location = new Point(20, 140), Size = new Size(520, 380), BackColor = Theme.Bg };
            Controls.Add(list);

            result = new Card { Location = new Point(20, 530), Size = new Size(520, 110), Visible = false };
            Controls.Add(result);

            restartBtn = new RoundButton { Text = "PC Restart karo", Location = new Point(20, 652), Size = new Size(250, 38), Fill = Theme.Accent, Fill2 = Theme.AccentDark, ForeColor = Color.FromArgb(20, 20, 26), Font = Theme.Semi(10F), Visible = false };
            restartBtn.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Saara kaam save kar liya? PC 10 second me restart hoga.", "Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try { Process.Start(new ProcessStartInfo("shutdown", "/r /t 10 /c \"Naba Game Booster - boost lagane ke liye restart\"") { UseShellExecute = false, CreateNoWindow = true }); } catch { }
                Close();
            };
            doneBtn = new RoundButton { Text = "Baad me", Location = new Point(290, 652), Size = new Size(250, 38), Fill = Theme.Row, Font = Theme.Semi(10F), Visible = false };
            doneBtn.Click += (s, e) => Close();
            Controls.Add(restartBtn); Controls.Add(doneBtn);

            BuildSteps();
            spin.Tick += (s, e) => { spinIdx = (spinIdx + 1) % 4; foreach (var st in steps) if (st.State == "run") st.Icon.Text = SpinChars[spinIdx]; };
            FormClosing += (s, e) => { if (!Finished) e.Cancel = true; };   // beech me band nahi
            Shown += (s, e) => RunAll();
        }

        private void BuildSteps()
        {
            bool emu = Emulators.AnyInstalled;
            bool nv = Tweaks.NvidiaGpuName() != null;
            bool win11 = Environment.OSVersion.Version.Build >= 22000;

            Add("PC check", () => true, log => log(pc.TierName + " PC pehchana"));
            Add("Windows power + Game Mode", () => true, Tweaks.GamingOptimize);
            Add(string.Format("BlueStacks / MSI tune  (RAM {0} GB, CPU {1}, FPS {2})", pc.BsRamMb / 1024, pc.BsCpus, pc.Fps), () => emu, log => Tweaks.BlueStacksApplyProfile(pc, log));
            Add("Fast Mode - Hyper-V band", () => emu, Booster.FastModeOn);
            Add("BlueStacks Turbo - priority + GPU", () => emu, Booster.TurboOn);
            Add("ASTC - textures GPU pe", () => emu, Tweaks.AstcOn);
            Add("Defender - emulator scan se bahar", () => emu, Tweaks.DefenderExclude);
            Add("NVIDIA Boost - 4 setting", () => nv, Tweaks.NvidiaApply);
            Add("RAM Cleaner + Game Watchdog", () => true, log => { Settings.RamCleaner = true; Settings.Watchdog = true; log("RAM Cleaner aur Watchdog ON"); });
            Add("Timer 0.5ms", () => win11, Booster.TimerOn);
            Add("Windows Update / Search game ke time rokna", () => true, log => { Settings.PauseServices = true; log("ON"); });

            int y = 0;
            foreach (var st in steps)
            {
                var row = new Card { Location = new Point(0, y), Size = new Size(520, 32), Fill = Theme.Card, Border = Theme.Card, Radius = 8 };
                st.Icon = new Label { Text = "•", Font = Theme.Semi(11F), ForeColor = Theme.Grey, Location = new Point(10, 5), Size = new Size(24, 22), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter };
                st.Text = new Label { Text = st.Title, Font = Theme.F(9.5F), ForeColor = Theme.SubText, Location = new Point(40, 7), Size = new Size(360, 20), BackColor = Color.Transparent, AutoEllipsis = true };
                st.Sub = new Label { Text = "", Font = Theme.F(8.5F), ForeColor = Theme.SubText, Location = new Point(404, 8), Size = new Size(110, 18), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleRight };
                row.Controls.AddRange(new Control[] { st.Icon, st.Text, st.Sub });
                list.Controls.Add(row);
                y += 34;
            }
        }

        private void Add(string title, Func<bool> applies, Action<Action<string>> run)
        {
            steps.Add(new Step { Title = title, Applies = applies, Run = run });
        }

        private void SetState(Step st, string state, string note)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetState(st, state, note))); return; }
            st.State = state;
            switch (state)
            {
                case "run": st.Icon.ForeColor = Theme.Accent; st.Text.ForeColor = Theme.Text; break;
                case "ok": st.Icon.Text = "✓"; st.Icon.ForeColor = Theme.Green; st.Text.ForeColor = Theme.Text; break;
                case "skip": st.Icon.Text = "–"; st.Icon.ForeColor = Theme.Grey; st.Text.ForeColor = Theme.SubText; break;
                case "fail": st.Icon.Text = "!"; st.Icon.ForeColor = Theme.Amber; st.Text.ForeColor = Theme.Text; break;
            }
            st.Sub.Text = note ?? "";
            int finished = 0; foreach (var x in steps) if (x.State == "ok" || x.State == "skip" || x.State == "fail") finished++;
            ring.Target = steps.Count == 0 ? 0 : 100f * finished / steps.Count;
        }

        private void RunAll()
        {
            spin.Start();
            Task.Factory.StartNew(() =>
            {
                foreach (var st in steps)
                {
                    bool applies;
                    try { applies = st.Applies(); } catch { applies = false; }
                    if (!applies) { SetState(st, "skip", "is PC pe nahi"); continue; }
                    SetState(st, "run", "");
                    Thread.Sleep(350);   // dikhe ki kaam ho raha hai
                    bool bad = false;
                    try
                    {
                        st.Run(m =>
                        {
                            if (mainLog != null) mainLog(m);
                            if (m.IndexOf("nahi", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("[OK]", StringComparison.Ordinal) < 0) bad = true;
                        });
                    }
                    catch (Exception ex) { bad = true; if (mainLog != null) mainLog(st.Title + ": " + ex.Message); }
                    SetState(st, bad ? "fail" : "ok", bad ? "dekho log" : "ho gaya");
                }
            }).ContinueWith(t => BeginInvoke(new Action(ShowResult)));
        }

        private void ShowResult()
        {
            spin.Stop();
            ring.Done();
            Finished = true;
            header.Text = "BOOSTED!";
            header.ForeColor = Theme.Green;

            int score = pc.Score;
            var scoreLbl = new Label { Text = score.ToString(), Font = Theme.Semi(30F), ForeColor = score >= 70 ? Theme.Green : score >= 45 ? Theme.Amber : Theme.Red, Location = new Point(14, 18), Size = new Size(100, 56), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter };
            var scoreCap = new Label { Text = "PC SCORE", Font = Theme.Semi(8F), ForeColor = Theme.SubText, Location = new Point(14, 76), Size = new Size(100, 18), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter };
            var info = new Label
            {
                Text = string.Format("{0} PC  •  Free Fire MAX me lagbhag {1}\n(Fast Mode + x86 version ke saath)\n\nFast Mode ke liye PC RESTART zaroori hai.", pc.TierName, pc.ScoreRange),
                Font = Theme.F(9.5F), ForeColor = Theme.Text, Location = new Point(124, 14), Size = new Size(384, 86), BackColor = Color.Transparent
            };
            result.Controls.AddRange(new Control[] { scoreLbl, scoreCap, info });
            result.Visible = true;
            restartBtn.Visible = true; doneBtn.Visible = true;
            if (mainLog != null) mainLog("ONE-CLICK BOOST poora. PC Score " + score + ". Restart ke baad Fast Mode lagega.");
        }
    }
}
