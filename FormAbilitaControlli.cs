using DigitalControl.DataType;
using DigitalControl.FW.Class;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    public partial class FormAbilitaControlli : Form
    {

        private int id_formato;
        private DataType.AlgoritmoControlloTappoParam paramTappo = null;
        private DataType.AlgoritmoControlloLivelloParam paramLivello = null;
        private readonly DBL.LinguaManager linguaMngr = null;

        public FormAbilitaControlli(int id_formato, DataType.AlgoritmoControlloTappoParam paramTappo, DataType.AlgoritmoControlloLivelloParam paramLivello, DBL.LinguaManager linguaMngr)
        {
            InitializeComponent();

            this.id_formato = id_formato;
            this.paramTappo = paramTappo;
            this.paramLivello = paramLivello;
            this.linguaMngr = linguaMngr;

            Object2Form(paramTappo, paramLivello);

            AdjustCulture();

            if (!Properties.Settings.Default.UsaStelvin)
            {
                chbAbilitaPiantaggio.Location = chbAbilitaSerraggioStelvin.Location;
                chbAbilitaSerraggioStelvin.Visible = false;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            DataType.AlgoritmoControlloTappoParam paramTappo = null;
            DataType.AlgoritmoControlloLivelloParam paramLivello = null;

            Form2Object(out paramTappo, out paramLivello);

            DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

            Formato formato = null;

            if (paramLivello != null)
            {
                if (Properties.Settings.Default.LivelloDaCamera)
                {
                    formato = new Formato(id_formato, MainForm.IDX_CORE_LIVELLO, TipiAlgoritmo.AlgoritmoLivello);

                    formato.XMLString = DataType.AlgoritmoControlloLivelloParam.Serialize(paramLivello);

                    dbmFormati.WriteControlliVisione(formato);
                }
                else
                {
                    //TODO : DISABILITO IL CONTROLLO LIVELLO AL PLC
                }
            }

            if (paramTappo != null)
            {
                formato = new Formato(id_formato, MainForm.IDX_CORE_TAPPO, TipiAlgoritmo.AlgoritmoTappo);

                formato.XMLString = DataType.AlgoritmoControlloTappoParam.Serialize(paramTappo);

                dbmFormati.WriteControlliVisione(formato);
            }
        }

        private void AdjustCulture()
        {
            lblTitolo.Text = linguaMngr.GetTranslation("FORM_ABILITA_CONTROLLI_TITLE");

            chbAbilitaLivello.Text = linguaMngr.GetTranslation("LBL_ABILITAZIONE_LIVELLO");
            chbAbilitaPresenza.Text = linguaMngr.GetTranslation("LBL_ABILITAZIONE_PRESENZA");
            chbAbilitazioneControlloAnello.Text = linguaMngr.GetTranslation("LBL_ABILITAZIONE_CONTROLLO_ANELLO");
            chbAbilitaSerraggio.Text = linguaMngr.GetTranslation("LBL_ABILITAZIONE_SERRAGGIO");
            chbAbilitaSerraggioStelvin.Text = linguaMngr.GetTranslation("LBL_ABILITAZIONE_SERRAGGIO_STELVIN");
            chbAbilitaPiantaggio.Text = linguaMngr.GetTranslation("LBL_ABILITAZIONE_PIANTAGGIO");
        }

        private void Object2Form(DataType.AlgoritmoControlloTappoParam paramTappo, DataType.AlgoritmoControlloLivelloParam paramLivello)
        {
            try
            {
                if (this.paramLivello != null)
                {
                    if (Properties.Settings.Default.LivelloDaCamera)
                    {
                        chbAbilitaLivello.Checked = paramLivello.AbilitaControllo;
                    }
                }

                if (this.paramTappo != null)
                {
                    chbAbilitaPresenza.Checked = paramTappo.AbilitaPresenza;
                    chbAbilitazioneControlloAnello.Checked = paramTappo.AbilitaControlloAnello;
                    chbAbilitaSerraggio.Checked = paramTappo.AbilitaSerraggio;
                    chbAbilitaSerraggioStelvin.Checked = paramTappo.AbilitaSerraggioStelvin;
                    chbAbilitaPiantaggio.Checked = paramTappo.AbilitaPiantaggio;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void Form2Object(out DataType.AlgoritmoControlloTappoParam paramTappo, out DataType.AlgoritmoControlloLivelloParam paramLivello)
        {
            paramTappo = this.paramTappo;
            paramLivello = this.paramLivello;

            if (paramLivello != null)
            {
                if (Properties.Settings.Default.LivelloDaCamera)
                {
                    paramLivello.AbilitaControllo = chbAbilitaLivello.Checked;
                }
            }

            if (paramTappo != null)
            {
                paramTappo.AbilitaPresenza = chbAbilitaPresenza.Checked;
                paramTappo.AbilitaControlloAnello = chbAbilitazioneControlloAnello.Checked;
                paramTappo.AbilitaSerraggio = chbAbilitaSerraggio.Checked;
                paramTappo.AbilitaSerraggioStelvin = chbAbilitaSerraggioStelvin.Checked;
                paramTappo.AbilitaPiantaggio = chbAbilitaPiantaggio.Checked;
            }
        }

        private void btnClose_Click(object sender, System.EventArgs e)
        {
            this.Close();
        }

    }
}