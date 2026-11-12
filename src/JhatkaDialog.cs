using System;
using System.Drawing;
using System.Windows.Forms;

namespace NabaGameBooster
{
    /// <summary>Game Mode STOP ke baad Jhatka report.</summary>
    internal class JhatkaDialog : Form
    {
        public JhatkaDialog(string report)
        {
            Text = "Jhatka Report";
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(620, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5F);

            Controls.Add(new IconBox { IconName = "eye", IconColor = Color.FromArgb(150, 120, 255), Location = new Point(22, 20), Size = new Size(28, 28) });
            Controls.Add(new Label { Text = "JHATKA REPORT", Font = Theme.Heavy(16F), ForeColor = Color.FromArgb(170, 140, 255), AutoSize = true, Location = new Point(56, 16) });
            Controls.Add(new Label { Text = "Game Mode ke dauran jo pakda gaya - asli wajah aur fix", Font = Theme.F(9F), ForeColor = Theme.SubText, AutoSize = true, Location = new Point(58, 50) });

            var box = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                BackColor = Theme.Card, ForeColor = Theme.Text, Font = new Font("Consolas", 9.5F),
                Location = new Point(24, 84), Size = new Size(572, 410),
                Text = (report ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n")
            };
            Controls.Add(box);

            var folder = new RoundButton { Text = "Data folder", Location = new Point(24, 508), Size = new Size(170, 38), Outline = true, OutlineColor = Theme.Purple, ChamferShape = true, Font = Theme.Semi(9.5F) };
            folder.Click += (s, e) => { try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Jhatka.Folder + "\""); } catch { } };
            var ok = new RoundButton { Text = "Theek hai", Location = new Point(426, 508), Size = new Size(170, 38), Fill = Theme.Purple, Fill2 = Theme.PurpleDark, ChamferShape = true, Font = Theme.Semi(10F) };
            ok.Click += (s, e) => Close();
            Controls.Add(folder); Controls.Add(ok);
            Shown += (s, e) => { box.SelectionStart = 0; box.SelectionLength = 0; ok.Focus(); };
        }
    }
}
