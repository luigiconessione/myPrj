using DigitalControl.FW.Class;
using System;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    public partial class FormDatiDiProduzione : Form
    {

        private readonly Class.PlcMacchinaManager plcMacchina = null;
        private readonly DBL.LinguaManager linguaMngr = null;
        private readonly DataType.Contatori contatori = null;

        public FormDatiDiProduzione(Class.PlcMacchinaManager plcMacchina, DataType.Contatori contatori, DBL.LinguaManager linguaMngr)
        {
            InitializeComponent();

            this.plcMacchina = plcMacchina;
            this.contatori = contatori;
            this.linguaMngr = linguaMngr;

            AdjustCulture();
            // |MP 5-2-19  gestione dei campi da visualizzare in funzione di cosa deve fare la macchina
            showCampiTappo();
            showCampiLivello();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            timerAggiornaStatoPLC.Tick -= timerAggiornaStatoPLC_Tick;
            timerAggiornaStatoPLC.Stop();
            timerAggiornaStatoPLC.Dispose();
        }


        private void AdjustCulture()
        {
            lblTitolo.Text = linguaMngr.GetTranslation("FORM_DATI_DI_PRODUZIONE_TITLE");

            lblDescrizioneTappo.Text = linguaMngr.GetTranslation("LBL_CNT_TAPPO");

            lblContatoreBuoniTappoDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_BUONI_TAPPO");
            lblContatoreScartiTappoDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_SCARTO_TAPPO");
            lblContatoreScartiTappoPresenzaDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_TAPPO_PRESENZA");
            lblContatoreScartiTappoSerraggioDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_TAPPO_SERRAGGIO");
            lblContatoreScartiTappoSerraggioStelvinDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_TAPPO_SERRAGGIO_STELVIN");
            lblContatoreScartiTappoPiantaggioDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_TAPPO_PIANTAGGIO");
            lblContatoreScartiTappoAnelloDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_TAPPO_ANELLO");
            lblContatoreScartiTappoGabbiettaDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_TAPPO_GABBIETTA");

            lblDescrizioneLivello.Text = linguaMngr.GetTranslation("LBL_CNT_LIVELLO");

            lblContatoreBuoniLivelloDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_BUONI_LIVELLO");
            lblContatoreScartiLivelloDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_SCARTO_LIVELLO");
            lblContatoreScartiLivelloMinDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_LIVELLO_MIN");
            lblContatoreScartiLivelloMaxDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_LIVELLO_MAX");
            lblContatoreScartiLivelloEmptyDescr.Text = linguaMngr.GetTranslation("LBL_SCARTO_LIVELLO_EMPTY");

            lblTempoMarciaDescr.Text = linguaMngr.GetTranslation("LBL_TEMPO_MARCIA");
            lblTempoFermoDescr.Text = linguaMngr.GetTranslation("LBL_TEMPO_FERMO");
        }

        // |MP 5-2-19 visualizzo i campi in funzione di quello che deve fare la macchina.
        private void showCampiTappo()
        {
            pnlNoRispTappo.Visible = false;
            btnResetScartiTappoPresenza.Visible = false;

            if (!Properties.Settings.Default.UsaControlloAnello)
            {
                panelAnello.Visible = false;
                btnResetScartiTappoAnello.Visible = false;
            }

            if (!Properties.Settings.Default.UsaControlloGabbietta)
            {
                panel13.Visible = false;
                btnResetScartiTappoGabbietta.Visible = false;
            }

            if (!Properties.Settings.Default.UsaControlloPiantaggio)
            {
                panel7.Visible = false;
                btnResetScartiTappoPiantaggio.Visible = false;
            }

            if (!Properties.Settings.Default.UsaControlloSerraggio)
            {
                panel6.Visible = false;
                btnResetScartiTappoSerraggio.Visible = false;
            }

            if (!Properties.Settings.Default.UsaStelvin)
            {
                panel10.Visible = false;
                btnresetScartiTappoSerraggioStelvin.Visible = false;
            }
        }

        // |MP 5-2-19 visualizzo i campi in funzione di quello che deve fare la macchina.
        private void showCampiLivello()
        {
            pnlNoRispLivello.Visible = false;
            panel9.Visible = false;
            panel4.Visible = false;
            btnResetScartiLivelloMin.Visible = false;
            btnResetScartiLivelloMax.Visible = false;
            btnResetScartiLivelloEmpty.Visible = false;

            if (!(Properties.Settings.Default.PRESENZA_SMART && Properties.Settings.Default.UsaControlloLivello))
            {
                lblDescrizioneLivello.Visible = false;
                panel3.Visible = false;
                panel2.Visible = false;
                btnResetBuoniLivello.Visible = false;
                btnresetScartiTappoSerraggioStelvin.Visible = false;
            }
        }

        private void timerAggiornaStatoPLC_Tick(object sender, EventArgs e)
        {
            timerAggiornaStatoPLC.Enabled = false;
            try
            {
                if (this.plcMacchina != null)
                {
                    lblContatoreBuoniTappo.Text = this.contatori.CntPezziBuoniTappo.ToString();
                    lblContatoreScartiTappo.Text = this.contatori.CntPezziScartoTappo.ToString();
                    lblContatoreScartiTappoPresenza.Text = this.contatori.CntPezziScartoTappoPresenza.ToString();
                    lblContatoreScartiTappoSerraggio.Text = this.contatori.CntPezziScartoTappoSerraggio.ToString();
                    lblContatoreScartiTappoSerraggioStelvin.Text = this.contatori.CntPezziScartoTappoSerraggioStelvin.ToString();
                    lblContatoreScartiTappoPiantaggio.Text = this.contatori.CntPezziScartoTappoPiantaggio.ToString();
                    lblContatoreScartiTappoAnello.Text = this.contatori.CntPezziScartoTappoAnello.ToString();
                    lblContatoreScartiTappoGabbietta.Text = this.contatori.CntPezziScartoTappoGabbietta.ToString();

                    lblContatoreBuoniLivello.Text = this.contatori.CntPezziBuoniLivello.ToString();
                    lblContatoreScartiLivello.Text = this.contatori.CntPezziScartoLivello.ToString();
                    lblContatoreScartiLivelloMin.Text = this.contatori.CntPezziScartoLivelloMin.ToString();
                    lblContatoreScartiLivelloMax.Text = this.contatori.CntPezziScartoLivelloMax.ToString();
                    lblContatoreScartiLivelloEmpty.Text = this.contatori.CntPezziScartoLivelloEmpty.ToString();

                    lblTempoMarcia.Text = this.plcMacchina.TempoMarcia.ToString(@"hh\:mm\:ss");
                    lblTempoFermo.Text = this.plcMacchina.TempoFermo.ToString(@"hh\:mm\:ss");
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                timerAggiornaStatoPLC.Enabled = true;
            }
        }


        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }



        private void btnResetBuoniTappo_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetPezziBuoniTappo();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiTappo_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetPezziScartoTappo();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiTappoPresenza_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiTappoPresenza();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiTappoSerraggio_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiTappoSerraggio();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnresetScartiTappoSerraggioStelvin_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiTappoSerraggioStelvin();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiTappoPiantaggio_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiTappoPiantaggio();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiTappoAnello_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiTappoAnello();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiTappoGabbietta_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiTappoGabbietta();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }












        private void btnResetBuoniLivello_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetPezziBuoniLivello();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiLivello_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetPezziScartoLivello();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiLivelloMin_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiLivelloMin();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiLivelloMax_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiLivelloMax();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetScartiLivelloEmpty_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetScartiLivelloEmpty();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

    }
}