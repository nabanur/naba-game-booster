using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NabaGameBooster
{
    /// <summary>
    /// Headshot practice: head ke size ke target aate hain, jaldi aur sahi click karo.
    /// 30 second ka round - hits, accuracy, reaction time, best score.
    /// </summary>
    internal class AimTrainer : Control
    {
        private const int RoundSeconds = 30;

        private enum Mode { Idle, Countdown, Running, Done }
        private Mode mode = Mode.Idle;

        private readonly Random rnd = new Random();
        private readonly Timer tick = new Timer { Interval = 15 };
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Stopwatch targetClock = new Stopwatch();
        private readonly List<Fx> fx = new List<Fx>();

        private PointF target, velocity;
        private float radius;
        private int hits, misses;
        private double reactionSum;
        private Rectangle startBtn;

        public bool Moving;   // chalte target
        public bool Small;    // chhote target (mushkil)

        private class Fx { public PointF P; public bool Hit; public long T; }

        public AimTrainer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(16, 18, 24);
            Cursor = Cursors.Cross;
            tick.Tick += (s, e) => Step();
        }

        private static int Best { get { return Settings.GetInt("AimBest", 0); } set { Settings.SetInt("AimBest", value); } }
        private static int BestAcc { get { return Settings.GetInt("AimBestAcc", 0); } set { Settings.SetInt("AimBestAcc", value); } }

        public void StartRound()
        {
            hits = 0; misses = 0; reactionSum = 0; fx.Clear();
            mode = Mode.Countdown;
            clock.Restart();
            tick.Start();
            Focus();
        }

        private void NewTarget()
        {
            float min = Small ? 9 : 14, max = Small ? 15 : 24;
            radius = (float)(min + rnd.NextDouble() * (max - min));
            float pad = radius + 12;
            target = new PointF(pad + (float)rnd.NextDouble() * (Width - 2 * pad), 40 + pad + (float)rnd.NextDouble() * (Height - 40 - 2 * pad));
            if (Moving)
            {
                double ang = rnd.NextDouble() * Math.PI * 2;
                float sp = 1.6f + (float)rnd.NextDouble() * 1.8f;
                velocity = new PointF((float)Math.Cos(ang) * sp, (float)Math.Sin(ang) * sp);
            }
            else velocity = PointF.Empty;
            targetClock.Restart();
        }

        private void Step()
        {
            if (mode == Mode.Countdown && clock.Elapsed.TotalSeconds >= 3)
            {
                mode = Mode.Running; clock.Restart(); NewTarget();
            }
            else if (mode == Mode.Running)
            {
                if (clock.Elapsed.TotalSeconds >= RoundSeconds) { Finish(); return; }
                if (Moving)
                {
                    target.X += velocity.X; target.Y += velocity.Y;
                    if (target.X < radius + 4 || target.X > Width - radius - 4) velocity.X = -velocity.X;
                    if (target.Y < 40 + radius || target.Y > Height - radius - 4) velocity.Y = -velocity.Y;
                }
            }
            fx.RemoveAll(f => clock.ElapsedMilliseconds - f.T > 350 || f.T > clock.ElapsedMilliseconds);
            Invalidate();
        }

        private void Finish()
        {
            mode = Mode.Done;
            tick.Stop();
            int acc = Accuracy();
            if (hits > Best || (hits == Best && acc > BestAcc)) { Best = hits; BestAcc = acc; }
            Invalidate();
        }

        private int Accuracy() { int t = hits + misses; return t == 0 ? 0 : (int)Math.Round(100.0 * hits / t); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (mode == Mode.Idle || mode == Mode.Done)
            {
                if (startBtn.Contains(e.Location)) StartRound();
                return;
            }
            if (mode != Mode.Running) return;
            float dx = e.X - target.X, dy = e.Y - target.Y;
            bool hit = dx * dx + dy * dy <= radius * radius;
            fx.Add(new Fx { P = e.Location, Hit = hit, T = clock.ElapsedMilliseconds });
            if (hit)
            {
                hits++;
                reactionSum += targetClock.Elapsed.TotalMilliseconds;
                NewTarget();
            }
            else misses++;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(BackColor);

            // halki grid
            using (var p = new Pen(Color.FromArgb(24, 255, 255, 255)))
            {
                for (int x = 0; x < Width; x += 40) g.DrawLine(p, x, 36, x, Height);
                for (int y = 36; y < Height; y += 40) g.DrawLine(p, 0, y, Width, y);
            }

            // upar ki patti
            int left = mode == Mode.Running ? Math.Max(0, RoundSeconds - (int)clock.Elapsed.TotalSeconds) : RoundSeconds;
            string hud = string.Format("Time {0}s     Hits {1}     Accuracy {2}%     Best {3} ({4}%)", left, hits, Accuracy(), Best, BestAcc);
            using (var f = Theme.Semi(9.5F))
                TextRenderer.DrawText(g, hud, f, new Rectangle(12, 8, Width - 24, 22), Theme.SubText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (mode == Mode.Running)
            {
                float w = (Width - 24) * (float)(1 - clock.Elapsed.TotalSeconds / RoundSeconds);
                using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, 12, 32, Math.Max(0, w), 3);
            }

            if (mode == Mode.Running)
            {
                // target (head)
                var r = new RectangleF(target.X - radius, target.Y - radius, radius * 2, radius * 2);
                using (var b = new SolidBrush(Color.FromArgb(239, 68, 68))) g.FillEllipse(b, r);
                var r2 = RectangleF.Inflate(r, -radius * 0.38f, -radius * 0.38f);
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, r2);
                var r3 = RectangleF.Inflate(r, -radius * 0.7f, -radius * 0.7f);
                using (var b = new SolidBrush(Color.FromArgb(239, 68, 68))) g.FillEllipse(b, r3);
            }

            foreach (var f in fx)
            {
                float t = (clock.ElapsedMilliseconds - f.T) / 350f;
                int a = (int)(255 * (1 - t));
                float rr = 6 + 14 * t;
                using (var pen = new Pen(Color.FromArgb(Math.Max(0, a), f.Hit ? Theme.Green : Theme.Red), 2f))
                    g.DrawEllipse(pen, f.P.X - rr, f.P.Y - rr, rr * 2, rr * 2);
            }

            if (mode == Mode.Countdown)
            {
                int n = 3 - (int)clock.Elapsed.TotalSeconds;
                using (var f = Theme.Semi(48F))
                    TextRenderer.DrawText(g, n.ToString(), f, ClientRectangle, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (mode == Mode.Idle || mode == Mode.Done)
            {
                string title, sub;
                if (mode == Mode.Idle)
                {
                    title = "HEADSHOT AIM TRAINER";
                    sub = "30 second. Laal-safed target = head. Jitna jaldi aur sahi click, utna achha.";
                }
                else
                {
                    double avg = hits == 0 ? 0 : reactionSum / hits;
                    title = string.Format("{0} hits   •   {1}% accuracy", hits, Accuracy());
                    sub = string.Format("Average reaction: {0:N0} ms   •   Best: {1} hits ({2}%)", avg, Best, BestAcc);
                }
                using (var f = Theme.Semi(16F))
                    TextRenderer.DrawText(g, title, f, new Rectangle(0, Height / 2 - 70, Width, 34), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                using (var f = Theme.F(9.5F))
                    TextRenderer.DrawText(g, sub, f, new Rectangle(20, Height / 2 - 34, Width - 40, 40), Theme.SubText, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);

                startBtn = new Rectangle(Width / 2 - 90, Height / 2 + 16, 180, 46);
                using (var path = Theme.Round(startBtn, 12))
                using (var b = new LinearGradientBrush(startBtn, Theme.Green, Theme.GreenDark, LinearGradientMode.Horizontal))
                    g.FillPath(b, path);
                using (var f = Theme.Semi(12F))
                    TextRenderer.DrawText(g, mode == Mode.Idle ? "START" : "DOBARA", f, startBtn, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tick.Dispose();
            base.Dispose(disposing);
        }
    }
}
