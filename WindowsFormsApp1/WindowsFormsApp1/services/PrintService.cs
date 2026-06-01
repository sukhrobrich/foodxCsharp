using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Printing;
using WindowsFormsApp1.services;

public class PrintService
{
    private static List<string> GetKitchenBlockOrder()
    {
        string[] defaultOrder = { "kt_category", "kt_items", "kt_separator", "kt_admin", "kt_order_num", "kt_place", "kt_time" };
        string saved = GetSetting("kitchen_order", "");
        List<string> ordered = new List<string>();

        if (!string.IsNullOrEmpty(saved))
        {
            string[] parts = saved.Split(',');
            foreach (string p in parts)
            {
                string id = p.Trim();
                if (!string.IsNullOrEmpty(id)) ordered.Add(id);
            }
        }
        else
        {
            ordered.AddRange(defaultOrder);
        }

        // Required blocks always visible; others respect kt_*_visible setting
        List<string> visible = new List<string>();
        foreach (string id in ordered)
        {
            if (id == "kt_category" || id == "kt_items") { visible.Add(id); continue; }
            string v = GetSetting(id + "_visible", "1");
            if (v == "1") visible.Add(id);
        }
        return visible;
    }

    public static void PrintKitchenTickets(int orderId, List<KitchenItem> items, string waiterName, DateTime orderTime)
    {
        if (items == null || items.Count == 0) return;

        string placeText = GetOrderPlaceText(orderId);
        List<string> blockOrder = GetKitchenBlockOrder();

        // Group by category — each category prints one ticket to its printer
        var groups = new Dictionary<string, List<KitchenItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (KitchenItem item in items)
        {
            if (string.IsNullOrEmpty(item.PrinterName)) continue;
            string key = string.IsNullOrEmpty(item.CategoryName) ? item.PrinterName : item.CategoryName;
            if (!groups.ContainsKey(key)) groups[key] = new List<KitchenItem>();
            groups[key].Add(item);
        }

        foreach (KeyValuePair<string, List<KitchenItem>> kv in groups)
        {
            string categoryName = kv.Key;
            List<KitchenItem> groupItems = new List<KitchenItem>(kv.Value);
            string printerName = groupItems[0].PrinterName;
            int oid = orderId;
            string wn = waiterName;
            DateTime ot = orderTime;
            string place = placeText;
            List<string> blocks = blockOrder;
            float kNorm = GetFloatSetting("font_kitchen_norm", 10f);
            float kBold = GetFloatSetting("font_kitchen_bold", 12f);
            float widthMm = GetFloatSetting("font_receipt_width_mm", 72f);

            PrintDocument doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;
            SetThermalPaper(doc, widthMm);
            doc.PrintPage += delegate (object sender, PrintPageEventArgs e)
            {
                Graphics g    = e.Graphics;
                Font fHeader  = new Font("Courier New", kBold, FontStyle.Bold);
                Font fBold    = new Font("Courier New", kNorm, FontStyle.Bold);
                Font fNorm    = new Font("Courier New", kNorm);
                Font fSmall   = new Font("Courier New", Math.Max(6f, kNorm - 2f));
                Brush br      = Brushes.Black;
                StringFormat ctr = new StringFormat { Alignment = StringAlignment.Center };

                float x  = e.MarginBounds.Left;
                float y  = e.MarginBounds.Top;
                float w  = e.MarginBounds.Width;
                string sep = new string('-', COLS);

                void L(string text, Font f = null)
                {
                    Font uf = f ?? fNorm;
                    g.DrawString(text, uf, br, x, y);
                    y += uf.GetHeight(g) + 2;
                }
                void C(string text, Font f = null)
                {
                    Font uf = f ?? fNorm;
                    float h = uf.GetHeight(g) + 2;
                    g.DrawString(text, uf, br, new RectangleF(x, y, w, h + 4), ctr);
                    y += h + 4;
                }

                foreach (string blockId in blocks)
                {
                    switch (blockId)
                    {
                        case "kt_category":
                            C(categoryName, fHeader);
                            break;
                        case "kt_items":
                            foreach (KitchenItem item in groupItems)
                            {
                                string unitStr  = string.IsNullOrEmpty(item.Unit) ? "ta" : item.Unit;
                                string qtyPart  = Trunc(item.Quantity.ToString(), 3).PadLeft(3) + " " + Trunc(unitStr, 4).PadRight(4) + " ";
                                string namePart = Trunc(item.FoodName, COLS - qtyPart.Length);
                                L(qtyPart + namePart, fBold);
                                if (!string.IsNullOrEmpty(item.Note))
                                {
                                    string note = "  >> " + Trunc(item.Note, COLS - 5);
                                    L(note, fSmall);
                                }
                            }
                            break;
                        case "kt_separator":
                            y += 2; L(sep); y += 2;
                            break;
                        case "kt_admin":
                            L(TwoCol("Ofitsiant:", Trunc(wn, 20)));
                            break;
                        case "kt_order_num":
                            L(TwoCol("Buyurtma:", "#" + oid));
                            break;
                        case "kt_place":
                            if (!string.IsNullOrEmpty(place))
                                L(TwoCol("Joy:", Trunc(place, 22)));
                            break;
                        case "kt_time":
                            L(TwoCol("Vaqt:", ot.ToString("dd.MM.yyyy HH:mm")));
                            break;
                    }
                }

                fHeader.Dispose(); fBold.Dispose(); fNorm.Dispose(); fSmall.Dispose();
                e.HasMorePages = false;
            };

            try { doc.Print(); } catch { }
        }
    }

