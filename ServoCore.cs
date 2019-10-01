using DigitalControl.DataType;
using System;

namespace DigitalControl.CMTL.Class
{
    public class ServoCore
    {
        public const double TOLLERANZA_POSIZIONAMENTO = 0.4;

        public enum SlaveEnum
        {
            Servo = 1
        }

        private Servo.ServoManager manager = null;

        public ServoCore(RS232Param connection)
        {
            try
            {
                manager = new Servo.ServoManager(connection.PortName
                    , (uint)connection.BaudRate
                    , connection.Parity
                    , (byte)connection.DataBits
                    , connection.StopBits
                    , 250);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Open()
        {
            manager.Open();
        }

        public void Close()
        {
            manager.Close();
        }

        public bool IsOpen()
        {
            return manager.IsOpen();
        }

        private bool IsSlaveConnected(SlaveEnum slave)
        {
            return manager.SlaveConnected((byte)slave);
        }

        public void GetTestiTastierino(SlaveEnum slave, out string testo, out string valore)
        {
            manager.GetText((byte)slave, out testo, out valore);
        }

        public Servo.ServoManager.MenuList GetCurrentMenu(SlaveEnum slave)
        {
            return manager.GetCurrentMenu((byte)slave);
        }

        public double GetPosizione(SlaveEnum slave)
        {
            //return manager.GetPosizione((byte)slave);

            return manager.GetPosizioneReal((byte)slave) / Properties.Settings.Default.RapportoEncoder_mmServo;
        }

        public int GetPosizioneReal(SlaveEnum slave)
        {
            return manager.GetPosizioneReal((byte)slave);
        }

        public void SetPosizioneReal(SlaveEnum slave, int posizione)
        {
            manager.SetPosizioneReal((byte)slave, posizione);
        }

        public void SendCambioFormato()
        {
            manager.SendCambioFormato();
        }

        public void AvviacambioPosizione(SlaveEnum slave)
        {
            manager.AvviacambioPosizione((byte)slave);
        }


        public bool MovimentazioneInCorso(SlaveEnum slave)
        {
            return manager.MovimentazioneInCorso((byte)slave);
        }

        public void SetAdmin(SlaveEnum slave)
        {
            manager.SetAdmin((byte)slave);
        }

        public bool SetModeMovimentazione(SlaveEnum slave)
        {
            return manager.SetModeMovimentazione((byte)slave);
        }

        public bool SetModeAzzeramento(SlaveEnum slave)
        {
            return manager.SetModeAzzeramento((byte)slave);
        }

        public bool SetModeRiallinemanto(SlaveEnum slave)
        {
            return manager.SetModeRiallinemanto((byte)slave);
        }

        public bool WriteEnter(SlaveEnum slave, bool state)
        {
            return manager.WriteEnter((byte)slave, state);
        }

        public bool WriteFunz(SlaveEnum slave, bool state)
        {
            return manager.WriteFunz((byte)slave, state);
        }

        public bool WriteUp(SlaveEnum slave, bool state)
        {
            return manager.WriteUp((byte)slave, state);
        }

        public bool WriteDown(SlaveEnum slave, bool state)
        {
            return manager.WriteDown((byte)slave, state);
        }

        public bool ReadAllarmeCoppia(SlaveEnum slave)
        {
            return manager.ReadAllarmeCoppia((byte)slave);
        }

        public bool ResetAllarmeCoppia(SlaveEnum slave)
        {
            return manager.ResetAllarmeCoppia((byte)slave);
        }

        public bool RiallinemantoRichiesto(SlaveEnum slave)
        {
            return manager.RiallinemantoRichiesto((byte)slave);
        }

        public bool CheckForServoConnected()
        {
            return this.IsOpen() && this.IsSlaveConnected(Class.ServoCore.SlaveEnum.Servo);
        }

    }
}