using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiPinDialog : Form
    {
        private static readonly Color Gold    = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage  = Color.FromArgb(249, 249, 251);
        private static readonly Color Muted   = Color.FromArgb(156, 163, 175);
        private static readonly Color Border  = Color.FromArgb(229, 231, 235);
        private static readonly Color Danger  = Color.FromArgb(220, 38, 38);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);

        private readonly string _waiterName;
        private TextBox  _txtPin;
        private Label    _lblErr;
        private Button   _btnOk;

        public string Pin => _txtPin?.Text ?? "";

        public MultiPinDialog(string waiterName)
        {
            _waiterName = waiterName;
            this.Text            = "FoodX — Kirish";
            this.Size            = new Size(360, 320);
            this.MinimumSize     = new Size(360, 320);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = Color.White;
            this.KeyPreview      = true;
            this.KeyDown        += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
            Build();
        }

        private void Build()
        {
            this.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Gold });

            int y = 24;

            // Avatar
            Panel avatar = new Panel { Width = 64, Height = 64, BackColor = Color.Transparent, Location = new Point((360 - 64) / 2, y) };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(243, 244, 246)), 0, 0, 63, 63);
                e.Graphics.DrawEllipse(new Pen(Gold, 2), 1, 1, 60, 60);
                string ini = _waiterName.Length >= 2 ? _waiterName.Substring(0, 2).ToUpper() : _waiterName.ToUpper();
                using (Font f = new Font("Segoe UI", 18, FontStyle.Bold))
                using (var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center })
                    e.Graphics.DrawString(ini, f, new SolidBrush(Gold), new RectangleF(0, 0, 63, 63), sf);
            };
            this.Controls.Add(avatar);
            y += 74;

            // Ism
            this.Controls.Add(new Label
            {
                Text = _waiterName,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = false,
                Width = 320, Height = 26, Left = 20, Top = y,
                TextAlign = ContentAlignment.MiddleCenter
            });
            y += 34;

            // Yo'riqnoma
            this.Controls.Add(new Label
            {
                Text = "PIN yoki parolni kiriting",
                Font = new Font("Segoe UI", 9), ForeColor = Muted,
                AutoSize = false, Width = 320, Height = 20, Left = 20, Top = y,
                TextAlign = ContentAlignment.MiddleCenter
            });
            y += 28;

            // PIN input
            _txtPin = new TextBox
            {
                Location = new Point(40, y), Width = 280, Height = 40,
                Font = new Font("Segoe UI", 16),
                TextAlign = HorizontalAlignment.Center,
                PasswordChar = '●',
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark
            };
            _txtPin.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) TryConfirm(); };
            this.Controls.Add(_txtPin);
            y += 50;

            // Xato labeli
            _lblErr = new Label
            {
                Text = "", ForeColor = Danger,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = false, Width = 320, Height = 18, Left = 20, Top = y,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_lblErr);
            y += 24;

            // Kirish tugmasi
            _btnOk = new Button
            {
                Text = "Kirish", Location = new Point(40, y),
                Width = 280, Height = 42,
                FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
            };
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.Click += (s, e) => TryConfirm();
            this.Controls.Add(_btnOk);

            this.ActiveControl = _txtPin;
        }

        private void TryConfirm()
        {
            if (string.IsNullOrEmpty(_txtPin.Text))
            {
                _lblErr.Text = "PIN kiritilishi shart";
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        public void ShowError(string msg)
        {
            _lblErr.Text = msg;
        }
    }
}
