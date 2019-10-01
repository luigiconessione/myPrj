using System;
using System.ComponentModel;

namespace DigitalControl.CMTL
{
    public class GlobalData
    {

        private static GlobalData istance = null;

        public static GlobalData GetIstance()
        {
            if (istance == null)
                istance = new GlobalData();

            return istance;
        }

        private GlobalData()
        {
            this.LastTryGrabTappo = new DateTime[3];
            this.LastGrabTappo = new DateTime[3];

            this.LastGrabImageStartTappo = new DateTime?[3];

            this.NumGrabTappo = new decimal[3];
        }

        [Category("CAMERA LIVELLO"), DisplayName("Last try grab livello"), ReadOnly(true)]
        public string LastTryGrabLivelloS { get { return LastTryGrabLivello.ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA LIVELLO"), DisplayName("Last grab livello"), ReadOnly(true)]
        public string LastGrabLivelloS { get { return LastGrabLivello.ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA LIVELLO"), DisplayName("Last restart livello")]
        public string LastGrabImageStartLivelloS { get { return LastGrabImageStartLivello.HasValue ? LastGrabImageStartLivello.Value.ToString("yyyy/MM/dd HH:mm:ss.fff") : string.Empty; } }
        [Category("CAMERA LIVELLO"), DisplayName("Numero Grab livello")]
        public decimal NumGrabLivelloS { get { return NumGrabLivello; } }
        

        [Category("CAMERA TAPPO 1"), DisplayName("Last try grab tappo 1")]
        public string LastTryGrabLivello1 { get { return LastTryGrabTappo[0].ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA TAPPO 1"), DisplayName("Last grab tappo 1")]
        public string LastGrabLivello1 { get { return LastGrabTappo[0].ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA TAPPO 1"), DisplayName("Last restart tappo 1")]
        public string LastGrabImageStartTappo1 { get { return LastGrabImageStartTappo[0].HasValue ? LastGrabImageStartTappo[0].Value.ToString("yyyy/MM/dd HH:mm:ss.fff") : string.Empty; } }
        [Category("CAMERA TAPPO 1"), DisplayName("Numero Grab tappo 1")]
        public decimal NumGrabTappo1 { get { return NumGrabTappo[0]; } }


        [Category("CAMERA TAPPO 2"), DisplayName("Last try grab tappo 2")]
        public string LastTryGrabLivello2 { get { return LastTryGrabTappo[1].ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA TAPPO 2"), DisplayName("Last grab tappo 2")]
        public string LastGrabLivello2 { get { return LastGrabTappo[1].ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA TAPPO 2"), DisplayName("Last restart tappo 2")]
        public string LastGrabImageStartTappo2 { get { return LastGrabImageStartTappo[1].HasValue ? LastGrabImageStartTappo[1].Value.ToString("yyyy/MM/dd HH:mm:ss.fff") : string.Empty; } }
        [Category("CAMERA TAPPO 2"), DisplayName("Numero Grab tappo 2")]
        public decimal NumGrabTappo2 { get { return NumGrabTappo[1]; } }


        [Category("CAMERA TAPPO 3"), DisplayName("Last try grab tappo 3")]
        public string LastTryGrabLivello3 { get { return LastTryGrabTappo[2].ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA TAPPO 3"), DisplayName("Last grab tappo 3")]
        public string LastGrabLivello3 { get { return LastGrabTappo[2].ToString("yyyy/MM/dd HH:mm:ss.fff"); } }
        [Category("CAMERA TAPPO 3"), DisplayName("Last restart tappo 3")]
        public string LastGrabImageStartTappo3 { get { return LastGrabImageStartTappo[2].HasValue ? LastGrabImageStartTappo[2].Value.ToString("yyyy/MM/dd HH:mm:ss.fff") : string.Empty; } }
        [Category("CAMERA TAPPO 3"), DisplayName("Numero Grab tappo 3")]
        public decimal NumGrabTappo3 { get { return NumGrabTappo[2]; } }


        [DisplayName("Ultimo tempo riposta"), ReadOnly(true)]
        public decimal LastTempo { get; set; }
        [DisplayName("Tempo max riposta"), ReadOnly(true)]
        public decimal MaxTempo { get; set; }
        [DisplayName("Tempo medio riposta")]
        public decimal AvgTempo { get { return NumeroTempi == 0 ? 0 : SommaTempi / NumeroTempi; } }


        [Browsable(false)]
        public DateTime LastGrabLivello { get; set; }
        [Browsable(false)]
        public DateTime LastTryGrabLivello { get; set; }
        [Browsable(false)]
        public DateTime? LastGrabImageStartLivello { get; set; }
        [Browsable(false)]
        public decimal NumGrabLivello { get; set; }

        [Browsable(false)]
        public DateTime[] LastTryGrabTappo { get; set; }
        [Browsable(false)]
        public DateTime[] LastGrabTappo { get; set; }
        [Browsable(false)]
        public DateTime?[] LastGrabImageStartTappo { get; set; }
        [Browsable(false)]
        public decimal[] NumGrabTappo { get; set; }

        [Browsable(false)]
        public decimal SommaTempi { get; set; }
        [Browsable(false)]
        public decimal NumeroTempi { get; set; }

    }
}