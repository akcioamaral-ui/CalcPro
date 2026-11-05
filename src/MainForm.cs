using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CalcPro
{
    class KeyDef
    {
        public KeyKind Kind;
        public string Label, Insert, Rpn, Action;
        public int Arity;
        public bool IsOp;
        public string SLabel, SInsert, SRpn, SAction;
        public int SArity;
        public bool SIsOp;
        public string RpnLabel, RpnAction;
        public KeyButton Button;

        public KeyDef(KeyKind k, string label, string insert) { Kind = k; Label = label; Insert = insert; }
        public KeyDef R(string op, int ar) { Rpn = op; Arity = ar; return this; }
        public KeyDef Op() { IsOp = true; return this; }
        public KeyDef Act(string a) { Action = a; return this; }
        public KeyDef Sh(string label, string insert) { SLabel = label; SInsert = insert; return this; }
        public KeyDef SR(string op, int ar) { SRpn = op; SArity = ar; return this; }
        public KeyDef SOp() { SIsOp = true; return this; }
        public KeyDef SAct(string a) { SAction = a; return this; }
        public KeyDef RpnAlt(string label, string action) { RpnLabel = label; RpnAction = action; return this; }
    }

    class HistItem
    {
        public string Expr;
        public double Value;
        public string Text;
    }

    class VarItem
    {
        public string Name, Display, Insert;
        public bool IsFunc;
    }

    class FnInfo
    {
        public string Cat, Sig, Name, Desc;
        public int Arity;
        public FnInfo(string cat, string sig, string name, int arity, string desc) { Cat = cat; Sig = sig; Name = name; Arity = arity; Desc = desc; }
    }

    public class MainForm : Form
    {
        const string AppName = "CalcPro";
        const string Version = "1.0";

        readonly Engine engine = new Engine();
        readonly FormatSettings fmt = new FormatSettings();
        Theme theme = Theme.Dark();
        bool rpn, shift, justEvaluated, sideVisible, showVarsTab, showDms = true, suppressText;
        float scale = 1f;
        int histNav = -1;
        readonly List<HistItem> history = new List<HistItem>();
        readonly List<double> stack = new List<double>();

        Panel topBar, keypad, side;
        KeyButton chipAngle, chipBase, chipMode, chipFormat, chipHist, chipMenu, tabHist, tabVars, sideClear;
        RoundPanel display;
        TextBox exprBox;
        FitLabel resultLabel, infoLabel, stepLabel;
        KeyButton chipSci;
        bool sciVisible = true;
        int funcBlockH;
        StackView stackView;
        ListBox sideList;
        ToolTip tip;
        List<KeyDef> funcDec, funcBase, mainKeys;
        readonly List<KeyButton> allButtons = new List<KeyButton>();

        static readonly FnInfo[] Catalog = {
            new FnInfo("Trigonometria", "sin(x)", "sin", 1, "seno"),
            new FnInfo("Trigonometria", "cos(x)", "cos", 1, "cosseno"),
            new FnInfo("Trigonometria", "tan(x)", "tan", 1, "tangente"),
            new FnInfo("Trigonometria", "cot(x)", "cot", 1, "cotangente"),
            new FnInfo("Trigonometria", "sec(x)", "sec", 1, "secante"),
            new FnInfo("Trigonometria", "csc(x)", "csc", 1, "cossecante"),
            new FnInfo("Trigonometria", "asin(x)", "asin", 1, "arco seno"),
            new FnInfo("Trigonometria", "acos(x)", "acos", 1, "arco cosseno"),
            new FnInfo("Trigonometria", "atan(x)", "atan", 1, "arco tangente"),
            new FnInfo("Trigonometria", "acot(x)", "acot", 1, "arco cotangente"),
            new FnInfo("Trigonometria", "atan2(y; x)", "atan2", 2, "ângulo do ponto (x; y)"),
            new FnInfo("Hiperbólicas", "sinh(x)", "sinh", 1, "seno hiperbólico"),
            new FnInfo("Hiperbólicas", "cosh(x)", "cosh", 1, "cosseno hiperbólico"),
            new FnInfo("Hiperbólicas", "tanh(x)", "tanh", 1, "tangente hiperbólica"),
            new FnInfo("Hiperbólicas", "asinh(x)", "asinh", 1, "inversa de sinh"),
            new FnInfo("Hiperbólicas", "acosh(x)", "acosh", 1, "inversa de cosh"),
            new FnInfo("Hiperbólicas", "atanh(x)", "atanh", 1, "inversa de tanh"),
            new FnInfo("Logaritmos", "ln(x)", "ln", 1, "logaritmo natural"),
            new FnInfo("Logaritmos", "log(x)", "log", 1, "logaritmo base 10"),
            new FnInfo("Logaritmos", "log(x; b)", "log", 2, "logaritmo na base b"),
            new FnInfo("Logaritmos", "log2(x)", "log2", 1, "logaritmo base 2"),
            new FnInfo("Logaritmos", "exp(x)", "exp", 1, "e elevado a x"),
            new FnInfo("Potências e raízes", "sqrt(x)", "sqrt", 1, "raiz quadrada"),
            new FnInfo("Potências e raízes", "cbrt(x)", "cbrt", 1, "raiz cúbica"),
            new FnInfo("Potências e raízes", "root(x; n)", "root", 2, "raiz n-ésima"),
            new FnInfo("Potências e raízes", "pow(x; y)", "pow", 2, "x elevado a y"),
            new FnInfo("Potências e raízes", "hypot(a; b)", "hypot", 2, "hipotenusa √(a²+b²)"),
            new FnInfo("Arredondamento", "abs(x)", "abs", 1, "valor absoluto"),
            new FnInfo("Arredondamento", "floor(x)", "floor", 1, "arredonda para baixo"),
            new FnInfo("Arredondamento", "ceil(x)", "ceil", 1, "arredonda para cima"),
            new FnInfo("Arredondamento", "round(x)", "round", 1, "arredonda"),
            new FnInfo("Arredondamento", "round(x; n)", "round", 2, "arredonda com n casas"),
            new FnInfo("Arredondamento", "trunc(x)", "trunc", 1, "parte inteira"),
            new FnInfo("Arredondamento", "frac(x)", "frac", 1, "parte fracionária"),
            new FnInfo("Arredondamento", "sign(x)", "sign", 1, "sinal (-1, 0, 1)"),
            new FnInfo("Combinatória", "fact(n)", "fact", 1, "fatorial n!"),
            new FnInfo("Combinatória", "gamma(x)", "gamma", 1, "função gama"),
            new FnInfo("Combinatória", "nPr(n; r)", "nPr", 2, "arranjos"),
            new FnInfo("Combinatória", "nCr(n; r)", "nCr", 2, "combinações"),
            new FnInfo("Inteiros", "gcd(a; b; …)", "gcd", 2, "máximo divisor comum (MDC)"),
            new FnInfo("Inteiros", "lcm(a; b; …)", "lcm", 2, "mínimo múltiplo comum (MMC)"),
            new FnInfo("Inteiros", "mod(a; b)", "mod", 2, "resto da divisão"),
            new FnInfo("Inteiros", "randint(a; b)", "randint", 2, "inteiro aleatório"),
            new FnInfo("Estatística", "min(a; b; …)", "min", 2, "mínimo"),
            new FnInfo("Estatística", "max(a; b; …)", "max", 2, "máximo"),
            new FnInfo("Estatística", "sum(a; b; …)", "sum", 2, "soma"),
            new FnInfo("Estatística", "avg(a; b; …)", "avg", 2, "média"),
            new FnInfo("Conversões", "todeg(x)", "todeg", 1, "radianos → graus"),
            new FnInfo("Conversões", "torad(x)", "torad", 1, "graus → radianos"),
            new FnInfo("Outras", "rand()", "rand", 0, "aleatório entre 0 e 1"),
        };

        static readonly string[][] PhysConsts = {
            new[] { "Velocidade da luz", "c", "299792458", "m/s" },
            new[] { "Gravidade padrão", "g", "9.80665", "m/s²" },
            new[] { "Constante gravitacional", "G", "6.6743E-11", "m³/(kg·s²)" },
            new[] { "Constante de Planck", "h", "6.62607015E-34", "J·s" },
            new[] { "Planck reduzida", "ħ", "1.054571817E-34", "J·s" },
            new[] { "Carga elementar", "e", "1.602176634E-19", "C" },
            new[] { "Massa do elétron", "mₑ", "9.1093837015E-31", "kg" },
            new[] { "Massa do próton", "mₚ", "1.67262192369E-27", "kg" },
            new[] { "Massa do nêutron", "mₙ", "1.67492749804E-27", "kg" },
            new[] { "Unidade de massa atômica", "u", "1.6605390666E-27", "kg" },
            new[] { "Número de Avogadro", "Nₐ", "6.02214076E23", "1/mol" },
            new[] { "Constante de Boltzmann", "k", "1.380649E-23", "J/K" },
            new[] { "Constante dos gases", "R", "8.314462618", "J/(mol·K)" },
            new[] { "Constante de Faraday", "F", "96485.33212", "C/mol" },
            new[] { "Permissividade do vácuo", "ε₀", "8.8541878128E-12", "F/m" },
            new[] { "Permeabilidade do vácuo", "μ₀", "1.25663706212E-6", "N/A²" },
            new[] { "Stefan-Boltzmann", "σ", "5.670374419E-8", "W/(m²·K⁴)" },
            new[] { "Raio de Bohr", "a₀", "5.29177210903E-11", "m" },
            new[] { "Atmosfera padrão", "atm", "101325", "Pa" },
            new[] { "Zero absoluto", "0 K", "-273.15", "°C" },
        };

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        int S(float v) { return (int)Math.Round(v * scale); }

        public MainForm()
        {
            Text = AppName + " — Calculadora Científica";
            using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(410), S(660));
            MinimumSize = new Size(S(350), S(560));
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            fmt.DecimalComma = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == ",";
            BuildUi();
            LoadState();
            engine.DecimalComma = fmt.DecimalComma;
            ApplyTheme();
            UpdateModeUi();
            RefreshSide();
        }

        // ================================================================ UI

        KeyButton MakeButton(Control parent, string text, KeyKind kind)
        {
            var b = new KeyButton { MainText = text, Kind = kind };
            parent.Controls.Add(b);
            allButtons.Add(b);
            return b;
        }

        KeyButton Chip(string text, string tipText, Action onClick)
        {
            var b = MakeButton(topBar, text, KeyKind.Chip);
            b.FontScale = 0.95f;
            b.Click += (s, e) => { onClick(); exprBox.Focus(); };
            tip.SetToolTip(b, tipText);
            return b;
        }

        void BuildUi()
        {
            tip = new ToolTip { InitialDelay = 600 };

            topBar = new Panel();
            Controls.Add(topBar);
            chipAngle = Chip("DEG", "Unidade angular: graus / radianos / grados  (F5)", CycleAngle);
            chipBase = Chip("DEC", "Base numérica: decimal / hexadecimal / octal / binário  (F6)", CycleBase);
            chipMode = Chip("ALG", "Modo de entrada: algébrico ou RPN  (F7)", ToggleRpn);
            chipFormat = Chip("NORM", "Formato de exibição: normal / científico / engenharia / fixo  (F8)", CycleFormat);
            chipSci = Chip("f(x)", "Mostrar / ocultar o teclado científico  (F4)", ToggleSci);
            chipHist = Chip("☰", "Histórico e variáveis  (F9)", ToggleSide);
            chipMenu = Chip("⚙", "Menu de configurações", ShowMainMenu);

            display = new RoundPanel { Radius = S(14) };
            Controls.Add(display);
            exprBox = new TextBox
            {
                Multiline = true,
                WordWrap = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.None,
                AcceptsReturn = false,
                Font = new Font("Segoe UI", 20f),
            };
            exprBox.TextChanged += (s, e) => { if (!suppressText) { justEvaluated = false; UpdatePreview(); } };
            exprBox.KeyDown += ExprKeyDown;
            exprBox.KeyPress += ExprKeyPress;
            resultLabel = new FitLabel { MaxPx = S(46), MinPx = S(12) };
            infoLabel = new FitLabel { MaxPx = S(15), MinPx = S(9) };
            stepLabel = new FitLabel { MaxPx = S(18), MinPx = S(9), AlignLeft = true };
            stackView = new StackView();
            display.Controls.Add(exprBox);
            display.Controls.Add(stepLabel);
            display.Controls.Add(resultLabel);
            display.Controls.Add(infoLabel);
            display.Controls.Add(stackView);
            resultLabel.Click += (s, e) => exprBox.Focus();

            keypad = new Panel();
            Controls.Add(keypad);
            BuildKeys();

            side = new Panel { Visible = false };
            Controls.Add(side);
            tabHist = MakeButton(side, "Histórico", KeyKind.Chip);
            tabVars = MakeButton(side, "Variáveis", KeyKind.Chip);
            sideClear = MakeButton(side, "Limpar", KeyKind.Chip);
            tabHist.FontScale = tabVars.FontScale = sideClear.FontScale = 0.8f;
            tabHist.Click += (s, e) => { showVarsTab = false; RefreshSide(); exprBox.Focus(); };
            tabVars.Click += (s, e) => { showVarsTab = true; RefreshSide(); exprBox.Focus(); };
            sideClear.Click += (s, e) =>
            {
                string what = showVarsTab ? "todas as variáveis e funções" : "todo o histórico";
                if (MessageBox.Show(this, "Apagar " + what + "?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (showVarsTab) ClearVars(); else history.Clear();
                    RefreshSide();
                }
                exprBox.Focus();
            };
            sideList = new ListBox
            {
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawVariable,
                IntegralHeight = false,
            };
            sideList.MeasureItem += SideMeasure;
            sideList.DrawItem += SideDraw;
            sideList.DoubleClick += (s, e) => UseSideItem(false);
            sideList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { UseSideItem(false); e.Handled = true; } };
            sideList.MouseDown += (s, e) =>
            {
                int i = sideList.IndexFromPoint(e.Location);
                if (i >= 0) sideList.SelectedIndex = i;
                if (e.Button == MouseButtons.Right && i >= 0) ShowListMenu(e.Location);
            };
            sideList.Resize += (s, e) => sideList.Invalidate();
            side.Controls.Add(sideList);

            Activated += (s, e) => FocusEntry();
            Shown += (s, e) => FocusEntry();
            ActiveControl = exprBox;
        }

        void FocusEntry()
        {
            ActiveControl = exprBox;
            exprBox.Focus();
            exprBox.SelectionStart = exprBox.TextLength;
        }

        void BuildKeys()
        {
            KeyKind F = KeyKind.Func, D = KeyKind.Digit, O = KeyKind.Op;
            funcDec = new List<KeyDef> {
                new KeyDef(KeyKind.Shift, "2nd", null).Act("2nd"),
                new KeyDef(F, "(", "(").Sh("[", "["),
                new KeyDef(F, ")", ")").Sh("]", "]"),
                new KeyDef(F, "x²", "²").Op().R("sqr", 1).Sh("x³", "³").SOp().SR("cube", 1),
                new KeyDef(F, "xʸ", "^").Op().R("^", 2).Sh("ʸ√x", "root(").SR("root", 2),
                new KeyDef(F, "√", "√").R("sqrt", 1).Sh("∛", "∛").SR("cbrt", 1),

                new KeyDef(F, "sin", "sin(").R("sin", 1).Sh("sin⁻¹", "asin(").SR("asin", 1),
                new KeyDef(F, "cos", "cos(").R("cos", 1).Sh("cos⁻¹", "acos(").SR("acos", 1),
                new KeyDef(F, "tan", "tan(").R("tan", 1).Sh("tan⁻¹", "atan(").SR("atan", 1),
                new KeyDef(F, "ln", "ln(").R("ln", 1).Sh("eˣ", "exp(").SR("exp", 1),
                new KeyDef(F, "log", "log(").R("log", 1).Sh("10ˣ", "10^(").SR("pow10", 1),
                new KeyDef(F, "n!", "!").Op().R("fact", 1).Sh("nCr", "nCr(").SR("nCr", 2),

                new KeyDef(F, "sinh", "sinh(").R("sinh", 1).Sh("sinh⁻¹", "asinh(").SR("asinh", 1),
                new KeyDef(F, "cosh", "cosh(").R("cosh", 1).Sh("cosh⁻¹", "acosh(").SR("acosh", 1),
                new KeyDef(F, "tanh", "tanh(").R("tanh", 1).Sh("tanh⁻¹", "atanh(").SR("atanh", 1),
                new KeyDef(F, "π", "π").Sh("φ", "φ"),
                new KeyDef(F, "e", "e").Sh("rand", "rand"),
                new KeyDef(F, "%", "%").Op().R("pct", 1).Sh("mod", " mod ").SOp().SR("mod", 2),

                new KeyDef(F, "|x|", "abs(").R("abs", 1).Sh("⌊x⌋", "floor(").SR("floor", 1),
                new KeyDef(F, "1/x", "⁻¹").Op().R("inv", 1).Sh("⌈x⌉", "ceil(").SR("ceil", 1),
                new KeyDef(F, "EXP", "E").Op().Sh("rnd", "round(").SR("round", 1),
                new KeyDef(F, "°", "°").Op().Sh("′", "'").SOp(),
                new KeyDef(F, "f(x)", null).Act("fx"),
                new KeyDef(F, "const", null).Act("const"),
            };

            funcBase = new List<KeyDef> {
                new KeyDef(KeyKind.Shift, "2nd", null).Act("2nd"),
                new KeyDef(F, "(", "(").Sh("[", "["),
                new KeyDef(F, ")", ")").Sh("]", "]"),
                new KeyDef(D, "A", "A"),
                new KeyDef(D, "B", "B"),
                new KeyDef(D, "C", "C"),

                new KeyDef(F, "AND", " and ").Op().R("and", 2),
                new KeyDef(F, "OR", " or ").Op().R("or", 2),
                new KeyDef(F, "XOR", " xor ").Op().R("xor", 2),
                new KeyDef(D, "D", "D"),
                new KeyDef(D, "E", "E"),
                new KeyDef(D, "F", "F"),

                new KeyDef(F, "NOT", "not ").R("not", 1),
                new KeyDef(F, "<<", " << ").Op().R("<<", 2),
                new KeyDef(F, ">>", " >> ").Op().R(">>", 2),
                new KeyDef(F, "mod", " mod ").Op().R("mod", 2),
                new KeyDef(F, "xʸ", "^").Op().R("^", 2),
                new KeyDef(F, "n!", "!").Op().R("fact", 1),

                new KeyDef(F, "|x|", "abs(").R("abs", 1),
                new KeyDef(F, "x²", "²").Op().R("sqr", 1),
                new KeyDef(F, "MDC", "gcd(").R("gcd", 2).Sh("MMC", "lcm(").SR("lcm", 2),
                new KeyDef(F, "nCr", "nCr(").R("nCr", 2).Sh("nPr", "nPr(").SR("nPr", 2),
                new KeyDef(F, "f(x)", null).Act("fx"),
                new KeyDef(F, "const", null).Act("const"),
            };

            mainKeys = new List<KeyDef> {
                new KeyDef(D, "7", "7"), new KeyDef(D, "8", "8"), new KeyDef(D, "9", "9"),
                new KeyDef(KeyKind.Action, "⌫", null).Act("back"),
                new KeyDef(KeyKind.Action, "C", null).Act("clear").Sh("AC", null).SAct("allclear"),

                new KeyDef(D, "4", "4"), new KeyDef(D, "5", "5"), new KeyDef(D, "6", "6"),
                new KeyDef(O, "×", "×").Op().R("*", 2),
                new KeyDef(O, "÷", "÷").Op().R("/", 2),

                new KeyDef(D, "1", "1"), new KeyDef(D, "2", "2"), new KeyDef(D, "3", "3"),
                new KeyDef(O, "+", "+").Op().R("+", 2),
                new KeyDef(O, "−", "−").Op().R("-", 2).Sh("±", null).SAct("neg"),

                new KeyDef(D, "0", "0"),
                new KeyDef(D, ".", null).Act("dec"),
                new KeyDef(O, ",", null).Act("sep").RpnAlt("x⇄y", "swap"),
                new KeyDef(O, "ans", "ans").RpnAlt("DROP", "drop"),
                new KeyDef(KeyKind.Eq, "=", null).Act("eq").RpnAlt("ENTER", "enter"),
            };

            foreach (var list in new[] { funcDec, funcBase, mainKeys })
                foreach (var k in list)
                {
                    var kd = k;
                    kd.Button = MakeButton(keypad, kd.Label, kd.Kind);
                    kd.Button.ShiftText = kd.SLabel;
                    kd.Button.Click += (s, e) => DoKey(kd);
                }
        }

        // ============================================================ Layout

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutAll();
        }

        void LayoutAll()
        {
            if (keypad == null) return;
            int W = ClientSize.Width, H = ClientSize.Height;
            if (W <= 0 || H <= 0) return;
            int sideW = sideVisible ? Math.Min(S(290), W / 2) : 0;
            int mainW = W - sideW;
            int pad = S(10);
            int topH = S(44);

            topBar.SetBounds(0, 0, mainW, topH);
            LayoutTop(mainW, topH, pad);

            side.Visible = sideVisible;
            if (sideVisible)
            {
                side.SetBounds(mainW, 0, sideW, H);
                LayoutSide();
            }

            int avail = H - topH - pad;
            int dispH = Math.Max(S(165), (int)(avail * 0.30));
            display.SetBounds(pad, topH, mainW - 2 * pad, dispH);
            LayoutDisplay();

            int ky = topH + dispH + pad;
            keypad.SetBounds(pad, ky, mainW - 2 * pad, Math.Max(10, H - ky - pad));
            LayoutKeys();
        }

        void LayoutTop(int w, int h, int pad)
        {
            int gap = S(6), y = S(8), ch = h - S(14);
            int cw = Math.Min(S(66), (w - 2 * pad - gap * 7 - 3 * S(40)) / 4);
            int x = pad;
            foreach (var b in new[] { chipAngle, chipBase, chipMode, chipFormat })
            {
                b.SetBounds(x, y, cw, ch);
                x += cw + gap;
            }
            int small = Math.Min(cw, S(44));
            x = w - pad - small;
            chipMenu.SetBounds(x, y, small, ch);
            x -= gap + small;
            chipHist.SetBounds(x, y, small, ch);
            x -= gap + small;
            chipSci.SetBounds(x, y, small, ch);
        }

        void LayoutDisplay()
        {
            int w = display.Width, h = display.Height, p = S(12);
            int infoH = S(22);
            infoLabel.SetBounds(p, h - S(6) - infoH, w - 2 * p, infoH);
            resultLabel.Visible = !rpn;
            stepLabel.Visible = !rpn;
            stackView.Visible = rpn;
            if (!rpn)
            {
                int resH = Math.Max(S(40), (int)((h - infoH - p) * 0.40));
                int stepH = S(24);
                resultLabel.SetBounds(p, infoLabel.Top - resH, w - 2 * p, resH);
                stepLabel.SetBounds(p, resultLabel.Top - stepH, w - 2 * p, stepH);
                exprBox.SetBounds(p, p, w - 2 * p, Math.Max(S(30), stepLabel.Top - S(2) - p));
            }
            else
            {
                int entH = S(40);
                exprBox.SetBounds(p, infoLabel.Top - entH, w - 2 * p, entH);
                stackView.SetBounds(p, S(6), w - 2 * p, Math.Max(S(30), exprBox.Top - S(8)));
            }
        }

        void LayoutKeys()
        {
            int W = keypad.Width, H = keypad.Height;
            float gap = S(6);
            float fw = 0.74f;
            float sep = S(6);
            float unit = (H - gap * 7 - sep) / (4 * fw + 4);
            if (!sciVisible) unit = (H - gap * 3) / 4f;
            float fh = unit * fw, mh = unit;
            if (sciVisible) funcBlockH = (int)Math.Round(4 * (fh + gap) + sep);

            float kw = (W - gap * 5) / 6f;
            foreach (var list in new[] { funcDec, funcBase })
                for (int i = 0; i < list.Count; i++)
                {
                    int r = i / 6, c = i % 6;
                    int x = (int)Math.Round(c * (kw + gap)), y = (int)Math.Round(r * (fh + gap));
                    int x2 = (int)Math.Round(c * (kw + gap) + kw), y2 = (int)Math.Round(r * (fh + gap) + fh);
                    list[i].Button.SetBounds(x, y, x2 - x, y2 - y);
                }

            float y0 = sciVisible ? 4 * (fh + gap) + sep : 0;
            float kw2 = (W - gap * 4) / 5f;
            for (int i = 0; i < mainKeys.Count; i++)
            {
                int r = i / 5, c = i % 5;
                int x = (int)Math.Round(c * (kw2 + gap)), y = (int)Math.Round(y0 + r * (mh + gap));
                int x2 = (int)Math.Round(c * (kw2 + gap) + kw2), y2 = (int)Math.Round(y0 + r * (mh + gap) + mh);
                mainKeys[i].Button.SetBounds(x, y, x2 - x, y2 - y);
            }
        }

        void LayoutSide()
        {
            int w = side.Width, h = side.Height, p = S(8), th = S(30);
            int clearW = S(62);
            int tabW = (w - p * 4 - clearW) / 2;
            tabHist.SetBounds(p, S(8), tabW, th);
            tabVars.SetBounds(p * 2 + tabW, S(8), tabW, th);
            sideClear.SetBounds(w - p - clearW, S(8), clearW, th);
            sideList.SetBounds(p, S(8) + th + p, w - 2 * p, Math.Max(10, h - (S(8) + th + 2 * p)));
        }

        // ============================================================= Theme

        void ApplyTheme()
        {
            BackColor = theme.Back;
            topBar.BackColor = theme.Back;
            keypad.BackColor = theme.Back;
            display.BackColor = theme.Back;
            display.Fill = theme.Display;
            exprBox.BackColor = theme.Display;
            exprBox.ForeColor = theme.Text;
            foreach (Control c in new Control[] { resultLabel, infoLabel, stepLabel, stackView }) c.BackColor = theme.Display;
            resultLabel.ForeColor = theme.Text;
            infoLabel.ForeColor = theme.SubText;
            stepLabel.ForeColor = theme.Accent;
            stackView.ForeColor = theme.Text;
            stackView.SubColor = theme.SubText;
            side.BackColor = theme.Panel;
            sideList.BackColor = theme.Panel;
            sideList.ForeColor = theme.Text;

            foreach (var b in allButtons)
            {
                b.BackColor = b.Parent == side ? theme.Panel : theme.Back;
                b.AccentColor = theme.Accent;
                b.TextColor = theme.KeyText;
                switch (b.Kind)
                {
                    case KeyKind.Digit: b.BaseColor = theme.KeyDigit; break;
                    case KeyKind.Op: b.BaseColor = theme.KeyOp; break;
                    case KeyKind.Eq: b.BaseColor = theme.KeyEq; b.TextColor = theme.KeyEqText; break;
                    case KeyKind.Action: b.BaseColor = theme.KeyOp; b.TextColor = theme.Accent; break;
                    case KeyKind.Chip: b.BaseColor = b.Parent == side ? theme.KeyFunc : theme.KeyFunc; break;
                    default: b.BaseColor = theme.KeyFunc; break;
                }
                b.Invalidate();
            }
            ApplyWindowTheme();
            Invalidate(true);
        }

        void ApplyWindowTheme()
        {
            if (!IsHandleCreated) return;
            try
            {
                int v = theme.Name == "dark" ? 1 : 0;
                DwmSetWindowAttribute(Handle, 20, ref v, 4);
                SetWindowTheme(sideList.Handle, theme.Name == "dark" ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyWindowTheme();
        }

        // ============================================================= Modes

        void UpdateModeUi()
        {
            chipAngle.MainText = engine.Angle == AngleMode.Deg ? "DEG" : engine.Angle == AngleMode.Rad ? "RAD" : "GRA";
            chipBase.MainText = engine.Base.ToString().ToUpperInvariant();
            chipBase.Toggled = engine.Base != NumBase.Dec;
            chipMode.MainText = rpn ? "RPN" : "ALG";
            chipMode.Toggled = rpn;
            switch (fmt.Format)
            {
                case DisplayFormat.Scientific: chipFormat.MainText = "SCI"; break;
                case DisplayFormat.Engineering: chipFormat.MainText = "ENG"; break;
                case DisplayFormat.Fixed: chipFormat.MainText = "FIX" + fmt.FixedDecimals; break;
                default: chipFormat.MainText = "NORM"; break;
            }
            chipFormat.Toggled = fmt.Format != DisplayFormat.Normal;
            chipHist.Toggled = sideVisible;

            bool dec = engine.Base == NumBase.Dec;
            int radix = (int)engine.Base;
            chipAngle.Enabled = dec;
            chipFormat.Enabled = dec;
            foreach (var k in funcDec) k.Button.Visible = dec && sciVisible;
            foreach (var k in funcBase) k.Button.Visible = !dec && sciVisible;
            chipSci.Toggled = sciVisible;
            MinimumSize = new Size(S(350), sciVisible ? S(560) : S(420));
            foreach (var k in funcBase.Concat(mainKeys))
            {
                if (k.Kind == KeyKind.Digit && k.Label.Length == 1)
                {
                    int dv = "0123456789ABCDEF".IndexOf(k.Label[0]);
                    if (dv >= 0) k.Button.Enabled = dv < radix;
                }
            }
            foreach (var k in mainKeys)
            {
                if (k.Action == "dec")
                {
                    k.Button.MainText = fmt.DecimalComma ? "," : ".";
                    k.Button.Enabled = dec;
                }
                else if (k.Action == "sep") k.Button.MainText = rpn ? k.RpnLabel : (fmt.DecimalComma ? ";" : ",");
                else if (k.RpnLabel != null) k.Button.MainText = rpn ? k.RpnLabel : k.Label;
                if (k.Action == "eq") k.Button.FontScale = rpn ? 0.85f : 1.15f;
            }
            exprBox.TextAlign = rpn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            exprBox.Font = new Font("Segoe UI", rpn ? 17f : 20f);
            TopMost = topMostSetting;
            foreach (var b in allButtons) b.Invalidate();
            LayoutAll();
            if (rpn) RefreshStack(); else UpdatePreview();
        }

        bool topMostSetting;

        void CycleAngle()
        {
            engine.Angle = engine.Angle == AngleMode.Deg ? AngleMode.Rad : engine.Angle == AngleMode.Rad ? AngleMode.Grad : AngleMode.Deg;
            UpdateModeUi();
        }

        void CycleBase()
        {
            NumBase nb = engine.Base == NumBase.Dec ? NumBase.Hex : engine.Base == NumBase.Hex ? NumBase.Oct :
                         engine.Base == NumBase.Oct ? NumBase.Bin : NumBase.Dec;
            SetBase(nb);
        }

        void SetBase(NumBase nb)
        {
            if (nb == engine.Base) return;
            if (!rpn)
            {
                string t = exprBox.Text.Trim();
                double v = 0;
                bool ok = false;
                if (t.Length > 0)
                {
                    try
                    {
                        var r = engine.Evaluate(t, false);
                        ok = r.DefinedFunction == null && r.AssignedVar == null;
                        v = r.Value;
                    }
                    catch (CalcException) { }
                }
                engine.Base = nb;
                if (ok)
                {
                    bool je = justEvaluated;
                    suppressText = true;
                    exprBox.Text = Formatter.Plain(v, nb, fmt);
                    exprBox.SelectionStart = exprBox.TextLength;
                    suppressText = false;
                    justEvaluated = je;
                }
            }
            else engine.Base = nb;
            UpdateModeUi();
        }

        void ToggleRpn()
        {
            rpn = !rpn;
            justEvaluated = false;
            suppressText = true;
            exprBox.Text = "";
            suppressText = false;
            UpdateModeUi();
        }

        void CycleFormat()
        {
            fmt.Format = fmt.Format == DisplayFormat.Normal ? DisplayFormat.Scientific :
                         fmt.Format == DisplayFormat.Scientific ? DisplayFormat.Engineering :
                         fmt.Format == DisplayFormat.Engineering ? DisplayFormat.Fixed : DisplayFormat.Normal;
            UpdateModeUi();
        }

        void ToggleSide()
        {
            sideVisible = !sideVisible;
            int sw = S(290);
            if (WindowState == FormWindowState.Normal)
            {
                var wa = Screen.FromControl(this).WorkingArea;
                int nw = Width + (sideVisible ? sw : -sw);
                int nx = Left;
                if (sideVisible && nx + nw > wa.Right) nx = Math.Max(wa.Left, wa.Right - nw);
                SetBounds(nx, Top, nw, Height);
            }
            RefreshSide();
            UpdateModeUi();
        }

        void ToggleSci()
        {
            int delta = funcBlockH > 0 ? funcBlockH : S(190);
            sciVisible = !sciVisible;
            if (WindowState == FormWindowState.Normal)
            {
                var wa = Screen.FromControl(this).WorkingArea;
                if (sciVisible)
                {
                    int nh = Height + delta;
                    int ny = Top;
                    if (ny + nh > wa.Bottom) ny = Math.Max(wa.Top, wa.Bottom - nh);
                    SetBounds(Left, ny, Width, Math.Min(nh, wa.Height));
                }
                else
                {
                    MinimumSize = new Size(S(350), S(420));
                    Height = Math.Max(S(420), Height - delta);
                }
            }
            if (shift && !sciVisible) SetShift(false);
            UpdateModeUi();
        }

        void SetShift(bool on)
        {
            shift = on;
            foreach (var list in new[] { funcDec, funcBase, mainKeys })
                foreach (var k in list)
                {
                    k.Button.ShiftActive = on;
                    if (k.Action == "2nd") k.Button.Toggled = on;
                    k.Button.Invalidate();
                }
        }

        // ============================================================== Keys

        void DoKey(KeyDef k)
        {
            bool sh = shift && k.SLabel != null;
            string action = sh ? k.SAction : k.Action;
            string insert = sh ? k.SInsert : k.Insert;
            string rpnOp = sh ? k.SRpn : k.Rpn;
            int arity = sh ? k.SArity : k.Arity;
            bool isOp = sh ? k.SIsOp : k.IsOp;
            if (rpn && !sh && k.RpnAction != null) action = k.RpnAction;
            if (shift && k.Action != "2nd") SetShift(false);

            if (action != null) DoAction(action, k.Button);
            else if (rpn && rpnOp != null) RpnApply(rpnOp, arity);
            else if (insert == ")" || insert == "]" || insert == "}") InsertCloser(insert[0]);
            else if (insert != null) InsertText(insert, isOp);
            exprBox.Focus();
        }

        void DoAction(string a, Control source)
        {
            switch (a)
            {
                case "2nd": SetShift(!shift); break;
                case "back":
                    if (rpn && exprBox.TextLength == 0) RpnDrop();
                    else { justEvaluated = false; Backspace(); }
                    break;
                case "clear": ClearEntry(); break;
                case "allclear":
                    ClearEntry();
                    stack.Clear();
                    RefreshStack();
                    break;
                case "eq": Commit(); break;
                case "enter": RpnEnter(); break;
                case "swap":
                    if (!RpnPushEntry()) break;
                    if (stack.Count >= 2)
                    {
                        int n = stack.Count;
                        double t = stack[n - 1]; stack[n - 1] = stack[n - 2]; stack[n - 2] = t;
                        RefreshStack();
                    }
                    break;
                case "drop": RpnDrop(); break;
                case "neg": Negate(); break;
                case "dec": InsertText(fmt.DecimalComma ? "," : ".", false); break;
                case "sep": InsertText(fmt.DecimalComma ? ";" : ",", false); break;
                case "fx": ShowFxMenu(source); break;
                case "const": ShowConstMenu(source); break;
            }
        }

        void ClearEntry()
        {
            justEvaluated = false;
            histNav = -1;
            exprBox.Text = "";
            if (!rpn) { resultLabel.Text = ""; infoLabel.Text = ""; stepLabel.Text = ""; }
            else RefreshStack();
        }

        void InsertText(string s, bool isOp)
        {
            if (justEvaluated && !rpn)
            {
                suppressText = true;
                exprBox.Text = isOp ? "ans" : "";
                exprBox.SelectionStart = exprBox.TextLength;
                suppressText = false;
                justEvaluated = false;
            }
            string sel = exprBox.SelectedText;
            if (sel.Length > 0 && s.EndsWith("(")) s = s + sel + ")";
            exprBox.SelectedText = s;
            exprBox.Focus();
        }

        static char OpenerOf(char closer) { return closer == ']' ? '[' : closer == '}' ? '{' : '('; }
        static char CloserOf(char opener) { return opener == '[' ? ']' : opener == '{' ? '}' : ')'; }

        /// <summary>
        /// Digita um fechamento ( ) ] } ). Se não houver a abertura correspondente antes do cursor,
        /// ela é inserida automaticamente: "56-3)" → "(56-3)", "[2+(3)×4]" etc.
        /// </summary>
        void InsertCloser(char closer)
        {
            char opener = OpenerOf(closer);
            justEvaluated = false; // mantém a expressão anterior: "56-3" Enter, ")" → "(56-3)"
            string sel = exprBox.SelectedText;
            if (sel.Length > 0)
            {
                exprBox.SelectedText = opener + sel + closer;
                return;
            }
            string t = exprBox.Text;
            int st = exprBox.SelectionStart;
            var open = new List<int>();
            for (int i = 0; i < st; i++)
            {
                char ch = t[i];
                if (ch == '(' || ch == '[' || ch == '{') open.Add(i);
                else if ((ch == ')' || ch == ']' || ch == '}') && open.Count > 0) open.RemoveAt(open.Count - 1);
            }
            // procura a abertura do mesmo tipo mais interna; fecha também as que estiverem dentro dela
            for (int k = open.Count - 1; k >= 0; k--)
            {
                if (t[open[k]] != opener) continue;
                var sb = new StringBuilder();
                for (int j = open.Count - 1; j > k; j--) sb.Append(CloserOf(t[open[j]]));
                sb.Append(closer);
                exprBox.SelectedText = sb.ToString();
                return;
            }
            // abre logo após a abertura pendente mais interna (ou no início da expressão)
            int ins = open.Count > 0 ? open[open.Count - 1] + 1 : 0;
            while (ins < st && t[ins] == ' ') ins++;
            if (ins >= st)
            {
                exprBox.SelectedText = closer.ToString();
                return;
            }
            string nt = t.Substring(0, ins) + opener + t.Substring(ins, st - ins) + closer + t.Substring(st);
            exprBox.Text = nt;
            exprBox.SelectionStart = st + 2;
        }

        void Backspace()
        {
            int st = exprBox.SelectionStart, len = exprBox.SelectionLength;
            if (len > 0) { exprBox.SelectedText = ""; return; }
            if (st == 0) return;
            string t = exprBox.Text;
            string before = t.Substring(0, st);
            int del = 1;
            Match m = Regex.Match(before, @"[\p{L}_][\p{L}\p{N}_]*\($");
            if (m.Success) del = m.Length;
            else
            {
                Match m2 = Regex.Match(before, @" (mod|and|or|xor|<<|>>) $");
                if (m2.Success) del = m2.Length;
                else if (before.EndsWith("⁻¹")) del = 2;
            }
            exprBox.Text = t.Remove(st - del, del);
            exprBox.SelectionStart = st - del;
        }

        void Negate()
        {
            if (rpn)
            {
                string t = exprBox.Text;
                if (t.Trim().Length > 0)
                {
                    exprBox.Text = t.StartsWith("-") ? t.Substring(1) : "-" + t;
                    exprBox.SelectionStart = exprBox.TextLength;
                }
                else if (stack.Count > 0)
                {
                    stack[stack.Count - 1] = -stack[stack.Count - 1];
                    RefreshStack();
                }
                return;
            }
            string cur = exprBox.Text.Trim();
            if (justEvaluated) cur = "ans";
            if (cur.Length == 0) { InsertText("−", false); return; }
            if (cur.StartsWith("-(") && cur.EndsWith(")")) cur = cur.Substring(2, cur.Length - 3);
            else cur = "-(" + cur + ")";
            justEvaluated = false;
            exprBox.Text = cur;
            exprBox.SelectionStart = exprBox.TextLength;
        }

        // ======================================================= Algebraic

        void UpdatePreview()
        {
            if (rpn) return;
            string t = exprBox.Text.Trim();
            stepLabel.Text = "";
            if (t.Length == 0) { resultLabel.Text = ""; infoLabel.Text = ""; return; }
            try
            {
                var r = engine.Evaluate(t, false);
                stepLabel.Text = Formatter.PercentSteps(r, engine.Base, fmt);
                if (r.DefinedFunction != null)
                {
                    resultLabel.ForeColor = theme.SubText;
                    resultLabel.Text = justEvaluated ? "✓ " + r.DefinedFunction.Signature : "Enter define " + r.DefinedFunction.Signature;
                    infoLabel.Text = "";
                }
                else ShowValue(r.Value, !justEvaluated, r.AssignedVar);
            }
            catch (CalcException)
            {
                resultLabel.Text = "";
                infoLabel.Text = "";
            }
        }

        void ShowValue(double v, bool preview, string varName)
        {
            resultLabel.ForeColor = preview ? theme.SubText : theme.Text;
            resultLabel.MaxPx = S(46);
            string txt = Formatter.Format(v, engine.Base, fmt);
            resultLabel.Text = (varName != null ? varName + " = " : "= ") + txt;
            infoLabel.ForeColor = theme.SubText;
            infoLabel.Text = BuildInfo(v);
        }

        string BuildInfo(double v)
        {
            if (engine.Base != NumBase.Dec)
            {
                long l;
                if (!Formatter.TryLong(v, out l)) return "";
                var parts = new List<string>();
                foreach (NumBase b in new[] { NumBase.Hex, NumBase.Dec, NumBase.Oct, NumBase.Bin })
                    if (b != engine.Base) parts.Add(b.ToString().ToUpperInvariant() + "  " + Formatter.FormatBase(v, b, true));
                return string.Join("     ", parts);
            }
            var info = new List<string>();
            string frac = Formatter.ToFraction(v, false);
            if (frac != null)
            {
                string mixed = Formatter.ToFraction(v, true);
                info.Add("Fração  " + frac + (mixed != frac ? "  =  " + mixed : ""));
            }
            if (showDms && v != Math.Floor(v) && engine.Angle == AngleMode.Deg)
            {
                string dms = Formatter.ToDms(v, fmt.DecimalComma);
                if (dms != null) info.Add("GMS  " + dms);
            }
            if (fmt.Format != DisplayFormat.Normal)
                info.Add(Formatter.FormatDec(v, fmt.Digits, DisplayFormat.Normal, 0, fmt.Grouping, fmt.DecimalComma));
            return string.Join("      ", info);
        }

        void ShowError(string msg)
        {
            if (rpn)
            {
                infoLabel.ForeColor = theme.Error;
                infoLabel.Text = msg;
            }
            else
            {
                resultLabel.ForeColor = theme.Error;
                resultLabel.MaxPx = S(24);
                resultLabel.Text = msg;
                infoLabel.Text = "";
                stepLabel.Text = "";
            }
            System.Media.SystemSounds.Beep.Play();
        }

        void Commit()
        {
            string t = exprBox.Text.Trim();
            if (t.Length == 0) return;
            try
            {
                var r = engine.Evaluate(t, true);
                stepLabel.Text = Formatter.PercentSteps(r, engine.Base, fmt);
                if (r.DefinedFunction != null)
                {
                    resultLabel.ForeColor = theme.Text;
                    resultLabel.Text = "✓ " + r.DefinedFunction.Signature;
                    infoLabel.Text = "Função definida: " + r.DefinedFunction.Signature + " = " + r.DefinedFunction.Body;
                    AddHistory(t, double.NaN, "✓ " + r.DefinedFunction.Signature);
                }
                else
                {
                    AddHistory(t, r.Value, Formatter.Format(r.Value, engine.Base, fmt));
                    ShowValue(r.Value, false, r.AssignedVar);
                }
                justEvaluated = true;
                histNav = -1;
                exprBox.SelectionStart = exprBox.TextLength;
                RefreshSide();
            }
            catch (CalcException ex) { ShowError(ex.Message); }
        }

        void AddHistory(string expr, double v, string text)
        {
            history.Insert(0, new HistItem { Expr = expr.Replace('\t', ' '), Value = v, Text = text });
            if (history.Count > 500) history.RemoveAt(history.Count - 1);
        }

        // ============================================================== RPN

        bool RpnPushEntry()
        {
            string t = exprBox.Text.Trim();
            if (t.Length == 0) return true;
            try
            {
                var r = engine.Evaluate(t, true);
                suppressText = true;
                exprBox.Text = "";
                suppressText = false;
                if (r.DefinedFunction != null)
                {
                    RefreshSide();
                    RefreshStack();
                    infoLabel.Text = "Função definida: " + r.DefinedFunction.Signature;
                    return false;
                }
                stack.Add(r.Value);
                if (r.AssignedVar != null) RefreshSide();
                RefreshStack();
                return true;
            }
            catch (CalcException ex)
            {
                ShowError(ex.Message);
                return false;
            }
        }

        void RpnEnter()
        {
            if (exprBox.Text.Trim().Length == 0)
            {
                if (stack.Count > 0) stack.Add(stack[stack.Count - 1]);
                RefreshStack();
            }
            else RpnPushEntry();
        }

        void RpnDrop()
        {
            if (exprBox.Text.Trim().Length > 0) { exprBox.Text = ""; return; }
            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            RefreshStack();
        }

        static string OpSymbol(string op)
        {
            switch (op)
            {
                case "*": return "×";
                case "/": return "÷";
                case "-": return "−";
                case "sqr": return "x²";
                case "cube": return "x³";
                case "inv": return "1/x";
                case "pct": return "%";
                case "pow10": return "10ˣ";
                case "fact": return "!";
                default: return op;
            }
        }

        void RpnApply(string op, int arity)
        {
            if (!RpnPushEntry()) return;
            if (stack.Count < arity)
            {
                ShowError("Pilha insuficiente: " + op + " requer " + arity + " valor(es)");
                return;
            }
            double[] args = stack.GetRange(stack.Count - arity, arity).ToArray();
            try
            {
                double v;
                if (arity == 0) v = engine.CallFunction(op, new double[0]);
                else if (arity == 1) v = engine.ApplyUnary(op, args[0]);
                else v = engine.ApplyOperator(op, args[0], args[1]);
                if (double.IsNaN(v)) throw new MathDomainException("Resultado indefinido");
                if (double.IsInfinity(v)) throw new MathDomainException("Estouro: resultado muito grande");
                stack.RemoveRange(stack.Count - arity, arity);
                stack.Add(v);
                engine.Vars["ans"] = v;
                string expr = string.Join(" ", args.Select(x => Formatter.Plain(x, engine.Base, fmt)).ToArray()) + " " + OpSymbol(op);
                AddHistory(expr.Trim(), v, Formatter.Format(v, engine.Base, fmt));
                RefreshStack();
                RefreshSide();
            }
            catch (CalcException ex) { ShowError(ex.Message); }
        }

        void RefreshStack()
        {
            stackView.Items = stack.Select(x => Formatter.Format(x, engine.Base, fmt)).ToList();
            stackView.Invalidate();
            if (rpn)
            {
                infoLabel.ForeColor = theme.SubText;
                infoLabel.Text = stack.Count > 0 ? BuildInfo(stack[stack.Count - 1]) : "";
            }
        }

        // ======================================================== Keyboard

        static readonly Regex AssignTargetRx = new Regex(@"^\s*[\p{L}_][\p{L}\p{N}_]*\s*(\([^()]*\))?\s*$");

        void ExprKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    if (rpn) RpnEnter(); else Commit();
                    return;
                case Keys.Escape:
                    e.SuppressKeyPress = true;
                    ClearEntry();
                    return;
                case Keys.F1: e.Handled = true; ShowHelp(); return;
                case Keys.F2: e.Handled = true; SetShift(!shift); return;
                case Keys.F4: e.Handled = true; ToggleSci(); return;
                case Keys.F5: e.Handled = true; CycleAngle(); return;
                case Keys.F6: e.Handled = true; CycleBase(); return;
                case Keys.F7: e.Handled = true; ToggleRpn(); return;
                case Keys.F8: e.Handled = true; CycleFormat(); return;
                case Keys.F9: e.Handled = true; ToggleSide(); return;
                case Keys.Up:
                case Keys.Down:
                    if (!rpn && history.Count > 0)
                    {
                        e.Handled = true;
                        int nidx = histNav + (e.KeyCode == Keys.Up ? 1 : -1);
                        if (nidx < -1) nidx = -1;
                        if (nidx >= history.Count) nidx = history.Count - 1;
                        histNav = nidx;
                        exprBox.Text = nidx >= 0 ? history[nidx].Expr : "";
                        exprBox.SelectionStart = exprBox.TextLength;
                        histNav = nidx;
                    }
                    else if (rpn && e.KeyCode == Keys.Up && stack.Count > 0 && exprBox.TextLength == 0)
                    {
                        e.Handled = true;
                        exprBox.Text = Formatter.Plain(stack[stack.Count - 1], engine.Base, fmt);
                        stack.RemoveAt(stack.Count - 1);
                        exprBox.SelectionStart = exprBox.TextLength;
                        RefreshStack();
                    }
                    return;
                case Keys.Back:
                    if (rpn && exprBox.TextLength == 0) { e.SuppressKeyPress = true; RpnDrop(); }
                    else if (e.Modifiers == Keys.None && exprBox.SelectionLength == 0)
                    {
                        e.SuppressKeyPress = true;
                        justEvaluated = false;
                        Backspace();
                    }
                    return;
                case Keys.C:
                    if (e.Control && exprBox.SelectionLength == 0) { e.SuppressKeyPress = true; CopyResult(); }
                    return;
                case Keys.A:
                    if (e.Control) { e.SuppressKeyPress = true; exprBox.SelectAll(); }
                    return;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Esc e Enter são "teclas de diálogo" e não chegam ao KeyDown da caixa de texto
            if (exprBox.Focused && keyData == Keys.Escape) { ClearEntry(); return true; }
            if (exprBox.Focused && keyData == Keys.Enter) { if (rpn) RpnEnter(); else Commit(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void ExprKeyPress(object sender, KeyPressEventArgs e)
        {
            char c = e.KeyChar;
            if (c == '=')
            {
                string t = exprBox.Text;
                if (!rpn && exprBox.SelectionStart == t.Length && AssignTargetRx.IsMatch(t) && !justEvaluated) return;
                e.Handled = true;
                if (rpn) RpnEnter(); else Commit();
                return;
            }
            if (c == ')' || c == ']' || c == '}')
            {
                e.Handled = true;
                InsertCloser(c);
                return;
            }
            if (rpn)
            {
                string ops = "+-*/^×÷";
                if (ops.IndexOf(c) >= 0)
                {
                    string t = exprBox.Text;
                    int st = exprBox.SelectionStart;
                    bool exponentSign = (c == '-' || c == '+') && st > 0 && (t[st - 1] == 'E' || t[st - 1] == 'e') && engine.Base == NumBase.Dec;
                    bool negStart = c == '-' && t.Trim().Length == 0 && stack.Count < 2;
                    bool inExpr = t.IndexOf('(') >= 0 && t.Count(ch => ch == '(') > t.Count(ch => ch == ')');
                    if (!exponentSign && !negStart && !inExpr)
                    {
                        e.Handled = true;
                        string op = c == '×' ? "*" : c == '÷' ? "/" : c.ToString();
                        RpnApply(op, 2);
                    }
                }
                return;
            }
            if (justEvaluated && !char.IsControl(c))
            {
                const string ops = "+-*/^!%²³×÷−";
                suppressText = true;
                exprBox.Text = ops.IndexOf(c) >= 0 ? "ans" : "";
                exprBox.SelectionStart = exprBox.TextLength;
                suppressText = false;
                justEvaluated = false;
            }
        }

        void CopyResult()
        {
            double v = double.NaN;
            if (rpn) { if (stack.Count > 0) v = stack[stack.Count - 1]; }
            else
            {
                try { v = engine.Evaluate(exprBox.Text, false).Value; } catch (CalcException) { }
            }
            if (double.IsNaN(v)) return;
            try { Clipboard.SetText(Formatter.Plain(v, engine.Base, fmt)); } catch { }
        }

        // ======================================================= Side panel

        void RefreshSide()
        {
            if (sideList == null) return;
            tabHist.Toggled = !showVarsTab;
            tabVars.Toggled = showVarsTab;
            tabHist.Invalidate(); tabVars.Invalidate();
            sideList.BeginUpdate();
            sideList.Items.Clear();
            if (!showVarsTab)
            {
                foreach (var h in history) sideList.Items.Add(h);
            }
            else
            {
                foreach (var kv in engine.Vars.OrderBy(k => k.Key.ToLowerInvariant() == "ans" ? "" : k.Key))
                    sideList.Items.Add(new VarItem { Name = kv.Key, Display = Formatter.Format(kv.Value, engine.Base, fmt), Insert = kv.Key });
                foreach (var f in engine.Funcs.Values.OrderBy(f => f.Name))
                    sideList.Items.Add(new VarItem { Name = f.Signature, Display = f.Body, Insert = f.Name + "(", IsFunc = true });
            }
            sideList.EndUpdate();
        }

        void SideMeasure(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = Math.Min(255, sideList.Items[e.Index] is HistItem ? S(56) : S(36));
        }

        void SideDraw(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= sideList.Items.Count) return;
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var br = new SolidBrush(sel ? theme.Selection : theme.Panel))
                g.FillRectangle(br, e.Bounds);
            var b = e.Bounds;
            int p = S(8);
            object item = sideList.Items[e.Index];
            var flagsL = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            var flagsR = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            var h = item as HistItem;
            if (h != null)
            {
                var r1 = new Rectangle(b.X + p, b.Y + S(5), b.Width - 2 * p, S(20));
                var r2 = new Rectangle(b.X + p, b.Y + S(24), b.Width - 2 * p, S(28));
                TextRenderer.DrawText(g, h.Expr, Gfx.FontPx(S(13), false), r1, theme.SubText, flagsR);
                TextRenderer.DrawText(g, double.IsNaN(h.Value) ? h.Text : "= " + h.Text, Gfx.FontPx(S(18), false), r2, theme.Text, flagsR);
            }
            var v = item as VarItem;
            if (v != null)
            {
                var r = new Rectangle(b.X + p, b.Y, b.Width - 2 * p, b.Height);
                int nameW = Math.Min(r.Width / 2, TextRenderer.MeasureText(v.Name, Gfx.FontPx(S(14), true)).Width + S(4));
                TextRenderer.DrawText(g, v.Name, Gfx.FontPx(S(14), true), new Rectangle(r.X, r.Y, nameW, r.Height), theme.Accent, flagsL);
                TextRenderer.DrawText(g, (v.IsFunc ? "= " : "") + v.Display, Gfx.FontPx(S(14), false),
                    new Rectangle(r.X + nameW + S(6), r.Y, r.Width - nameW - S(6), r.Height), theme.Text, flagsR);
            }
            using (var pen = new Pen(Gfx.Mix(theme.Panel, theme.SubText, 0.18f)))
                g.DrawLine(pen, b.X + p, b.Bottom - 1, b.Right - p, b.Bottom - 1);
        }

        void UseSideItem(bool expression)
        {
            object item = sideList.SelectedItem;
            var h = item as HistItem;
            if (h != null)
            {
                if (expression || double.IsNaN(h.Value))
                {
                    justEvaluated = false;
                    exprBox.Text = rpn ? Formatter.Plain(h.Value, engine.Base, fmt) : h.Expr;
                    exprBox.SelectionStart = exprBox.TextLength;
                }
                else InsertText(Formatter.Plain(h.Value, engine.Base, fmt), false);
            }
            var v = item as VarItem;
            if (v != null) InsertText(v.Insert, false);
            exprBox.Focus();
        }

        void ClearVars()
        {
            double ans;
            engine.Vars.TryGetValue("ans", out ans);
            engine.Vars.Clear();
            engine.Vars["ans"] = ans;
            engine.Funcs.Clear();
        }

        ContextMenuStrip NewMenu()
        {
            return new ContextMenuStrip
            {
                Renderer = new ThemeRenderer(theme),
                Font = new Font("Segoe UI", 10f),
                ShowImageMargin = true,
            };
        }

        static ToolStripMenuItem Item(string text, Action a)
        {
            var it = new ToolStripMenuItem(text);
            it.Click += (s, e) => a();
            return it;
        }

        static ToolStripMenuItem Check(string text, bool check, Action a)
        {
            var it = Item(text, a);
            it.Checked = check;
            return it;
        }

        void ShowListMenu(Point at)
        {
            var m = NewMenu();
            object item = sideList.SelectedItem;
            var h = item as HistItem;
            var v = item as VarItem;
            if (h != null)
            {
                if (!double.IsNaN(h.Value))
                {
                    m.Items.Add(Item("Inserir resultado", () => UseSideItem(false)));
                    m.Items.Add(Item("Usar expressão", () => UseSideItem(true)));
                    m.Items.Add(Item("Copiar resultado", () => { try { Clipboard.SetText(Formatter.Plain(h.Value, engine.Base, fmt)); } catch { } }));
                }
                m.Items.Add(Item("Copiar expressão", () => { try { Clipboard.SetText(h.Expr); } catch { } }));
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add(Item("Excluir", () => { history.Remove(h); RefreshSide(); }));
            }
            else if (v != null)
            {
                m.Items.Add(Item("Inserir", () => UseSideItem(false)));
                if (!v.IsFunc || true)
                    m.Items.Add(Item("Excluir", () =>
                    {
                        if (v.IsFunc) engine.Funcs.Remove(v.Insert.TrimEnd('('));
                        else if (!v.Name.Equals("ans", StringComparison.OrdinalIgnoreCase)) engine.Vars.Remove(v.Name);
                        RefreshSide();
                    }));
            }
            m.Show(sideList, at);
        }

        // ============================================================ Menus

        void ShowMainMenu()
        {
            var m = NewMenu();

            var ang = new ToolStripMenuItem("Unidade angular");
            ang.DropDownItems.Add(Check("Graus (DEG)", engine.Angle == AngleMode.Deg, () => { engine.Angle = AngleMode.Deg; UpdateModeUi(); }));
            ang.DropDownItems.Add(Check("Radianos (RAD)", engine.Angle == AngleMode.Rad, () => { engine.Angle = AngleMode.Rad; UpdateModeUi(); }));
            ang.DropDownItems.Add(Check("Grados (GRA)", engine.Angle == AngleMode.Grad, () => { engine.Angle = AngleMode.Grad; UpdateModeUi(); }));
            m.Items.Add(ang);

            var bas = new ToolStripMenuItem("Base numérica");
            bas.DropDownItems.Add(Check("Decimal", engine.Base == NumBase.Dec, () => SetBase(NumBase.Dec)));
            bas.DropDownItems.Add(Check("Hexadecimal", engine.Base == NumBase.Hex, () => SetBase(NumBase.Hex)));
            bas.DropDownItems.Add(Check("Octal", engine.Base == NumBase.Oct, () => SetBase(NumBase.Oct)));
            bas.DropDownItems.Add(Check("Binário", engine.Base == NumBase.Bin, () => SetBase(NumBase.Bin)));
            m.Items.Add(bas);

            var mode = new ToolStripMenuItem("Modo de entrada");
            mode.DropDownItems.Add(Check("Algébrico (expressões)", !rpn, () => { if (rpn) ToggleRpn(); }));
            mode.DropDownItems.Add(Check("RPN (pilha)", rpn, () => { if (!rpn) ToggleRpn(); }));
            m.Items.Add(mode);

            var fm = new ToolStripMenuItem("Formato do resultado");
            fm.DropDownItems.Add(Check("Normal", fmt.Format == DisplayFormat.Normal, () => { fmt.Format = DisplayFormat.Normal; UpdateModeUi(); }));
            fm.DropDownItems.Add(Check("Científico (1.23E4)", fmt.Format == DisplayFormat.Scientific, () => { fmt.Format = DisplayFormat.Scientific; UpdateModeUi(); }));
            fm.DropDownItems.Add(Check("Engenharia (12.3E3)", fmt.Format == DisplayFormat.Engineering, () => { fmt.Format = DisplayFormat.Engineering; UpdateModeUi(); }));
            var fix = new ToolStripMenuItem("Fixo (casas decimais)");
            fix.Checked = fmt.Format == DisplayFormat.Fixed;
            for (int i = 0; i <= 10; i++)
            {
                int n = i;
                fix.DropDownItems.Add(Check(n + (n == 1 ? " casa" : " casas"), fmt.Format == DisplayFormat.Fixed && fmt.FixedDecimals == n,
                    () => { fmt.Format = DisplayFormat.Fixed; fmt.FixedDecimals = n; UpdateModeUi(); }));
            }
            fm.DropDownItems.Add(fix);
            fm.DropDownItems.Add(new ToolStripSeparator());
            var prec = new ToolStripMenuItem("Algarismos significativos");
            foreach (int d in new[] { 6, 8, 10, 12, 14, 15 })
            {
                int n = d;
                prec.DropDownItems.Add(Check(n.ToString(), fmt.Digits == n, () => { fmt.Digits = n; UpdateModeUi(); }));
            }
            fm.DropDownItems.Add(prec);
            m.Items.Add(fm);

            var sepm = new ToolStripMenuItem("Separador decimal");
            sepm.DropDownItems.Add(Check("Ponto   1.5   (argumentos com , ou ;)", !fmt.DecimalComma, () => SetDecimalComma(false)));
            sepm.DropDownItems.Add(Check("Vírgula   1,5   (argumentos com ;)", fmt.DecimalComma, () => SetDecimalComma(true)));
            m.Items.Add(sepm);
            m.Items.Add(Check("Separar milhares", fmt.Grouping, () => { fmt.Grouping = !fmt.Grouping; UpdateModeUi(); RefreshSide(); }));
            m.Items.Add(Check("Mostrar graus-minutos-segundos", showDms, () => { showDms = !showDms; UpdateModeUi(); }));
            m.Items.Add(new ToolStripSeparator());

            var th = new ToolStripMenuItem("Tema");
            th.DropDownItems.Add(Check("Escuro", theme.Name == "dark", () => { theme = Theme.Dark(); ApplyTheme(); UpdateModeUi(); }));
            th.DropDownItems.Add(Check("Claro", theme.Name == "light", () => { theme = Theme.Light(); ApplyTheme(); UpdateModeUi(); }));
            m.Items.Add(th);
            m.Items.Add(Check("Sempre visível (sobre outras janelas)", topMostSetting, () => { topMostSetting = !topMostSetting; TopMost = topMostSetting; }));
            m.Items.Add(Check("Teclado científico  (F4)", sciVisible, ToggleSci));
            m.Items.Add(Check("Painel de histórico  (F9)", sideVisible, ToggleSide));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item("Copiar resultado  (Ctrl+C)", CopyResult));
            m.Items.Add(Item("Limpar histórico", () => { history.Clear(); RefreshSide(); }));
            m.Items.Add(Item("Limpar variáveis e funções", () => { ClearVars(); RefreshSide(); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item("Ajuda  (F1)", ShowHelp));
            m.Items.Add(Item("Sobre o " + AppName, () => MessageBox.Show(this,
                AppName + " " + Version + "\nCalculadora científica para Windows.\n\nAplicativo nativo (.NET Framework / WinForms), sem navegador.\n" +
                "Dados salvos em:\n" + StatePath, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information)));
            m.Show(chipMenu, new Point(chipMenu.Width - 2, chipMenu.Height), ToolStripDropDownDirection.BelowLeft);
        }

        void SetDecimalComma(bool on)
        {
            fmt.DecimalComma = on;
            engine.DecimalComma = on;
            UpdateModeUi();
            RefreshSide();
        }

        void InsertFunction(FnInfo f)
        {
            if (rpn) { RpnApply(f.Name, f.Arity); return; }
            InsertText(f.Arity == 0 ? f.Name + "()" : f.Name + "(", false);
        }

        void ShowFxMenu(Control source)
        {
            var m = NewMenu();
            foreach (var grp in Catalog.GroupBy(f => f.Cat))
            {
                var sub = new ToolStripMenuItem(grp.Key);
                foreach (var f in grp)
                {
                    var fi = f;
                    sub.DropDownItems.Add(Item(fi.Sig + "   —   " + fi.Desc, () => { InsertFunction(fi); exprBox.Focus(); }));
                }
                m.Items.Add(sub);
            }
            m.Items.Add(new ToolStripSeparator());
            var uf = new ToolStripMenuItem("Minhas funções");
            if (engine.Funcs.Count == 0)
            {
                var none = new ToolStripMenuItem("Nenhuma.  Defina com:  f(x) = x^2 + 1") { Enabled = false };
                uf.DropDownItems.Add(none);
            }
            foreach (var f in engine.Funcs.Values.OrderBy(x => x.Name))
            {
                var ff = f;
                uf.DropDownItems.Add(Item(ff.Signature + " = " + ff.Body, () =>
                {
                    if (rpn) RpnApply(ff.Name, ff.Params.Length);
                    else InsertText(ff.Name + "(", false);
                    exprBox.Focus();
                }));
            }
            m.Items.Add(uf);
            m.Show(source, new Point(0, source.Height));
        }

        void ShowConstMenu(Control source)
        {
            var m = NewMenu();
            m.Items.Add(Item("π   =  3.14159265359   (pi)", () => { InsertText("π", false); exprBox.Focus(); }));
            m.Items.Add(Item("e   =  2.71828182846   (Euler)", () => { InsertText("e", false); exprBox.Focus(); }));
            m.Items.Add(Item("φ   =  1.61803398875   (razão áurea)", () => { InsertText("φ", false); exprBox.Focus(); }));
            m.Items.Add(new ToolStripSeparator());
            var phys = new ToolStripMenuItem("Física e química");
            phys.Enabled = engine.Base == NumBase.Dec;
            foreach (var c in PhysConsts)
            {
                string val = c[2];
                string shown = fmt.DecimalComma ? val.Replace('.', ',') : val;
                phys.DropDownItems.Add(Item(c[1] + "   " + c[0] + "   =  " + shown + "  " + c[3], () =>
                {
                    string t = shown;
                    int st = exprBox.SelectionStart;
                    if (!rpn && !justEvaluated && st > 0 && (char.IsLetterOrDigit(exprBox.Text[st - 1]) || exprBox.Text[st - 1] == ')'))
                        t = "×" + t;
                    if (rpn && exprBox.Text.Trim().Length > 0 && !RpnPushEntry()) return;
                    InsertText(t, false);
                    exprBox.Focus();
                }));
            }
            m.Items.Add(phys);
            m.Show(source, new Point(0, source.Height));
        }

        void ShowHelp()
        {
            const string help =
@"CALCPRO — AJUDA

ENTRADA
  Digite a expressão livremente (teclado ou botões). O resultado aparece em
  tempo real; Enter (ou =) confirma e envia para o histórico.
  Depois do resultado, digitar um operador continua a partir de 'ans'.
  ↑ / ↓ navegam pelas expressões anteriores.  Esc limpa.

OPERADORES
  + − × ÷ ^      soma, subtração, multiplicação, divisão, potência
  2(3+4)  2π     multiplicação implícita
  x² x³ x⁻¹ n!   pós-fixos          √x  ∛x   raízes
  %              200+10% = 220 · 50×10% = 5 · 10% = 0,1
                 Em somas/subtrações o passo aparece abaixo: 50-3% → 50-(1,5)
  mod            resto: 7 mod 3 = 1
  1.5E3          notação científica (EXP)
  30°15'20""      graus-minutos-segundos (converte para a unidade atual)
  Parênteses que faltarem no final são fechados automaticamente.
  Fechar sem ter aberto abre sozinho:  56-3)  →  (56-3)
  Vale para ( ) [ ] { }.  Ex.: [10-(4+2   e  ]  →  [10-(4+2)]
  Logo após um resultado, ) envolve a expressão anterior.
  2nd + ( ou ) insere [ ou ].

FUNÇÕES   (botão f(x) lista todas)
  sin cos tan cot sec csc, asin acos atan acot atan2
  sinh cosh tanh asinh acosh atanh
  ln log log2 exp  ·  log(x; base)
  sqrt cbrt root(x; n) pow hypot
  abs floor ceil round(x; casas) trunc frac sign
  fact gamma nPr nCr gcd lcm min max sum avg rand randint
  Separe argumentos com ';' (ou ',' quando o decimal é ponto).

VARIÁVEIS E FUNÇÕES PRÓPRIAS
  x = 5              cria/atualiza a variável x
  área(r) = π r²     define uma função  →  área(2)
  f(a; b) = a² + b   funções com vários parâmetros
  ans                último resultado
  Constantes: π (pi), e, φ (phi) e o botão const (constantes físicas).

MODOS (barra superior)
  DEG / RAD / GRA    unidade angular                       F5
  DEC / HEX / OCT / BIN  base numérica (inteiros 64 bits)  F6
     Em HEX/OCT/BIN: AND OR XOR NOT << >> e prefixos 0x 0b 0o
  ALG / RPN          algébrico ou notação polonesa reversa F7
     RPN: digite um número e ENTER; operadores usam a pilha.
     x⇄y troca, DROP remove, ⌫ com entrada vazia remove o topo.
  NORM / SCI / ENG / FIX  formato do resultado             F8
  f(x) mostra / oculta o teclado científico                F4
  ☰  histórico e variáveis                                 F9
  2nd (F2) ativa as funções secundárias (em laranja).

ATALHOS
  Enter =       calcular        Ctrl+C (sem seleção)  copiar resultado
  Esc           limpar          F1                    esta ajuda
  Duplo clique no histórico insere o resultado; botão direito = mais opções.
";
            var f = new Form
            {
                Text = AppName + " — Ajuda",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(S(660), S(700)),
                BackColor = theme.Panel,
                ShowInTaskbar = false,
                MinimizeBox = false,
                Icon = Icon,
            };
            var tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10.5f),
                BackColor = theme.Panel,
                ForeColor = theme.Text,
                BorderStyle = BorderStyle.None,
                Text = help.Replace("\r\n", "\n").Replace("\n", "\r\n"),
            };
            var holder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(14)), BackColor = theme.Panel };
            holder.Controls.Add(tb);
            f.Controls.Add(holder);
            f.Shown += (s, e) => { tb.SelectionLength = 0; tb.SelectionStart = 0; };
            f.HandleCreated += (s, e) =>
            {
                try { int v = theme.Name == "dark" ? 1 : 0; DwmSetWindowAttribute(f.Handle, 20, ref v, 4); } catch { }
            };
            f.ShowDialog(this);
        }

        // ======================================================= Persistence

        static string StatePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName, "estado.txt");
            }
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveState();
            base.OnFormClosing(e);
        }

        static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '); }

        void SaveState()
        {
            try
            {
                var sb = new StringBuilder();
                Action<string, string> set = (k, v) => sb.Append("set\t").Append(k).Append('\t').Append(v).Append('\n');
                set("angle", engine.Angle.ToString());
                set("base", engine.Base.ToString());
                set("rpn", rpn ? "1" : "0");
                set("format", fmt.Format.ToString());
                set("digits", fmt.Digits.ToString(Inv));
                set("fixed", fmt.FixedDecimals.ToString(Inv));
                set("comma", fmt.DecimalComma ? "1" : "0");
                set("group", fmt.Grouping ? "1" : "0");
                set("dms", showDms ? "1" : "0");
                set("theme", theme.Name);
                set("topmost", topMostSetting ? "1" : "0");
                set("side", sideVisible ? "1" : "0");
                set("varstab", showVarsTab ? "1" : "0");
                set("sci", sciVisible ? "1" : "0");
                set("max", WindowState == FormWindowState.Maximized ? "1" : "0");
                Rectangle bnd = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                set("bounds", bnd.X + "," + bnd.Y + "," + bnd.Width + "," + bnd.Height);
                set("expr", Clean(exprBox.Text));
                foreach (var kv in engine.Vars)
                    sb.Append("var\t").Append(Clean(kv.Key)).Append('\t').Append(kv.Value.ToString("R", Inv)).Append('\n');
                foreach (var f in engine.Funcs.Values)
                    sb.Append("fn\t").Append(f.Name).Append('\t').Append(string.Join(";", f.Params)).Append('\t').Append(Clean(f.Body)).Append('\n');
                foreach (var v in stack)
                    sb.Append("stk\t").Append(v.ToString("R", Inv)).Append('\n');
                for (int i = Math.Min(history.Count, 300) - 1; i >= 0; i--)
                {
                    var h = history[i];
                    sb.Append("hist\t").Append(Clean(h.Expr)).Append('\t').Append(h.Value.ToString("R", Inv)).Append('\t').Append(Clean(h.Text)).Append('\n');
                }
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
                File.WriteAllText(StatePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        void LoadState()
        {
            string path = StatePath;
            if (!File.Exists(path)) return;
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); } catch { return; }
            bool maximized = false;
            string expr = "";
            foreach (string line in lines)
            {
                try
                {
                    string[] f = line.Split('\t');
                    if (f.Length < 2) continue;
                    switch (f[0])
                    {
                        case "set":
                            if (f.Length < 3) break;
                            string v = f[2];
                            switch (f[1])
                            {
                                case "angle": engine.Angle = (AngleMode)Enum.Parse(typeof(AngleMode), v); break;
                                case "base": engine.Base = (NumBase)Enum.Parse(typeof(NumBase), v); break;
                                case "rpn": rpn = v == "1"; break;
                                case "format": fmt.Format = (DisplayFormat)Enum.Parse(typeof(DisplayFormat), v); break;
                                case "digits": fmt.Digits = Math.Max(1, Math.Min(15, int.Parse(v, Inv))); break;
                                case "fixed": fmt.FixedDecimals = Math.Max(0, Math.Min(10, int.Parse(v, Inv))); break;
                                case "comma": fmt.DecimalComma = v == "1"; break;
                                case "group": fmt.Grouping = v == "1"; break;
                                case "dms": showDms = v == "1"; break;
                                case "theme": theme = v == "light" ? Theme.Light() : Theme.Dark(); break;
                                case "topmost": topMostSetting = v == "1"; break;
                                case "side": sideVisible = v == "1"; break;
                                case "varstab": showVarsTab = v == "1"; break;
                                case "max": maximized = v == "1"; break;
                                case "sci":
                                    sciVisible = v != "0";
                                    MinimumSize = new Size(S(350), sciVisible ? S(560) : S(420));
                                    break;
                                case "expr": expr = v; break;
                                case "bounds":
                                    {
                                        string[] b = v.Split(',');
                                        var r = new Rectangle(int.Parse(b[0], Inv), int.Parse(b[1], Inv), int.Parse(b[2], Inv), int.Parse(b[3], Inv));
                                        if (r.Width >= MinimumSize.Width && r.Height >= MinimumSize.Height &&
                                            Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r)))
                                        {
                                            StartPosition = FormStartPosition.Manual;
                                            Bounds = r;
                                        }
                                        break;
                                    }
                            }
                            break;
                        case "var":
                            if (f.Length >= 3) engine.Vars[f[1]] = double.Parse(f[2], NumberStyles.Float, Inv);
                            break;
                        case "fn":
                            if (f.Length >= 4)
                                engine.Funcs[f[1]] = new UserFunction
                                {
                                    Name = f[1],
                                    Params = f[2].Length == 0 ? new string[0] : f[2].Split(';'),
                                    Body = f[3]
                                };
                            break;
                        case "stk":
                            stack.Add(double.Parse(f[1], NumberStyles.Float, Inv));
                            break;
                        case "hist":
                            if (f.Length >= 4)
                                history.Insert(0, new HistItem { Expr = f[1], Value = double.Parse(f[2], NumberStyles.Float, Inv), Text = f[3] });
                            break;
                    }
                }
                catch { }
            }
            if (maximized) WindowState = FormWindowState.Maximized;
            suppressText = true;
            exprBox.Text = expr;
            exprBox.SelectionStart = exprBox.TextLength;
            suppressText = false;
        }
    }
}
