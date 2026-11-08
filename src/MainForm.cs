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
            public string Icon;
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


        private static bool AutoUpdateEnabled
        {
            get { return Settings.AutoUpdate; }
            set { Settings.AutoUpdate = value; }
        }

        private Bitmap backdrop;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (backdrop == null || backdrop.Width != ClientSize.Width || backdrop.Height != ClientSize.Height)
            {
                if (backdrop != null) backdrop.Dispose();
                backdrop = Backdrop.Render(ClientSize.Width, ClientSize.Height);
            }
            e.Graphics.DrawImageUnscaled(backdrop, 0, 0);
        }

        public MainForm()
        {
            Text = "Naba Game Booster";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(860, 818);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5F);
            DoubleBuffered = true;
            Anim.Blocked = () => GameMode.IsOn;
            Anim.Attach(this);
            Opacity = 0;   // khulte waqt smooth fade-in
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
                var fade = new System.Windows.Forms.Timer { Interval = 15 };
                fade.Tick += (s2, e2) => { double o = Opacity + 0.08; if (o >= 1) { Opacity = 1; fade.Stop(); fade.Dispose(); } else Opacity = o; };
                fade.Start();
                GameMode.CleanStaleState();
                Log("Naba Game Booster v" + Updater.CurrentText + " shuru.");
                if (GameMode.IsOn) Log("Game Mode abhi bhi ON hai - khel ke STOP dabao.");
                RefreshLive();
                RefreshStates();
                LoadStartup();
                CheckUpdates(false);
                Task.Factory.StartNew(() => PcProfile.Current).ContinueWith(t =>
                {
                    if (IsDisposed || t.Exception != null) return;
                    var p = t.Result;
                    BeginInvoke(new Action(() => profileLbl.Text = string.Format("{0} PC  •  {1}  •  {2} GB", p.TierName, p.Gpu, p.RamGb)));
                });
            };
        }

        // ================= Header =================
        private void BuildHeader()
        {
            var logo = new Logo { Location = new Point(14, 8), Size = new Size(72, 72) };
            var title = new HeaderTitle { Location = new Point(90, 10), Size = new Size(430, 46) };
            var sub = new Label { Text = "BlueStacks / MSI App Player   •   Free Fire FPS booster   •   v" + Updater.CurrentText, Font = Theme.F(9.5F), ForeColor = Theme.SubText, AutoSize = true, Location = new Point(94, 56), BackColor = Color.Transparent };
            Controls.AddRange(new Control[] { logo, title, sub });

            checkBtn = new RoundButton { Text = "Update check", IconName = "refresh", Outline = true, OutlineColor = Theme.Accent, ChamferShape = true, Location = new Point(684, 16), Size = new Size(164, 46), Font = Theme.Semi(10F) };
            checkBtn.Click += (s, e) => CheckUpdates(true);
            Controls.Add(checkBtn);

            autoToggle = new ToggleSwitch { Location = new Point(620, 22), Size = new Size(56, 32) };
            var autoLbl = new Label { Text = "Auto update", ForeColor = Theme.Text, Font = Theme.F(9.5F), AutoSize = true, BackColor = Color.Transparent };
            autoLbl.Location = new Point(autoToggle.Left - autoLbl.PreferredWidth - 6, 29);   // text hamesha switch se pehle khatam
            autoToggle.Checked = AutoUpdateEnabled;
            autoToggle.Toggled += (s, e) =>
            {
                AutoUpdateEnabled = !AutoUpdateEnabled;
                autoToggle.Checked = AutoUpdateEnabled;
                Log(AutoUpdateEnabled ? "Auto update ON - naya version khud lag jayega." : "Auto update OFF - update ke liye button dabana hoga.");
            };
            Controls.Add(autoToggle); Controls.Add(autoLbl); autoLbl.BringToFront(); autoToggle.BringToFront();

            updateStrip = new Card { Location = new Point(8, 84), Size = new Size(844, 58), Neon = true, ChamferSize = 12, NeonA = Theme.Green, NeonB = Color.FromArgb(40, 200, 160), Fill = Color.FromArgb(14, 26, 26) };
            var tick = new IconBox { IconName = "check", IconColor = Theme.Green, Location = new Point(30, 18), Size = new Size(24, 24) };
            updateText = new Label { Text = "Update check ho raha hai...", ForeColor = Theme.SubText, Font = Theme.Semi(10.5F), Location = new Point(62, 19), Size = new Size(580, 24), BackColor = Color.Transparent };
            updateNow = new RoundButton { Text = "Abhi update karo", Location = new Point(660, 10), Size = new Size(166, 38), Fill = Theme.Green, Fill2 = Theme.GreenDark, Visible = false, Font = Theme.Semi(9.5F), ChamferShape = true };
            updateNow.Click += (s, e) => StartUpdate();
            updateStrip.Controls.Add(tick);
            updateStrip.Controls.Add(updateText);
            updateStrip.Controls.Add(updateNow);
            Controls.Add(updateStrip);
        }

        // ================= Game Mode card =================
        private void BuildGameModeCard()
        {
            var card = new Card { Location = new Point(8, 146), Size = new Size(332, 360), Neon = true };
            card.Controls.Add(SectionLabel("GAME MODE", 24, 20));
            gmButton = new RoundButton { Pulse = true, Location = new Point(18, 54), Size = new Size(296, 132), ChamferShape = true, IconName = "play", Font = Theme.Heavy(26F) };
            gmButton.Click += (s, e) =>
            {
                if (busy) return;
                if (GameMode.IsOn) RunJob("Game Mode STOP", GameMode.Off);
                else RunJob("Game Mode START", GameMode.On);
            };
            gmStatus = new Label { Location = new Point(26, 190), Size = new Size(286, 22), Font = Theme.Semi(10F), BackColor = Color.Transparent };
            var hint = new Label
            {
                Text = "Browser, Premiere, Telegram waghera pause - kaam jaisa ka taisa. STOP pe sab wahin se chalu.",
                Location = new Point(26, 214), Size = new Size(286, 38), ForeColor = Theme.SubText, Font = Theme.F(9F), BackColor = Color.Transparent
            };
            var aiRow = new Card { Location = new Point(20, 256), Size = new Size(292, 42), Fill = Theme.Row, Border = Theme.RowBorder, Radius = 9 };
            aiRow.Controls.Add(new IconBox { IconName = "chat", Location = new Point(10, 9), Size = new Size(24, 24) });
            aiRow.Controls.Add(new Label { Text = "Claude / ChatGPT bhi pause", Font = Theme.F(9.5F), ForeColor = Theme.Text, Location = new Point(42, 11), AutoSize = true, BackColor = Color.Transparent });
            aiToggle = new ToggleSwitch { Location = new Point(232, 6), Size = new Size(54, 30) };
            aiToggle.Checked = Settings.PauseAiApps;
            aiToggle.Toggled += (s, e) =>
            {
                if (GameMode.IsOn) { Info("Game Mode chal raha hai - STOP karke phir badlo."); return; }
                Settings.PauseAiApps = !Settings.PauseAiApps;
                aiToggle.Checked = Settings.PauseAiApps;
                Log(Settings.PauseAiApps ? "Claude / ChatGPT desktop bhi Game Mode me pause honge." : "Claude / ChatGPT desktop Game Mode me chalte rahenge.");
            };
            aiRow.Controls.Add(aiToggle);
            var svRow = new Card { Location = new Point(20, 304), Size = new Size(292, 42), Fill = Theme.Row, Border = Theme.RowBorder, Radius = 9 };
            svRow.Controls.Add(new IconBox { IconName = "windows", Location = new Point(10, 9), Size = new Size(24, 24) });
            svRow.Controls.Add(new Label { Text = "Windows Update / Search band", Font = Theme.F(9.5F), ForeColor = Theme.Text, Location = new Point(42, 11), AutoSize = true, BackColor = Color.Transparent });
            var svToggle = new ToggleSwitch { Location = new Point(232, 6), Size = new Size(54, 30) };
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
            var card = new Card { Location = new Point(8, 512), Size = new Size(332, 186), Neon = true };
            card.Controls.Add(SectionLabel("SYSTEM", 24, 18));
            cleanBtn = new RoundButton { Text = "RAM saaf", IconName = "broom", Outline = true, OutlineColor = Theme.Accent, ChamferShape = true, Location = new Point(196, 14), Size = new Size(122, 40), Font = Theme.Semi(9.5F) };
            cleanBtn.Click += (s, e) =>
            {
                if (busy) return;
                RunJob("RAM saaf", log => { double got = Booster.CleanNow(); log(string.Format("Cache RAM saaf - {0:N1} GB khali hua.", Math.Max(0, got))); });
            };
            ramText = InfoLabel(26, 60); ramText.ForeColor = Theme.Text; ramText.Font = Theme.Semi(9.5F);
            ramMeter = new Meter { Location = new Point(22, 84), Size = new Size(290, 12) };
            card.Controls.Add(new IconBox { IconName = "chip", Location = new Point(24, 104), Size = new Size(20, 20) });
            gpuText = InfoLabel(50, 105);
            gpuText.Text = "GPU:  " + (nvidiaName ?? "NVIDIA card nahi mila");
            card.Controls.Add(new IconBox { IconName = "layers", Location = new Point(24, 126), Size = new Size(20, 20) });
            bsText = InfoLabel(50, 127);
            card.Controls.Add(new IconBox { IconName = "bolt", Location = new Point(24, 148), Size = new Size(20, 20) });
            planText = InfoLabel(50, 149);
            card.Controls.AddRange(new Control[] { ramText, ramMeter, gpuText, bsText, planText, cleanBtn });
            Controls.Add(card);
        }

        // ================= Right panel (ek hi scroll panel) =================
        private void BuildRightPanel()
        {
            var card = new Card { Location = new Point(344, 146), Size = new Size(508, 552), Neon = true };
            Controls.Add(card);
            scroll = new Panel { Location = new Point(18, 18), Size = new Size(474, 518), AutoScroll = true, BackColor = Theme.Card };
            scroll.MouseEnter += (s, e) => scroll.Focus();
            card.Controls.Add(scroll);

            int y = 6;
            // ---- ONE-CLICK BOOST ----
            var boostCard = new Card { Location = new Point(0, y), Size = new Size(454, 118), Neon = true, ChamferSize = 16, NeonA = Theme.Accent, NeonB = Theme.AccentDark, Fill = Color.FromArgb(40, 22, 12) };
            boostCard.Controls.Add(new IconBox { IconName = "bolt", IconColor = Theme.Accent, Location = new Point(20, 20), Size = new Size(30, 34) });
            boostCard.Controls.Add(new Label { Text = "ONE-CLICK BOOST", Font = Theme.Heavy(15F), ForeColor = Theme.Accent, Location = new Point(52, 18), AutoSize = true, BackColor = Color.Transparent });
            boostCard.Controls.Add(new Label { Text = "PC dekh ke saari best settings ek saath", Font = Theme.F(9F), ForeColor = Color.FromArgb(255, 190, 120), Location = new Point(55, 50), Size = new Size(230, 18), BackColor = Color.Transparent });
            profileLbl = new Label { Text = "PC check ho raha hai...", Font = Theme.Semi(9F), ForeColor = Theme.Text, Location = new Point(22, 78), Size = new Size(262, 22), BackColor = Color.Transparent, AutoEllipsis = true };
            boostCard.Controls.Add(profileLbl);
            var boostBtn = new RoundButton { Shine = true, Text = "BOOST", IconName = "bolt", ChamferShape = true, GlowColor = Theme.Accent, Location = new Point(282, 20), Size = new Size(160, 80), Fill = Color.FromArgb(255, 176, 40), Fill2 = Theme.AccentDark, ForeColor = Color.FromArgb(30, 14, 4), Font = Theme.Heavy(17F) };
            boostBtn.Click += (s, e) => OneClickBoost();
            boostCard.Controls.Add(boostBtn);
            allButtons.Add(boostBtn);
            scroll.Controls.Add(boostCard);
            y += 128;

            scroll.Controls.Add(SectionLabel("OPTIMIZE", 4, y));
            scroll.Controls.Add(new Label { Text = "Switch dabao - setting lagegi ya hategi", ForeColor = Theme.SubText, Font = Theme.F(9F), AutoSize = true, Location = new Point(212, y + 7), BackColor = Color.Transparent });
            y += 38;
            optimizeTop = y;
            BuildOptimizations(scroll);
            y = optimizeTop + features.Count * 52 + 14;

            // ---- FPS limit ----
            scroll.Controls.Add(SectionLabel("FPS LIMIT", 4, y)); y += 38;
            var guide = new Card { Location = new Point(2, y), Size = new Size(450, 66), Fill = Theme.Row, Border = Theme.RowBorder, Radius = 9 };
            guide.Controls.Add(new IconBox { IconName = "fps", Location = new Point(12, 20), Size = new Size(26, 26) });
            fpsInfo = new Label { Font = Theme.Semi(9F), ForeColor = Theme.Text, Location = new Point(48, 10), Size = new Size(206, 20), BackColor = Color.Transparent, AutoEllipsis = true };
            guide.Controls.Add(fpsInfo);
            guide.Controls.Add(new Label { Text = "Game me BlueStacks ke FPS counter me dikhega", Font = Theme.F(8.5F), ForeColor = Theme.SubText, Location = new Point(48, 34), Size = new Size(206, 28), BackColor = Color.Transparent });
            var change = new RoundButton { Text = "Badlo", Location = new Point(258, 14), Size = new Size(86, 38), Fill = Theme.Accent, Fill2 = Theme.AccentDark, ForeColor = Color.FromArgb(30, 14, 4), Font = Theme.Semi(9.5F), ChamferShape = true };
            change.Click += (s, e) =>
            {
                if (busy) return;
                if (!Emulators.AnyInstalled) { Info("BlueStacks / MSI install nahi hai."); return; }
                if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks / MSI poora band karo (tray me bhi), phir badlo."); return; }
                int fps = FpsDialog.Ask(this);
                if (fps <= 0) return;
                Settings.FpsLimit = fps;
                RunJob("FPS limit " + fps, Tweaks.BlueStacksOptimize);
            };
            var gb = new RoundButton { Text = "240 guide", Location = new Point(350, 12), Size = new Size(94, 42), Outline = true, OutlineColor = Theme.Purple, ChamferShape = true, Font = Theme.Semi(9F) };
            gb.Click += (s, e) => ShowFpsGuide();
            guide.Controls.Add(change); guide.Controls.Add(gb);
            scroll.Controls.Add(guide);
            y += 80;

            // ---- Startup apps ----
            scroll.Controls.Add(SectionLabel("STARTUP APPS", 4, y));
            startupSummary = new Label { Location = new Point(250, y + 7), Size = new Size(120, 18), ForeColor = Theme.SubText, Font = Theme.F(9F), BackColor = Color.Transparent, Text = "Load..." };
            var refresh = new RoundButton { Text = "Refresh", IconName = "refresh", Location = new Point(366, y), Size = new Size(86, 32), Outline = true, OutlineColor = Theme.Purple, ChamferShape = true, Font = Theme.Semi(8.5F) };
            refresh.Click += (s, e) => LoadStartup();
            scroll.Controls.Add(startupSummary); scroll.Controls.Add(refresh);
            startupTop = y + 40;
        }

        private int optimizeTop = 32;
        private Label fpsInfo;
        private Label profileLbl;
        private readonly List<RoundButton> allButtons = new List<RoundButton>();

        private void OneClickBoost()
        {
            if (busy) return;
            if (GameMode.IsOn) { Info("Pehle Game Mode STOP karo, phir Boost karo."); return; }
            if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks / MSI poora band karo (tray me bhi), phir BOOST dabao."); return; }
            var pc = PcProfile.Current;
            string msg = string.Format(
                "Tumhara PC: {0}\n{1}  •  {2}  •  {3} GB RAM\n\nYeh sab ek saath lagega:\n" +
                "- Windows power plan, Game Mode, Xbox recording off\n" +
                "- BlueStacks / MSI: RAM {4} GB, CPU {5}, FPS {6}{7}\n" +
                "- Fast Mode (Hyper-V band) - PC restart chahiye\n" +
                "- Turbo, ASTC, Defender exclusion{8}\n" +
                "- RAM Cleaner, Watchdog, Timer\n\n" +
                "Dhyan do: Fast Mode se WSL / Docker / Windows Sandbox nahi chalenge.\nShuru karein?",
                pc.TierName, pc.Cpu, pc.Gpu, pc.RamGb, pc.BsRamMb / 1024, pc.BsCpus, pc.Fps,
                pc.Width > 0 ? ", " + pc.Width + "x" + pc.Height : "",
                nvidiaName != null ? ", NVIDIA Boost" : "");
            if (!Ask(msg)) return;
            SetBusy(true);
            Log("---- ONE-CLICK BOOST ----");
            using (var d = new BoostDialog(pc, Log)) d.ShowDialog(this);
            SetBusy(false);
            RefreshLive();
            RefreshStates();
        }

        private void ShowFpsGuide()
        {
            var msg =
                "FREE FIRE MAX - 240 FPS tak kaise le jayein:\n\n" +
                "1.  Naba me 'BlueStacks Fast Mode' ON karo aur PC restart karo.\n" +
                "2.  FPS LIMIT me 'Badlo' -> 240 chuno.\n" +
                "3.  'NVIDIA Boost' ON karo (VSync off).\n" +
                "4.  Free Fire MAX ka naya x86 version lo: BlueStacks ke Free Fire MAX page pe\n" +
                "     'Play Free Fire MAX on PC' se install karo. (Pehle game me account bind\n" +
                "     check kar lo - Google/Facebook - taaki data safe rahe.)\n" +
                "5.  Free Fire ke andar Settings -> Graphics: 'Smooth', High FPS ka sabse uncha\n" +
                "     option, Shadow OFF.\n" +
                "6.  Khelne se pehle Naba me GAME MODE START dabao.\n\n" +
                "FPS upar-neeche ho raha ho to limit monitor ke barabar rakho (sabse smooth).\n" +
                "240 ek upar ki limit hai - asli FPS PC aur game ke scene pe depend karta hai.\n" +
                "BlueStacks ke FPS counter me apna asli number dekh lo.\n\n" +
                "BlueStacks ka 240 FPS guide page kholna hai?";
            if (MessageBox.Show(this, msg, "240 FPS Guide", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", "\"https://www.bluestacks.com/blog/bluestacks-exclusives/ffm-high-fps-guide-en.html\"") { UseShellExecute = false }); }
                catch { }
            }
        }

        // ================= Optimize tab =================
        private void BuildOptimizations(Panel host)
        {
            string d;
            AddFeature(host, new Feature
            {
                Title = "Gaming Optimize", Icon = "game",
                GetState = x => { var st = Status.GamingOptimize(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Yeh lagega:\n\n- Ultimate Performance power plan\n- Windows Game Mode ON\n- Xbox background recording OFF\n- GPU scheduling ON (restart ke baad)\n\nLagana hai?"),
                ConfirmOff = () => Ask("Power plan Balanced pe, Xbox recording aur GPU scheduling Windows default pe wapas.\n\nHatana hai?"),
                On = Tweaks.GamingOptimize, Off = Tweaks.GamingOptimizeUndo
            });
            AddFeature(host, new Feature
            {
                Title = "BlueStacks / MSI Optimize", Icon = "layers",
                GetState = x => { var st = Status.BlueStacks(out d); Pass(d); return st; },
                ConfirmOn = () =>
                {
                    if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks / MSI poora band karo (tray me bhi), phir switch dabao."); return false; }
                    int fps = FpsDialog.Ask(this);
                    if (fps <= 0) return false;
                    Settings.FpsLimit = fps;
                    return true;
                },
                ConfirmOff = () =>
                {
                    if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks / MSI poora band karo, phir switch dabao."); return false; }
                    if (!Tweaks.AnyBackup) { Info("Purani setting ka backup nahi mila - wapas nahi ja sakte."); return false; }
                    return Ask("BlueStacks / MSI ki purani setting (backup) wapas lagani hai?");
                },
                On = Tweaks.BlueStacksOptimize, Off = Tweaks.BlueStacksRestore
            });
            AddFeature(host, new Feature
            {
                Title = "BlueStacks Fast Mode", Icon = "rocket",
                GetState = x => { var st = Booster.FastModeState(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Windows ka Hyper-V band hoga - BlueStacks / MSI apne tez engine pe chalega.\nFPS zyada aur tika hua rehta hai.\n\nDhyan do:\n- PC RESTART karna padega\n- WSL, Docker, Windows Sandbox nahi chalenge\n- Memory Integrity bhi band rahegi\n- BIOS me Virtualization (VT-x / SVM) ON hona chahiye\n\nLagana hai?"),
                ConfirmOff = () => Ask("Hyper-V wapas chalu karna hai? (restart ke baad lagega)"),
                On = Booster.FastModeOn, Off = Booster.FastModeOff
            });
            AddFeature(host, new Feature
            {
                Title = "BlueStacks / MSI Turbo", Icon = "bolt",
                GetState = x => { var st = Booster.TurboState(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("BlueStacks / MSI ke liye:\n\n- Hamesha HIGH CPU priority\n- High performance GPU\n- Fullscreen optimization OFF\n\nEmulator ek baar band karke kholna padega. Lagana hai?"),
                ConfirmOff = () => Ask("Turbo hata ke Windows default pe wapas?"),
                On = Booster.TurboOn, Off = Booster.TurboOff
            });
            AddFeature(host, new Feature
            {
                Title = "ASTC - textures GPU pe", Icon = "cubes",
                GetState = x => { var st = Tweaks.AstcState(out d); Pass(d); return st; },
                ConfirmOn = () =>
                {
                    if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks / MSI band karo, phir switch dabao."); return false; }
                    return Ask("Free Fire ke textures CPU ki jagah graphics card kholega - 4v4 me CPU halka.\nAgar game me koi texture ajeeb dikhe to switch band kar dena.\n\nLagana hai?");
                },
                ConfirmOff = () => { if (Tweaks.BlueStacksRunning) { Info("Pehle BlueStacks / MSI band karo."); return false; } return true; },
                On = Tweaks.AstcOn, Off = Tweaks.AstcOff
            });
            AddFeature(host, new Feature
            {
                Title = "Defender Exclusion", Icon = "shield",
                GetState = x => { var st = Status.Defender(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Windows Defender BlueStacks / MSI ke folders scan nahi karega (game ke beech FPS drop kam).\nBaaki PC waise hi protected rahega.\n\nEmulator me anjaan/modded APK mat daalna. Lagana hai?"),
                ConfirmOff = () => Ask("Emulator ko dobara Defender scan me daalna hai?"),
                On = Tweaks.DefenderExclude, Off = Tweaks.DefenderRemove
            });
            AddFeature(host, new Feature
            {
                Title = "NVIDIA Boost", Icon = "chip",
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
                Title = "RAM Cleaner", Icon = "ram",
                GetState = x => { var st = Booster.RamCleanerState(out d); Pass(d); return st; },
                ConfirmOn = () => true, ConfirmOff = () => true,
                On = Booster.RamCleanerOn, Off = Booster.RamCleanerOff
            });
            AddFeature(host, new Feature
            {
                Title = "Game Watchdog", Icon = "eye",
                GetState = x => { d = Settings.Watchdog ? "Game Mode me CPU khaane wale programs ki priority kam" : "Band - switch dabao"; Pass(d); return Settings.Watchdog ? FeatureState.On : FeatureState.Off; },
                ConfirmOn = () => true, ConfirmOff = () => true,
                On = log => { Settings.Watchdog = true; log("[OK] Game Watchdog ON - Game Mode START pe chalega."); },
                Off = log => { Settings.Watchdog = false; log("[OK] Game Watchdog band."); }
            });
            AddFeature(host, new Feature
            {
                Title = "Timer 0.5ms (smooth)", Icon = "timer",
                GetState = x => { var st = Booster.TimerState(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Game Mode START pe Windows ka timer 0.5ms ho jayega - frames aur input smooth.\nWindows 11 pe poora asar ek baar restart ke baad.\n\nLagana hai?"),
                ConfirmOff = () => true,
                On = Booster.TimerOn, Off = Booster.TimerOff
            });
            AddFeature(host, new Feature
            {
                Title = "Mouse 1:1  (aim)", Icon = "mouse",
                GetState = x => { var st = Status.Mouse(out d); Pass(d); return st; },
                ConfirmOn = () => true, ConfirmOff = () => true,
                On = Tweaks.MouseRaw, Off = Tweaks.MouseDefault
            });
            AddFeature(host, new Feature
            {
                Title = "Memory Integrity band (FPS+)", Icon = "lock",
                GetState = x => { var st = Status.MemoryIntegrityDisabled(out d); Pass(d); return st; },
                ConfirmOn = () => Ask("Memory Integrity band karne se games aur BlueStacks smooth chalte hain,\npar PC ki ek extra security layer hat jaati hai.\n\nRestart ke baad lagega. Karna hai?"),
                ConfirmOff = () => Ask("Memory Integrity wapas chalu karna hai? (zyada security, gaming thodi dheemi)\nRestart ke baad lagega." + (Booster.HypervisorRunning() ? "" : "\n\nNote: BlueStacks Fast Mode ON hai to Memory Integrity tabhi chalegi jab Fast Mode OFF karoge.")),
                On = log => Tweaks.MemoryIntegrity(false, log), Off = log => Tweaks.MemoryIntegrity(true, log)
            });
        }

        [ThreadStatic] private static string passDetail;
        private static void Pass(string d) { passDetail = d; }

        private void AddFeature(Control parent, Feature f)
        {
            int i = features.Count;
            var row = new Card { Location = new Point(2, optimizeTop + i * 52), Size = new Size(450, 48), Fill = Theme.Row, Border = Theme.RowBorder, Radius = 9 };
            row.Controls.Add(new IconBox { IconName = f.Icon, Location = new Point(12, 11), Size = new Size(26, 26) });
            var title = new Label { Text = f.Title, Font = Theme.Semi(10.5F), ForeColor = Theme.Text, Location = new Point(48, 4), AutoSize = true, BackColor = Color.Transparent };
            f.Detail = new Label { Text = "...", Font = Theme.F(8.5F), ForeColor = Theme.SubText, Location = new Point(48, 26), Size = new Size(240, 17), BackColor = Color.Transparent, AutoEllipsis = true };
            f.Pill = new Pill { Location = new Point(292, 11), Size = new Size(86, 27) };
            f.Toggle = new ToggleSwitch { Location = new Point(386, 9), Size = new Size(56, 30) };
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
                var row = new Card { Location = new Point(2, y), Size = new Size(450, 46), Fill = Theme.Row, Border = Theme.RowBorder, Radius = 9 };
                row.Controls.Add(new IconBox { IconName = "list", Location = new Point(12, 12), Size = new Size(22, 22) });
                var name = new Label { Text = item.Name, Font = Theme.Semi(10F), ForeColor = Theme.Text, Location = new Point(44, 4), Size = new Size(300, 19), AutoEllipsis = true, BackColor = Color.Transparent };
                var cmd = new Label { Text = (item.Protected ? "Windows Security - band nahi kar sakte  •  " : item.Source + "  •  ") + item.Command, Font = Theme.F(8F), ForeColor = Theme.SubText, Location = new Point(44, 25), Size = new Size(320, 16), AutoEllipsis = true, BackColor = Color.Transparent };
                var tg = new ToggleSwitch { Location = new Point(386, 8), Size = new Size(56, 30), Enabled = !item.Protected };
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
                y += 50;
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
            var card = new Card { Location = new Point(8, 702), Size = new Size(844, 112), Neon = true, ChamferSize = 14 };
            card.Controls.Add(SectionLabel("ACTIVITY", 24, 14));
            logBox = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                Location = new Point(26, 48), Size = new Size(796, 52), BackColor = Theme.Card, ForeColor = Color.FromArgb(120, 240, 140),
                Font = new Font("Consolas", 9F), TabStop = false
            };
            card.Controls.Add(logBox);
            Controls.Add(card);
        }

        // ================= Helpers =================
        private static Control SectionLabel(string text, int x, int y)
        {
            string icon = text.StartsWith("GAME") ? "game" : text.StartsWith("SYSTEM") ? "monitor" : text.StartsWith("OPTIMIZE") ? "gear" :
                          text.StartsWith("FPS") ? "fps" : text.StartsWith("STARTUP") ? "list" : text.StartsWith("ACTIVITY") ? "terminal" : null;
            int w = TextRenderer.MeasureText(text, Theme.Heavy(13F)).Width + 78;
            return new NeonHeader(text, icon) { Location = new Point(x, y), Size = new Size(w, 32) };
        }

        private static Label InfoLabel(int x, int y)
        {
            return new Label { Font = Theme.F(9.5F), ForeColor = Theme.SubText, Location = new Point(x, y), Size = new Size(270, 20), BackColor = Color.Transparent, AutoEllipsis = true };
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
            foreach (var x in allButtons) x.Enabled = !b;
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
                gmButton.Fill = Theme.Red; gmButton.Fill2 = Theme.RedDark; gmButton.GlowColor = Theme.Red; gmButton.IconName = null; gmButton.Spin = true; gmButton.Pulse = false;
                int n = GameMode.PausedCount;
                gmStatus.Text = "ON  •  " + n + " process paused";
                gmStatus.ForeColor = Theme.Green;
            }
            else
            {
                gmButton.Text = "START";
                gmButton.SubText = "apps pause + RAM khali";
                gmButton.Fill = Color.FromArgb(60, 240, 100); gmButton.Fill2 = Color.FromArgb(10, 140, 40); gmButton.GlowColor = Theme.Green; gmButton.IconName = "play"; gmButton.Spin = false; gmButton.Pulse = true;
                gmStatus.Text = "OFF  •  sab normal chal raha hai";
                gmStatus.ForeColor = Theme.SubText;
            }
            gmButton.Invalidate();
        }

        private double ramShown = -1; private int loadShown;
        private System.Windows.Forms.Timer ramTw;
        private void AnimateRam(int load, double free, double total)
        {
            Action<double, int> show = (f, l) => ramText.Text = string.Format("RAM:  {0}% use me   •   {1:N1} / {2:N1} GB khali", l, f, total);
            if (ramShown < 0 || !Anim.Active || Math.Abs(free - ramShown) < 0.15) { ramShown = free; loadShown = load; show(free, load); return; }
            double f0 = ramShown; int l0 = loadShown; int step = 0; const int N = 30;
            if (ramTw != null) { ramTw.Stop(); ramTw.Dispose(); }
            ramTw = new System.Windows.Forms.Timer { Interval = 20 };
            ramTw.Tick += (s, e) =>
            {
                step++; double k = Math.Min(1.0, step / (double)N); k = 1 - (1 - k) * (1 - k);
                ramShown = f0 + (free - f0) * k; loadShown = (int)Math.Round(l0 + (load - l0) * k);
                show(ramShown, loadShown);
                if (step >= N) { ramTw.Stop(); }
            };
            ramTw.Start();
        }

        private void RefreshLive()
        {
            double free = Native.FreeRamGB(), total = Native.TotalRamGB();
            int load = Native.RamLoadPercent();
            AnimateRam(load, free, total);
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
                return Tuple.Create(results, plan, Tweaks.TargetFps, Tweaks.MonitorHz());
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
                    if (fpsInfo != null) fpsInfo.Text = "Limit: " + t.Result.Item3 + " FPS   •   Monitor: " + t.Result.Item4 + " Hz";
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
                        updateText.Text = "Aap latest version pe ho  (v" + Updater.CurrentText + ")";
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
