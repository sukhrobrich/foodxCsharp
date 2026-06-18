using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiCashierPage : Form
    {
        private static readonly Color Gold    = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg  = Color.FromArgb(255, 248, 230);
        private static readonly Color BgPage  = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard  = Color.White;
        private static readonly Color TextDark= Color.FromArgb(17, 24, 39);
        private static readonly Color Muted   = Color.FromArgb(107, 114, 128);
        private static readonly Color Border  = Color.FromArgb(229, 231, 235);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger  = Color.FromArgb(220, 38, 38);
        private static readonly Color Blue    = Color.FromArgb(37, 99, 235);

        private readonly MultiMonoblokClient _client;
        private readonly string              _userName;

        private FlowLayoutPanel _flpOrders;
        private Label           _lblStatus;
        private Label           _lblCount;
        private Timer           _timer;

        public MultiCashierPage(MultiMonoblokClient client, string userName)
        {
            _client   = client;
            _userName = userName;
            BuildUI();
            this.Load += async (s, e) => await RefreshAsync();

            _timer = new Timer { Interval = 5000 };
            _timer.Tick += async (s, e) => await RefreshAsync();
            _timer.Start();
        }

        private void BuildUI()
        {
            this.WindowState     = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor       = BgPage;
            this.Text            = "FoodX — Kassa";

            // === HEADER ===
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = BgCard };
            header.Paint += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(Danger), 0, 0, header.Width, 4);
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            };

            header.Controls.Add(new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true, Location = new Point(16, 20)
            });

            _lblCount = new Label
            {
                Text = "Kassa", Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Danger, AutoSize = true
            };
            header.Controls.Add(_lblCount);
            header.Resize += (s, e) =>
                _lblCount.Location = new Point((header.Width - _lblCount.Width) / 2, (header.Height - _lblCount.Height) / 2);

            _lblStatus = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9),
                ForeColor = Muted, AutoSize = true
            };
            header.Controls.Add(_lblStatus);
            header.Resize += (s, e) =>
                _lblStatus.Location = new Point(header.Width - _lblStatus.Width - 180, (header.Height - _lblStatus.Height) / 2);

            Button btnRefresh = new Button
            {
                Text = "↺ Yangilash", Width = 120, Height = 36,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (s, e) => await RefreshAsync();

            Button btnLogout = new Button
            {
                Text = "✕ Chiqish", Width = 110, Height = 36,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) => this.Close();

            Panel btnPanel = new Panel { Width = 250, Height = 64, BackColor = Color.Transparent };
            btnPanel.Controls.Add(btnRefresh);
            btnPanel.Controls.Add(btnLogout);
            btnRefresh.Location = new Point(0, 14);
            btnLogout.Location  = new Point(130, 14);
            header.Controls.Add(btnPanel);
            header.Resize += (s, e) => btnPanel.Location = new Point(header.Width - 260, 0);

            // === ORDERS GRID ===
            _flpOrders = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                AutoScroll    = true,
                BackColor     = BgPage,
                Padding       = new Padding(24, 20, 24, 20),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true
            };

            this.Controls.Add(_flpOrders);
            this.Controls.Add(header);
        }

        private async Task RefreshAsync()
        {
            if (this.IsDisposed) return;
            try
            {
                string json   = await _client.GetActiveOrdersAsync();
                var orders    = MultiMonoblokClient.JsonArr(json);

                if (this.IsDisposed) return;
                this.BeginInvoke(new Action(() => BuildOrderCards(orders)));
            }
            catch { }
        }

        private void BuildOrderCards(List<string> orders)
        {
            if (this.IsDisposed) return;

            _flpOrders.SuspendLayout();
            _flpOrders.Controls.Clear();

            int count = orders.Count;
            _lblCount.Text = count == 0 ? "Kassa — faol buyurtma yo'q"
                                        : $"Kassa — {count} ta faol buyurtma";

            if (count == 0)
            {
                _flpOrders.Controls.Add(new Label
                {
                    Text      = "Hozircha to'lanmagan buyurtma yo'q.",
                    Font      = new Font("Segoe UI", 13),
                    ForeColor = Muted,
                    AutoSize  = true,
                    Margin    = new Padding(40)
                });
                _flpOrders.ResumeLayout();
                return;
            }

            foreach (var o in orders)
                _flpOrders.Controls.Add(MakeOrderCard(o));

            _flpOrders.ResumeLayout();
        }

        private Control MakeOrderCard(string o)
        {
            int     orderId    = MultiMonoblokClient.JsonInt(o, "id");
            string  placeName  = MultiMonoblokClient.JsonStr(o, "place_name");
            decimal total      = MultiMonoblokClient.JsonDec(o, "total");
            int     itemsCount = MultiMonoblokClient.JsonInt(o, "items_count");
            string  custName   = MultiMonoblokClient.JsonStr(o, "customer_name");
            string  createdAt  = MultiMonoblokClient.JsonStr(o, "created_at");

            string timeStr = "";
            if (DateTime.TryParse(createdAt, out DateTime dt))
                timeStr = dt.ToString("HH:mm");

            string tableLbl = string.IsNullOrEmpty(placeName) ? $"#{orderId}" : placeName;

            Panel card = new Panel
            {
                Width = 240, Height = 160,
                Margin = new Padding(8), BackColor = BgCard, Cursor = Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 12))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    using (var pen = new Pen(Blue, 2)) e.Graphics.DrawPath(pen, path);
                }
            };

            card.Controls.Add(new Panel { Height = 4, Width = card.Width, BackColor = Blue, Location = new Point(0, 0) });

            // Stol nomi + vaqt
            card.Controls.Add(new Label
            {
                Text = tableLbl, Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark, Width = 160, Height = 26,
                Location = new Point(12, 12), TextAlign = ContentAlignment.MiddleLeft
            });
            card.Controls.Add(new Label
            {
                Text = timeStr, Font = new Font("Segoe UI", 9),
                ForeColor = Muted, Width = 60, Height = 26,
                Location = new Point(172, 12), TextAlign = ContentAlignment.MiddleRight
            });

            // Ajratuvchi
            card.Controls.Add(new Panel { Height = 1, Width = 216, BackColor = Border, Location = new Point(12, 44) });

            // Taomlar soni
            card.Controls.Add(new Label
            {
                Text = $"{itemsCount} ta taom", Font = new Font("Segoe UI", 9),
                ForeColor = Muted, Width = 216, Height = 20,
                Location = new Point(12, 52), TextAlign = ContentAlignment.MiddleLeft
            });

            // Mijoz
            if (!string.IsNullOrEmpty(custName))
                card.Controls.Add(new Label
                {
                    Text = custName, Font = new Font("Segoe UI", 9),
                    ForeColor = Muted, Width = 216, Height = 20,
                    Location = new Point(12, 72), TextAlign = ContentAlignment.MiddleLeft
                });

            // Jami
            card.Controls.Add(new Label
            {
                Text = FormatMoney(total),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark, Width = 216, Height = 28,
                Location = new Point(12, 92), TextAlign = ContentAlignment.MiddleLeft
            });

            // To'lov tugmasi
            Button btnPay = new Button
            {
                Text = "To'lov →", Width = 216, Height = 34,
                Location = new Point(12, 120),
                FlatStyle = FlatStyle.Flat, BackColor = Blue, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnPay.FlatAppearance.BorderSize = 0;
            btnPay.Click += async (s, e) => await OpenPayForOrderAsync(orderId, total);
            card.Controls.Add(btnPay);

            // Karta ustiga bosish = to'lov (btnPay dan tashqari)
            EventHandler onClick = (s, e) =>
            {
                if (!(s is Button)) OpenOrderDetail(orderId, placeName);
            };
            card.Click += onClick;
            foreach (Control c in card.Controls)
                if (!(c is Button)) c.Click += onClick;

            return card;
        }

        private void OpenOrderDetail(int orderId, string tableName)
        {
            _timer.Stop();
            var form = new MultiOrderForm(_client, 0, tableName, orderId, "kassir");
            form.FormClosed += async (s, e) =>
            {
                await RefreshAsync();
                _timer.Start();
            };
            form.ShowDialog(this);
        }

        private async Task OpenPayForOrderAsync(int orderId, decimal total)
        {
            string payJson;
            try { payJson = await _client.GetPaymentsAsync(); }
            catch { MessageBox.Show("To'lov usullari yuklanmadi.", "FoodX", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var payments = MultiMonoblokClient.JsonArr(payJson);
            if (payments.Count == 0) { MessageBox.Show("To'lov usullari topilmadi.", "FoodX", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            _timer.Stop();
            using (var dlg = new MultiPayForm(_client, orderId, total, payments))
            {
                if (dlg.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                    await RefreshAsync();
            }
            _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Dispose();
            base.OnFormClosed(e);
        }

        private static string FormatMoney(decimal amount)
            => string.Format("{0:N0}", amount).Replace(",", " ") + " so'm";

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            p.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