    private static string GetOrderPlaceText(int orderId)
    {
        try
        {
            dbconnect db = new dbconnect();
            string sql = @"SELECT ISNULL(po.name, '') AS zone, ISNULL(pi.room_name, '') AS room
                           FROM [order] o
                           LEFT JOIN place_in pi ON pi.id = o.place_id
                           LEFT JOIN place_out po ON po.id = pi.place_out_id
                           WHERE o.id = @oid";
            SqlCommand cmd = new SqlCommand(sql, db.GetCon());
            cmd.Parameters.AddWithValue("@oid", orderId);
            db.OpenCon();
            SqlDataReader dr = cmd.ExecuteReader();
            string result = "";
            if (dr.Read())
            {
                string zone = dr["zone"].ToString();
                string room = dr["room"].ToString();
                result = !string.IsNullOrEmpty(zone) && !string.IsNullOrEmpty(room)
                    ? zone + " " + room
                    : zone + room;
            }
            dr.Close();
            db.CloseCon();
            return result;
        }
        catch { return ""; }
    }

    // ─── 80mm thermal: Courier New 8pt → 32 chars per line ─────────────────
    private const int COLS = 32;

    private static string RFmt(decimal v)
    {
        // compact thousands: 140000 → "140 000"
        return ((long)Math.Round(v)).ToString("N0",
            System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
    }

    private static string Trunc(string s, int n) =>
        s.Length > n ? s.Substring(0, n) : s;

    // left-aligned left col, right-aligned right col, total = COLS
    private static string TwoCol(string left, string right, int cols = COLS)
    {
        right = Trunc(right, cols);
        int leftLen = Math.Max(0, cols - right.Length);
        left = Trunc(left, leftLen).PadRight(leftLen);
        return left + right;
    }

    // 4-column item row: Name(12) Qty(4) Price(8) [space] Sum(7) = 32
    private static string ItemRow(string name, string qty, string price, string sum)
    {
        name  = Trunc(name,  12).PadRight(12);
        qty   = Trunc(qty,    4).PadLeft(4);
        price = Trunc(price,  8).PadLeft(8);
        sum   = Trunc(sum,    7).PadLeft(7);
        return name + qty + price + " " + sum;
    }

    public static void PrintReceipt(int orderId)
    {
        try
        {
            string cafeName    = GetSetting("cafe_name",    "FoodX Cafe");
            string cafeAddress = GetSetting("cafe_address", "");
            string cafePhone   = GetSetting("cafe_phone",   "");
            string thankMsg    = GetSetting("thank_msg",    "Buyurtmangiz uchun rahmat");
            string logoPath    = GetSetting("logo_path",    "");
            decimal svcPct     = 0;
            decimal.TryParse(GetSetting("service_charge", "0"), out svcPct);

            string printerName = GetSetting("main_printer", "");
            if (string.IsNullOrEmpty(printerName))
                try { printerName = new PrinterSettings().PrinterName; } catch { return; }
            if (string.IsNullOrEmpty(printerName)) return;

            dbconnect db = new dbconnect();
            string sql = @"SELECT o.id, o.created_at, o.total,
                                  ISNULL(pay.name,'') AS pay_name,
                                  ISNULL(u.name, CAST(o.user_id AS NVARCHAR)) AS waiter,
                                  ISNULL(pi.room_name,'') AS tbl,
                                  ISNULL(po.name,'') AS zone,
                                  ISNULL(po.serviceFee,'YES') AS svc_fee,
                                  ISNULL(o.discount_pct, 0)    AS discount_pct,
                                  ISNULL(o.discount_amount, 0) AS discount_amount,
                                  ISNULL(o.delivery_address,'') AS delivery_address,
                                  ISNULL(o.delivery_phone,'')   AS delivery_phone,
                                  ISNULL(o.is_delivery, 0)      AS is_delivery,
                                  ISNULL(o.order_note,'')        AS order_note,
                                  ISNULL(o.customer_name,'')     AS customer_name
                           FROM [order] o
                           LEFT JOIN [user] u ON u.id = o.user_id
                           LEFT JOIN payment pay ON pay.id = o.payment_id
                           LEFT JOIN place_in pi ON pi.id = o.place_id
                           LEFT JOIN place_out po ON po.id = pi.place_out_id
                           WHERE o.id = @oid";
            SqlCommand cmd = new SqlCommand(sql, db.GetCon());
            cmd.Parameters.AddWithValue("@oid", orderId);
            db.OpenCon();
            SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) { dr.Close(); db.CloseCon(); throw new Exception("Buyurtma #" + orderId + " topilmadi."); }

            int      oId          = Convert.ToInt32(dr["id"]);
            DateTime created      = Convert.ToDateTime(dr["created_at"]);
            decimal  orderTotal   = Convert.ToDecimal(dr["total"]);
            string   payName      = dr["pay_name"].ToString();
            string   waiter       = dr["waiter"].ToString();
            string   tbl          = dr["tbl"].ToString();
            string   zone         = dr["zone"].ToString();
            bool     hasSvcFee    = dr["svc_fee"].ToString().ToUpper() == "YES";
            decimal  discountPct  = Convert.ToDecimal(dr["discount_pct"]);
            decimal  discountAmt  = Convert.ToDecimal(dr["discount_amount"]);
            string   deliveryAddr = dr["delivery_address"].ToString();
            string   deliveryPhone= dr["delivery_phone"].ToString();
            bool     isDelivery   = Convert.ToInt32(dr["is_delivery"]) == 1;
            string   orderNote    = dr["order_note"].ToString();
            string   customerName = dr["customer_name"].ToString();
            dr.Close();

            // Load all payment entries from order_payments
            var paymentRows = new List<(string name, decimal amount)>();
            try
            {
                SqlCommand pCmd = new SqlCommand(
                    "SELECT p.name, op.amount FROM order_payments op JOIN payment p ON p.id=op.payment_id WHERE op.order_id=@oid ORDER BY op.id",
                    db.GetCon());
                pCmd.Parameters.AddWithValue("@oid", orderId);
                SqlDataReader pDr = pCmd.ExecuteReader();
                while (pDr.Read())
                    paymentRows.Add((pDr["name"].ToString(), Convert.ToDecimal(pDr["amount"])));
                pDr.Close();
            }
            catch { }
            if (paymentRows.Count == 0 && !string.IsNullOrEmpty(payName))
                paymentRows.Add((payName, orderTotal));

            // Qarz ma'lumotlari
            string   debtorName  = "";
            string   debtorPhone = "";
            decimal  debtAmount  = 0;
            try
            {
                SqlCommand dCmd = new SqlCommand(
                    "SELECT TOP 1 debtor_name, debtor_phone, amount FROM order_debt WHERE order_id=@oid AND ISNULL(is_paid,0)=0",
                    db.GetCon());
                dCmd.Parameters.AddWithValue("@oid", orderId);
                SqlDataReader dDr = dCmd.ExecuteReader();
                if (dDr.Read())
                {
                    debtorName  = dDr["debtor_name"].ToString();
                    debtorPhone = dDr["debtor_phone"].ToString();
                    debtAmount  = Convert.ToDecimal(dDr["amount"]);
                }
                dDr.Close();
            }
            catch { }

            string sql2 = @"SELECT f.name, ofd.quantity, f.selling_price
                            FROM order_food ofd JOIN food f ON f.id=ofd.food_id
                            WHERE ofd.order_id=@oid ORDER BY f.name";
            SqlCommand cmd2 = new SqlCommand(sql2, db.GetCon());
            cmd2.Parameters.AddWithValue("@oid", orderId);
            SqlDataReader dr2 = cmd2.ExecuteReader();
            List<string[]> items = new List<string[]>();
            while (dr2.Read())
                items.Add(new[] { dr2["name"].ToString(), dr2["quantity"].ToString(), dr2["selling_price"].ToString() });
            dr2.Close();
            db.CloseCon();

            decimal foodSubtotal = 0;
            foreach (string[] it in items)
                foodSubtotal += int.Parse(it[1]) * decimal.Parse(it[2]);
            decimal svcAmt = hasSvcFee && svcPct > 0 ? Math.Round(foodSubtotal * svcPct / 100) : 0;
            decimal total  = orderTotal;

            System.Drawing.Image logoImg = null;
            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
                try { logoImg = System.Drawing.Image.FromFile(logoPath); } catch { }

            PrintDocument doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;
            SetThermalPaper(doc);

            float rNorm    = GetFloatSetting("font_receipt_norm",        9f);
            float rBold    = GetFloatSetting("font_receipt_bold",        9f);
            float rTitle   = GetFloatSetting("font_receipt_title",       16f);
            float rSmall   = GetFloatSetting("font_receipt_small",       7.5f);
            float rTotal   = GetFloatSetting("font_receipt_total",       18f);
            float rOrderN  = GetFloatSetting("font_receipt_ordernum",    12f);
            float rSpacing = GetFloatSetting("font_receipt_linespacing", 4f);
            float rWidthMm = GetFloatSetting("font_receipt_width_mm",    72f);
            SetThermalPaper(doc, rWidthMm);

            doc.PrintPage += delegate (object sender, PrintPageEventArgs e)
            {
                Graphics g     = e.Graphics;
                Font fTitle    = new Font("Courier New", rTitle,  FontStyle.Bold);
                Font fBig      = new Font("Courier New", rTotal,  FontStyle.Bold);
                Font fOrderN   = new Font("Courier New", rOrderN, FontStyle.Bold);
                Font fBold     = new Font("Courier New", rBold,   FontStyle.Bold);
                Font fNorm     = new Font("Courier New", rNorm);
                Font fSmall    = new Font("Courier New", rSmall);
                Brush br       = Brushes.Black;
                StringFormat ctr = new StringFormat { Alignment = StringAlignment.Center };

                // anchor to margin — everything uses single x
                float x  = e.MarginBounds.Left;
                float y  = e.MarginBounds.Top;
                float w  = e.MarginBounds.Width;
                string sep = new string('-', COLS);

                void L(string text, Font f = null)
                {
                    Font uf = f ?? fNorm;
                    g.DrawString(text, uf, br, x, y);
                    y += uf.GetHeight(g) + rSpacing;
                }
                void C(string text, Font f = null)
                {
                    Font uf = f ?? fNorm;
                    float h = uf.GetHeight(g) + rSpacing;
                    g.DrawString(text, uf, br, new RectangleF(x, y, w, h + 2), ctr);
                    y += h + 2;
                }

                // Logo
                if (logoImg != null)
                {
                    float logoW = w * 0.45f;
                    float logoH = logoW * logoImg.Height / logoImg.Width;
                    if (logoH > 70) { logoH = 70; logoW = logoH * logoImg.Width / logoImg.Height; }
                    g.DrawImage(logoImg, x + (w - logoW) / 2f, y, logoW, logoH);
                    y += logoH + 4;
                }

                // Cafe header (centred)
                C(cafeName, fTitle);
                if (!string.IsNullOrEmpty(cafeAddress)) C(cafeAddress);
                if (!string.IsNullOrEmpty(cafePhone))   C(cafePhone);

                L(sep);
                L("Buyurtma: " + oId, fOrderN);
                L(sep);

                // Item table header (32 chars total)
                L(ItemRow("Mahsulotlar", "Miq", "Nar.", "Jami"), fBold);
                L(sep);

                // Items
                foreach (string[] item in items)
                {
                    int     qty   = int.Parse(item[1]);
                    decimal price = decimal.Parse(item[2]);
                    decimal sum   = qty * price;
                    L(ItemRow(item[0], qty.ToString(), RFmt(price), RFmt(sum)), fBold);
                }

                L(sep);
                L(TwoCol("Jami:", RFmt(foodSubtotal) + " UZS"), fBold);

                // Xizmat haqi (qo'shiladi avval, keyin chegirma)
                if (svcAmt > 0)
                    L(TwoCol($"Xizmat haqqi {svcPct:0.#}%", "+" + RFmt(svcAmt) + " UZS"));

                // Chegirma (xizmat haqi qo'shilgandan keyin)
                if (discountAmt > 0)
                {
                    string dLabel = discountPct > 0
                        ? $"Chegirma -{discountPct:0.#}%"
                        : "Chegirma";
                    L(TwoCol(dLabel, "-" + RFmt(discountAmt) + " UZS"));
                }

                L(sep);

                // Ofitsiant / joy / vaqt
                L(TwoCol("Ofitsiant:", Trunc(waiter, 14)));
                L(TwoCol("Joy:",       Trunc(zone + " " + tbl, 18)));
                L(TwoCol("Sana:",      created.ToString("dd.MM HH:mm")));
                if (!string.IsNullOrEmpty(customerName))
                    L(TwoCol("Mijoz:", Trunc(customerName, 18)));

                // Yetkazib berish
                if (isDelivery)
                {
                    L(sep);
                    L("Yetkazib berish:", fBold);
                    if (!string.IsNullOrEmpty(deliveryPhone))
                        L("  Tel: " + Trunc(deliveryPhone, COLS - 6));
                    if (!string.IsNullOrEmpty(deliveryAddr))
                    {
                        // Manzil bir necha qatorga sig'ishi mumkin
                        string addr = deliveryAddr;
                        while (addr.Length > 0)
                        {
                            L("  " + Trunc(addr, COLS - 2));
                            addr = addr.Length > COLS - 2 ? addr.Substring(COLS - 2) : "";
                        }
                    }
                }

                // Buyurtma eslatmasi
                if (!string.IsNullOrEmpty(orderNote))
                {
                    L(sep);
                    L("Eslatma:", fBold);
                    string note = orderNote;
                    while (note.Length > 0)
                    {
                        L("  " + Trunc(note, COLS - 2));
                        note = note.Length > COLS - 2 ? note.Substring(COLS - 2) : "";
                    }
                }

                L(sep);

                // Jami (grand total) — big font orqali TwoCol kengligi sahifadan oshib ketadi,
                // shuning uchun label va summani alohida markazlashtirib chop etamiz
                C("JAMI:", fBig);
                C(RFmt(total) + " UZS", fBig);
                L(sep);

                // To'lov turi
                foreach (var pr in paymentRows)
                    L(TwoCol(Trunc(pr.name, 14), RFmt(pr.amount) + " UZS"));

                // Qarz
                if (!string.IsNullOrEmpty(debtorName))
                {
                    L(sep);
                    L("*** QARZ ***", fBold);
                    L(TwoCol("Qarzdor:", Trunc(debtorName, 18)));
                    if (!string.IsNullOrEmpty(debtorPhone))
                        L(TwoCol("Tel:", Trunc(debtorPhone, 20)));
                    if (debtAmount > 0)
                        L(TwoCol("Qarz summasi:", RFmt(debtAmount) + " UZS"), fBold);
                }

                L(sep);

                y += 4;
                C(thankMsg, fSmall);
                C("v2.8.202", fSmall);

                fTitle.Dispose(); fBig.Dispose(); fOrderN.Dispose();
                fBold.Dispose(); fNorm.Dispose(); fSmall.Dispose();
                logoImg?.Dispose();
                e.HasMorePages = false;
            };

            try { doc.Print(); }
            catch (Exception ex) { throw new Exception("Printer xatosi: " + ex.Message); }
        }
        catch { throw; }
    }

