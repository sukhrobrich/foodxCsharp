using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1.services
{
    public class MultiMonoblokClient
    {
        private readonly string _apiUrl;
        private readonly int    _tenantId;
        public string Token    { get; set; }
        public int    TenantId => _tenantId;
        public string ApiUrl   => _apiUrl;

        public MultiMonoblokClient(string apiUrl, int tenantId)
        {
            _apiUrl   = apiUrl.TrimEnd('/');
            _tenantId = tenantId;
        }

        // ── API metodlar ─────────────────────────────────────────────────────

        public Task<string> GetStaffListAsync()
            => RunAsync(() => Request("GET", $"/api/auth/staff-list?tenantId={_tenantId}", null, auth: false));

        public Task<string> QuickLoginAsync(int userId)
            => RunAsync(() => Request("POST", "/api/auth/quick-login",
                $"{{\"userId\":{userId},\"tenantId\":{_tenantId}}}", auth: false));

        public Task<string> LoginWithPinAsync(string login, string pin)
            => RunAsync(() => Request("POST", "/api/auth/login",
                $"{{\"login\":\"{Esc(login)}\",\"password\":\"{Esc(pin)}\",\"tenantId\":{_tenantId}}}", auth: false));

        public Task<string> GetPlacesAsync()
            => RunAsync(() => Request("GET", "/api/places"));

        public Task<string> GetFoodsAsync(int? categoryId = null)
            => RunAsync(() => Request("GET",
                categoryId.HasValue ? $"/api/foods?categoryId={categoryId}" : "/api/foods"));

        public Task<string> GetFoodCategoriesAsync()
            => RunAsync(() => Request("GET", "/api/foods/categories"));

        public Task<string> GetOrderAsync(int orderId)
            => RunAsync(() => Request("GET", $"/api/orders/{orderId}"));

        public Task<string> CreateOrderAsync(string json)
            => RunAsync(() => Request("POST", "/api/orders", json));

        public Task<string> UpdateOrderItemsAsync(int orderId, string json)
            => RunAsync(() => Request("PUT", $"/api/orders/{orderId}/items", json));

        public Task<string> GetPaymentsAsync()
            => RunAsync(() => Request("GET", "/api/payments"));

        public Task<string> PayOrderAsync(int orderId, int paymentId, decimal amount)
        {
            string body = $"{{\"payments\":[{{\"paymentId\":{paymentId},\"amount\":{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}";
            return RunAsync(() => Request("PUT", $"/api/orders/{orderId}/pay", body));
        }

        // ── JSON yordamchi metodlar ──────────────────────────────────────────

        public static string JsonStr(string json, string key)
        {
            string q = "\"" + key + "\":";
            int i = json.IndexOf(q, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            i += q.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\n' || json[i] == '\r')) i++;
            if (i >= json.Length) return "";
            if (json[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < json.Length && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < json.Length)
                    {
                        i++;
                        switch (json[i])
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            default: sb.Append(json[i]); break;
                        }
                    }
                    else sb.Append(json[i]);
                    i++;
                }
                return sb.ToString();
            }
            int end = json.IndexOfAny(new[] { ',', '}', ']', ' ', '\n', '\r', '\t' }, i);
            return end < 0 ? json.Substring(i) : json.Substring(i, end - i).Trim();
        }

        public static int JsonInt(string json, string key)
        {
            int.TryParse(JsonStr(json, key), out int v);
            return v;
        }

        public static decimal JsonDec(string json, string key)
        {
            decimal.TryParse(JsonStr(json, key),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal v);
            return v;
        }

        // Root massivdan JSON obyektlarini ajratib olish
        public static List<string> JsonArr(string json)
        {
            var list = new List<string>();
            int i = json.IndexOf('[');
            if (i < 0) return list;
            i++;
            while (i < json.Length)
            {
                if (json[i] == '{')
                {
                    int depth = 0, start = i;
                    bool inStr = false;
                    while (i < json.Length)
                    {
                        char c = json[i];
                        if (!inStr)
                        {
                            if (c == '"')   inStr = true;
                            else if (c == '{') depth++;
                            else if (c == '}')
                            {
                                depth--;
                                if (depth == 0) { list.Add(json.Substring(start, i - start + 1)); i++; break; }
                            }
                        }
                        else
                        {
                            if (c == '\\') i++;
                            else if (c == '"') inStr = false;
                        }
                        i++;
                    }
                }
                else if (json[i] == ']') break;
                else i++;
            }
            return list;
        }

        // Ichki massiv yoki obyekt qiymatini string sifatida olish
        public static string JsonNested(string json, string key)
        {
            string q = "\"" + key + "\":";
            int i = json.IndexOf(q, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "[]";
            i += q.Length;
            while (i < json.Length && json[i] != '{' && json[i] != '[') i++;
            if (i >= json.Length) return "[]";
            char open = json[i];
            char close = open == '{' ? '}' : ']';
            int depth = 0, start = i;
            bool inStr = false;
            while (i < json.Length)
            {
                char c = json[i];
                if (!inStr)
                {
                    if (c == '"')       inStr = true;
                    else if (c == open)  depth++;
                    else if (c == close) { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
                }
                else { if (c == '\\') i++; else if (c == '"') inStr = false; }
                i++;
            }
            return "[]";
        }

        // JSON string qiymatini escape qilish
        public static string Esc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        // ── HTTP so'rovlar ──────────────────────────────────────────────────

        private string Request(string method, string path, string body = null, bool auth = true)
        {
            var req = (HttpWebRequest)WebRequest.Create(_apiUrl + path);
            req.Method  = method;
            req.Timeout = 10000;
            if (body != null) req.ContentType = "application/json";
            if (auth && !string.IsNullOrEmpty(Token))
                req.Headers["Authorization"] = "Bearer " + Token;

            if (body != null)
            {
                byte[] buf = Encoding.UTF8.GetBytes(body);
                req.ContentLength = buf.Length;
                using (var s = req.GetRequestStream()) s.Write(buf, 0, buf.Length);
            }

            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd   = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch (WebException wx)
            {
                if (wx.Response is HttpWebResponse err)
                {
                    string errBody = "";
                    try { using (var rd = new StreamReader(err.GetResponseStream(), Encoding.UTF8)) errBody = rd.ReadToEnd(); } catch { }
                    throw new MultiApiException((int)err.StatusCode, errBody, wx);
                }
                // Server yoqilmagan yoki tarmoq xatosi — MultiApiException(0) sifatida qayta yuborish
                throw new MultiApiException(0, "{\"message\":\"API serverga ulanib bo'lmadi\"}", wx);
            }
        }

        private static Task<string> RunAsync(Func<string> fn)
            => Task.Run(fn);
    }

    public class MultiApiException : Exception
    {
        public int    StatusCode { get; }
        public string Body       { get; }
        public MultiApiException(int code, string body, Exception inner)
            : base($"HTTP {code}: {body}", inner)
        {
            StatusCode = code;
            Body       = body;
        }
    }
}
