using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace DigitalControl.CMTL.Class
{
    public class PasswordManager
    {

        public enum LivelloPassword
        {
            NN = -1,
            Operatore = 0,
            Tecnico = 1,
            Costruttore = 2
        }

        private LivelloPassword lastLivello = LivelloPassword.NN;
        private Stopwatch swTempoLogin = null;

        public PasswordManager()
        {
            swTempoLogin = new Stopwatch();
        }

        public LivelloPassword GetLastLivello()
        {
            return lastLivello;
        }

        public bool CanOpen(LivelloPassword minLevel, DBL.LinguaManager linguaMngr)
        {
            return CanOpen(minLevel, linguaMngr, false);
        }

        public bool CanOpen(LivelloPassword minLevel, DBL.LinguaManager linguaMngr, bool force)
        {
            bool ok = false;

            if (Properties.Settings.Default.UsaPassword || force)
            {

                if (this.lastLivello != LivelloPassword.NN && swTempoLogin.ElapsedMilliseconds < Properties.Settings.Default.TempoPassword * 60 * 1000 && minLevel <= this.lastLivello)
                {
                    ok = minLevel <= this.lastLivello;
                }
                else
                {
                    FormPassword frmPsw = new FormPassword(linguaMngr);

                    if (frmPsw.ShowDialog() == DialogResult.OK)
                    {

                        bool pswTrovata = false;
                        bool livBasso = true;

                        DataType.ConfigurazioneCorrente confObj = DataType.ConfigurazioneCorrente.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

                        string psw = frmPsw.GetPassword();

                        if (psw == confObj.PswCostruttore)
                        {
                            ok = true;
                            lastLivello = LivelloPassword.Costruttore;
                            swTempoLogin.Restart();

                            pswTrovata = true;
                            livBasso = false;
                        }

                        if (!ok && psw == confObj.PswTecnico)
                        {
                            if (minLevel <= LivelloPassword.Tecnico)
                            {
                                ok = true;
                                lastLivello = LivelloPassword.Tecnico;
                                swTempoLogin.Restart();

                                livBasso = false;
                            }

                            pswTrovata = true;
                        }

                        if (!ok && psw == confObj.PswOperatore)
                        {
                            if (minLevel <= LivelloPassword.Operatore)
                            {
                                ok = true;
                                lastLivello = LivelloPassword.Operatore;
                                swTempoLogin.Restart();

                                livBasso = false;
                            }

                            pswTrovata = true;
                        }

                        if (!pswTrovata)
                        {
                            MessageBox.Show(linguaMngr.GetTranslation("MSG_PSW_ERRATA"), linguaMngr.GetTranslation("MSG_ERRORE"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else if (livBasso)
                        {
                            MessageBox.Show(linguaMngr.GetTranslation("MSG_LIVELLO_PSW_BASSO"), linguaMngr.GetTranslation("MSG_ATTENZIONE"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                    }
                }

            }
            else
            {
                ok = true;
            }

            return ok;
        }

    }
}