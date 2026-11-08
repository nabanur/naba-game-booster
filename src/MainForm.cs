using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private readonly ToolTip tip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 300 };
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
        private Panel scroll;
        private int startupTop;
        private readonly List<Control> startupRows = new List<Control>();
        private Label startupSummary;
        private Label fpsBefore, fpsAfter, fpsDiff;
        private RoundButton fpsBtn1, fpsBtn2, fpsSave;

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
                LoadStartup();
                CheckUpdates(false);
            };
        }

        // ================= Header =================
        private void BuildHeader()
        {
            var logo = new Logo { Location = new Point(24, 16) };
            var title = new Label { Text = "Naba Game Booster", Font = Theme.Semi(17F), ForeColor = Theme.Text, AutoSize = true, Location = new Point(78, 12), BackColor = Color.Transparent };
            var sub = new Label { Text = "BlueStacks / MSI App Player  •  Free Fire FPS booster   •   v" + Updater.CurrentText, Font = Theme.F(9F), ForeColor = Theme.SubText, AutoSize = true, Location = new Point(80, 44), BackColor = Color.Transparent };
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
            var card = new Card { Location = new Point(24, 130), Size = new Size(300, 332) };
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
            var svRow = new Card { Location = new Point(14, 282), Size = new Size(272, 38), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
            svRow.Controls.Add(new Label { Text = "Windows Update / Search rok do", Font = Theme.F(9F), ForeColor = Theme.Text, Location = new Point(12, 10), AutoSize = true, BackColor = Color.Transparent });
            var svToggle = new ToggleSwitch { Location = new Point(218, 8), Size = new Size(44, 22) };
            svToggle.Checked = Settings.PauseServices;
            svToggle.Toggled += (s, e) =>
            {
                if (GameMode.IsOn) { Info("Game Mode chal raha hai - STOP karke phir badlo."); return; }
                Settings.PauseServices = !Settings.PauseServices;
                svToggle.Checked = Settings.PauseServices;
                Log(Settings.PauseServices ? "Game Mode me Windows Update, Search, SysMain rukenge (FPS drop kam)." : "Game Mode me Windows services nahi rukengi.");
            };
            svRow.Controls.Add(svToggle);
            tip.SetToolTip(svRow, "Game ke time Windows Update, Search, SysMain, Telemetry thodi der rukte hain - STOP pe wapas chalu.");
            card.Controls.AddRange(new Control[] { gmButton, gmStatus, hint, aiRow, svRow });
            Controls.Add(card);
        }

        // ================= System card =================
        private void BuildSystemCard()
        {
            var card = new Card { Location = new Point(24, 472), Size = new Size(300, 146) };
            card.Controls.Add(SectionLabel("SYSTEM", 18, 14));
            ramText = InfoLabel(18, 38);
            ramMeter = new Meter { Location = new Point(18, 60), Size = new Size(264, 8) };
            gpuText = InfoLabel(18, 74);
            gpuText.Text = "GPU:  " + (nvidiaName ?? "NVIDIA card nahi mila");
            bsText = InfoLabel(18, 92);
            planText = InfoLabel(18, 110);
            cleanBtn = new RoundButton { Text = "RAM saaf", Location = new Point(196, 8), Size = new Size(88, 24), Fill = Theme.Row, Font = Theme.Semi(9F) };
            cleanBtn.Click += (s, e) =>
            {
                if (busy) return;
                RunJob("RAM saaf", log => { double got = Booster.CleanNow(); log(string.Format("Cache RAM saaf - {0:N1} GB khali hua.", Math.Max(0, got))); });
            };
            card.Controls.AddRange(new Control[] { ramText, ramMeter, gpuText, bsText, planText, cleanBtn });
            Controls.Add(card);
        }

        // ================= Right panel (ek hi scroll panel) =================
        private void BuildRightPanel()
        {
            var card = new Card { Location = new Point(340, 130), Size = new Size(496, 488) };
            Controls.Add(card);
            scroll = new Panel { Location = new Point(6, 8), Size = new Size(486, 472), AutoScroll = true, BackColor = Theme.Card };
            scroll.MouseEnter += (s, e) => scroll.Focus();
            card.Controls.Add(scroll);

            int y = 6;
            scroll.Controls.Add(SectionLabel("OPTIMIZE", 10, y));
            scroll.Controls.Add(new Label { Text = "Switch dabao - setting lagegi ya hategi", ForeColor = Theme.SubText, Font = Theme.F(8.5F), AutoSize = true, Location = new Point(100, y + 1), BackColor = Color.Transparent });
            y += 26;
            optimizeTop = y;
            BuildOptimizations(scroll);
            y = optimizeTop + features.Count * 47 + 14;

            // ---- 240 FPS guide ----
            scroll.Controls.Add(SectionLabel("240 FPS", 10, y)); y += 26;
            var guide = new Card { Location = new Point(8, y), Size = new Size(452, 64), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
            guide.Controls.Add(new Label { Text = "Free Fire MAX ko 240 FPS tak le jao", Font = Theme.Semi(9.5F), ForeColor = Theme.Text, Location = new Point(12, 8), AutoSize = true, BackColor = Color.Transparent });
            guide.Controls.Add(new Label { Text = "Naya x86 version + game ki setting - steps dekho", Font = Theme.F(8F), ForeColor = Theme.SubText, Location = new Point(12, 30), Size = new Size(290, 28), BackColor = Color.Transparent });
            var gb = new RoundButton { Text = "Steps dekho", Location = new Point(320, 16), Size = new Size(118, 32), Fill = Theme.Accent, Fill2 = Theme.AccentDark, ForeColor = Color.FromArgb(20, 20, 26), Font = Theme.Semi(9F), Radius = 8 };
            gb.Click += (s, e) => ShowFpsGuide();
            guide.Controls.Add(gb);
            scroll.Controls.Add(guide);
            y += 78;

            // ---- FPS test ----
            scroll.Controls.Add(SectionLabel("FPS TEST  (asli FPS, pehle vs baad)", 10, y)); y += 26;
            var ft = new Card { Location = new Point(8, y), Size = new Size(452, 176), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
            ft.Controls.Add(new Label
            {
                Text = "Free Fire training ground me jao. Button dabao, 5 second me game me wapas jao aur 60 second normal khelo.",
                Font = Theme.F(8.5F), ForeColor = Theme.SubText, Location = new Point(12, 8), Size = new Size(428, 34), BackColor = Color.Transparent
            });
            fpsBtn1 = new RoundButton { Text = "Test 1: Bina boost", Location = new Point(12, 46), Size = new Size(208, 34), Fill = Theme.Card, Font = Theme.Semi(9F), Radius = 8 };
            fpsBtn2 = new RoundButton { Text = "Test 2: Boost ke saath", Location = new Point(232, 46), Size = new Size(208, 34), Fill = Theme.Green, Fill2 = Theme.GreenDark, Font = Theme.Semi(9F), Radius = 8 };
            fpsBtn1.Click += (s, e) => RunFps("Before");
            fpsBtn2.Click += (s, e) => RunFps("After");
            fpsBefore = new Label { Location = new Point(12, 88), Size = new Size(208, 40), Font = Theme.Semi(10F), ForeColor = Theme.SubText, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter };
            fpsAfter = new Label { Location = new Point(232, 88), Size = new Size(208, 40), Font = Theme.Semi(10F), ForeColor = Theme.Green, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter };
            fpsDiff = new Label { Location = new Point(12, 134), Size = new Size(280, 30), Font = Theme.Semi(11F), ForeColor = Theme.Accent, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            fpsSave = new RoundButton { Text = "Result card save", Location = new Point(300, 134), Size = new Size(140, 30), Fill = Theme.Card, Font = Theme.Semi(8.5F), Radius = 8 };
            fpsSave.Click += (s, e) =>
            {
                string f = FpsTest.SaveCard(nvidiaName);
                if (f == null) { Info("Pehle Test 1 aur Test 2 dono karo."); return; }
                Log("Result card Desktop pe save hua: " + Path.GetFileName(f));
                try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + f + "\"") { UseShellExecute = false }); } catch { }
            };
            ft.Controls.AddRange(new Control[] { fpsBtn1, fpsBtn2, fpsBefore, fpsAfter, fpsDiff, fpsSave });
            scroll.Controls.Add(ft);
            y += 190;
            ShowFpsResults();

            // ---- Startup apps ----
            scroll.Controls.Add(SectionLabel("STARTUP APPS", 10, y));
            startupSummary = new Label { Location = new Point(124, y + 1), Size = new Size(240, 18), ForeColor = Theme.SubText, Font = Theme.F(8.5F), BackColor = Color.Transparent, Text = "Load ho raha hai..." };
            var refresh = new RoundButton { Text = "Refresh", Location = new Point(378, y - 3), Size = new Size(82, 24), Fill = Theme.Row, Font = Theme.Semi(8F), Radius = 7 };
            refresh.Click += (s, e) => LoadStartup();
            scroll.Controls.Add(startupSummary); scroll.Controls.Add(refresh);
            startupTop = y + 28;
        }

        private int optimizeTop = 32;

        private void ShowFpsGuide()
        {
            var msg =
                "FREE FIRE MAX - 240 FPS tak kaise le jayein:\n\n" +
                "1.  Naba me 'BlueStacks / MSI Optimize' ON karo (FPS limit 240).\n" +
                "2.  'NVIDIA Boost' ON karo (VSync off - FPS monitor pe nahi rukega).\n" +
                "3.  Free Fire MAX ka naya x86 version lo: BlueStacks ke Free Fire MAX page pe\n" +
                "     'Play Free Fire MAX on PC' se install karo. (Pehle game me account bind\n" +
                "     check kar lo - Google/Facebook - taaki data safe rahe.)\n" +
                "4.  Free Fire ke andar Settings -> Graphics: 'Smooth', High FPS ka sabse uncha\n" +
                "     option, Shadow OFF.\n" +
                "5.  Khelne se pehle Naba me GAME MODE START dabao.\n\n" +
                "240 ek upar ki limit hai - asli FPS PC aur game ke scene pe depend karta hai.\n" +
                "FPS TEST se apna asli number dekh lo.\n\n" +
                "BlueStacks ka 240 FPS guide page kholna hai?";
            if (MessageBox.Show(this, msg, "240 FPS Guide", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", "\"https://www.bluestacks.com/blog/bluestacks-exclusives/ffm-high-fps-guide-en.html\"") { UseShellExecute = false }); }
                catch { }
            }
        }

        private void ShowFpsResults()
        {
            double a, l;
            fpsBefore.Text = FpsTest.Load("Before", out a, out l) ? string.Format("{0:N0} FPS\n1% low {1:N0}", a, l) : "Test 1 baaki";
            double a2, l2;
            fpsAfter.Text = FpsTest.Load("After", out a2, out l2) ? string.Format("{0:N0} FPS\n1% low {1:N0}", a2, l2) : "Test 2 baaki";
            if (a > 0 && a2 > 0)
            {
                double d = a2 - a;
                fpsDiff.Text = d >= 0 ? string.Format("+{0:N0} FPS  ({1:N0}% zyada)", d, d / a * 100) : string.Format("{0:N0} FPS - boost check karo", d);
                fpsDiff.ForeColor = d >= 0 ? Theme.Accent : Theme.Red;
            }
            else fpsDiff.Text = "";
        }

        private void RunFps(string which)
        {
            if (busy) return;
            if (!FpsTest.ToolAvailable) { Info("FPS test tool nahi mila - app ka naya version install karo."); return; }
            if (!Emulators.AnyRunning) { Info("Pehle BlueStacks / MSI me Free Fire kholo, training ground me jao, phir test dabao."); return; }
            if (which == "After" && !GameMode.IsOn && !Ask("Game Mode abhi OFF hai. Boost wala test Game Mode START karke karna chahiye.\n\nPhir bhi abhi test karna hai?")) return;
            RunJob(which == "Before" ? "FPS Test 1 (bina boost)" : "FPS Test 2 (boost ke saath)", log =>
            {
                for (int i = 5; i > 0; i--) { log("Game me jao... " + i); System.Threading.Thread.Sleep(1000); }
                log("FPS gin raha hoon - 60 second normal khelo...");
                var r = FpsTest.Measure(60, log);
                FpsTest.Save(which, r);
                log(string.Format("Result: Average {0:N0} FPS  •  1% low {1:N0}  •  Sabse kam {2:N0}  ({3} frames)", r.Avg, r.Low1, r.Min, r.Frames));
                BeginInvoke(new Action(ShowFpsResults));
            });
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
                Title = "BlueStacks / MSI Optimize (240 FPS)",
                GetState = x => { var st = Status.BlueStacks(out d); Pass(d); return st; },
                ConfirmOn = () =>
                {
                    if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks / MSI poora band karo (tray me bhi), phir switch dabao."); return false; }
                    int ram, cpus, pcRam, threads; Tweaks.BlueStacksPlan(out ram, out cpus, out pcRam, out threads);
                    return Ask(string.Format("Is PC me {0} GB RAM aur {1} CPU threads hain.\n\nBlueStacks / MSI App Player me lagega:\n- RAM: {2} GB\n- CPU: {3} cores\n- FPS limit: 240 (High FPS ON)\n- VSync: OFF\n\nPurani setting ka backup Documents me banega. Lagana hai?", pcRam, threads, ram / 1024, cpus));
                },
                ConfirmOff = () =>
                {
                    if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks poora band karo, phir switch dabao."); return false; }
                    if (!Tweaks.AnyBackup) { Info("Purani setting ka backup nahi mila - wapas nahi ja sakte."); return false; }
                    return Ask("BlueStacks / MSI ki purani setting (backup) wapas lagani hai?");
                },
                On = Tweaks.BlueStacksOptimize, Off = Tweaks.BlueStacksRestore
            });
            AddFeature(host, new Feature
            {
                Title = "BlueStacks / MSI Turbo",
                GetState = x => { var st = Booster.TurboState(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("BlueStacks / MSI ke liye:\n\n- Hamesha HIGH CPU priority\n- High performance GPU\n- Fullscreen optimization OFF\n\nEmulator ek baar band karke kholna padega. Lagana hai?"),
                ConfirmOff = () => Ask("Turbo hata ke Windows default pe wapas?"),
                On = Booster.TurboOn, Off = Booster.TurboOff
            });
            AddFeature(host, new Feature
            {
                Title = "Defender Exclusion",
                GetState = x => { var st = Status.Defender(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Windows Defender BlueStacks / MSI ke folders scan nahi karega (game ke beech FPS drop kam).\nBaaki PC waise hi protected rahega.\n\nEmulator me anjaan/modded APK mat daalna. Lagana hai?"),
                ConfirmOff = () => Ask("Emulator ko dobara Defender scan me daalna hai?"),
                On = Tweaks.DefenderExclude, Off = Tweaks.DefenderRemove
            });
            AddFeature(host, new Feature
            {
                Title = "NVIDIA Boost",
                GetState = x => { var st = Status.Nvidia(nvidiaName, out d); Pass(d); return st; },
                ConfirmOn = () => Ask("NVIDIA me 4 setting lagengi:\n\n- Power management = Prefer maximum performance\n- Shader Cache Size = Unlimited\n- Vertical Sync = Force OFF (FPS monitor ki limit pe nahi rukega)\n- Low Latency = ON\n\nVSync off se kabhi screen me halki line (tearing) dikh sakti hai.\nLagana hai?"),
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
            var row = new Card { Location = new Point(8, optimizeTop + i * 47), Size = new Size(452, 43), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
            var title = new Label { Text = f.Title, Font = Theme.Semi(9.5F), ForeColor = Theme.Text, Location = new Point(12, 4), AutoSize = true, BackColor = Color.Transparent };
            f.Detail = new Label { Text = "...", Font = Theme.F(8F), ForeColor = Theme.SubText, Location = new Point(12, 23), Size = new Size(290, 16), BackColor = Color.Transparent, AutoEllipsis = true };
            f.Pill = new Pill { Location = new Point(310, 11), Size = new Size(72, 21) };
            f.Toggle = new ToggleSwitch { Location = new Point(396, 10), Size = new Size(44, 23) };
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

        // ================= Startup apps (scroll panel ke neeche) =================
        private void LoadStartup()
        {
            startupSummary.Text = "Load ho raha hai...";
            Task.Factory.StartNew(() => StartupApps.List()).ContinueWith(t =>
            {
                if (IsDisposed || t.Exception != null) return;
                BeginInvoke(new Action(() => FillStartup(t.Result)));
            });
        }

        private void FillStartup(List<StartupItem> items)
        {
            int keepScroll = scroll.VerticalScroll.Value;
            scroll.SuspendLayout();
            foreach (var c in startupRows) { scroll.Controls.Remove(c); c.Dispose(); }
            startupRows.Clear();
            int y = startupTop - keepScroll;   // AutoScroll panel me location scroll ke hisaab se hoti hai
            foreach (var it in items)
            {
                var item = it;
                var row = new Card { Location = new Point(8, y), Size = new Size(452, 44), Fill = Theme.Row, Border = Theme.Row, Radius = 9 };
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
                scroll.Controls.Add(row);
                startupRows.Add(row);
                y += 48;
            }
            var spacer = new Label { Location = new Point(0, y + 4), Size = new Size(10, 10), BackColor = Color.Transparent };
            scroll.Controls.Add(spacer); startupRows.Add(spacer);
            scroll.ResumeLayout();
            UpdateStartupSummary(items);
        }

        private void UpdateStartupSummary(List<StartupItem> items)
        {
            int on = 0; foreach (var i in items) if (i.Enabled) on++;
            startupSummary.Text = string.Format("{0} chalu  •  {1} band", on, items.Count - on);
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
            bool bs = Emulators.AnyRunning;
            var emus = Emulators.Installed();
            bsText.Text = "Emulator:  " + (emus.Count == 0 ? "BlueStacks / MSI install nahi hai" : string.Join(", ", emus.Select(e => e.Name)) + (bs ? "  (chal raha hai)" : "  (band)"));
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
