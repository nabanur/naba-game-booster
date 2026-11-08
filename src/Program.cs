using System;
using System.Threading;
using System.Windows.Forms;

namespace NabaGameBooster
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (!Elevation.IsAdmin())
            {
                // App pehle se khuli hai? (admin wali) - to dobara mat kholo
                bool open = false;
                try { using (Mutex.OpenExisting("NabaGameBooster_SingleInstance")) open = true; }
                catch (UnauthorizedAccessException) { open = true; }
                catch { }
                if (open)
                {
                    MessageBox.Show("Naba Game Booster pehle se khula hai.", "Naba Game Booster", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!Elevation.RelaunchElevated(args) && Array.IndexOf(args, "--from-uac") >= 0)
                    MessageBox.Show("Naba Game Booster ko Administrator permission chahiye.", "Naba Game Booster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Elevation.EnsureTask();

            bool first;
            using (var mutex = new Mutex(true, "NabaGameBooster_SingleInstance", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Naba Game Booster pehle se khula hai.", "Naba Game Booster", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
