using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.report
{
    public partial class ReportWaiter1 : UserControl
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg    = Color.FromArgb(255, 248, 230);
        private static readonly Color BgMain    = Color.FromArgb(248, 249, 250);
        private static readonly Color CardBg    = Color.White;
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);

        private ComboBox comboWaiter;
        private DateTimePicker dtFrom;
        private DateTimePicker dtTo;
        private DataGridView dgvReport;
        private Label lblTotal;

        public ReportWaiter1()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.BackColor = BgMain;

            // ── HEADER ───────────────────────────────────────────────────────
            Panel header = new Panel
            {
                Dock = DockStyle.Top, Height = 58, BackColor = CardBg
            };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);

            Label lblTitle = new Label
            {
                Text = "Ofitsiantlar hisoboti",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(24, 14)
            };
            header.Controls.Add(lblTitle);
            this.Controls.Add(header);

            // ── FILTER BAR ───────────────────────────────────────────────────
            Panel filterBar = new Panel
            {
                Dock = DockStyle.Top, Height = 70, BackColor = CardBg
            };
            filterBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, filterBar.Height - 1, filterBar.Width, filterBar.Height - 1);

            // Waiter combo
            AddFilterLabel(filterBar, "Ofitsiant:", 24);
            comboWaiter = new ComboBox
            {
                Location = new Point(106, 22), Width = 190, Height = 34,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = CardBg
            };
            filterBar.Controls.Add(comboWaiter);

            // From date
            AddFilterLabel(filterBar, "Dan:", 318);
            dtFrom = MakeDatePicker(new Point(360, 20));
            dtFrom.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            filterBar.Controls.Add(dtFrom);

            // To date
            AddFilterLabel(filterBar, "Gacha:", 518);
            dtTo = MakeDatePicker(new Point(570, 20));
            dtTo.Value = DateTime.Now;
            filterBar.Controls.Add(dtTo);

            // Filter button
            Button btnFilter = new Button
            {
                Text = "Qidirish",
                Location = new Point(730, 18),
                Width = 110, Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Gold, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnFilter.FlatAppearance.BorderSize = 0;
            btnFilter.Click += (s, e) => LoadReport();
            filterBar.Controls.Add(btnFilter);

            // Total label (right side)
            lblTotal = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Success, AutoSize = true,
                Location = new Point(858, 26)
            };
            filterBar.Controls.Add(lblTotal);
            filterBar.Resize += (s, e) => lblTotal.Location = new Point(filterBar.Width - lblTotal.Width - 20, 26);

            this.Controls.Add(filterBar);

            // ── DATA GRID ────────────────────────────────────────────────────
            Panel gridWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Padding = new Padding(16, 16, 16, 16) };

            dgvReport = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.None,
                BackgroundColor = CardBg,
                GridColor = Border,
                Font = new Font("Segoe UI", 9),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 36 }
            };

            dgvReport.DefaultCellStyle.BackColor = CardBg;
            dgvReport.DefaultCellStyle.ForeColor = TextDark;
            dgvReport.DefaultCellStyle.SelectionBackColor = GoldBg;
            dgvReport.DefaultCellStyle.SelectionForeColor = TextDark;
            dgvReport.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

            dgvReport.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);

            dgvReport.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(243, 244, 246);
            dgvReport.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted;
            dgvReport.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            dgvReport.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dgvReport.ColumnHeadersHeight = 38;
            dgvReport.EnableHeadersVisualStyles = false;

            gridWrap.Controls.Add(dgvReport);
            this.Controls.Add(gridWrap);
        }

        private void AddFilterLabel(Panel parent, string text, int x)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(x, 28)
            });
        }

        private DateTimePicker MakeDatePicker(Point location)
        {
            return new DateTimePicker
            {
                Location = location,
                Width = 138, Height = 30,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy",
                Font = new Font("Segoe UI", 9),
                CalendarFont = new Font("Segoe UI", 9),
                CalendarForeColor = TextDark,
                CalendarTitleBackColor = Gold,
                CalendarTitleForeColor = Color.White,
                CalendarTrailingForeColor = TextMuted
            };
        }

        private void LoadReport()
        {
            try
            {
                int waiterId = comboWaiter.SelectedValue != null ? Convert.ToInt32(comboWaiter.SelectedValue) : 0;
                DateTime fromDate = dtFrom.Value.Date;
                DateTime toDate = dtTo.Value.Date.AddDays(1).AddSeconds(-1);

                string sql = @"
                    SELECT
                        o.id          AS [Zakaz №],
                        u.name        AS [Ofitsiant],
                        p.name        AS [To'lov turi],
                        pi.room_name  AS [Stol],
                        FORMAT(o.total,'N0') + ' so''m' AS [Summa],
                        FORMAT(o.created_at,'dd.MM.yyyy HH:mm') AS [Sana]
                    FROM [order] o
                    JOIN [user] u        ON u.id  = o.user_id
                    JOIN payment p       ON p.id  = o.payment_id
                    JOIN place_in pi     ON pi.id = o.place_id
                    WHERE o.paid = 'YES'
                      AND o.created_at BETWEEN @from AND @to";

                if (waiterId != 0) sql += " AND o.user_id = @uid";
                sql += " ORDER BY o.created_at DESC";

                dbconnect db = new dbconnect();
                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                cmd.Parameters.AddWithValue("@from", fromDate);
                cmd.Parameters.AddWithValue("@to", toDate);
                if (waiterId != 0) cmd.Parameters.AddWithValue("@uid", waiterId);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                dgvReport.DataSource = dt;

                // Sum
                decimal total = 0;
                foreach (DataRow row in dt.Rows)
                {
                    string raw = row["Summa"].ToString().Replace(" so'm", "").Replace(" ", "").Replace(".", "").Replace(",", "");
                    if (decimal.TryParse(raw, out decimal v)) total += v;
                }
                lblTotal.Text = $"Jami: {total:N0} so'm";

                // Column styles
                foreach (DataGridViewColumn col in dgvReport.Columns)
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    col.SortMode = DataGridViewColumnSortMode.NotSortable;
                }
                if (dgvReport.Columns.Contains("Zakaz №"))
                {
                    dgvReport.Columns["Zakaz №"].FillWeight = 60;
                    dgvReport.Columns["Zakaz №"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                if (dgvReport.Columns.Contains("Summa"))
                {
                    dgvReport.Columns["Summa"].DefaultCellStyle.ForeColor = Success;
                    dgvReport.Columns["Summa"].DefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message);
            }
        }

        private void LoadWaiters()
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                string sql = @"SELECT u.id, u.name FROM [user] u
                               JOIN user_category uc ON uc.id = u.user_category_id
                               WHERE uc.name = 'ofitsiant'
                               ORDER BY u.name";
                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                SqlDataReader dr = cmd.ExecuteReader();
                DataTable dt = new DataTable();
                dt.Load(dr);
                db.CloseCon();

                DataRow all = dt.NewRow();
                all["id"] = 0; all["name"] = "Barchasi";
                dt.Rows.InsertAt(all, 0);

                comboWaiter.DataSource = dt;
                comboWaiter.DisplayMember = "name";
                comboWaiter.ValueMember = "id";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ofitsiantlarni yuklashda xatolik: " + ex.Message);
            }
        }

        private void ReportWaiter1_Load(object sender, EventArgs e)
        {
            LoadWaiters();
            LoadReport();
        }
    }
}
