using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CalcPro
{
    public enum DisplayFormat { Normal, Scientific, Engineering, Fixed }

    public class FormatSettings
    {
        public DisplayFormat Format = DisplayFormat.Normal;
        public int Digits = 12;        // algarismos significativos
        public int FixedDecimals = 4;  // casas no modo FIX
        public bool DecimalComma = false;
        public bool Grouping = true;
    }

    public static class Formatter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Format(double v, NumBase b, FormatSettings s)
        {
            if (double.IsNaN(v)) return "Erro";
            if (double.IsPositiveInfinity(v)) return "∞";
            if (double.IsNegativeInfinity(v)) return "-∞";
            if (b != NumBase.Dec) return FormatBase(v, b, s.Grouping);
            return FormatDec(v, s.Digits, s.Format, s.FixedDecimals, s.Grouping, s.DecimalComma);
        }

        /// <summary>Texto que pode ser reinserido numa expressão (sem separador de milhar).</summary>
        public static string Plain(double v, NumBase b, FormatSettings s)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            if (b != NumBase.Dec)
            {
                long l;
                if (!TryLong(v, out l)) return "0";
                if (l < 0 && l != long.MinValue) return "-" + ToBase(-l, b);
                return ToBase(l, b);
            }
            return FormatDec(v, 15, DisplayFormat.Normal, 0, false, s.DecimalComma);
        }

        public static bool TryLong(double v, out long l)
        {
            l = 0;
            if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) >= 9.2233720368547758E18) return false;
            l = (long)Math.Truncate(v);
            return true;
        }

        public static string ToBase(long l, NumBase b)
        {
            switch (b)
            {
                case NumBase.Hex: return l.ToString("X");
                case NumBase.Oct: return Convert.ToString(l, 8);
                case NumBase.Bin: return Convert.ToString(l, 2);
                default: return l.ToString(Inv);
            }
        }

        public static string FormatBase(double v, NumBase b, bool group)
        {
            long l;
            if (!TryLong(v, out l)) return "Estouro";
            string s = ToBase(l, b);
            if (!group) return s;
            if (b == NumBase.Dec)
            {
                bool neg = s.StartsWith("-");
                if (neg) s = s.Substring(1);
                return (neg ? "-" : "") + GroupDigits(s, 3, " ");
            }
            return GroupDigits(s, b == NumBase.Oct ? 3 : 4, " ");
        }

        static string GroupDigits(string digits, int size, string sep)
        {
            if (digits.Length <= size) return digits;
            var sb = new StringBuilder();
            int first = digits.Length % size;
            if (first == 0) first = size;
            sb.Append(digits, 0, first);
            for (int i = first; i < digits.Length; i += size)
            {
                sb.Append(sep);
                sb.Append(digits, i, size);
            }
            return sb.ToString();
        }

        static string TrimZeros(string s)
        {
            if (s.IndexOf('.') < 0) return s;
            s = s.TrimEnd('0');
            if (s.EndsWith(".")) s = s.Substring(0, s.Length - 1);
            return s;
        }

        static void SplitE(double av, int digits, out string mant, out int exp)
        {
            string r = av.ToString("E" + (digits - 1), Inv);
            int ei = r.IndexOf('E');
            mant = r.Substring(0, ei);
            exp = int.Parse(r.Substring(ei + 1), Inv);
        }

        static string Sci(double av, int digits, bool eng)
        {
            string mant; int exp;
            SplitE(av, digits, out mant, out exp);
            if (eng)
            {
                int shift = ((exp % 3) + 3) % 3;
                if (shift > 0)
                {
                    double m = double.Parse(mant, Inv) * Math.Pow(10, shift);
                    exp -= shift;
                    mant = m.ToString("F" + Math.Max(0, digits - 1 - shift), Inv);
                }
            }
            mant = TrimZeros(mant);
            return exp == 0 ? mant : mant + "E" + exp.ToString(Inv);
        }

        public static string FormatDec(double v, int digits, DisplayFormat fmt, int fixedDec, bool group, bool comma)
        {
            if (double.IsNaN(v)) return "Erro";
            if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";
            if (digits < 1) digits = 1;
            if (digits > 15) digits = 15;
            if (v == 0) return "0";
            bool neg = v < 0;
            double av = Math.Abs(v);
            string body;
            switch (fmt)
            {
                case DisplayFormat.Scientific: body = Sci(av, digits, false); break;
                case DisplayFormat.Engineering: body = Sci(av, digits, true); break;
                case DisplayFormat.Fixed:
                    if (av >= 1e15) body = Sci(av, digits, false);
                    else body = av.ToString("F" + fixedDec, Inv);
                    break;
                default:
                    {
                        string mant; int exp;
                        SplitE(av, digits, out mant, out exp);
                        if (exp >= digits || exp < -6) body = Sci(av, digits, false);
                        else
                        {
                            int dec = Math.Max(0, digits - 1 - exp);
                            body = TrimZeros(av.ToString("F" + dec, Inv));
                        }
                        break;
                    }
            }
            body = Localize(body, group, comma);
            bool isZero = body.Trim('0', '.', ',') == "";
            return (neg && !isZero ? "-" : "") + body;
        }

        static string Localize(string body, bool group, bool comma)
        {
            string expPart = "";
            int ei = body.IndexOf('E');
            if (ei >= 0) { expPart = body.Substring(ei); body = body.Substring(0, ei); }
            string ip = body, fp = "";
            int di = body.IndexOf('.');
            if (di >= 0) { ip = body.Substring(0, di); fp = body.Substring(di + 1); }
            if (group) ip = GroupDigits(ip, 3, comma ? "." : ",");
            string res = ip;
            if (fp.Length > 0) res += (comma ? "," : ".") + fp;
            return res + expPart;
        }

        /// <summary>Expressão com cada "b%" de soma/subtração trocado pelo seu valor: 50-3% → 50-(1,5).</summary>
        public static string PercentSteps(EvalResult r, NumBase b, FormatSettings s)
        {
            if (r == null || r.Source == null || r.Percents.Count == 0) return "";
            var sb = new StringBuilder();
            if (r.AssignedVar != null) sb.Append(r.AssignedVar).Append(" = ");
            int pos = 0;
            foreach (var st in r.Percents.OrderBy(x => x.Start))
            {
                if (st.Start < pos || st.End > r.Source.Length) continue;
                sb.Append(r.Source, pos, st.Start - pos);
                sb.Append('(').Append(Format(st.Value, b, s)).Append(')');
                pos = st.End;
            }
            sb.Append(r.Source.Substring(pos));
            return sb.ToString().Trim();
        }

        /// <summary>Converte em fração (ou null se não houver uma fração simples).</summary>
        public static string ToFraction(double v, bool mixed)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) return null;
            if (v == Math.Floor(v)) return null;
            double x = Math.Abs(v);
            long h0 = 0, h1 = 1, k0 = 1, k1 = 0;
            double f = x;
            bool found = false;
            for (int i = 0; i < 64; i++)
            {
                double a = Math.Floor(f);
                if (a > 1e13) break;
                long ai = (long)a;
                long h2 = ai * h1 + h0, k2 = ai * k1 + k0;
                if (k2 > 100000) break;
                h0 = h1; h1 = h2; k0 = k1; k1 = k2;
                if (Math.Abs(x - (double)h1 / k1) <= 1e-12 * Math.Max(1, x)) { found = true; break; }
                double rem = f - a;
                if (rem < 1e-15) break;
                f = 1 / rem;
            }
            if (!found || k1 <= 1) return null;
            string sign = v < 0 ? "-" : "";
            if (mixed && h1 > k1)
                return sign + (h1 / k1).ToString(Inv) + " " + (h1 % k1).ToString(Inv) + "/" + k1.ToString(Inv);
            return sign + h1.ToString(Inv) + "/" + k1.ToString(Inv);
        }

        /// <summary>Graus decimais → graus, minutos, segundos.</summary>
        public static string ToDms(double deg, bool comma)
        {
            if (double.IsNaN(deg) || double.IsInfinity(deg) || Math.Abs(deg) > 1e12) return null;
            string sign = deg < 0 ? "-" : "";
            double a = Math.Abs(deg);
            long d = (long)Math.Floor(a);
            double mf = (a - d) * 60;
            long m = (long)Math.Floor(mf);
            double s = Math.Round((mf - m) * 60, 2);
            if (s >= 60) { s -= 60; m++; }
            if (m >= 60) { m -= 60; d++; }
            string ss = s.ToString("0.##", Inv);
            if (comma) ss = ss.Replace('.', ',');
            return sign + d.ToString(Inv) + "°" + m.ToString("00", Inv) + "′" + ss + "″";
        }
    }
}
