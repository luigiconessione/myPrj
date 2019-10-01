using DigitalControl.FW.Class;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    public partial class FormMenuHome : Form
    {

        private readonly Class.Core[] core = null;
        private readonly Class.PlcMacchinaManager plcMacchina = null;
        private readonly DBL.LinguaManager linguaMngr = null;
        private readonly object repaintLock = null;

        private readonly Class.PasswordManager pwdManager = null;


        public FormMenuHome(Class.Core[] core, Class.PlcMacchinaManager plcMacchina, Class.PasswordManager pwdManager, DBL.LinguaManager linguaMngr, object repaintLock)
        {
            InitializeComponent();

            this.core = core;
            this.plcMacchina = plcMacchina;
            this.pwdManager = pwdManager;
            this.linguaMngr = linguaMngr;
            this.repaintLock = repaintLock;

            AdjustCulture();

            if (!Properties.Settings.Default.UsaPassword)
            {
                this.Width -= btnPassword.Width;
                btnPassword.Visible = false;
            }

        }

        private void AdjustCulture()
        {
            lblTitolo.Text = linguaMngr.GetTranslation("FORM_MENU_HOME_TITLE");
        }

        private void NascondiForm()
        {
            this.Size = new System.Drawing.Size(50, 50);
            this.Location = new System.Drawing.Point((int)(Screen.PrimaryScreen.Bounds.Width / 2) - 25, (int)(Screen.PrimaryScreen.Bounds.Height / 2) - 25);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonRicette_Click(object sender, EventArgs e)
        {
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                bool ok = this.pwdManager.CanOpen(Class.PasswordManager.LivelloPassword.Operatore, this.linguaMngr);

                if (ok)
                {
                    Formati.FormGestioneFormati f = new Formati.FormGestioneFormati(this.pwdManager, this.linguaMngr);

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
            }
        }

        private void btnFormati_Click(object sender, EventArgs e)
        {
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                DataType.ConfigurazioneCorrente confObj = DataType.ConfigurazioneCorrente.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));
                DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

                //bool esisteFormato = dbmFormati.EsisteFormato(confObj.IdFormato);  |MP 18-1-19 COMMENTATO
                if (dbmFormati.EsisteFormato(confObj.IdFormato))  //esisteFormato)   |MP 18-1-19 COMMENTATO
                {
                    //bool ok = this.pwdManager.CanOpen(Class.PasswordManager.LivelloPassword.Tecnico, this.linguaMngr);  |MP 18-1-19 COMMENTATO
                    if (this.pwdManager.CanOpen(Class.PasswordManager.LivelloPassword.Tecnico, this.linguaMngr)) //ok)  |MP 18-1-19 COMMENTATO
                    {
                        FormGestioneFormati f = new FormGestioneFormati(this.core, this.plcMacchina, confObj.IdFormato, this.linguaMngr, this.repaintLock);
                        NascondiForm();
                        f.ShowDialog();
                    }
                }
                else
                {
                    MessageBox.Show(linguaMngr.GetTranslation("MSG_FORMATO_INESISTENTE"), linguaMngr.GetTranslation("MSG_ATTENZIONE"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
            }
        }

        private void btnRegolazioniInstallazione_Click(object sender, EventArgs e)
        {
            System.Drawing.Size sizeTmp = this.Size;
            System.Drawing.Point locationTmp = this.Location;

            try
            {
                bool ok = this.pwdManager.CanOpen(Class.PasswordManager.LivelloPassword.Costruttore, this.linguaMngr, true);

                if (ok)
                {
                    RegolazioniCPD.FormRegolazioniCPDFixed f = new RegolazioniCPD.FormRegolazioniCPDFixed(this.core, this.plcMacchina, this.linguaMngr);

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
            }
        }

        private void btnStatistiche_Click(object sender, EventArgs e)
        {
            try
            {
                new FormStatistiche(this.linguaMngr).ShowDialog();
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(Properties.Settings.Default.PathTW);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnPassword_Click(object sender, EventArgs e)
        {
            try
            {
                bool ok = this.pwdManager.CanOpen(Class.PasswordManager.LivelloPassword.Operatore, this.linguaMngr);

                if (ok)
                {
                    new FormModificaPassword(this.pwdManager, this.linguaMngr).ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

    }
}