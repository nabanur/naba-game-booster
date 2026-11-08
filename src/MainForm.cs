using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NabaGameBooster
{
    internal class MainForm : Form
    {
        private class Feature
        {
            public string Title;
            public Func<string, FeatureState> GetState;   // detail out via closure
            public Func<bool> ConfirmOn, ConfirmOff;
            public Action<Action<string>> On, Off;
            public Card Row; public Label Detail; public Pill Pill; public ToggleSwitch Toggle;
            public FeatureState State = FeatureState.Off;
            public string DetailText = "";
        }

        private readonly List<Feature> features = new List<Feature>();
        private readonly string nvidiaName;
        private bool busy;

        // Header / update
        private Card updateStrip;
        private Label updateText;
        private RoundButton updateNow, checkBtn;
        private ToggleSwitch autoToggle;
        private Updater.UpdateInfo pendingUpdate;
        private bool autoUpdateArmed;
        private int countdown = -1;

        // Game mode / system
        private RoundButton gmButton;
        private Label gmStatus, ramText, gpuText, bsText, planText;
        private Meter ramMeter;
        private TextBox logBox;
        private ToggleSwitch aiToggle;
        private RoundButton cleanBtn;
        private readonly Timer ramCleanTimer = new Timer { Interval = 5 * 60 * 1000 };
        private Panel tabOptimize, tabStartup, tabAim;
        private RoundButton[] tabButtons;
        private Panel startupList;
        private Label startupSummary;
        private bool startupLoaded;

        private static bool AutoUpdateEnabled
        {
            get { return Settings.AutoUpdate; }
            set { Settings.AutoUpdate = value; }
        }

        public MainForm()
        {
            Text = "Naba Game Booster";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(860, 770);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5F);
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            nvidiaName = Tweaks.NvidiaGpuName();

            BuildHeader();
            BuildGameModeCard();
            BuildSystemCard();
            BuildRightPanel();
            BuildLog();

            ramCleanTimer.Tick += (s2, e2) =>
            {
                if (!GameMode.IsOn || !Settings.RamCleaner || busy) return;
                Task.Factory.StartNew(() => Booster.CleanNow()).ContinueWith(t =>
                {
                    if (t.Exception == null && t.Result > 0.05) Log(string.Format("RAM Cleaner: +{0:N1} GB khali", t.Result));
                });
            };
            ramCleanTimer.Start();

            var fast = new Timer { Interval = 2000 };
            fast.Tick += (s, e) => { RefreshLive(); TickAutoUpdate(); };
            fast.Start();

            var slow = new Timer { Interval = 2 * 60 * 60 * 1000 }; // har 2 ghante update check
            slow.Tick += (s, e) => CheckUpdates(false);
            slow.Start();

            FormClosing += (s, e) =>
            {
                if (busy) { e.Cancel = true; Info("Abhi kaam chal raha hai - thoda ruko, phir band karo."); return; }
                if (GameMode.IsOn) { Cursor = Cursors.WaitCursor; try { GameMode.SafetyResume(Log); } catch { } }
            };

            Shown += (s, e) =>
            {
                GameMode.CleanStaleState();
                Log("Naba Game Booster v" + Updater.CurrentText + " shuru.");
                if (GameMode.IsOn) Log("Game Mode abhi bhi ON hai - khel ke STOP dabao.");
                RefreshLive();
                RefreshStates();
                CheckUpdates(false);
            };
        }

        // ================= Header =================
        private void BuildHeader()
        {
            var logo = new Logo { Location = new Point(24, 16) };
            var title = new Label { Text = "Naba Game Booster", Font = Theme.Semi(17F), ForeColor = Theme.Text, AutoSize = true, Location = new Point(78, 12), BackColor = Color.Transparent };
            var sub = new Label { Text = "BlueStacks / Free Fire ke liye FPS booster   •   v" + Updater.CurrentText, Font = Theme.F(9F), ForeColor = Theme.SubText, AutoSize = true, Location = new Point(80, 44), BackColor = Color.Transparent };
            Controls.AddRange(new Control[] { logo, title, sub });

            checkBtn = new RoundButton { Text = "Update check", Location = new Point(716, 20), Size = new Size(120, 34), Fill = Theme.Row, Font = Theme.Semi(9F) };
            checkBtn.Click += (s, e) => CheckUpdates(true);
            Controls.Add(checkBtn);

            var autoLbl = new Label { Text = "Auto update", ForeColor = Theme.SubText, Font = Theme.F(9F), AutoSize = true, Location = new Point(586, 28), BackColor = Color.Transparent };
            autoToggle = new ToggleSwitch { Location = new Point(666, 26), Size = new Size(40, 22) };
            autoToggle.Checked = AutoUpdateEnabled;
            autoToggle.Toggled += (s, e) =>
            {
                AutoUpdateEnabled = !AutoUpdateEnabled;
                autoToggle.Checked = AutoUpdateEnabled;
                Log(AutoUpdateEnabled ? "Auto update ON - naya version khud lag jayega." : "Auto update OFF - update ke liye button dabana hoga.");
            };
            Controls.Add(autoLbl); Controls.Add(autoToggle);

            updateStrip = new Card { Location = new Point(24, 76), Size = new Size(812, 42), Fill = Theme.Card, Radius = 10 };
            updateText = new Label { Text = "Update check ho raha hai...", ForeColor = Theme.SubText, Font = Theme.F(9.5F), Location = new Point(16, 11), Size = new Size(600, 22), BackColor = Color.Transparent };
            updateNow = new RoundButton { Text = "Abhi update karo", Location = new Point(652, 5), Size = new Size(152, 32), Fill = Theme.Green, Fill2 = Theme.GreenDark, Visible = false, Font = Theme.Semi(9F) };
            updateNow.Click += (s, e) => StartUpdate();
            updateStrip.Controls.Add(updateText);
            updateStrip.Controls.Add(updateNow);
            Controls.Add(updateStrip);
        }

        // ================= Game Mode card =================
        private void BuildGameModeCard()
        {
            var card = new Card { Location = new Point(24, 130), Size = new Size(300, 290) };
            card.Controls.Add(SectionLabel("GAME MODE", 18, 14));
            gmButton = new RoundButton { Location = new Point(18, 44), Size = new Size(264, 124), Radius = 16, Font = Theme.Semi(20F) };
            gmButton.Click += (s, e) =>
            {
                if (busy) return;
                if (GameMode.IsOn) RunJob("Game Mode STOP", GameMode.Off);
                else RunJob("Game Mode START", GameMode.On);
            };
            gmStatus = new Label { Location = new Point(18, 176), Size = new Size(264, 22), Font = Theme.Semi(9.5F), BackColor = Color.Transparent };
            var hint = new Label
            {
                Text = "Browser, Premiere, Telegram waghera pause - kaam jaisa ka taisa. STOP pe sab wahin se chalu.",
                Location = new Point(18, 200), Size = new Size(264, 36), ForeColor = Theme.SubText, Font = Theme.F(8.5F), BackColor = Color.Transparent
            };
            var aiRow = new Card { Location = new Point(14, 240), Size = new Size(272, 38), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
            aiRow.Controls.Add(new Label { Text = "Claude / ChatGPT bhi pause", Font = Theme.F(9F), ForeColor = Theme.Text, Location = new Point(12, 10), AutoSize = true, BackColor = Color.Transparent });
            aiToggle = new ToggleSwitch { Location = new Point(218, 8), Size = new Size(44, 22) };
            aiToggle.Checked = Settings.PauseAiApps;
            aiToggle.Toggled += (s, e) =>
            {
                if (GameMode.IsOn) { Info("Game Mode chal raha hai - STOP karke phir badlo."); return; }
                Settings.PauseAiApps = !Settings.PauseAiApps;
                aiToggle.Checked = Settings.PauseAiApps;
                Log(Settings.PauseAiApps ? "Claude / ChatGPT desktop bhi Game Mode me pause honge." : "Claude / ChatGPT desktop Game Mode me chalte rahenge.");
            };
            aiRow.Controls.Add(aiToggle);
            card.Controls.AddRange(new Control[] { gmButton, gmStatus, hint, aiRow });
            Controls.Add(card);
        }

        // ================= System card =================
        private void BuildSystemCard()
        {
            var card = new Card { Location = new Point(24, 432), Size = new Size(300, 186) };
            card.Controls.Add(SectionLabel("SYSTEM", 18, 14));
            ramText = InfoLabel(18, 40);
            ramMeter = new Meter { Location = new Point(18, 64), Size = new Size(264, 8) };
            gpuText = InfoLabel(18, 82);
            gpuText.Text = "GPU:  " + (nvidiaName ?? "NVIDIA card nahi mila");
            bsText = InfoLabel(18, 102);
            planText = InfoLabel(18, 122);
            cleanBtn = new RoundButton { Text = "RAM saaf karo", Location = new Point(18, 146), Size = new Size(264, 30), Fill = Theme.Row, Font = Theme.Semi(9F) };
            cleanBtn.Click += (s, e) =>
            {
                if (busy) return;
                RunJob("RAM saaf", log => { double got = Booster.CleanNow(); log(string.Format("Cache RAM saaf - {0:N1} GB khali hua.", Math.Max(0, got))); });
            };
            card.Controls.AddRange(new Control[] { ramText, ramMeter, gpuText, bsText, planText, cleanBtn });
            Controls.Add(card);
        }

        // ================= Right panel (tabs) =================
        private void BuildRightPanel()
        {
            var card = new Card { Location = new Point(340, 130), Size = new Size(496, 488) };
            Controls.Add(card);

            string[] names = { "OPTIMIZE", "STARTUP APPS", "AIM TRAINER" };
            tabButtons = new RoundButton[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var b = new RoundButton { Text = names[i], Location = new Point(14 + i * 158, 12), Size = new Size(152, 32), Radius = 9, Font = Theme.Semi(9F) };
                b.Click += (s, e) => SelectTab(idx);
                tabButtons[i] = b;
                card.Controls.Add(b);
            }

            tabOptimize = new Panel { Location = new Point(0, 52), Size = new Size(496, 430), BackColor = Color.Transparent };
            tabStartup = new Panel { Location = new Point(0, 52), Size = new Size(496, 430), BackColor = Color.Transparent, Visible = false };
            tabAim = new Panel { Location = new Point(0, 52), Size = new Size(496, 430), BackColor = Color.Transparent, Visible = false };
            card.Controls.Add(tabOptimize); card.Controls.Add(tabStartup); card.Controls.Add(tabAim);

            BuildOptimizations(tabOptimize);
            BuildStartup(tabStartup);
            BuildAim(tabAim);
            SelectTab(0);
        }

        private void SelectTab(int i)
        {
            tabOptimize.Visible = i == 0; tabStartup.Visible = i == 1; tabAim.Visible = i == 2;
            for (int k = 0; k < tabButtons.Length; k++)
            {
                tabButtons[k].Fill = k == i ? Theme.Accent : Theme.Row;
                tabButtons[k].Fill2 = k == i ? Theme.AccentDark : Color.Empty;
                tabButtons[k].ForeColor = k == i ? Color.FromArgb(20, 20, 26) : Theme.Text;
                tabButtons[k].Invalidate();
            }
            if (i == 1 && !startupLoaded) LoadStartup();
        }

        // ================= Optimize tab =================
        private void BuildOptimizations(Panel host)
        {
            string d;
            AddFeature(host, new Feature
            {
                Title = "Gaming Optimize",
                GetState = x => { var st = Status.GamingOptimize(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Yeh lagega:\n\n- Ultimate Performance power plan\n- Windows Game Mode ON\n- Xbox background recording OFF\n- GPU scheduling ON (restart ke baad)\n\nLagana hai?"),
                ConfirmOff = () => Ask("Power plan Balanced pe, Xbox recording aur GPU scheduling Windows default pe wapas.\n\nHatana hai?"),
                On = Tweaks.GamingOptimize, Off = Tweaks.GamingOptimizeUndo
            });
            AddFeature(host, new Feature
            {
                Title = "BlueStacks Optimize",
                GetState = x => { var st = Status.BlueStacks(out d); Pass(d); return st; },
                ConfirmOn = () =>
                {
                    if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks poora band karo (tray me bhi), phir switch dabao."); return false; }
                    int ram, cpus, pcRam, threads; Tweaks.BlueStacksPlan(out ram, out cpus, out pcRam, out threads);
                    return Ask(string.Format("Is PC me {0} GB RAM aur {1} CPU threads hain.\n\nBlueStacks me lagega:\n- RAM: {2} GB\n- CPU: {3} cores\n- Max FPS: 120\n- VSync: OFF\n\nPurani setting ka backup Documents me banega. Lagana hai?", pcRam, threads, ram / 1024, cpus));
                },
                ConfirmOff = () =>
                {
                    if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks poora band karo, phir switch dabao."); return false; }
                    if (Tweaks.LatestBlueStacksBackup() == null) { Info("Purani setting ka backup nahi mila - wapas nahi ja sakte."); return false; }
                    return Ask("BlueStacks ki purani setting (backup) wapas lagani hai?");
                },
                On = Tweaks.BlueStacksOptimize, Off = Tweaks.BlueStacksRestore
            });
            AddFeature(host, new Feature
            {
                Title = "BlueStacks Turbo",
                GetState = x => { var st = Booster.TurboState(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("BlueStacks ke liye:\n\n- Hamesha HIGH CPU priority\n- High performance GPU (RTX)\n- Fullscreen optimization OFF\n\nBlueStacks ek baar band karke kholna padega. Lagana hai?"),
                ConfirmOff = () => Ask("BlueStacks Turbo hata ke Windows default pe wapas?"),
                On = Booster.TurboOn, Off = Booster.TurboOff
            });
            AddFeature(host, new Feature
            {
                Title = "Defender Exclusion",
                GetState = x => { var st = Status.Defender(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Windows Defender BlueStacks ke folders scan nahi karega (game ke beech FPS drop kam).\nBaaki PC waise hi protected rahega.\n\nBlueStacks me anjaan/modded APK mat daalna. Lagana hai?"),
                ConfirmOff = () => Ask("BlueStacks ko dobara Defender scan me daalna hai?"),
                On = Tweaks.DefenderExclude, Off = Tweaks.DefenderRemove
            });
            AddFeature(host, new Feature
            {
                Title = "NVIDIA Boost",
                GetState = x => { var st = Status.Nvidia(nvidiaName, out d); Pass(d); return st; },
                ConfirmOn = () => Ask("NVIDIA me 2 setting lagengi:\n\n- Power management mode = Prefer maximum performance\n- Shader Cache Size = Unlimited\n\nLagana hai?"),
                ConfirmOff = () =>
                {
                    Info("NVIDIA ki setting wapas karne ke liye:\nNVIDIA Control Panel -> Manage 3D settings -> 'Restore' dabao.");
                    using (var k = Registry.CurrentUser.CreateSubKey(@"Software\NabaGameBooster")) k.SetValue("NvidiaApplied", 0, RegistryValueKind.DWord);
                    RefreshStates();
                    return false;
                },
                On = Tweaks.NvidiaApply, Off = null
            });
            AddFeature(host, new Feature
            {
                Title = "RAM Cleaner",
                GetState = x => { var st = Booster.RamCleanerState(out d); Pass(d); return st; },
                ConfirmOn = () => true, ConfirmOff = () => true,
                On = Booster.RamCleanerOn, Off = Booster.RamCleanerOff
            });
            AddFeature(host, new Feature
            {
                Title = "Timer 0.5ms (smooth)",
                GetState = x => { var st = Booster.TimerState(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Game Mode START pe Windows ka timer 0.5ms ho jayega - frames aur input smooth.\nWindows 11 pe poora asar ek baar restart ke baad.\n\nLagana hai?"),
                ConfirmOff = () => true,
                On = Booster.TimerOn, Off = Booster.TimerOff
            });
            AddFeature(host, new Feature
            {
                Title = "Mouse 1:1  (aim)",
                GetState = x => { var st = Status.Mouse(out d); Pass(d); return st; },
                ConfirmOn = () => true, ConfirmOff = () => true,
                On = Tweaks.MouseRaw, Off = Tweaks.MouseDefault
            });
            AddFeature(host, new Feature
            {
                Title = "Memory Integrity band (FPS+)",
                GetState = x => { var st = Status.MemoryIntegrityDisabled(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Memory Integrity band karne se games aur BlueStacks smooth chalte hain,\npar PC ki ek extra security layer hat jaati hai.\n\nRestart ke baad lagega. Karna hai?"),
                ConfirmOff = () => Ask("Memory Integrity wapas chalu karna hai? (zyada security, gaming thodi dheemi)\nRestart ke baad lagega."),
                On = log => Tweaks.MemoryIntegrity(false, log), Off = log => Tweaks.MemoryIntegrity(true, log)
            });
        }

        [ThreadStatic] private static string passDetail;
        private static void Pass(string d) { passDetail = d; }

        private void AddFeature(Control parent, Feature f)
        {
            int i = features.Count;
            var row = new Card { Location = new Point(14, 2 + i * 47), Size = new Size(468, 43), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
            var title = new Label { Text = f.Title, Font = Theme.Semi(9.5F), ForeColor = Theme.Text, Location = new Point(12, 4), AutoSize = true, BackColor = Color.Transparent };
            f.Detail = new Label { Text = "...", Font = Theme.F(8F), ForeColor = Theme.SubText, Location = new Point(12, 23), Size = new Size(300, 16), BackColor = Color.Transparent, AutoEllipsis = true };
            f.Pill = new Pill { Location = new Point(324, 11), Size = new Size(72, 21) };
            f.Toggle = new ToggleSwitch { Location = new Point(410, 10), Size = new Size(44, 23) };
            f.Toggle.Toggled += (s, e) => OnToggle(f);
            row.Controls.AddRange(new Control[] { title, f.Detail, f.Pill, f.Toggle });
            parent.Controls.Add(row);
            f.Row = row;
            features.Add(f);
        }

        private void OnToggle(Feature f)
        {
            if (busy || f.State == FeatureState.NotAvailable) return;
            bool turnOn = !(f.State == FeatureState.On || (f.State == FeatureState.Pending && f.Toggle.Checked));
            if (f.State == FeatureState.Partial) turnOn = true;
            if (turnOn)
            {
                if (f.ConfirmOn != null && !f.ConfirmOn()) return;
                RunJob(f.Title + " - lagao", f.On);
            }
            else
            {
                if (f.ConfirmOff != null && !f.ConfirmOff()) return;
                if (f.Off != null) RunJob(f.Title + " - hatao", f.Off);
            }
        }

        // ================= Startup tab =================
        private void BuildStartup(Panel host)
        {
            startupSummary = new Label { Location = new Point(16, 4), Size = new Size(380, 20), ForeColor = Theme.SubText, Font = Theme.F(9F), BackColor = Color.Transparent, Text = "Load ho raha hai..." };
            var refresh = new RoundButton { Text = "Refresh", Location = new Point(400, 0), Size = new Size(82, 26), Fill = Theme.Row, Font = Theme.Semi(8.5F), Radius = 8 };
            refresh.Click += (s, e) => LoadStartup();
            startupList = new Panel { Location = new Point(14, 32), Size = new Size(470, 392), AutoScroll = true, BackColor = Theme.Card };
            host.Controls.Add(startupSummary); host.Controls.Add(refresh); host.Controls.Add(startupList);
        }

        private void LoadStartup()
        {
            startupLoaded = true;
            startupSummary.Text = "Load ho raha hai...";
            Task.Factory.StartNew(() => StartupApps.List()).ContinueWith(t =>
            {
                if (IsDisposed || t.Exception != null) return;
                BeginInvoke(new Action(() => FillStartup(t.Result)));
            });
        }

        private void FillStartup(List<StartupItem> items)
        {
            startupList.SuspendLayout();
            foreach (Control c in startupList.Controls) c.Dispose();
            startupList.Controls.Clear();
            int y = 0;
            foreach (var it in items)
            {
                var item = it;
                var row = new Card { Location = new Point(0, y), Size = new Size(450, 44), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
                var name = new Label { Text = item.Name, Font = Theme.Semi(9.5F), ForeColor = Theme.Text, Location = new Point(12, 4), Size = new Size(300, 18), AutoEllipsis = true, BackColor = Color.Transparent };
                var cmd = new Label { Text = (item.Protected ? "Windows Security - band nahi kar sakte  •  " : item.Source + "  •  ") + item.Command, Font = Theme.F(7.5F), ForeColor = Theme.SubText, Location = new Point(12, 24), Size = new Size(330, 16), AutoEllipsis = true, BackColor = Color.Transparent };
                var tg = new ToggleSwitch { Location = new Point(394, 11), Size = new Size(44, 23), Enabled = !item.Protected };
                tg.Checked = item.Enabled;
                tg.Toggled += (s, e) =>
                {
                    bool want = !item.Enabled;
                    if (StartupApps.SetEnabled(item, want))
                    {
                        tg.Checked = item.Enabled;
                        Log((want ? "Startup ON: " : "Startup OFF: ") + item.Name + (want ? "" : "  (agli baar PC chalu hone pe nahi khulega)"));
                        UpdateStartupSummary(items);
                    }
                    else Log("Startup badal nahi paya: " + item.Name);
                };
                row.Controls.AddRange(new Control[] { name, cmd, tg });
                startupList.Controls.Add(row);
                y += 48;
            }
            startupList.ResumeLayout();
            UpdateStartupSummary(items);
        }

        private void UpdateStartupSummary(List<StartupItem> items)
        {
            int on = 0; foreach (var i in items) if (i.Enabled) on++;
            startupSummary.Text = string.Format("{0} apps PC chalu hote hi khulte hain  •  {1} band", on, items.Count - on);
        }

        // ================= Aim tab =================
        private void BuildAim(Panel host)
        {
            var trainer = new AimTrainer { Location = new Point(14, 34), Size = new Size(468, 390) };
            var movLbl = new Label { Text = "Chalte target", Font = Theme.F(9F), ForeColor = Theme.Text, Location = new Point(16, 6), AutoSize = true, BackColor = Color.Transparent };
            var mov = new ToggleSwitch { Location = new Point(110, 4), Size = new Size(40, 21) };
            mov.Toggled += (s, e) => { trainer.Moving = !trainer.Moving; mov.Checked = trainer.Moving; trainer.Invalidate(); };
            var smlLbl = new Label { Text = "Chhote target", Font = Theme.F(9F), ForeColor = Theme.Text, Location = new Point(176, 6), AutoSize = true, BackColor = Color.Transparent };
            var sml = new ToggleSwitch { Location = new Point(270, 4), Size = new Size(40, 21) };
            sml.Toggled += (s, e) => { trainer.Small = !trainer.Small; sml.Checked = trainer.Small; trainer.Invalidate(); };
            var tip2 = new Label { Text = "Match se pehle 2 round", Font = Theme.F(8.5F), ForeColor = Theme.SubText, Location = new Point(330, 7), AutoSize = true, BackColor = Color.Transparent };
            host.Controls.AddRange(new Control[] { movLbl, mov, smlLbl, sml, tip2, trainer });
        }

        // ================= Log =================
        private void BuildLog()
        {
            var card = new Card { Location = new Point(24, 630), Size = new Size(812, 128) };
            card.Controls.Add(SectionLabel("ACTIVITY", 18, 10));
            logBox = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                Location = new Point(18, 32), Size = new Size(778, 88), BackColor = Theme.Card, ForeColor = Color.FromArgb(170, 220, 180),
                Font = new Font("Consolas", 9F), TabStop = false
            };
            card.Controls.Add(logBox);
            Controls.Add(card);
        }

        // ================= Helpers =================
        private static Label SectionLabel(string text, int x, int y)
        {
            return new Label { Text = text, Font = Theme.Semi(9F), ForeColor = Theme.Accent, AutoSize = true, Location = new Point(x, y), BackColor = Color.Transparent };
        }

        private static Label InfoLabel(int x, int y)
        {
            return new Label { Font = Theme.F(9F), ForeColor = Theme.SubText, Location = new Point(x, y), Size = new Size(264, 18), BackColor = Color.Transparent, AutoEllipsis = true };
        }

        private bool Ask(string msg) { return MessageBox.Show(this, msg, "Naba Game Booster", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; }
        private void Info(string msg) { MessageBox.Show(this, msg, "Naba Game Booster", MessageBoxButtons.OK, MessageBoxIcon.Information); }

        private void Log(string msg)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), msg); return; }
            if (logBox.TextLength > 0) logBox.AppendText(Environment.NewLine);
            logBox.AppendText(string.IsNullOrEmpty(msg) ? "" : DateTime.Now.ToString("HH:mm") + "  " + msg);
        }

        private void SetBusy(bool b)
        {
            busy = b;
            gmButton.Enabled = !b;
            if (cleanBtn != null) cleanBtn.Enabled = !b;
            foreach (var f in features) f.Toggle.Enabled = !b && f.State != FeatureState.NotAvailable;
            Cursor = b ? Cursors.WaitCursor : Cursors.Default;
            RefreshGameButton();
        }

        private void RunJob(string name, Action<Action<string>> job)
        {
            if (busy) return;
            SetBusy(true);
            Log("---- " + name + " ----");
            Task.Factory.StartNew(() =>
            {
                try { job(Log); }
                catch (Exception ex) { Log("Gadbad: " + ex.Message); }
            }).ContinueWith(t => BeginInvoke(new Action(() => { SetBusy(false); RefreshLive(); RefreshStates(); })));
        }

        // ================= Refresh =================
        private void RefreshGameButton()
        {
            bool on = GameMode.IsOn;
            if (on)
            {
                gmButton.Text = "STOP";
                gmButton.SubText = "dabao - sab apps wapas chalu";
                gmButton.Fill = Theme.Red; gmButton.Fill2 = Theme.RedDark;
                int n = GameMode.PausedCount;
                gmStatus.Text = "ON  •  " + n + " process paused";
                gmStatus.ForeColor = Theme.Green;
            }
            else
            {
                gmButton.Text = "START";
                gmButton.SubText = "apps pause + RAM khali";
                gmButton.Fill = Theme.Green; gmButton.Fill2 = Theme.GreenDark;
                gmStatus.Text = "OFF  •  sab normal chal raha hai";
                gmStatus.ForeColor = Theme.SubText;
            }
            gmButton.Invalidate();
        }

        private void RefreshLive()
        {
            double free = Native.FreeRamGB(), total = Native.TotalRamGB();
            int load = Native.RamLoadPercent();
            ramText.Text = string.Format("RAM:  {0}% use me   •   {1:N1} / {2:N1} GB khali", load, free, total);
            ramMeter.Value = load;
            bool bs = Status.BlueStacksRunning();
            bsText.Text = "BlueStacks:  " + (Tweaks.BlueStacksInstalled ? (bs ? "chal raha hai" : "band hai") : "install nahi hai");
            bsText.ForeColor = bs ? Theme.Green : Theme.SubText;
            if (!busy) RefreshGameButton();
        }

        private void RefreshStates()
        {
            foreach (var f in features) { f.Pill.SetLoading(); }
            Task.Factory.StartNew(() =>
            {
                var results = new List<Tuple<Feature, FeatureState, string>>();
                foreach (var f in features)
                {
                    FeatureState st; string det;
                    try { passDetail = ""; st = f.GetState(null); det = passDetail; }
                    catch { st = FeatureState.Off; det = ""; }
                    results.Add(Tuple.Create(f, st, det));
                }
                string g; string plan = Status.ActivePowerPlan(out g);
                return Tuple.Create(results, plan);
            }).ContinueWith(t =>
            {
                if (IsDisposed || t.Exception != null) return;
                BeginInvoke(new Action(() =>
                {
                    foreach (var r in t.Result.Item1)
                    {
                        var f = r.Item1;
                        f.State = r.Item2;
                        f.DetailText = r.Item3;
                        f.Pill.Set(f.State);
                        f.Detail.Text = f.DetailText;
                        bool wantOn = f.State == FeatureState.On || (f.State == FeatureState.Pending && f.DetailText.Contains("band hoga"));
                        f.Toggle.Checked = wantOn;
                        f.Toggle.Enabled = !busy && f.State != FeatureState.NotAvailable;
                    }
                    planText.Text = "Power plan:  " + t.Result.Item2;
                }));
            });
        }

        // ================= Updates =================
        private void CheckUpdates(bool manual)
        {
            if (manual) { updateText.Text = "Update check ho raha hai..."; updateText.ForeColor = Theme.SubText; }
            Task.Factory.StartNew(() => Updater.Check()).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    if (t.Exception != null)
                    {
                        updateText.Text = "Update check nahi ho paya (internet?) - baad me dobara try hoga.";
                        updateText.ForeColor = Theme.Amber;
                        if (manual) Log("Update check fail: " + t.Exception.GetBaseException().Message);
                        return;
                    }
                    var info = t.Result;
                    if (info == null)
                    {
                        updateText.Text = "✓  Aap latest version pe ho  (v" + Updater.CurrentText + ")";
                        updateText.ForeColor = Theme.Green;
                        updateNow.Visible = false;
                        if (manual) Log("Aap latest version pe ho.");
                        return;
                    }
                    pendingUpdate = info;
                    updateStrip.Fill = Color.FromArgb(20, 54, 34);
                    updateStrip.Border = Theme.GreenDark;
                    updateStrip.Invalidate();
                    updateText.ForeColor = Theme.Text;
                    updateNow.Visible = true;
                    Log("Naya version v" + info.VersionText + " mila." + (string.IsNullOrWhiteSpace(info.Notes) ? "" : "  " + info.Notes.Replace("\r", "").Replace("\n", "  ")));
                    if (AutoUpdateEnabled)
                    {
                        autoUpdateArmed = true;
                        updateText.Text = "Naya version v" + info.VersionText + " - auto update hoga" + (GameMode.IsOn ? " (Game Mode STOP hone ke baad)" : "");
                        if (!GameMode.IsOn && !busy) countdown = 8;
                    }
                    else
                    {
                        updateText.Text = "Naya version v" + info.VersionText + " aaya hai!";
                    }
                }));
            });
        }

        private void TickAutoUpdate()
        {
            if (!autoUpdateArmed || pendingUpdate == null) return;
            if (GameMode.IsOn || busy) { countdown = -1; return; }
            if (countdown < 0) countdown = 8;
            countdown -= 2;
            if (countdown > 0)
            {
                updateText.Text = string.Format("Naya version v{0} - {1} second me auto update shuru...", pendingUpdate.VersionText, countdown);
                return;
            }
            autoUpdateArmed = false;
            StartUpdate();
        }

        private void StartUpdate()
        {
            if (pendingUpdate == null || busy) return;
            if (GameMode.IsOn) { Info("Pehle Game Mode STOP karo, phir update hoga."); return; }
            autoUpdateArmed = false;
            SetBusy(true);
            updateNow.Enabled = false;
            var info = pendingUpdate;
            Log("Update v" + info.VersionText + " download ho raha hai...");
            Task.Factory.StartNew(() =>
            {
                Updater.DownloadAndRun(info, pct => BeginInvoke(new Action(() => updateText.Text = "Download: " + pct + "%")));
            }).ContinueWith(t => BeginInvoke(new Action(() =>
            {
                if (t.Exception != null)
                {
                    SetBusy(false);
                    updateNow.Enabled = true;
                    updateText.Text = "Update download nahi hua - 'Abhi update karo' dobara dabao.";
                    Log("Update fail: " + t.Exception.GetBaseException().Message);
                    return;
                }
                updateText.Text = "Install ho raha hai - app band hokar naye version me khulega...";
                Log("Install shuru - app naye version me khud khulega.");
                busy = false;
                Application.Exit();
            })));
        }
    }
}
