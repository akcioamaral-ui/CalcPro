using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CalcPro
{
    public class Theme
    {
        public string Name;
        public Color Back, Display, Panel, Text, SubText, KeyDigit, KeyFunc, KeyOp, KeyEq, KeyEqText, KeyText, Accent, Error, Selection;

        public static Theme Dark()
        {
            return new Theme
            {
                Name = "dark",
                Back = Color.FromArgb(23, 24, 28),
                Display = Color.FromArgb(14, 15, 18),
                Panel = Color.FromArgb(29, 30, 35),
                Text = Color.FromArgb(240, 241, 243),
                SubText = Color.FromArgb(138, 143, 152),
                KeyDigit = Color.FromArgb(48, 50, 58),
                KeyFunc = Color.FromArgb(35, 37, 43),
                KeyOp = Color.FromArgb(60, 63, 74),
                KeyEq = Color.FromArgb(255, 149, 0),
                KeyEqText = Color.White,
                KeyText = Color.FromArgb(237, 237, 237),
                Accent = Color.FromArgb(255, 179, 64),
                Error = Color.FromArgb(255, 99, 99),
                Selection = Color.FromArgb(44, 46, 54)
            };
        }

        public static Theme Light()
        {
            return new Theme
            {
                Name = "light",
                Back = Color.FromArgb(236, 238, 242),
                Display = Color.White,
                Panel = Color.FromArgb(247, 248, 250),
                Text = Color.FromArgb(24, 26, 30),
                SubText = Color.FromArgb(107, 114, 128),
                KeyDigit = Color.White,
                KeyFunc = Color.FromArgb(222, 226, 232),
                KeyOp = Color.FromArgb(208, 214, 224),
                KeyEq = Color.FromArgb(255, 138, 0),
                KeyEqText = Color.White,
                KeyText = Color.FromArgb(28, 30, 34),
                Accent = Color.FromArgb(205, 105, 10),
                Error = Color.FromArgb(214, 40, 40),
                Selection = Color.FromArgb(226, 231, 238)
            };
        }
    }

    internal static class Gfx
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var gp = new GraphicsPath();
            float d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        public static Font FontPx(float px, bool bold)
        {
            int key = Math.Max(6, (int)Math.Round(px));
            string k = key + (bold ? "b" : "");
            Font f;
            if (!cache.TryGetValue(k, out f))
            {
                f = new Font("Segoe UI", key, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                cache[k] = f;
            }
            return f;
        }
    }

    public enum KeyKind { Digit, Func, Op, Eq, Action, Shift, Chip }

    /// <summary>Tecla desenhada sob medida que não rouba o foco da caixa de expressão.</summary>
    public class KeyButton : Control
    {
        public string MainText = "";
        public string ShiftText;
        public bool ShiftActive;
        public bool Toggled;
        public KeyKind Kind;
        public Color BaseColor = Color.Gray, TextColor = Color.White, AccentColor = Color.Orange;
        public float FontScale = 1f;
        bool hover, pressed;

        public KeyButton()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.StandardDoubleClick, false);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool dark = BaseColor.GetBrightness() < 0.5f;
            Color fill = BaseColor;
            if (Toggled) fill = Kind == KeyKind.Chip ? Gfx.Mix(BaseColor, AccentColor, 0.35f) : AccentColor;
            if (!Enabled) fill = Gfx.Mix(fill, BackColor, 0.55f);
            else if (pressed) fill = Gfx.Mix(fill, dark ? Color.White : Color.Black, 0.18f);
            else if (hover) fill = Gfx.Mix(fill, dark ? Color.White : Color.Black, 0.08f);

            float rad = Math.Min(Height, Width) * 0.22f;
            using (var gp = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), rad))
            using (var br = new SolidBrush(fill))
                g.FillPath(br, gp);

            string main = MainText;
            string hint = ShiftText;
            Color mainColor = TextColor;
            if (ShiftActive && !string.IsNullOrEmpty(ShiftText))
            {
                main = ShiftText; hint = null;
                mainColor = Kind == KeyKind.Eq ? TextColor : AccentColor;
            }
            if (Toggled && Kind != KeyKind.Chip) mainColor = Color.White;
            if (!Enabled) mainColor = Gfx.Mix(mainColor, BackColor, 0.6f);

            float basePx = Math.Min(Height * 0.40f, Width * 0.34f) * FontScale;
            if (Kind == KeyKind.Chip) basePx = Height * 0.42f * FontScale;
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            Rectangle mainRect = ClientRectangle;
            if (!string.IsNullOrEmpty(hint) && Height > 30)
            {
                int hh = (int)(Height * 0.32f);
                var hintRect = new Rectangle(0, (int)(Height * 0.06f), Width, hh);
                Color hc = Enabled ? AccentColor : Gfx.Mix(AccentColor, BackColor, 0.6f);
                TextRenderer.DrawText(g, hint, Gfx.FontPx(Height * 0.22f, false), hintRect, hc, flags);
                mainRect = new Rectangle(0, (int)(Height * 0.28f), Width, Height - (int)(Height * 0.30f));
                basePx *= 0.9f;
            }
            bool bold = Kind == KeyKind.Eq;
            Font mf = Gfx.FontPx(basePx, bold);
            while (basePx > 8 && TextRenderer.MeasureText(g, main, mf, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > Width * 0.86f)
            {
                basePx -= 1;
                mf = Gfx.FontPx(basePx, bold);
            }
            TextRenderer.DrawText(g, main, mf, mainRect, mainColor, flags);
        }
    }

    /// <summary>Texto alinhado à direita que encolhe para caber.</summary>
    public class FitLabel : Control
    {
        public float MaxPx = 40, MinPx = 10;
        public bool AlignLeft;
        public bool Bold;

        public FitLabel()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            string t = Text ?? "";
            if (t.Length == 0) return;
            float px = Math.Min(MaxPx, Height * 0.9f);
            Font f = Gfx.FontPx(px, Bold);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                        (AlignLeft ? TextFormatFlags.Left : TextFormatFlags.Right);
            while (px > MinPx && TextRenderer.MeasureText(e.Graphics, t, f, new Size(int.MaxValue, Height), flags).Width > Width)
            {
                px -= 1;
                f = Gfx.FontPx(px, Bold);
            }
            if (TextRenderer.MeasureText(e.Graphics, t, f, new Size(int.MaxValue, Height), flags).Width > Width)
                flags |= AlignLeft ? TextFormatFlags.EndEllipsis : TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, t, f, ClientRectangle, ForeColor, flags);
        }
    }

    /// <summary>Visualização da pilha RPN.</summary>
    public class StackView : Control
    {
        public List<string> Items = new List<string>();
        public Color SubColor = Color.Gray;

        public StackView()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            int levels = Math.Max(2, Math.Min(6, Height / 30));
            float lh = Height / (float)levels;
            for (int i = 0; i < levels; i++)
            {
                int level = i + 1; // 1 = topo (embaixo)
                float y = Height - lh * (i + 1);
                var r = new Rectangle(0, (int)y, Width, (int)lh);
                bool top = level == 1;
                Font lf = Gfx.FontPx(lh * 0.38f, false);
                TextRenderer.DrawText(g, level + ":", lf, r, SubColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                int idx = Items.Count - level;
                if (idx >= 0)
                {
                    string t = Items[idx];
                    float px = lh * (top ? 0.62f : 0.5f);
                    Font f = Gfx.FontPx(px, false);
                    var tr = new Rectangle(r.X + (int)(lh * 0.8f), r.Y, r.Width - (int)(lh * 0.8f), r.Height);
                    while (px > 9 && TextRenderer.MeasureText(g, t, f).Width > tr.Width) { px -= 1; f = Gfx.FontPx(px, false); }
                    TextRenderer.DrawText(g, t, f, tr, top ? ForeColor : Gfx.Mix(ForeColor, BackColor, 0.25f),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }

    /// <summary>Painel de fundo arredondado.</summary>
    public class RoundPanel : Panel
    {
        public Color Fill = Color.Black;
        public float Radius = 14;

        public RoundPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gp = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Radius))
            using (var br = new SolidBrush(Fill))
                e.Graphics.FillPath(br, gp);
        }
    }

    /// <summary>Cores dos menus de acordo com o tema.</summary>
    public class ThemeColorTable : ProfessionalColorTable
    {
        readonly Theme t;
        public ThemeColorTable(Theme theme) { t = theme; UseSystemColors = false; }
        public override Color ToolStripDropDownBackground { get { return t.Panel; } }
        public override Color ImageMarginGradientBegin { get { return t.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return t.Panel; } }
        public override Color ImageMarginGradientEnd { get { return t.Panel; } }
        public override Color MenuBorder { get { return t.KeyOp; } }
        public override Color MenuItemBorder { get { return t.KeyOp; } }
        public override Color MenuItemSelected { get { return t.Selection; } }
        public override Color MenuItemSelectedGradientBegin { get { return t.Selection; } }
        public override Color MenuItemSelectedGradientEnd { get { return t.Selection; } }
        public override Color SeparatorDark { get { return t.KeyOp; } }
        public override Color SeparatorLight { get { return t.Panel; } }
        public override Color CheckBackground { get { return Gfx.Mix(t.Panel, t.Accent, 0.3f); } }
        public override Color CheckSelectedBackground { get { return Gfx.Mix(t.Panel, t.Accent, 0.45f); } }
        public override Color CheckPressedBackground { get { return Gfx.Mix(t.Panel, t.Accent, 0.45f); } }
    }

    public class ThemeRenderer : ToolStripProfessionalRenderer
    {
        readonly Theme t;
        public ThemeRenderer(Theme theme) : base(new ThemeColorTable(theme)) { t = theme; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? t.Text : t.SubText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = t.SubText;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            using (var pen = new Pen(t.Accent, 2f))
            {
                g.DrawLines(pen, new[] {
                    new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.52f),
                    new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.74f),
                    new PointF(r.Left + r.Width * 0.82f, r.Top + r.Height * 0.28f) });
            }
        }
    }
}
