using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiPinDialog : Form
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage   = Color.FromArgb(249, 249, 251);
        private static readonly Color Muted    = Color.FromArgb(156, 163, 175);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);

        private readonly string _waiterName;
        private TextBox _txtPin;
        private Label   _lblErr;

        public string Pin { get { return _txtPin != null ? _txtPin.Text : ""; } }

        public MultiPinDialog(string waiterName)
        {
            _waiterName          = waiterName;
            this.Text            = "FoodX - Kirish";
            this.Size            = new Size(360, 300);
            this.MinimumSize     = new Size(360, 300);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = Color.White;
            this.KeyPreview      = true;
            this.KeyDown        += OnKeyDown;
            Build();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                this.Close();
            }
        }

        private void Build()
        {
            // Yuqori oltin chiziq
            Panel topLine = new Panel();
            topLine.Height    = 5;
            topLine.Dock      = DockStyle.Top;
            topLine.BackColor = Gold;
            this.Controls.Add(topLine);

            int cx = 40;
            int w  = 280;
            int y  = 20;

            // Avatar label (initials)
            string ini   = _waiterName.Length >= 2 ? _waiterName.Substring(0, 2).ToUpper() : _waiterName.ToUpper();
            Label avatar = new Label();
            avatar.Text      = ini;
            avatar.Font      = new Font("Segoe UI", 20, FontStyle.Bold);
            avatar.ForeColor = Gold;
            avatar.BackColor = Color.FromArgb(255, 248, 230);
            avatar.Width     = 64;
            avatar.Height    = 64;
            avatar.Left      = (360 - 64) / 2;
            avatar.Top       = y;
            avatar.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(avatar);
            y += 74;

            // Ism
            Label lblName = new Label();
            lblName.Text      = _waiterName;
            lblName.Font      = new Font("Segoe UI", 13, FontStyle.Bold);
            lblName.ForeColor = TextDark;
            lblName.AutoSize  = false;
            lblName.Width     = w;
            lblName.Height    = 26;
            lblName.Left      = cx;
            lblName.Top       = y;
            lblName.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(lblName);
            y += 32;

            // Yo'riqnoma
            Label lblHint = new Label();
            lblHint.Text      = "PIN yoki parolni kiriting";
            lblHint.Font      = new Font("Segoe UI", 9);
            lblHint.ForeColor = Muted;
            lblHint.AutoSize  = false;
            lblHint.Width     = w;
            lblHint.Height    = 20;
            lblHint.Left      = cx;
            lblHint.Top       = y;
            lblHint.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(lblHint);
            y += 28;

            // PIN input
            _txtPin                 = new TextBox();
            _txtPin.Location        = new Point(cx, y);
            _txtPin.Width           = w;
            _txtPin.Height          = 40;
            _txtPin.Font            = new Font("Segoe UI", 16);
            _txtPin.TextAlign       = HorizontalAlignment.Center;
            _txtPin.PasswordChar    = '*';
            _txtPin.BorderStyle     = BorderStyle.FixedSingle;
            _txtPin.BackColor       = BgPage;
            _txtPin.ForeColor       = TextDark;
            _txtPin.KeyDown        += OnPinKeyDown;
            this.Controls.Add(_txtPin);
            y += 48;

            // Xato labeli
            _lblErr           = new Label();
            _lblErr.Text      = "";
            _lblErr.ForeColor = Danger;
            _lblErr.Font      = new Font("Segoe UI", 8, FontStyle.Bold);
            _lblErr.AutoSize  = false;
            _lblErr.Width     = w;
            _lblErr.Height    = 18;
            _lblErr.Left      = cx;
            _lblErr.Top       = y;
            _lblErr.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(_lblErr);
            y += 24;

            // Kirish tugmasi
            Button btnOk      = new Button();
            btnOk.Text        = "Kirish";
            btnOk.Location    = new Point(cx, y);
            btnOk.Width       = w;
            btnOk.Height      = 42;
            btnOk.FlatStyle   = FlatStyle.Flat;
            btnOk.BackColor   = Gold;
            btnOk.ForeColor   = Color.White;
            btnOk.Font        = new Font("Segoe UI", 11, FontStyle.Bold);
            btnOk.Cursor      = Cursors.Hand;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click      += OnOkClick;
            this.Controls.Add(btnOk);

            this.ActiveControl = _txtPin;
        }

        private void OnPinKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) TryConfirm();
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            TryConfirm();
        }

        private void TryConfirm()
        {
            if (_txtPin == null || _txtPin.Text.Length == 0)
            {
                _lblErr.Text = "PIN kiritilishi shart";
                return;
            }
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }

        public void ShowError(string msg)
        {
            if (_lblErr != null) _lblErr.Text = msg;
        }
    }
}