    public static void PrintKitchenCancellation(int orderId, List<KitchenItem> items, string waiterName, DateTime orderTime)
    {
        if (items == null || items.Count == 0) return;

        string placeText = GetOrderPlaceText(orderId);

        var groups = new Dictionary<string, List<KitchenItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (KitchenItem item in items)
        {
            if (string.IsNullOrEmpty(item.PrinterName)) continue;
            string key = string.IsNullOrEmpty(item.CategoryName) ? item.PrinterName : item.CategoryName;
            if (!groups.ContainsKey(key)) groups[key] = new List<KitchenItem>();
            groups[key].Add(item);
        }

        foreach (KeyValuePair<string, List<KitchenItem>> kv in groups)
        {
            string categoryName = kv.Key;
            List<KitchenItem> groupItems = new List<KitchenItem>(kv.Value);
            string printerName = groupItems[0].PrinterName;
            int oid = orderId; string wn = waiterName; DateTime ot = orderTime; string place = placeText;
            float ckNorm = GetFloatSetting("font_kitchen_norm", 10f);
            float ckBold = GetFloatSetting("font_kitchen_bold", 12f);
            float cwMm   = GetFloatSetting("font_receipt_width_mm", 72f);

            PrintDocument doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;
            SetThermalPaper(doc, cwMm);
            doc.PrintPage += delegate (object sender, PrintPageEventArgs e)
            {
                Graphics g    = e.Graphics;
                Font fHeader  = new Font("Courier New", ckBold, FontStyle.Bold);
                Font fBold    = new Font("Courier New", ckNorm, FontStyle.Bold);
                Font fNorm    = new Font("Courier New", ckNorm);
                Font fSmall   = new Font("Courier New", Math.Max(6f, ckNorm - 2f));
                Brush br      = Brushes.Black;
                StringFormat ctr = new StringFormat { Alignment = StringAlignment.Center };

                float x  = e.MarginBounds.Left;
                float y  = e.MarginBounds.Top;
                float w  = e.MarginBounds.Width;
                string sep = new string('-', COLS);

                void L(string text, Font f = null) { Font uf = f ?? fNorm; g.DrawString(text, uf, br, x, y); y += uf.GetHeight(g) + 2; }
                void C(string text, Font f = null) { Font uf = f ?? fNorm; float h = uf.GetHeight(g) + 2; g.DrawString(text, uf, br, new RectangleF(x, y, w, h + 4), ctr); y += h + 4; }

                C("*** BEKOR QILINDI ***", fHeader);
                L(sep);
                C(categoryName, fBold);
                L(sep);

                foreach (KitchenItem item in groupItems)
                {
                    string cUnit    = string.IsNullOrEmpty(item.Unit) ? "ta" : item.Unit;
                    string qtyPart  = "-" + Trunc(item.Quantity.ToString(), 3).PadLeft(3) + " " + Trunc(cUnit, 4).PadRight(4) + " ";
                    string namePart = Trunc(item.FoodName, COLS - qtyPart.Length);
                    L(qtyPart + namePart, fBold);
                }

                L(sep);
                L(TwoCol("Ofitsiant:", Trunc(wn,    20)));
                L(TwoCol("Buyurtma:",  "#" + oid));
                if (!string.IsNullOrEmpty(place)) L(TwoCol("Joy:", Trunc(place, 22)));
                L(TwoCol("Vaqt:",      ot.ToString("dd.MM.yyyy HH:mm")));

                fHeader.Dispose(); fBold.Dispose(); fNorm.Dispose(); fSmall.Dispose();
                e.HasMorePages = false;
            };
            try { doc.Print(); } catch { }
        }
    }

