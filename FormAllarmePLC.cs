using DigitalControl.FW.Class;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    public partial class FormAllarmePLC : Form
    {

        private Class.PlcMacchinaManager plcMacchinaManager = null;
        private DBL.LinguaManager linguaMngr = null;

        private bool allarmeLicenza = false;

        private bool[] errori = new bool[5];

        public FormAllarmePLC(Class.PlcMacchinaManager plcMacchinaManager, bool allarmeLicenza, DBL.LinguaManager linguaMngr)
        {
            InitializeComponent();

            this.plcMacchinaManager = plcMacchinaManager;
            this.linguaMngr = linguaMngr;

            this.allarmeLicenza = allarmeLicenza;

            PopolaSchermataErrore(plcMacchinaManager, linguaMngr);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            timerAggiornaStatoPLC.Tick -= timerAggiornaStatoPLC_Tick;
            timerAggiornaStatoPLC.Stop();
        }

        private void PopolaSchermataErrore(Class.PlcMacchinaManager plcMacchinaManager, DBL.LinguaManager linguaMngr)
        {
            txtDescrizioneErrore.Text = string.Empty;

            GestioneVisualizzazioneErrore(Class.PlcMacchinaManager.Allarmi.EMERGENZA_PREMUTA, linguaMngr.GetTranslation("ALLARME_EMERGENZA_PREMUTA"));
            GestioneVisualizzazioneErrore(Class.PlcMacchinaManager.Allarmi.RAGGIUNTO_NUMERO_DI_SCARTI_CONSECUTIVI, linguaMngr.GetTranslation("ALLARME_RAGGIUNTO_NUMERO_DI_SCARTI_CONSECUTIVI"));
            GestioneVisualizzazioneErrore(Class.PlcMacchinaManager.Allarmi.AVARIA_BATTERIE_UPS, linguaMngr.GetTranslation("ALLARME_AVARIA_BATTERIE_UPS"));
            GestioneVisualizzazioneErrore(Class.PlcMacchinaManager.Allarmi.AVARIA_TENSIONE_UPS, linguaMngr.GetTranslation("ALLARME_AVARIA_TENSIONE_UPS"));
            GestioneVisualizzazioneErrore(Class.PlcMacchinaManager.Allarmi.DISALLINEAMENTO, linguaMngr.GetTranslation("ALLARME_DISALLINEAMENTO"));

            if (this.allarmeLicenza)
            {
                txtDescrizioneErrore.Text = string.Format("{0}{1}\n\r\n\r", txtDescrizioneErrore.Text, "H_ERR_WDBID	 4057	 Image data management: object-ID outside the valid range");
            }
        }

        private void GestioneVisualizzazioneErrore(Class.PlcMacchinaManager.Allarmi flagAllarme, string messaggio)
        {
            if (plcMacchinaManager.CodiceAllarme.HasFlag(flagAllarme))
            {
                txtDescrizioneErrore.Text = string.Format("{0}{1}\n\r\n\r", txtDescrizioneErrore.Text, messaggio);
            }
        }

        private void GestioneStoricoAllarmi(int indiceErrore, Class.PlcMacchinaManager.Allarmi flagAllarme, DBL.StatisticheManager dbmStatistiche)
        {
            if (errori[indiceErrore] == false && plcMacchinaManager.CodiceAllarme.HasFlag(flagAllarme))
            {
                errori[indiceErrore] = true;

                DigitalControl.DataType.StatisticheAllarme allarme = new DigitalControl.DataType.StatisticheAllarme();

                allarme.Nodo = 0;
                allarme.IdAllarme = indiceErrore;
                allarme.Data = DateTime.Now;

                dbmStatistiche.WriteStoricoAllarmi(allarme);

            }
            else if (errori[indiceErrore] == true && !plcMacchinaManager.CodiceAllarme.HasFlag(flagAllarme))
            {
                errori[indiceErrore] = false;
            }
        }

        private void timerAggiornaStatoPLC_Tick(object sender, EventArgs e)
        {
            if (plcMacchinaManager.SpegniTutto)
            {
                if (Properties.Settings.Default.SpegniPC_da_PLC)
                    Process.Start("shutdown", "/f /s /t 10");
                Application.Exit();
            }

            if (plcMacchinaManager.CodiceAllarme != Class.PlcMacchinaManager.Allarmi.NO_ALLARMI || allarmeLicenza)
            {
                DBL.StatisticheManager dbmStatistiche = new DBL.StatisticheManager(Properties.Settings.Default.ConnectionStringStatistiche);

                GestioneStoricoAllarmi(0, Class.PlcMacchinaManager.Allarmi.RAGGIUNTO_NUMERO_DI_SCARTI_CONSECUTIVI, dbmStatistiche);
                GestioneStoricoAllarmi(1, Class.PlcMacchinaManager.Allarmi.EMERGENZA_PREMUTA, dbmStatistiche);
                GestioneStoricoAllarmi(2, Class.PlcMacchinaManager.Allarmi.AVARIA_BATTERIE_UPS, dbmStatistiche);
                GestioneStoricoAllarmi(3, Class.PlcMacchinaManager.Allarmi.AVARIA_TENSIONE_UPS, dbmStatistiche);
                GestioneStoricoAllarmi(4, Class.PlcMacchinaManager.Allarmi.DISALLINEAMENTO, dbmStatistiche);

                PopolaSchermataErrore(this.plcMacchinaManager, this.linguaMngr);
            }
            else
            {
                timerAggiornaStatoPLC.Enabled = false;
                this.Close();
            }

        }

        private void btnResetAllarmi_Click(object sender, EventArgs e)
        {
            try
            {
                plcMacchinaManager.ResetAllarmi();
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

    }
}