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

        private static bool AutoUpdateEnabled
        {
            get { var v = Status.Reg(Registry.CurrentUser, @"Software\NabaGameBooster", "AutoUpdate"); return v == null || Convert.ToInt32(v) == 1; }
            set { using (var k = Registry.CurrentUser.CreateSubKey(@"Software\NabaGameBooster")) k.SetValue("AutoUpdate", value ? 1 : 0, RegistryValueKind.DWord); }
        }

        public MainForm()
        {
            Text = "Naba Game Booster";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(860, 660);
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
            BuildOptimizations();
            BuildLog();

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
            var card = new Card { Location = new Point(24, 130), Size = new Size(300, 262) };
            card.Controls.Add(SectionLabel("GAME MODE", 18, 14));
            gmButton = new RoundButton { Location = new Point(18, 44), Size = new Size(264, 124), Radius = 16, Font = Theme.Semi(20F) };
            gmButton.Click += (s, e) =>
            {
                if (busy) return;
                if (GameMode.IsOn) RunJob("Game Mode STOP", GameMode.Off);
                else RunJob("Game Mode START", GameMode.On);
            };
            gmStatus = new Label { Location = new Point(18, 178), Size = new Size(264, 22), Font = Theme.Semi(9.5F), BackColor = Color.Transparent };
            var hint = new Label
            {
                Text = "START: browser, Premiere, Telegram, Codex waghera pause - kaam jaisa ka taisa rahega. STOP: sab wahin se chalu.",
                Location = new Point(18, 202), Size = new Size(264, 52), ForeColor = Theme.SubText, Font = Theme.F(8.5F), BackColor = Color.Transparent
            };
            card.Controls.Add(gmButton); card.Controls.Add(gmStatus); card.Controls.Add(hint);
            Controls.Add(card);
        }

        // ================= System card =================
        private void BuildSystemCard()
        {
            var card = new Card { Location = new Point(24, 404), Size = new Size(300, 148) };
            card.Controls.Add(SectionLabel("SYSTEM", 18, 14));
            ramText = InfoLabel(18, 40);
            ramMeter = new Meter { Location = new Point(18, 64), Size = new Size(264, 8) };
            gpuText = InfoLabel(18, 80);
            gpuText.Text = "GPU:  " + (nvidiaName ?? "NVIDIA card nahi mila");
            bsText = InfoLabel(18, 100);
            planText = InfoLabel(18, 120);
            card.Controls.AddRange(new Control[] { ramText, ramMeter, gpuText, bsText, planText });
            Controls.Add(card);
        }

        // ================= Optimizations =================
        private void BuildOptimizations()
        {
            var card = new Card { Location = new Point(340, 130), Size = new Size(496, 422) };
            card.Controls.Add(SectionLabel("OPTIMIZATIONS", 18, 14));
            card.Controls.Add(new Label { Text = "Switch dabao - setting lagegi ya hategi", ForeColor = Theme.SubText, Font = Theme.F(8.5F), AutoSize = true, Location = new Point(140, 15), BackColor = Color.Transparent });
            Controls.Add(card);

            string d;
            AddFeature(card, new Feature
            {
                Title = "Gaming Optimize",
                GetState = x => { var st = Status.GamingOptimize(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Yeh lagega:\n\n- Ultimate Performance power plan\n- Windows Game Mode ON\n- Xbox background recording OFF\n- GPU scheduling ON (restart ke baad)\n\nLagana hai?"),
                ConfirmOff = () => Ask("Power plan Balanced pe, Xbox recording aur GPU scheduling Windows default pe wapas.\n\nHatana hai?"),
                On = Tweaks.GamingOptimize, Off = Tweaks.GamingOptimizeUndo
            });
            AddFeature(card, new Feature
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
            AddFeature(card, new Feature
            {
                Title = "Defender Exclusion",
                GetState = x => { var st = Status.Defender(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Windows Defender BlueStacks ke folders scan nahi karega (game ke beech FPS drop kam).\nBaaki PC waise hi protected rahega.\n\nBlueStacks me anjaan/modded APK mat daalna. Lagana hai?"),
                ConfirmOff = () => Ask("BlueStacks ko dobara Defender scan me daalna hai?"),
                On = Tweaks.DefenderExclude, Off = Tweaks.DefenderRemove
            });
            AddFeature(card, new Feature
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
            AddFeature(card, new Feature
            {
                Title = "Mouse 1:1  (aim)",
                GetState = x => { var st = Status.Mouse(out d); Pass(d); return st; },
                ConfirmOn = () => true, ConfirmOff = () => true,
                On = Tweaks.MouseRaw, Off = Tweaks.MouseDefault
            });
            AddFeature(card, new Feature
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

        private void AddFeature(Card parent, Feature f)
        {
            int i = features.Count;
            var row = new Card { Location = new Point(14, 44 + i * 62), Size = new Size(468, 56), Fill = Theme.Row, Border = Theme.Row, Radius = 10 };
            var title = new Label { Text = f.Title, Font = Theme.Semi(10F), ForeColor = Theme.Text, Location = new Point(14, 8), AutoSize = true, BackColor = Color.Transparent };
            f.Detail = new Label { Text = "...", Font = Theme.F(8.5F), ForeColor = Theme.SubText, Location = new Point(14, 30), Size = new Size(300, 18), BackColor = Color.Transparent, AutoEllipsis = true };
            f.Pill = new Pill { Location = new Point(322, 17) };
            f.Toggle = new ToggleSwitch { Location = new Point(408, 16), Size = new Size(46, 24) };
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

        // ================= Log =================
        private void BuildLog()
        {
            var card = new Card { Location = new Point(24, 564), Size = new Size(812, 82) };
            card.Controls.Add(SectionLabel("ACTIVITY", 18, 10));
            logBox = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                Location = new Point(18, 32), Size = new Size(778, 42), BackColor = Theme.Card, ForeColor = Color.FromArgb(170, 220, 180),
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