    public static List<KitchenItem> GetKitchenItemsFromFoods(List<int> foodIds, Dictionary<int, int> quantities, Dictionary<int, string> names, Dictionary<int, string> notes = null)
    {
        List<KitchenItem> result = new List<KitchenItem>();
        if (foodIds == null || foodIds.Count == 0) return result;

        try
        {
            // foodIds contains only int values from our own DB — safe to inline
            string ids = string.Join(",", foodIds);
            dbconnect db = new dbconnect();
            string sql = "SELECT f.id, ISNULL(f.unit,'ta') AS unit, ISNULL(fc.printer_name, '') AS printer, ISNULL(fc.name, '') AS cat_name " +
                         "FROM food f JOIN food_category fc ON fc.id = f.food_category_id WHERE f.id IN (" + ids + ")";
            SqlCommand cmd = new SqlCommand(sql, db.GetCon());
            db.OpenCon();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                int fid = (int)dr["id"];
                string printer = dr["printer"].ToString();
                result.Add(new KitchenItem
                {
                    FoodId = fid,
                    FoodName = names.ContainsKey(fid) ? names[fid] : "",
                    Quantity = quantities.ContainsKey(fid) ? quantities[fid] : 1,
                    Unit = dr["unit"].ToString(),
                    PrinterName = printer,
                    CategoryName = dr["cat_name"].ToString(),
                    Note = notes != null && notes.ContainsKey(fid) ? notes[fid] : ""
                });
            }
            dr.Close();
            db.CloseCon();
        }
        catch { }

