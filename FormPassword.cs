using System;
using System.Windows.Forms;

namespace DigitalControl.CMTL
{
    public partial class FormPassword : Form
    {

        private readonly DBL.LinguaManager linguaMngr = null;

        public FormPassword(DBL.LinguaManager linguaMngr)
        {
            InitializeComponent();

            this.linguaMngr = linguaMngr;

            AdjustCulture();
        }

        private void AdjustCulture()
        {
            lblDescrizione.Text = this.linguaMngr.GetTranslation("LBL_DESCRIZIONE_PSW");
        }

        public string GetPassword()
        {
            return txtPassword.Text;
        }

        private void btnAnnulla_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                this.DialogResult = DialogResult.OK;
            }
        }

        private void btnKeyboard_Click(object sender, EventArgs e)
        {
            DigitalControl.FW.Utilities.KeyBoardOsk.showKeypad((sender as Control).Handle);
        }

    }
}