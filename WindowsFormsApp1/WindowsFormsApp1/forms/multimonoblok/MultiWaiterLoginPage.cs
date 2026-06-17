using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiWaiterLoginPage : Form
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg    = Color.FromArgb(255, 248, 230);
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard    = Color.White;
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Danger    = Color.FromArgb(220, 38, 38);
        private static readonly Color Blue      = Color.FromArgb(59, 130, 246);

        private readonly MultiMonoblokClient _client;
        private FlowLayoutPanel _flpStaff;
        private Label           _lblCafe;
        private Label           _lblErr;
        private Label           _lblConn;
        private Timer           _licenseTimer;

        public MultiWaiterLoginPage(MultiMonoblokClient client)
        {
            _client = client;
            BuildUI();

            this.Load += async (s, e) => await LoadStaffAsync();

            // Har 3 daqiqada litsenziyani tekshirish
            _licenseTimer = new Timer { Interval = 180_000 };
            _licenseTimer.Tick += async (s, e) => await CheckLicenseAsync();
            _licenseTimer.Start();
        }

        private void BuildUI()
        {
            this.WindowState     = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor       = BgPage;
            this.Text            = "FoodX — Ofitsiant Terminali";

            // === FOOTER ===
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = BgCard };
            footer.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, footer.Width, 0);

            _lblConn = new Label
            {
                Text = $"Monoblok  •  {_client.ApiUrl}",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = TextMuted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            footer.Controls.Add(_lblConn);

            Button btnSetup = new Button
            {
                Text = "⚙ Sozlash",
                AutoSize = true, Height = 28,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand
            };
            btnSetup.FlatAppearance.BorderSize = 0;
            btnSetup.MouseEnter += (s, e) => btnSetup.ForeColor = Gold;
            btnSetup.MouseLeave += (s, e) => btnSetup.ForeColor = TextMuted;
            btnSetup.Click += (s, e) => OpenSetup();
            footer.Controls.Add(btnSetup);
            footer.Resize += (s, e) =>
                btnSetup.Location = new Point(footer.Width - btnSetup.Width - 12, (footer.Height - btnSetup.Height) / 2);

            this.Controls.Add(footer);

            // === HEADER ===
            Panel header = new Panel { Dock = DockStyle.Top, Height = 170, BackColor = BgCard };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);

            Label logo = new Label
            {
                Text = "FoodX",
                Font = new Font("Segoe UI", 52, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true
            };
            _lblCafe = new Label
            {
                Text = "Monoblok Terminali",
                Font = new Font("Segoe UI", 12), ForeColor = TextMuted, AutoSize = true
            };
            header.Controls.Add(logo);
            header.Controls.Add(_lblCafe);
            header.Resize += (s, e) =>
            {
                logo.Location   = new Point((header.Width - logo.Width) / 2, 22);
                _lblCafe.Location = new Point((header.Width - _lblCafe.Width) / 2, 106);
            };

            // === SELECT LABEL ===
            Panel lblPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = BgPage };
            var lblSelect = new Label
            {
                Text = "Ofitsiantni tanlang",
                Font = new Font("Segoe UI", 11), ForeColor = TextMuted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
            };
            lblPanel.Controls.Add(lblSelect);

            // === ERROR LABEL ===
            _lblErr = new Label
            {
                Text = "", Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Danger, Dock = DockStyle.Top, Height = 0,
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(255, 240, 240)
            };
            this.Controls.Add(_lblErr);

            // === STAFF GRID ===
            _flpStaff = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgPage,
                Padding = new Padding(40, 20, 40, 20),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            this.Controls.Add(_flpStaff);
            this.Controls.Add(_lblErr);
            this.Controls.Add(lblPanel);
            this.Controls.Add(header);
        }

        private async Task LoadStaffAsync()
        {
            if (this.IsDisposed) return;
            _flpStaff.Controls.Clear();
            ShowErr("");

            // Yuklanmoqda ko'rsatish
            _flpStaff.Controls.Add(new Label
            {
                Text = "⏳  Yuklanmoqda...",
                Font = new Font("Segoe UI", 14), ForeColor = TextMuted,
                AutoSize = true, Margin = new Padding(40)
            });

            try
            {
                string json = await _client.GetStaffListAsync();
                string cafeName  = MultiMonoblokClient.JsonStr(json, "cafeName");
                int    tenantId  = MultiMonoblokClient.JsonInt(json, "tenantId");

                if (!string.IsNullOrEmpty(cafeName))
                    _lblCafe.Text = cafeName;

                // tenantId ni config ga yangilash (birinchi marta 0 bo'lishi mumkin)
                if (tenantId > 0)
                    MultiMonoblokConfig.Save(_client.ApiUrl, tenantId);

                string staffJson = MultiMonoblokClient.JsonNested(json, "staff");
                var staff = MultiMonoblokClient.JsonArr(staffJson);

                _flpStaff.Controls.Clear();

                if (staff.Count == 0)
                {
                    ShowErr("Ofitsiantlar topilmadi.");
                    return;
                }

                foreach (var s in staff)
                {
                    int    id   = MultiMonoblokClient.JsonInt(s, "id");
                    string name = MultiMonoblokClient.JsonStr(s, "name");
                    string role = MultiMonoblokClient.JsonStr(s, "role_type");
                    _flpStaff.Controls.Add(MakeCard(id, name, role, tenantId));
                }
            }
            catch (MultiApiException ex)
            {
                _flpStaff.Controls.Clear();
                string msg = MultiMonoblokClient.JsonStr(ex.Body, "message");
                if (ex.StatusCode == 403)
                    ShowErr("Faqat mahalliy tarmoqdan kirish mumkin.");
                else if (ex.StatusCode == 404)
                    ShowErr("Litsenziya muddati tugagan yoki kafe topilmadi.");
                else
                    ShowErr(string.IsNullOrEmpty(msg) ? $"Xatolik: HTTP {ex.StatusCode}" : msg);
            }
            catch (Exception ex)
            {
                _flpStaff.Controls.Clear();
                ShowErr("Server bilan ulanib bo'lmadi:\n" + ex.Message);
            }
        }

        private Control MakeCard(int userId, string name, string role, int tenantId)
        {
            string initials = name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
            Color roleColor = role == "admin" ? Color.FromArgb(124, 58, 237)
                            : role == "kassir" ? Color.FromArgb(220, 38, 38)
                            : Color.FromArgb(22, 163, 74);

            Panel card = new Panel { Width = 192, Height = 208, Margin = new Padding(12), BackColor = BgCard, Cursor = Cursors.Hand };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 12))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    using (var pen = new Pen(Border, 1.5f)) e.Graphics.DrawPath(pen, path);
                }
            };

            card.Controls.Add(new Panel { Height = 4, Width = card.Width, BackColor = roleColor, Location = new Point(0, 0) });

            Panel avatar = new Panel { Width = 76, Height = 76, BackColor = Color.Transparent, Location = new Point((card.Width - 76) / 2, 20) };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(243, 244, 246)), 0, 0, 75, 75);
                e.Graphics.DrawEllipse(new Pen(roleColor, 2.5f), 1, 1, 72, 72);
                using (Font f = new Font("Segoe UI", 22, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(initials, f, new SolidBrush(roleColor), new RectangleF(0, 0, 75, 75), sf);
            };
            card.Controls.Add(avatar);

            card.Controls.Add(new Label { Text = name, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = TextDark, Width = 172, Height = 24, Location = new Point(10, 108), TextAlign = ContentAlignment.MiddleCenter });
            card.Controls.Add(new Label { Text = role, Font = new Font("Segoe UI", 9), ForeColor = roleColor, Width = 172, Height = 20, Location = new Point(10, 133), TextAlign = ContentAlignment.MiddleCenter });
            card.Controls.Add(new Panel { Height = 1, Width = 120, BackColor = Border, Location = new Point(36, 160) });
            card.Controls.Add(new Label { Text = "Kirish uchun bosing", Font = new Font("Segoe UI", 8), ForeColor = TextMuted, Width = 172, Height = 18, Location = new Point(10, 170), TextAlign = ContentAlignment.MiddleCenter });

            Action<bool> hover = on => { card.BackColor = on ? GoldBg : BgCard; card.Refresh(); };
            card.MouseEnter += (s, e) => hover(true);
            card.MouseLeave += (s, e) => hover(false);
            foreach (Control c in card.Controls) { c.MouseEnter += (s, e) => hover(true); c.MouseLeave += (s, e) => hover(false); }

            EventHandler onClick = async (s, e) => await LoginAsync(userId, name, role, tenantId);
            card.Click += onClick;
            foreach (Control c in card.Controls) c.Click += onClick;

            return card;
        }

        private async Task LoginAsync(int userId, string userName, string role, int tenantId)
        {
            ShowErr("");
            try
            {
                string json  = await _client.QuickLoginAsync(userId);
                string token = MultiMonoblokClient.JsonStr(json, "token");
                if (string.IsNullOrEmpty(token)) { ShowErr("Token olinmadi."); return; }

                _client.Token = token;

                var tablePage = new MultiTablePage(_client, userId, userName);
                tablePage.FormClosed += async (s, e) =>
                {
                    _client.Token = null;
                    this.Show();
                    await LoadStaffAsync();
                };
                this.Hide();
                tablePage.Show();
            }
            catch (MultiApiException ex)
            {
                string msg = MultiMonoblokClient.JsonStr(ex.Body, "message");
                ShowErr(string.IsNullOrEmpty(msg) ? $"Kirish muvaffaqiyatsiz ({ex.StatusCode})" : msg);
            }
            catch (Exception ex)
            {
                ShowErr("Xatolik: " + ex.Message);
            }
        }

        private async Task CheckLicenseAsync()
        {
            try
            {
                await _client.GetStaffListAsync();
            }
            catch (MultiApiException ex) when (ex.StatusCode == 404)
            {
                _licenseTimer.Stop();
                this.BeginInvoke(new Action(async () =>
                {
                    ShowErr("Litsenziya muddati tugadi! Yangilash uchun administrator bilan bog'laning.");
                    await LoadStaffAsync();
                }));
            }
            catch { }
        }

        private void OpenSetup()
        {
            using (var dlg = new MultiSetupForm())
            {
                var res = dlg.ShowDialog(this);
                if (res == DialogResult.OK)
                {
                    // Yangi IP bilan qayta ulanish uchun restart
                    Application.Restart();
                }
                else if (res == DialogResult.Abort)
                {
                    // Monoblok rejimi o'chirildi
                    Application.Restart();
                }
            }
        }

        private void ShowErr(string msg)
        {
            _lblErr.Text   = msg;
            _lblErr.Height = string.IsNullOrEmpty(msg) ? 0 : 40;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _licenseTimer?.Stop();
            _licenseTimer?.Dispose();
            base.OnFormClosed(e);
        }

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
