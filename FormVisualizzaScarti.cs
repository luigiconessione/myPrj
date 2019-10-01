using DigitalControl.FW.Class;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ViewROI;

namespace DigitalControl.CMTL
{
    public partial class FormVisualizzaScarti : Form
    {

        private HWndCtrl viewControl = null;
        private readonly DBL.LinguaManager linguaMngr = null;
        private readonly object repaintLock = null;

        public FormVisualizzaScarti(List<Utilities.CacheErrorObject> dataSource, DBL.LinguaManager linguaMngr, object repaintLock)
        {
            InitializeComponent();

            viewControl = new HWndCtrl(hMainWndCntrl);
            this.linguaMngr = linguaMngr;
            this.repaintLock = repaintLock;

            bdsCacheErrorObject.DataSource = dataSource;

            AdjustCulture();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            VisualizzaSelezionato();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // in chiusura dispongo gli erroi visualizati
            List<Utilities.CacheErrorObject> lstTmp = bdsCacheErrorObject.DataSource as List<Utilities.CacheErrorObject>;
            if (lstTmp != null)
            {
                lstTmp.ForEach(k => k.Dispose());
            }
        }


        private void AdjustCulture()
        {
            lblTitolo.Text = linguaMngr.GetTranslation("FORM_ULTIMI_ERRORI_TITLE");
        }

        private void VisualizzaSelezionato()
        {
            try
            {
                if (lbScarti.SelectedItem != null && lbScarti.SelectedItem != DBNull.Value)
                {
                    Utilities.CacheErrorObject ceo = (Utilities.CacheErrorObject)lbScarti.SelectedItem;

                    // ne creo uno nuovo poiche i metodi di utility dispongono i controlli che vengono visualizzati, e ricreandolo ne faccio solo le copie
                    ceo = new Utilities.CacheErrorObject(ceo.IconicVar, ceo.ElaborateResult);

                    Utilities.CommonUtility.DisplayRegolazioni(ceo.IconicVar, viewControl, hMainWndCntrl, repaintLock);
                    Utilities.CommonUtility.DisplayResult(ceo.ElaborateResult, hMainWndCntrl, repaintLock);

                }
            }
            catch (Exception ex)
            {
                ExceptionManager.AddException(ex);
            }
        }


        private void lbScarti_SelectedValueChanged(object sender, EventArgs e)
        {
            VisualizzaSelezionato();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }
}