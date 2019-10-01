using DigitalControl.DataType;
using DigitalControl.FW.Class;
using HalconDotNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ViewROI;

namespace DigitalControl.CMTL
{
    public partial class MainForm : Form
    {

        #region Variabili Private

        private int CORE_NUM; // = 2;  |MP 18-1-19 commentato il 2

        //public const int IDX_CORE_TAPPO = 1;   //con questo valore funziona il livello senza camera tappo, quindi impostato a 0 nella configurazione
        public const int IDX_CORE_TAPPO = 1;  //valore originario =0
        //public const int IDX_CORE_LIVELLO = 0; //con questo valore funziona il livello senza camera tappo, quindi impostato a 0 nella configurazione
        public const int IDX_CORE_LIVELLO = 0;//valore originario=1

        private readonly bool isSimulazione = false;

        private Utilities.FormAttesa frmAttesa { get; set; }

        private int id_formato;
        private string descrizioneFormato;

        private FrameGrabberManager[] frameGrabber = null;
        private Class.Core[] core = null;

        private Class.PlcMacchinaManager plcMacchina = null;
        private DataType.Contatori contatori = null;

        private IOManager mIOManager = null;

        private HWndCtrl viewControl1 = null;
        private HWndCtrl viewControl2 = null;
        private HWndCtrl viewControl3 = null;
        private HWndCtrl viewControl4 = null;
        private object repaintLock = new object();

        private Class.AlgoritmoControlloTappo algoritmoTappo = null;
        private Class.AlgoritmoControlloLivello algoritmoLivello = null;
        private DataType.PosizioniServoParam posizioniServoParam = null;
        private DataType.PosizioniSlittaParam posizioniSlittaParam = null;

        private DBL.LinguaManager linguaMngr = null;

        private SerialPort syncPort = null;
        private Class.SyncFotoManager syncFotoManagerTappo = null;
        private Class.SyncFotoManager syncFotoManagerLivello = null;

        private bool useDisplay = true;

        private int numeroCamere;

        private Class.PasswordManager pwdManager = null;

        // |MP x far in modo che venga eseguita sia livello che tappo
        bool bDaTappoALivello = true;
        bool bDaLivelloATappo = true;
        static public DataType.AlgoritmoControlloTappoParam datiFormato; // |MP 4-2-19 x mem i dati del formato


        #endregion Variabili Private

        #region Controllo Licenza

        private DateTime dataValidita;
        private DateTime dataErrore = DateTime.MaxValue;

        #endregion

        public MainForm(DateTime dataValidita, Utilities.FormAttesa formAttesaIniziale)
        {
            InitializeComponent();

            formAttesaIniziale.Close();

            //Attivo la gestione delle eccezioni
            ErrorHandler.ExceptionHandler.AddHandler(true, true, true);
            ExceptionManager.Init();

#if _Simulazione
            this.isSimulazione = true;
#endif

            this.pwdManager = new Class.PasswordManager();

            // |MP 11/01/2019  X LE NUOVE IMPOSTAZIONI
            /*
            if (!Properties.Settings.Default.LivelloDaCamera)
                this.CORE_NUM = 1;

            this.numeroCamere = Properties.Settings.Default.NumeroCamereTappo; // |MP  questi dati si trovano in DigitalControl.CMTL con il "file" con icona a chiave inglese Properties
            if (Properties.Settings.Default.LivelloDaCamera)
                this.numeroCamere += 1;
            */
            // |MP 11-01-19 se è una smart una sola camera di livello, se è cmtl nr camere tappo + livello tranne che abbia il sensore al posto della camera
            if (Properties.Settings.Default.PRESENZA_SMART)
            {
                this.numeroCamere = 1; //una camera sola fa il controllo tappo e livello
                this.CORE_NUM = 1 + Properties.Settings.Default.NumeroCamereTappo; //il nr fisso 1 è x il livello;  |MP
            }
            else
                if (!Properties.Settings.Default.PRESENZA_SENSORE_LIVELLO)
            {
                this.numeroCamere = (Properties.Settings.Default.NumeroCamereTappo) + 1;
                this.CORE_NUM = 1;  // |MP 1 XCHè GESTISCE LIVELLO
            }
            else
            {
                this.numeroCamere = Properties.Settings.Default.NumeroCamereTappo;          //se sensore
                this.CORE_NUM = 1 + Properties.Settings.Default.NumeroCamereTappo; //il nr fisso 1 è x il livello;  |MP
            }
            HSystem.SetSystem("width", 5000);
            HSystem.SetSystem("height", 5000);
            HSystem.SetSystem("do_low_error", "false");

            viewControl1 = new HWndCtrl(hMainWndCntrl1);
            /* ---quaaa   |MP 17/1-19  CREATO METODO PRIVATO "visualizzoElementiFormMain" X LA GESTIONE
            //viewControl2 = new HWndCtrl(hMainWndCntrl2);  //|MP  28-12-18
            //viewControl3 = new HWndCtrl(hMainWndCntrl3);  |MP  28-12-18
            //viewControl4 = new HWndCtrl(hMainWndCntrl4);  |MP  28-12-18
            Console.WriteLine("Properties.Settings.Default.DatiVisionePath: {0}", Properties.Settings.Default.DatiVisionePath);
            */
            DataType.ConfigurazioneCorrente confObj = DataType.ConfigurazioneCorrente.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

            linguaMngr = new DBL.LinguaManager(Properties.Settings.Default.DatiVisionePath);

            if (confObj != null)
                linguaMngr.ChangeLanguage(confObj.CodiceLingua);

            AdjustCulture();

            Utilities.CommonUtility.InitCommonUtility(linguaMngr, true);

            ucLedControlTappo.BackColor = Color.FromArgb(8, 93, 150);
            ucLedControlLivello.BackColor = Color.FromArgb(8, 93, 150);

            ucLedControlTappo.Init(Properties.Settings.Default.Direzione);
            ucLedControlLivello.Init(Properties.Settings.Default.Direzione);

            GestioneControlliLivello();

            this.dataValidita = dataValidita;

            if (DateTime.Now > this.dataValidita)
            {
                int ora = new Random(DateTime.Now.Millisecond).Next(0, 23);
                int minuti = new Random(DateTime.Now.Millisecond).Next(0, 59);

                dataErrore = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, ora, minuti, 0);

                TimeSpan ts = dataErrore.Subtract(DateTime.Now);

                if (ts.TotalHours < 5 && ts.TotalHours >= 0)
                {
                    dataErrore = DateTime.Now.AddHours(new Random(DateTime.Now.Millisecond).Next(5, 7));
                }

                if (dataErrore < DateTime.Now)
                {
                    dataErrore = dataErrore.AddDays(1);
                }
            }

            frmAttesa = new Utilities.FormAttesa();

            bwInizializazione.RunWorkerAsync();
            //|MP  04-01-19 aggiunto 
            try
            {
                frmAttesa.ShowDialog();
            }
            catch (Exception)
            {

                // throw;
            }

        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!this.isSimulazione)
            {
                if (Properties.Settings.Default.UsaSincronismo)
                {
                    this.syncFotoManagerTappo.OnSyncInpuntChange += syncFotoManagerTappo_OnSyncInpuntChange;

                    if (Properties.Settings.Default.LivelloDaCamera)
                        this.syncFotoManagerLivello.OnSyncInpuntChange += syncFotoManagerLivello_OnSyncInpuntChange;
                }
            }

