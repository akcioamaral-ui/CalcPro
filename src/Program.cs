using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CalcPro
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);

        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "CalcPro_SingleInstance_Mutex", out created))
            {
                if (!created)
                {
                    // Já está aberto: traz a janela existente para frente
                    var me = Process.GetCurrentProcess();
                    foreach (var p in Process.GetProcessesByName(me.ProcessName))
                    {
                        if (p.Id == me.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                        if (IsIconic(p.MainWindowHandle)) ShowWindow(p.MainWindowHandle, 9); // SW_RESTORE
                        SetForegroundWindow(p.MainWindowHandle);
                        return;
                    }
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) =>
                    MessageBox.Show("Erro inesperado:\n" + e.Exception.Message, "CalcPro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Run(new MainForm());
            }
        }
    }
}
