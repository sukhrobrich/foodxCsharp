using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.license
{
    public class LicenseLoginForm : Form
    {
        private TextBox _login;
        private TextBox _pass;
        private Label   _err;
        private Button  _btnEnter;

        private static readonly Color Gold    = Color.FromArgb(217, 119, 6);
        private static readonly Color Bg      = Color.FromArgb(15, 23, 42);
        private static readonly Color CardBg  = Color.White;
        private static readonly Color Muted   = Color.FromArgb(100, 116, 139);
        private static readonly Color Border  = Color.FromArgb(226, 232, 240);
        private static readonly Color Danger  = Color.FromArgb(220, 38, 38);

        // Muvaffaqiyatli kirishdan keyin saqlash uchun
        public string SavedLogin     { get; private set; }
        public string SavedPassword  { get; private set; }
        public int    SavedTenantId  { get; private set; }
        public bool   SavedIsOffline { get; private set; }
        public string SavedCafeName  { get; private set; }
        public string SavedExpiresAt { get; private set; }
        public int    SavedDaysLeft  { get; private set; } = int.MaxValue;

        public LicenseLoginForm(string initialError = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialError))
                ShowErr(initialError);

            // Faqat login nomini oldindan to'ldirish (parol har safar kiritiladi)
            var saved = LicenseService.LoadSaved();
            if (saved.HasValue)
                _login.Text = saved.Value.Item1;
        }

        private void InitializeComponent()
        {
            this.Text            = "FoodX — Litsenziya";
            this.Size            = new Size(420, 500);
            this.MinimumSize     = new Size(420, 500);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = Bg;

            // ── Header ────────────────────────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Bg };

            Label logo = new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 30, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true
            };
            Label sub = new Label
            {
                Text = "Dastur litsenziyasini tasdiqlang",
                Font = new Font("Segoe UI", 10), ForeColor = Muted, AutoSize = true
            };
            header.Controls.Add(logo);
            header.Controls.Add(sub);
            header.Resize += (s, e) =>
            {
                logo.Location = new Point((header.Width - logo.Width) / 2, 18);
                sub.Location  = new Point((header.Width - sub.Width) / 2, 62);
            };

            // ── Card ──────────────────────────────────────────────────
            Panel card = new Panel
            {
                Dock = DockStyle.Fill, BackColor = CardBg,
                Padding = new Padding(36, 28, 36, 28)
            };
            card.Paint += (s, e) =>
            {
                // Yuqori chiziq
                e.Graphics.FillRectangle(new SolidBrush(Gold), 0, 0, card.Width, 4);
            };

            int y = 10;

            // Login
            card.Controls.Add(MakeLabel("Login", y)); y += 22;
            _login = MakeInput(y); card.Controls.Add(_login); y += 46;

            // Parol
            card.Controls.Add(MakeLabel("Parol", y)); y += 22;
            _pass = MakeInput(y, isPass: true);
            _pass.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Enter(); };
            card.Controls.Add(_pass); y += 56;

            // Kirish tugmasi
            _btnEnter = new Button
            {
                Text = "Kirish", Location = new Point(0, y), Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = Gold,
                ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnEnter.FlatAppearance.BorderSize = 0;
            _btnEnter.Click += (s, e) => Enter();
            card.Controls.Add(_btnEnter); y += 58;

            // Xato xabari
            _err = new Label
            {
                Location = new Point(0, y), Height = 52,
                Font = new Font("Segoe UI", 9), ForeColor = Danger,
                TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, Visible = false
            };
            card.Controls.Add(_err);

            // Footer
            Label footer = new Label
            {
                Text = "Muammo bo'lsa sotuvchi bilan bog'laning",
                Font = new Font("Segoe UI", 8), ForeColor = Muted,
                Dock = DockStyle.Bottom, Height = 30,
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(footer);

            // Kengliklarga bog'lash
            card.Resize += (s, e) =>
            {
                int w = card.ClientSize.Width - 72;
                _login.Width = w; _pass.Width = w;
                _btnEnter.Width = w; _err.Width = w;
                _login.Location    = new Point(36, _login.Location.Y);
                _pass.Location     = new Point(36, _pass.Location.Y);
                _btnEnter.Location = new Point(36, _btnEnter.Location.Y);
                _err.Location      = new Point(36, _err.Location.Y);
            };

            this.Controls.Add(card);
            this.Controls.Add(header);
            this.Shown += (s, e) =>
            {
                if (string.IsNullOrEmpty(_login.Text)) _login.Focus();
                else _pass.Focus();
            };
        }

        private void Enter()
        {
            string l = _login.Text.Trim();
            string p = _pass.Text;
            if (string.IsNullOrEmpty(l)) { ShowErr("Login kiritilsin"); return; }
            if (string.IsNullOrEmpty(p)) { ShowErr("Parol kiritilsin"); return; }

            _btnEnter.Enabled = false;
            _btnEnter.Text    = "Tekshirilmoqda...";
            _err.Visible      = false;
            Application.DoEvents();

            var result = LicenseService.Verify(l, p);

            _btnEnter.Enabled = true;
            _btnEnter.Text    = "Kirish";

            if (result.Valid)
            {
                // Login va parolni keyingi session uchun saqlash (oflayn rejim va watchdog uchun)
                LicenseService.Save(l, p, result.TenantId);
                SavedLogin     = l;
                SavedPassword  = p;
                SavedTenantId  = result.TenantId;
                SavedIsOffline = result.Offline;
                SavedCafeName  = result.CafeName;
                SavedExpiresAt = result.ExpiresAt;
                SavedDaysLeft  = result.DaysLeft;

                if (result.Offline)
                    MessageBox.Show("Server bilan ulanish yo'q.\nOflayn rejimda ishlanmoqda.",
                        "FoodX — Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else if (result.DaysLeft <= 7 && result.DaysLeft >= 0)
                    MessageBox.Show("Diqqat: obuna muddati " + result.DaysLeft + " kun ichida tugaydi!\n(" + result.ExpiresAt + " gacha)",
                        "FoodX — Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                _pass.Clear();
                _pass.Focus();
                ShowErr(result.Message);
            }
        }

        private void ShowErr(string msg)
        {
            _err.Text    = msg;
            _err.Visible = true;
        }

        // ── Yordamchi metodlar ─────────────────────────────────────────────
        private static Label MakeLabel(string text, int y) => new Label
        {
            Text = text, Location = new Point(36, y), AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Muted
        };

        private static TextBox MakeInput(int y, bool isPass = false) => new TextBox
        {
            Location = new Point(36, y), Height = 36,
            Font = new Font("Segoe UI", 11),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 250, 252),
            PasswordChar = isPass ? '●' : '\0'
        };
    }
}
