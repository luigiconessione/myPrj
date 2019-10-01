using System;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    static class Program
    {
        /// <summary>
        /// Punto di ingresso principale dell'applicazione.
        /// </summary>
        [STAThread]
        static void Main()
        {

            // se una istanza di programma è gia attiva mi chiudo
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Length > 1)
                return;

            DateTime dataValidita = DateTime.MaxValue;

#if !_Simulazione
            if (!Utilities.CommonUtility.CheckLicenza(out dataValidita))
            {
                MessageBox.Show("No license Key", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            else
#endif
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Utilities.FormAttesa formAttesa = new Utilities.FormAttesa();
                formAttesa.StartPosition = FormStartPosition.CenterScreen;
                formAttesa.Show();

                Application.DoEvents();
                MainForm formMain = new MainForm(dataValidita, formAttesa);

                Application.Run(formMain);
            }
        }
    }
}