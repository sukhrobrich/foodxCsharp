using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiPayForm : Form
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage   = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard   = Color.White;
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color Muted    = Color.FromArgb(156, 163, 175);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color Blue     = Color.FromArgb(37, 99, 235);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);

        private readonly MultiMonoblokClient _client;
        private readonly int                 _orderId;
        private readonly decimal             _total;
        private readonly List<string>        _payments;

        private int     _selectedPaymentId   = 0;
        private string  _selectedPaymentName = "";
        private Label   _lblStatus;
        private Button  _btnConfirm;
        private Panel   _payPanel;

        public MultiPayForm(MultiMonoblokClient client, int orderId, decimal total, List<string> payments)
        {
            _client   = client;
            _orderId  = orderId;
            _total    = total;
            _payments = payments;

            this.Text            = "FoodX - To'lov";
            this.Size            = new Size(500, 600);
            this.MinimumSize     = new Size(500, 600);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = BgCard;
            Build();
        }

        private void Build()
        {
            // Yuqori oltin chiziq
            Panel topLine = new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Gold };
            this.Controls.Add(topLine);

            // Header
            Panel header = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = BgCard };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            header.Controls.Add(new Label
            {
                Text = "To'lov qabul qilish",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(20, 14)
            });
            header.Controls.Add(new Label
            {
                Text = "Jami: " + FormatMoney(_total),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true, Location = new Point(20, 46)
            });
            this.Controls.Add(header);

            // To'lov usullari
            Label lblTitle = new Label
            {
                Text = "To'lov usulini tanlang:",
                Font = new Font("Segoe UI", 10), ForeColor = Muted,
                Dock = DockStyle.Top, Height = 36,
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(16, 0, 0, 0)
            };
            this.Controls.Add(lblTitle);

            _payPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = Math.Min(_payments.Count, 6) * 58 + 8,
                BackColor = BgPage,
                Padding = new Padding(12, 8, 12, 8)
            };
            BuildPaymentButtons();
            this.Controls.Add(_payPanel);

            // Xato/holat labeli
            _lblStatus = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Danger, Dock = DockStyle.Top, Height = 28,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_lblStatus);

            // Pastki panel: tugmalar
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = BgCard };
            bottom.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, bottom.Width, 0);

            _btnConfirm = new Button
            {
                Text = "Tasdiqlash", Width = 200, Height = 42,
                FlatStyle = FlatStyle.Flat, BackColor = Blue, ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.Click += OnConfirmClick;

            Button btnCancel = new Button
            {
                Text = "Bekor", Width = 110, Height = 42,
                FlatStyle = FlatStyle.Flat, BackColor = BgPage, ForeColor = TextDark,
                Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.BorderColor = Border;
            btnCancel.Click += (s, e) => { this.DialogResult = System.Windows.Forms.DialogResult.Cancel; this.Close(); };

            bottom.Controls.Add(_btnConfirm);
            bottom.Controls.Add(btnCancel);
            bottom.Resize += (s, e) =>
            {
                int totalW = _btnConfirm.Width + btnCancel.Width + 12;
                int startX = (bottom.Width - totalW) / 2;
                _btnConfirm.Location = new Point(startX, 11);
                btnCancel.Location   = new Point(startX + _btnConfirm.Width + 12, 11);
            };
            this.Controls.Add(bottom);
        }

        private void BuildPaymentButtons()
        {
            _payPanel.Controls.Clear();
            int y = 4;
            foreach (var p in _payments)
            {
                int    pid  = MultiMonoblokClient.JsonInt(p, "id");
                string name = MultiMonoblokClient.JsonStr(p, "name");
                if (pid <= 0 || string.IsNullOrEmpty(name)) continue;

                int capturedId   = pid;
                string capturedNm = name;

                Button btn = new Button
                {
                    Text      = name,
                    Location  = new Point(4, y),
                    Width     = _payPanel.Width - 20,
                    Height    = 50,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = BgCard,
                    ForeColor = TextDark,
                    Font      = new Font("Segoe UI", 11),
                    Cursor    = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                btn.Padding = new Padding(12, 0, 0, 0);
                btn.FlatAppearance.BorderSize  = 1;
                btn.FlatAppearance.BorderColor = Border;
                btn.Name = "pay_" + pid;

                btn.Click += (s, e) => SelectPayment(capturedId, capturedNm);
                _payPanel.Controls.Add(btn);
                y += 56;
            }
        }

        private void SelectPayment(int id, string name)
        {
            _selectedPaymentId   = id;
            _selectedPaymentName = name;

            // Barcha tugmalar rangini tiklash
            foreach (Control c in _payPanel.Controls)
            {
                if (c is Button b)
                {
                    bool selected = b.Name == "pay_" + id;
                    b.BackColor = selected ? Color.FromArgb(239, 246, 255) : BgCard;
                    b.ForeColor = selected ? Blue : TextDark;
                    b.FlatAppearance.BorderColor = selected ? Blue : Border;
                    b.Font = new Font("Segoe UI", 11, selected ? FontStyle.Bold : FontStyle.Regular);
                }
            }

            _btnConfirm.Text    = $"Tasdiqlash — {name}";
            _btnConfirm.Enabled = true;
        }

        private async void OnConfirmClick(object sender, EventArgs e)
        {
            if (_selectedPaymentId <= 0) return;

            _btnConfirm.Enabled = false;
            _btnConfirm.Text    = "Saqlanmoqda...";
            _lblStatus.Text     = "";

            try
            {
                await _client.PayOrderAsync(_orderId, _selectedPaymentId, _total);
                _lblStatus.ForeColor = Success;
                _lblStatus.Text      = "✓ To'lov qabul qilindi!";
                await Task.Delay(600);
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
            catch (MultiApiException ex)
            {
                string msg = MultiMonoblokClient.JsonStr(ex.Body, "message");
                _lblStatus.ForeColor = Danger;
                _lblStatus.Text = string.IsNullOrEmpty(msg) ? "Xatolik: HTTP " + ex.StatusCode : msg;
                _btnConfirm.Enabled = true;
                _btnConfirm.Text    = $"Tasdiqlash — {_selectedPaymentName}";
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Danger;
                _lblStatus.Text      = "Server xatosi: " + ex.Message;
                _btnConfirm.Enabled  = true;
                _btnConfirm.Text     = $"Tasdiqlash — {_selectedPaymentName}";
            }
        }

        private static string FormatMoney(decimal amount)
        {
            return string.Format("{0:N0}", amount).Replace(",", " ") + " so'm";
        }
    }
}
