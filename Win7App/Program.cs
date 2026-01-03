using System;
using System.Windows.Forms;

namespace Win7App
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Check if started from Windows startup (with --minimized argument)
            bool startMinimized = false;
            foreach (string arg in args)
            {
                if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startMinimized = true;
                    break;
                }
            }
            
            Application.Run(new MainForm(startMinimized));
        }
    }
}
