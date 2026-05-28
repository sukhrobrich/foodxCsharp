using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1.forms.settings
{
    public class ConnectionSetupForm : Form
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage   = Color.FromArgb(249, 249, 251);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger    = Color.FromArgb(220, 38, 38);

        public static string CfgFile =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.cfg");

        private TextBox txtServer;
        private Label   lblStatus;
        private Button  btnConnect;

        public ConnectionSetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size            = new Size(480, 360);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = Color.White;
            BuildUI();
        }

        private void BuildUI()
        {
            // Gold top accent
            this.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Gold });

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            this.Controls.Add(main);

            int cx = 80;
            int cw = 320;

            // Logo
            main.Controls.Add(new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 30, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true, Location = new Point(152, 24)
            });
            main.Controls.Add(new Label
            {
                Text = "Server ulanish sozlamalari",
                Font = new Font("Segoe UI", 10), ForeColor = TextMuted,
                AutoSize = true, Location = new Point(128, 74)
            });

            // Divider
            main.Controls.Add(new Panel { Height = 1, BackColor = Border, Location = new Point(0, 108), Width = 480 });

            // Server IP label + input
            main.Controls.Add(new Label
            {
                Text = "Server IP manzili",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted, AutoSize = true,
                Location = new Point(cx, 132)
            });

            txtServer = new TextBox
            {
                Location = new Point(cx, 156), Width = cw, Height = 34,
                Font = new Font("Segoe UI", 13),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark,
                TextAlign = HorizontalAlignment.Center
            };
            SetHint(txtServer, "masalan:  192.168.35.10");
            main.Controls.Add(txtServer);

            // Status label
            lblStatus = new Label
            {
                Location = new Point(cx, 204), Width = cw, Height = 20,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter, AutoSize = false
            };
            main.Controls.Add(lblStatus);

            // Connect button
            btnConnect = new Button
            {
                Text = "Ulash", Location = new Point(cx, 230),
                Width = cw, Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White,
                Font = new Font("Segoe UI", 13, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnConnect.FlatAppearance.BorderSize = 0;
            btnConnect.Click += BtnConnect_Click;
            main.Controls.Add(btnConnect);

            // Skip button
            Button btnSkip = new Button
            {
                Text = "← Shu kompyuter (standart)",
                Location = new Point(cx, 292), Width = cw, Height = 24,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand
            };
            btnSkip.FlatAppearance.BorderSize = 0;
            btnSkip.MouseEnter += (s, e) => btnSkip.ForeColor = Gold;
            btnSkip.MouseLeave += (s, e) => btnSkip.ForeColor = TextMuted;
            btnSkip.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            main.Controls.Add(btnSkip);

            // Load saved IP
            if (File.Exists(CfgFile))
            {
                try
                {
                    var b = new SqlConnectionStringBuilder(File.ReadAllText(CfgFile).Trim());
                    if (!string.IsNullOrEmpty(b.DataSource))
                        txtServer.Text = b.DataSource;
                }
                catch { }
            }

            // Enter key triggers connect
            txtServer.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) BtnConnect_Click(null, null);
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

        private static void SetHint(TextBox tb, string hint)
        {
            tb.HandleCreated += (s, e) => SendMessage(tb.Handle, 0x1501, (IntPtr)1, hint);
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            string server = txtServer.Text.Trim();
            if (string.IsNullOrEmpty(server))
            {
                SetStatus("IP manzilini kiriting", Danger);
                return;
            }

            btnConnect.Enabled = false;
            btnConnect.Text    = "Tekshirilmoqda...";
            SetStatus("", TextMuted);

            // SQL Server auth (sa) — Integrated Security Linux da ishlamaydi
            string appCs = System.Configuration.ConfigurationManager
                .ConnectionStrings["FoodX"]?.ConnectionString ?? "";
            string user = "sa", pass = "Ac0323301";
            try
            {
                var b2 = new SqlConnectionStringBuilder(appCs);
                if (!b2.IntegratedSecurity && !string.IsNullOrEmpty(b2.UserID))
                { user = b2.UserID; pass = b2.Password; }
            }
            catch { }

            string cs = $"Data Source={server},1433;Initial Catalog=FoodX;" +
                        $"User ID={user};Password={pass};" +
                        $"TrustServerCertificate=True;Encrypt=False;Connect Timeout=6";

            bool ok = await Task.Run(() =>
            {
                try { using (var c = new SqlConnection(cs)) { c.Open(); return true; } }
                catch { return false; }
            });

            btnConnect.Enabled = true;
            btnConnect.Text    = "Ulash";

            if (ok)
            {
                File.WriteAllText(CfgFile, cs);
                SetStatus("✓  Ulandi! Dastur boshlanmoqda...", Success);
                await Task.Delay(700);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                SetStatus("✗  Ulanib bo'lmadi. IP manzilni tekshiring.", Danger);
            }
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text      = text;
            lblStatus.ForeColor = color;
        }
    }
}
