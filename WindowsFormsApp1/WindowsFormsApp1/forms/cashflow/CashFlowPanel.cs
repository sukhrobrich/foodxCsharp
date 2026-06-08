using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.cashflow
{
    public class CashFlowPanel : UserControl
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg    = Color.FromArgb(255, 248, 230);
        private static readonly Color BgMain    = Color.FromArgb(248, 249, 250);
        private static readonly Color CardBg    = Color.White;
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger    = Color.FromArgb(220, 38, 38);
        private static readonly Color Blue      = Color.FromArgb(59, 130, 246);

        private Label  lblKirim, lblChiqim, lblSavdo, lblBalans;
        private Panel  rowsPanel;
        private DateTimePicker dtpFrom, dtpTo;

        // Summary card panels (needed for responsive layout)
        private Panel cardKirim, cardChiqim, cardSavdo, cardBalans;

        public CashFlowPanel()
        {
            InitializeComponent();
            EnsureTable();
            BuildUI();
            this.Load += (s, e) => BeginInvoke((Action)LoadData);
        }

        private void InitializeComponent() { }

        private void EnsureTable()
        {
            try
            {
                var db = new dbconnect();
                db.OpenCon();
                using (var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='cash_transaction')
                    CREATE TABLE cash_transaction (
                        id           INT IDENTITY PRIMARY KEY,
                        type         VARCHAR(10)    NOT NULL,
                        category     NVARCHAR(100)  NOT NULL,
                        amount       DECIMAL(18,2)  NOT NULL,
                        description  NVARCHAR(500)  NULL,
                        created_by   INT            NULL,
                        created_at   DATETIME       NOT NULL DEFAULT GETDATE()
                    )", db.GetCon()))
                    cmd.ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        private void BuildUI()
        {
            this.BackColor = BgMain;

            // ── HEADER ───────────────────────────────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = CardBg };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            header.Controls.Add(new Label
            {
                Text = "Xarajatlar va Kassa",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(24, 16)
            });

            Button btnChiqim = MakeBtn("+ Chiqim", Danger, 120);
            Button btnKirim  = MakeBtn("+ Kirim",  Success, 110);
            btnChiqim.Click += (s, e) =>
            {
                using (var f = new CashFlowForm("CHIQIM"))
                    if (f.ShowDialog(this) == DialogResult.OK) LoadData();
            };
            btnKirim.Click += (s, e) =>
            {
                using (var f = new CashFlowForm("KIRIM"))
                    if (f.ShowDialog(this) == DialogResult.OK) LoadData();
            };
            header.Controls.Add(btnChiqim);
            header.Controls.Add(btnKirim);
            header.Resize += (s, e) =>
            {
                btnChiqim.Location = new Point(header.Width - 24 - btnChiqim.Width, 16);
                btnKirim.Location  = new Point(btnChiqim.Left - btnKirim.Width - 8, 16);
            };

            // ── SUMMARY CARDS (4 kartochka) ───────────────────────────────────
            Panel summaryBar = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = BgMain };

            cardKirim  = MakeSummaryCard("Kassa kirim",   Success, out lblKirim);
            cardChiqim = MakeSummaryCard("Kassa chiqim",  Danger,  out lblChiqim);
            cardSavdo  = MakeSummaryCard("Savdo foydasi", Blue,    out lblSavdo);
            cardBalans = MakeSummaryCard("Jami balans",   Gold,    out lblBalans);

            summaryBar.Controls.AddRange(new Control[] { cardKirim, cardChiqim, cardSavdo, cardBalans });

            // Responsive: reposition cards on resize
            summaryBar.Resize += (s, e) => LayoutSummaryCards(summaryBar);
            summaryBar.Paint  += (s, e) => LayoutSummaryCards(summaryBar);

            // ── FILTER BAR ────────────────────────────────────────────────────
            Panel filterBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = CardBg };
            filterBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, filterBar.Height - 1, filterBar.Width, filterBar.Height - 1);

            filterBar.Controls.Add(new Label
            {
                Text = "Dan:", Font = new Font("Segoe UI", 9), ForeColor = TextMuted,
                AutoSize = true, Location = new Point(24, 16)
            });
            dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9),
                Location = new Point(56, 12), Width = 110, Height = 30,
                Value = DateTime.Today
            };
            filterBar.Controls.Add(dtpFrom);

            filterBar.Controls.Add(new Label
            {
                Text = "Gacha:", Font = new Font("Segoe UI", 9), ForeColor = TextMuted,
                AutoSize = true, Location = new Point(176, 16)
            });
            dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9),
                Location = new Point(218, 12), Width = 110, Height = 30,
                Value = DateTime.Today
            };
            filterBar.Controls.Add(dtpTo);

            Button btnFilter = MakeBtn("Ko'rish", Gold, 90);
            btnFilter.Location = new Point(340, 11);
            btnFilter.Height = 30;
            btnFilter.Click += (s, e) => LoadData();
            filterBar.Controls.Add(btnFilter);

            // ── COLUMN HEADER ─────────────────────────────────────────────────
            Panel colHdr = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = GoldBg };
            colHdr.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, colHdr.Height - 1, colHdr.Width, colHdr.Height - 1);
                BuildColHdr(e.Graphics, colHdr.Width);
            };
            colHdr.Resize += (s, e) => colHdr.Invalidate();

            // ── ROWS ──────────────────────────────────────────────────────────
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgMain };
            rowsPanel = new Panel { Dock = DockStyle.Top, BackColor = BgMain };
            scroll.Controls.Add(rowsPanel);

            this.Controls.Add(scroll);
            this.Controls.Add(colHdr);
            this.Controls.Add(filterBar);
            this.Controls.Add(summaryBar);
            this.Controls.Add(header);
        }

        private void LayoutSummaryCards(Panel bar)
        {
            if (bar.Width < 10) return;
            int total = bar.Width - 32; // 16px padding each side
            int gap   = 12;
            int cw    = (total - gap * 3) / 4;
            int h     = bar.Height - 16;

            cardKirim.SetBounds( 16,                   8, cw, h);
            cardChiqim.SetBounds(16 + (cw + gap),      8, cw, h);
            cardSavdo.SetBounds( 16 + (cw + gap) * 2,  8, cw, h);
            cardBalans.SetBounds(16 + (cw + gap) * 3,  8, cw, h);
        }

        private Panel MakeSummaryCard(string title, Color color, out Label valueLbl)
        {
            Panel card = new Panel { BackColor = CardBg };
            card.Paint += (s, e) =>
            {
                e.Graphics.DrawRectangle(new Pen(Border), 0, 0, card.Width - 1, card.Height - 1);
                e.Graphics.FillRectangle(new SolidBrush(color), 0, 0, 4, card.Height);
            };
            card.Controls.Add(new Label
            {
                Text = title, Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMuted, AutoSize = true, Location = new Point(12, 7)
            });
            var lbl = new Label
            {
                Text = "0 so'm", Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = color, AutoSize = true, Location = new Point(12, 26)
            };
            card.Controls.Add(lbl);
            valueLbl = lbl;
            return card;
        }

        private static readonly float[] Pcts = { .16f, .18f, .18f, .22f, .14f, .12f };
        private static readonly string[] Cols = { "Sana", "Tur", "Kategoriya", "Izoh", "Summa", "Amallar" };

        private void BuildColHdr(System.Drawing.Graphics g, int w)
        {
            float x = 24;
            for (int i = 0; i < Cols.Length; i++)
            {
                float cw = w * Pcts[i];
                var r = new RectangleF(x, 0, cw, 34);
                using (var sf = new System.Drawing.StringFormat { LineAlignment = System.Drawing.StringAlignment.Center })
                    g.DrawString(Cols[i], new Font("Segoe UI", 9, FontStyle.Bold),
                        new SolidBrush(Color.FromArgb(120, 90, 20)), r, sf);
                x += cw;
            }
        }

        private void LoadData()
        {
            DateTime from = dtpFrom.Value.Date;
            DateTime to   = dtpTo.Value.Date.AddDays(1).AddSeconds(-1);

            try
            {
                var db = new dbconnect();

                // 1. Kassa + xaridlar birlashtirilgan
                var dt = new DataTable();
                using (var da = new SqlDataAdapter(@"
                    SELECT id, type, category, amount, description, created_at, 'cash' AS src
                    FROM cash_transaction
                    WHERE created_at BETWEEN @f AND @t
                    UNION ALL
                    SELECT ip.id, 'CHIQIM', N'Xaridlar', ip.total_price,
                           N'Masalliq: ' + i.name, ip.purchased_at, 'purchase'
                    FROM ingredient_purchase ip
                    JOIN ingredient i ON i.id = ip.ingredient_id
                    WHERE ip.purchased_at BETWEEN @f AND @t
                    UNION ALL
                    SELECT fp.id, 'CHIQIM', N'Xaridlar', fp.total_price,
                           N'Mahsulot: ' + f.name, fp.purchased_at, 'purchase'
                    FROM food_purchase fp
                    JOIN food f ON f.id = fp.food_id
                    WHERE fp.purchased_at BETWEEN @f AND @t
                    ORDER BY created_at DESC", db.GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@f", from);
                    da.SelectCommand.Parameters.AddWithValue("@t", to);
                    da.Fill(dt);
                }

                decimal totalKirim = 0, totalChiqim = 0;
                foreach (DataRow r in dt.Rows)
                {
                    decimal amt = Convert.ToDecimal(r["amount"]);
                    if (r["type"].ToString() == "KIRIM") totalKirim  += amt;
                    else                                 totalChiqim += amt;
                }

                // 2. Savdo foydasi (hisobotlar bilan bir xil formula)
                decimal savdoFoyda = CalcSavdoFoyda(from, to.Date);

                // 3. Jami balans = kassa kirim - kassa chiqim + savdo foydasi
                decimal balans = totalKirim - totalChiqim + savdoFoyda;

                lblKirim.Text  = "+" + totalKirim.ToString("N0") + " so'm";
                lblChiqim.Text = "-" + totalChiqim.ToString("N0") + " so'm";
                lblSavdo.Text  = (savdoFoyda >= 0 ? "+" : "") + savdoFoyda.ToString("N0") + " so'm";
                lblSavdo.ForeColor = savdoFoyda >= 0 ? Blue : Danger;
                lblBalans.Text = (balans >= 0 ? "+" : "") + balans.ToString("N0") + " so'm";
                lblBalans.ForeColor = balans >= 0 ? Success : Danger;

                // 4. Qatorlarni chizish
                rowsPanel.SuspendLayout();
                rowsPanel.Controls.Clear();
                int y = 0;
                foreach (DataRow r in dt.Rows)
                    rowsPanel.Controls.Add(MakeRow(r, ref y));

                if (dt.Rows.Count == 0)
                {
                    rowsPanel.Controls.Add(new Label
                    {
                        Text = "Bu sana oralig'ida kassa operatsiyalari yo'q.",
                        Font = new Font("Segoe UI", 11), ForeColor = TextMuted,
                        AutoSize = true, Location = new Point(24, 20)
                    });
                    y = 60;
                }
                rowsPanel.Height = y;
                rowsPanel.ResumeLayout();
            }
            catch (Exception ex) { MessageBox.Show("Yuklashda xatolik: " + ex.Message); }
        }

        // ReportPage.LoadStats() bilan bir xil formula
        private decimal CalcSavdoFoyda(DateTime from, DateTime to)
        {
            decimal fp = 0, fee = 0, plP = 0;

            try
            {
                var db = new dbconnect();
                db.OpenCon();

                // Taomlardan foyda: (selling_price - original_price) * qty
                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM((f.selling_price - ISNULL(f.original_price,0)) * ofd.quantity), 0)
                        FROM [order] o
                        JOIN order_food ofd ON ofd.order_id = o.id
                        JOIN food f ON f.id = ofd.food_id
                        WHERE o.paid='YES'
                          AND CAST(o.created_at AS DATE) BETWEEN @f AND @t", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@f", from);
                        cmd.Parameters.AddWithValue("@t", to);
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) fp = Convert.ToDecimal(v);
                    }
                }
                catch { }

                // Xizmat haqqi — custom_svc_fee (ReportPage bilan bir xil)
                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM(ISNULL(custom_svc_fee,0)), 0) FROM [order]
                        WHERE paid='YES' AND ISNULL(custom_svc_fee,0) > 0
                          AND CAST(created_at AS DATE) BETWEEN @f AND @t", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@f", from);
                        cmd.Parameters.AddWithValue("@t", to);
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) fee = Convert.ToDecimal(v);
                    }
                }
                catch
                {
                    decimal oSum = 0;
                    try
                    {
                        using (var cmd = new SqlCommand(@"
                            SELECT ISNULL(SUM(total), 0) FROM [order]
                            WHERE paid='YES' AND CAST(created_at AS DATE) BETWEEN @f AND @t", db.GetCon()))
                        {
                            cmd.Parameters.AddWithValue("@f", from);
                            cmd.Parameters.AddWithValue("@t", to);
                            object v = cmd.ExecuteScalar();
                            if (v != null && v != DBNull.Value) oSum = Convert.ToDecimal(v);
                        }
                    }
                    catch { }
                    decimal svcPct = 0;
                    decimal.TryParse(PrintService.GetSetting("service_charge", "0"), out svcPct);
                    if (svcPct > 0) fee = Math.Round(oSum * svcPct / (100 + svcPct));
                }

                // Joy narxi — price_type hisobiga (ReportPage bilan bir xil)
                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM(
                          CASE WHEN ISNULL(po.price_type,'UZS')='UZS' THEN ISNULL(po.price,0)
                               ELSE ISNULL(fo.food_total,0)*ISNULL(po.price,0)/100.0
                          END),0)
                        FROM [order] o
                        JOIN place_in pi ON pi.id = o.place_id
                        JOIN place_out po ON po.id = pi.place_out_id
                        LEFT JOIN (
                          SELECT ofd.order_id, SUM(ofd.quantity*f.selling_price) AS food_total
                          FROM order_food ofd JOIN food f ON f.id=ofd.food_id GROUP BY ofd.order_id
                        ) fo ON fo.order_id = o.id
                        WHERE o.paid='YES'
                          AND CAST(o.created_at AS DATE) BETWEEN @f AND @t", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@f", from);
                        cmd.Parameters.AddWithValue("@t", to);
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) plP = Convert.ToDecimal(v);
                    }
                }
                catch { }

                db.CloseCon();
            }
            catch { }

            return fp + fee + plP;
        }

        private Panel MakeRow(DataRow r, ref int y)
        {
            int     id         = Convert.ToInt32(r["id"]);
            string  type       = r["type"].ToString();
            string  cat        = r["category"].ToString();
            decimal amt        = Convert.ToDecimal(r["amount"]);
            string  desc       = r["description"] == DBNull.Value ? "" : r["description"].ToString();
            DateTime dt        = Convert.ToDateTime(r["created_at"]);
            bool    isPurchase = r.Table.Columns.Contains("src") && r["src"].ToString() == "purchase";

            bool  isKirim   = type == "KIRIM";
            Color typeColor = isKirim ? Success : Danger;

            Panel row = new Panel
            {
                Location = new Point(0, y), Height = 48, BackColor = CardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            row.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, row.Height - 1, row.Width, row.Height - 1);
                e.Graphics.FillRectangle(new SolidBrush(typeColor), 0, 8, 3, 32);
            };

            Action layout = () =>
            {
                int w = row.Width > 0 ? row.Width : 800;
                float[] colW = new float[Pcts.Length];
                for (int i = 0; i < Pcts.Length; i++) colW[i] = w * Pcts[i];

                float cx = 24;
                for (int i = 0; i < Pcts.Length; i++)
                {
                    foreach (Control c in row.Controls)
                    {
                        if (c.Tag is int ci && ci == i)
                        {
                            c.Location = new Point((int)cx, 0);
                            c.Width    = (int)colW[i] - 4;
                        }
                    }
                    cx += colW[i];
                }
            };

            Label MkLbl(string text, int col, Color color, FontStyle fs = FontStyle.Regular)
            {
                var l = new Label
                {
                    Text = text, Tag = col,
                    Font = new Font("Segoe UI", 9, fs),
                    ForeColor = color, AutoSize = false,
                    Height = 48, TextAlign = ContentAlignment.MiddleLeft
                };
                row.Controls.Add(l);
                return l;
            }

            MkLbl(dt.ToString("dd.MM.yy  HH:mm"), 0, TextMuted);
            MkLbl(isKirim ? "↑ KIRIM" : "↓ CHIQIM", 1, typeColor, FontStyle.Bold);
            MkLbl(cat, 2, TextDark);
            MkLbl(desc, 3, TextMuted);
            MkLbl((isKirim ? "+" : "-") + amt.ToString("N0") + " so'm", 4, typeColor, FontStyle.Bold);

            if (!isPurchase)
            {
                Button btnDel = new Button
                {
                    Text = "O'ch", Tag = 5,
                    Width = 50, Height = 28,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(254, 226, 226), ForeColor = Danger,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand
                };
                btnDel.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
                btnDel.FlatAppearance.BorderSize  = 1;
                btnDel.Click += (s, e) =>
                {
                    if (MessageBox.Show("Bu operatsiyani o'chirishni tasdiqlaysizmi?", "O'chirish",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    try
                    {
                        var db = new dbconnect();
                        db.OpenCon();
                        using (var cmd = new SqlCommand("DELETE FROM cash_transaction WHERE id=@id", db.GetCon()))
                        { cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
                        db.CloseCon();
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("O'chirishda xatolik: " + ex.Message); }
                };
                row.Controls.Add(btnDel);
            }

            row.Resize += (s, e) =>
            {
                layout();
                if (!isPurchase)
                {
                    int w2 = row.Width > 0 ? row.Width : 800;
                    float cx2 = 24;
                    for (int i = 0; i < Pcts.Length - 1; i++) cx2 += w2 * Pcts[i];
                    foreach (Control c in row.Controls)
                        if (c is Button b && (int)(b.Tag ?? -1) == 5)
                            b.Location = new Point((int)cx2 + 4, 10);
                }
            };
            layout();

            rowsPanel.Resize += (s, e) => { if (rowsPanel.Width > 0) row.Width = rowsPanel.Width; };
            row.Width = this.Width > 0 ? this.Width : 900;

            y += 48;
            return row;
        }

        private static Button MakeBtn(string text, Color bg, int w)
        {
            var b = new Button
            {
                Text = text, Width = w, Height = 34,
                FlatStyle = FlatStyle.Flat, BackColor = bg,
                ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
