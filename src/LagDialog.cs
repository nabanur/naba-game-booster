using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NabaGameBooster
{
    /// <summary>LAG FIX: har check chalta dikhta hai, jo theek ho sakta hai khud theek, baaki "aapko karna hai".</summary>
    internal class LagDialog : Form
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

        public LagDialog(PcProfile profile, Action<string> mainLogger)
        {
            pc = profile; mainLog = mainLogger;
            Text = "Lag Fix";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(560, 700);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5F);
            DoubleBuffered = true;

            header = new Label { Text = "Lag dhoondh raha hoon...", Font = Theme.Semi(16F), ForeColor = Theme.Accent, AutoSize = true, Location = new Point(22, 34) };
            sub = new Label
            {
                Text = "High FPS par bhi laggy feel ki har wajah check ho rahi hai",
                Font = Theme.F(9F), ForeColor = Theme.SubText, Location = new Point(24, 70), Size = new Size(380, 40), AutoEllipsis = true
            };
            ring = new BoostRing { Location = new Point(418, 8), RunText = "SCANNING", DoneText = "FIXED" };
            Controls.Add(ring);
            Controls.Add(header); Controls.Add(sub);

            list = new Panel { Location = new Point(20, 140), Size = new Size(520, 380), BackColor = Theme.Bg };
            Controls.Add(list);

            result = new Card { Location = new Point(20, 530), Size = new Size(520, 110), Visible = false };
            Controls.Add(result);

            restartBtn = new RoundButton { Text = "NVIDIA Control Panel kholo", Location = new Point(20, 652), Size = new Size(250, 38), Fill = Theme.Accent, Fill2 = Theme.AccentDark, ForeColor = Color.FromArgb(20, 20, 26), Font = Theme.Semi(10F), Visible = false };
            restartBtn.Click += (s, e) => OpenNvcp();
            doneBtn = new RoundButton { Text = "Theek hai", Location = new Point(290, 652), Size = new Size(250, 38), Fill = Theme.Row, Font = Theme.Semi(10F), Visible = false };
            doneBtn.Click += (s, e) => Close();
            Controls.Add(restartBtn); Controls.Add(doneBtn);

            BuildSteps();
            spin.Tick += (s, e) => { spinIdx = (spinIdx + 1) % 4; foreach (var st in steps) if (st.State == "run") st.Icon.Text = SpinChars[spinIdx]; };
            FormClosing += (s, e) => { if (!Finished) e.Cancel = true; };   // beech me band nahi
            Shown += (s, e) => RunAll();
        }

        private void BuildSteps()
        {
            LagFix.Todo.Clear(); LagFix.SuggestNvcp = false;
            bool emu = Emulators.AnyInstalled;
            Add("Monitor refresh rate (Hz)", () => true, LagFix.RefreshRate);
            Add("Cable + FreeSync / G-SYNC", () => true, LagFix.Cable);
            Add("BlueStacks full screen", () => emu, LagFix.FullScreen);
            Add("FPS vs monitor (jhatke)", () => emu, LagFix.Fps);
            Add("Background apps (CPU)", () => true, LagFix.Background);
            Add("Internet - ping / jitter / Wi-Fi", () => true, LagFix.Network);
            Add("NVIDIA low latency", () => true, LagFix.Nvidia);
            Add("BlueStacks Fast Mode", () => emu, LagFix.FastMode);

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
                    bool fixd = false;
                    SetState(st, "run", "");
                    Thread.Sleep(350);   // dikhe ki kaam ho raha hai
                    bool bad = false;
                    try
                    {
                        st.Run(m =>
                        {
                            if (mainLog != null) mainLog(m);
                            if (m.IndexOf("[OK]", StringComparison.Ordinal) < 0) bad = true;
                            if (m.IndexOf("FIX:", StringComparison.Ordinal) >= 0) fixd = true;
                        });
                    }
                    catch (Exception ex) { bad = true; if (mainLog != null) mainLog(st.Title + ": " + ex.Message); }
                    SetState(st, bad ? "fail" : "ok", bad ? "dhyan do" : fixd ? "theek kiya" : "sahi hai");
                }
            }).ContinueWith(t => BeginInvoke(new Action(ShowResult)));
        }

        private void ShowResult()
        {
            spin.Stop();
            ring.Done();
            Finished = true;
            int fixedN = 0; foreach (var x in steps) if (x.Sub.Text == "theek kiya") fixedN++;
            var todo = new List<string>(LagFix.Todo.Distinct());
            header.Text = todo.Count == 0 ? "SAB THEEK!" : "LAG FIX HO GAYA";
            header.ForeColor = Theme.Green;
            sub.Text = fixedN + " cheez app ne khud theek ki" + (todo.Count > 0 ? "  •  " + todo.Count + " aapko karni hai" : "");

            var sb = new System.Text.StringBuilder();
            if (todo.Count == 0) sb.Append("Lag ki koi wajah nahi bachi. Game full screen me, stable FPS pe khelo.");
            else { sb.Append("AAPKO KARNA HAI:\n"); int k = 1; foreach (var t in todo.Take(4)) sb.Append(k++ + ". " + t + "\n"); }
            var info = new Label { Text = sb.ToString().TrimEnd(), Font = Theme.F(9F), ForeColor = Theme.Text, Location = new Point(14, 10), Size = new Size(492, 92), BackColor = Color.Transparent };
            result.Controls.Add(info);
            result.Visible = true;
            restartBtn.Visible = LagFix.SuggestNvcp; doneBtn.Visible = true;
            if (!LagFix.SuggestNvcp) doneBtn.Location = new Point(155, 652);
            if (mainLog != null)
            {
                mainLog("LAG FIX poora. " + fixedN + " cheez khud theek ki.");
                foreach (var t in todo) mainLog("  -> Aapko karna hai: " + t);
            }
        }

        private void OpenNvcp()
        {
            string[] paths = {
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\NVIDIA Corporation\Control Panel Client\nvcplui.exe"),
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\nvcplui.exe") };
            foreach (var p in paths)
                if (System.IO.File.Exists(p)) { try { Process.Start(p); return; } catch { } }
            try { Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel") { UseShellExecute = false }); }
            catch { MessageBox.Show(this, "Desktop pe right-click karke \"NVIDIA Control Panel\" kholo.", "NVIDIA"); }
        }
    }
}
