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
        public static readonly Color Bg = Color.FromArgb(13, 15, 20);
        public static readonly Color Card = Color.FromArgb(22, 25, 33);
        public static readonly Color CardBorder = Color.FromArgb(36, 40, 52);
        public static readonly Color Row = Color.FromArgb(28, 32, 42);
        public static readonly Color RowHover = Color.FromArgb(34, 39, 51);
        public static readonly Color Text = Color.FromArgb(236, 238, 244);
        public static readonly Color SubText = Color.FromArgb(140, 146, 162);
        public static readonly Color Accent = Color.FromArgb(255, 153, 41);
        public static readonly Color AccentDark = Color.FromArgb(214, 112, 16);
        public static readonly Color Green = Color.FromArgb(34, 197, 94);
        public static readonly Color GreenDark = Color.FromArgb(21, 128, 61);
        public static readonly Color Red = Color.FromArgb(239, 68, 68);
        public static readonly Color RedDark = Color.FromArgb(153, 27, 27);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Blue = Color.FromArgb(59, 130, 246);
        public static readonly Color Grey = Color.FromArgb(75, 82, 99);

        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style);
        }

        public static Font Semi(float size)
        {
            try { return new Font("Segoe UI Semibold", size); } catch { return new Font("Segoe UI", size, FontStyle.Bold); }
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

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }

    /// <summary>Gol kono wala card.</summary>
    internal class Card : Panel
    {
        public int Radius = 14;
        public Color Fill = Theme.Card;
        public Color Border = Theme.CardBorder;

        public Card()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent != null) e.Graphics.Clear(Parent.BackColor == Color.Transparent ? Theme.Bg : Parent.BackColor);
            Theme.Smooth(e.Graphics);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(r, Radius))
            using (var b = new SolidBrush(Fill))
            using (var pen = new Pen(Border, 1f))
            {
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    /// <summary>iPhone jaisa ON/OFF switch.</summary>
    [DefaultEvent("Toggled")]
    internal class ToggleSwitch : Control
    {
        private bool on;
        private float knob; // 0..1 animation
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
                if (Math.Abs(target - knob) < 0.02f) { knob = target; anim.Stop(); }
                Invalidate();
            };
        }

        [DefaultValue(false)]
        public bool Checked
        {
            get { return on; }
            set { if (on == value) return; on = value; anim.Start(); }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Enabled && Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Smooth(e.Graphics);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            Color off = Theme.Grey;
            Color track = Blend(off, OnColor, knob);
            if (!Enabled) track = Color.FromArgb(45, 49, 60);
            using (var p = Theme.Round(r, r.Height / 2))
            using (var b = new SolidBrush(track)) e.Graphics.FillPath(b, p);
            float d = r.Height - 6;
            float x = r.X + 3 + (r.Width - d - 6) * knob;
            using (var b = new SolidBrush(Enabled ? Color.White : Color.FromArgb(110, 115, 128)))
                e.Graphics.FillEllipse(b, x, r.Y + 3, d, d);
        }

        private static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>Chhota rangeen status tag: ON / OFF / ADHURA / RESTART / N/A.</summary>
    internal class Pill : Control
    {
        private FeatureState state = FeatureState.Off;
        private bool loading = true;

        public Pill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(74, 22);
            Font = Theme.Semi(8F);
        }

        public void Set(FeatureState s) { state = s; loading = false; Invalidate(); }
        public void SetLoading() { loading = true; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Smooth(e.Graphics);
            string text; Color c;
            if (loading) { text = "CHECK..."; c = Theme.Grey; }
            else
            {
                switch (state)
                {
                    case FeatureState.On: text = "ON"; c = Theme.Green; break;
                    case FeatureState.Partial: text = "ADHURA"; c = Theme.Amber; break;
                    case FeatureState.Pending: text = "RESTART"; c = Theme.Blue; break;
                    case FeatureState.NotAvailable: text = "N/A"; c = Theme.Grey; break;
                    default: text = "OFF"; c = Color.FromArgb(120, 126, 142); break;
                }
            }
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var p = Theme.Round(r, r.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(40, c)))
            using (var pen = new Pen(Color.FromArgb(150, c)))
            {
                e.Graphics.FillPath(b, p);
                e.Graphics.DrawPath(pen, p);
            }
            TextRenderer.DrawText(e.Graphics, text, Font, Rectangle.Round(r), c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Gol kono wala button (hover effect ke saath).</summary>
    internal class RoundButton : Control
    {
        public Color Fill = Theme.Row;
        public Color Fill2 = Color.Empty; // gradient ka doosra rang (optional)
        public int Radius = 10;
        public string SubText;
        private bool hover, down;

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
            Theme.Smooth(e.Graphics);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color a = Fill, b = Fill2 == Color.Empty ? Fill : Fill2;
            if (!Enabled) { a = b = Color.FromArgb(40, 44, 55); }
            else if (down) { a = ControlPaint.Dark(a, 0.05f); b = ControlPaint.Dark(b, 0.05f); }
            else if (hover) { a = ControlPaint.Light(a, 0.12f); b = ControlPaint.Light(b, 0.12f); }
            using (var p = Theme.Round(r, Radius))
            using (var br = new LinearGradientBrush(r, a, b, LinearGradientMode.Horizontal))
                e.Graphics.FillPath(br, p);

            var fg = Enabled ? ForeColor : Color.FromArgb(110, 115, 128);
            if (string.IsNullOrEmpty(SubText))
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            else
            {
                var top = new Rectangle(0, Height / 2 - 26, Width, 30);
                var bot = new Rectangle(0, Height / 2 + 4, Width, 22);
                TextRenderer.DrawText(e.Graphics, Text, Font, top, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                using (var sf = Theme.F(9F))
                    TextRenderer.DrawText(e.Graphics, SubText, sf, bot, Color.FromArgb(220, fg), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>RAM jaisa meter (0-100%).</summary>
    internal class Meter : Control
    {
        private float value;
        public Meter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Height = 8;
        }

        public float Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Smooth(e.Graphics);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var p = Theme.Round(r, r.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(40, 45, 58))) e.Graphics.FillPath(b, p);
            float w = r.Width * value / 100f;
            if (w < r.Height) return;
            Color c = value >= 85 ? Theme.Red : value >= 70 ? Theme.Amber : Theme.Green;
            using (var p = Theme.Round(new RectangleF(0, 0, w, r.Height), r.Height / 2))
            using (var b = new SolidBrush(c)) e.Graphics.FillPath(b, p);
        }
    }

    /// <summary>App ka logo (orange bijli) - code se bana, koi image file nahi chahiye.</summary>
    internal class Logo : Control
    {
        public Logo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(44, 44);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Smooth(e.Graphics);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var p = Theme.Round(r, 11))
            using (var b = new LinearGradientBrush(r, Theme.Accent, Theme.AccentDark, 45f)) e.Graphics.FillPath(b, p);
            float s = Width / 44f;
            var bolt = new[]
            {
                new PointF(25 * s, 7 * s), new PointF(12 * s, 25 * s), new PointF(21 * s, 25 * s),
                new PointF(18 * s, 38 * s), new PointF(32 * s, 18 * s), new PointF(23 * s, 18 * s), new PointF(28 * s, 7 * s)
            };
            using (var b = new SolidBrush(Color.FromArgb(20, 20, 26))) e.Graphics.FillPolygon(b, bolt);
        }
    }
}
