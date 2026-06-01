using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1;
using WindowsFormsApp1.forms.main;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.user
{
    public partial class Password : Form
    {
        private readonly string login;
        private readonly bool isLoginMode;

        private TextBox txtPin;
        private TableLayoutPanel keypad;
        private Timer clearTimer;
        private const int PIN_LENGTH = 4;

        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color BgCard    = Color.White;
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);

        private const int LEFT_W  = 420;  // left brand panel width
        private const int FORM_W  = 980;
        private const int FORM_H  = 680;
        private const int RIGHT_W = FORM_W - LEFT_W;  // 560

        public Password(string login1, bool loginMode)
        {
            login = login1.ToLower();
            isLoginMode = loginMode;
            RunMigration();
            BuildUI();
            StartClearTimer();
        }

        private void RunMigration()
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                // Add role_type column if missing
                using (SqlCommand cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME='user_category' AND COLUMN_NAME='role_type')
                    BEGIN
                        ALTER TABLE user_category ADD role_type NVARCHAR(20);
                        UPDATE user_category SET role_type = LOWER(name);
                        UPDATE user_category SET role_type = 'ofitsiant'
                            WHERE role_type NOT IN ('admin','kassir','ofitsiant') OR role_type IS NULL;
                    END", db.GetCon()))
                    cmd.ExecuteNonQuery();

                // Hash any remaining plain-text PINs (4 digits)
                var toHash = new System.Collections.Generic.List<(int id, string pin)>();
                using (SqlCommand sel = new SqlCommand("SELECT id, password FROM [user] WHERE LEN(password)=4", db.GetCon()))
                using (SqlDataReader dr = sel.ExecuteReader())
                    while (dr.Read())
                    {
                        string pw = dr["password"].ToString();
                        if (Session.IsPlainPin(pw)) toHash.Add((Convert.ToInt32(dr["id"]), pw));
                    }
                foreach (var (uid, pin) in toHash)
                    using (SqlCommand upd = new SqlCommand("UPDATE [user] SET password=@h WHERE id=@id", db.GetCon()))
                    {
                        upd.Parameters.AddWithValue("@h", Session.HashPin(pin));
                        upd.Parameters.AddWithValue("@id", uid);
                        upd.ExecuteNonQuery();
                    }

                db.CloseCon();
            }
            catch { }
        }

        private void BuildUI()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size            = new Size(FORM_W, FORM_H);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = BgCard;

            // ── LEFT brand panel ───────────────────────────────────────────
            Panel left = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(LEFT_W, FORM_H),
                BackColor = Color.FromArgb(255, 248, 230)
            };
            left.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Color.FromArgb(253, 230, 138)), left.Width - 1, 0, left.Width - 1, left.Height);
                // decorative circles
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
                Text      = "FoodX",
                Font      = new Font("Segoe UI", 52, FontStyle.Bold),
                ForeColor = Gold,
                AutoSize  = true
            };
            Label tagline = new Label
            {
                Text      = "Kafe Boshqaruv Tizimi",
                Font      = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(180, 120, 20),
                AutoSize  = true
            };
            left.Controls.Add(logo);
            left.Controls.Add(tagline);
            left.Resize += (s, e) =>
            {
                logo.Location    = new Point((left.Width - logo.Width) / 2, (left.Height / 2) - 70);
                tagline.Location = new Point((left.Width - tagline.Width) / 2, (left.Height / 2) + 10);
            };

            // ── RIGHT PIN panel (hardcoded positions, RIGHT_W = 560) ───────
            Panel right = new Panel
            {
                Location  = new Point(LEFT_W, 0),
                Size      = new Size(RIGHT_W, FORM_H),
                BackColor = BgCard
            };
            this.Controls.Add(right);

            // All controls are centered based on RIGHT_W = 560
            int cx = RIGHT_W / 2;  // 280

            // Title
            Label lblTitle = new Label
            {
                Text      = isLoginMode ? "Xush kelibsiz!" : "Admin paroli o'rnatish",
                Font      = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = TextDark,
                Width     = RIGHT_W,
                Height    = 30,
                Location  = new Point(0, 48),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize  = false
            };
            right.Controls.Add(lblTitle);

            // Login name
            Label lblLogin = new Label
            {
                Text      = isLoginMode ? login.ToUpper() : "ADMIN",
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Gold,
                Width     = RIGHT_W,
                Height    = 22,
                Location  = new Point(0, 84),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize  = false
            };
            right.Controls.Add(lblLogin);

            // Subtitle
            Label lblSub = new Label
            {
                Text      = isLoginMode
                    ? "4-raqamli PIN ni kiriting"
                    : "Ilk o'rnatish: admin uchun PIN kiriting",
                Font      = new Font("Segoe UI", 9),
                ForeColor = TextMuted,
                Width     = RIGHT_W,
                Height    = 18,
                Location  = new Point(0, 112),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize  = false
            };
            right.Controls.Add(lblSub);

            // Ilk o'rnatish uchun qo'shimcha izoh
            if (!isLoginMode)
            {
                Panel setupNote = new Panel
                {
                    Location  = new Point(60, 132),
                    Size      = new Size(RIGHT_W - 120, 40),
                    BackColor = Color.FromArgb(254, 243, 199)
                };
                setupNote.Paint += (s, e) =>
                    e.Graphics.DrawRectangle(new Pen(Color.FromArgb(253, 230, 138)), 0, 0, setupNote.Width-1, setupNote.Height-1);
                setupNote.Controls.Add(new Label
                {
                    Text = "⚠️  Ushbu PIN keyingi barcha kirish uchun ishlatiladi",
                    Font = new Font("Segoe UI", 8),
                    ForeColor = Color.FromArgb(146, 64, 14),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                right.Controls.Add(setupNote);
            }

            // PIN display box
            int pinBoxW = 280;
            int pinBoxX = cx - pinBoxW / 2;  // 140
            int pinBoxY = isLoginMode ? 138 : 180;
            Panel pinBox = new Panel
            {
                Location  = new Point(pinBoxX, pinBoxY),
                Size      = new Size(pinBoxW, 52),
                BackColor = BgPage
            };
            pinBox.Paint += (s, e) =>
                e.Graphics.DrawRectangle(new Pen(Border), 0, 0, pinBox.Width - 1, pinBox.Height - 1);

            txtPin = new TextBox
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
            pinBox.Controls.Add(txtPin);
            right.Controls.Add(pinBox);

            // Gold underline
            Panel pinLine = new Panel
            {
                Location  = new Point(pinBoxX, pinBoxY + 54),
                Size      = new Size(pinBoxW, 3),
                BackColor = Gold
            };
            right.Controls.Add(pinLine);

            // KEYPAD (300x300, centered at x = (560-300)/2 = 130)
            int keypadW = 300;
            int keypadH = 300;
            int keypadX = cx - keypadW / 2;  // 130
            keypad = new TableLayoutPanel
            {
                Location        = new Point(keypadX, pinBoxY + 70),
                Size            = new Size(keypadW, keypadH),
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
                if (txtPin.Text.Length > 0)
                    txtPin.Text = txtPin.Text.Substring(0, txtPin.Text.Length - 1);
            }), 0, 3);
            keypad.Controls.Add(MakeDigitBtn("0"), 1, 3);
            keypad.Controls.Add(MakeActionBtn("OK", Color.FromArgb(22, 163, 74), Color.White, BtnLogin_Click), 2, 3);
            right.Controls.Add(keypad);

            // Bottom action buttons
            int bottomW = 300;
            int bottomX = cx - bottomW / 2;  // 130
            Panel bottomRow = new Panel
            {
                Location  = new Point(bottomX, 522),
                Size      = new Size(bottomW, 40),
                BackColor = Color.Transparent
            };
            right.Controls.Add(bottomRow);

            Button btnClear = MakeLinkBtn("Tozalash");
            btnClear.Click    += (s, e) => txtPin.Text = "";
            btnClear.Location  = new Point(0, 6);
            bottomRow.Controls.Add(btnClear);

            Button btnBack = MakeLinkBtn("← Orqaga");
            btnBack.Click    += GoBack;
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
            b.Click      += (s, e) => { if (txtPin.Text.Length < PIN_LENGTH) { txtPin.Text += digit; ResetTimer(); } };
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
            clearTimer = new Timer { Interval = 8000 };
            clearTimer.Tick += (s, e) => txtPin.Text = "";
            clearTimer.Start();
        }

        private void ResetTimer() { clearTimer.Stop(); clearTimer.Start(); }

        private void GoBack(object s, EventArgs e)
        {
            this.Hide();
            new Form1().Show();
        }

        private async void ShakeError()
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
            txtPin.Text = "";
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (txtPin.Text.Length != PIN_LENGTH) { ShakeError(); return; }

            string password = txtPin.Text;
            string hashedPin = Session.HashPin(password);
            dbconnect db = new dbconnect();

            if (isLoginMode)
            {
                try
                {
                    // Compare against hashed PIN; also accept plain PIN for backward-compat (will be auto-hashed on next RunMigration)
                    string query = @"SELECT u.id, u.name, u.login,
                                            ISNULL(uc.role_type, LOWER(uc.name)) AS category
                                     FROM [user] u
                                     JOIN user_category uc ON uc.id = u.user_category_id
                                     WHERE u.login = @login AND (u.password = @hash OR u.password = @plain)";

                    using (SqlCommand cmd = new SqlCommand(query, db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@login", login);
                        cmd.Parameters.AddWithValue("@hash",  hashedPin);
                        cmd.Parameters.AddWithValue("@plain", password);
                        db.OpenCon();
                        SqlDataReader dr = cmd.ExecuteReader();
                        if (dr.Read())
                        {
                            Session.UserId       = Convert.ToInt32(dr["id"]);
                            Session.Login        = dr["login"].ToString();
                            Session.UserName     = dr["name"].ToString();
                            Session.UserCategory = dr["category"].ToString().ToLower();
                            dr.Close();
                            db.CloseCon();
                            this.Hide();
                            if (Session.UserCategory == "admin")
                                new MainPage(Session.UserId).Show();
                            else if (Session.UserCategory == "kassir")
                                new KassirPage().Show();
                            else
                                new WaiterPage().Show();
                        }
                        else
                        {
                            dr.Close();
                            db.CloseCon();
                            ShakeError();
                        }
                    }
                }
                catch (Exception ex) { db.CloseCon(); MessageBox.Show("Xatolik: " + ex.Message); }
            }
            else
            {
                try
                {
                    db.OpenCon();

                    // Kategoriyalarni role_type BILAN yaratish (JWT auth uchun muhim)
                    int adminCatId = ExecScalarOrExisting(db,
                        "IF NOT EXISTS(SELECT 1 FROM user_category WHERE LOWER(name)='admin') " +
                        "  INSERT INTO user_category(name,role_type,color) VALUES('admin','admin','#DC2626');" +
                        "SELECT id FROM user_category WHERE LOWER(name)='admin'");
                    ExecNonQuerySafe(db,
                        "IF NOT EXISTS(SELECT 1 FROM user_category WHERE LOWER(name)='kassir') " +
                        "  INSERT INTO user_category(name,role_type,color) VALUES('kassir','kassir','#2563EB')");
                    ExecNonQuerySafe(db,
                        "IF NOT EXISTS(SELECT 1 FROM user_category WHERE LOWER(name)='ofitsiant') " +
                        "  INSERT INTO user_category(name,role_type,color) VALUES('ofitsiant','ofitsiant','#D97706')");

                    // Default to'lov usullari
                    ExecNonQuerySafe(db, "IF NOT EXISTS(SELECT 1 FROM payment WHERE name='Naqd') INSERT INTO payment(name,sort_order) VALUES('Naqd',1)");
                    ExecNonQuerySafe(db, "IF NOT EXISTS(SELECT 1 FROM payment WHERE name='Plastik') INSERT INTO payment(name,sort_order) VALUES('Plastik',2)");
                    ExecNonQuerySafe(db, "IF NOT EXISTS(SELECT 1 FROM payment WHERE name='Payme') INSERT INTO payment(name,sort_order) VALUES('Payme',3)");

                    // Admin foydalanuvchi
                    int newUID = 0;
                    using (SqlCommand ins = new SqlCommand(
                        "IF NOT EXISTS(SELECT 1 FROM [user] WHERE LOWER(login)='admin') " +
                        "  INSERT INTO [user](name,user_category_id,login,password,phone_number,created_at) " +
                        "  VALUES('Admin',@cid,'admin',@pw,'',GETDATE());" +
                        "SELECT id FROM [user] WHERE LOWER(login)='admin'",
                        db.GetCon()))
                    {
                        ins.Parameters.AddWithValue("@cid", adminCatId);
                        ins.Parameters.AddWithValue("@pw", hashedPin);
                        newUID = Convert.ToInt32(ins.ExecuteScalar());
                    }

                    // Parolni yangilash (agar user allaqachon mavjud bo'lsa)
                    using (SqlCommand upd = new SqlCommand(
                        "UPDATE [user] SET password=@pw WHERE LOWER(login)='admin'", db.GetCon()))
                    {
                        upd.Parameters.AddWithValue("@pw", hashedPin);
                        upd.ExecuteNonQuery();
                    }

                    db.CloseCon();

                    Session.UserId = newUID; Session.Login = "admin";
                    Session.UserName = "Admin"; Session.UserCategory = "admin";

                    this.Hide();
                    new MainPage(newUID).Show();
                }
                catch (Exception ex) { db.CloseCon(); MessageBox.Show("Xatolik: " + ex.Message); }
            }
        }

        private int ExecScalarInt(dbconnect db, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, db.GetCon()))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // Mavjud bo'lsa ID sini, yo'q bo'lsa yangi qo'shib ID sini qaytaradi
        private int ExecScalarOrExisting(dbconnect db, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, db.GetCon()))
            {
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private void ExecNonQuery(dbconnect db, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, db.GetCon()))
                cmd.ExecuteNonQuery();
        }

        // Xatoni e'tiborsiz qoldirib bajaradi
        private void ExecNonQuerySafe(dbconnect db, string sql)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand(sql, db.GetCon()))
                    cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(FORM_W, FORM_H);
            this.Name = "Password";
            this.ResumeLayout(false);
        }
    }
}
