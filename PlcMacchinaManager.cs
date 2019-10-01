using DigitalControl.DataType;
using DigitalControl.FW.Class;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DigitalControl.CMTL.Class
{
    public class PlcMacchinaManager
    {

        public const double TOLLERANZA_POSIZIONAMENTO_SLITTA = 2;

        #region Variabili Private

        private CPD.CPDManager CPDManagerPLC = null;
        private CPD.CPDManager CPDManagerMovimentazioni = null;

        private Stopwatch swTempoStop = new Stopwatch();
        private Stopwatch swTempoStart = new Stopwatch();
        private Stopwatch swTempoTroppoPieno = new Stopwatch();
        private Stopwatch swTempoIngessoVuoto = new Stopwatch();

        private System.Timers.Timer readTimer = null;

        #region modificheMAX
        // |MP  9-01-19 X SCRIVERE LE PROPERTIES PRESENTI
        //private int INDIRIZZO_PLC_MACCHINA { get { return int.Parse(Properties.Settings.Default.INDIRIZZO_PLC_MACCHINA, System.Globalization.NumberStyles.HexNumber); } }           //private const int INDIRIZZO_PLC_MACCHINA = 0x80;
        // |MP AGGIUNTO TEST X VERIFICARE PRESENZA
        /*private int INDIRIZZO_PLC_LIVELLO { get { return int.Parse(Properties.Settings.Default.INDIRIZZO_PLC_LIVELLO, System.Globalization.NumberStyles.HexNumber); } }             //private const int INDIRIZZO_PLC_LIVELLO = 0x82;
        private int INDIRIZZO_PLC_ESPULSORE { get { return int.Parse(Properties.Settings.Default.INDIRIZZO_PLC_ESPULSORE, System.Globalization.NumberStyles.HexNumber); } }         //private const int INDIRIZZO_PLC_ESPULSORE = 0x81;
        private int INDIRIZZO_PLC_TESTA { get { return int.Parse(Properties.Settings.Default.INDIRIZZO_PLC_TESTA, System.Globalization.NumberStyles.HexNumber); } }                 //private const int INDIRIZZO_PLC_TESTA = 0x83;
        */
        // |MP AGGIUNTO TEST X VERIFICARE PRESENZA INVECE DELLE ISTRUZIONI PRIVATE SOPRA
        private int INDIRIZZO_PLC_MACCHINA;
        private int INDIRIZZO_PLC_LIVELLO;
        private int INDIRIZZO_PLC_ESPULSORE;
        private int INDIRIZZO_PLC_TESTA;
        #endregion modificheMAX


        private const int INTERVALLO_LETTURA = 5000;
        private const int INTERVALLO_LETTURA_VELOCE = 500;

        private const int WM0 = 0x700;
        private const int WM1 = 0x702;
        private const int WM2 = 0x704;
        private const int WM3 = 0x706;
        private const int WM4 = 0x708;
        private const int WM5 = 0x70A;
        private const int WM6 = 0x70C;
        private const int WM7 = 0x70E;
        private const int WM8 = 0x710;
        private const int WM9 = 0x712;
        private const int WM10 = 0x714;
        private const int WM11 = 0x716;
        private const int WM12 = 0x718;
        private const int WM13 = 0x71A;
        private const int WM14 = 0x71C;
        private const int WM15 = 0x71E;
        private const int WM16 = 0x720;
        private const int WM17 = 0x722;
        private const int WM18 = 0x724;
        private const int WM19 = 0x726;
        private const int WM20 = 0x728;
        private const int WM21 = 0x72A;
        private const int WM22 = 0x72C;
        private const int WM23 = 0x72E;
        private const int WM24 = 0x730;
        private const int WM25 = 0x732;
        private const int WM26 = 0x734;
        private const int WM27 = 0x736;
        private const int WM28 = 0x738;
        private const int WM29 = 0x73A;
        private const int WM30 = 0x73C;
        private const int WM31 = 0x73E;
        private const int WM32 = 0x740;
        private const int WM33 = 0x742;
        private const int WM34 = 0x744;

        private const int WM40 = 0x750;
        private const int WM41 = 0x752;
        private const int WM42 = 0x754;
        private const int WM43 = 0x756;
        private const int WM44 = 0x758;
        private const int WM45 = 0x75A;
        private const int WM46 = 0x75C;

        private const int WM63 = 0X77E;
        private const int WM64 = 0x780;

        private const int WM120 = 0x7F0;
        private const int WM121 = 0x7F2;
        private const int WM122 = 0x7F4;
        private const int WM123 = 0x7F6;

        #endregion Variabili Private

        #region Variabili Pubbliche

        public enum Stati
        {
            MACCHINA_IN_STOP_SENZA_ALLARMI = 1,
            MACCHINA_IN_MARCIA = 2,
            //RAGGIUNTO_NUMERO_DI_SCARTI_CONSECUTIVI = 4,
            MANCANZA_PRODOTTO_IN_INGRESSO = 8,
            TROPPO_PIENO_IN_USCITA = 16,
            CONSENSO_MOVIMENTAZIONE = 32
        }

        public enum Allarmi
        {
            NO_ALLARMI = 0,
            RAGGIUNTO_NUMERO_DI_SCARTI_CONSECUTIVI = 1,
            EMERGENZA_PREMUTA = 2,
            AVARIA_BATTERIE_UPS = 4,
            AVARIA_TENSIONE_UPS = 8,
            DISALLINEAMENTO = 16
        }

        private enum Comandi
        {
            RICALCOLA = 1,

            RESET_BUONI_TAPPO = 10,
            RESET_SCARTO_TAPPO = 11,
            RESET_NO_RISPOSTA_TAPPO = 12,

            RESET_BUONI_LIVELLO = 13,
            RESET_SCARTO_LIVELLO = 14,
            RESET_NO_RISPOSTA_LIVELLO = 15,

            ABILITA_ESPULSORE = 20,
            DISABILITA_ESPULSORE = 21,

            RESET_TOT_BUONI = 22,
            RESET_TOT_SCARTI = 23,

            ENTRA_REGOLAZIONE = 30,
            ESCI_REGOLAZIONE = 31,

            ENTRA_CAMBIO_FORMATO_SLITTE = 32,
            ESCI_CAMBIO_FORMATO_SLITTE = 33,

            ENTRA_REGOLAZIONE_SLITTA_CAMERE = 34,
            ESCI_REGOLAZIONE_SLITTA_CAMERE = 35,

            ENTRA_CARICAMENTO_RICETTA = 36,
            ESCI_CARICAMENTO_RICETTA = 37,

            RESET_ALLARMI = 40,

            START_PLC = 50,
            STOP_PLC = 51,
        }

        public enum StatiSlitta
        {
            IN_MOVIMENTO = 1,
            IN_POSIZIONE = 2
        }

        private enum ComandiSlitta
        {
            AZZERAMENTO = 10,
            STOPEMERGENZA = 11,
            POSIZIONAMENTO = 20,
            DISCESA = 6,
            SALITA = 5,
            RICERCA_LIVELLO = 30,

            //RESET_BUONI_LIVELLO = 13,
            //RESET_SCARTO_LIVELLO = 14,
            //RESET_NO_RISPOSTA_LIVELLO = 15,

            ENTRA_CAMBIO_FORMATO_SLITTE = 32,
            ESCI_CAMBIO_FORMATO_SLITTE = 33,

            ENTRA_REGOLAZIONE_SLITTA_CAMERE = 34,
            ESCI_REGOLAZIONE_SLITTA_CAMERE = 35,

            RESET_ALLARMI = 40,

            START_PLC = 50,
            STOP_PLC = 51,

            //ABILITA_ESPULSORE = 60,
            //DISABILITA_ESPULSORE = 61
        }

        public TimeSpan TempoMarcia { get { return swTempoStart.Elapsed; } }
        public TimeSpan TempoFermo { get { return swTempoStop.Elapsed; } }
        public TimeSpan TempoTroppoPieno { get { return swTempoTroppoPieno.Elapsed; } }
        public TimeSpan TempoIngressoVuoto { get { return swTempoIngessoVuoto.Elapsed; } }

        /*---------LIVELLO---------*/
        //public int CntPezziBuoniLivello { get; private set; }
        //public int CntPezziScartoLivello { get; private set; }

        //public int CntPezziScartoLivelloMin { get; private set; }
        //public int CntPezziScartoLivelloMax { get; private set; }
        //public int CntPezziScartoLivelloEmpty { get; private set; }

        //public int CntNoRispostaLivello { get; private set; }
        /*---------LIVELLO---------*/

        /*---------TAPPO---------*/
        //public int CntPezziBuoniTappo { get; private set; }
        //public int CntPezziScartoTappo { get; private set; }

        //public int CntPezziScartoTappoPresenza { get; private set; }
        //public int CntPezziScartoTappoSerraggio { get; private set; }
        //public int CntPezziScartoTappoSerraggioStelvin { get; private set; }
        //public int CntPezziScartoTappoPiantaggio { get; private set; }
        //public int CntPezziScartoTappoAnello { get; private set; }

        //public int CntNoRispostaTappo { get; private set; }
        /*---------TAPPO---------*/

        //public int CntTotBuoni { get; private set; }
        //public int CntTotScarti { get; private set; }

        public int ProduzioneOraria { get; private set; }
        public bool EspulsoreAbilitato { get; private set; }

        public Stati CodiceStato { get; private set; }
        public Allarmi CodiceAllarme { get; private set; }

        public bool SpegniTutto { get; private set; }


        //public int PosizioneSlitta { get; private set; }
        public bool LetturaLivello { get; set; }
        public int CodaRisultatiLivello { get; set; }

        public MotoreManager MotoreTesta { get; set; }
        public MotoreManager MotoreLivello { get; set; }

        #endregion Variabili Pubbliche

        public PlcMacchinaManager(RS232Param connectionPLC, RS232Param connectionMovimentazioni)
        {
            // |MP   FATTA INTERA REGIONE
            #region modificheMAX
            bool bINDIRIZZO_PLC_TESTA = false;
            bool bINDIRIZZO_PLC_LIVELLO = false;
            bool bINDIRIZZO_PLC_ESPULSORE = false;

            try
            {

                foreach (System.Configuration.SettingsProperty proprieta in Properties.Settings.Default.Properties)
                {
                    Console.WriteLine("foreach proprieta: {0}", proprieta.Name);
                    if ("INDIRIZZO_PLC_LIVELLO".Equals(proprieta.Name))
                    {
                        Console.WriteLine("foreach proprieta.DefaultValue: {0}", proprieta.DefaultValue);
                        INDIRIZZO_PLC_LIVELLO = int.Parse(proprieta.DefaultValue.ToString(), System.Globalization.NumberStyles.HexNumber);
                        Console.WriteLine("foreach INDIRIZZO_PLC_LIVELLO: {0}", INDIRIZZO_PLC_LIVELLO);
                        bINDIRIZZO_PLC_LIVELLO = true;
                    }
                    if ("INDIRIZZO_PLC_MACCHINA".Equals(proprieta.Name))
                    {
                        Console.WriteLine("foreach proprieta.DefaultValue: {0}", proprieta.DefaultValue);
                        INDIRIZZO_PLC_MACCHINA = int.Parse(proprieta.DefaultValue.ToString(), System.Globalization.NumberStyles.HexNumber);
                        Console.WriteLine("foreach INDIRIZZO_PLC_MACCHINA: {0}", INDIRIZZO_PLC_MACCHINA);
                    }
                    if ("INDIRIZZO_PLC_TESTA".Equals(proprieta.Name))
                    {
                        Console.WriteLine("foreach proprieta.DefaultValue: {0}", proprieta.DefaultValue);
                        INDIRIZZO_PLC_TESTA = int.Parse(proprieta.DefaultValue.ToString(), System.Globalization.NumberStyles.HexNumber);
                        Console.WriteLine("foreach INDIRIZZO_PLC_TESTA: {0}", INDIRIZZO_PLC_TESTA);
                        bINDIRIZZO_PLC_TESTA = true;
                    }
                }

            }
            catch (Exception)
            {

                throw;
            }

            this.CPDManagerPLC = new CPD.CPDManager(connectionPLC);
            this.CPDManagerMovimentazioni = new CPD.CPDManager(connectionMovimentazioni);
            if (bINDIRIZZO_PLC_TESTA)
                this.MotoreTesta = new MotoreManager(this.CPDManagerMovimentazioni, INDIRIZZO_PLC_TESTA);

            if (bINDIRIZZO_PLC_LIVELLO)
                this.MotoreLivello = new MotoreManager(this.CPDManagerMovimentazioni, INDIRIZZO_PLC_LIVELLO);

            #endregion modificheMAX

            readTimer = new System.Timers.Timer();
            // lo imposto a 50 per avere una prima lettura immediata, poi lo metto a INTERVALLO_LETTURA
            readTimer.Interval = 50;
            readTimer.Elapsed += new System.Timers.ElapsedEventHandler(readTimer_Tick);
            readTimer.Start();
        }



        private void ScriviPlc(int plcAddress, int address, int value)
        {
            CPD.CPDManager CPDManager = null;

            if (plcAddress == INDIRIZZO_PLC_LIVELLO || plcAddress == INDIRIZZO_PLC_TESTA)
                CPDManager = CPDManagerMovimentazioni;
            else
                CPDManager = CPDManagerPLC;

            CPDManager.SendMessage(plcAddress, address, value);
        }

        private void ScriviPlc(int plcAddress, int[] address, int[] value)
        {
            CPD.CPDManager CPDManager = null;

            if (plcAddress == INDIRIZZO_PLC_LIVELLO || plcAddress == INDIRIZZO_PLC_TESTA)
                CPDManager = CPDManagerMovimentazioni;
            else
                CPDManager = CPDManagerPLC;

            CPDManager.SendMessage(plcAddress, address, value);
        }

        public int LeggiPlc(int plcAddress, int address)
        {
            CPD.CPDManager CPDManager = null;

            if (plcAddress == INDIRIZZO_PLC_LIVELLO || plcAddress == INDIRIZZO_PLC_TESTA)
                CPDManager = CPDManagerMovimentazioni;
            else
                CPDManager = CPDManagerPLC;

            return CPDManager.ReadMessage(plcAddress, address);
        }

        public int[] LeggiPlc(int plcAddress, int startAddress, int count)
        {
            CPD.CPDManager CPDManager = null;

            if (plcAddress == INDIRIZZO_PLC_LIVELLO || plcAddress == INDIRIZZO_PLC_TESTA)
                CPDManager = CPDManagerMovimentazioni;
            else
                CPDManager = CPDManagerPLC;

            return CPDManager.ReadMessage(plcAddress, startAddress, count);
        }

        public int[] LeggiPlc(int plcAddress, int[] address)
        {
            CPD.CPDManager CPDManager = null;

            if (plcAddress == INDIRIZZO_PLC_LIVELLO || plcAddress == INDIRIZZO_PLC_TESTA)
                CPDManager = CPDManagerMovimentazioni;
            else
                CPDManager = CPDManagerPLC;

            return CPDManager.ReadMessage(plcAddress, address);
        }


        public Dictionary<int, int> LeggiPlcDict(int plcAddress, int[] address)
        {
            CPD.CPDManager CPDManager = null;

            if (plcAddress == INDIRIZZO_PLC_MACCHINA || plcAddress == INDIRIZZO_PLC_ESPULSORE)
                CPDManager = CPDManagerPLC;
            else
                CPDManager = CPDManagerMovimentazioni;

            return CPDManager.ReadMessageDict(plcAddress, address);
        }



        public void Close()
        {
            this.CPDManagerPLC.Close();
            this.CPDManagerMovimentazioni.Close();
        }

        public void InviaRisultatoTappo(int valore)
        {
            this.ScriviPlc(INDIRIZZO_PLC_MACCHINA, WM0, valore);
        }

        public void InviaRisultatoLivello(int valore)
        {
            this.ScriviPlc(INDIRIZZO_PLC_MACCHINA, WM1, valore);
        }

        public void CambioRicetta(DataType.CPDParamFixed paramFixed, DataType.CPDParam param)
        {
            CambioRicettaMacchina(paramFixed, param);
            //CambioRicettaLivello(paramFixed, param);
            if (Properties.Settings.Default.UsaEspulsore)
                CambioRicettaEspulsore(paramFixed, param);

            try
            {
                DataType.SlittaParamFixed paramSlitta = DataType.SlittaParamFixed.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "SLITTA.xml"));
                paramSlitta = paramSlitta ?? new DataType.SlittaParamFixed();

                this.MotoreTesta.CambioRicetta(paramSlitta.OffsetTestaNastro);
            }
            catch (Exception) { }
        }

        private void CambioRicettaMacchina(DataType.CPDParamFixed paramFixed, DataType.CPDParam param)
        {
            double mm_to_impulsi = (paramFixed.ImpulsiEncoder * 4) / (paramFixed.DiametroPuleggia * Math.PI);

            int[] address = new int[] { WM26, WM27, WM28, WM29, WM30, WM31, WM20, WM21, WM22 };
            int[] value = new int[]
            {
                (int)(param.DistanzaCameraLivello * mm_to_impulsi)
                , (int)(param.DistanzaCameraTappo * mm_to_impulsi)
                , param.NumeroScartiConsecutivi
                , (int)(param.FiltroBottiglieContigue * mm_to_impulsi)
                , paramFixed.DiametroPuleggia
                , paramFixed.ImpulsiEncoder
                , (int)(paramFixed.Libero1 * mm_to_impulsi)
                , (int)(paramFixed.Libero2 * mm_to_impulsi)
                , paramFixed.Libero3
            };

            this.ScriviPlc(INDIRIZZO_PLC_MACCHINA, address, value);

            Thread.Sleep(100);

            // indica al plc di ricaricare i valori scritti
            InviaComando(Comandi.RICALCOLA);
        }

        //private void CambioRicettaLivello(DataType.CPDParamFixed paramFixed, DataType.CPDParam param)
        //{
        //    int[] address = new int[] { WM28 };
        //    int[] value = new int[] 
        //    {
        //        param.NumeroScartiConsecutivi
        //    };

        //    this.ScriviPlc(INDIRIZZO_PLC_LIVELLO, address, value);
        //}

        private void CambioRicettaEspulsore(DataType.CPDParamFixed paramFixed, DataType.CPDParam param)
        {
            int[] address = new int[] { WM11, WM12, WM13, WM14, WM15, WM16, WM17 };
            int[] value = new int[]
            {
                paramFixed.DistanzaFotocellulaOutLivello
                , paramFixed.DistanzaFotocellulaOutTappo
                , param.DurataAttivazionePaletta
                , paramFixed.DirezioneEspulsione
                , param.PaletteOnOff
                , paramFixed.ImpulsiEncoder
                , paramFixed.DiametroPuleggia
            };

            this.ScriviPlc(INDIRIZZO_PLC_ESPULSORE, address, value);

            Thread.Sleep(100);

            // indica al plc di ricaricare i valori scritti
            this.ScriviPlc(INDIRIZZO_PLC_ESPULSORE, WM1, 255);
        }

        private void InviaComando(Comandi comando)
        {
            this.ScriviPlc(INDIRIZZO_PLC_MACCHINA, WM25, (int)comando);
        }

        /*
        public void ResetPezziBuoniTot()
        {
            try
            {
                InviaComando(Comandi.RESET_TOT_BUONI);
                this.CntTotBuoni = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ResetPezziScartoTot()
        {
            try
            {
                InviaComando(Comandi.RESET_TOT_SCARTI);
                this.CntTotScarti = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ResetPezziBuoniLivello()
        {
            try
            {
                InviaComando(Comandi.RESET_BUONI_LIVELLO);
                this.CntPezziBuoniLivello = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ResetPezziScartoLivello()
        {
            try
            {
                InviaComando(Comandi.RESET_SCARTO_LIVELLO);
                this.CntPezziScartoLivello = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ResetPezziNoRispostaLivello()
        {
            try
            {
                InviaComando(Comandi.RESET_NO_RISPOSTA_LIVELLO);
                this.CntNoRispostaLivello = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ResetPezziBuoniTappo()
        {
            try
            {
                InviaComando(Comandi.RESET_BUONI_TAPPO);
                this.CntPezziBuoniTappo = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ResetPezziScartoTappo()
        {
            try
            {
                InviaComando(Comandi.RESET_SCARTO_TAPPO);
                this.CntPezziScartoTappo = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ResetPezziNoRispostaTappo()
        {
            try
            {
                InviaComando(Comandi.RESET_NO_RISPOSTA_TAPPO);
                this.CntNoRispostaTappo = 0;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }
        */

        public void AbilitaEspulsore()
        {
            try
            {
                InviaComando(Comandi.ABILITA_ESPULSORE);
                this.EspulsoreAbilitato = true;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void DisabilitaEspulsore()
        {
            try
            {
                InviaComando(Comandi.DISABILITA_ESPULSORE);
                this.EspulsoreAbilitato = false;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void SettaAbilitazioneControlli(bool tappo, bool livello)
        {
            int tappo_i = tappo ? 1 : 0;
            int livello_i = livello ? 1 : 0;

            int comando = (livello_i << 1) + tappo_i;

            this.ScriviPlc(INDIRIZZO_PLC_MACCHINA, WM24, (int)comando);
        }

        public void LeggiAbilitazioneControlli(out bool tappo, out bool livello)
        {
            int lettura = this.LeggiPlc(INDIRIZZO_PLC_MACCHINA, WM24);

            tappo = (lettura & 1) == 1;
            livello = (lettura & 2) == 2;
        }

        private void AggiornaTempi()
        {
            this.swTempoStop.Stop();
            this.swTempoStart.Stop();
            this.swTempoTroppoPieno.Stop();
            this.swTempoIngessoVuoto.Stop();

            if (this.CodiceStato.HasFlag(Stati.MACCHINA_IN_STOP_SENZA_ALLARMI))
            {
                this.swTempoStop.Start();
            }

            if (this.CodiceStato.HasFlag(Stati.MACCHINA_IN_MARCIA))
            {
                this.swTempoStart.Start();
            }

            if (this.CodiceStato.HasFlag(Stati.TROPPO_PIENO_IN_USCITA))
            {
                this.swTempoStart.Start();
                this.swTempoTroppoPieno.Start();
            }

            //if (this.CodiceStato.HasFlag(Stati.MANCANZA_PRODOTTO_IN_INGRESSO))
            //{
            //    this.swTempoStart.Start();
            //    this.swTempoIngessoVuoto.Start();
            //}
        }


        private bool letturaRidotta = true;
        private bool letturaPosizionamento = false;

        public void SetLetturaRidotta(bool letturaRidotta)
        {
            this.letturaRidotta = letturaRidotta;

            if (!letturaRidotta)
            {
                // lo imposto a 50 per avere lettura immediata poichè sto per entrare nella schermata dei contartori
                //, poi lo metto a INTERVALLO_LETTURA dento al timer
                readTimer.Interval = 50;
            }
        }

        public void SetLetturaPosizionamento(bool letturaPosizionamento)
        {
            this.letturaPosizionamento = letturaPosizionamento;

            if (letturaPosizionamento)
            {
                // lo imposto a 50 per avere lettura immediata poichè sto per entrare nella schermata dei contartori
                //, poi lo metto a INTERVALLO_LETTURA dento al timer
                readTimer.Interval = 50;
            }
        }



        private void readTimer_Tick(object sender, EventArgs e)
        {
            AggiornaDatiContatoriPlc();
        }

        public void AggiornaDatiContatoriPlc()
        {
            readTimer.Enabled = false;
            try
            {

#if !_Simulazione

                bool inMarciaOld = this.CodiceStato.HasFlag(Stati.MACCHINA_IN_MARCIA);

                if (letturaPosizionamento || this.inRegolazioneSlittaLivello)
                {
                    int[] address = new int[] { WM9 };

                    int[] lettura = this.CPDManagerPLC.ReadMessage(INDIRIZZO_PLC_MACCHINA, address);

                    this.CodiceStato = (Stati)lettura[0];
                }
                else
                {
                    if (!this.inRegolazioneSlittaLivello)
                    {
                        //int[] address = new int[] { WM9, WM10, WM11, WM12, WM13, WM14, WM17, WM18, WM5, WM23, WM30, WM31, WM32, WM33, WM44, WM19 };
                        int[] address = new int[] { WM9, WM10, WM11, WM12, WM13, WM14/*, WM17, WM18, WM5, WM23, WM15, WM16, WM4, WM8, WM19*/ };

                        int[] lettura = this.CPDManagerPLC.ReadMessage(INDIRIZZO_PLC_MACCHINA, address);

                        this.CodiceStato = (Stati)lettura[0];                                   //WM9
                        this.CodiceAllarme = (Allarmi)lettura[1];                               //WM10

                        this.ProduzioneOraria = (lettura[3] << 16) + lettura[2];                //DM11

                        this.EspulsoreAbilitato = (lettura[4] == 1);                            //WM13
                        this.SpegniTutto = (lettura[5] == 1);                                   //WM14

                        //this.CntPezziBuoniTappo = (lettura[7] << 16) + lettura[6];            //DM17
                        //this.CntPezziScartoTappo = lettura[8];                                //WM5

                        //this.CntNoRispostaTappo = lettura[9];                                 //WM23

                        //this.CntPezziBuoniLivello = (lettura[11] << 16) + lettura[10];        //DM15
                        //this.CntPezziScartoLivello = lettura[12];                             //WM4

                        //this.CntNoRispostaLivello = lettura[13];                              //WM8

                        //this.CntPezziScartoTappoPresenza = lettura[10];                       //WM30
                        //this.CntPezziScartoTappoSerraggio = lettura[11];                      //WM31
                        //this.CntPezziScartoTappoSerraggioStelvin = lettura[12];               //WM32
                        //this.CntPezziScartoTappoPiantaggio = lettura[13];                     //WM33
                        //this.CntPezziScartoTappoAnello = lettura[14];                         //WM34

                        //this.CodaRisultatiLivello = lettura[14];                              //WM19

                        //this.CodiceAllarme = CodiceAllarmeTappo | this.CodiceAllarmeLivello;
                    }

                    AggiornaTempi();

                }

                //bool inMarciaNew = this.CodiceStato.HasFlag(Stati.MACCHINA_IN_MARCIA);

                //if (inMarciaOld != inMarciaNew)
                //{
                //    if (inMarciaNew)
                //        InviaComandoSlitta(ComandiSlitta.START_PLC);
                //    else
                //        InviaComandoSlitta(ComandiSlitta.STOP_PLC);
                //}
#endif

            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                if (!letturaPosizionamento && !this.inRegolazioneSlittaLivello)
                    readTimer.Interval = INTERVALLO_LETTURA;
                else
                    readTimer.Interval = INTERVALLO_LETTURA_VELOCE;

                readTimer.Enabled = true;
            }
        }

        //        public void AggiornaDatiPlcLivello()
        //        {
        //            readTimer.Enabled = false;
        //            try
        //            {

        //#if !_Simulazione

        //                if (!this.inRegolazioneSlittaLivello)
        //                {
        //                    int[] address = new int[] { WM40, WM41, WM42, WM43, WM44, WM45, WM46, WM12 };

        //                    int[] lettura = this.CPDManagerMovimentazioni.ReadMessage(INDIRIZZO_PLC_LIVELLO, address);

        //                    this.CntPezziBuoniLivello = (lettura[1] << 16) + lettura[0];        //DM40
        //                    this.CntPezziScartoLivello = lettura[2];                            //WM42

        //                    this.CntNoRispostaLivello = lettura[3];                             //WM43

        //                    //this.CntPezziScartoLivelloMin = lettura[4];                       //WM44
        //                    //this.CntPezziScartoLivelloMax = lettura[5];                       //WM45
        //                    //this.CntPezziScartoLivelloEmpty = lettura[6];                     //WM46

        //                    this.CodiceAllarmeLivello = (Allarmi)lettura[7];                    //WM10

        //                    this.CodiceAllarme = CodiceAllarmeTappo | this.CodiceAllarmeLivello;
        //                }
        //                else
        //                {
        //                    int[] address = new int[] { WM120, WM121, WM63 };

        //                    int[] lettura = this.LeggiPlc(INDIRIZZO_PLC_LIVELLO, address);

        //                    //PosizioneSlitta = (lettura[1] << 16) + lettura[0];
        //                    LetturaLivello = lettura[2] == 1;
        //                }

        //#endif

        //            }
        //            catch (Exception ex)
        //            {
        //                ExceptionManager.AddException(ex);
        //            }
        //            finally
        //            {
        //                if (!letturaPosizionamento && !this.inRegolazioneSlittaLivello)
        //                    readTimer.Interval = INTERVALLO_LETTURA;
        //                else
        //                    readTimer.Interval = INTERVALLO_LETTURA_VELOCE;

        //                readTimer.Enabled = true;
        //            }
        //        }

        //--------------------------------------------------

        private bool inRegolazione = false;

        public bool IsInRegolazione()
        {
            return this.inRegolazione;
        }

        public void EnterRegolazioni()
        {
            try
            {
                InviaComando(Comandi.ENTRA_REGOLAZIONE);
                this.inRegolazione = true;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ExitRegolazioni()
        {
            try
            {
                InviaComando(Comandi.ESCI_REGOLAZIONE);
                this.inRegolazione = false;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        //--------------------------------------------------

        private bool inCambioFormatoSlitte = false;

        public bool IsInCambioFormatoSlitte()
        {
            return this.inCambioFormatoSlitte;
        }

        public void EnterCambioFormatoSlitte()
        {
            try
            {
                InviaComando(Comandi.ENTRA_CAMBIO_FORMATO_SLITTE);
                this.inCambioFormatoSlitte = true;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ExitCambioFormatoSlitte()
        {
            try
            {
                InviaComando(Comandi.ESCI_CAMBIO_FORMATO_SLITTE);
                this.inCambioFormatoSlitte = false;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        //--------------------------------------------------

        private bool inRegolazioneSlittaLivello = false;

        public bool IsInRegolazioneSlittaLivello()
        {
            return this.inRegolazioneSlittaLivello;
        }

        public void EnterRegolazioniSlittaLivello()
        {
            try
            {
                InviaComando(Comandi.ENTRA_REGOLAZIONE_SLITTA_CAMERE);
                this.MotoreLivello.EnterRegolazioni();
                this.inRegolazioneSlittaLivello = true;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ExitRegolazioniSlittaLivello()
        {
            try
            {
                InviaComando(Comandi.ESCI_REGOLAZIONE_SLITTA_CAMERE);
                this.MotoreLivello.ExitRegolazioni();
                this.inRegolazioneSlittaLivello = false;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        //--------------------------------------------------

        private bool inRegolazioneSlittaCamere = false;

        public bool IsInRegolazioneSlittaCamere()
        {
            return this.inRegolazioneSlittaCamere;
        }

        public void EnterRegolazioniSlittaCamere()
        {
            try
            {
                InviaComando(Comandi.ENTRA_REGOLAZIONE_SLITTA_CAMERE);
                // |MP  11-01-2019 SE SONO IN SMART NON SERVE LA REGOLAZIONE DELLA TESTA
                if (!Properties.Settings.Default.PRESENZA_SMART)
                    this.MotoreTesta.EnterRegolazioni();
                this.inRegolazioneSlittaCamere = true;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ExitRegolazioniSlittaCamere()
        {
            try
            {
                InviaComando(Comandi.ESCI_REGOLAZIONE_SLITTA_CAMERE);
                this.MotoreTesta.ExitRegolazioni();
                this.inRegolazioneSlittaCamere = false;
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        //--------------------------------------------------

        public void EnterCaricamentoRicetta()
        {
            try
            {
                InviaComando(Comandi.ENTRA_CARICAMENTO_RICETTA);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ExitCaricamentoRicetta()
        {
            try
            {
                InviaComando(Comandi.ESCI_CARICAMENTO_RICETTA);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }


        public void ResetAllarmi()
        {
            try
            {
                InviaComando(Comandi.RESET_ALLARMI);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }


        public void StartPLC()
        {
            try
            {
                InviaComando(Comandi.START_PLC);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void StopPLC()
        {
            try
            {
                InviaComando(Comandi.STOP_PLC);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }


        public bool IsPLCConnected(out List<int> notConnected)
        {
            notConnected = new List<int>();

            bool ok1 = CPDManagerPLC.IsNodeConnected(INDIRIZZO_PLC_MACCHINA, WM0);

            bool ok2 = true;
            /* |MP 10-1-2019  QUESTA PARTE è CAMBIATA PER FAR IN MODO CHE GLI INDIRIZZI PLC SIANO IN AUTOMATICO E NON CABLATI A CODICE
                        if (Properties.Settings.Default.UsaEspulsore)
                            ok2 = CPDManagerPLC.IsNodeConnected(INDIRIZZO_PLC_ESPULSORE, WM0);

                        bool ok3 = CPDManagerMovimentazioni.IsNodeConnected(INDIRIZZO_PLC_TESTA, WM0); ;
                        bool ok4 = CPDManagerMovimentazioni.IsNodeConnected(INDIRIZZO_PLC_LIVELLO, WM0); ;


                        if (!ok1)
                            notConnected.Add(INDIRIZZO_PLC_MACCHINA);
                        if (!ok2)
                            notConnected.Add(INDIRIZZO_PLC_ESPULSORE);
                        if (!ok3)
                            notConnected.Add(INDIRIZZO_PLC_TESTA);
                        if (!ok4)
                            notConnected.Add(INDIRIZZO_PLC_LIVELLO);
            */
            // |MP  10/01/2019  NUOVA SOLUZIONE 
            bool ok3 = true;
            bool ok4 = true;

            if (Properties.Settings.Default.UsaEspulsore && INDIRIZZO_PLC_ESPULSORE > 0)
                ok2 = CPDManagerPLC.IsNodeConnected(INDIRIZZO_PLC_ESPULSORE, WM0);

            if (INDIRIZZO_PLC_TESTA > 0)
                ok3 = CPDManagerMovimentazioni.IsNodeConnected(INDIRIZZO_PLC_TESTA, WM0); ;
            if (INDIRIZZO_PLC_LIVELLO > 0)
                ok4 = CPDManagerMovimentazioni.IsNodeConnected(INDIRIZZO_PLC_LIVELLO, WM0); ;


            if (!ok1)
                notConnected.Add(INDIRIZZO_PLC_MACCHINA);
            if (!ok2)
                notConnected.Add(INDIRIZZO_PLC_ESPULSORE);
            if (!ok3)
                notConnected.Add(INDIRIZZO_PLC_TESTA);
            if (!ok4)
                notConnected.Add(INDIRIZZO_PLC_LIVELLO);

            return ok1 && ok2 && ok3 && ok4;
        }





        /*
        private void InviaComandoSlitta(ComandiSlitta comando)
        {
            this.ScriviPlc(INDIRIZZO_PLC_LIVELLO, WM25, (int)comando);
        }

        public void AzzeraSlitta(int corsa)
        {
            int[] address = new int[] { WM64 };
            int[] value = new int[] { (int)(corsa * Properties.Settings.Default.RapportoEncoder_mmSlittino) };

            this.ScriviPlc(INDIRIZZO_PLC_LIVELLO, address, value);

            Thread.Sleep(100);

            InviaComandoSlitta(ComandiSlitta.AZZERAMENTO);
        }

        public void InviaPosizionamentoSlitta(int posizione)
        {
            int[] address = new int[] { WM122, WM123 };

            ushort posizioneH = (ushort)((int)(posizione) >> 16);
            ushort posizioneL = ((ushort)((int)(posizione) & 0xFFFF));

            int[] value = new int[] { posizioneL, posizioneH };

            this.ScriviPlc(INDIRIZZO_PLC_LIVELLO, address, value);

            Thread.Sleep(100);

            InviaComandoSlitta(ComandiSlitta.POSIZIONAMENTO);
        }

        public void AvviaRicercaLivello()
        {
            InviaComandoSlitta(ComandiSlitta.RICERCA_LIVELLO);
        }

        public void MuoviSlittaSu()
        {
            InviaComandoSlitta(ComandiSlitta.SALITA);
        }

        public void MuoviSlittaGiu()
        {
            InviaComandoSlitta(ComandiSlitta.DISCESA);
        }

        public void FermaSlitta()
        {
            InviaComandoSlitta(ComandiSlitta.STOPEMERGENZA);
        }

        public int GetPosizioneSlitta()
        {
            int[] address = new int[] { WM120, WM121 };

            int[] lettura = this.LeggiPlc(INDIRIZZO_PLC_LIVELLO, address);

            return (lettura[1] << 16) + lettura[0];
        }

        public bool InMovimento()
        {
            int valore = this.LeggiPlc(INDIRIZZO_PLC_LIVELLO, WM4);
            StatiSlitta sato = (StatiSlitta)valore;
            return sato.HasFlag(StatiSlitta.IN_MOVIMENTO);
        }

        public bool InPosizione()
        {
            int valore = this.LeggiPlc(INDIRIZZO_PLC_LIVELLO, WM4);
            StatiSlitta sato = (StatiSlitta)valore;
            return sato.HasFlag(StatiSlitta.IN_POSIZIONE);
        }

        public bool RicercaLivelloOK()
        {
            int valore = this.LeggiPlc(INDIRIZZO_PLC_LIVELLO, WM5);

            if (valore == 1)
            {
                this.ScriviPlc(INDIRIZZO_PLC_LIVELLO, WM5, 0);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool AzzeramentoOK()
        {
            int valore = this.LeggiPlc(INDIRIZZO_PLC_LIVELLO, WM5);

            if (valore == 2)
            {
                this.ScriviPlc(INDIRIZZO_PLC_LIVELLO, WM5, 0);
                return true;
            }
            else
            {
                return false;
            }
        }
        */
    }
}