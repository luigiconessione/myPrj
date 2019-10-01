using DigitalControl.DataType;
using HalconDotNet;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalControl.CMTL.Class
{
    public class CoreRegolazioni
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

        #endregion delegati

        #region Variabili Private

        private readonly Core core = null;

        private HImage[] lastGrabImg = null;

        private int[] rotazione = null;

        #endregion Variabili Private

        public bool IsRunning { get; private set; }

        public CoreRegolazioni(Core core, int rotazione)
        {
            try
            {
                this.IsRunning = false;
                this.core = core;
                this.lastGrabImg = new HImage[1];
                this.rotazione = new int[] { rotazione };

                core.OnNewImageArrayForRegolazioni += core_OnNewImageArrayForRegolazioni;
                core.OnNewImageForRegolazioni += core_OnNewImageForRegolazioni;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public CoreRegolazioni(Core core, int[] rotazione)
        {
            try
            {
                this.IsRunning = false;
                this.core = core;
                this.lastGrabImg = new HImage[rotazione.Length];
                this.rotazione = rotazione;

                core.OnNewImageArrayForRegolazioni += core_OnNewImageArrayForRegolazioni;
                core.OnNewImageForRegolazioni += core_OnNewImageForRegolazioni;
            }
            catch (Exception)
            {
                throw;
            }
        }


        public void SetRotazione(int rotazione)
        {
            this.rotazione[0] = rotazione;
        }

        public void SetRotazione(int[] rotazione)
        {
            this.rotazione = rotazione;
        }

        public void SetRotazione(int rotazione, int idx)
        {
            this.rotazione[idx] = rotazione;
        }

        public int[] GetRotazione()
        {
            return this.rotazione;
        }

        public void CloseFrameGrabber()
        {
            core.OnNewImageForRegolazioni -= core_OnNewImageForRegolazioni;
            core.OnNewImageArrayForRegolazioni -= core_OnNewImageArrayForRegolazioni;
        }

        public void ExecuteOnLastImage()
        {
            if (lastGrabImg[0] != null && lastGrabImg[0].IsInitialized())
            {
                CoreOnNewImage(new HImage[] { lastGrabImg[0].CopyImage() }, Guid.Empty);
            }
        }

        private void core_OnNewImageArrayForRegolazioni(object sender, HImage[] e)
        {
            if (this.IsRunning)
                CoreOnNewImage(e, Guid.Empty);
        }

        private void core_OnNewImageForRegolazioni(object sender, HImage e)
        {
            if (this.IsRunning)
                CoreOnNewImage(e, Guid.Empty);
        }

        private CancellationTokenSource cts;

        private void CoreOnNewImage(HImage hImage, Guid tmpSessionId)
        {
            lock (OnNewImageLock)
            {
                double startTime = HSystem.CountSeconds();

                if (lastGrabImg[0] != null)
                {
                    lastGrabImg[0].Dispose();
                }
                lastGrabImg[0] = hImage.CopyImage();

                HImage rotateImage = hImage.RotateImage(new HTuple(rotazione[0]), "constant");
                //HImage rotateImage = rotateImage_.MirrorImage("column");
                //rotateImage_.Dispose();

                hImage.Dispose();

                cts = new CancellationTokenSource(Properties.Settings.Default.TimeoutAlgoritmo);

                ArrayList iconicVarList;
                ElaborateResult result;
                ElaborateImage(rotateImage, cts, out iconicVarList, out result);
                double tAnalisi = HSystem.CountSeconds();
                tAnalisi = (tAnalisi - startTime) * 1000.0;

                result.ElapsedTime = tAnalisi;

                RaiseNewImageToDisplayEvent(iconicVarList, result);
            }
        }

        private void CoreOnNewImage(HImage[] hImage, Guid tmpSessionId)
        {
            lock (OnNewImageLock)
            {
                double startTime = HSystem.CountSeconds();

                HImage[] rotateImage = new HImage[hImage.Length];

                Parallel.For(0, Properties.Settings.Default.NumeroCamereTappo, i =>
                {
                    if (lastGrabImg[i] != null)
                    {
                        lastGrabImg[i].Dispose();
                    }
                    // |MP 25-1-19
                    if (null != hImage[i])
                    {
                        lastGrabImg[i] = hImage[i].CopyImage();

                        HImage rotateImage_ = hImage[i].RotateImage(new HTuple(rotazione[i]), "constant");
                        rotateImage[i] = rotateImage_.MirrorImage("column");
                        rotateImage_.Dispose();

                        hImage[i].Dispose();
                    }
                    hImage[i] = null;
                });

                cts = new CancellationTokenSource(Properties.Settings.Default.TimeoutAlgoritmo);

                ArrayList[] iconicVarList;
                ElaborateResult[] result;
                ElaborateImage(rotateImage, cts, out iconicVarList, out result);
                double tAnalisi = HSystem.CountSeconds();
                tAnalisi = (tAnalisi - startTime) * 1000.0;

                for (int i = 0; i < result.Length; i++)
                {
                    result[i].ElapsedTime = tAnalisi;
                }

                RaiseNewImageToDisplayEvent(iconicVarList, result);
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


        private void RaiseNewImageToDisplayEvent(ArrayList iconicVar, ElaborateResult result)
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

        private void RaiseNewImageToDisplayEvent(ArrayList[] iconicVar, ElaborateResult[] result)
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
            this.IsRunning = true;
        }

        public void StopAndWaitEnd(bool forceTrigger)
        {
            this.IsRunning = false;
        }

    }
}