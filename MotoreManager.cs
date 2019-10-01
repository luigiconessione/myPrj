using DigitalControl.FW.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalControl.CMTL.Class
{
    public class MotoreManager
    {

        private enum StatiMotori
        {
            IN_MOVIMENTO = 1,
            IN_POSIZIONE = 2
        }

        private enum ComandiMotori
        {
            RICALCOLA = 1,
            AZZERAMENTO = 10,
            STOPEMERGENZA = 11,
            POSIZIONAMENTO = 20,
            DISCESA = 6,
            SALITA = 5,

            ENTRA_CAMBIO_FORMATO = 32,
            ESCI_CAMBIO_FORMATO = 33,

            START_PLC = 50,
            STOP_PLC = 51
        }

        #region Variabili Private

        private CPD.CPDManager CPDManagerMotori = null;
        private int plcAddress = 0;

        private const int WM2 = 0x704;
        private const int WM4 = 0x708;
        private const int WM5 = 0x70A;
        private const int WM25 = 0x732;
        private const int WM64 = 0x780;
        private const int WM116 = 0x7E8;
        private const int WM117 = 0x7EA;
        private const int WM118 = 0x7EC;
        private const int WM119 = 0x7EE;
        private const int WM120 = 0x7F0;
        private const int WM121 = 0x7F2;
        private const int WM122 = 0x7F4;
        private const int WM123 = 0x7F6;



        #endregion Variabili Private

        public MotoreManager(CPD.CPDManager CPDManagerMotori, int plcAddress)
        {
            this.CPDManagerMotori = CPDManagerMotori;
            this.plcAddress = plcAddress;
        }

        // Gestione motori

        private void InviaComandoMotori(ComandiMotori comando)
        {
            this.ScriviPlc(this.plcAddress, WM25, (int)comando);
        }


        public void AzzeraSlitta(int corsa)
        {
            //int[] address = new int[] { WM64 };
            //int[] value = new int[] { (int)corsa };

            //this.ScriviPlc(this.plcAddress, address, value);

            //Thread.Sleep(100);

            InviaComandoMotori(ComandiMotori.AZZERAMENTO);
        }

        public void InviaPosizionamentoSlitta(int posizione)
        {
            int[] address = new int[] { WM122, WM123 };

            ushort posizioneH = (ushort)((int)(posizione) >> 16);
            ushort posizioneL = ((ushort)((int)(posizione) & 0xFFFF));

            int[] value = new int[] { posizioneL, posizioneH };

            this.ScriviPlc(this.plcAddress, address, value);

            Thread.Sleep(100);

            InviaComandoMotori(ComandiMotori.POSIZIONAMENTO);
        }


        public bool InMovimento()
        {
            int valore = this.LeggiPlc(this.plcAddress, WM4);
            StatiMotori sato = (StatiMotori)valore;
            return sato.HasFlag(StatiMotori.IN_MOVIMENTO);
        }

        public bool InPosizione()
        {
            int valore = this.LeggiPlc(this.plcAddress, WM4);
            StatiMotori sato = (StatiMotori)valore;
            return sato.HasFlag(StatiMotori.IN_POSIZIONE);
        }





        public void EnterRegolazioni()
        {
            try
            {
                this.ScriviPlc(this.plcAddress, WM2, 2);
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
                this.ScriviPlc(this.plcAddress, WM2, 0);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }


        public void EnterCambioFormato()
        {
            try
            {
                InviaComandoMotori(ComandiMotori.ENTRA_CAMBIO_FORMATO);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }

        public void ExitCambioFormato()
        {
            try
            {
                InviaComandoMotori(ComandiMotori.ESCI_CAMBIO_FORMATO);
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }


        public int GetPosizione()
        {
            int[] address = new int[] { WM120, WM121 };

            int[] lettura = this.LeggiPlc(this.plcAddress, address);

            return (lettura[1] << 16) + lettura[0];
        }



        public void MuoviSlittaSu()
        {
            InviaComandoMotori(ComandiMotori.SALITA);
        }

        public void MuoviSlittaGiu()
        {
            InviaComandoMotori(ComandiMotori.DISCESA);
        }

        public void FermaSlitta()
        {
            InviaComandoMotori(ComandiMotori.STOPEMERGENZA);
        }

        public bool AzzeramentoOK()
        {
            int valore = this.LeggiPlc(this.plcAddress, WM5);

            if (valore == 2)
            {
                this.ScriviPlc(this.plcAddress, WM5, 0);
                return true;
            }
            else
            {
                return false;
            }
        }


        public void CambioRicetta(int offset)
        {
            int[] address = new int[] { WM116, WM117 };

            ushort posizioneH = (ushort)((int)(offset) >> 16);
            ushort posizioneL = ((ushort)((int)(offset) & 0xFFFF));

            int[] value = new int[] { posizioneL, posizioneH };

            this.ScriviPlc(this.plcAddress, address, value);
        }





















        private void ScriviPlc(int plcAddress, int address, int value)
        {
            this.CPDManagerMotori.SendMessage(plcAddress, address, value);
        }

        private void ScriviPlc(int plcAddress, int[] address, int[] value)
        {
            this.CPDManagerMotori.SendMessage(plcAddress, address, value);
        }

        public int LeggiPlc(int plcAddress, int address)
        {
            return this.CPDManagerMotori.ReadMessage(plcAddress, address);
        }

        public int[] LeggiPlc(int plcAddress, int startAddress, int count)
        {
            return this.CPDManagerMotori.ReadMessage(plcAddress, startAddress, count);
        }

        public int[] LeggiPlc(int plcAddress, int[] address)
        {
            return this.CPDManagerMotori.ReadMessage(plcAddress, address);
        }

    }
}