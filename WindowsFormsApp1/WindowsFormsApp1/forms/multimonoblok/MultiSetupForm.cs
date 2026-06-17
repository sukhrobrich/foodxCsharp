using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiSetupForm : Form
    {
        private static readonly Color Gold    = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage  = Color.FromArgb(249, 249, 251);
        private static readonly Color Muted   = Color.FromArgb(156, 163, 175);
        private static readonly Color Border  = Color.FromArgb(229, 231, 235);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);

        private TextBox _txtIp;
        private Label   _lblStatus;
        private Button  _btnConnect;

        // Muvaffaqiyatli sozlangandan keyin o'qiladi
        public string   SavedApiUrl  { get; private set; }
        public int      SavedTenantId { get; private set; }
        public string   SavedCafeName { get; private set; }

        public MultiSetupForm()
        {
            this.Text            = "FoodX — Qo'shimcha Monoblok";
            this.Size            = new Size(480, 420);
            this.MinimumSize     = new Size(480, 420);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = Color.White;

            Build();

            // Saqlangan sozlamani yuklash
            var cfg = MultiMonoblokConfig.Load();
            if (!string.IsNullOrEmpty(cfg.apiUrl))
            {
                string savedIp = cfg.apiUrl
                    .Replace("http://", "")
                    .Replace(":5050", "")
                    .Trim();
                _txtIp.Text = savedIp;
            }
        }

        private void Build()
        {
            // Yuqori oltin chiziq
            this.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Gold });

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(60, 0, 60, 0) };
            this.Controls.Add(main);

            // Logo
            main.Controls.Add(new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 32, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true, Location = new Point(134, 24)
            });

            // Sarlavha
            main.Controls.Add(new Label
            {
                Text = "Qo'shimcha Monoblok Sozlamasi",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(78, 80)
            });
            main.Controls.Add(new Label
            {
                Text = "Asosiy kompyuterning IP manzilini kiriting.\n(FoodX dasturi o'sha kompyuterda ishlab turishi kerak.)",
                Font = new Font("Segoe UI", 9), ForeColor = Muted,
                AutoSize = false, Width = 360, Height = 40, Location = new Point(0, 110)
            });

            // Ajratgich
            main.Controls.Add(new Panel { Height = 1, BackColor = Border, Location = new Point(0, 158), Width = 360 });

            // IP kiritish
            main.Controls.Add(new Label
            {
                Text = "Asosiy kompyuter IP manzili",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Muted, AutoSize = true, Location = new Point(0, 172)
            });

            _txtIp = new TextBox
            {
                Location = new Point(0, 196), Width = 360, Height = 38,
                Font = new Font("Segoe UI", 13),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark,
                TextAlign = HorizontalAlignment.Center
            };
            SetPlaceholder(_txtIp, "masalan: 192.168.1.5");
            _txtIp.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Connect(); };
            main.Controls.Add(_txtIp);

            // Holat labeli
            _lblStatus = new Label
            {
                Location = new Point(0, 242), Width = 360, Height = 22,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter, AutoSize = false
            };
            main.Controls.Add(_lblStatus);

            // Ulash tugmasi
            _btnConnect = new Button
            {
                Text = "Ulash", Location = new Point(0, 268),
                Width = 360, Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold), Cursor = Cursors.Hand
            };
            _btnConnect.FlatAppearance.BorderSize = 0;
            _btnConnect.Click += (s, e) => Connect();
            main.Controls.Add(_btnConnect);

            // Orqaga tugmasi
            Button btnBack = new Button
            {
                Text = "← Oddiy rejimga qaytish",
                Location = new Point(0, 328), Width = 360, Height = 26,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = Muted, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.MouseEnter += (s, e) => btnBack.ForeColor = Gold;
            btnBack.MouseLeave += (s, e) => btnBack.ForeColor = Muted;
            btnBack.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            main.Controls.Add(btnBack);

            // O'chirish tugmasi (agar allaqachon sozlangan bo'lsa)
            if (MultiMonoblokConfig.IsMultiMonoblokMode)
            {
                Button btnClear = new Button
                {
                    Text = "✕ Monoblok rejimini o'chirish",
                    Location = new Point(0, 360), Width = 360, Height = 22,
                    FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                    ForeColor = Danger, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand
                };
                btnClear.FlatAppearance.BorderSize = 0;
                btnClear.Click += (s, e) =>
                {
                    if (MessageBox.Show("Multimonoblok rejimini o'chirilsinmi?\n\nKeyingi ishga tushirishda oddiy rejimda ochiladi.",
                        "FoodX — Tasdiqlash", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        MultiMonoblokConfig.Clear();
                        DialogResult = DialogResult.Abort;
                        Close();
                    }
                };
                main.Controls.Add(btnClear);
            }

            main.Resize += (s, e) =>
            {
                int cx = (main.ClientSize.Width - 360) / 2;
                foreach (Control c in main.Controls)
                    if (c != null && !(c is Label && ((Label)c).Text.StartsWith("FoodX")) && !(c is Label && ((Label)c).Text.StartsWith("Qo'")))
                        c.Left = cx;
                _txtIp.Left      = cx;
                _lblStatus.Left  = cx;
                _btnConnect.Left = cx;
            };
        }

        private async void Connect()
        {
            string ip = _txtIp.Text.Trim();
            if (string.IsNullOrEmpty(ip)) { SetStatus("IP manzilini kiriting", Danger); return; }

            // IP dan URL yasash
            if (!ip.StartsWith("http"))
                ip = "http://" + ip;
            if (!ip.Contains(":5050") && !ip.Contains(":5051"))
                ip = ip.TrimEnd('/') + ":5050";

            string apiUrl = ip;

            _btnConnect.Enabled = false;
            _btnConnect.Text    = "Tekshirilmoqda...";
            SetStatus("", Muted);

            try
            {
                var client = new MultiMonoblokClient(apiUrl, 0);
                string json = await client.GetStaffListAsync();

                int tid      = MultiMonoblokClient.JsonInt(json, "tenantId");
                string cname = MultiMonoblokClient.JsonStr(json, "cafeName");

                if (tid <= 0)
                {
                    SetStatus("✗  Server topildi, lekin kafe aniqlanmadi.", Danger);
                    return;
                }

                MultiMonoblokConfig.Save(apiUrl, tid);

                SavedApiUrl   = apiUrl;
                SavedTenantId = tid;
                SavedCafeName = cname;

                SetStatus($"✓  Ulandi! Kafe: {cname}", Success);
                await Task.Delay(800);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (MultiApiException ex)
            {
                string msg = MultiMonoblokClient.JsonStr(ex.Body, "message");
                SetStatus("✗  " + (string.IsNullOrEmpty(msg) ? $"HTTP {ex.StatusCode}" : msg), Danger);
            }
            catch (Exception ex)
            {
                string hint = ex.Message.Contains("refused") || ex.Message.Contains("connect")
                    ? "Kompyuter IP manzilini yoki port 5050 ni tekshiring."
                    : ex.Message;
                SetStatus("✗  " + hint, Danger);
            }
            finally
            {
                _btnConnect.Enabled = true;
                _btnConnect.Text    = "Ulash";
            }
        }

        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text      = text;
            _lblStatus.ForeColor = color;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

        private static void SetPlaceholder(TextBox tb, string hint)
        {
            tb.HandleCreated += (s, e) => SendMessage(tb.Handle, 0x1501, (IntPtr)1, hint);
        }
    }
}
