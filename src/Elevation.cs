using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace NabaGameBooster
{
    /// <summary>
    /// Har baar UAC (Yes/No) na aaye: pehli baar admin permission milne par Windows Task Scheduler me
    /// ek task banta hai ("highest privileges"). Agli baar shortcut khulte hi app usi task se seedha admin
    /// mode me chal jaati hai - koi popup nahi. Pro apps (MSI Afterburner jaise) yahi tareeka use karte hain.
    /// </summary>
    internal static class Elevation
    {
        public const string TaskName = "NabaGameBooster";

        public static bool IsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static string ExePath { get { return Process.GetCurrentProcess().MainModule.FileName; } }

        private static int Schtasks(string args, out string output, int timeoutMs = 10000)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.Default
                };
                using (var p = Process.Start(psi))
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return p.HasExited ? p.ExitCode : -1;
                }
            }
            catch { return -1; }
        }

        /// <summary>Normal (bina admin) chalne par: task se admin app kholo. Task na ho to ek baar UAC.</summary>
        /// <returns>true = naya admin process shuru ho gaya, ye wala band ho jaye.</returns>
        public static bool RelaunchElevated(string[] args)
        {
            bool fromTask = Array.IndexOf(args, "--from-task") >= 0, fromUac = Array.IndexOf(args, "--from-uac") >= 0;
            if (fromUac) return false;                       // UAC ke baad bhi admin nahi - loop mat karo
            string o;
            if (!fromTask && Schtasks("/run /tn \"" + TaskName + "\"", out o) == 0) return true;
            try
            {
                Process.Start(new ProcessStartInfo(ExePath, "--from-uac") { UseShellExecute = true, Verb = "runas" });
                return true;
            }
            catch { return false; }   // user ne No dabaya
        }

        /// <summary>Admin mode me: task bana do / sahi path pe update karo (background me).</summary>
        public static void EnsureTask()
        {
            new Thread(() =>
            {
                try
                {
                    string exe = ExePath;
                    string cur;
                    if (Schtasks("/query /tn \"" + TaskName + "\" /xml", out cur) == 0 &&
                        cur.IndexOf(exe, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        cur.IndexOf("--from-task", StringComparison.Ordinal) >= 0) return;

                    string user;
                    using (var id = WindowsIdentity.GetCurrent()) user = id.Name;
                    Func<string, string> X = s => System.Security.SecurityElement.Escape(s);
                    string xml =
"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
"  <RegistrationInfo><Description>Naba Game Booster ko bina UAC popup ke kholne ke liye</Description></RegistrationInfo>\r\n" +
"  <Principals><Principal id=\"Author\"><UserId>" + X(user) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
"  <Settings>\r\n" +
"    <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>\r\n" +
"    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
"    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
"    <AllowHardTerminate>true</AllowHardTerminate>\r\n" +
"    <StartWhenAvailable>false</StartWhenAvailable>\r\n" +
"    <AllowStartOnDemand>true</AllowStartOnDemand>\r\n" +
"    <Enabled>true</Enabled>\r\n" +
"    <Hidden>false</Hidden>\r\n" +
"    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
"    <Priority>4</Priority>\r\n" +
"  </Settings>\r\n" +
"  <Actions Context=\"Author\"><Exec><Command>\"" + X(exe) + "\"</Command><Arguments>--from-task</Arguments><WorkingDirectory>" + X(Path.GetDirectoryName(exe)) + "</WorkingDirectory></Exec></Actions>\r\n" +
"</Task>\r\n";
                    string tmp = Path.Combine(Path.GetTempPath(), "ngb_task.xml");
                    File.WriteAllText(tmp, xml, Encoding.Unicode);
                    string o;
                    Schtasks("/create /tn \"" + TaskName + "\" /xml \"" + tmp + "\" /f", out o);
                    try { File.Delete(tmp); } catch { }
                }
                catch { }
            }) { IsBackground = true }.Start();
        }
    }
}
