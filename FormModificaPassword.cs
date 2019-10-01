using DigitalControl.FW.Class;
using System;
using System.IO;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    public partial class FormModificaPassword : Form
    {

        private readonly Class.PasswordManager pwdManager = null;
        private readonly DBL.LinguaManager linguaMngr = null;

        public FormModificaPassword(Class.PasswordManager pwdManager, DBL.LinguaManager linguaMngr)
        {
            InitializeComponent();

            this.pwdManager = pwdManager;
            this.linguaMngr = linguaMngr;

            Class.PasswordManager.LivelloPassword lastLivello = this.pwdManager.GetLastLivello();

            rbLivelloTecnico.Enabled = lastLivello >= Class.PasswordManager.LivelloPassword.Tecnico;
            rbLivelloCostruttore.Enabled = lastLivello >= Class.PasswordManager.LivelloPassword.Costruttore;

            AdjustCulture();
        }

        private void AdjustCulture()
        {
            lblDescrizione.Text = this.linguaMngr.GetTranslation("LBL_MODIFICA_PSW");

            rbLivelloOperatore.Text = this.linguaMngr.GetTranslation("LBL_LIVELLO_OPERATORE");
            rbLivelloTecnico.Text = this.linguaMngr.GetTranslation("LBL_LIVELLO_TECNICO");
            rbLivelloCostruttore.Text = this.linguaMngr.GetTranslation("LBL_LIVELLO_COSTRUTTORE");

            lblNuovaPassword.Text = this.linguaMngr.GetTranslation("LBL_NUOVA_PASSWORD");
            lblConfermaPassword.Text = this.linguaMngr.GetTranslation("LBL_CONFERMA_PASSWORD");

            btnCambiaPassword.Text = this.linguaMngr.GetTranslation("BTN_CAMBIA_PASSWORD");
        }

        private void btnKeyboard_Click(object sender, EventArgs e)
        {
            DigitalControl.FW.Utilities.KeyBoardOsk.showKeypad((sender as Control).Handle);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnCambiaPassword_Click(object sender, EventArgs e)
        {
            try
            {
                bool ok = false;

                if (!string.IsNullOrWhiteSpace(txtNuovaPassword.Text) && txtNuovaPassword.Text == txtConfermaPassword.Text)
                {
                    string psw = txtNuovaPassword.Text;

                    DataType.ConfigurazioneCorrente confObj = DataType.ConfigurazioneCorrente.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

                    if (rbLivelloOperatore.Checked == true)
                    {
                        if (confObj.PswTecnico != psw && confObj.PswCostruttore != psw)
                        {
                            confObj.PswOperatore = psw;
                            ok = true;
                        }
                    }
                    else if (rbLivelloTecnico.Checked == true)
                    {
                        if (confObj.PswOperatore != psw && confObj.PswCostruttore != psw)
                        {
                            confObj.PswTecnico = psw;
                            ok = true;
                        }
                    }
                    else if (rbLivelloCostruttore.Checked == true)
                    {
                        if (confObj.PswOperatore != psw && confObj.PswTecnico != psw)
                        {
                            confObj.PswCostruttore = psw;
                            ok = true;
                        }
                    }

                    if (ok)
                        DataType.ConfigurazioneCorrente.Serialize(confObj, Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

                }

                if (ok)
                {
                    MessageBox.Show(linguaMngr.GetTranslation("MSG_CAMBIO_PSW_OK"), linguaMngr.GetTranslation("MSG_ATTENZIONE"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Close();
                }
                else
                {
                    MessageBox.Show(linguaMngr.GetTranslation("MSG_CAMBIO_PSW_NON_VALIDO"), linguaMngr.GetTranslation("MSG_ERRORE"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }


            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

    }
}