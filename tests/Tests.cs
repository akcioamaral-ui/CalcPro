using System;
using CalcPro;

static class Tests
{
    static int fails, total;
    static Engine e = new Engine();
    static FormatSettings fs = new FormatSettings();

    static void T(string expr, string expected)
    {
        total++;
        string got;
        try { got = Formatter.Format(e.Evaluate(expr, true).Value, e.Base, fs); }
        catch (CalcException ex) { got = "ERR:" + ex.Message; }
        bool ok = expected.StartsWith("ERR") ? got.StartsWith("ERR") : got == expected;
        if (!ok) { fails++; Console.WriteLine("FALHOU  " + expr + "  => " + got + "   (esperado " + expected + ")"); }
    }

    static void F(string actual, string expected)
    {
        total++;
        if (actual != expected) { fails++; Console.WriteLine("FALHOU fmt  " + actual + "  (esperado " + expected + ")"); }
    }

    static int Main()
    {
        fs.Grouping = false;
        T("1+2*3", "7");
        T("(1+2)*3", "9");
        T("2^3^2", "512");
        T("-2^2", "-4");
        T("2^-1", "0.5");
        T("0.1+0.2", "0.3");
        T("10/4", "2.5");
        T("1/0", "ERR");
        T("5!", "120");
        T("0.5!", "0.886226925453");
        T("sin(30)", "0.5");
        T("sin 30 + 1", "1.5");
        T("cos(90)", "0");
        T("tan(45)", "1");
        T("tan(90)", "ERR");
        T("sin(180)", "0");
        T("asin(1)", "90");
        T("2π", "6.28318530718");
        T("2(3+4)", "14");
        T("√16+2", "6");
        T("√(9)²", "9");
        T("∛-27", "-3");
        T("root(32;5)", "2");
        T("(-8)^(1/3)", "-2");
        T("3²+4²", "25");
        T("200+10%", "220");
        T("200-10%", "180");
        T("50*10%", "5");
        T("10%", "0.1");
        T("ln(e)", "1");
        T("log(1000)", "3");
        T("log(8;2)", "3");
        T("log2(1024)", "10");
        T("nCr(10;3)", "120");
        T("nPr(5;2)", "20");
        T("gcd(12;18)", "6");
        T("lcm(4;6)", "12");
        T("7 mod 3", "1");
        T("-7 mod 3", "2");
        T("max(1;5;3)", "5");
        T("avg(2;4;6)", "4");
        T("1.5E3", "1500");
        T("2e3", "2000");
        T("2e", "5.43656365692");
        T("x = 5", "5");
        T("2x+1", "11");
        T("ans*2", "22");
        T("f(a;b) = a^2+b", "Erro");
        T("f(3;1)", "10");
        T("f(2;0)+x", "9");
        T("área(r) = π r²", "Erro");
        T("área(1)", "3.14159265359");
        T("preço = 10", "10");
        T("preço*2", "20");
        T("π = 3", "ERR");
        T("((2+3)*4", "20");
        T("30°15'", "30.25");
        T("12°30'36\"", "12.51");
        T("abs(-3)", "3");
        T("round(2.555;2)", "2.56");
        T("0xFF + 0b11", "258");
        T("12 and 10", "8");
        T("12 or 3", "15");
        T("1 << 4", "16");
        T("not 0", "-1");
        T("2*", "ERR");
        T("foo", "ERR");
        T("sin(", "ERR");
        T("1e400", "ERR");

        e.Angle = AngleMode.Rad;
        T("sin(pi/2)", "1");
        T("sin(90°)", "1");
        e.Angle = AngleMode.Deg;

        e.Base = NumBase.Hex;
        T("FF + 1", "100");
        T("A*B", "6E");
        T("FF/2", "7F");
        T("not 0", "FFFFFFFFFFFFFFFF");
        T("1.5", "ERR");
        e.Base = NumBase.Bin;
        T("101 + 11", "1000");
        T("102", "ERR");
        e.Base = NumBase.Dec;

        // Formatação
        F(Formatter.FormatDec(1234567.891, 12, DisplayFormat.Normal, 4, true, false), "1,234,567.891");
        F(Formatter.FormatDec(1234567.891, 12, DisplayFormat.Normal, 4, true, true), "1.234.567,891");
        F(Formatter.FormatDec(1e20, 12, DisplayFormat.Normal, 4, true, false), "1E20");
        F(Formatter.FormatDec(0.000000123, 12, DisplayFormat.Normal, 4, true, false), "1.23E-7");
        F(Formatter.FormatDec(12345, 12, DisplayFormat.Scientific, 4, true, false), "1.2345E4");
        F(Formatter.FormatDec(12345, 12, DisplayFormat.Engineering, 4, true, false), "12.345E3");
        F(Formatter.FormatDec(Math.PI, 12, DisplayFormat.Fixed, 4, true, false), "3.1416");
        F(Formatter.FormatDec(-0.00001, 12, DisplayFormat.Fixed, 2, true, false), "0.00");
        F(Formatter.ToFraction(0.75, false), "3/4");
        F(Formatter.ToFraction(1.0 / 3, false), "1/3");
        F(Formatter.ToFraction(7.0 / 4, true), "1 3/4");
        F(Formatter.ToFraction(Math.PI, false) ?? "null", "null");
        F(Formatter.ToDms(12.51, false), "12°30′36″");
        F(Formatter.FormatBase(255, NumBase.Bin, true), "1111 1111");

        // Passo da porcentagem
        var cs = new FormatSettings { DecimalComma = true, Grouping = false };
        e.DecimalComma = true;
        F(Formatter.PercentSteps(e.Evaluate("50-3%", false), e.Base, cs), "50-(1,5)");
        F(Formatter.PercentSteps(e.Evaluate("200 + 10% - 5%", false), e.Base, cs), "200 + (20) - (11)");
        F(Formatter.PercentSteps(e.Evaluate("(100+20%)×2", false), e.Base, cs), "(100+(20))×2");
        F(Formatter.PercentSteps(e.Evaluate("y = 80 - 25%", false), e.Base, cs), "y = 80 - (20)");
        F(Formatter.PercentSteps(e.Evaluate("50×10%", false), e.Base, cs), "");
        T("200 + 10% - 5%", "209");
        e.DecimalComma = false;

        Console.WriteLine(total - fails + "/" + total + " testes OK");
        return fails == 0 ? 0 : 1;
    }
}
