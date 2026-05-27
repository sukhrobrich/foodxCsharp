namespace WindowsFormsApp1.forms.payment
{
    partial class PaymentPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "PaymentPanel";
            this.Size = new System.Drawing.Size(900, 600);
            this.Load += new System.EventHandler(this.PaymentPanel_Load);
            this.ResumeLayout(false);
        }
    }
}
