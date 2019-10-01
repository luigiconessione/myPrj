using DigitalControl.FW.Class;
using System;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    public partial class FormGestioneFormati : Form
    {

        private readonly Class.Core[] core = null;
        private readonly Class.PlcMacchinaManager plcMacchina = null;
        private readonly int id_formato;
        private readonly DBL.LinguaManager linguaMngr = null;
        private readonly object repaintLock = null;

        public FormGestioneFormati(Class.Core[] core, Class.PlcMacchinaManager plcMacchina, int id_formato, DBL.LinguaManager linguaMngr, object repaintLock)
        {
            InitializeComponent();

            this.core = core;
            this.plcMacchina = plcMacchina;
            this.id_formato = id_formato;
            this.linguaMngr = linguaMngr;
            this.repaintLock = repaintLock;

            AdjustCulture();

            DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

            string descrizioneFormato = dbmFormati.GetDescrizioneFormato(id_formato);
            lblRicettaCorrente.Text = string.Format(linguaMngr.GetTranslation("LBL_RICETTA_CORRENTE"), descrizioneFormato);

            GestioneControlliLivello();

        }

        private void GestioneControlliLivello()
        {
            if (!Properties.Settings.Default.LivelloDaCamera)
            {
                btnSetupLivello.Visible = false;
                this.Width -= btnSetupLivello.Width;
            }

            if (Properties.Settings.Default.NumeroCamereTappo == 2 && Properties.Settings.Default.LivelloDaCamera)
            {
                btnRegolazioneSlittaLivello.BackgroundImage = Properties.Resources.img_regolazione_slitta_camera_livello;
            }
            else if (Properties.Settings.Default.NumeroCamereTappo == 3 && Properties.Settings.Default.LivelloDaCamera)
            {
                btnRegolazioneSlittaLivello.BackgroundImage = Properties.Resources.img_regolazione_slitta_livello_esterno;
            }
            else if (Properties.Settings.Default.NumeroCamereTappo == 3 && !Properties.Settings.Default.LivelloDaCamera)
            {
                btnRegolazioneSlittaLivello.BackgroundImage = Properties.Resources.img_regolazione_slitta_sensore_livello;
            }
            else if (Properties.Settings.Default.PRESENZA_SMART) // |MP 11/01/2019  se sono in smart non ho la motorizzazione della "testa tipo cmtl standard" 
            {
                if (Properties.Settings.Default.NumeroCamereTappo == 1)
                {
                    btnRegolazioniServo.Visible = true;
                    btnRegolazioniServo.Enabled = true;
                    btnSetupTappo.Visible = true;
                    btnSetupTappo.Enabled = true;
                    btnSetupLivello.Visible = false;
                    btnSetupLivello.Enabled = false;
                    btnRegolazioneSlittaLivello.Visible = false;
                    btnRegolazioneSlittaLivello.Enabled = false;
                }
                else
                {
                    //btnRegolazioniServo.Visible = false;
                    //btnRegolazioniServo.Enabled = false;
                    btnSetupTappo.Visible = false;
                    btnSetupTappo.Enabled = false;
                }
            }
        }

        private void AdjustCulture()
        {
            //btnSetupTappo.Text = linguaMngr.GetTranslation("BTN_REGOLAZIONE_TAPPO");
            //btnSetupLivello.Text = linguaMngr.GetTranslation("BTN_REGOLAZIONE_LIVELLO");
            //btnRegolazioniServo.Text = linguaMngr.GetTranslation("BTN_REGOLAZIONE_SERVO");
            //btnRegolazioneCPD.Text = linguaMngr.GetTranslation("BTN_REGOLAZIONE_CPD");
        }

        private void NascondiForm()
        {
            this.Size = new System.Drawing.Size(50, 50);
            this.Location = new System.Drawing.Point((int)(Screen.PrimaryScreen.Bounds.Width / 2) - 25, (int)(Screen.PrimaryScreen.Bounds.Height / 2) - 25);
        }

        private void btnSetupTappo_Click(object sender, EventArgs e)
        {
            //NascondiForm();
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                this.plcMacchina.EnterRegolazioni();
                System.Threading.Thread.Sleep(100);

                RegolazioniVisione.FormRegolazioneControlloTappo f = null;

                lock (this.repaintLock)
                {
                    Console.WriteLine("sizeTmp.Height: {0}   sizeTmp.Width: {1}", sizeTmp.Height, sizeTmp.Width);
                    f = new RegolazioniVisione.FormRegolazioneControlloTappo(this.core[MainForm.IDX_CORE_TAPPO], this.plcMacchina, id_formato, MainForm.IDX_CORE_TAPPO, this.linguaMngr, this.repaintLock);
                }

               // NascondiForm();  // |MP posizione originale

                f.ShowDialog();
                f.Dispose();
                f = null;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                this.Size = sizeTmp;
                this.Location = locationTmp;
                Console.WriteLine("____sizeTmp.Height: {0}   sizeTmp.Width: {1}", sizeTmp.Height, sizeTmp.Width);
                timerExitRegolazioni.Start();
            }
        }

        private void btnSetupLivello_Click(object sender, EventArgs e)
        {
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                this.plcMacchina.EnterRegolazioni();
                System.Threading.Thread.Sleep(100);

                RegolazioniVisione.FormRegolazioneControlloLivello f = null;

                lock (this.repaintLock)
                {
                    f = new RegolazioniVisione.FormRegolazioneControlloLivello(this.core[MainForm.IDX_CORE_LIVELLO], this.plcMacchina, id_formato, MainForm.IDX_CORE_LIVELLO, this.linguaMngr, this.repaintLock);
                }

                NascondiForm();

                f.ShowDialog();
                f.Dispose();
                f = null;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                this.Size = sizeTmp;
                this.Location = locationTmp;

                timerExitRegolazioni.Start();
            }
        }

        private void btnRegolazioniServo_Click(object sender, EventArgs e)
        {
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                this.plcMacchina.EnterRegolazioni();
                System.Threading.Thread.Sleep(100);
                this.plcMacchina.EnterRegolazioniSlittaCamere();

                RegolazioniMeccaniche.FormGestioneMotoreTesta f;

                lock (this.repaintLock)
                {
                    f = new RegolazioniMeccaniche.FormGestioneMotoreTesta(this.plcMacchina, this.core[MainForm.IDX_CORE_TAPPO], this.id_formato, this.linguaMngr, this.repaintLock);
                }

                NascondiForm();

                f.ShowDialog();
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                this.Size = sizeTmp;
                this.Location = locationTmp;

                this.plcMacchina.ExitRegolazioniSlittaCamere();
                System.Threading.Thread.Sleep(100);

                timerExitRegolazioni.Start();
            }
        }

        private void btnRegolazioneSlittaLivello_Click(object sender, EventArgs e)
        {
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                this.plcMacchina.EnterRegolazioni();
                System.Threading.Thread.Sleep(100);
                this.plcMacchina.EnterRegolazioniSlittaLivello();
                System.Threading.Thread.Sleep(100);
                this.plcMacchina.SetLetturaRidotta(false);

                if (Properties.Settings.Default.LivelloDaCamera)
                {
                    RegolazioniMeccaniche.FormGestioneSlittaCamera f;

                    lock (this.repaintLock)
                    {
                        f = new RegolazioniMeccaniche.FormGestioneSlittaCamera(this.core[MainForm.IDX_CORE_LIVELLO], this.plcMacchina, this.id_formato, this.linguaMngr, this.repaintLock);
                    }

                    NascondiForm();

                    f.ShowDialog();
                }
                else
                {
                    RegolazioniMeccaniche.FormGestioneSlittaSensore f = new RegolazioniMeccaniche.FormGestioneSlittaSensore(this.plcMacchina, this.id_formato, this.linguaMngr);

                    NascondiForm();

                    f.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                this.Size = sizeTmp;
                this.Location = locationTmp;

                this.plcMacchina.SetLetturaRidotta(true);

                this.plcMacchina.ExitRegolazioniSlittaLivello();
                System.Threading.Thread.Sleep(100);

                timerExitRegolazioni.Start();
            }
        }

        private void btnRegolazioneCPD_Click(object sender, EventArgs e)
        {
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                RegolazioniCPD.FormRegolazioniCPD f = new RegolazioniCPD.FormRegolazioniCPD(this.plcMacchina, this.id_formato, this.linguaMngr);

                NascondiForm();

                f.ShowDialog();
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                this.Size = sizeTmp;
                this.Location = locationTmp;
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void timerExitRegolazioni_Tick(object sender, EventArgs e)
        {
            this.plcMacchina.ExitRegolazioni();

            timerExitRegolazioni.Stop();
        }

    }
}