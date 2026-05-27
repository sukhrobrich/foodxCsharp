public static class Session
{
    public static int    UserId     { get; set; }
    public static string Login      { get; set; }
    public static string UserName   { get; set; }
    public static string UserCategory { get; set; }

    // Litsenziya / tenant
    public static int    TenantId   { get; set; }
    public static bool   IsOnline   { get; set; }

    public static bool IsAdmin => UserCategory == "admin";
    public static bool IsKassir => UserCategory == "kassir";
    public static bool IsWaiter => UserCategory == "ofitsiant";
    public static bool CanManageOrders => IsAdmin || IsKassir;

    public static void Clear()
    {
        UserId = 0;
        Login = null;
        UserName = null;
        UserCategory = null;
    }

    // SHA-256 hash of a PIN; 64-char hex string
    public static string HashPin(string pin)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pin));
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

    // Returns true if value looks like a plain 4-digit PIN (not yet hashed)
    public static bool IsPlainPin(string value) =>
        value != null && value.Length == 4 && System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}$");
}
