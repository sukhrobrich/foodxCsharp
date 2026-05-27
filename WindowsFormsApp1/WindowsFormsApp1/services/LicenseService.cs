using System;
using System.IO;
using System.Net;
using System.Text;

namespace WindowsFormsApp1.services
{
    internal static class LicenseService
    {
        public static string ApiUrl = "http://192.168.35.230/api/licenses/verify";

        private static readonly string LicFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FoodX", "lic.dat");

        // ── Natija ──────────────────────────────────────────────────────────
        public class Result
        {
            public bool   Valid;
            public bool   Offline;
            public string Message    = "";
            public string ClientName = "";
            public string CafeName   = "";
            public string ExpiresAt  = "";
            public int    DaysLeft;
            public int    TenantId;
        }

        // ── Saqlash / o'qish ────────────────────────────────────────────────
        public static (string login, string pass, int tenantId)? LoadSaved()
        {
            try
            {
                if (!File.Exists(LicFile)) return null;
                var lines = File.ReadAllLines(LicFile, Encoding.UTF8);
                if (lines.Length >= 2 && !string.IsNullOrEmpty(lines[0]))
                {
                    int tid = lines.Length >= 3 ? (int.TryParse(lines[2], out int t) ? t : 0) : 0;
                    return (lines[0], lines[1], tid);
                }
            }
            catch { }
            return null;
        }

        public static void Save(string login, string pass, int tenantId = 0)
        {
            try
            {
                string dir = Path.GetDirectoryName(LicFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(LicFile, new[] { login, pass, tenantId.ToString() }, Encoding.UTF8);
            }
            catch { }
        }

        public static void Clear()
        {
            try { if (File.Exists(LicFile)) File.Delete(LicFile); } catch { }
        }

        // ── Asosiy tekshiruv ────────────────────────────────────────────────
        public static Result Verify(string login, string password)
        {
            try
            {
                string body = string.Format(
                    "{{\"login\":\"{0}\",\"password\":\"{1}\",\"machineName\":\"{2}\"}}",
                    Esc(login), Esc(password), Esc(Environment.MachineName));

                var wr = (HttpWebRequest)WebRequest.Create(ApiUrl);
                wr.Method      = "POST";
                wr.ContentType = "application/json";
                wr.Timeout     = 8000;
                byte[] buf = Encoding.UTF8.GetBytes(body);
                wr.ContentLength = buf.Length;
                using (var s = wr.GetRequestStream()) s.Write(buf, 0, buf.Length);

                string json;
                using (var resp = (HttpWebResponse)wr.GetResponse())
                using (var rd   = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    json = rd.ReadToEnd();

                return Parse(json, false);
            }
            catch (WebException wx) when (
                wx.Status == WebExceptionStatus.ConnectFailure  ||
                wx.Status == WebExceptionStatus.Timeout         ||
                wx.Status == WebExceptionStatus.NameResolutionFailure)
            {
                // Server yetib bo'lmasa — saqlangan tenant_id bilan oflayn ruxsat
                int savedTid = 0;
                var saved = LoadSaved();
                if (saved.HasValue) savedTid = saved.Value.tenantId;
                return new Result { Valid = true, Offline = true, TenantId = savedTid,
                    Message = "Server bilan ulanish yo'q. Oflayn rejimda ishlayapti.", DaysLeft = 999 };
            }
            catch (Exception ex)
            {
                return new Result { Valid = false, Message = "Xatolik: " + ex.Message };
            }
        }

        // ── JSON parser (tashqi kutubxonasiz) ──────────────────────────────
        private static Result Parse(string json, bool offline)
        {
            var r = new Result { Offline = offline };
            r.Valid      = json.Contains("\"valid\":true");
            r.Message    = Get(json, "message");
            r.ClientName = Get(json, "clientName");
            r.CafeName   = Get(json, "cafeName");
            r.ExpiresAt  = Get(json, "expiresAt");
            int.TryParse(Get(json, "daysLeft"),  out r.DaysLeft);
            int.TryParse(Get(json, "tenantId"),  out r.TenantId);
            return r;
        }

        private static string Get(string json, string key)
        {
            string q = "\"" + key + "\":";
            int i = json.IndexOf(q, StringComparison.Ordinal);
            if (i < 0) return "";
            i += q.Length;
            if (i >= json.Length) return "";
            if (json[i] == '"')
            {
                i++;
                int end = json.IndexOf('"', i);
                return end < 0 ? "" : json.Substring(i, end - i);
            }
            int n = json.IndexOfAny(new[] { ',', '}' }, i);
            return n < 0 ? "" : json.Substring(i, n - i).Trim();
        }

        private static string Esc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
