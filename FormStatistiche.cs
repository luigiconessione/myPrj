using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{

    public partial class FormStatistiche : Form
    {

        private readonly DBL.LinguaManager linguaMngr = null;


        public FormStatistiche(DBL.LinguaManager linguaMngr)
        {
            InitializeComponent();

            this.linguaMngr = linguaMngr;

            AdjustCulture();

            FillFormatoBox();

            AggiornaDati();
        }

        private void AdjustCulture()
        {
            lblTitolo.Text = linguaMngr.GetTranslation("FORM_STATISTICHE_TITLE");

            lblRicetta.Text = linguaMngr.GetTranslation("LBL_RICETTA");
            lblDataInizio.Text = linguaMngr.GetTranslation("LBL_DATA_INIZIO");
            lblDataFine.Text = linguaMngr.GetTranslation("LBL_DATA_FINE");

            btnUpdate.Text = linguaMngr.GetTranslation("BTN_AGGIORNA");
            btnReset.Text = linguaMngr.GetTranslation("BTN_ELIMINA");

            tabProduzioneGiornaliera.Text = linguaMngr.GetTranslation("LBL_PROD_GIORNALIERA");
            tabProduzionePerRicetta.Text = linguaMngr.GetTranslation("LBL_PROD_PER_RICETTA");
            tabProduzioneOraria.Text = linguaMngr.GetTranslation("LBL_PROD_ORARIA");
            tabStoricoAllarmi.Text = linguaMngr.GetTranslation("LBL_STORICO_ALLARMI");

            dgvAllarmi.Columns["dataColumn"].HeaderText = linguaMngr.GetTranslation("LBL_DATA");
            dgvAllarmi.Columns["descrizioneAllarmeColumn"].HeaderText = linguaMngr.GetTranslation("LBL_ALLARME");

        }

        private void FillFormatoBox()
        {
            Console.WriteLine("Properties.Settings.Default.ConnectionStringFormati: {0}", Properties.Settings.Default.ConnectionStringFormati);
            DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

            List<DigitalControl.DataType.StatisticheFormato> listCombo = dbmFormati.ReadFormatiForStatistiche();

            listCombo.Insert(0, new DigitalControl.DataType.StatisticheFormato() { IdFormato = -1 });

            cmbFormati.DataSource = listCombo;
            cmbFormati.ValueMember = "IdFormato";
            cmbFormati.DisplayMember = "Descrizione";
        }

        private void AggiornaDati()
        {
            if (tabPageStatistiche.SelectedTab == tabProduzioneGiornaliera)
            {
                AggiornaGraficoProduzioneGiornaliera();
            }
            else if (tabPageStatistiche.SelectedTab == tabProduzionePerRicetta)
            {
                AggiornaGraficoProduzionePerFormato();
            }
            else if (tabPageStatistiche.SelectedTab == tabProduzioneOraria)
            {
                AggiornaGraficoProduzioneOraria();
            }
            else if (tabPageStatistiche.SelectedTab == tabStoricoAllarmi)
            {
                AggiornaStoricoAllarmi();
            }
        }

        private void StatisticheTab_SelectedIndexChanged(object sender, EventArgs e)
        {
            AggiornaDati();
        }


        private void btnUpdate_Click(object sender, EventArgs e)
        {
            AggiornaDati();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(linguaMngr.GetTranslation("MSG_ELIMINA_STATISTICHE"), linguaMngr.GetTranslation("MSG_ELIMINARE"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                DigitalControl.DBL.StatisticheManager dbm = new DigitalControl.DBL.StatisticheManager(Properties.Settings.Default.ConnectionStringStatistiche);
                dbm.DeleteStatistiche();

                AggiornaDati();
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private void AggiornaGraficoProduzioneGiornaliera()
        {
            int? formato = null;

            DateTime inizio = dtDataInizio.Value.Date;
            DateTime fine = dtDataFine.Value.Date.AddDays(1);

            if (!(cmbFormati.SelectedValue == null || (int)cmbFormati.SelectedValue < 0))
            {
                formato = (int)cmbFormati.SelectedValue;
            }

            List<SerieBaseProduzioneGiornaliera> serie = null;

            GetProduzioneGiornaliera(formato, inizio, fine, out serie);

            chartProduzioneGiornaliera.Series[0].Points.Clear();
            chartProduzioneGiornaliera.Series[1].Points.Clear();

            foreach (var item in serie)
            {
                chartProduzioneGiornaliera.Series[0].Points.AddXY(item.Data, item.ValoreBuoni);
                chartProduzioneGiornaliera.Series[1].Points.AddXY(item.Data, item.ValoreScarti);
            }

            chartProduzioneGiornaliera.Update();
        }

        private void AggiornaGraficoProduzionePerFormato()
        {
            int? formato = null;

            DateTime inizio = dtDataInizio.Value.Date;
            DateTime fine = dtDataFine.Value.Date.AddDays(1);

            if (!(cmbFormati.SelectedValue == null || (int)cmbFormati.SelectedValue < 0))
            {
                formato = (int)cmbFormati.SelectedValue;
            }

            List<SerieBaseProduzionePerFormato> serie = null;

            GetProduzionePerFormato(formato, inizio, fine, out serie);

            chartProduzionePerRicetta.Series[0].Points.Clear();
            chartProduzionePerRicetta.Series[1].Points.Clear();

            foreach (var item in serie)
            {
                chartProduzionePerRicetta.Series[0].Points.AddXY(item.Formato, item.ValoreBuoni);
                chartProduzionePerRicetta.Series[1].Points.AddXY(item.Formato, item.ValoreScarti);
            }

            chartProduzionePerRicetta.Update();
        }

        private void AggiornaGraficoProduzioneOraria()
        {
            int? formato = null;

            DateTime inizio = dtDataInizio.Value.Date;
            DateTime fine = dtDataFine.Value.Date.AddDays(1);

            if (!(cmbFormati.SelectedValue == null || (int)cmbFormati.SelectedValue < 0))
            {
                formato = (int)cmbFormati.SelectedValue;
            }

            List<SerieBaseProduzioneOraria> serie = null;

            GetProduzioneOraria(formato, inizio, fine, out serie);

            chartProduzioneOraria.Series[0].Points.Clear();

            foreach (var item in serie)
            {
                chartProduzioneOraria.Series[0].Points.AddXY(item.Data, item.Valore);
            }

            chartProduzioneOraria.Update();
        }

        private void AggiornaStoricoAllarmi()
        {
            int? formato = null;

            DateTime inizio = dtDataInizio.Value.Date;
            DateTime fine = dtDataFine.Value.Date.AddDays(1);

            if (!(cmbFormati.SelectedValue == null || (int)cmbFormati.SelectedValue < 0))
            {
                formato = (int)cmbFormati.SelectedValue;
            }

            DBL.StatisticheManager dbm = new DBL.StatisticheManager(Properties.Settings.Default.ConnectionStringStatistiche);

            List<DigitalControl.DataType.StatisticheAllarme> allarmi = null;

            if (formato == null)
            {
                allarmi = dbm.ReadStoricoAllarmi(inizio, fine);
            }
            else
            {
                allarmi = dbm.ReadStoricoAllarmi(formato.Value, inizio, fine);
            }

            for (int i = 0; i < allarmi.Count; i++)
            {
                allarmi[i].DescrizioneAllarme = linguaMngr.GetTranslation(allarmi[i].DescrizioneAllarme);
            }

            statisticheAllarmeBindingSource.DataSource = allarmi;

        }

        public class SerieBaseProduzioneGiornaliera
        {
            public SerieBaseProduzioneGiornaliera(DateTime data, int valoreBuoni, int valoreScarti)
            {
                this.Data = data;
                this.ValoreBuoni = valoreBuoni;
                this.ValoreScarti = valoreScarti;
            }

            public DateTime Data { get; set; }
            public int ValoreBuoni { get; set; }
            public int ValoreScarti { get; set; }

        }

        public class SerieBaseProduzionePerFormato
        {
            public SerieBaseProduzionePerFormato(string formato, int valoreBuoni, int valoreScarti)
            {
                this.Formato = formato;
                this.ValoreBuoni = valoreBuoni;
                this.ValoreScarti = valoreScarti;
            }

            public string Formato { get; set; }
            public int ValoreBuoni { get; set; }
            public int ValoreScarti { get; set; }

        }

        public class SerieBaseProduzioneOraria
        {
            public SerieBaseProduzioneOraria(DateTime data, int valore)
            {
                this.Data = data;
                this.Valore = valore;
            }

            public DateTime Data { get; set; }
            public int Valore { get; set; }

        }

        public class SerieBaseStatoMacchina
        {
            public SerieBaseStatoMacchina(DateTime data, int stato)
            {
                this.Data = data;
                this.Stato = stato;
            }

            public DateTime Data { get; set; }
            public int Stato { get; set; }

        }


        private void GetProduzioneGiornaliera(int? formato, DateTime inizio, DateTime fine, out List<SerieBaseProduzioneGiornaliera> serie)
        {

            DBL.StatisticheManager dbm = new DBL.StatisticheManager(Properties.Settings.Default.ConnectionStringStatistiche);
            List<DigitalControl.DataType.StatisticheProduzione> produzioneList = null;

            if (formato == null)
            {
                produzioneList = dbm.ReadProduzioneGiornaliera(-1, inizio, fine);
            }
            else
            {
                produzioneList = dbm.ReadProduzioneGiornaliera(formato.Value, -1, inizio, fine);
            }

            serie = new List<SerieBaseProduzioneGiornaliera>();

            //if (produzioneList != null && produzioneList.Count > 0)
            if (produzioneList != null) // |MP 23-1-19 
            {
                // |MP 23-1-19 modificate x far vedere la pg con qualcosa anche se non c'è scritto nulla
                int prevPzBuoni=0;
                int pzBuoni=0;
                int prevPzScarto=0;
                int pzScarto = 0;
                DateTime prevData = System.DateTime.Now;

                if (produzioneList.Count > 0)   // |MP 23-1-19 
                {
                    DigitalControl.DataType.StatisticheProduzione prodObj = produzioneList[0];

                    prevPzBuoni = prodObj.PzBuoni;
                    pzBuoni = prevPzBuoni;
                    prevPzScarto = prodObj.Scarto;
                    pzScarto = prevPzScarto;

                    DateTime currData = prodObj.Data.Date;
                    prevData = prodObj.Data.Date;

                    for (int i = 1; i < produzioneList.Count; i++)
                    {
                        prodObj = produzioneList[i];

                        int currPzBuoni = prodObj.PzBuoni;
                        int currPzScarto = prodObj.Scarto;

                        currData = prodObj.Data.Date;

                        if (currData > prevData)
                        {
                            serie.Add(new SerieBaseProduzioneGiornaliera(prevData, pzBuoni, pzScarto));

                            if (currPzBuoni >= prevPzBuoni)
                            {
                                pzBuoni = (currPzBuoni - prevPzBuoni);
                            }
                            else
                            {
                                pzBuoni = currPzBuoni;
                            }

                            if (currPzScarto >= prevPzScarto)
                            {
                                pzScarto = (currPzScarto - prevPzScarto);
                            }
                            else
                            {
                                pzScarto = currPzScarto;
                            }
                        }
                        else
                        {
                            if (currPzBuoni >= prevPzBuoni)
                            {
                                pzBuoni += (currPzBuoni - prevPzBuoni);
                            }
                            else
                            {
                                pzBuoni += currPzBuoni;
                            }

                            if (currPzScarto >= prevPzScarto)
                            {
                                pzScarto += (currPzScarto - prevPzScarto);
                            }
                            else
                            {
                                pzScarto += currPzScarto;
                            }
                        }

                        prevPzBuoni = currPzBuoni;
                        prevPzScarto = currPzScarto;
                        prevData = currData;

                    }
                }
                serie.Add(new SerieBaseProduzioneGiornaliera(prevData, pzBuoni, pzScarto));

            }

        }

        private void GetProduzionePerFormato(int? formato, DateTime inizio, DateTime fine, out List<SerieBaseProduzionePerFormato> serie)
        {

            DBL.StatisticheManager dbmStatistiche = new DBL.StatisticheManager(Properties.Settings.Default.ConnectionStringStatistiche);
            DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

            List<DigitalControl.DataType.StatisticheProduzione> produzioneList = null;

            if (formato == null)
            {
                produzioneList = dbmStatistiche.ReadProduzionePerFormato(-1, inizio, fine);
            }
            else
            {
                produzioneList = dbmStatistiche.ReadProduzionePerFormato(formato.Value, -1, inizio, fine);
            }

            serie = new List<SerieBaseProduzionePerFormato>();

            if (produzioneList != null && produzioneList.Count > 0)
            {
                DigitalControl.DataType.StatisticheProduzione prodObj = produzioneList[0];

                int prevPzBuoni = prodObj.PzBuoni;
                int pzBuoni = prevPzBuoni;
                int prevPzScarto = prodObj.Scarto;
                int pzScarto = prevPzScarto;

                int prevFormato = prodObj.IdFormato;
                int currFormato = prodObj.IdFormato;

                DateTime prevdata = prodObj.Data;
                DateTime currdata = prodObj.Data;

                string formatoDescr = string.Empty;

                for (int i = 1; i < produzioneList.Count; i++)
                {
                    prodObj = produzioneList[i];

                    // calcola il delta dei pezzi prodotti
                    int currPzBuoni = prodObj.PzBuoni;
                    int currPzScarto = prodObj.Scarto;

                    currFormato = prodObj.IdFormato;
                    currdata = prodObj.Data;

                    if (prevdata.Date != currdata.Date)
                    {
                        prevdata = currdata;
                    }

                    if (currFormato > prevFormato)
                    {
                        formatoDescr = dbmFormati.GetDescrizioneFormato(prevFormato);
                        serie.Add(new SerieBaseProduzionePerFormato(formatoDescr, pzBuoni, pzScarto));

                        if (currPzBuoni >= prevPzBuoni)
                        {
                            pzBuoni = currPzBuoni - prevPzBuoni;
                        }
                        else
                        {
                            pzBuoni = currPzBuoni;
                        }

                        if (currPzScarto >= prevPzScarto)
                        {
                            pzScarto = currPzScarto - prevPzScarto;
                        }
                        else
                        {
                            pzScarto = currPzScarto;
                        }
                    }
                    else
                    {
                        if (currPzBuoni >= prevPzBuoni)
                        {
                            pzBuoni += currPzBuoni - prevPzBuoni;
                        }
                        else
                        {
                            pzBuoni += currPzBuoni;
                        }

                        if (currPzScarto >= prevPzScarto)
                        {
                            pzScarto += currPzScarto - prevPzScarto;
                        }
                        else
                        {
                            pzScarto += currPzScarto;
                        }
                    }

                    prevPzBuoni = currPzBuoni;
                    prevPzScarto = currPzScarto;
                    prevFormato = currFormato;
                    prevdata = currdata;
                }

                formatoDescr = dbmFormati.GetDescrizioneFormato(currFormato);
                serie.Add(new SerieBaseProduzionePerFormato(formatoDescr, pzBuoni, pzScarto));

            }

        }

        private void GetProduzioneOraria(int? formato, DateTime inizio, DateTime fine, out List<SerieBaseProduzioneOraria> serie)
        {

            DBL.StatisticheManager dbm = new DBL.StatisticheManager(Properties.Settings.Default.ConnectionStringStatistiche);
            List<DigitalControl.DataType.StatisticheProduzione> produzioneList = null;

            if (formato == null)
            {
                produzioneList = dbm.ReadProduzioneGiornaliera(-1, inizio, fine);
            }
            else
            {
                produzioneList = dbm.ReadProduzioneGiornaliera(formato.Value, -1, inizio, fine);
            }

            serie = new List<SerieBaseProduzioneOraria>();

            if (produzioneList != null && produzioneList.Count > 0)
            {

                DateTime datatPrima = produzioneList[0].Data;

                for (int i = 0; i < produzioneList.Count; i++)
                {
                    DigitalControl.DataType.StatisticheProduzione prodObj = produzioneList[i];

                    if ((prodObj.Data - datatPrima).TotalSeconds > 10)
                    {
                        serie.Add(new SerieBaseProduzioneOraria(datatPrima.AddSeconds(5), 0));
                        serie.Add(new SerieBaseProduzioneOraria(prodObj.Data.AddSeconds(-5), 0));
                    }

                    datatPrima = prodObj.Data;

                    serie.Add(new SerieBaseProduzioneOraria(prodObj.Data, prodObj.ProdOraria));
                }

            }

        }

    }

}