using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiPinDialog : Form
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color BgCard    = Color.White;
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);

        private const int LEFT_W = 420;
        private const int FORM_W = 980;
        private const int FORM_H = 680;
        private const int RIGHT_W = FORM_W - LEFT_W;
        private const int PIN_LENGTH = 4;

        private readonly MultiMonoblokClient _client;
        private readonly string _waiterName;
        private readonly string _login;

        private TextBox _txtPin;
        private Label   _lblErr;
        private Timer   _clearTimer;

        public MultiPinDialog(MultiMonoblokClient client, string waiterName, string login)
        {
            _client      = client;
            _waiterName  = waiterName;
            _login       = login;
            BuildUI();
            StartClearTimer();
        }

        private void BuildUI()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size            = new Size(FORM_W, FORM_H);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = BgCard;

            // ── CHAP: brand panel ──────────────────────────────────────────
            Panel left = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(LEFT_W, FORM_H),
                BackColor = Color.FromArgb(255, 248, 230)
            };
            left.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Color.FromArgb(253, 230, 138)), left.Width - 1, 0, left.Width - 1, left.Height);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(40, 217, 119, 6), 1))
                {
                    e.Graphics.DrawEllipse(pen, -60, left.Height - 180, 240, 240);
                    e.Graphics.DrawEllipse(pen, left.Width - 80, -80, 180, 180);
                }
            };
            this.Controls.Add(left);

            Label logo = new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 52, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true
            };
            Label tagline = new Label
            {
                Text = "Ofitsiant Terminali", Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(180, 120, 20), AutoSize = true
            };
            left.Controls.Add(logo);
            left.Controls.Add(tagline);
            left.Resize += (s, e) =>
            {
                logo.Location    = new Point((left.Width - logo.Width) / 2, (left.Height / 2) - 70);
                tagline.Location = new Point((left.Width - tagline.Width) / 2, (left.Height / 2) + 10);
            };

            // ── O'NG: PIN panel ────────────────────────────────────────────
            Panel right = new Panel
            {
                Location  = new Point(LEFT_W, 0),
                Size      = new Size(RIGHT_W, FORM_H),
                BackColor = BgCard
            };
            this.Controls.Add(right);

            int cx = RIGHT_W / 2;

            right.Controls.Add(new Label
            {
                Text = "Xush kelibsiz!", Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = TextDark, Width = RIGHT_W, Height = 30,
                Location = new Point(0, 48), TextAlign = ContentAlignment.MiddleCenter, AutoSize = false
            });
            right.Controls.Add(new Label
            {
                Text = _waiterName.ToUpper(), Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Gold, Width = RIGHT_W, Height = 22,
                Location = new Point(0, 84), TextAlign = ContentAlignment.MiddleCenter, AutoSize = false
            });
            right.Controls.Add(new Label
            {
                Text = "4-raqamli PIN ni kiriting",
                Font = new Font("Segoe UI", 9), ForeColor = TextMuted,
                Width = RIGHT_W, Height = 18, Location = new Point(0, 112),
                TextAlign = ContentAlignment.MiddleCenter, AutoSize = false
            });

            // Xato labeli
            _lblErr = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 38, 38), Width = RIGHT_W, Height = 20,
                Location = new Point(0, 132), TextAlign = ContentAlignment.MiddleCenter, AutoSize = false
            };
            right.Controls.Add(_lblErr);

            // PIN display
            int pinBoxW = 280;
            int pinBoxX = cx - pinBoxW / 2;
            Panel pinBox = new Panel
            {
                Location = new Point(pinBoxX, 156), Size = new Size(pinBoxW, 52), BackColor = BgPage
            };
            pinBox.Paint += (s, e) =>
                e.Graphics.DrawRectangle(new Pen(Border), 0, 0, pinBox.Width - 1, pinBox.Height - 1);

            _txtPin = new TextBox
            {
                ReadOnly              = true,
                UseSystemPasswordChar = true,
                Font                  = new Font("Segoe UI", 26, FontStyle.Bold),
                TextAlign             = HorizontalAlignment.Center,
                Dock                  = DockStyle.Fill,
                BackColor             = BgPage,
                ForeColor             = Gold,
                BorderStyle           = BorderStyle.None
            };
            pinBox.Controls.Add(_txtPin);
            right.Controls.Add(pinBox);

            right.Controls.Add(new Panel
            {
                Location = new Point(pinBoxX, 210), Size = new Size(pinBoxW, 3), BackColor = Gold
            });

            // Numpad
            int keypadW = 300;
            TableLayoutPanel keypad = new TableLayoutPanel
            {
                Location        = new Point(cx - keypadW / 2, 226),
                Size            = new Size(keypadW, 300),
                RowCount        = 4,
                ColumnCount     = 3,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            for (int i = 0; i < 3; i++) keypad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            for (int i = 0; i < 4; i++) keypad.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            int n = 1;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    keypad.Controls.Add(MakeDigitBtn((n++).ToString()), c, r);

            keypad.Controls.Add(MakeActionBtn("←", Color.FromArgb(229, 231, 235), Color.FromArgb(100, 100, 110), (s, e) =>
            {
                if (_txtPin.Text.Length > 0)
                    _txtPin.Text = _txtPin.Text.Substring(0, _txtPin.Text.Length - 1);
            }), 0, 3);
            keypad.Controls.Add(MakeDigitBtn("0"), 1, 3);
            keypad.Controls.Add(MakeActionBtn("OK", Color.FromArgb(22, 163, 74), Color.White, BtnLogin_Click), 2, 3);
            right.Controls.Add(keypad);

            // Pastki tugmalar
            int bx = cx - 150;
            Panel bottomRow = new Panel
            {
                Location = new Point(bx, 538), Size = new Size(300, 40), BackColor = Color.Transparent
            };
            right.Controls.Add(bottomRow);

            Button btnClear = MakeLinkBtn("Tozalash");
            btnClear.Click   += (s, e) => { _txtPin.Text = ""; _lblErr.Text = ""; };
            btnClear.Location  = new Point(0, 6);
            bottomRow.Controls.Add(btnClear);

            Button btnBack = MakeLinkBtn("← Orqaga");
            btnBack.Click   += (s, e) => { this.DialogResult = System.Windows.Forms.DialogResult.Cancel; this.Close(); };
            btnBack.Location  = new Point(190, 6);
            bottomRow.Controls.Add(btnBack);
        }

        private Button MakeDigitBtn(string digit)
        {
            Button b = new Button
            {
                Text      = digit,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(4),
                Font      = new Font("Segoe UI", 17, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = Color.FromArgb(17, 24, 39),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize  = 1;
            b.FlatAppearance.BorderColor = Border;
            b.MouseEnter += (s, e) => { b.BackColor = Color.FromArgb(255, 248, 230); b.FlatAppearance.BorderColor = Gold; };
            b.MouseLeave += (s, e) => { b.BackColor = Color.FromArgb(243, 244, 246); b.FlatAppearance.BorderColor = Border; };
            b.Click      += (s, e) => { if (_txtPin.Text.Length < PIN_LENGTH) { _txtPin.Text += digit; ResetTimer(); } };
            return b;
        }

        private Button MakeActionBtn(string text, Color bg, Color fg, EventHandler handler)
        {
            Button b = new Button
            {
                Text      = text,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(4),
                Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += handler;
            return b;
        }

        private Button MakeLinkBtn(string text)
        {
            Button b = new Button
            {
                Text      = text,
                Width     = 110,
                Height    = 28,
                Font      = new Font("Segoe UI", 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TextMuted,
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (s, e) => b.ForeColor = Gold;
            b.MouseLeave += (s, e) => b.ForeColor = TextMuted;
            return b;
        }

        private void StartClearTimer()
        {
            _clearTimer = new Timer { Interval = 8000 };
            _clearTimer.Tick += (s, e) => _txtPin.Text = "";
            _clearTimer.Start();
        }

        private void ResetTimer() { _clearTimer.Stop(); _clearTimer.Start(); }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (_txtPin.Text.Length != PIN_LENGTH) { await ShakeError(); return; }

            string pin = _txtPin.Text;
            _lblErr.Text = "";

            try
            {
                string json  = await _client.LoginWithPinAsync(_login, pin);
                string token = MultiMonoblokClient.JsonStr(json, "token");

                if (string.IsNullOrEmpty(token)) { await ShakeError(); return; }

                _client.Token = token;
                _clearTimer.Stop();
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
            catch (MultiApiException ex)
            {
                string msg = MultiMonoblokClient.JsonStr(ex.Body, "message");
                if (ex.StatusCode == 401)
                    _lblErr.Text = "PIN noto'g'ri";
                else if (ex.StatusCode == 403)
                    _lblErr.Text = "Litsenziya tugagan!";
                else
                    _lblErr.Text = string.IsNullOrEmpty(msg) ? "Xatolik: " + ex.StatusCode : msg;
                await ShakeError();
            }
            catch (Exception ex)
            {
                _lblErr.Text = "Server bilan ulanib bo'lmadi";
                await ShakeError();
            }
        }

        private async Task ShakeError()
        {
            int x = this.Location.X;
            for (int i = 0; i < 3; i++)
            {
                this.Location = new Point(x - 8, this.Location.Y);
                await Task.Delay(30);
                this.Location = new Point(x + 8, this.Location.Y);
                await Task.Delay(30);
            }
            this.Location = new Point(x, this.Location.Y);
            _txtPin.Text  = "";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _clearTimer?.Stop();
            _clearTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
