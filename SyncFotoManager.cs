using System;
using System.IO.Ports;

namespace DigitalControl.CMTL.Class
{
    public class SyncFotoManager
    {

        public event EventHandler OnSyncInpuntChange;

        public bool AccettaFoto { get; private set; }

        private SerialPort syncPort = null;
        private bool syncInpuntEnabled = true;
        private System.Timers.Timer syncInpuntTimer;

        private SerialPinChange inputPin;

        public SyncFotoManager(SerialPort syncPort, SerialPinChange inputPin)
        {
            try
            {
                this.inputPin = inputPin;
                this.syncPort = syncPort;
                this.syncPort.PinChanged += new SerialPinChangedEventHandler(PlcMacchinaInputChanged);

                syncInpuntTimer = new System.Timers.Timer();
                syncInpuntTimer.Interval = 10;
                syncInpuntTimer.Elapsed += new System.Timers.ElapsedEventHandler(syncInpuntTimer_Tick);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void syncInpuntTimer_Tick(object sender, EventArgs e)
        {
            syncInpuntEnabled = true;
            syncInpuntTimer.Stop();
        }
        
        private void PlcMacchinaInputChanged(object sender, SerialPinChangedEventArgs e)
        {

            if (e.EventType == this.inputPin)
            {
                if (syncInpuntEnabled)
                {
                    syncInpuntEnabled = false;

                    if ((e.EventType == SerialPinChange.CtsChanged && syncPort.CtsHolding)
                        || (e.EventType == SerialPinChange.CDChanged && syncPort.CDHolding))
                    {
                        // il segnale è alto
                        AccettaFoto = true;
                    }
                    else
                    {
                        // il segnale è basso                    
                        AccettaFoto = false;
                    }

                    EventHandler ev = OnSyncInpuntChange;
                    if (ev != null)
                    {
                        ev(this, EventArgs.Empty);
                    }

                }
                syncInpuntTimer.Start();
            }

        }

    }
}