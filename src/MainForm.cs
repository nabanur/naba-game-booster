using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NabaGameBooster
{
    internal class MainForm : Form
    {
        // Rang
        private static readonly Color Bg = Color.FromArgb(20, 20, 26);
        private static readonly Color Card = Color.FromArgb(38, 38, 48);
        private static readonly Color CardHover = Color.FromArgb(56, 56, 70);
        private static readonly Color Orange = Color.FromArgb(255, 166, 48);
        private static readonly Color Green = Color.FromArgb(32, 140, 72);
        private static readonly Color Red = Color.FromArgb(170, 52, 52);
        private static readonly Color Muted = Color.FromArgb(160, 160, 172);

        private readonly Label status;
        private readonly Label gmBadge;
        private readonly TextBox logBox;
        private readonly Panel updatePanel;
        private readonly Label updateLabel;
        private readonly Button updateBtn;
        private readonly Button btnOn, btnOff, btnNvidia;
        private readonly List<Button> allButtons = new List<Button>();
        private readonly ToolTip tip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 300 };
        private readonly string nvidiaName;
        private Updater.UpdateInfo pendingUpdate;
        private bool busy;

        public MainForm()
        {
            Text = "Naba Game Booster";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(560, 720);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            nvidiaName = Tweaks.NvidiaGpuName();

            // ---- Header ----
            var title = new Label { Text = "NABA GAME BOOSTER", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = Orange, AutoSize = true, Location = new Point(18, 12) };
            var ver = new Label { Text = "v" + Updater.CurrentText, ForeColor = Muted, AutoSize = true, Location = new Point(480, 24) };
            status = new Label { ForeColor = Color.FromArgb(150, 220, 160), Location = new Point(20, 54), Size = new Size(520, 22) };
            var gpu = new Label { ForeColor = Muted, Location = new Point(20, 76), Size = new Size(520, 22), Text = "GPU: " + (nvidiaName ?? "NVIDIA card nahi mila") };
            Controls.AddRange(new Control[] { title, ver, status, gpu });

            // ---- Update banner ----
            updatePanel = new Panel { Location = new Point(18, 104), Size = new Size(524, 44), BackColor = Color.FromArgb(40, 70, 40), Visible = false };
            updateLabel = new Label { ForeColor = Color.White, Location = new Point(10, 12), Size = new Size(360, 22) };
            updateBtn = MakeButton("Update karo", new Point(380, 6), new Size(136, 32), Green);
            updateBtn.Click += (s, e) => DoUpdate();
            updatePanel.Controls.Add(updateLabel);
            updatePanel.Controls.Add(updateBtn);
            Controls.Add(updatePanel);

            // ---- Game Mode ----
            int y = 158;
            AddHeader("GAME MODE  (har baar)", y); y += 28;
            btnOn = MakeButton("GAME MODE  ON", new Point(18, y), new Size(256, 62), Green);
            btnOn.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            btnOff = MakeButton("GAME MODE  OFF", new Point(286, y), new Size(256, 62), Red);
            btnOff.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            Controls.Add(btnOn); Controls.Add(btnOff);
            tip.SetToolTip(btnOn, "Browser, Adobe background, Telegram, Codex, Drive waghera BAND.\nRAM khali + BlueStacks ko HIGH priority.\nEditing apps (Premiere/Animate) save ka poochenge.");
            tip.SetToolTip(btnOff, "Game Mode me jo apps band hue the, wo wapas khul jaate hain.");
            btnOn.Click += (s, e) => RunJob("Game Mode ON", GameMode.On);
            btnOff.Click += (s, e) => RunJob("Game Mode OFF", GameMode.Off);
            y += 70;
            gmBadge = new Label { ForeColor = Muted, Location = new Point(20, y), Size = new Size(520, 20) };
            Controls.Add(gmBadge);
            y += 30;

            // ---- One-time settings ----
            AddHeader("EK BAAR KI SETTING  (naye PC pe ek baar)", y); y += 28;
            var b1 = Grid("Gaming Optimize", 0, y);
            var b2 = Grid("BlueStacks Optimize", 1, y); y += 48;
            var b3 = Grid("Defender Exclusion", 0, y);
            btnNvidia = Grid(nvidiaName != null ? "NVIDIA Setting" : "NVIDIA (card nahi mila)", 1, y); y += 48;
            var b5 = Grid("Mouse 1:1  (aim)", 0, y);
            var b6 = Grid("Mouse Default", 1, y); y += 48;
            var b7 = Grid("Memory Integrity OFF", 0, y);
            var b8 = Grid("Memory Integrity ON", 1, y); y += 54;

            tip.SetToolTip(b1, "Ultimate Performance power plan, Windows Game Mode ON,\nXbox background recording OFF, GPU scheduling ON.");
            tip.SetToolTip(b2, "PC ki RAM dekh ke BlueStacks ki RAM, CPU, FPS (120) set karta hai.\nBlueStacks band hona chahiye. Purani setting ka backup banta hai.");
            tip.SetToolTip(b3, "Windows Defender BlueStacks ke folder scan nahi karega -\ngame ke beech achanak FPS drop kam honge.");
            tip.SetToolTip(btnNvidia, "NVIDIA: Power management = Prefer maximum performance\nShader Cache Size = Unlimited");
            tip.SetToolTip(b5, "Mouse acceleration OFF, speed 10 - jitna haath hile utna hi crosshair.");
            tip.SetToolTip(b6, "Mouse ko Windows ki default setting pe wapas.");
            tip.SetToolTip(b7, "Games/BlueStacks smooth, par PC ki ek security layer kam. Restart chahiye.");
            tip.SetToolTip(b8, "Security wapas ON. Restart chahiye.");

            b1.Click += (s, e) =>
            {
                if (Ask("Yeh lagega:\n\n- Ultimate Performance power plan\n- Windows Game Mode ON\n- Xbox background recording OFF\n- GPU scheduling ON (restart ke baad)\n\nLagana hai?"))
                    RunJob("Gaming Optimize", Tweaks.GamingOptimize);
            };
            b2.Click += (s, e) =>
            {
                if (!Tweaks.BlueStacksInstalled) { Info("Is PC me BlueStacks 5 nahi mila."); return; }
                if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks poora band karo (tray me bhi), phir dobara dabao."); return; }
                int ram, cpus, pcRam, threads;
                Tweaks.BlueStacksPlan(out ram, out cpus, out pcRam, out threads);
                if (Ask(string.Format("Is PC me {0} GB RAM aur {1} CPU threads hain.\n\nBlueStacks me lagega:\n- RAM: {2} GB\n- CPU: {3} cores\n- Max FPS: 120\n- VSync: OFF\n\nPurani setting ka backup Documents me banega. Lagana hai?", pcRam, threads, ram / 1024, cpus)))
                    RunJob("BlueStacks Optimize", Tweaks.BlueStacksOptimize);
            };
            b3.Click += (s, e) =>
            {
                if (Ask("Windows Defender BlueStacks ke folders scan nahi karega.\nBaaki PC waise hi protected rahega.\n\nBlueStacks me anjaan/modded APK mat daalna.\n\nLagana hai?"))
                    RunJob("Defender Exclusion", Tweaks.DefenderExclude);
            };
            btnNvidia.Enabled = nvidiaName != null;
            btnNvidia.Click += (s, e) =>
            {
                if (Ask("NVIDIA me 2 setting lagengi:\n\n- Power management mode = Prefer maximum performance\n- Shader Cache Size = Unlimited\n\nLagana hai?"))
                    RunJob("NVIDIA Setting", Tweaks.NvidiaApply);
            };
            b5.Click += (s, e) => RunJob("Mouse 1:1", Tweaks.MouseRaw);
            b6.Click += (s, e) => RunJob("Mouse Default", Tweaks.MouseDefault);
            b7.Click += (s, e) =>
            {
                if (Ask("Memory Integrity OFF karne se games aur BlueStacks smooth chalte hain,\npar PC ki ek extra security layer hat jaati hai.\n\nRestart ke baad lagega. Karna hai?"))
                    RunJob("Memory Integrity OFF", log => Tweaks.MemoryIntegrity(false, log));
            };
            b8.Click += (s, e) =>
            {
                if (Ask("Memory Integrity ON karna hai? (zyada security, gaming thodi dheemi)\nRestart ke baad lagega."))
                    RunJob("Memory Integrity ON", log => Tweaks.MemoryIntegrity(true, log));
            };

            // ---- Log ----
            logBox = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Location = new Point(18, y), Size = new Size(524, 720 - y - 16),
                BackColor = Color.FromArgb(10, 10, 14), ForeColor = Color.FromArgb(190, 255, 200),
                BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.5F)
            };
            Controls.Add(logBox);

            // ---- Status timer ----
            var timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (s, e) => RefreshStatus();
            timer.Start();

            Shown += (s, e) =>
            {
                RefreshStatus();
                Log("Naba Game Booster v" + Updater.CurrentText + " taiyaar hai. Button pe mouse rakhoge to batayega kya karta hai.");
                CheckUpdateAsync();
            };
        }

        // ---------------- UI helpers ----------------
        private void AddHeader(string text, int y)
        {
            Controls.Add(new Label { Text = text, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(120, 180, 255), AutoSize = true, Location = new Point(18, y) });
        }

        private Button Grid(string text, int col, int y)
        {
            var b = MakeButton(text, new Point(col == 0 ? 18 : 286, y), new Size(256, 40), Card);
            Controls.Add(b);
            return b;
        }

        private Button MakeButton(string text, Point loc, Size size, Color color)
        {
            var b = new Button
            {
                Text = text, Location = loc, Size = size, FlatStyle = FlatStyle.Flat,
                BackColor = color, ForeColor = Color.White, Cursor = Cursors.Hand, UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = color == Card ? CardHover : ControlPaint.Light(color, 0.15f);
            allButtons.Add(b);
            return b;
        }

        private bool Ask(string msg)
        {
            return MessageBox.Show(this, msg, "Naba Game Booster", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void Info(string msg)
        {
            MessageBox.Show(this, msg, "Naba Game Booster", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Log(string msg)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), msg); return; }
            logBox.AppendText(msg + Environment.NewLine);
        }

        private void RefreshStatus()
        {
            status.Text = string.Format("RAM khali: {0:N1} / {1:N1} GB   ({2}% use me)", Native.FreeRamGB(), Native.TotalRamGB(), Native.RamLoadPercent());
            bool on = GameMode.IsOn;
            gmBadge.Text = on ? "Game Mode: ON  - khel ke 'GAME MODE OFF' dabana" : "Game Mode: OFF";
            gmBadge.ForeColor = on ? Color.FromArgb(120, 230, 140) : Muted;
        }

        private void SetBusy(bool b)
        {
            busy = b;
            foreach (var btn in allButtons)
            {
                if (btn == btnNvidia) btn.Enabled = !b && nvidiaName != null;
                else btn.Enabled = !b;
            }
            Cursor = b ? Cursors.WaitCursor : Cursors.Default;
        }

        private void RunJob(string name, Action<Action<string>> job)
        {
            if (busy) return;
            SetBusy(true);
            Log("");
            Log("---- " + name + " ----");
            Task.Factory.StartNew(() =>
            {
                try { job(Log); }
                catch (Exception ex) { Log("Gadbad: " + ex.Message); }
            }).ContinueWith(t => BeginInvoke(new Action(() => { SetBusy(false); RefreshStatus(); })));
        }

        // ---------------- Update ----------------
        private void CheckUpdateAsync()
        {
            Task.Factory.StartNew(() =>
            {
                try { return Updater.Check(); } catch { return null; }
            }).ContinueWith(t =>
            {
                var info = t.Result;
                if (info == null || IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    pendingUpdate = info;
                    updateLabel.Text = "Naya version v" + info.VersionText + " aaya hai!";
                    updatePanel.Visible = true;
                    Log("Naya version v" + info.VersionText + " available hai - upar 'Update karo' dabao.");
                    if (!string.IsNullOrWhiteSpace(info.Notes)) Log("Kya naya hai: " + info.Notes.Replace("\r", "").Replace("\n", " | "));
                }));
            });
        }

        private void DoUpdate()
        {
            if (pendingUpdate == null || busy) return;
            SetBusy(true);
            updateBtn.Text = "Download...";
            var info = pendingUpdate;
            Task.Factory.StartNew(() =>
            {
                Updater.DownloadAndRun(info, pct => BeginInvoke(new Action(() => updateBtn.Text = pct + "%")));
            }).ContinueWith(t => BeginInvoke(new Action(() =>
            {
                if (t.Exception != null)
                {
                    SetBusy(false);
                    updateBtn.Text = "Update karo";
                    Log("Update download nahi hua: " + t.Exception.GetBaseException().Message);
                    return;
                }
                Log("Update install ho raha hai - app band hokar naye version me khulega.");
                Application.Exit();
            })));
        }
    }
}
