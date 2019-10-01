using DigitalControl.DataType;
using DigitalControl.FW.Class;
using HalconDotNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalControl.CMTL.Class
{
    public class Core
    {

        #region delegati

        public delegate void OnExecuteAlgorithmDelegate(HImage image, CancellationToken token, out ArrayList objectTodisplay, out ElaborateResult result);
        private OnExecuteAlgorithmDelegate OnExecuteAlgorithm;

        public delegate void OnExecuteMultiAlgorithmListDelegate(HImage[] image, CancellationToken token, out ArrayList[] objectTodisplay, out ElaborateResult[] result);
        private OnExecuteMultiAlgorithmListDelegate OnExecuteMultiAlgorithmList;

        private object OnExecuteAlgorithmLock = new object();
        private object OnNewImageLock = new object();

        public delegate void OnNewImageToDisplayDelegate(ArrayList iconicVar, ElaborateResult result);
        private OnNewImageToDisplayDelegate OnNewImageToDisplay;
        private OnNewImageToDisplayDelegate[] OnNewImageToDisplayArray;
        private object newImageEventLock = new object();

        public event EventHandler<HImage> OnNewImageForRegolazioni;

        public event EventHandler<HImage[]> OnNewImageArrayForRegolazioni;

        public event EventHandler OnFineElaborazione;

        #endregion delegati

        #region Variabili Private

        private FrameGrabberManager[] frameGrabberForThisObject = null;
        private object[] frameGrabberLock = null;
        private HImage[] imgGrab = null;
        private PlcMacchinaManager plcMacchina;

        private CancellationTokenSource cancelToken;
        private Task mainTask;

        private string errorFolderName = string.Empty;
        private List<Utilities.CacheErrorObject> LastErrors = new List<Utilities.CacheErrorObject>();
        private object lastErrorsLock = new object();

        private object lastResultLock = new object();
        private ElaborateResult[] lastResult = null;

        private int[] rotazione = null;

        private bool taskRunning = false;

        private SyncFotoManager syncFotoManager = null;

        private IOManager mIOManager = null;
        private int numOutRis = -1;
        private int numOutBusy = -1;

        #endregion Variabili Private

        public bool IsRunning { get; private set; }

        public Core(FrameGrabberManager frameGrab, PlcMacchinaManager plc, int rotazione, IOManager mIOManager, int numOutBusy, int numOutRis)
        {
            try
            {
                this.plcMacchina = plc;
                this.mIOManager = mIOManager;
                this.numOutRis = numOutRis;
                this.numOutBusy = numOutBusy;

                this.IsRunning = false;

                this.frameGrabberForThisObject = new FrameGrabberManager[1];
                this.frameGrabberLock = new object[1] { new object() };
                this.imgGrab = new HImage[1];
                this.rotazione = new int[1];

                this.frameGrabberForThisObject[0] = frameGrab;
                this.rotazione[0] = rotazione;
            }
            catch (Exception)
            {
                throw;
            }
            cancelToken = new CancellationTokenSource();
            cancelToken.Cancel();//Ho bisogno di allocarlo ma non voglio partire bloccato
        }

        public Core(FrameGrabberManager[] frameGrab, PlcMacchinaManager plc, int[] rotazione, IOManager mIOManager, int numOutBusy, int numOutRis)
        {
            try
            {
                this.plcMacchina = plc;
                this.mIOManager = mIOManager;
                this.numOutRis = numOutRis;
                this.numOutBusy = numOutBusy;

                this.IsRunning = false;

                this.frameGrabberForThisObject = new FrameGrabberManager[frameGrab.Length];
                this.frameGrabberLock = new object[frameGrab.Length];
                this.imgGrab = new HImage[frameGrab.Length];
                this.rotazione = new int[frameGrab.Length];

                for (int i = 0; i < frameGrab.Length; i++)
                {
                    this.frameGrabberLock[i] = new object();

                    this.frameGrabberForThisObject[i] = frameGrab[i];
                    this.rotazione[i] = rotazione[i];
                }

            }
            catch (Exception)
            {
                throw;
            }
            cancelToken = new CancellationTokenSource();
            cancelToken.Cancel();//Ho bisogno di allocarlo ma non voglio partire bloccato
        }


        public void SetRotazione(int rotazione)
        {
            this.rotazione[0] = rotazione;
        }

        public void SetRotazione(int[] rotazione)
        {
            this.rotazione = rotazione;
        }

        public int[] GetRotazione()
        {
            return this.rotazione;
        }

        public void SetErrorFolderName(string errorFolderName)
        {
            this.errorFolderName = errorFolderName;
        }

        public void SetSyncManager(SyncFotoManager sync)
        {
            if (sync != null)
            {
                this.syncFotoManager = sync;
            }
        }

        public FrameGrabberManager[] GetFrameGrabberManager()
        {
            return this.frameGrabberForThisObject;
        }

        public void CloseFrameGrabber()
        {
            if (frameGrabberForThisObject != null)
            {
                for (int i = 0; i < frameGrabberForThisObject.Length; i++)
                {
                    if (frameGrabberForThisObject[i] != null)
                        frameGrabberForThisObject[i].Dispose();
                }
            }
        }

        private CancellationTokenSource cts;

        //DateTime start = DateTime.Now;

        private void CoreOnNewImage(HImage hImage, Guid tmpSessionId)
        {
            //-----------------------------------------------
            lock (this.mIOManager.objectLock)
            {
                this.mIOManager.SetOutput(numOutBusy, true);
                this.mIOManager.Write();
            }
            //-----------------------------------------------

            lock (OnNewImageLock)
            {
                double startTime = HSystem.CountSeconds();

                HImage rotateImage = hImage.RotateImage(new HTuple(rotazione[0]), "constant");
                //HImage rotateImage = rotateImage_.MirrorImage("column");
                //rotateImage_.Dispose();

                cts = new CancellationTokenSource(Properties.Settings.Default.TimeoutAlgoritmo);

                ArrayList iconicVarList;
                ElaborateResult result;

                ElaborateImage(rotateImage, cts, out iconicVarList, out result);

                //-----------------------------------------------
                lock (this.mIOManager.objectLock)
                {
                    this.mIOManager.SetOutput(numOutRis, result.Success);
                    this.mIOManager.Write();
                    this.mIOManager.SetOutput(numOutBusy, false);
                    this.mIOManager.Write();
                }
                //-----------------------------------------------

                double tAnalisi = HSystem.CountSeconds();
                tAnalisi = (tAnalisi - startTime) * 1000.0;

                result.ElapsedTime = tAnalisi;

                if (sessionId != tmpSessionId)
                {
                    //Sessione SCADUTA
                }
                else
                {
                    lock (lastResultLock)
                    {
                        lastResult = new ElaborateResult[] { result };
                    }
                }

                EventHandler OnFineElaborazioneTmp = OnFineElaborazione;
                if (OnFineElaborazioneTmp != null)
                {
                    OnFineElaborazioneTmp(this, EventArgs.Empty);
                }

                ManageErrorImage(iconicVarList, result);

                RaiseNewImageToDisplayEvent(iconicVarList, result);

            }
        }

        private void CoreOnNewImage(HImage[] hImage, Guid tmpSessionId)
        {
            //-----------------------------------------------
            lock (this.mIOManager.objectLock)
            {
                //this.mIOManager.SetOutput(numOutRis, false);
                //this.mIOManager.Write(); |MP
                this.mIOManager.SetOutput(numOutBusy, true);
                this.mIOManager.Write();
            }
            //-----------------------------------------------

            //Debug.WriteLine(DateTime.Now.Subtract(start).TotalMilliseconds);
            //start = DateTime.Now;

            lock (OnNewImageLock)
            {
                double startTime = HSystem.CountSeconds();

                HImage[] rotateImage = new HImage[hImage.Length];

                for (int i = 0; i < hImage.Length; i++)
                {
                    HImage rotateImage_ = hImage[i].RotateImage(new HTuple(rotazione[i]), "constant");
                    rotateImage[i] = rotateImage_.MirrorImage("column");
                    rotateImage_.Dispose();
                }

                cts = new CancellationTokenSource(Properties.Settings.Default.TimeoutAlgoritmo);

                ArrayList[] iconicVarList;
                ElaborateResult[] result;

                ElaborateImage(rotateImage, cts, out iconicVarList, out result);

                //-----------------------------------------------
                lock (this.mIOManager.objectLock)
                {
                    this.mIOManager.SetOutput(numOutRis, result.Count(k => k.Success == true) == result.Length);
                    this.mIOManager.Write();
                    this.mIOManager.SetOutput(numOutBusy, false);
                    this.mIOManager.Write();
                }
                //-----------------------------------------------

                double tAnalisi = HSystem.CountSeconds();
                tAnalisi = (tAnalisi - startTime) * 1000.0;

                if (sessionId != tmpSessionId)
                {
                    //Sessione SCADUTA
                }
                else
                {
                    lock (lastResultLock)
                    {
                        lastResult = result;
                    }
                }

                EventHandler OnFineElaborazioneTmp = OnFineElaborazione;
                if (OnFineElaborazioneTmp != null)
                {
                    OnFineElaborazioneTmp(this, EventArgs.Empty);
                }

                if (result != null)
                {
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i].ElapsedTime = tAnalisi;
                        ManageErrorImage(iconicVarList[i], result[i]);
                    }
                }

                RaiseNewImageToDisplayEvent(iconicVarList, result);
            }
        }



        private Task taskWriteErrorImageToDisk = null;

        private void ManageErrorImage(ArrayList iconicVarList, ElaborateResult result)
        {
            if (result.Success == false && Properties.Settings.Default.NumeroErrori > 0)
            {
                lock (lastErrorsLock)
                {
                    if (LastErrors.Count == Properties.Settings.Default.NumeroErrori)
                    {
                        LastErrors[Properties.Settings.Default.NumeroErrori - 1].Dispose();
                        LastErrors.RemoveAt(Properties.Settings.Default.NumeroErrori - 1);
                    }
                    Utilities.CacheErrorObject ceo = new Utilities.CacheErrorObject(iconicVarList, result);

                    if (Properties.Settings.Default.ErroriSuDisco)
                    {
                        //WriteErrorImageToDisk(ceo);
                        if (ceo != null && ceo.IconicVar != null && ceo.IconicVar.Count > 0 && ((Utilities.ObjectToDisplay)ceo.IconicVar[0]).IconicVar is HImage)
                        {
                            HImage img = (((Utilities.ObjectToDisplay)ceo.IconicVar[0]).IconicVar as HImage).CopyImage();

                            Action actionSave = () =>
                            {
                                try
                                {
                                    WriteErrorImageToDisk(img);
                                }
                                catch (Exception ex)
                                {
                                    ExceptionManager.AddException(ex);
                                }
                            };

                            if (this.taskWriteErrorImageToDisk == null)
                                this.taskWriteErrorImageToDisk = Task.Run(actionSave);
                            else
                                this.taskWriteErrorImageToDisk = this.taskWriteErrorImageToDisk.ContinueWith(k => actionSave());
                        }
                    }

                    LastErrors.Insert(0, ceo);
                }
            }
        }

        private void WriteErrorImageToDisk(Utilities.CacheErrorObject ceo)
        {
            DateTime d = DateTime.Now;

            //string path = Path.Combine(Properties.Settings.Default.DatiVisionePath, "ERRORI", d.Year.ToString(), d.Month.ToString(), d.Day.ToString(), errorFolderName);
            string path = Path.Combine(Properties.Settings.Default.DatiVisionePath, "ERRORI", errorFolderName);


            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            //string fileName = Path.Combine(path, string.Format("{0}.tif", d.ToString("yyyyMMddHHmmss.fff")));
            string fileName = Path.Combine(path, string.Format("{0}.tif", d.ToString("yyyyMMdd HH mm ss.fff")));


            if (ceo != null && ceo.IconicVar != null && ceo.IconicVar.Count > 0 && ((Utilities.ObjectToDisplay)ceo.IconicVar[0]).IconicVar is HImage)
            {
                (((Utilities.ObjectToDisplay)ceo.IconicVar[0]).IconicVar as HImage).WriteImage("tiff", 255, fileName);

                /* cancello le immagini più vecchie che oltre il numero di immagini da tenere*/
                string[] filesInError = Directory.GetFiles(path);

                if (filesInError.Length > Properties.Settings.Default.NumeroErrori)
                {
                    filesInError = filesInError.OrderByDescending(k => k).Skip(Properties.Settings.Default.NumeroErrori).ToArray();
                    try
                    {
                        foreach (var file in filesInError)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception)
                    {
                        // Volutamente vuoto
                    }
                }
            }
        }

        private void WriteErrorImageToDisk(HImage img)
        {
            DateTime d = DateTime.Now;

            //string path = Path.Combine(Properties.Settings.Default.DatiVisionePath, "ERRORI", d.Year.ToString(), d.Month.ToString(), d.Day.ToString(), errorFolderName);
            string path = Path.Combine(Properties.Settings.Default.DatiVisionePath, "ERRORI", errorFolderName);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            //string fileName = Path.Combine(path, string.Format("{0}.tif", d.ToString("yyyyMMddHHmmss.fff")));
            string fileName = Path.Combine(path, string.Format("{0}.tif", d.ToString("yyyyMMdd HH mm ss.fff")));

            img.WriteImage("tiff", 255, fileName);
            img.Dispose();

            /* cancello le immagini più vecchie che oltre il numero di immagini da tenere*/
            string[] filesInError = Directory.GetFiles(path);

            if (filesInError.Length > Properties.Settings.Default.NumeroErrori)
            {
                filesInError = filesInError.OrderByDescending(k => k).Skip(Properties.Settings.Default.NumeroErrori).ToArray();
                try
                {
                    foreach (var file in filesInError)
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception)
                {
                    // Volutamente vuoto
                }
            }
        }


        private void ElaborateImage(HImage hImage, CancellationTokenSource cts, out ArrayList iconicVar, out ElaborateResult oResult)
        {
            OnExecuteAlgorithmDelegate del;

            ArrayList retValue = new ArrayList();
            ElaborateResult result = new ElaborateResult();

            lock (OnExecuteAlgorithmLock)
            {
                del = OnExecuteAlgorithm;
            }

            if (del != null)
            {
                del(hImage, cts.Token, out retValue, out result);
            }

            iconicVar = retValue;
            oResult = result;
        }

        private void ElaborateImage(HImage[] hImage, CancellationTokenSource cts, out ArrayList[] iconicVar, out ElaborateResult[] oResult)
        {
            OnExecuteMultiAlgorithmListDelegate del;

            ArrayList[] retValue = null;
            ElaborateResult[] result = null;

            lock (OnExecuteAlgorithmLock)
            {
                del = OnExecuteMultiAlgorithmList;
            }

            if (del != null)
            {
                del(hImage, cts.Token, out retValue, out result);
            }

            iconicVar = retValue;
            oResult = result;
        }


        private Task taskRaiseNewImageToDisplayEvent = null;

        private void RaiseNewImageToDisplayEvent(ArrayList iconicVar, ElaborateResult result)
        {
            //OnNewImageToDisplayDelegate del;

            //lock (newImageEventLock)
            //{
            //    del = OnNewImageToDisplay;
            //}

            //if (del != null)
            //{
            //    del(iconicVar, result);
            //}

            Action action = () =>
            {
                try
                {
                    OnNewImageToDisplayDelegate del;

                    lock (newImageEventLock)
                    {
                        del = OnNewImageToDisplay;
                    }

                    if (del != null)
                    {
                        del(iconicVar, result);
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                }
            };

            if (this.taskRaiseNewImageToDisplayEvent == null)
                this.taskRaiseNewImageToDisplayEvent = Task.Run(action);
            else
                this.taskRaiseNewImageToDisplayEvent = this.taskRaiseNewImageToDisplayEvent.ContinueWith(k => action());
        }

        private void RaiseNewImageToDisplayEvent(ArrayList[] iconicVar, ElaborateResult[] result)
        {
            //if (OnNewImageToDisplayArray != null)
            //{
            //    OnNewImageToDisplayDelegate del;

            //    for (int i = 0; i < OnNewImageToDisplayArray.Length; i++)
            //    {
            //        lock (newImageEventLock)
            //        {
            //            del = OnNewImageToDisplayArray[i];
            //        }

            //        if (del != null)
            //        {
            //            del(iconicVar[i], result[i]);
            //        }

            //    }
            //}

            Action action = () =>
            {
                try
                {
                    if (OnNewImageToDisplayArray != null)
                    {
                        OnNewImageToDisplayDelegate del;

                        for (int i = 0; i < OnNewImageToDisplayArray.Length; i++)
                        {
                            lock (newImageEventLock)
                            {
                                del = OnNewImageToDisplayArray[i];
                            }

                            if (del != null)
                            {
                                del(iconicVar[i], result[i]);
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                }
            };

            if (this.taskRaiseNewImageToDisplayEvent == null)
                this.taskRaiseNewImageToDisplayEvent = Task.Run(action);
            else
                this.taskRaiseNewImageToDisplayEvent = this.taskRaiseNewImageToDisplayEvent.ContinueWith(k => action());
        }


        public void SetNewImageToDisplayEvent(OnNewImageToDisplayDelegate del)
        {
            lock (newImageEventLock)
            {
                OnNewImageToDisplay = del;
            }
        }

        public void SetNewImageToDisplayEventArray(OnNewImageToDisplayDelegate[] del)
        {
            lock (newImageEventLock)
            {
                OnNewImageToDisplayArray = del;
            }
        }


        public void SetAlgorithm(OnExecuteMultiAlgorithmListDelegate del)
        {
            lock (OnExecuteAlgorithmLock)
            {
                OnExecuteMultiAlgorithmList = del;
            }
        }

        public void SetAlgorithm(OnExecuteAlgorithmDelegate algoritmo)
        {
            lock (OnExecuteAlgorithmLock)
            {
                OnExecuteAlgorithm = algoritmo;
            }
        }


        public void Run()
        {
            try
            {
                if (taskRunning)
                    return;//test se già in run

                if (mainTask != null && mainTask.Status == TaskStatus.Running)
                    return;//test se già in run

                cancelToken = new CancellationTokenSource();

                for (int i = 0; i < frameGrabberForThisObject.Length; i++)
                {
                    frameGrabberForThisObject[i].GrabImageStart();
                }

                //                if (frameGrabberForThisObject.Length == 11)
                //                {
                //                    mainTask = Task.Run(() =>
                //                    {
                //                        try
                //                        {
                //                            taskRunning = true;

                //                            while (!cancelToken.Token.IsCancellationRequested)
                //                            {
                //                                try
                //                                {
                //                                    RunSingleCamera();
                //                                }
                //                                catch (Exception ex)
                //                                {
                //                                    if (!cancelToken.Token.IsCancellationRequested)
                //                                    {
                //                                        ExceptionManager.AddException(ex);
                //                                    }
                //                                }

                //#if _Simulazione
                //                                Thread.Sleep(500);
                //#endif
                //                            }

                //                            taskRunning = false;
                //                        }
                //                        catch (Exception ex)
                //                        {
                //                            if (!cancelToken.Token.IsCancellationRequested)
                //                            {
                //                                ExceptionManager.AddException(ex);
                //                            }
                //                        }
                //                    }, cancelToken.Token);
                //                }
                //                else
                //                {
                mainTask = Task.Run(async () =>
                {
                    taskRunning = true;

                    Task produttore = ProduttoreAsync();
                    Task consumatore = ConsumatoreAsync();

                    await produttore;
                    await consumatore;

                    taskRunning = false;
                }, cancelToken.Token);
                //}

                this.IsRunning = true;

            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void StopAndWaitEnd(bool forceTrigger)
        {
            try
            {
                if (this.IsRunning)
                {
                    if (forceTrigger)
                    {
                        for (int i = 0; i < frameGrabberForThisObject.Length; i++)
                        {
                            frameGrabberForThisObject[i].ForceTrigger();
                        }
                    }

                    cancelToken.Cancel();

                    mainTask.Wait();

                    this.IsRunning = false;
                }
            }
            catch (Exception) { }
        }


        public ElaborateResult[] GetLastResult()
        {
            if (cts != null)
                cts.Cancel();

            lock (lastResultLock)
            {
                ElaborateResult[] ret = lastResult;
                lastResult = null;
                return ret;
            }
        }

        public List<Utilities.CacheErrorObject> GetLastErrorsClone()
        {
            lock (lastErrorsLock)
            {
                return this.LastErrors.Select(k => new Utilities.CacheErrorObject(k.IconicVar, k.ElaborateResult, k.TimeStamp)).ToList();
            }
        }


        private HImage AcquisitionTask()
        {
            return AcquisitionTask_(0);
        }

        private HImage AcquisitionTask_(int numTask)
        {
            HImage imgGrabTmp = null;
            try
            {
                imgGrabTmp = frameGrabberForThisObject[numTask].GrabASyncNoDelegate();
            }
            catch (HOperatorException ex)
            {
                if (!(ex.GetErrorNumber() == 5322 || ex.GetErrorNumber() == 5306))
                {
                    //Non deve dare errori nel momento in cui ho appena dato ABORT all'acquisizione 
                    if (!cancelToken.Token.IsCancellationRequested)
                        ExceptionManager.AddException(ex);
                }
            }
            return imgGrabTmp;
        }

        private void AcquisitionTask(int numTask)
        {
            try
            {
                HImage imgGrabTmp = frameGrabberForThisObject[numTask].GrabASyncNoDelegate();

                if (imgGrab[numTask] != null && imgGrab[numTask].IsInitialized())
                    imgGrab[numTask].Dispose();

                imgGrab[numTask] = imgGrabTmp;
            }
            catch (HOperatorException ex)
            {
                if (!(ex.GetErrorNumber() == 5322 || ex.GetErrorNumber() == 5306))
                {
                    //Non deve dare errori nel momento in cui ho appena dato ABORT all'acquisizione 
                    if (!cancelToken.Token.IsCancellationRequested)
                        ExceptionManager.AddException(ex);
                }
            }
        }


        private object objLock = new object();
        private Guid sessionId = Guid.Empty;



        public Task taskNewImageForRegolazioni = null;

        private void RunSingleCamera()
        {
            GlobalData globalData = GlobalData.GetIstance();

            globalData.LastTryGrabLivello = DateTime.Now;

            HImage imgGrabTmp = AcquisitionTask();

            if (imgGrabTmp != null && imgGrabTmp.IsInitialized())
            {
                sessionId = Guid.NewGuid();

                globalData.LastGrabLivello = DateTime.Now;
                globalData.NumGrabLivello++;

                lock (lastResultLock)
                {
                    lastResult = null;
                }

                CoreOnNewImage(imgGrabTmp, sessionId);

                EventHandler<HImage> ev = OnNewImageForRegolazioni;
                if (ev != null)
                {
                    HImage imgRegolazioni = imgGrabTmp.CopyImage();

                    Action action = () =>
                    {
                        ev(this, imgRegolazioni);
                    };

                    if (this.taskNewImageForRegolazioni == null)
                        this.taskNewImageForRegolazioni = Task.Run(action);
                    else
                        this.taskNewImageForRegolazioni = this.taskNewImageForRegolazioni.ContinueWith(k => action());
                }

                imgGrabTmp.Dispose();
            }
            //else
            //{
            //    //Riarma la camera se è da 10 secondi che non grabba 
            //    if (DateTime.Now - globalData.LastGrabLivello > TimeSpan.FromSeconds(10))
            //    {
            //        GrabImageStart(0);

            //        globalData.LastGrabImageStartLivello = DateTime.Now;
            //    }

            //    Thread.Sleep(1);
            //}
        }


        private async Task ConsumatoreAsync()
        {
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    while (!cancelToken.Token.IsCancellationRequested)
                    {
                        try
                        {
                            if (!(syncFotoManager != null && !syncFotoManager.AccettaFoto))
                            {
                                Guid tmpSessionId = Guid.Empty;
                                HImage[] tmpImgGrab = new HImage[imgGrab.Length];

                                bool ok = false;

                                lock (objLock)
                                {
                                    if (imgGrab[0] != null && imgGrab[0].IsInitialized())
                                    {
                                        for (int i = 0; i < imgGrab.Length; i++)
                                        {
                                            tmpImgGrab[i] = imgGrab[i];
                                            imgGrab[i] = null;
                                        }
                                        tmpSessionId = sessionId;

                                        ok = true;
                                    }
                                }

                                if (ok)
                                {
                                    CoreOnNewImage(tmpImgGrab, tmpSessionId);

                                    EventHandler<HImage[]> ev = OnNewImageArrayForRegolazioni;
                                    if (ev != null)
                                    {
                                        HImage[] imgRegolazioni = tmpImgGrab.Select(k => k.CopyImage()).ToArray();

                                        Action action = () =>
                                        {
                                            ev(this, imgRegolazioni);
                                        };

                                        if (this.taskNewImageForRegolazioni == null)
                                            this.taskNewImageForRegolazioni = Task.Run(action);
                                        else
                                            this.taskNewImageForRegolazioni = this.taskNewImageForRegolazioni.ContinueWith(k => action());
                                    }

                                    for (int i = 0; i < tmpImgGrab.Length; i++)
                                    {
                                        tmpImgGrab[i].Dispose();
                                        tmpImgGrab[i] = null;
                                    }
                                }
                                else
                                {
                                    Thread.Sleep(10);
                                    //await Task.Delay(1); 
                                }
                            }
                            else
                            {
                                //Thread.Sleep(1);
                                await Task.Delay(1);
                            }

                        }
                        catch (Exception ex)
                        {
                            //Non deve dare errori nel momento in cui ho appena dato ABORT all'acquisizione 
                            if (!cancelToken.Token.IsCancellationRequested)
                            {
                                ExceptionManager.AddException(ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Non deve dare errori nel momento in cui ho appena dato ABORT all'acquisizione 
                    if (!cancelToken.Token.IsCancellationRequested)
                    {
                        ExceptionManager.AddException(ex);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }



        private void ProduttoreAsyncCamera(int num)
        {
            GlobalData globalData = GlobalData.GetIstance();

            while (!cancelToken.Token.IsCancellationRequested)
            {
                try
                {
                    globalData.LastTryGrabTappo[num] = DateTime.Now;
                    // MP_ 15/1/19  FORZATURA XCHè NUM=0 LO FORZO A 1  POI BUTTARE!!

                    HImage imgGrabTmpProd = AcquisitionTask_(num);

                    if (imgGrabTmpProd != null && imgGrabTmpProd.IsInitialized())
                    {

                        globalData.LastGrabTappo[num] = DateTime.Now;
                        globalData.NumGrabTappo[num]++;

                        cntGrabKo[num] = 0;

                        lock (objLock)
                        {
                            imgGrab[num] = imgGrabTmpProd;
                            imgGrabTmpProd = null;
                        }
                    }
                    else
                    {
                        //Riarma la camera se tra lei e le altre sono passati più di 500 ms
                        if (globalData.LastGrabTappo.Max() - globalData.LastGrabTappo[num] > TimeSpan.FromMilliseconds(500))
                        {
                            cntGrabKo[num]++;

                            if (cntGrabKo[num] > 3)
                            {
                                GrabImageStart(num);

                                cntGrabKo[num] = 0;

                                globalData.LastGrabImageStartTappo[num] = DateTime.Now;
                            }
                        }

                        /*
                        //Riarma la camera se è da 10 secondi che non grabba 
                        if (DateTime.Now - globalData.LastGrabTappo[num] > TimeSpan.FromSeconds(10))
                        {
                            GrabImageStart(num);

                            globalData.LastGrabImageStartTappo[num] = DateTime.Now;
                        }
                        */

                    }

#if _Simulazione
                    Thread.Sleep(500);
#endif
                }
                catch (Exception ex)
                {
                    ExceptionManager.AddException(ex);
                }
            }
        }

        private async Task ProduttoreAsync()
        {
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    Task[] acqTasks = new Task[frameGrabberForThisObject.Length];

                    try
                    {
                        acqTasks = new Task[frameGrabberForThisObject.Length];

                        acqTasks[0] = Task.Factory.StartNew(() => { ProduttoreAsyncCamera(0); }, cancelToken.Token);
                        /*   acqTasks[1] = Task.Factory.StartNew(() => { ProduttoreAsyncCamera(1); }, cancelToken.Token);  |MP  04-01-19
                           if (frameGrabberForThisObject.Length == 3)
                               acqTasks[2] = Task.Factory.StartNew(() => { ProduttoreAsyncCamera(2); }, cancelToken.Token); */

                        Task.WaitAll(acqTasks);

                        await Task.Delay(1);

                    }
                    catch (Exception ex)
                    {
                        //Non deve dare errori nel momento in cui ho appena dato ABORT all'acquisizione 
                        if (!cancelToken.Token.IsCancellationRequested)
                        {
                            ExceptionManager.AddException(ex);
                        }
                    }

                }
                catch (Exception ex)
                {
                    //Non deve dare errori nel momento in cui ho appena dato ABORT all'acquisizione 
                    if (!cancelToken.Token.IsCancellationRequested)
                    {
                        ExceptionManager.AddException(ex);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }



        /*DEBUG*/

        private static int[] cntGrabKo = new int[3];


        public void GrabImageStart(int numCam)
        {
            frameGrabberForThisObject[numCam].GrabImageStart();
        }

        public void ReOpen(int numCam)
        {
            frameGrabberForThisObject[numCam].ReOpen();
        }

        /*END DEBUG*/

    }
}