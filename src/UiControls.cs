using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NabaGameBooster
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(10, 11, 22);
        public static readonly Color Bg2 = Color.FromArgb(18, 12, 34);
        public static readonly Color Card = Color.FromArgb(16, 18, 34);
        public static readonly Color CardBorder = Color.FromArgb(46, 44, 80);
        public static readonly Color Row = Color.FromArgb(22, 25, 44);
        public static readonly Color RowBorder = Color.FromArgb(42, 46, 74);
        public static readonly Color RowHover = Color.FromArgb(30, 34, 58);
        public static readonly Color Text = Color.FromArgb(240, 242, 250);
        public static readonly Color SubText = Color.FromArgb(150, 156, 184);
        public static readonly Color Accent = Color.FromArgb(255, 150, 30);
        public static readonly Color AccentDark = Color.FromArgb(230, 92, 0);
        public static readonly Color Purple = Color.FromArgb(150, 80, 255);
        public static readonly Color PurpleDark = Color.FromArgb(90, 40, 200);
        public static readonly Color Green = Color.FromArgb(40, 230, 90);
        public static readonly Color GreenDark = Color.FromArgb(10, 150, 50);
        public static readonly Color Red = Color.FromArgb(255, 64, 84);
        public static readonly Color RedDark = Color.FromArgb(170, 20, 50);
        public static readonly Color Amber = Color.FromArgb(255, 190, 40);
        public static readonly Color Blue = Color.FromArgb(60, 160, 255);
        public static readonly Color Grey = Color.FromArgb(78, 84, 110);

        public static Font F(float size, FontStyle style = FontStyle.Regular) { return new Font("Segoe UI", size, style); }

        public static Font Semi(float size)
        {
            try { return new Font("Segoe UI Semibold", size); } catch { return new Font("Segoe UI", size, FontStyle.Bold); }
        }

        /// <summary>Gaming heading font: Segoe UI Black Italic (na mile to Bold Italic).</summary>
        public static Font Heavy(float size)
        {
            try
            {
                var f = new Font("Segoe UI Black", size, FontStyle.Italic);
                if (f.Name == "Segoe UI Black") return f;
                f.Dispose();
            }
            catch { }
            return new Font("Segoe UI", size, FontStyle.Bold | FontStyle.Italic);
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Kate hue kone (gaming style): upar-baayen aur neeche-daayen.</summary>
        public static GraphicsPath Chamfer(RectangleF r, float c)
        {
            var p = new GraphicsPath();
            float s = c * 0.45f;
            p.AddPolygon(new[]
            {
                new PointF(r.X + c, r.Y), new PointF(r.Right - s, r.Y), new PointF(r.Right, r.Y + s),
                new PointF(r.Right, r.Bottom - c), new PointF(r.Right - c, r.Bottom), new PointF(r.X + s, r.Bottom),
                new PointF(r.X, r.Bottom - s), new PointF(r.X, r.Y + c)
            });
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        /// <summary>Neon chamak: path ke chaaron taraf halki moti lakeerein.</summary>
        public static void Glow(Graphics g, GraphicsPath p, Color c, int layers = 5, float baseWidth = 1.5f, int alpha = 70)
        {
            for (int i = layers; i >= 1; i--)
            {
                using (var pen = new Pen(Color.FromArgb(Math.Max(4, alpha / (i + 1)), c), baseWidth + i * 2.6f) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, p);
            }
        }

        public static void GradientBorder(Graphics g, GraphicsPath p, RectangleF r, Color a, Color b, float width)
        {
            using (var br = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), a, b, 25f))
            using (var pen = new Pen(br, width) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, p);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    // ======================= ANIMATION ENGINE =======================
    /// <summary>
    /// Saari animation yahin se chalti hai. Sirf tab chalti hai jab app samne ho aur mouse
    /// app ke andar ho. Minimize / peeche / mouse bahar / Game Mode ON = sab band, 0% CPU.
    /// </summary>
    internal static class Anim
    {
        private static Form owner;
        private static readonly Timer fast = new Timer { Interval = 33 };
        private static readonly Timer poll = new Timer { Interval = 150 };
        private static readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        public static bool Active { get; private set; }
        public static float T { get { return (float)sw.Elapsed.TotalSeconds; } }
        public static event EventHandler Frame;
        public static Func<bool> Blocked;
        public static readonly Random Rnd = new Random();

        public static void Attach(Form f)
        {
            owner = f;
            fast.Tick += (s, e) => Raise();
            poll.Tick += (s, e) => Check();
            poll.Start();
        }

        private static void Check()
        {
            bool want = false;
            try
            {
                want = owner != null && owner.Visible && owner.WindowState != FormWindowState.Minimized
                       && Form.ActiveForm == owner && owner.Bounds.Contains(Cursor.Position)
                       && !(Blocked != null && Blocked());
            }
            catch { }
            if (want == Active) return;
            Active = want;
            if (want) fast.Start();
            else { fast.Stop(); Raise(); }   // ek aakhri frame - sab static ho jaye
        }

        private static void Raise()
        {
            var h = Frame;
            if (h != null) try { h(null, EventArgs.Empty); } catch { }
        }

        /// <summary>0..1 saans jaisa (sin). Band ho to 0.5.</summary>
        public static float Pulse(float speed)
        {
            return Active ? 0.5f + 0.5f * (float)Math.Sin(T * speed * Math.PI) : 0.5f;
        }
    }

    /// <summary>Animation helpers: path pe chalti light, bijli.</summary>
    internal static class Fx
    {
        public static PointF[] Flat(GraphicsPath p)
        {
            using (var c = (GraphicsPath)p.Clone()) { c.Flatten(); return c.PathPoints; }
        }

        public static float Perimeter(PointF[] pts)
        {
            float per = 0;
            for (int i = 0; i < pts.Length; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Length]; per += (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)); }
            return per;
        }

        public static PointF At(PointF[] pts, float per, float d)
        {
            d %= per; if (d < 0) d += per;
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % pts.Length];
                float len = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                if (d <= len && len > 0) { float t = d / len; return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }
                d -= len;
            }
            return pts[0];
        }

        /// <summary>Path pe patli, halki roshni ka tukda (bina chamakte gole ke) - premium ring jaisa.</summary>
        public static void Sweep(Graphics g, PointF[] pts, float per, float head, float length, Color c, float width)
        {
            const int N = 24;
            float step = length / N;
            for (int k = 0; k < N; k++)
            {
                float t = 1f - (float)k / N;           // aage tez, peeche fade
                float fadeIn = Math.Min(1f, k / 4f);  // aage ka sira bhi narm
                var p1 = At(pts, per, head - k * step); var p0 = At(pts, per, head - (k + 1) * step);
                using (var pen = new Pen(Color.FromArgb((int)(230 * t * (0.4f + 0.6f * fadeIn)), c), width) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, p0, p1);
            }
        }
    }

    // ======================= ICONS (code se bane, koi image file nahi) =======================
    internal static class Icons
    {
        public static void Draw(Graphics g, string name, RectangleF r, Color c, float stroke = 1.7f)
        {
            if (string.IsNullOrEmpty(name)) return;
            Theme.Smooth(g);
            float x = r.X, y = r.Y, w = r.Width, h = r.Height;
            Func<float, float, PointF> P = (px, py) => new PointF(x + px * w, y + py * h);
            using (var pen = new Pen(c, stroke) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var br = new SolidBrush(c))
            {
                switch (name)
                {
                    case "bolt":
                        g.FillPolygon(br, new[] { P(.58f, .02f), P(.18f, .56f), P(.46f, .56f), P(.36f, .98f), P(.82f, .40f), P(.53f, .40f), P(.66f, .02f) });
                        break;
                    case "play":
                        g.FillPolygon(br, new[] { P(.18f, .08f), P(.92f, .5f), P(.18f, .92f) });
                        break;
                    case "game":
                        using (var gp = Theme.Round(new RectangleF(x + w * .04f, y + h * .24f, w * .92f, h * .56f), h * .26f)) g.DrawPath(pen, gp);
                        g.DrawLine(pen, P(.22f, .52f), P(.38f, .52f)); g.DrawLine(pen, P(.30f, .44f), P(.30f, .60f));
                        g.FillEllipse(br, x + w * .62f, y + h * .40f, w * .09f, h * .09f); g.FillEllipse(br, x + w * .74f, y + h * .52f, w * .09f, h * .09f);
                        break;
                    case "layers":
                        g.DrawPolygon(pen, new[] { P(.5f, .12f), P(.94f, .34f), P(.5f, .56f), P(.06f, .34f) });
                        g.DrawLines(pen, new[] { P(.06f, .52f), P(.5f, .74f), P(.94f, .52f) });
                        g.DrawLines(pen, new[] { P(.06f, .70f), P(.5f, .92f), P(.94f, .70f) });
                        break;
                    case "rocket":
                        g.DrawPolygon(pen, new[] { P(.86f, .14f), P(.66f, .58f), P(.42f, .58f), P(.42f, .34f) });
                        g.DrawLines(pen, new[] { P(.42f, .40f), P(.22f, .42f), P(.12f, .56f), P(.30f, .56f) });
                        g.DrawLines(pen, new[] { P(.60f, .58f), P(.58f, .78f), P(.44f, .88f), P(.44f, .70f) });
                        g.DrawLine(pen, P(.30f, .70f), P(.14f, .86f));
                        g.DrawEllipse(pen, x + w * .58f, y + h * .28f, w * .12f, h * .12f);
                        break;
                    case "cubes":
                        DrawCube(g, pen, P(.5f, .08f), w * .24f, h);
                        DrawCube(g, pen, P(.27f, .50f), w * .24f, h);
                        DrawCube(g, pen, P(.73f, .50f), w * .24f, h);
                        break;
                    case "shield":
                        g.DrawPolygon(pen, new[] { P(.5f, .06f), P(.88f, .2f), P(.84f, .58f), P(.5f, .94f), P(.16f, .58f), P(.12f, .2f) });
                        g.DrawLines(pen, new[] { P(.34f, .5f), P(.46f, .62f), P(.68f, .38f) });
                        break;
                    case "chip":
                        g.DrawRectangle(pen, x + w * .22f, y + h * .22f, w * .56f, h * .56f);
                        for (int i = 0; i < 3; i++)
                        {
                            float t = .34f + i * .16f;
                            g.DrawLine(pen, P(t, .06f), P(t, .22f)); g.DrawLine(pen, P(t, .78f), P(t, .94f));
                            g.DrawLine(pen, P(.06f, t), P(.22f, t)); g.DrawLine(pen, P(.78f, t), P(.94f, t));
                        }
                        break;
                    case "broom":
                        g.DrawLine(pen, P(.86f, .08f), P(.52f, .50f));
                        g.FillPolygon(br, new[] { P(.44f, .44f), P(.62f, .58f), P(.42f, .94f), P(.10f, .86f), P(.18f, .62f) });
                        break;
                    case "monitor":
                        g.DrawRectangle(pen, x + w * .06f, y + h * .12f, w * .88f, h * .58f);
                        g.DrawLine(pen, P(.5f, .70f), P(.5f, .86f)); g.DrawLine(pen, P(.28f, .88f), P(.72f, .88f));
                        break;
                    case "terminal":
                        g.DrawRectangle(pen, x + w * .06f, y + h * .14f, w * .88f, h * .72f);
                        g.DrawLines(pen, new[] { P(.22f, .36f), P(.38f, .50f), P(.22f, .64f) });
                        g.DrawLine(pen, P(.46f, .66f), P(.70f, .66f));
                        break;
                    case "chat":
                        using (var gp = Theme.Round(new RectangleF(x + w * .08f, y + h * .14f, w * .84f, h * .56f), h * .2f)) g.DrawPath(pen, gp);
                        g.DrawLines(pen, new[] { P(.30f, .70f), P(.24f, .90f), P(.48f, .70f) });
                        g.FillEllipse(br, x + w * .30f, y + h * .38f, w * .08f, h * .08f); g.FillEllipse(br, x + w * .46f, y + h * .38f, w * .08f, h * .08f); g.FillEllipse(br, x + w * .62f, y + h * .38f, w * .08f, h * .08f);
                        break;
                    case "windows":
                        g.FillPolygon(br, new[] { P(.08f, .18f), P(.44f, .12f), P(.44f, .47f), P(.08f, .47f) });
                        g.FillPolygon(br, new[] { P(.50f, .11f), P(.92f, .05f), P(.92f, .47f), P(.50f, .47f) });
                        g.FillPolygon(br, new[] { P(.08f, .53f), P(.44f, .53f), P(.44f, .88f), P(.08f, .82f) });
                        g.FillPolygon(br, new[] { P(.50f, .53f), P(.92f, .53f), P(.92f, .95f), P(.50f, .89f) });
                        break;
                    case "gear":
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            g.DrawLine(new Pen(c, stroke * 2.2f), (float)(x + w * (.5 + .30 * Math.Cos(a))), (float)(y + h * (.5 + .30 * Math.Sin(a))), (float)(x + w * (.5 + .44 * Math.Cos(a))), (float)(y + h * (.5 + .44 * Math.Sin(a))));
                        }
                        g.FillEllipse(br, x + w * .2f, y + h * .2f, w * .6f, h * .6f);
                        using (var hole = new SolidBrush(Theme.Card)) g.FillEllipse(hole, x + w * .37f, y + h * .37f, w * .26f, h * .26f);
                        break;
                    case "check":
                        g.DrawLines(new Pen(c, stroke * 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }, new[] { P(.12f, .52f), P(.40f, .78f), P(.90f, .22f) });
                        break;
                    case "refresh":
                        g.DrawArc(pen, x + w * .14f, y + h * .14f, w * .72f, h * .72f, 300, 290);
                        g.FillPolygon(br, new[] { P(.68f, .02f), P(.92f, .22f), P(.62f, .32f) });
                        break;
                    case "ram":
                        g.DrawRectangle(pen, x + w * .06f, y + h * .28f, w * .88f, h * .40f);
                        for (int i = 0; i < 4; i++) g.FillRectangle(br, x + w * (.16f + i * .2f), y + h * .38f, w * .1f, h * .2f);
                        for (int i = 0; i < 5; i++) g.DrawLine(pen, P(.14f + i * .18f, .68f), P(.14f + i * .18f, .80f));
                        break;
                    case "timer":
                        g.DrawEllipse(pen, x + w * .14f, y + h * .18f, w * .72f, h * .72f);
                        g.DrawLine(pen, P(.5f, .54f), P(.5f, .34f)); g.DrawLine(pen, P(.5f, .54f), P(.64f, .62f));
                        g.DrawLine(pen, P(.40f, .06f), P(.60f, .06f));
                        break;
                    case "mouse":
                        using (var gp = Theme.Round(new RectangleF(x + w * .24f, y + h * .06f, w * .52f, h * .88f), w * .26f)) g.DrawPath(pen, gp);
                        g.DrawLine(pen, P(.5f, .16f), P(.5f, .34f));
                        break;
                    case "lock":
                        g.DrawRectangle(pen, x + w * .18f, y + h * .44f, w * .64f, h * .48f);
                        g.DrawArc(pen, x + w * .30f, y + h * .10f, w * .40f, h * .62f, 180, 180);
                        break;
                    case "eye":
                        g.DrawBezier(pen, P(.04f, .5f), P(.30f, .12f), P(.70f, .12f), P(.96f, .5f));
                        g.DrawBezier(pen, P(.04f, .5f), P(.30f, .88f), P(.70f, .88f), P(.96f, .5f));
                        g.FillEllipse(br, x + w * .38f, y + h * .38f, w * .24f, h * .24f);
                        break;
                    case "list":
                        for (int i = 0; i < 3; i++)
                        {
                            float yy = .24f + i * .26f;
                            g.FillEllipse(br, x + w * .06f, y + h * (yy - .05f), w * .10f, h * .10f);
                            g.DrawLine(pen, P(.28f, yy), P(.94f, yy));
                        }
                        break;
                    case "fps":
                        g.DrawArc(pen, x + w * .06f, y + h * .16f, w * .88f, h * .88f, 180, 180);
                        g.DrawLine(pen, P(.5f, .60f), P(.76f, .34f));
                        g.FillEllipse(br, x + w * .44f, y + h * .54f, w * .12f, h * .12f);
                        break;
                }
            }
        }

        private static void DrawCube(Graphics g, Pen pen, PointF top, float s, float h)
        {
            float k = s * 0.55f;
            var a = new PointF(top.X, top.Y); var b = new PointF(top.X + s, top.Y + k); var c = new PointF(top.X, top.Y + 2 * k); var d = new PointF(top.X - s, top.Y + k);
            g.DrawPolygon(pen, new[] { a, b, c, d });
            g.DrawLines(pen, new[] { d, new PointF(d.X, d.Y + s * .9f), new PointF(c.X, c.Y + s * .9f), new PointF(b.X, b.Y + s * .9f), b });
            g.DrawLine(pen, c, new PointF(c.X, c.Y + s * .9f));
        }
    }

    /// <summary>Chhota icon control.</summary>
    internal class IconBox : Control
    {
        public string IconName;
        public Color IconColor = Theme.Accent;
        public float Stroke = 1.7f;
        public IconBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; Size = new Size(24, 24);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Icons.Draw(e.Graphics, IconName, new RectangleF(2, 2, Width - 4, Height - 4), Enabled ? IconColor : Theme.Grey, Stroke);
        }
    }

    /// <summary>Gaming section heading: icon + MOTA ITALIC naam + orange //.</summary>
    internal class NeonHeader : Control
    {
        public string IconName;
        public NeonHeader(string text, string icon)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; Text = text; IconName = icon;
            Font = Theme.Heavy(13F); Size = new Size(260, 30);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            int x = 0;
            if (!string.IsNullOrEmpty(IconName))
            {
                Icons.Draw(g, IconName, new RectangleF(0, 4, 22, 22), Theme.Accent);
                x = 30;
            }
            var sz = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Text, Font, new Point(x, (Height - sz.Height) / 2), Theme.Text, TextFormatFlags.NoPadding);
            float sx = x + sz.Width + 8;
            using (var b1 = new SolidBrush(Theme.Accent)) using (var b2 = new SolidBrush(Color.FromArgb(150, Theme.AccentDark)))
            {
                g.FillPolygon(b1, new[] { new PointF(sx + 6, 9), new PointF(sx + 12, 9), new PointF(sx + 6, 22), new PointF(sx, 22) });
                g.FillPolygon(b2, new[] { new PointF(sx + 16, 9), new PointF(sx + 22, 9), new PointF(sx + 16, 22), new PointF(sx + 10, 22) });
            }
        }
    }

    /// <summary>Card: Flat (rounded) ya Neon (kate kone + purple-orange chamakta border).</summary>
    internal class Card : Panel
    {
        public int Radius = 12;
        public Color Fill = Theme.Card;
        public Color Border = Theme.CardBorder;
        public bool Neon;
        public float ChamferSize = 18;
        public Color NeonA = Theme.Purple, NeonB = Theme.Accent;
        public bool HoverGlow = true;           // sirf flat rows pe
        private float hov;                       // 0..1 hover chamak
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Anim.Frame += OnFrame; }
        protected override void OnHandleDestroyed(EventArgs e) { Anim.Frame -= OnFrame; base.OnHandleDestroyed(e); }

        private void OnFrame(object s, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated || !Visible) return;
            if (!Neon && !HoverGlow) return;
            bool want = false;
            if (Anim.Active) try { want = RectangleToScreen(ClientRectangle).Contains(Cursor.Position); } catch { }
            float target = want ? 1f : 0f;
            if (Math.Abs(hov - target) < 0.001f) return;
            hov += (target - hov) * (Neon ? 0.18f : 0.3f);
            if (Math.Abs(hov - target) < 0.03f) hov = target;
            if (Neon)
            {
                using (var reg = new Region(ClientRectangle))
                {
                    reg.Exclude(Rectangle.Inflate(ClientRectangle, -17, -17));
                    Invalidate(reg, false);
                }
            }
            else Invalidate(true);
        }

        public Card()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);   // parent ka background (pattern) dikhe
            var g = e.Graphics;
            Theme.Smooth(g);
            if (Neon)
            {
                var r = new RectangleF(7, 7, Width - 15, Height - 15);
                using (var path = Theme.Chamfer(r, ChamferSize))
                {
                    Theme.Glow(g, path, Theme.Mix(NeonA, NeonB, .5f), 3, 1.5f, (int)(60 + 60 * hov));
                    using (var b = new SolidBrush(Fill)) g.FillPath(b, path);
                    Theme.GradientBorder(g, path, r, ControlPaint.Light(NeonA, .35f * hov), ControlPaint.Light(NeonB, .35f * hov), 1.6f + hov * .6f);
                }
            }
            else
            {
                var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                using (var path = Theme.Round(r, Radius))
                using (var b = new SolidBrush(Theme.Mix(Fill, Color.FromArgb(30, 30, 56), hov)))
                using (var pen = new Pen(Theme.Mix(Border, Color.FromArgb(110, 80, 200), hov), 1f))
                {
                    g.FillPath(b, path);
                    if (hov > 0.01f)
                    {
                        using (var bar = Theme.Round(new RectangleF(r.X + 2, r.Y + 10, 2.5f, r.Height - 20), 1.2f))
                        using (var bb = new SolidBrush(Color.FromArgb((int)(220 * hov), Theme.Accent))) g.FillPath(bb, bar);
                    }
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    /// <summary>iPhone jaisa ON/OFF switch - ON pe hari chamak.</summary>
    [DefaultEvent("Toggled")]
    internal class ToggleSwitch : Control
    {
        private bool on;
        private float knob, flash;
        private readonly Timer anim = new Timer { Interval = 15 };
        public event EventHandler Toggled;
        public Color OnColor = Theme.Green;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
            anim.Tick += (s, e) =>
            {
                float target = on ? 1f : 0f;
                knob += (target - knob) * 0.35f;
                if (Math.Abs(target - knob) < 0.02f) knob = target;
                if (flash > 0) flash = Math.Max(0, flash - 0.045f);
                if (knob == target && flash <= 0) anim.Stop();
                Invalidate();
            };
        }

        [DefaultValue(false)]
        public bool Checked { get { return on; } set { if (on == value) return; on = value; anim.Start(); } }

        protected override void OnClick(EventArgs e) { base.OnClick(e); if (Enabled && Toggled != null) Toggled(this, EventArgs.Empty); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            var r = new RectangleF(3, 3, Width - 7, Height - 7);
            Color track = Theme.Mix(Color.FromArgb(52, 56, 80), OnColor, knob);
            if (!Enabled) track = Color.FromArgb(40, 42, 60);
            using (var p = Theme.Round(r, r.Height / 2))
            {
                if (Enabled && knob > 0.3f) Theme.Glow(g, p, OnColor, 2, 1f, (int)(90 * knob));
                using (var b = new SolidBrush(track)) g.FillPath(b, p);
            }
            float d = r.Height - 6;
            float x = r.X + 3 + (r.Width - d - 6) * knob;
            using (var b = new SolidBrush(Enabled ? Color.White : Color.FromArgb(110, 115, 128))) g.FillEllipse(b, x, r.Y + 3, d, d);
        }
    }

    /// <summary>Neon status tag: ON / OFF / ADHURA / RESTART / N/A.</summary>
    internal class Pill : Control
    {
        private FeatureState state = FeatureState.Off;
        private bool loading = true;

        public Pill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(84, 26);
            Font = Theme.Semi(8.5F);
        }

        public void Set(FeatureState s) { state = s; loading = false; Invalidate(); }
        public void SetLoading() { loading = true; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            string text; Color c; bool glow = true;
            if (loading) { text = "CHECK..."; c = Theme.Grey; glow = false; }
            else
            {
                switch (state)
                {
                    case FeatureState.On: text = "ON"; c = Theme.Green; break;
                    case FeatureState.Partial: text = "ADHURA"; c = Theme.Amber; break;
                    case FeatureState.Pending: text = "RESTART"; c = Theme.Blue; break;
                    case FeatureState.NotAvailable: text = "N/A"; c = Theme.Grey; glow = false; break;
                    default: text = "OFF"; c = Color.FromArgb(130, 136, 160); glow = false; break;
                }
            }
            var r = new RectangleF(3, 3, Width - 7, Height - 7);
            using (var p = Theme.Round(r, r.Height / 2))
            {
                if (glow) Theme.Glow(g, p, c, 2, 1f, 70);
                using (var b = new SolidBrush(Color.FromArgb(30, c))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(220, c), 1.3f)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, text, Font, Rectangle.Round(r), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Button: rounded ya kate kone, gradient, chamak, icon, outline.</summary>
    internal class RoundButton : Control
    {
        public Color Fill = Theme.Row;
        public Color Fill2 = Color.Empty;
        public int Radius = 10;
        public string SubText;
        public bool ChamferShape;
        public Color GlowColor = Color.Empty;
        public string IconName;
        public bool Outline;          // sirf chamakta border, andar gehra
        public Color OutlineColor = Theme.Accent;
        public bool Pulse;            // glow halka saans jaisa
        public bool Shine;            // har kuch second me roshni ki patti guzarti hai
        public bool Spin;             // button ke chaaron taraf patli roshni ghoomti hai
        private bool hover, down, wasActive;
        private readonly float phase = (float)Anim.Rnd.NextDouble() * 2f;

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Anim.Frame += OnFrame; }
        protected override void OnHandleDestroyed(EventArgs e) { Anim.Frame -= OnFrame; base.OnHandleDestroyed(e); }

        private void OnFrame(object s, EventArgs e)
        {
            if (IsDisposed || !Visible) return;
            bool anim = (Pulse && GlowColor != Color.Empty) || Shine || Spin;
            if (anim && (Anim.Active || wasActive)) Invalidate();
            wasActive = Anim.Active;
        }

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            Font = Theme.Semi(10F);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            bool glow = GlowColor != Color.Empty && Enabled;
            float pad = glow ? 8 : (Outline ? 4 : 0.5f);
            var r = new RectangleF(pad, pad, Width - 2 * pad - 1, Height - 2 * pad - 1);
            Color a = Fill, b = Fill2 == Color.Empty ? Fill : Fill2;
            if (!Enabled) { a = b = Color.FromArgb(36, 38, 56); }
            else if (down) { a = ControlPaint.Dark(a, 0.05f); b = ControlPaint.Dark(b, 0.05f); }
            else if (hover) { a = ControlPaint.Light(a, 0.15f); b = ControlPaint.Light(b, 0.15f); }

            using (var p = ChamferShape ? Theme.Chamfer(r, Math.Min(r.Height * .32f, 22)) : Theme.Round(r, Radius))
            {
                int ga = hover ? 120 : 85;
                if (Pulse && Anim.Active) ga = (int)((hover ? 100 : 70) + 45 * Anim.Pulse(0.55f));
                if (glow) Theme.Glow(g, p, GlowColor, 4, 2f, Math.Min(255, ga));
                if (Outline)
                {
                    using (var br = new SolidBrush(hover ? Theme.RowHover : Theme.Row)) g.FillPath(br, p);
                    if (Enabled) Theme.Glow(g, p, OutlineColor, 2, 1f, 70);
                    using (var pen = new Pen(Enabled ? OutlineColor : Theme.Grey, 1.5f)) g.DrawPath(pen, p);
                }
                else
                {
                    using (var br = new LinearGradientBrush(r, a, b, LinearGradientMode.Horizontal)) g.FillPath(br, p);
                    if (glow)
                    {
                        // upar halki chamak
                        var hl = new RectangleF(r.X, r.Y, r.Width, r.Height * .45f);
                        using (var hb = new LinearGradientBrush(hl, Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                        {
                            var old = g.Clip; g.SetClip(p); g.FillRectangle(hb, hl); g.Clip = old;
                        }
                        using (var pen = new Pen(Color.FromArgb(200, ControlPaint.Light(GlowColor, .4f)), 1.4f)) g.DrawPath(pen, p);
                    }
                }

                // premium shine: tirchhi roshni ki patti dheere guzarti hai (har 3.6s me ek baar, 0.9s)
                if (Shine && Anim.Active && Enabled)
                {
                    float cyc = (Anim.T + phase) % 3.6f;
                    if (cyc < 0.9f)
                    {
                        float k = cyc / 0.9f; k = k * k * (3 - 2 * k);          // smooth ease
                        float bw = r.Height * 0.9f;
                        float cx = r.X - bw * 1.5f + (r.Width + bw * 3f) * k;
                        var old = g.Clip; g.SetClip(p, CombineMode.Intersect);
                        var st = g.Save();
                        g.TranslateTransform(cx, r.Y + r.Height / 2); g.RotateTransform(20);
                        var band = new RectangleF(-bw / 2, -r.Height, bw, r.Height * 2);
                        using (var lb = new LinearGradientBrush(new RectangleF(band.X - 1, band.Y, band.Width + 2, band.Height), Color.Transparent, Color.Transparent, 0f))
                        {
                            var bl = new ColorBlend(3)
                            {
                                Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(75, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) },
                                Positions = new[] { 0f, .5f, 1f }
                            };
                            lb.InterpolationColors = bl;
                            g.FillRectangle(lb, band);
                        }
                        g.Restore(st); g.Clip = old;
                    }
                }
                // ON hone pe patli roshni button ke kinaare pe dheere ghoomti hai
                if (Spin && Anim.Active && Enabled)
                {
                    var pts = Fx.Flat(p); float per = Fx.Perimeter(pts);
                    Fx.Sweep(g, pts, per, Anim.T * per / 3.2f, per * .22f, Color.FromArgb(255, 255, 230, 230), 2f);
                }
            }

            var fg = Enabled ? ForeColor : Color.FromArgb(110, 115, 128);
            var rr = Rectangle.Round(r);
            if (string.IsNullOrEmpty(SubText))
            {
                var sz = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
                float icon = string.IsNullOrEmpty(IconName) ? 0 : Math.Min(rr.Height * .55f, sz.Height * 1.1f);
                float total = sz.Width + (icon > 0 ? icon + 8 : 0);
                float x0 = rr.X + (rr.Width - total) / 2;
                if (icon > 0) Icons.Draw(g, IconName, new RectangleF(x0, rr.Y + (rr.Height - icon) / 2, icon, icon), Outline ? OutlineColor : fg, 1.8f);
                TextRenderer.DrawText(g, Text, Font, new Point((int)(x0 + (icon > 0 ? icon + 8 : 0)), rr.Y + (rr.Height - sz.Height) / 2), fg, TextFormatFlags.NoPadding);
            }
            else
            {
                var sz = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
                float icon = string.IsNullOrEmpty(IconName) ? 0 : sz.Height * .75f;
                float total = sz.Width + (icon > 0 ? icon + 10 : 0);
                float x0 = rr.X + (rr.Width - total) / 2;
                int ty = rr.Y + rr.Height / 2 - sz.Height + 4;
                if (icon > 0) Icons.Draw(g, IconName, new RectangleF(x0, ty + (sz.Height - icon) / 2, icon, icon), fg, 2f);
                TextRenderer.DrawText(g, Text, Font, new Point((int)(x0 + (icon > 0 ? icon + 10 : 0)), ty), fg, TextFormatFlags.NoPadding);
                using (var sf = Theme.F(9.5F))
                    TextRenderer.DrawText(g, SubText, sf, new Rectangle(rr.X, rr.Y + rr.Height / 2 + 6, rr.Width, 22), Color.FromArgb(235, fg), TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }

        }
    }

    /// <summary>RAM jaisa meter (0-100%) - chamakta.</summary>
    internal class Meter : Control
    {
        private float value;
        public Meter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Height = 12;
        }

        private float target;
        private Timer tw;
        public float Value
        {
            get { return target; }
            set
            {
                target = Math.Max(0, Math.Min(100, value));
                if (!Anim.Active || !IsHandleCreated) { this.value = target; Invalidate(); return; }
                if (tw == null)
                {
                    tw = new Timer { Interval = 16 };
                    tw.Tick += (s, e) =>
                    {
                        this.value += (target - this.value) * 0.12f;
                        if (Math.Abs(target - this.value) < 0.2f) { this.value = target; tw.Stop(); }
                        Invalidate();
                    };
                }
                tw.Start();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            var r = new RectangleF(2, 2, Width - 5, Height - 5);
            using (var p = Theme.Round(r, r.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(40, 44, 70))) g.FillPath(b, p);
            float w = r.Width * value / 100f;
            if (w < r.Height) return;
            Color c = value >= 85 ? Theme.Red : value >= 70 ? Theme.Amber : Theme.Green;
            using (var p = Theme.Round(new RectangleF(r.X, r.Y, w, r.Height), r.Height / 2))
            {
                Theme.Glow(g, p, c, 2, 1f, 80);
                using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y, w + 1, r.Height), ControlPaint.Dark(c, .1f), ControlPaint.Light(c, .2f), 0f)) g.FillPath(b, p);
            }
        }
    }

    /// <summary>App ka logo: chamakta hexagon + bijli.</summary>
    internal class Logo : Control
    {
        public Logo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(64, 64);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            float cx = Width / 2f, cy = Height / 2f, R = Width / 2f - 8;
            var pts = new PointF[6];
            for (int i = 0; i < 6; i++) { double a = Math.PI / 3 * i - Math.PI / 2; pts[i] = new PointF(cx + (float)(R * Math.Cos(a)), cy + (float)(R * Math.Sin(a))); }
            using (var p = new GraphicsPath())
            {
                p.AddPolygon(pts);
                Theme.Glow(g, p, Theme.Accent, 4, 2f, 110);
                using (var b = new LinearGradientBrush(new RectangleF(cx - R, cy - R, 2 * R, 2 * R), Color.FromArgb(60, 24, 8), Color.FromArgb(24, 10, 4), 90f)) g.FillPath(b, p);
                using (var pen = new Pen(Theme.Accent, 2.4f)) g.DrawPath(pen, p);
            }
            Icons.Draw(g, "bolt", new RectangleF(cx - R * .55f, cy - R * .62f, R * 1.1f, R * 1.24f), Theme.Accent);
        }
    }

    /// <summary>Header ka bada naam: NABA (safed) GAME BOOSTER (orange gradient) + //.</summary>
    internal class HeaderTitle : Control
    {
        public HeaderTitle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(460, 46);
            Font = Theme.Heavy(23F);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            var f = TextFormatFlags.NoPadding;
            var s1 = TextRenderer.MeasureText(g, "NABA ", Font, Size.Empty, f);
            var s2 = TextRenderer.MeasureText(g, "GAME BOOSTER", Font, Size.Empty, f);
            int y = (Height - s1.Height) / 2;
            TextRenderer.DrawText(g, "NABA ", Font, new Point(0, y), Color.White, f);
            // orange gradient text
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                float em = g.DpiY * Font.Size / 72f;
                path.AddString("GAME BOOSTER", Font.FontFamily, (int)Font.Style, em, new PointF(s1.Width - 2, y + 1), sf);
                var bounds = path.GetBounds();
                using (var br = new LinearGradientBrush(new RectangleF(bounds.X, bounds.Y - 1, bounds.Width, bounds.Height + 2), Color.FromArgb(255, 205, 60), Theme.AccentDark, 90f))
                    g.FillPath(br, path);
                float sx = bounds.Right + 14;
                using (var b1 = new SolidBrush(Theme.Accent)) using (var b2 = new SolidBrush(Color.FromArgb(140, Theme.AccentDark)))
                {
                    g.FillPolygon(b1, new[] { new PointF(sx + 9, 14), new PointF(sx + 16, 14), new PointF(sx + 7, 34), new PointF(sx, 34) });
                    g.FillPolygon(b2, new[] { new PointF(sx + 21, 14), new PointF(sx + 28, 14), new PointF(sx + 19, 34), new PointF(sx + 12, 34) });
                }
            }
        }
    }

    /// <summary>Phone booster jaisi gol ring: 0 se 100% smooth bharti hai, beech me ginti.</summary>
    internal class BoostRing : Control
    {
        private float shown, target; private bool done;
        private readonly Timer tm = new Timer { Interval = 16 };
        private readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        public BoostRing()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent; Size = new Size(124, 124);
            tm.Tick += (s, e) =>
            {
                shown += (target - shown) * 0.08f + (target > shown ? 0.15f : 0);
                if (shown > target) shown = target;
                Invalidate();
                if (done && Math.Abs(target - shown) < 0.05f) { shown = target; tm.Stop(); Invalidate(); }
            };
            tm.Start();
        }
        public float Target { get { return target; } set { target = Math.Max(0, Math.Min(100, value)); } }
        public void Done() { target = 100; done = true; }
        protected override void OnHandleDestroyed(EventArgs e) { tm.Stop(); base.OnHandleDestroyed(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Smooth(g);
            float th = 9f; var r = new RectangleF(th + 4, th + 4, Width - 2 * th - 9, Height - 2 * th - 9);
            bool fin = done && shown >= 99.9f;
            using (var pen = new Pen(Color.FromArgb(34, 38, 62), th)) g.DrawEllipse(pen, r);
            float sweep = 360f * shown / 100f;
            if (sweep > 0.5f)
            {
                Color a = fin ? Theme.Green : Theme.Accent, b = fin ? Color.FromArgb(40, 200, 160) : Theme.Purple;
                for (int i = 3; i >= 1; i--)
                    using (var gp = new Pen(Color.FromArgb(fin ? 30 : 22, a), th + i * 5) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(gp, r, -90, sweep);
                using (var br = new LinearGradientBrush(RectangleF.Inflate(r, th, th), a, b, 45f))
                using (var pen = new Pen(br, th) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, r, -90, sweep);
            }
            // chalte waqt ring ke andar patli ghoomti roshni
            if (!fin)
            {
                float ang = (float)(sw.Elapsed.TotalSeconds * 220 % 360);
                var ir = RectangleF.Inflate(r, -th - 3, -th - 3);
                using (var pen = new Pen(Color.FromArgb(70, Theme.Accent), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, ir, ang, 70);
            }
            string big = fin ? "✓" : ((int)Math.Round(shown)).ToString() + "%";
            using (var f = fin ? Theme.Semi(26F) : Theme.Heavy(19F))
            {
                var sz = TextRenderer.MeasureText(g, big, f, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, big, f, new Point((Width - sz.Width) / 2, (Height - sz.Height) / 2 - 6), fin ? Theme.Green : Theme.Text, TextFormatFlags.NoPadding);
            }
            using (var f = Theme.Semi(7.5F))
            {
                string cap = fin ? "BOOSTED" : "BOOSTING";
                var sz = TextRenderer.MeasureText(g, cap, f, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, cap, f, new Point((Width - sz.Width) / 2, Height / 2 + 14), Theme.SubText, TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Window ka background: gehra gradient + hexagon pattern + neon lakeerein (original art).</summary>
    internal static class Backdrop
    {
        public static Bitmap Render(int w, int h)
        {
            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h));
            using (var g = Graphics.FromImage(bmp))
            {
                Theme.Smooth(g);
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, w, h), Theme.Bg2, Theme.Bg, 60f)) g.FillRectangle(b, 0, 0, w, h);

                // hexagon pattern
                using (var pen = new Pen(Color.FromArgb(14, 150, 120, 255), 1f))
                {
                    float R = 22, hw = (float)(Math.Sqrt(3) * R);
                    for (float yy = -R; yy < h + R; yy += R * 1.5f)
                    {
                        int row = (int)Math.Round((yy + R) / (R * 1.5f));
                        float off = (row % 2) * hw / 2;
                        for (float xx = -hw + off; xx < w + hw; xx += hw)
                        {
                            var pts = new PointF[6];
                            for (int i = 0; i < 6; i++) { double a = Math.PI / 3 * i - Math.PI / 2; pts[i] = new PointF(xx + (float)(R * Math.Cos(a)), yy + (float)(R * Math.Sin(a))); }
                            g.DrawPolygon(pen, pts);
                        }
                    }
                }

                // header ke peeche neon chamak + bijli ki lakeerein
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(w * 0.30f, -h * 0.20f, w * 0.55f, h * 0.30f);
                    using (var pb = new PathGradientBrush(p) { CenterColor = Color.FromArgb(70, 120, 60, 255), SurroundColors = new[] { Color.FromArgb(0, 120, 60, 255) } })
                        g.FillPath(pb, p);
                }
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(w * 0.55f, -h * 0.15f, w * 0.5f, h * 0.26f);
                    using (var pb = new PathGradientBrush(p) { CenterColor = Color.FromArgb(50, 255, 120, 20), SurroundColors = new[] { Color.FromArgb(0, 255, 120, 20) } })
                        g.FillPath(pb, p);
                }
                var rnd = new Random(7);
                for (int i = 0; i < 7; i++)
                {
                    float x0 = w * (0.45f + (float)rnd.NextDouble() * 0.5f), y0 = 4 + (float)rnd.NextDouble() * 40;
                    var col = i % 2 == 0 ? Theme.Purple : Theme.Accent;
                    using (var pen = new Pen(Color.FromArgb(45 + rnd.Next(40), col), 1.4f + (float)rnd.NextDouble() * 1.6f))
                        g.DrawLine(pen, x0, y0, x0 + 90 + rnd.Next(120), y0 - 30 - rnd.Next(30));
                }
                // neeche ke kone me halki purple chamak
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(-w * 0.2f, h * 0.75f, w * 0.6f, h * 0.4f);
                    using (var pb = new PathGradientBrush(p) { CenterColor = Color.FromArgb(40, 120, 60, 255), SurroundColors = new[] { Color.FromArgb(0, 120, 60, 255) } })
                        g.FillPath(pb, p);
                }
            }
            return bmp;
        }
    }
}
