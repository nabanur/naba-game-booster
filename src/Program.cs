using System;
using System.Threading;
using System.Windows.Forms;

namespace NabaGameBooster
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
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