        return result;
    }

    private static void SetThermalPaper(PrintDocument doc, float widthMm = 72f)
    {
        int w = Math.Max(100, (int)(widthMm * 3.937f));
        try { doc.DefaultPageSettings.PaperSize = new PaperSize("ThermalCustom", w, 2750); } catch { }
        doc.DefaultPageSettings.Margins = new Margins(8, 8, 5, 5);
    }

    public static void EnsureSettingsTable()
    {
        try
        {
            dbconnect db = new dbconnect();
            // Jadval yo'q bo'lsa yaratish (tenant_id bilan composite PK)
            string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID(N'settings') AND type=N'U')
                BEGIN
                    CREATE TABLE settings (
                        [key]      NVARCHAR(100) NOT NULL,
                        [value]    NVARCHAR(MAX) NULL,
                        tenant_id  INT           NOT NULL DEFAULT 0,
                        CONSTRAINT PK_settings PRIMARY KEY ([key], tenant_id)
                    )
                END";
            SqlCommand cmd = new SqlCommand(ddl, db.GetCon());
            db.OpenCon();
            cmd.ExecuteNonQuery();
            db.CloseCon();
        }
        catch { }
    }

    // Online holatda tenant_id bilan, oflayn holatda 0 bilan ishlaydi
    private static int EffectiveTenantId()
    {
        return Session.IsOnline && Session.TenantId > 0 ? Session.TenantId : 0;
    }

    public static string GetSetting(string key, string defaultValue = "")
    {
        try
        {
            EnsureSettingsTable();
            int tid = EffectiveTenantId();
            dbconnect db = new dbconnect();
            // tenant_id bo'yicha aniq filter — RLS ga ishonmaymiz
            SqlCommand cmd = new SqlCommand(
                "SELECT [value] FROM settings WHERE [key]=@k AND tenant_id=@tid",
                db.GetCon());
            cmd.Parameters.AddWithValue("@k",   key);
            cmd.Parameters.AddWithValue("@tid", tid);
            db.OpenCon();
            object result = cmd.ExecuteScalar();
            db.CloseCon();
            return result != null && result != DBNull.Value ? result.ToString() : defaultValue;
        }
        catch { return defaultValue; }
    }

    public static float GetFloatSetting(string key, float defaultValue)
    {
        string val = GetSetting(key, defaultValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        float f;
        return (float.TryParse(val.Replace(",", "."), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out f) && f > 0) ? f : defaultValue;
    }

    public static void SetSetting(string key, string value)
    {
        try
        {
            EnsureSettingsTable();
            int tid = EffectiveTenantId();
            dbconnect db = new dbconnect();
            string sql = @"
                IF EXISTS(SELECT 1 FROM settings WHERE [key]=@k AND tenant_id=@tid)
                    UPDATE settings SET [value]=@v WHERE [key]=@k AND tenant_id=@tid
                ELSE
                    INSERT INTO settings([key],[value],tenant_id) VALUES(@k,@v,@tid)";
            SqlCommand cmd = new SqlCommand(sql, db.GetCon());
            cmd.Parameters.AddWithValue("@k",   key);
            cmd.Parameters.AddWithValue("@v",   (object)value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tid", tid);
            db.OpenCon();
            cmd.ExecuteNonQuery();
            db.CloseCon();
        }
        catch { }
    }
}

public struct KitchenItem
{
    public int FoodId;
    public string FoodName;
    public int Quantity;
    public string Unit;
    public string PrinterName;
    public string CategoryName;
    public string Note;
}