            timerAggiornaStatoPLC.Enabled = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            try
            {
                timerAggiornaStatoPLC.Tick -= timerAggiornaStatoPLC_Tick;

                if (this.plcMacchina != null)
                    this.plcMacchina.StopPLC();

                StopAllCore();

                for (int j = 0; j < core.Length; j++)
                {
                    if (null != core[j])  // |MP 23-1-19 il controllo null  aggiunto xchè core[0] è null in quanto livello nel caso smart
                        core[j].CloseFrameGrabber();
                }

            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        private void GestioneControlliLivello()    //--- |MP controllare se serve
        {
            // per la gestione delle 3 o 2 cemere per il tappo
            if (this.numeroCamere == 3 && Properties.Settings.Default.LivelloDaCamera) // |MP  28-12-18
            {
                tableLayoutPanel2.Controls.Remove(hMainWndCntrl4);
                tableLayoutPanel2.Controls.Remove(lblCamera4);

                //tableLayoutPanel2.SetRowSpan(hMainWndCntrl2, 3);  |MP  28-12-18
            }
            else if (this.numeroCamere == 3 && !Properties.Settings.Default.LivelloDaCamera)
            {
                tableLayoutPanel2.Controls.Remove(hMainWndCntrl4);
                tableLayoutPanel2.Controls.Remove(lblCamera4);
            }   // |MP  28-12-18  

            if (!Properties.Settings.Default.LivelloDaCamera)
            {
                btnAbilitaControlli.Location = btnVisualizzaScartiLivello.Location;
                btnVisualizzaScartiLivello.Visible = false;
            }

        }

        private void AdjustCulture()
        {
            btnLingua.BackgroundImage = Image.FromFile(linguaMngr.GetBandierinaImg());

            lblCamera1.Text = string.Format(linguaMngr.GetTranslation("LBL_CAMERA_X"), 1);

            // per la gestione delle 3 o 2 camere per il tappo "dovrebbe servire x la traslazione della visualizzazione ??
            /*   if (00 == 3)   |MP   28-12-18
               {
                   //lblCamera2.Text = string.Format(linguaMngr.GetTranslation("LBL_CAMERA_X"), 2);  |MP   28-12-18
                   //lblCamera3.Text = string.Format(linguaMngr.GetTranslation("LBL_CAMERA_X"), 3);    |MP   28-12-18
               }
               else
               {
                   //lblCamera2.Text = string.Format(linguaMngr.GetTranslation("LBL_CAMERA_X"), 3);    |MP  28-12-18
                   //lblCamera3.Text = string.Format(linguaMngr.GetTranslation("LBL_CAMERA_X"), 2);    |MP   28-12-18
               } */

            // lblCamera4.Text = string.Format(linguaMngr.GetTranslation("LBL_CAMERA_X"), 4);            |MP  28-12-18


            visualizzoElementiFormMain(Properties.Settings.Default.NumeroCamereTappo);  //|MP  17-01-18

            lblContatoreBuoniLivelloDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_BUONI_LIVELLO");
            lblContatoreScartiLivelloDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_SCARTO_LIVELLO");
            lblLivello.Text = linguaMngr.GetTranslation("LBL_LIVELLO_PALLINI");


            lblProduzioneOrariaDescr.Text = linguaMngr.GetTranslation("LBL_PRODUZIONE_ORARIA");
        }

        // |MP verifico la presenza delle camera/e tappo, se lo sono attivo la visualizzazione degli elementi grafici corrispondenti altrementi la nego
        private void visualizzoElementiFormMain(int iCameraTappo)
        {
            if (Properties.Settings.Default.NumeroCamereTappo > 0)
            {
                ucLedControlTappo.Enabled = true;
                ucLedControlTappo.Visible = true;
                panel2.Visible = true;
                panel3.Visible = true;
                lblTappo.Text = linguaMngr.GetTranslation("LBL_TAPPO_PALLINI");
                lblContatoreBuoniTappoDescr.Visible = true;
                lblContatoreBuoniTappoDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_BUONI_TAPPO");
                lblContatoreScartiTappoDescr.Visible = true;
                lblContatoreScartiTappoDescr.Text = linguaMngr.GetTranslation("LBL_PEZZI_SCARTO_TAPPO");
                lblContatoreBuoniTappo.Visible = true;
                lblContatoreScartiTappo.Visible = true;

                switch (iCameraTappo)
                {
                    case 1:
                        ridimensionaFinestreCamera(iCameraTappo, Properties.Settings.Default.PRESENZA_SMART);
                        break;
                    case 2:
                        viewControl2 = new HWndCtrl(hMainWndCntrl2);
                        viewControl3 = new HWndCtrl(hMainWndCntrl3);
                        ridimensionaFinestreCamera(iCameraTappo, Properties.Settings.Default.PRESENZA_SMART);
                        break;
                    case 3:
                        viewControl2 = new HWndCtrl(hMainWndCntrl2);
                        viewControl3 = new HWndCtrl(hMainWndCntrl3);
                        viewControl4 = new HWndCtrl(hMainWndCntrl4);
                        ridimensionaFinestreCamera(iCameraTappo, Properties.Settings.Default.PRESENZA_SMART);
                        break;
                    default:
                        break;
                }
            }
            else
                //non esiste nessuna camera x il tappo
                ridimensionaFinestreCamera(0, Properties.Settings.Default.PRESENZA_SMART);
        }

        // |MP 17-1-19 in funzione del numero di camere tappo abilita e ridimensiona le celle del tablePanel 
        private void ridimensionaFinestreCamera(int nrCamere, bool bSmart)
        {
            switch (nrCamere)
            {
                case 0:
                case 1:
                    tableLayoutPanel2.Controls.Remove(hMainWndCntrl4);
                    tableLayoutPanel2.Controls.Remove(lblCamera4);
                    tableLayoutPanel2.Controls.Remove(hMainWndCntrl3);
                    tableLayoutPanel2.Controls.Remove(lblCamera3);
                    tableLayoutPanel2.Controls.Remove(hMainWndCntrl2);
                    tableLayoutPanel2.Controls.Remove(lblCamera2);
                    if (Properties.Settings.Default.NumeroCamereTappo > 0 && bSmart)
                    {   //SONIO SMART ctrl tappo NO ctrl livello separato
                        panel2.Visible = true;
                        panel3.Visible = true;
                        btnResetBuoniTappo.Visible = true;
                        btnResetScartiTappo.Visible = true;
                        lblTappo.Visible = true;
                        btnVisualizzaScartiLivello.Visible = false;
                        btnVisualizzaScartiLivello.Enabled = false;
                    }
                    else if (Properties.Settings.Default.NumeroCamereTappo == 0 && !bSmart)
                    {   //c'è il solo livello manca il ctrl tappo 
                        panel2.Visible = false;
                        panel3.Visible = false;
                        lblTappo.Visible = false;
                        btnResetBuoniTappo.Visible = false;
                        btnResetScartiTappo.Visible = false;
                        tableLayoutPanel1.Controls.Remove(ucLedControlTappo);
                        btnDatiDiProduzione.Location = new Point(13, 407);
                    }
                    else if (Properties.Settings.Default.NumeroCamereTappo > 0 && !bSmart)
                    {   //c'è il solo tappo manca il ctrl livello
                        panel2.Visible = false;
                        panel3.Visible = false;
                        lblTappo.Visible = false;
                        btnResetBuoniTappo.Visible = false;
                        btnResetScartiTappo.Visible = false;
                        tableLayoutPanel1.Controls.Remove(ucLedControlTappo);
                        btnDatiDiProduzione.Location = new Point(13, 407);
                    }

                    tableLayoutPanel2.SetColumnSpan(hMainWndCntrl1, 3);  //il nr di colonne e righe si vede andando sul tableLayoutPanel2 e cliccando sulla
                    tableLayoutPanel2.SetRowSpan(hMainWndCntrl1, 4);     //"freccia" alta dx con il pls Sx del mouse e poi "modifica righe e colonne.."
                    lblCamera1.Anchor = System.Windows.Forms.AnchorStyles.Right;
                    break;
                case 2:
                    tableLayoutPanel2.Controls.Remove(hMainWndCntrl4);
                    tableLayoutPanel2.Controls.Remove(lblCamera4);

                    tableLayoutPanel2.SetRowSpan(hMainWndCntrl2, 4);
                    break;
                case 3:

                    break;
                default:
                    break;
            }
        }

        private FrameGrabberConfig GetFrameGrabberConfig(bool simulazione, int idCamera)
        {
            //Recupera le impostazioni delle telecamere
            FrameGrabberConfig ret = null;

            string cameraFile = string.Empty;
            if (simulazione)
            {
                cameraFile = string.Format("FSimulazione_{0}.xml", (idCamera + 1));
            }
            else
            {
                cameraFile = string.Format("FTelecamera_{0}.xml", (idCamera + 1));
            }

            cameraFile = Path.Combine(Properties.Settings.Default.DatiVisionePath, cameraFile);

            if (File.Exists(cameraFile))
            {
                ret = FrameGrabberConfig.Deserialize(cameraFile);
            }
            return ret;
        }

        private Dictionary<string, string> GetSerialNumberSaperaLT()
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            HTuple valueList;
            string information = HInfo.InfoFramegrabber("SaperaLT", "device", out valueList);

            if (valueList.Type != HTupleType.EMPTY)
                if (valueList.SArr != null)
                    for (int i = 0; i < valueList.SArr.Length; i++)
                    {
                        HTuple fg = null;
                        try
                        {
                            string camera = valueList.SArr[i];
                            HOperatorSet.OpenFramegrabber("SaperaLT", 1, 1, 0, 0, 0, 0, "default", 8, "default", -1, "false", "", camera, -1, -1, out fg);
                            HTuple sn;
                            HOperatorSet.GetFramegrabberParam(fg, "DeviceSerialNumber", out sn);

                            if (sn != null && sn.S != null)
                                ret.Add(sn.S, camera);

                        }
                        finally
                        {
                            if (fg != null)
                                HOperatorSet.CloseFramegrabber(fg);
                        }
                    }

            return ret;
        }

        private FrameGrabberConfigOverride GetFrameGrabberConfigOverride(bool simulazione, int idCamera, int id_formato)
        {
            //Recupera le impostazioni delle telecamere
            FrameGrabberConfigOverride ret = null;

            if (!simulazione)
            {
                string cameraFile = string.Format("FTelecameraOverride_{0}.xml", (idCamera + 1));

                cameraFile = Path.Combine(Properties.Settings.Default.DatiVisionePath, "FORMATI", string.Format("F{0:0000}", id_formato), cameraFile);

                if (File.Exists(cameraFile))
                {
                    ret = FrameGrabberConfigOverride.Deserialize(cameraFile);
                }
            }

            return ret;
        }

