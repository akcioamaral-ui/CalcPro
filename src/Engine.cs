using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CalcPro
{
    public enum AngleMode { Deg, Rad, Grad }
    public enum NumBase { Dec = 10, Hex = 16, Oct = 8, Bin = 2 }

    public class CalcException : Exception
    {
        public CalcException(string msg) : base(msg) { }
    }

    /// <summary>Erro matemático (domínio, divisão por zero...). A sintaxe está correta.</summary>
    public class MathDomainException : CalcException
    {
        public MathDomainException(string msg) : base(msg) { }
    }

    public class UserFunction
    {
        public string Name;
        public string[] Params;
        public string Body;
        public string Signature { get { return Name + "(" + string.Join("; ", Params) + ")"; } }
    }

    /// <summary>Trecho "b%" de uma soma/subtração e o valor que ele representa (a·b/100).</summary>
    public class PercentStep
    {
        public int Start, End;
        public double Value;
    }

    public class EvalResult
    {
        public double Value = double.NaN;
        public string AssignedVar;
        public UserFunction DefinedFunction;
        public string Source;                 // texto avaliado (lado direito, se atribuição)
        public List<PercentStep> Percents = new List<PercentStep>();
    }

    internal enum TokType { Num, Ident, Op, LParen, RParen, Comma, End }

    internal class Token
    {
        public TokType Type;
        public string Text;
        public double Value;
        public int Pos, End;
        public Token(TokType t, string text, double v) { Type = t; Text = text; Value = v; }
    }

    public class Engine
    {
        public AngleMode Angle = AngleMode.Deg;
        public NumBase Base = NumBase.Dec;
        public bool DecimalComma = false;

        public readonly Dictionary<string, double> Vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, UserFunction> Funcs = new Dictionary<string, UserFunction>(StringComparer.OrdinalIgnoreCase);

        static readonly Random Rng = new Random();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "mod", "and", "or", "xor", "not", "shl", "shr" };

        static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "pi", "e", "phi", "rand", "ans", "π", "φ" };

        public static readonly string[] BuiltinNames = {
            "sin","cos","tan","cot","sec","csc","asin","acos","atan","acot","atan2",
            "sinh","cosh","tanh","asinh","acosh","atanh",
            "ln","log","log2","exp","sqrt","cbrt","root","pow","abs","floor","ceil","round","trunc","int","frac","sign",
            "fact","gamma","nPr","nCr","gcd","lcm","min","max","sum","avg","mod","hypot","rand","randint","todeg","torad"
        };
        static readonly HashSet<string> BuiltinSet = new HashSet<string>(BuiltinNames, StringComparer.OrdinalIgnoreCase);

        static readonly Regex AssignRx = new Regex(@"^\s*([\p{L}_][\p{L}\p{N}_]*)\s*(\(\s*([^()=]*)\))?\s*=(.*)$");
        static readonly Regex IdentRx = new Regex(@"^[\p{L}_][\p{L}\p{N}_]*$");

        List<Token> toks;
        int p;
        Dictionary<string, double> locals;
        int depth;
        bool recording;
        List<PercentStep> pctSteps = new List<PercentStep>();

        public Engine()
        {
            Vars["ans"] = 0;
        }

        public static bool IsBuiltin(string name) { return BuiltinSet.Contains(name); }

        // ------------------------------------------------------------------ API

        /// <summary>Avalia uma linha. commit=false não altera variáveis (pré-visualização).</summary>
        public EvalResult Evaluate(string text, bool commit)
        {
            if (text == null) text = "";
            text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (text.EndsWith("=") && !text.EndsWith("==")) text = text.Substring(0, text.Length - 1).Trim();
            if (text.Length == 0) throw new CalcException("Expressão vazia");

            var res = new EvalResult();
            Match m = AssignRx.Match(text);
            if (m.Success)
            {
                string name = m.Groups[1].Value;
                string body = m.Groups[4].Value.Trim();
                if (body.Length == 0) throw new CalcException("Falta o valor após '='");
                CheckName(name);
                if (m.Groups[2].Success)
                {
                    string raw = m.Groups[3].Value.Trim();
                    string[] ps = raw.Length == 0 ? new string[0] : raw.Split(new[] { ',', ';' });
                    for (int i = 0; i < ps.Length; i++)
                    {
                        ps[i] = ps[i].Trim();
                        if (!IdentRx.IsMatch(ps[i])) throw new CalcException("Parâmetro inválido: '" + ps[i] + "'");
                    }
                    var uf = new UserFunction { Name = name, Params = ps, Body = body };
                    // valida o corpo com parâmetros = 1
                    var loc = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    foreach (string pn in ps) loc[pn] = 1;
                    try { EvalExpr(body, loc); }
                    catch (MathDomainException) { }
                    if (commit) Funcs[name] = uf;
                    res.DefinedFunction = uf;
                    return res;
                }
                pctSteps = res.Percents;
                double v = EvalExpr(body, null, true);
                if (commit) { Vars[name] = v; Vars["ans"] = v; }
                res.Value = v;
                res.AssignedVar = name;
                res.Source = body;
                return res;
            }

            pctSteps = res.Percents;
            res.Source = text;
            res.Value = EvalExpr(text, null, true);
            if (commit) Vars["ans"] = res.Value;
            return res;
        }

        void CheckName(string name)
        {
            if (Reserved.Contains(name)) throw new CalcException("'" + name + "' é um nome reservado");
            if (Keywords.Contains(name)) throw new CalcException("'" + name + "' é um operador");
            if (BuiltinSet.Contains(name)) throw new CalcException("'" + name + "' é uma função interna");
            if (Base == NumBase.Hex && IsAllHex(name)) throw new CalcException("Em HEX, '" + name + "' é um número");
        }

        public double EvalExpr(string s, Dictionary<string, double> loc)
        {
            return EvalExpr(s, loc, false);
        }

        double EvalExpr(string s, Dictionary<string, double> loc, bool record)
        {
            var sToks = toks; int sP = p; var sLoc = locals; bool sRec = recording;
            try
            {
                toks = Tokenize(s);
                p = 0;
                locals = loc;
                recording = record;
                double v = ParseExpr();
                if (Peek.Type != TokType.End)
                {
                    if (Peek.Type == TokType.RParen) throw new CalcException("')' sem '(' correspondente");
                    throw new CalcException("Símbolo inesperado: '" + Peek.Text + "'");
                }
                return Check(v);
            }
            finally { toks = sToks; p = sP; locals = sLoc; recording = sRec; }
        }

        // ------------------------------------------------------------- Tokenizer

        static bool IsAllHex(string w)
        {
            foreach (char c in w) if (HexVal(c) < 0) return false;
            return w.Length > 0;
        }

        static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        static double ParseRadix(string digits, int radix)
        {
            double v = 0;
            foreach (char c in digits)
            {
                int d = HexVal(c);
                if (d < 0 || d >= radix) throw new CalcException("Dígito '" + c + "' inválido na base " + radix);
                v = v * radix + d;
            }
            return v;
        }

        List<Token> Tokenize(string s)
        {
            var list = new List<Token>();
            int i = 0, n = s.Length;
            int tokStart = 0, fixedCount = 0;
            while (true)
            {
                // registra a posição dos tokens criados na iteração anterior
                for (int k = fixedCount; k < list.Count; k++) { list[k].Pos = tokStart; list[k].End = i; }
                fixedCount = list.Count;
                tokStart = i;
                if (i >= n) break;
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                tokStart = i;

                bool decSep = c == '.' || (DecimalComma && c == ',');
                if (char.IsDigit(c) || (decSep && i + 1 < n && char.IsDigit(s[i + 1])))
                {
                    // prefixos 0x / 0b / 0o
                    if (c == '0' && i + 2 < n && "xXbBoO".IndexOf(s[i + 1]) >= 0 && HexVal(s[i + 2]) >= 0)
                    {
                        char pc = char.ToLowerInvariant(s[i + 1]);
                        int radix = pc == 'x' ? 16 : pc == 'b' ? 2 : 8;
                        int st = i + 2, j = st;
                        while (j < n && char.IsLetterOrDigit(s[j])) j++;
                        list.Add(new Token(TokType.Num, s.Substring(i, j - i), ParseRadix(s.Substring(st, j - st), radix)));
                        i = j;
                        continue;
                    }
                    if (Base != NumBase.Dec)
                    {
                        int radix = (int)Base;
                        int j = i;
                        while (j < n && (char.IsDigit(s[j]) || (radix == 16 && HexVal(s[j]) >= 0))) j++;
                        if (j < n && (s[j] == '.' || (DecimalComma && s[j] == ',' && j + 1 < n && char.IsDigit(s[j + 1]))))
                            throw new CalcException("Bases não decimais aceitam apenas inteiros");
                        list.Add(new Token(TokType.Num, s.Substring(i, j - i), ParseRadix(s.Substring(i, j - i), radix)));
                        i = j;
                        continue;
                    }
                    var sb = new StringBuilder();
                    while (i < n && char.IsDigit(s[i])) sb.Append(s[i++]);
                    if (i < n && (s[i] == '.' || (DecimalComma && s[i] == ',')) && i + 1 < n && char.IsDigit(s[i + 1]))
                    {
                        sb.Append('.'); i++;
                        while (i < n && char.IsDigit(s[i])) sb.Append(s[i++]);
                    }
                    else if (i < n && s[i] == '.' ) { i++; } // "5." é aceito
                    if (i < n && (s[i] == 'E' || s[i] == 'e'))
                    {
                        int j = i + 1;
                        if (j < n && (s[j] == '+' || s[j] == '-' || s[j] == '−')) j++;
                        if (j < n && char.IsDigit(s[j]))
                        {
                            sb.Append('E');
                            if (s[i + 1] == '-' || s[i + 1] == '−') sb.Append('-');
                            i = j;
                            while (i < n && char.IsDigit(s[i])) sb.Append(s[i++]);
                        }
                    }
                    string num = sb.ToString();
                    double val;
                    if (!double.TryParse(num, NumberStyles.Float, Inv, out val))
                        throw new CalcException("Número inválido: " + num);
                    list.Add(new Token(TokType.Num, num, val));
                    continue;
                }

                if (c == 'π') { list.Add(new Token(TokType.Ident, "pi", 0)); i++; continue; }
                if (c == 'φ') { list.Add(new Token(TokType.Ident, "phi", 0)); i++; continue; }

                if (char.IsLetter(c) || c == '_')
                {
                    int j = i;
                    while (j < n && (char.IsLetterOrDigit(s[j]) || s[j] == '_') && s[j] != 'π' && s[j] != 'φ') j++;
                    string w = s.Substring(i, j - i);
                    i = j;
                    if (Base == NumBase.Hex && IsAllHex(w))
                        list.Add(new Token(TokType.Num, w, ParseRadix(w, 16)));
                    else
                        list.Add(new Token(TokType.Ident, w, 0));
                    continue;
                }

                // operadores de dois caracteres
                if (i + 1 < n)
                {
                    string two = s.Substring(i, 2);
                    string op2 = null;
                    if (two == "<<") op2 = "<<";
                    else if (two == ">>") op2 = ">>";
                    else if (two == "**") op2 = "^";
                    else if (two == "⁻¹") op2 = "⁻¹";
                    if (op2 != null) { list.Add(new Token(TokType.Op, op2, 0)); i += 2; continue; }
                }

                i++;
                switch (c)
                {
                    case '+': list.Add(new Token(TokType.Op, "+", 0)); break;
                    case '-': case '−': case '–': list.Add(new Token(TokType.Op, "-", 0)); break;
                    case '*': case '×': case '·': case '∙': list.Add(new Token(TokType.Op, "*", 0)); break;
                    case '/': case '÷': list.Add(new Token(TokType.Op, "/", 0)); break;
                    case '^': list.Add(new Token(TokType.Op, "^", 0)); break;
                    case '!': list.Add(new Token(TokType.Op, "!", 0)); break;
                    case '%': list.Add(new Token(TokType.Op, "%", 0)); break;
                    case '²': list.Add(new Token(TokType.Op, "²", 0)); break;
                    case '³': list.Add(new Token(TokType.Op, "³", 0)); break;
                    case '°': case 'º': list.Add(new Token(TokType.Op, "°", 0)); break;
                    case '\'': case '′': list.Add(new Token(TokType.Op, "'", 0)); break;
                    case '"': case '″': list.Add(new Token(TokType.Op, "\"", 0)); break;
                    case '√': list.Add(new Token(TokType.Op, "√", 0)); break;
                    case '∛': list.Add(new Token(TokType.Op, "∛", 0)); break;
                    case '&': list.Add(new Token(TokType.Op, "and", 0)); break;
                    case '|': list.Add(new Token(TokType.Op, "or", 0)); break;
                    case '~': list.Add(new Token(TokType.Op, "not", 0)); break;
                    case '(': case '[': case '{': list.Add(new Token(TokType.LParen, "(", 0)); break;
                    case ')': case ']': case '}': list.Add(new Token(TokType.RParen, ")", 0)); break;
                    case ';': case ',': list.Add(new Token(TokType.Comma, ";", 0)); break;
                    case '=': throw new CalcException("'=' só pode ser usado para definir variável: x = 5");
                    default: throw new CalcException("Caractere inválido: '" + c + "'");
                }
            }
            list.Add(new Token(TokType.End, "fim", 0) { Pos = n, End = n });
            return list;
        }

        // ---------------------------------------------------------------- Parser

        Token Peek { get { return toks[p]; } }

        bool Accept(string word)
        {
            Token t = Peek;
            if ((t.Type == TokType.Op || t.Type == TokType.Ident) && string.Equals(t.Text, word, StringComparison.OrdinalIgnoreCase))
            {
                p++;
                return true;
            }
            return false;
        }

        bool IsOp(string op) { return Peek.Type == TokType.Op && Peek.Text == op; }

        void ExpectRParen()
        {
            if (Peek.Type == TokType.RParen) { p++; return; }
            if (Peek.Type == TokType.End) return; // fecha parênteses automaticamente
            throw new CalcException("Esperado ')' antes de '" + Peek.Text + "'");
        }

        double ParseExpr() { return ParseOr(); }

        double ParseOr()
        {
            double v = ParseXor();
            while (Accept("or")) v = ApplyOperator("or", v, ParseXor());
            return v;
        }

        double ParseXor()
        {
            double v = ParseAnd();
            while (Accept("xor")) v = ApplyOperator("xor", v, ParseAnd());
            return v;
        }

        double ParseAnd()
        {
            double v = ParseShift();
            while (Accept("and")) v = ApplyOperator("and", v, ParseShift());
            return v;
        }

        double ParseShift()
        {
            double v = ParseAdd();
            while (true)
            {
                if (Accept("<<") || Accept("shl")) v = ApplyOperator("<<", v, ParseAdd());
                else if (Accept(">>") || Accept("shr")) v = ApplyOperator(">>", v, ParseAdd());
                else return v;
            }
        }

        double ParseAdd()
        {
            bool pct;
            double v = ParseMul(out pct);
            while (IsOp("+") || IsOp("-"))
            {
                string op = Peek.Text; p++;
                bool rp;
                int st = p;
                double r = ParseMul(out rp);
                if (rp)
                {
                    r = v * r; // 200 + 10% = 220
                    if (recording && p > st)
                        pctSteps.Add(new PercentStep { Start = toks[st].Pos, End = toks[p - 1].End, Value = r });
                }
                v = ApplyOperator(op, v, r);
            }
            return v;
        }

        bool StartsImplicit()
        {
            Token t = Peek;
            if (t.Type == TokType.Num || t.Type == TokType.LParen) return true;
            if (t.Type == TokType.Ident) return !Keywords.Contains(t.Text);
            if (t.Type == TokType.Op) return t.Text == "√" || t.Text == "∛";
            return false;
        }

        double ParseMul(out bool pct)
        {
            double v = ParseUnary(out pct);
            while (true)
            {
                bool dummy;
                if (IsOp("*")) { p++; v = ApplyOperator("*", v, ParseUnary(out dummy)); pct = false; }
                else if (IsOp("/")) { p++; v = ApplyOperator("/", v, ParseUnary(out dummy)); pct = false; }
                else if (Accept("mod")) { v = ApplyOperator("mod", v, ParseUnary(out dummy)); pct = false; }
                else if (StartsImplicit()) { v = ApplyOperator("*", v, ParseUnary(out dummy)); pct = false; }
                else return v;
            }
        }

        double ParseUnary(out bool pct)
        {
            if (IsOp("-")) { p++; double v = ParseUnary(out pct); return -v; }
            if (IsOp("+")) { p++; return ParseUnary(out pct); }
            if (Accept("not")) { double v = ParseUnary(out pct); pct = false; return ApplyUnary("not", v); }
            return ParsePower(out pct);
        }

        double ParsePower(out bool pct)
        {
            double b = ParsePostfix(out pct);
            if (IsOp("^"))
            {
                p++;
                bool ep;
                double e = ParseUnary(out ep);
                pct = false;
                return ApplyOperator("^", b, e);
            }
            return b;
        }

        double ParsePostfix(out bool pct)
        {
            double v = ParsePrimary();
            pct = false;
            while (true)
            {
                if (IsOp("!")) { p++; v = Factorial(v); pct = false; }
                else if (IsOp("²")) { p++; v = v * v; pct = false; }
                else if (IsOp("³")) { p++; v = v * v * v; pct = false; }
                else if (IsOp("⁻¹")) { p++; v = ApplyUnary("inv", v); pct = false; }
                else if (IsOp("%")) { p++; v = v / 100.0; pct = true; }
                else return v;
            }
        }

        double ParsePrimary()
        {
            Token t = Peek;
            switch (t.Type)
            {
                case TokType.Num:
                    {
                        p++;
                        double v = t.Value;
                        if (IsOp("°"))
                        {
                            p++;
                            if (Peek.Type == TokType.Num && toks[p + 1].Type == TokType.Op && toks[p + 1].Text == "'")
                            {
                                v += Math.Sign(v == 0 ? 1 : v) * Peek.Value / 60.0; p += 2;
                            }
                            if (Peek.Type == TokType.Num && toks[p + 1].Type == TokType.Op && toks[p + 1].Text == "\"")
                            {
                                v += Math.Sign(v == 0 ? 1 : v) * Peek.Value / 3600.0; p += 2;
                            }
                            v = FromDegrees(v);
                        }
                        return v;
                    }
                case TokType.LParen:
                    {
                        p++;
                        if (Peek.Type == TokType.RParen) throw new CalcException("Parênteses vazios");
                        double v = ParseExpr();
                        ExpectRParen();
                        if (IsOp("°")) { p++; v = FromDegrees(v); }
                        return v;
                    }
                case TokType.Op:
                    if (t.Text == "√" || t.Text == "∛")
                    {
                        p++;
                        bool d, neg = false;
                        if (IsOp("-")) { p++; neg = true; }
                        double arg = ParsePostfix(out d);
                        return CallFunction(t.Text == "√" ? "sqrt" : "cbrt", new[] { neg ? -arg : arg });
                    }
                    throw new CalcException("Operador '" + t.Text + "' fora de lugar");
                case TokType.Ident:
                    {
                        p++;
                        string name = t.Text;
                        if (Keywords.Contains(name)) throw new CalcException("Operador '" + name + "' fora de lugar");
                        if (Peek.Type == TokType.LParen)
                        {
                            p++;
                            var args = new List<double>();
                            if (Peek.Type == TokType.RParen) p++;
                            else
                            {
                                do { args.Add(ParseExpr()); }
                                while (Peek.Type == TokType.Comma && ++p > 0);
                                ExpectRParen();
                            }
                            return CallFunction(name, args.ToArray());
                        }
                        double val;
                        if (locals != null && locals.TryGetValue(name, out val)) return val;
                        string ln = name.ToLowerInvariant();
                        if (ln == "pi") return Math.PI;
                        if (ln == "e") return Math.E;
                        if (ln == "phi") return (1 + Math.Sqrt(5)) / 2;
                        if (ln == "rand") return Rng.NextDouble();
                        if (Vars.TryGetValue(name, out val)) return val;
                        // função sem parênteses: "sin 30", "ln 2"
                        if (BuiltinSet.Contains(name) || Funcs.ContainsKey(name))
                        {
                            if (Peek.Type == TokType.End) throw new CalcException("Falta o argumento de " + name);
                            bool d;
                            return CallFunction(name, new[] { ParseUnary(out d) });
                        }
                        throw new CalcException("Variável desconhecida: " + name);
                    }
                case TokType.End:
                    throw new CalcException("Expressão incompleta");
                default:
                    throw new CalcException("Símbolo inesperado: '" + t.Text + "'");
            }
        }

        // ------------------------------------------------------------ Operações

        static double Check(double v)
        {
            if (double.IsNaN(v)) throw new MathDomainException("Resultado indefinido");
            if (double.IsInfinity(v)) throw new MathDomainException("Estouro: resultado muito grande");
            return v;
        }

        static long ToLong(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) >= 9.2233720368547758E18)
                throw new MathDomainException("Valor fora do intervalo inteiro de 64 bits");
            return (long)Math.Truncate(v);
        }

        static long ToInt(double v, string fn)
        {
            if (v != Math.Floor(v)) throw new MathDomainException(fn + " requer números inteiros");
            return ToLong(v);
        }

        public double ApplyOperator(string op, double a, double b)
        {
            switch (op)
            {
                case "+": return a + b;
                case "-": return a - b;
                case "*": return a * b;
                case "/":
                    if (b == 0) throw new MathDomainException("Divisão por zero");
                    return Base == NumBase.Dec ? a / b : Math.Truncate(a / b);
                case "^": return Pow(a, b);
                case "mod":
                    if (b == 0) throw new MathDomainException("Divisão por zero (mod)");
                    return a - b * Math.Floor(a / b);
                case "and": return ToLong(a) & ToLong(b);
                case "or": return ToLong(a) | ToLong(b);
                case "xor": return ToLong(a) ^ ToLong(b);
                case "<<": return (double)(ToLong(a) << (int)Math.Min(63, Math.Max(0, ToLong(b))));
                case ">>": return (double)(ToLong(a) >> (int)Math.Min(63, Math.Max(0, ToLong(b))));
                case "root": return CallFunction("root", new[] { a, b });
                default: return CallFunction(op, new[] { a, b });
            }
        }

        public double ApplyUnary(string op, double x)
        {
            switch (op)
            {
                case "neg": return -x;
                case "sqr": return x * x;
                case "cube": return x * x * x;
                case "inv":
                    if (x == 0) throw new MathDomainException("Divisão por zero");
                    return 1 / x;
                case "pct": return x / 100;
                case "pow10": return Math.Pow(10, x);
                case "not": return ~ToLong(x);
                default: return CallFunction(op, new[] { x });
            }
        }

        static double Pow(double b, double e)
        {
            if (b < 0 && e != Math.Floor(e))
            {
                double inv = 1 / e;
                double ri = Math.Round(inv);
                if (Math.Abs(inv - ri) < 1e-9 && ((long)ri) % 2 != 0)
                    return -Math.Pow(-b, e);
                throw new MathDomainException("Potência fracionária de número negativo");
            }
            if (b == 0 && e < 0) throw new MathDomainException("Divisão por zero");
            return Math.Pow(b, e);
        }

        double ToRad(double x)
        {
            switch (Angle)
            {
                case AngleMode.Deg: return (x % 360) * Math.PI / 180;
                case AngleMode.Grad: return (x % 400) * Math.PI / 200;
                default: return x;
            }
        }

        double FromRad(double r)
        {
            switch (Angle)
            {
                case AngleMode.Deg: return r * 180 / Math.PI;
                case AngleMode.Grad: return r * 200 / Math.PI;
                default: return r;
            }
        }

        double FromDegrees(double deg)
        {
            switch (Angle)
            {
                case AngleMode.Rad: return deg * Math.PI / 180;
                case AngleMode.Grad: return deg * 400 / 360;
                default: return deg;
            }
        }

        static double Clean(double r) { return Math.Abs(r) < 1e-14 ? 0 : r; }

        static void Need(double[] a, int n, string fn)
        {
            if (a.Length != n)
                throw new CalcException(fn + " requer " + n + (n == 1 ? " argumento" : " argumentos"));
        }

        static void NeedAtLeast(double[] a, int n, string fn)
        {
            if (a.Length < n) throw new CalcException(fn + " requer pelo menos " + n + " argumento(s)");
        }

        public double CallFunction(string name, double[] a)
        {
            string n = name.ToLowerInvariant();
            double x = a.Length > 0 ? a[0] : 0;
            switch (n)
            {
                case "sin": Need(a, 1, n); return Clean(Math.Sin(ToRad(x)));
                case "cos": Need(a, 1, n); return Clean(Math.Cos(ToRad(x)));
                case "tan":
                    {
                        Need(a, 1, n);
                        double r = ToRad(x), c = Clean(Math.Cos(r));
                        if (c == 0) throw new MathDomainException("tan indefinida neste ângulo");
                        return Clean(Math.Sin(r)) / c;
                    }
                case "cot":
                    {
                        Need(a, 1, n);
                        double r = ToRad(x), s = Clean(Math.Sin(r));
                        if (s == 0) throw new MathDomainException("cot indefinida neste ângulo");
                        return Clean(Math.Cos(r)) / s;
                    }
                case "sec":
                    {
                        Need(a, 1, n);
                        double c = Clean(Math.Cos(ToRad(x)));
                        if (c == 0) throw new MathDomainException("sec indefinida neste ângulo");
                        return 1 / c;
                    }
                case "csc":
                    {
                        Need(a, 1, n);
                        double s = Clean(Math.Sin(ToRad(x)));
                        if (s == 0) throw new MathDomainException("csc indefinida neste ângulo");
                        return 1 / s;
                    }
                case "asin":
                    Need(a, 1, n);
                    if (x < -1 || x > 1) throw new MathDomainException("asin: argumento fora de [-1; 1]");
                    return FromRad(Math.Asin(x));
                case "acos":
                    Need(a, 1, n);
                    if (x < -1 || x > 1) throw new MathDomainException("acos: argumento fora de [-1; 1]");
                    return FromRad(Math.Acos(x));
                case "atan": Need(a, 1, n); return FromRad(Math.Atan(x));
                case "acot": Need(a, 1, n); return FromRad(x == 0 ? Math.PI / 2 : Math.Atan(1 / x));
                case "atan2": Need(a, 2, n); return FromRad(Math.Atan2(a[0], a[1]));
                case "sinh": Need(a, 1, n); return Math.Sinh(x);
                case "cosh": Need(a, 1, n); return Math.Cosh(x);
                case "tanh": Need(a, 1, n); return Math.Tanh(x);
                case "asinh": Need(a, 1, n); return Math.Log(x + Math.Sqrt(x * x + 1));
                case "acosh":
                    Need(a, 1, n);
                    if (x < 1) throw new MathDomainException("acosh: argumento menor que 1");
                    return Math.Log(x + Math.Sqrt(x * x - 1));
                case "atanh":
                    Need(a, 1, n);
                    if (x <= -1 || x >= 1) throw new MathDomainException("atanh: argumento fora de (-1; 1)");
                    return 0.5 * Math.Log((1 + x) / (1 - x));
                case "ln":
                    Need(a, 1, n);
                    if (x <= 0) throw new MathDomainException("ln de número não positivo");
                    return Math.Log(x);
                case "log":
                    if (a.Length == 2)
                    {
                        if (a[0] <= 0 || a[1] <= 0 || a[1] == 1) throw new MathDomainException("log: argumentos inválidos");
                        return Math.Log(a[0]) / Math.Log(a[1]);
                    }
                    Need(a, 1, n);
                    if (x <= 0) throw new MathDomainException("log de número não positivo");
                    return Math.Log10(x);
                case "log2":
                    Need(a, 1, n);
                    if (x <= 0) throw new MathDomainException("log2 de número não positivo");
                    return Math.Log(x, 2);
                case "exp": Need(a, 1, n); return Math.Exp(x);
                case "sqrt":
                    Need(a, 1, n);
                    if (x < 0) throw new MathDomainException("Raiz quadrada de número negativo");
                    return Math.Sqrt(x);
                case "cbrt": Need(a, 1, n); return x < 0 ? -Math.Pow(-x, 1.0 / 3) : Math.Pow(x, 1.0 / 3);
                case "root":
                    {
                        Need(a, 2, n);
                        double k = a[1];
                        if (k == 0) throw new MathDomainException("Raiz de índice zero");
                        if (x < 0)
                        {
                            if (k == Math.Floor(k) && ((long)k) % 2 != 0) return -Math.Pow(-x, 1 / k);
                            throw new MathDomainException("Raiz par de número negativo");
                        }
                        double r = Math.Pow(x, 1 / k);
                        double rr = Math.Round(r);
                        if (rr != 0 && Math.Abs(Math.Pow(rr, k) - x) < 1e-9 * Math.Max(1, Math.Abs(x))) return rr;
                        return r;
                    }
                case "pow": Need(a, 2, n); return Pow(a[0], a[1]);
                case "abs": Need(a, 1, n); return Math.Abs(x);
                case "floor": Need(a, 1, n); return Math.Floor(x);
                case "ceil": Need(a, 1, n); return Math.Ceiling(x);
                case "round":
                    {
                        if (a.Length == 2)
                        {
                            int d = (int)Math.Max(0, Math.Min(15, a[1]));
                            return Math.Round(x, d, MidpointRounding.AwayFromZero);
                        }
                        Need(a, 1, n);
                        return Math.Round(x, MidpointRounding.AwayFromZero);
                    }
                case "trunc": case "int": Need(a, 1, n); return Math.Truncate(x);
                case "frac": Need(a, 1, n); return x - Math.Truncate(x);
                case "sign": Need(a, 1, n); return Math.Sign(x);
                case "fact": Need(a, 1, n); return Factorial(x);
                case "gamma": Need(a, 1, n); return Gamma(x);
                case "npr":
                    {
                        Need(a, 2, "nPr");
                        long nn = ToInt(a[0], "nPr"), r = ToInt(a[1], "nPr");
                        if (r < 0 || nn < 0 || r > nn) throw new MathDomainException("nPr: requer 0 ≤ r ≤ n");
                        double res = 1;
                        for (long i = nn - r + 1; i <= nn; i++) res *= i;
                        return res;
                    }
                case "ncr":
                    {
                        Need(a, 2, "nCr");
                        long nn = ToInt(a[0], "nCr"), r = ToInt(a[1], "nCr");
                        if (r < 0 || nn < 0 || r > nn) throw new MathDomainException("nCr: requer 0 ≤ r ≤ n");
                        r = Math.Min(r, nn - r);
                        double res = 1;
                        for (long i = 1; i <= r; i++) res = res * (nn - r + i) / i;
                        return Math.Round(res);
                    }
                case "gcd":
                    {
                        NeedAtLeast(a, 2, n);
                        long g = Math.Abs(ToInt(a[0], n));
                        for (int i = 1; i < a.Length; i++) g = Gcd(g, Math.Abs(ToInt(a[i], n)));
                        return g;
                    }
                case "lcm":
                    {
                        NeedAtLeast(a, 2, n);
                        double l = Math.Abs(ToInt(a[0], n));
                        for (int i = 1; i < a.Length; i++)
                        {
                            long b = Math.Abs(ToInt(a[i], n));
                            if (l == 0 || b == 0) return 0;
                            l = l / Gcd((long)l, b) * b;
                        }
                        return l;
                    }
                case "min": { NeedAtLeast(a, 1, n); double r = a[0]; foreach (double v in a) r = Math.Min(r, v); return r; }
                case "max": { NeedAtLeast(a, 1, n); double r = a[0]; foreach (double v in a) r = Math.Max(r, v); return r; }
                case "sum": { double r = 0; foreach (double v in a) r += v; return r; }
                case "avg": { NeedAtLeast(a, 1, n); double r = 0; foreach (double v in a) r += v; return r / a.Length; }
                case "mod": Need(a, 2, n); return ApplyOperator("mod", a[0], a[1]);
                case "hypot": Need(a, 2, n); return Math.Sqrt(a[0] * a[0] + a[1] * a[1]);
                case "rand": Need(a, 0, n); return Rng.NextDouble();
                case "randint":
                    {
                        Need(a, 2, n);
                        long lo = ToInt(a[0], n), hi = ToInt(a[1], n);
                        if (hi < lo) { long t = lo; lo = hi; hi = t; }
                        return lo + Math.Floor(Rng.NextDouble() * (hi - lo + 1));
                    }
                case "todeg": Need(a, 1, n); return x * 180 / Math.PI;
                case "torad": Need(a, 1, n); return x * Math.PI / 180;
            }

            UserFunction uf;
            if (Funcs.TryGetValue(name, out uf))
            {
                if (a.Length != uf.Params.Length)
                    throw new CalcException(uf.Name + " requer " + uf.Params.Length + " argumento(s)");
                if (depth > 64) throw new CalcException("Recursão excessiva em " + uf.Name);
                var loc = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < a.Length; i++) loc[uf.Params[i]] = a[i];
                depth++;
                try { return EvalExpr(uf.Body, loc); }
                finally { depth--; }
            }
            throw new CalcException("Função desconhecida: " + name);
        }

        static long Gcd(long a, long b)
        {
            while (b != 0) { long t = a % b; a = b; b = t; }
            return a;
        }

        static double Factorial(double x)
        {
            if (x == Math.Floor(x))
            {
                if (x < 0) throw new MathDomainException("Fatorial de inteiro negativo");
                if (x > 170) throw new MathDomainException("Estouro: fatorial muito grande");
                double r = 1;
                for (int i = 2; i <= (int)x; i++) r *= i;
                return r;
            }
            return Gamma(x + 1);
        }

        static readonly double[] Lg = {
            0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313,
            -176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7 };

        static double Gamma(double x)
        {
            if (x == Math.Floor(x) && x <= 0) throw new MathDomainException("Gama indefinida em inteiros ≤ 0");
            if (x < 0.5) return Math.PI / (Math.Sin(Math.PI * x) * Gamma(1 - x));
            if (x == Math.Floor(x) && x <= 171) return Factorial(x - 1);
            x -= 1;
            double s = Lg[0];
            double t = x + 7.5;
            for (int i = 1; i < 9; i++) s += Lg[i] / (x + i);
            return Math.Sqrt(2 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * s;
        }
    }
}
