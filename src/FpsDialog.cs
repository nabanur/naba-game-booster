using System;
using System.Drawing;
using System.Windows.Forms;

namespace NabaGameBooster
{
    /// <summary>FPS limit chunne ki chhoti window: Smooth (monitor ke barabar) / 120 / 240.</summary>
    internal class FpsDialog : Form
    {
        public int Chosen { get; private set; }

        public FpsDialog(int monitorHz, int current)
        {
            Text = "FPS limit chuno";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(440, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5F);

            Controls.Add(new Label { Text = "FPS limit chuno", Font = Theme.Semi(14F), ForeColor = Theme.Text, AutoSize = true, Location = new Point(20, 16) });
            Controls.Add(new Label
            {
                Text = "Tumhara monitor " + monitorHz + " Hz ka hai. FPS ek jagah tika rahe to game sabse smooth lagta hai.",
                ForeColor = Theme.SubText, Font = Theme.F(9F), Location = new Point(22, 48), Size = new Size(400, 36)
            });

            int rec = Math.Max(60, Math.Min(240, monitorHz));
            int y = 92;
            AddOption(rec + " FPS  -  Smooth (sujhaya)", "Monitor ke barabar, FPS tika rahega", rec, ref y, true, current);
            if (rec >= 75) AddOption((rec - 3) + " FPS  -  G-SYNC / FreeSync", "Sirf tab chuno jab monitor me FreeSync + NVIDIA me G-SYNC ON ho", rec - 3, ref y, false, current);
            if (rec != 120) AddOption("120 FPS  -  Stable", "Free Fire ka aam high setting", 120, ref y, false, current);
            if (rec != 240) AddOption("240 FPS  -  Max", "Sabse upar - kamzor PC pe upar-neeche ho sakta hai", 240, ref y, false, current);

            var cancel = new RoundButton { Text = "Cancel", Location = new Point(320, y + 4), Size = new Size(100, 32), Fill = Theme.Row, Font = Theme.Semi(9F), Radius = 8 };
            cancel.Click += (s, e) => { Chosen = 0; DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
            ClientSize = new Size(440, y + 50);
        }

        private void AddOption(string title, string sub, int fps, ref int y, bool recommended, int current)
        {
            var b = new RoundButton
            {
                Text = title + (current == fps ? "   ✓" : ""), SubText = sub,
                Location = new Point(20, y), Size = new Size(400, 62), Radius = 10,
                Fill = recommended ? Theme.Green : Theme.Row, Fill2 = recommended ? Theme.GreenDark : Color.Empty,
                Font = Theme.Semi(11F)
            };
            b.Click += (s, e) => { Chosen = fps; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(b);
            y += 70;
        }

        /// <summary>Dialog dikhata hai; chuna hua FPS ya 0 (cancel).</summary>
        public static int Ask(IWin32Window owner)
        {
            using (var d = new FpsDialog(Tweaks.MonitorHz(), Settings.FpsLimit))
                return d.ShowDialog(owner) == DialogResult.OK ? d.Chosen : 0;
        }
    }
}
