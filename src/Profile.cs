using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace NabaGameBooster
{
    internal enum Tier { Low, Mid, High }

    /// <summary>PC ko pehchan ke Low / Mid / High profile aur uske hisaab se BlueStacks settings.</summary>
    internal class PcProfile
    {
        public Tier Tier;
        public string Cpu = "";
        public string Gpu = "";
        public bool DedicatedGpu;
        public int RamGb;
        public int PhysicalCores;
        public int Threads;
        public int MonitorHz;

        // Sujhai gayi emulator settings
        public int BsRamMb, BsCpus, Fps;
        public int Width, Height, Dpi;   // 0 = resolution mat badlo

        public string TierName { get { return Tier == Tier.High ? "High-end" : Tier == Tier.Mid ? "Mid-range" : "Low-end"; } }

        /// <summary>Free Fire MAX me lagbhag kitna FPS (Fast Mode + x86 version ke saath).</summary>
        public string ScoreRange
        {
            get { return Tier == Tier.High ? "90 - " + Fps + " FPS" : Tier == Tier.Mid ? "60 - " + Fps + " FPS" : "40 - 60 FPS"; }
        }

        public int Score
        {
            get
            {
                int s = Math.Min(20, RamGb);                         // RAM (16 GB = 16)
                s += Math.Min(25, PhysicalCores * 3);                // CPU core (6 = 18)
                s += GpuPoints();                                    // graphics
                s += MonitorHz >= 144 ? 10 : MonitorHz >= 100 ? 8 : 5;
                return Math.Max(10, Math.Min(100, s));
            }
        }

        private int GpuPoints()
        {
            if (!DedicatedGpu) return 8;
            string g = Gpu;
            if (Regex.IsMatch(g, @"RTX\s*[45]0\d0|RX\s*[79]\d00", RegexOptions.IgnoreCase)) return 40;
            if (Regex.IsMatch(g, @"RTX\s*30\d0|RX\s*6\d00|Arc", RegexOptions.IgnoreCase)) return 32;
            if (Regex.IsMatch(g, @"RTX\s*20\d0|GTX\s*16\d0|RX\s*5\d00", RegexOptions.IgnoreCase)) return 25;
            return 18;
        }

        private static readonly Regex Integrated = new Regex(@"Intel\(R\) (UHD|HD|Iris)|AMD Radeon\(TM\) Graphics|Radeon.*Vega|Microsoft Basic", RegexOptions.IgnoreCase);
        private static readonly Regex Discrete = new Regex(@"GeForce|RTX|GTX|Quadro|Radeon.*\bRX\b|Radeon Pro|Arc(\(TM\))? A\d", RegexOptions.IgnoreCase);

        public static PcProfile Detect()
        {
            var p = new PcProfile();
            p.RamGb = (int)Math.Round(Native.TotalRamGB());
            p.Threads = Environment.ProcessorCount;
            p.MonitorHz = Tweaks.MonitorHz();
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor"))
                    foreach (ManagementObject mo in q.Get())
                    {
                        p.Cpu = (mo["Name"] as string ?? "").Trim();
                        p.PhysicalCores += Convert.ToInt32(mo["NumberOfCores"] ?? 0);
                    }
            }
            catch { }
            if (p.PhysicalCores <= 0) p.PhysicalCores = Math.Max(1, p.Threads / 2);

            var gpus = new List<string>();
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    foreach (ManagementObject mo in q.Get()) { var n = mo["Name"] as string; if (!string.IsNullOrEmpty(n)) gpus.Add(n.Trim()); }
            }
            catch { }
            var disc = gpus.FirstOrDefault(g => Discrete.IsMatch(g) && !Integrated.IsMatch(g));
            p.DedicatedGpu = disc != null;
            p.Gpu = disc ?? gpus.FirstOrDefault() ?? "?";

            // Tier
            if (p.RamGb < 7 || p.PhysicalCores <= 2) p.Tier = Tier.Low;
            else if (p.DedicatedGpu && p.RamGb >= 12 && p.PhysicalCores >= 4) p.Tier = Tier.High;
            else p.Tier = Tier.Mid;

            int hz = Math.Max(60, Math.Min(240, p.MonitorHz));
            switch (p.Tier)
            {
                case Tier.High:
                    p.BsRamMb = p.RamGb >= 15 ? 6144 : 4096;
                    p.BsCpus = p.PhysicalCores >= 6 ? 6 : 4;
                    p.Fps = hz;
                    break;
                case Tier.Mid:
                    p.BsRamMb = p.RamGb >= 12 ? 4096 : 3072;
                    p.BsCpus = p.PhysicalCores >= 4 ? 4 : 2;
                    p.Fps = Math.Max(60, Math.Min(hz, 75));
                    p.Width = 1600; p.Height = 900; p.Dpi = 240;
                    break;
                default:
                    p.BsRamMb = p.RamGb >= 7 ? 3072 : 2048;
                    p.BsCpus = 2;
                    p.Fps = 60;
                    p.Width = 1280; p.Height = 720; p.Dpi = 240;
                    break;
            }
            return p;
        }

        private static PcProfile cached;
        public static PcProfile Current { get { if (cached == null) cached = Detect(); return cached; } }
    }
}