        private void OnNewImage1(ArrayList iconicVarList, ElaborateResult result)
        {
            try
            {
                if (useDisplay)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            Utilities.CommonUtility.DisplayRegolazioni(iconicVarList, viewControl1, hMainWndCntrl1, repaintLock);
                            Utilities.CommonUtility.DisplayResult(result, hMainWndCntrl1, repaintLock);
                        }
                        catch (Exception ex)
                        {
                            ExceptionManager.AddException(ex);
                        }
                    }));
                }
                else
                {
                    if (iconicVarList != null)
                    {
                        for (int i = 0; i < iconicVarList.Count; i++)
                        {
                            if (iconicVarList[i] != null)
                                ((Utilities.ObjectToDisplay)iconicVarList[i]).Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }



        private void OnNewImage2(ArrayList iconicVarList, ElaborateResult result)
        {
            try
            {
                if (useDisplay)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            Utilities.CommonUtility.DisplayRegolazioni(iconicVarList, viewControl2, hMainWndCntrl2, repaintLock);
                            Utilities.CommonUtility.DisplayResult(result, hMainWndCntrl2, repaintLock);
                        }
                        catch (Exception ex)
                        {
                            ExceptionManager.AddException(ex);
                        }
                    }));
                }
                else
                {
                    if (iconicVarList != null)
                    {
                        for (int i = 0; i < iconicVarList.Count; i++)
                        {
                            if (iconicVarList[i] != null)
                                ((Utilities.ObjectToDisplay)iconicVarList[i]).Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void OnNewImage3(ArrayList iconicVarList, ElaborateResult result)
        {
            try
            {
                if (useDisplay)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            Utilities.CommonUtility.DisplayRegolazioni(iconicVarList, viewControl3, hMainWndCntrl3, repaintLock);
                            Utilities.CommonUtility.DisplayResult(result, hMainWndCntrl3, repaintLock);
                        }
                        catch (Exception ex)
                        {
                            ExceptionManager.AddException(ex);
                        }
                    }));
                }
                else
                {
                    if (iconicVarList != null)
                    {
                        for (int i = 0; i < iconicVarList.Count; i++)
                        {
                            if (iconicVarList[i] != null)
                                ((Utilities.ObjectToDisplay)iconicVarList[i]).Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }


        private void OnNewImage4(ArrayList iconicVarList, ElaborateResult result)
        {
            try
            {
                if (useDisplay)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            Utilities.CommonUtility.DisplayRegolazioni(iconicVarList, viewControl4, hMainWndCntrl4, repaintLock);
                            Utilities.CommonUtility.DisplayResult(result, hMainWndCntrl4, repaintLock);
                        }
                        catch (Exception ex)
                        {
                            ExceptionManager.AddException(ex);
                        }
                    }));
                }
                else
                {
                    if (iconicVarList != null)
                    {
                        for (int i = 0; i < iconicVarList.Count; i++)
                        {
                            if (iconicVarList[i] != null)
                                ((Utilities.ObjectToDisplay)iconicVarList[i]).Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void StopAllCore()
        {
            for (int i = 0; i < core.Length; i++)
                if (null != core[i])  // |MP il controllo null aggiunto xchè il core[0] è il livello e nella smart è null
                {
                    core[i].StopAndWaitEnd(true);
                    core[i].SetNewImageToDisplayEvent(null);
                }
        }

        private void PrepareRegolazioni()
        {
            for (int i = 0; i < core.Length; i++)
                if (null != core[i])
                    core[i].SetNewImageToDisplayEvent(null);
        }

        private void ExitRegolazioniNoReloadRicette()
        {
            Class.Core.OnNewImageToDisplayDelegate[] del = new Class.Core.OnNewImageToDisplayDelegate[this.numeroCamere];

            /*  if (Properties.Settings.Default.NumeroCamereTappo == 3)  |MP   28-12-18  C'è UNA SOLA CAMERA
              {  */
            //del[0] = OnNewImage1;                     //|MP 21-1-19
            del = popolaDEL(this.numeroCamere, del);    //|MP 21-1-19

            //core[IDX_CORE_LIVELLO].SetNewImageToDisplayEvent(OnNewImage1);

            //del[1] = OnNewImage2; |MP  28-12-18
            //del[2] = OnNewImage3; |MP   28-12-18
            /*    }     |MP   28-12-18  TANTO C'è UNA SOLA CAMERA
                else
                {
                    del[0] = OnNewImage1;
                    // del[1] = OnNewImage3; |MP   28-12-18
                } */



            if (Properties.Settings.Default.NumeroCamereTappo > 0)
                core[IDX_CORE_TAPPO].SetNewImageToDisplayEventArray(del);


            /* |MP  28-12-18
             * if (Properties.Settings.Default.LivelloDaCamera)
              {
                  if (Properties.Settings.Default.NumeroCamereTappo == 3)
                  {
                      core[IDX_CORE_LIVELLO].SetNewImageToDisplayEvent(OnNewImage4);
                  }
                /_*  else  |MP 28-12-18
                  {
                      core[IDX_CORE_LIVELLO].SetNewImageToDisplayEvent(OnNewImage2);
                  } 
              }  */
        }

        private void CaricaRicette(int id_formato)
        {
            int ricettaCaricata = id_formato;
            try
            {
                /*Leggo ed imposto gli algoritmi*/
                DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);
                DataType.AlgoritmoControlloTappoParam paramTappo = null;    // |MP 4-2-19
                bool esisteFormato = dbmFormati.EsisteFormato(id_formato);

                if (esisteFormato)
                {
                    paramTappo = GetAlgoritmoControlloTappoParam(dbmFormati, id_formato);   // |MP 4-2-19
                    descrizioneFormato = dbmFormati.GetDescrizioneFormato(id_formato);
                    //paramTappo.AbilitaControlloLivello
                    /*   if (paramTappo.AbilitaControlloLivello)  // |MP 4-2-19
                       {
                           hhh
                           //dbmFormati.Read(id_formato, IDX_CORE_TAPPO, paramTappo.);
                       }  */

                    try
                    {
                        /*Setto le telecamere*/
                        Console.WriteLine("frameGrabber.Length: {0}", frameGrabber.Length);
                        // |MP  09-01-19  aggiunto io x rendere automatico la selezione dei numeri delle camere
                        for (int i = 0; i < frameGrabber.Length; i++)
                        {
                            this.frameGrabber[i].SetOverrideParameter(GetFrameGrabberConfigOverride(this.isSimulazione, i, this.id_formato));
                        }


                        /*    
                           this.frameGrabber[0].SetOverrideParameter(GetFrameGrabberConfigOverride(this.isSimulazione, 0, this.id_formato));
                           this.frameGrabber[1].SetOverrideParameter(GetFrameGrabberConfigOverride(this.isSimulazione, 1, this.id_formato));    |MP  04-01-19
                           this.frameGrabber[2].SetOverrideParameter(GetFrameGrabberConfigOverride(this.isSimulazione, 2, this.id_formato));    

                            if (this.numeroCamere == 4 && this.frameGrabber[3] != null)
                                this.frameGrabber[3].SetOverrideParameter(GetFrameGrabberConfigOverride(this.isSimulazione, 3, this.id_formato)); */
                    }
                    catch (Exception ex)
                    {
                        ExceptionManager.AddException(ex);
                    }

                    #region Servomanopola

                    FormatoServo formatoServo = dbmFormati.ReadFormatoServo(id_formato);

                    this.posizioniServoParam = null;
                    if (formatoServo != null)
                        this.posizioniServoParam = DataType.PosizioniServoParam.Deserialize(formatoServo.XMLString);

                    this.posizioniServoParam = this.posizioniServoParam ?? new DataType.PosizioniServoParam();

                    #endregion Servomanopola

                    #region Slitte

                    FormatoSlitta formatoSlitta = dbmFormati.ReadFormatoSlitta(id_formato);

                    this.posizioniSlittaParam = null;
                    if (formatoSlitta != null)
                        this.posizioniSlittaParam = DataType.PosizioniSlittaParam.Deserialize(formatoSlitta.XMLString);

                    this.posizioniSlittaParam = this.posizioniSlittaParam ?? new DataType.PosizioniSlittaParam();

                    #endregion Slitte

                    #region PLC
                    FormatoCPD formatoCPD = dbmFormati.ReadFormatoCPD(id_formato);
                    DataType.CPDParam CPDParam = null;

                    if (formatoCPD != null)
                        CPDParam = DataType.CPDParam.Deserialize(formatoCPD.XMLString);

                    CPDParam = CPDParam ?? new DataType.CPDParam();

                    DataType.CPDParamFixed CPDParamFixed = DataType.CPDParamFixed.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "CPD.xml"));

                    CPDParamFixed = CPDParamFixed ?? new DataType.CPDParamFixed();

                    plcMacchina.CambioRicetta(CPDParamFixed, CPDParam);
                    #endregion PLC
                }
                else
                {
                    descrizioneFormato = string.Format(linguaMngr.GetTranslation("MSG_NO_RECIPE_2"), id_formato);
                    this.posizioniServoParam = null;
                    this.posizioniSlittaParam = null;

                    ricettaCaricata = 999;
                }

                //DataType.AlgoritmoControlloTappoParam paramTappo = null;
                DataType.AlgoritmoControlloLivelloParam paramLivello = null;

                #region Controllo Tappo
                // |MP 16/1-19  INSERITO X IL CONTROLLO PRESENZA CAMERE TAPPO
                if (Properties.Settings.Default.NumeroCamereTappo > 0)
                {
                    //paramTappo = GetAlgoritmoControlloTappoParam(dbmFormati, id_formato);

                    algoritmoTappo = new Class.AlgoritmoControlloTappo(this.linguaMngr);
                    algoritmoTappo.SetControlloTappoParam(paramTappo);

                    int[] rotazioni = new int[Properties.Settings.Default.NumeroCamereTappo];

                    for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
                    {
                        rotazioni[i] = dbmFormati.ReadRotazioneCamera(i + 1);
                    }

                    core[IDX_CORE_TAPPO].SetRotazione(rotazioni);
                    core[IDX_CORE_TAPPO].SetAlgorithm(algoritmoTappo.WorkingControlloTappoAlgorithm);

                    Class.Core.OnNewImageToDisplayDelegate[] del = new Class.Core.OnNewImageToDisplayDelegate[this.numeroCamere];

                    /*   if (Properties.Settings.Default.NumeroCamereTappo == 3)   |MP   28-12-18  C'è UNA SOLA CAMERA
                       {  */
                    //del[0] = OnNewImage1; |MP 21-1-19
                    //del[1] = OnNewImage2; |MP 28-12-18
                    //del[2] = OnNewImage3; |MP   28-12-18
                    /*    }     |MP   28-12-18  C'è UNA SOLA CAMERA
                        else
                        {
                            del[0] = OnNewImage1;
                            //del[1] = OnNewImage3; |MP   28-12-18
                        } */

                    // |MP 21-1-19
                    del = popolaDEL(this.numeroCamere, del);

                    core[IDX_CORE_TAPPO].SetNewImageToDisplayEventArray(del);
                }
                #endregion Controllo Tappo

                bool enableTappo = false; // |MP 11-01-19 true;
                bool enableLivello = false;

                #region Controllo Livello
                // |MP 23-01-19 aggiunto !Properties.Settings.Default.PRESENZA_SMART chè se è smart il livello è dentro al tappo xchè si fa tutto 
                // con una sola telecamera (quindi l'uso di un solo core)
                if (Properties.Settings.Default.LivelloDaCamera && !Properties.Settings.Default.PRESENZA_SMART)
                {
                    paramLivello = GetAlgoritmoControlloLivelloParam(dbmFormati, id_formato);

                    algoritmoLivello = new Class.AlgoritmoControlloLivello(this.linguaMngr);
                    algoritmoLivello.SetControlloLivelloParam(paramLivello);

                    int idCamLivello = this.numeroCamere;

                    core[IDX_CORE_LIVELLO].SetRotazione(dbmFormati.ReadRotazioneCamera(idCamLivello));

                    core[IDX_CORE_LIVELLO].SetAlgorithm(algoritmoLivello.WorkingControlloLivelloAlgorithm);
                    core[IDX_CORE_LIVELLO].SetNewImageToDisplayEvent(OnNewImage1);

                    /* |MP  28-12-18
                     * if (Properties.Settings.Default.NumeroCamereTappo == 3) // |MP <--- QUI ESSERE PARAMETRIZZATO
                        {
                            core[IDX_CORE_LIVELLO].SetNewImageToDisplayEvent(OnNewImage4);
                        } */
                    /*  else |MP 28-12-18
                      {
                          core[IDX_CORE_LIVELLO].SetNewImageToDisplayEvent(OnNewImage2);
                      } */
                    enableLivello = true;
                }
                #endregion Controllo Livello



                // |MP 11-01-19
                //if (paramTappo != null) ORIGINALE
                if (!Properties.Settings.Default.PRESENZA_SMART && paramTappo != null)
                    enableTappo = paramTappo.AbilitaPresenza || paramTappo.AbilitaSerraggio ||
                                    paramTappo.AbilitaSerraggioStelvin || paramTappo.AbilitaPiantaggio || paramTappo.AbilitaControlloAnello;

                if (paramLivello != null)
                {
                    if (Properties.Settings.Default.LivelloDaCamera && paramLivello != null)
                        enableLivello = paramLivello.AbilitaControllo;
                }

                plcMacchina.SettaAbilitazioneControlli(enableTappo, enableLivello);
                EseguiPrimaVolta();
            }
            catch (Exception ex)
            {
                ricettaCaricata = 999;
                ExceptionManager.AddException(ex);
            }
        }


        // |MP 21-1-19  metodo che popola array delle camere
        private Class.Core.OnNewImageToDisplayDelegate[] popolaDEL(int nrCamere, Class.Core.OnNewImageToDisplayDelegate[] del_)
        {
            del_[0] = OnNewImage1;
            if (this.numeroCamere == 2)
                del_[1] = OnNewImage2;
            else if (this.numeroCamere == 3)
            {
                del_[1] = OnNewImage2;
                del_[2] = OnNewImage3;
            }
            else if (this.numeroCamere == 4)
            {
                del_[1] = OnNewImage2;
                del_[2] = OnNewImage3;
                del_[3] = OnNewImage4;
            }

            return del_;
        }



        private void EseguiPrimaVolta()
        {
            try
            {
                DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

                int[] rotazioni = new int[4];
                Console.WriteLine("this.frameGrabber: {0}", this.frameGrabber.Length);
                //for (int i = 0; i < 3; i++)     // |MP 10/01/2019  IL 3 CORRISPONDE AL NR DI CAMERE, MA DEVE ESSERE UNA VARIABILE LEGATA ALLA PRESENZA REALE DELLE CAMERE
                for (int i = 0; i < this.frameGrabber.Length; i++)
                    rotazioni[i] = dbmFormati.ReadRotazioneCamera(i + 1);

                // Eseguo l' algoritmo la prima volta così da eliminare la prima foto lenta

                HImage[] img = new HImage[4];
                ArrayList[] iconicList;
                ElaborateResult[] result;

                //for (int i = 0; i < 3; i++)  // |MP 10/01/2019  IL 3 CORRISPONDE AL NR DI CAMERE, MA DEVE ESSERE UNA VARIABILE LEGATA ALLA PRESENZA REALE DELLE CAMERE
                for (int i = 0; i < this.frameGrabber.Length; i++)
                    img[i] = this.frameGrabber[i].GetImgFirsGrab().RotateImage(new HTuple(rotazioni[i]), "constant");

                if (Properties.Settings.Default.NumeroCamereTappo > 0)
                    // la eseguo 5 volte per sicurezza
                    for (int i = 0; i < 5; i++)
                        algoritmoTappo.WorkingControlloTappoAlgorithm(img, new System.Threading.CancellationToken(), out iconicList, out result);

            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void RicaricaRicetteAndStart()
        {
            this.plcMacchina.EnterCaricamentoRicetta();
            frmAttesa = new Utilities.FormAttesa();
            bwRicaricaRicette.RunWorkerAsync();
            frmAttesa.ShowDialog();
        }


        // |MP 01-02-19 FATTA SOLO PER LA SMART IN CUI LA SLITTA LIVELLO COINCIDE CON LA SLITTA DEL TAPPO
        private bool CheckForServoPositionSmart(DataType.PosizioniServoParam parametriServo, Class.PlcMacchinaManager plcMacchina, DataType.PosizioniSlittaParam parametriSlitta)
        {
            bool ret = true;
            double posizioneTappo = plcMacchina.MotoreTesta.GetPosizione();
            //double posizioneLivello = plcMacchina.MotoreLivello.GetPosizione();

            if (Math.Abs(parametriServo.PosizioneTappo.Value - posizioneTappo) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)
                //  || Math.Abs(parametriSlitta.PosizioneSlitta.Value - posizioneLivello) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)

                if (isCheckPosizioneTappoLivello(parametriServo, plcMacchina, parametriSlitta))
                {
                    //MessageBox.Show(linguaMngr.GetTranslation("MSG_SERVO_NON_IN_POSIZIONE"), linguaMngr.GetTranslation("MSG_ATTENZIONE"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    try
                    {
                        this.plcMacchina.EnterCambioFormatoSlitte();
                        System.Threading.Thread.Sleep(100);
                        this.plcMacchina.SetLetturaPosizionamento(true);

                        ret = new RegolazioniMeccaniche.FormPosizionamentoManuale(parametriServo, plcMacchina, parametriSlitta, this.linguaMngr).ShowDialog() == System.Windows.Forms.DialogResult.OK;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        this.plcMacchina.ExitCambioFormatoSlitte();
                        System.Threading.Thread.Sleep(100);
                        this.plcMacchina.SetLetturaPosizionamento(false);
                    }

                }
            return ret;
        }





        private bool CheckForServoPosition(DataType.PosizioniServoParam parametriServo, Class.PlcMacchinaManager plcMacchina, DataType.PosizioniSlittaParam parametriSlitta)
        {
            bool ret = true;
            // |MP 10-01-2019  eseguo il controllo delle altezze considerando la presenza della posizione tappo
            double posizioneTappo = plcMacchina.MotoreTesta.GetPosizione();
            double posizioneLivello = plcMacchina.MotoreLivello.GetPosizione();

            if (Math.Abs(parametriServo.PosizioneTappo.Value - posizioneTappo) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA
                || Math.Abs(parametriSlitta.PosizioneSlitta.Value - posizioneLivello) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)

                if (isCheckPosizioneTappoLivello(parametriServo, plcMacchina, parametriSlitta))
                {
                    //MessageBox.Show(linguaMngr.GetTranslation("MSG_SERVO_NON_IN_POSIZIONE"), linguaMngr.GetTranslation("MSG_ATTENZIONE"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    try
                    {
                        this.plcMacchina.EnterCambioFormatoSlitte();
                        System.Threading.Thread.Sleep(100);
                        this.plcMacchina.SetLetturaPosizionamento(true);

                        ret = new RegolazioniMeccaniche.FormPosizionamentoManuale(parametriServo, plcMacchina, parametriSlitta, this.linguaMngr).ShowDialog() == System.Windows.Forms.DialogResult.OK;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        this.plcMacchina.ExitCambioFormatoSlitte();
                        System.Threading.Thread.Sleep(100);
                        this.plcMacchina.SetLetturaPosizionamento(false);
                    }

                }
            return ret;
        }

        // |MP 10-01-2019  eseguo il controllo delle altezze considerando la presenza della posizione tappo
        private bool isCheckPosizioneTappoLivello(DataType.PosizioniServoParam parametriServo, Class.PlcMacchinaManager plcMacchina, DataType.PosizioniSlittaParam parametriSlitta)
        {
            bool bEsito = false;
            double posizioneTappo = 0d;
            double posizioneLivello = 0d;
            if (null != plcMacchina.MotoreTesta && Properties.Settings.Default.PRESENZA_SMART)
                posizioneTappo = plcMacchina.MotoreTesta.GetPosizione();
            if (null != plcMacchina.MotoreTesta && !Properties.Settings.Default.PRESENZA_SMART)
                posizioneTappo = plcMacchina.MotoreTesta.GetPosizione();
            if (null != plcMacchina.MotoreLivello && !Properties.Settings.Default.PRESENZA_SMART)
                posizioneLivello = plcMacchina.MotoreLivello.GetPosizione();

            // |MP sono nella condizione smart con controllo tappo e livello. Ma la slitta considerata
            //è quella del tappo (la slitta camera tappo e livello coincidono)
            if (posizioneTappo > 0 && Properties.Settings.Default.PRESENZA_SMART
                && Properties.Settings.Default.UsaControlloTappo) // posizioneTappo == 0) //c'è la presenza solo della camera livello QUINDI SMART
                if (Math.Abs(parametriServo.PosizioneTappo.Value - posizioneTappo) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)
                    bEsito = true;

            /*    if (Math.Abs(parametriSlitta.PosizioneSlitta.Value - posizioneTappo) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)
                    bEsito = true;
            */

            /*if (posizioneLivello > 0 && Properties.Settings.Default.PRESENZA_SMART) // posizioneTappo == 0) //c'è la presenza solo della camera livello QUINDI SMART
                if (Math.Abs(parametriSlitta.PosizioneSlitta.Value - posizioneLivello) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)
                    bEsito = true;*/

            /* IL IVELLO SEMPRE PRESENTE  if(posizioneLivello == 0 && posizioneTappo > 0) //c'è la presenza solo della camera tappo
                  if (Math.Abs(parametriServo.PosizioneTappo.Value - posizioneTappo) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)
                      bEsito = true; */

            if (posizioneLivello > 0 && posizioneTappo > 0 && !Properties.Settings.Default.PRESENZA_SMART) //c'è la presenza contemporanea delle camere TAPPO E LIVELLO
                if (Math.Abs(parametriServo.PosizioneTappo.Value - posizioneTappo) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA
                    || Math.Abs(parametriSlitta.PosizioneSlitta.Value - posizioneLivello) > Class.PlcMacchinaManager.TOLLERANZA_POSIZIONAMENTO_SLITTA)
                    bEsito = true;

            return bEsito;
        }


        private void CheckForRunAndRun()
        {
            List<int> notConnected = null;
            // |MP 15-1-19 FATTO QUESTO XCHè CON INSERIMENTO CAMERA VA IN "CONFLITTO" AVVISANDO notConnected=NULL OVVIO VISTO LA RIGA SOPRA
            //MA IL METODO USA (OUT...) QUINDI NON DOVREBBE ALZARE ALLARME
            bool plcOk = this.plcMacchina.IsPLCConnected(out notConnected);   // |MP   RIGA ORIGINALE CHE DOVREBBE ESSERE OK
            //bool plcOk = false;   // |MP  
            try
            {
                notConnected = new List<int>();
                //plcMacchina è null!!!
                plcOk = this.plcMacchina.IsPLCConnected(out notConnected);
            }
            catch (Exception)
            {
                //è una schifezza
                //throw;
            }

            if (!plcOk)
            {
                string plcList = string.Empty;

                // |MP  15/01/19 AGGIUNTO RIGA if (null != notConnected)  XCHè notConnected è NULL
                if (null != notConnected)
                {
                    for (int i = 0; i < notConnected.Count; i++)
                    {
                        if (i == 0)
                            plcList = notConnected[i].ToString("X");
                        else
                            plcList = string.Format("{0}, {1}", plcList, notConnected[i].ToString("X"));
                    }
                }

                MessageBox.Show(string.Format(linguaMngr.GetTranslation("MSG_CONN_PLC"), plcList), linguaMngr.GetTranslation("MSG_ERRORE"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (Properties.Settings.Default.PRESENZA_SMART)
                if (this.posizioniServoParam != null && this.posizioniServoParam.PosizioneTappo.HasValue && this.posizioniSlittaParam != null)
                {
                    if (CheckForServoPositionSmart(this.posizioniServoParam, this.plcMacchina, this.posizioniSlittaParam))
                    {
                        // BOOO ERA COSI'
                    }
                }
                else
                {
                    MessageBox.Show(linguaMngr.GetTranslation("MSG_SERVO_NO_POSIZIONE"), linguaMngr.GetTranslation("MSG_ERRORE"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            if (!Properties.Settings.Default.PRESENZA_SMART)
                if (this.posizioniServoParam != null && this.posizioniServoParam.PosizioneTappo.HasValue && this.posizioniSlittaParam != null && this.posizioniSlittaParam.PosizioneSlitta.HasValue)
                {
                    if (CheckForServoPosition(this.posizioniServoParam, this.plcMacchina, this.posizioniSlittaParam))
                    {
                        // BOOO ERA COSI'
                    }
                }
                else
                {
                    MessageBox.Show(linguaMngr.GetTranslation("MSG_SERVO_NO_POSIZIONE"), linguaMngr.GetTranslation("MSG_ERRORE"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }



            //if (this.plcMacchina != null && !this.plcMacchina.EspulsoreAbilitato)
            //{
            //    if (MessageBox.Show(linguaMngr.GetTranslation("MSG_ESPULSORE_DISABILITATO_ABILITARE"), linguaMngr.GetTranslation("MSG_ATTENZIONE"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            //    {
            //        this.plcMacchina.AbilitaEspulsore();
            //    }
            //}

            if (Properties.Settings.Default.NumeroCamereTappo > 0)     // |MP 16-1  INSERITO
                core[IDX_CORE_TAPPO].Run();

            Console.WriteLine("Properties.Settings.Default.LivelloDaCamera: {0}", Properties.Settings.Default.LivelloDaCamera);

            //if (Properties.Settings.Default.LivelloDaCamera)
            //    core[IDX_CORE_LIVELLO].Run();

            timerExitRegolazioni.Start();

        }

        private DataType.AlgoritmoControlloTappoParam GetAlgoritmoControlloTappoParam(DBL.FormatoManager dbmFormati, int id_formato)
        {
            Formato formatoTappo = dbmFormati.Read(id_formato, IDX_CORE_TAPPO, TipiAlgoritmo.AlgoritmoTappo);

            DataType.AlgoritmoControlloTappoParam paramTappo = null;

            if (formatoTappo != null)
                paramTappo = DataType.AlgoritmoControlloTappoParam.Deserialize(formatoTappo.XMLString);

            //paramTappo = paramTappo ?? new DataType.AlgoritmoControlloTappoParam();

            return paramTappo;
        }

        private DataType.AlgoritmoControlloLivelloParam GetAlgoritmoControlloLivelloParam(DBL.FormatoManager dbmFormati, int id_formato)
        {
            Formato formatoLivello = dbmFormati.Read(id_formato, IDX_CORE_LIVELLO, TipiAlgoritmo.AlgoritmoLivello);

            DataType.AlgoritmoControlloLivelloParam paramLivello = null;

            if (formatoLivello != null)
                paramLivello = DataType.AlgoritmoControlloLivelloParam.Deserialize(formatoLivello.XMLString);

            //paramLivello = paramLivello ?? new DataType.AlgoritmoControlloLivelloParam();

            return paramLivello;
        }

        #region Eventi form

        private void btnChiudi_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnLingua_Click(object sender, EventArgs e)
        {
            try
            {
                DataType.ConfigurazioneCorrente obj = DataType.ConfigurazioneCorrente.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));
                Utilities.FormSelezioneLingua frm = new Utilities.FormSelezioneLingua(this.linguaMngr, obj.LinguaList);

                if (frm.ShowDialog() == DialogResult.OK)
                {
                    obj.CodiceLingua = frm.LinguaSelezionata;
                    DataType.ConfigurazioneCorrente.Serialize(obj, Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

                    linguaMngr.ChangeLanguage(obj.CodiceLingua);

                    AdjustCulture();

                    lblRicettaCorrente.Text = string.Format(linguaMngr.GetTranslation("LBL_RICETTA_CORRENTE"), this.descrizioneFormato);

                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnMenuHome_Click(object sender, EventArgs e)
        {
            try
            {
                useDisplay = false;
                PrepareRegolazioni();

                new FormMenuHome(this.core, this.plcMacchina, this.pwdManager, this.linguaMngr, this.repaintLock).ShowDialog();

            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                RicaricaRicetteAndStart();
                useDisplay = true;
            }
        }

        private void btnDatiDiProduzione_Click(object sender, EventArgs e)
        {
            try
            {

                useDisplay = false;

                //this.plcMacchina.SetLetturaRidotta(false);

                new FormDatiDiProduzione(this.plcMacchina, this.contatori, this.linguaMngr).ShowDialog();
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                //this.plcMacchina.SetLetturaRidotta(true);

                useDisplay = true;
            }
        }

        private void btnVisualizzaScartiTappo_Click(object sender, EventArgs e)
        {
            try
            {
                useDisplay = false;
                PrepareRegolazioni();

                FormVisualizzaScarti frmVisualizzaScarti = null;

                lock (repaintLock)
                {
                    frmVisualizzaScarti = new FormVisualizzaScarti(core[IDX_CORE_TAPPO].GetLastErrorsClone(), this.linguaMngr, this.repaintLock);
                    frmVisualizzaScarti.ShowDialog();
                }

            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                ExitRegolazioniNoReloadRicette();
                useDisplay = true;
            }
        }

        private void btnAbilitaControlli_Click(object sender, EventArgs e)
        {
            try
            {
                DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

                bool esisteFormato = dbmFormati.EsisteFormato(id_formato);
                if (esisteFormato)
                {
                    bool ok = this.pwdManager.CanOpen(Class.PasswordManager.LivelloPassword.Tecnico, this.linguaMngr);

                    if (ok)
                    {

                        DataType.AlgoritmoControlloTappoParam paramTappo = GetAlgoritmoControlloTappoParam(dbmFormati, id_formato);
                        DataType.AlgoritmoControlloLivelloParam paramLivello = null;

                        if (Properties.Settings.Default.LivelloDaCamera)
                        {
                            paramLivello = GetAlgoritmoControlloLivelloParam(dbmFormati, id_formato);
                        }

                        // apro la form che salva le abilitazioni / disabilitazioni degli algoritmi
                        new FormAbilitaControlli(this.id_formato, paramTappo, paramLivello, this.linguaMngr).ShowDialog();

                        // ricarico i valori salvati con le varie abilitazioni / disabilitazioni
                        paramTappo = GetAlgoritmoControlloTappoParam(dbmFormati, id_formato);

                        algoritmoTappo.SetControlloTappoParam(paramTappo);

                        if (Properties.Settings.Default.LivelloDaCamera)
                        {
                            paramLivello = GetAlgoritmoControlloLivelloParam(dbmFormati, id_formato);

                            algoritmoLivello.SetControlloLivelloParam(paramLivello);
                        }

                        bool enableTappo = paramTappo.AbilitaPresenza || paramTappo.AbilitaSerraggio || paramTappo.AbilitaSerraggioStelvin || paramTappo.AbilitaPiantaggio || paramTappo.AbilitaControlloAnello;
                        bool enableLivello = true;

                        if (Properties.Settings.Default.LivelloDaCamera && paramLivello != null)
                        {
                            enableLivello = paramLivello.AbilitaControllo;
                        }

                        plcMacchina.SettaAbilitazioneControlli(enableTappo, enableLivello);

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
        }

        private void btnAbilitaEspulsore_Click(object sender, EventArgs e)
        {
            try
            {
                bool ok = this.pwdManager.CanOpen(Class.PasswordManager.LivelloPassword.Tecnico, this.linguaMngr);

                if (ok)
                {
                    if (this.plcMacchina != null)
                    {
                        if (this.plcMacchina.EspulsoreAbilitato)
                        {
                            this.plcMacchina.DisabilitaEspulsore();
                        }
                        else
                        {
                            this.plcMacchina.AbilitaEspulsore();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnResetBuoniTappo_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.contatori != null)
                {
                    this.contatori.ResetPezziBuoniTappo();
                    plcMacchina.InviaRisultatoTappo(1);  // |MP x PROVARE POI TOGLIERE!!!
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


        private void btnResetBuoniLivello_Click_1(object sender, EventArgs e)
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


        private void btnResetScartiLivello_Click_1(object sender, EventArgs e)
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


        #endregion Eventi form

        #region BackgroundWorker

        private void bwInizializazione_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);

                bwInizializazione.ReportProgress(0, linguaMngr.GetTranslation("MSG_LOAD_CONFIG"));

                #region Leggo configurazioni

                DataType.ConfigurazioneCorrente confObj = DataType.ConfigurazioneCorrente.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

                if (confObj != null)
                {
                    id_formato = confObj.IdFormato;
                }
                else
                {
                    id_formato = 1;
                    confObj = new DataType.ConfigurazioneCorrente() { IdFormato = 1, CodiceLingua = "it" };
                }

                DataType.ConfigurazioneCorrente.Serialize(confObj, Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

                #endregion

                #region Leggo contatori

                this.contatori = DataType.Contatori.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "Contatori.xml"));

                if (this.contatori == null)
                    this.contatori = new DataType.Contatori();

                #endregion

                bwInizializazione.ReportProgress(1, linguaMngr.GetTranslation("MSG_INIT_CAMERE"));

                #region Inizializzo le camere
                FrameGrabberConfig[] frameGrabberConfig = new FrameGrabberConfig[this.numeroCamere];
                frameGrabberConfig[0] = GetFrameGrabberConfig(this.isSimulazione, 0);
                /*    frameGrabberConfig[1] = GetFrameGrabberConfig(this.isSimulazione, 1);     |MP 28-12-18
                    frameGrabberConfig[2] = GetFrameGrabberConfig(this.isSimulazione, 2); */

                if (this.numeroCamere == 4)
                    frameGrabberConfig[3] = GetFrameGrabberConfig(this.isSimulazione, 3);

                Dictionary<string, string> serialNumberSaperaLT = GetSerialNumberSaperaLT();
                for (int i = 0; i < frameGrabberConfig.Length; i++)
                {
                    if (frameGrabberConfig[i] != null)
                        if (frameGrabberConfig[i].Name.ToUpper().Contains("SAPERALT"))
                            if (serialNumberSaperaLT.ContainsKey(frameGrabberConfig[i].Device))
                                frameGrabberConfig[i].Device = serialNumberSaperaLT[frameGrabberConfig[i].Device];
                }

                frameGrabber = new FrameGrabberManager[this.numeroCamere];

                for (int i = 0; i < frameGrabber.Length; i++)
                //Parallel.For(0, frameGrabber.Length, i =>
                {
                    try
                    {
                        //bwInizializazione.ReportProgress(1, string.Format(linguaMngr.GetTransString("MSG_INIT_CAMERA_X"), (i + 1)));
                        frameGrabber[i] = new FrameGrabberManager(frameGrabberConfig[i]);
                    }
                    catch (Exception ex)
                    {
                        ExceptionManager.AddException(ex);
                        MessageBox.Show(string.Format(linguaMngr.GetTranslation("ERR_CAMERA_NOT_FOUND"), i + 1), linguaMngr.GetTranslation("MSG_ERRORE"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }//);
                #endregion

                bwInizializazione.ReportProgress(3, linguaMngr.GetTranslation("MSG_INIT_PLC"));

                #region Inizializzo PLC

                this.plcMacchina = new Class.PlcMacchinaManager(confObj.PlcConnectionTappo, confObj.PlcConnectionLivello);
                if (this.plcMacchina != null)
                    this.plcMacchina.StartPLC();

                #endregion Inizializzo PLC

                bwInizializazione.ReportProgress(4, linguaMngr.GetTranslation("MSG_INIT_CORE"));

                #region Inizializzo i Core

                if (!this.isSimulazione && Properties.Settings.Default.UsaSincronismo)
                {
                    this.syncPort = new SerialPort();
                    this.syncPort.PortName = confObj.SyncConnection.PortName;
                    this.syncPort.BaudRate = confObj.SyncConnection.BaudRate;
                    this.syncPort.DataBits = confObj.SyncConnection.DataBits;
                    this.syncPort.StopBits = confObj.SyncConnection.StopBits;
                    this.syncPort.Handshake = Handshake.None;
                    this.syncPort.Parity = confObj.SyncConnection.Parity;

                    this.syncPort.DtrEnable = true; // alimentazione
                    this.syncPort.Open();

                    this.syncFotoManagerTappo = new Class.SyncFotoManager(syncPort, SerialPinChange.CtsChanged);
                    this.syncFotoManagerLivello = new Class.SyncFotoManager(syncPort, SerialPinChange.CDChanged);

                }
                this.mIOManager = new IOManager();
                core = new Class.Core[CORE_NUM];
                /************************************ GESTIONE CAMERE TAPPO **********************************/
                if (Properties.Settings.Default.NumeroCamereTappo > 0)
                {
                    FrameGrabberManager[] fgm = new FrameGrabberManager[Properties.Settings.Default.NumeroCamereTappo];
                    int[] rotazioni = new int[Properties.Settings.Default.NumeroCamereTappo];

                    for (int i = 0; i < Properties.Settings.Default.NumeroCamereTappo; i++)
                    {
                        fgm[i] = frameGrabber[i];
                        rotazioni[i] = dbmFormati.ReadRotazioneCamera(i + 1);
                    }

                    core[IDX_CORE_TAPPO] = new Class.Core(fgm, plcMacchina, rotazioni, this.mIOManager, 0, 1);
                    core[IDX_CORE_TAPPO].SetErrorFolderName("TAPPO");
                    //se sono in simulazione non uso il sincronismo
                    if (Properties.Settings.Default.UsaSincronismo)
                    {
                        if (!isSimulazione)
                        {
                            core[IDX_CORE_TAPPO].SetSyncManager(syncFotoManagerTappo);
                        }
                    }
                    else
                    {
                        core[IDX_CORE_TAPPO].OnFineElaborazione += MainForm_OnFineElaborazioneTappo;
                    }
                }
                /************************************** GESTIONE CAMERE LIVELLO *********************************/
                /*
                if (Properties.Settings.Default.LivelloDaCamera)
                {
                    int idCamLivello = this.numeroCamere;

                    int rotazione = dbmFormati.ReadRotazioneCamera(idCamLivello);
                    core[IDX_CORE_LIVELLO] = new Class.Core(frameGrabber[idCamLivello - 1], plcMacchina, rotazione, this.mIOManager, 1);
                    core[IDX_CORE_LIVELLO].SetErrorFolderName("LIVELLO");
                    //se sono in simulazione non uso il sincronismo
                    if (Properties.Settings.Default.UsaSincronismo)
                    {
                        if (!isSimulazione)
                        {
                            core[IDX_CORE_LIVELLO].SetSyncManager(syncFotoManagerLivello);
                        }
                    }
                    else
                    {
                        core[IDX_CORE_LIVELLO].OnFineElaborazione += MainForm_OnFineElaborazioneLivello;
                    }
                }
                */
                /**************************************************************************************/

                #endregion

                bwInizializazione.ReportProgress(5, linguaMngr.GetTranslation("MSG_LOAD_AND_START"));

                #region Avvio i core

                CaricaRicette(id_formato);

                #endregion

            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void bwRicaricaRicette_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                bwRicaricaRicette.ReportProgress(0, linguaMngr.GetTranslation("MSG_LOAD_CONFIG"));

                #region Leggo configurazioni

                DataType.ConfigurazioneCorrente confObj = DataType.ConfigurazioneCorrente.Deserialize(Path.Combine(Properties.Settings.Default.DatiVisionePath, "ConfigurazioneCorrente.xml"));

                id_formato = confObj.IdFormato;

                #endregion

                bwRicaricaRicette.ReportProgress(1, linguaMngr.GetTranslation("MSG_LOAD_AND_START"));

                #region Carico le ricette

                CaricaRicette(id_formato);

                #endregion
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            frmAttesa.Text = e.UserState as string;
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            frmAttesa.Close();
            frmAttesa.Dispose();

            lblRicettaCorrente.Text = string.Format(linguaMngr.GetTranslation("LBL_RICETTA_CORRENTE"), this.descrizioneFormato);

            CheckForRunAndRun();
        }

        #endregion BackgroundWorker

        private void timerAggiornaStatoPLC_Tick(object sender, EventArgs e)
        {
            timerAggiornaStatoPLC.Enabled = false;
            try
            {
                if (this.plcMacchina != null)
                {
                    if ((this.plcMacchina.CodiceAllarme != Class.PlcMacchinaManager.Allarmi.NO_ALLARMI || DateTime.Now >= this.dataErrore) && !plcMacchina.IsInRegolazione())
                    {
                        if (useDisplay)
                        {
                            try
                            {

                                if (DateTime.Now >= this.dataErrore)
                                {
                                    this.plcMacchina.DisabilitaEspulsore();
                                }

                                //useDisplay = false;
                                //PrepareRegolazioni();

                                new FormAllarmePLC(plcMacchina, DateTime.Now >= this.dataErrore, this.linguaMngr).ShowDialog();
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            finally
                            {
                                //ExitRegolazioniNoReloadRicette();
                                //useDisplay = true;
                            }
                        }
                    }

                    if (this.plcMacchina.EspulsoreAbilitato)
                        btnAbilitaEspulsore.BackgroundImage = Properties.Resources.img_espulsore_attivo;
                    else
                        btnAbilitaEspulsore.BackgroundImage = Properties.Resources.img_espulsore_disabilitato;

                    if (this.plcMacchina.CodiceStato.HasFlag(Class.PlcMacchinaManager.Stati.MACCHINA_IN_STOP_SENZA_ALLARMI))
                        pictureBoxLed.BackgroundImage = Properties.Resources.img_led_rosso;
                    else
                        pictureBoxLed.BackgroundImage = Properties.Resources.img_led_verde;

                    lblContatoreBuoniLivello.Text = this.contatori.CntPezziBuoniLivello.ToString();   // |MP  28-12-18
                    lblContatoreScartiLivello.Text = this.contatori.CntPezziScartoLivello.ToString(); // |MP  28-12-18
                    if (Properties.Settings.Default.NumeroCamereTappo > 0)      // |MP  18-1-18
                    {
                        lblContatoreBuoniTappo.Text = this.contatori.CntPezziBuoniTappo.ToString();
                        lblContatoreScartiTappo.Text = this.contatori.CntPezziScartoTappo.ToString();
                    }
                    lblProduzioneOraria.Text = this.plcMacchina.ProduzioneOraria.ToString();

                    if (lastResTappo.HasValue)
                    {
                        ucLedControlTappo.DisplayShift(lastResTappo);
                        lastResTappo = null;
                    }

                    if (!Properties.Settings.Default.LivelloDaCamera)
                    {
                        BitArray codaRisultatiLivello = new BitArray(new int[] { this.plcMacchina.CodaRisultatiLivello });
                        bool[] codaRisultatiLivelloBool = new bool[codaRisultatiLivello.Length];

                        for (int i = 0; i < codaRisultatiLivello.Length; i++)
                        {
                            codaRisultatiLivelloBool[i] = codaRisultatiLivello[i];
                        }

                        this.BeginInvoke(new Action(() =>
                        {
                            ucLedControlLivello.DisplayShift(codaRisultatiLivelloBool);
                        }));
                    }
                    else
                    {
                        if (lastResLivello.HasValue)
                        {
                            ucLedControlLivello.DisplayShift(lastResLivello);
                            lastResLivello = null;
                        }
                    }

                    if (this.plcMacchina.SpegniTutto)
                    {
                        if (Properties.Settings.Default.SpegniPC_da_PLC)
                            Process.Start("shutdown", "/f /s /t 10");
                        Application.Exit();
                    }

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

        private void timerAggiornaStatistiche_Tick(object sender, EventArgs e)
        {
            timerAggiornaStatistiche.Enabled = false;
            try
            {
                if (contatori != null && plcMacchina != null)
                {
                    DBL.StatisticheManager dbmStatistiche = new DBL.StatisticheManager(Properties.Settings.Default.ConnectionStringStatistiche);
                    StatisticheProduzione prodStat = new StatisticheProduzione();
                    prodStat.IdFormato = this.id_formato;
                    prodStat.IdCore = -1;
                    // |MP 4-2-19 questa if serve x trovare il nr pz da visualizzare, si sceglie il valor min tra i 2
                    //conteggi in quanto corrisponde ad entrambi i pz  (livello e tappo ok)
                    DataType.AlgoritmoControlloTappoParam paramTappo = null;
                    DBL.FormatoManager dbmFormati = new DBL.FormatoManager(Properties.Settings.Default.ConnectionStringFormati);
                    paramTappo = GetAlgoritmoControlloTappoParam(dbmFormati, id_formato);
                    //if (null != datiFormato && !datiFormato.AbilitaControlloLivello)
                    if (null != paramTappo && !paramTappo.AbilitaControlloLivello)
                    {
                        prodStat.PzBuoni = this.contatori.CntPezziBuoniTappo;
                        prodStat.Scarto = this.contatori.CntPezziScartoTappo;
                    }
                    else if (null != paramTappo && paramTappo.AbilitaControlloLivello)
                    {
                        StatisticheProduzione prodStatTMP = showDatiStatistiche(prodStat);
                        prodStat.PzBuoni = prodStatTMP.PzBuoni;
                        prodStat.Scarto = prodStatTMP.Scarto;
                    }
                    else
                    {
                        prodStat.PzBuoni = this.contatori.CntPezziBuoniTappo;
                        prodStat.Scarto = this.contatori.CntPezziScartoTappo;
                    }

                    prodStat.ProdOraria = plcMacchina.ProduzioneOraria;
                    dbmStatistiche.WriteDatiProduzione(prodStat);
                    DataType.Contatori.Serialize(this.contatori, Path.Combine(Properties.Settings.Default.DatiVisionePath, "Contatori.xml"));
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                timerAggiornaStatistiche.Enabled = true;
            }
        }

        private void timerExitRegolazioni_Tick(object sender, EventArgs e)
        {
            this.plcMacchina.ExitCaricamentoRicetta();
            System.Threading.Thread.Sleep(100);
            this.plcMacchina.ExitRegolazioni();

            timerExitRegolazioni.Stop();
        }


        // |MP 4-2-19 x la visualizzazione della statistica
        private StatisticheProduzione showDatiStatistiche(StatisticheProduzione prodStat)
        {
            if (this.contatori.CntPezziBuoniLivello > this.contatori.CntPezziBuoniTappo)
                prodStat.PzBuoni = this.contatori.CntPezziBuoniTappo;
            else if (this.contatori.CntPezziBuoniTappo > this.contatori.CntPezziBuoniLivello)
                prodStat.PzBuoni = this.contatori.CntPezziBuoniLivello;
            else
                prodStat.PzBuoni = this.contatori.CntPezziBuoniLivello;

            //scrittura pezzi scarto
            if (this.contatori.CntPezziScartoLivello > this.contatori.CntPezziScartoTappo)
                prodStat.Scarto = this.contatori.CntPezziScartoTappo;
            else if (this.contatori.CntPezziScartoTappo > this.contatori.CntPezziScartoLivello)
                prodStat.Scarto = this.contatori.CntPezziScartoLivello;
            else
                prodStat.Scarto = this.contatori.CntPezziScartoLivello;

            return prodStat;
        }


        private bool? lastResTappo = null;
        private bool? lastResLivello = null;

        //------------------------------------------------------------------------ TAPPO+LIVELLO -----------------
        // |MP 22-1-19 per fare a tempo tappo e livello con una sola CAMERA
        private void InviaRisultatoTappoLivello()
        {
            if (!this.plcMacchina.IsInRegolazioneSlittaCamere() && !this.plcMacchina.IsInRegolazioneSlittaLivello())
            {
                ElaborateResult[] risTappo = this.core[IDX_CORE_TAPPO].GetLastResult();

                int resTappo = 1;
                int resLivello = 1;
                bool risultatoCompletoTappo = false;
                bool risultatoCompletoLivello = false;

                //---------------------- tappo ---------------------
                if (risTappo != null && risTappo.Length == risTappo.Count(k => k != null))
                {
                    risultatoCompletoLivello = true;
                    risultatoCompletoTappo = true;
                }

                if (risTappo != null)
                {
                    resTappo = risTappo.Count(k => k == null || !k.Success) > 0 ? 1 : 0;
                    if (null != risTappo)
                        resLivello = risTappo.Count(k => k == null || !k.Success) > 0 ? 1 : 0;
                    else
                        resLivello = 1;
                }

                int resTappoCodice = resTappo;

                //************************ livello ******************
                if (risTappo != null && risTappo.Length == 1 && risTappo[0] != null)
                {
                    risultatoCompletoLivello = true;
                    risultatoCompletoTappo = true;
                }

                if (risTappo != null)
                {
                    resTappo = risTappo.Count(k => k == null || !k.Success) > 0 ? 1 : 0;
                    resLivello = risTappo.Count(k => k == null || !k.Success) > 0 ? 1 : 0;
                }

                int resLivelloCodice = resLivello;

                //---------------------- tappo ---------------------
                if (resTappo == 1 && risultatoCompletoTappo)
                {
                    int errorAnalisiPresenzaTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_PRESENCE");
                    int errorAnalisiSerraggioTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_CLAMPING");
                    int errorAnalisiSerraggioStelvinTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_STELVIN");
                    int errorAnalisiPiantaggioTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_POSITION");
                    int errorAnalisiControlloAnello = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_RING");
                    int errorAnalisiGabbietta = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_GABBIETTA");

                    // |MP 28-1-19 xchè non riesce a discriminare l'errore del livello quando è con il tappo, quindi
                    //se non è tappo ma si ha il codice di errore vuol dire che è livello   TACCONATA DA APPROFONDIRE TODO
                    if (errorAnalisiPresenzaTappo == 0 && errorAnalisiSerraggioTappo == 0 && errorAnalisiSerraggioStelvinTappo == 0 &&
                        errorAnalisiPiantaggioTappo == 0 && errorAnalisiControlloAnello == 0 && errorAnalisiGabbietta == 0)
                    {
                        resTappo = 0;
                        resTappoCodice = 0;
                    }
                    else
                    {
                        resTappoCodice = (errorAnalisiGabbietta << 6)
                            + (errorAnalisiControlloAnello << 5)
                            + (errorAnalisiPiantaggioTappo << 4)
                            + (errorAnalisiSerraggioStelvinTappo << 3)
                            + (errorAnalisiSerraggioTappo << 2)
                            + (errorAnalisiPresenzaTappo << 1)
                            + resTappo;

                        this.contatori.CntPezziScartoTappoPresenza += errorAnalisiPresenzaTappo;
                        this.contatori.CntPezziScartoTappoSerraggio += errorAnalisiSerraggioTappo;
                        this.contatori.CntPezziScartoTappoSerraggioStelvin += errorAnalisiSerraggioStelvinTappo;
                        this.contatori.CntPezziScartoTappoPiantaggio += errorAnalisiPiantaggioTappo;
                        this.contatori.CntPezziScartoTappoAnello += errorAnalisiControlloAnello;
                        this.contatori.CntPezziScartoTappoGabbietta += errorAnalisiGabbietta;
                    }
                }

                //************************ livello ******************
                if (resLivello == 1 && risultatoCompletoLivello)
                {
                    int errorLevelMin = GetEsitoDettaglioElaborazione(risTappo, "KO_LEVEL_MIN");
                    int errorLevelMax = GetEsitoDettaglioElaborazione(risTappo, "KO_LEVEL_MAX");
                    int errorEmpty = GetEsitoDettaglioElaborazione(risTappo, "KO_LEVEL_EMPTY");

                    // |MP 28-1-19 se errore tappo ma non livello non li deve conteggiare
                    if (errorLevelMin == 0 && errorLevelMax == 0 && errorEmpty == 0)
                    {
                        resLivello = 0;
                        resLivelloCodice = 0;
                    }
                    else
                    {
                        resLivelloCodice = (errorEmpty << 3) + (errorLevelMax << 2) + (errorLevelMin << 1) + resLivello;

                        this.contatori.CntPezziScartoLivelloMin += errorLevelMin;
                        this.contatori.CntPezziScartoLivelloMax += errorLevelMax;
                        this.contatori.CntPezziScartoLivelloEmpty += errorEmpty;
                    }
                }

                //---------------------- tappo ---------------------
                this.contatori.CntPezziBuoniTappo += resTappoCodice == 0 ? 1 : 0;
                this.contatori.CntPezziScartoTappo += resTappoCodice > 0 ? 1 : 0;
                //************************ livello ******************
                this.contatori.CntPezziBuoniLivello += resLivelloCodice == 0 ? 1 : 0;
                this.contatori.CntPezziScartoLivello += resLivelloCodice > 0 ? 1 : 0;

                this.plcMacchina.InviaRisultatoTappo(resTappoCodice);
                this.plcMacchina.InviaRisultatoLivello(resLivelloCodice);

                GlobalData globalData = GlobalData.GetIstance();

                decimal t = (decimal)(DateTime.Now - globalData.LastGrabTappo.Min()).TotalMilliseconds;
                globalData.LastTempo = t;

                if (t > globalData.MaxTempo)
                {
                    globalData.MaxTempo = t;
                }

                globalData.SommaTempi += t;
                globalData.NumeroTempi++;

                if (useDisplay)
                {
                    //this.BeginInvoke(new Action(() =>
                    //{
                    //    ucLedControlTappo.DisplayShift(risultatoCompleto ? (bool?)(resTappo == 0) : null);
                    //}));
                    lastResTappo = risultatoCompletoTappo ? (bool?)(resTappo == 0) : null;
                    lastResLivello = risultatoCompletoLivello ? (bool?)(resLivello == 0) : null;
                }

            }
        }
        //***** F I N E ********************************************************** TAPPO+LIVELLO -----------------


        private void InviaRisultatoTappo()
        {
            if (!this.plcMacchina.IsInRegolazioneSlittaCamere() && !this.plcMacchina.IsInRegolazioneSlittaLivello())
            {
                ElaborateResult[] risTappo = this.core[IDX_CORE_TAPPO].GetLastResult();

                int resTappo = 1;

                bool risultatoCompleto = false;
                if (risTappo != null && risTappo.Length == risTappo.Count(k => k != null))
                    risultatoCompleto = true;

                if (risTappo != null)
                    resTappo = risTappo.Count(k => k == null || !k.Success) > 0 ? 1 : 0;

                int resTappoCodice = resTappo;
                if (resTappo == 1 && risultatoCompleto)
                {
                    int errorAnalisiPresenzaTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_PRESENCE");
                    int errorAnalisiSerraggioTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_CLAMPING");
                    int errorAnalisiSerraggioStelvinTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_STELVIN");
                    int errorAnalisiPiantaggioTappo = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_POSITION");
                    int errorAnalisiControlloAnello = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_RING");
                    int errorAnalisiGabbietta = GetEsitoDettaglioElaborazione(risTappo, "KO_CAP_GABBIETTA");

                    resTappoCodice = (errorAnalisiGabbietta << 6)
                        + (errorAnalisiControlloAnello << 5)
                        + (errorAnalisiPiantaggioTappo << 4)
                        + (errorAnalisiSerraggioStelvinTappo << 3)
                        + (errorAnalisiSerraggioTappo << 2)
                        + (errorAnalisiPresenzaTappo << 1)
                        + resTappo;

                    this.contatori.CntPezziScartoTappoPresenza += errorAnalisiPresenzaTappo;
                    this.contatori.CntPezziScartoTappoSerraggio += errorAnalisiSerraggioTappo;
                    this.contatori.CntPezziScartoTappoSerraggioStelvin += errorAnalisiSerraggioStelvinTappo;
                    this.contatori.CntPezziScartoTappoPiantaggio += errorAnalisiPiantaggioTappo;
                    this.contatori.CntPezziScartoTappoAnello += errorAnalisiControlloAnello;
                    this.contatori.CntPezziScartoTappoGabbietta += errorAnalisiGabbietta;

                }

                this.contatori.CntPezziBuoniTappo += resTappoCodice == 0 ? 1 : 0;
                this.contatori.CntPezziScartoTappo += resTappoCodice > 0 ? 1 : 0;


                this.plcMacchina.InviaRisultatoTappo(resTappoCodice);

                GlobalData globalData = GlobalData.GetIstance();

                decimal t = (decimal)(DateTime.Now - globalData.LastGrabTappo.Min()).TotalMilliseconds;
                globalData.LastTempo = t;

                if (t > globalData.MaxTempo)
                {
                    globalData.MaxTempo = t;
                }

                globalData.SommaTempi += t;
                globalData.NumeroTempi++;

                if (useDisplay)
                {
                    //this.BeginInvoke(new Action(() =>
                    //{
                    //    ucLedControlTappo.DisplayShift(risultatoCompleto ? (bool?)(resTappo == 0) : null);
                    //}));
                    lastResTappo = risultatoCompleto ? (bool?)(resTappo == 0) : null;
                }
            }
        }

        private void InviaRisultatoLivello()
        {
            if (!this.plcMacchina.IsInRegolazioneSlittaLivello() && !this.plcMacchina.IsInRegolazioneSlittaCamere())
            {
                ElaborateResult[] risLivello = this.core[IDX_CORE_LIVELLO].GetLastResult();

                int resLivello = 1;

                bool risultatoCompleto = false;
                if (risLivello != null && risLivello.Length == 1 && risLivello[0] != null)
                    risultatoCompleto = true;

                if (risLivello != null)
                    resLivello = risLivello.Count(k => k == null || !k.Success) > 0 ? 1 : 0;

                int resLivelloCodice = resLivello;
                if (resLivello == 1 && risultatoCompleto)
                {
                    int errorLevelMin = GetEsitoDettaglioElaborazione(risLivello, "KO_LEVEL_MIN");
                    int errorLevelMax = GetEsitoDettaglioElaborazione(risLivello, "KO_LEVEL_MAX");
                    int errorEmpty = GetEsitoDettaglioElaborazione(risLivello, "KO_LEVEL_EMPTY");

                    resLivelloCodice = (errorEmpty << 3) + (errorLevelMax << 2) + (errorLevelMin << 1) + resLivello;

                    this.contatori.CntPezziScartoLivelloMin += errorLevelMin;
                    this.contatori.CntPezziScartoLivelloMax += errorLevelMax;
                    this.contatori.CntPezziScartoLivelloEmpty += errorEmpty;

                }

                this.contatori.CntPezziBuoniLivello += resLivelloCodice == 0 ? 1 : 0;
                this.contatori.CntPezziScartoLivello += resLivelloCodice > 0 ? 1 : 0;

                this.plcMacchina.InviaRisultatoLivello(resLivelloCodice);

                if (useDisplay)
                {
                    //this.BeginInvoke(new Action(() =>
                    //{
                    //    ucLedControlLivello.DisplayShift(risultatoCompleto ? (bool?)(resLivello == 0) : null);
                    //}));
                    lastResLivello = risultatoCompleto ? (bool?)(resLivello == 0) : null;
                }
            }
        }

        private int GetEsitoDettaglioElaborazione(ElaborateResult[] res, string key)
        {
            int ret = 0;

            ret = res.Where(K => K.DettaglioElaborazione.ContainsKey(key)).Sum(K => K.DettaglioElaborazione[key] ? 1 : 0) > 0 ? 1 : 0;

            return ret;
        }

        private void syncFotoManagerTappo_OnSyncInpuntChange(object sender, EventArgs e)
        {
            if (this.core[IDX_CORE_TAPPO] != null && this.core[IDX_CORE_TAPPO].IsRunning)
            {
                if (syncFotoManagerTappo.AccettaFoto == false)
                {
                    InviaRisultatoTappo();
                }
                else
                {
                }
            }
        }

        private void syncFotoManagerLivello_OnSyncInpuntChange(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.LivelloDaCamera)
            {
                if (this.core[IDX_CORE_LIVELLO] != null && this.core[IDX_CORE_LIVELLO].IsRunning)
                {
                    if (syncFotoManagerLivello.AccettaFoto == false)
                    {
                        InviaRisultatoLivello();
                    }
                    else
                    {
                    }
                }
            }
        }

        private void MainForm_OnFineElaborazioneTappo(object sender, EventArgs e)
        {
            try
            {
                if (Properties.Settings.Default.PRESENZA_SMART) // |MP 22-1-19  x far anche questa
                    InviaRisultatoTappoLivello(); // |MP 22-1-19  x far anche questa
                else
                    InviaRisultatoTappo();
                //if (this.plcMacchina != null)
                //{
                //    this.plcMacchina.AggiornaDatiPlcTappo();
                //}
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void MainForm_OnFineElaborazioneLivello(object sender, EventArgs e)
        {
            try
            {
                if (Properties.Settings.Default.PRESENZA_SMART) // |MP 22-1-19  x far anche questa
                    InviaRisultatoTappoLivello(); // |MP 22-1-19  x far anche questa
                else
                    InviaRisultatoLivello();

                //if (this.plcMacchina != null)
                //{
                //    this.plcMacchina.AggiornaDatiPlcLivello();
                //}
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        private void btnVisualizzaScartiLivello_Click(object sender, EventArgs e)
        {
            try
            {
                useDisplay = false;
                PrepareRegolazioni();

                FormVisualizzaScarti frmVisualizzaScarti = null;

                lock (repaintLock)
                {
                    frmVisualizzaScarti = new FormVisualizzaScarti(core[IDX_CORE_LIVELLO].GetLastErrorsClone(), this.linguaMngr, this.repaintLock);
                    frmVisualizzaScarti.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
            finally
            {
                ExitRegolazioniNoReloadRicette();
                useDisplay = true;
            }
        }



    }
}