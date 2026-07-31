internal static class UIScale
{
    static float F => Math.Min(1f, System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width / 1366f);
    public static int Px(int dp, int min = 0) { int s = (int)(dp * F); return min > 0 ? Math.Max(min, s) : s; }
    public static int Sidebar      => Px(240, 180);
    public static int CartPanel    => Px(380, 290);
    public static int CatPanel     => Px(200, 150);
    public static int TilePhW      => Px(170, 130);
    public static int TilePhH      => Px(218, 165);
    public static int TileImgH     => Px(138, 104);
    public static int TilePhNamW   => Px(148, 108);
    public static int TileTxW      => Px(200, 155);
    public static int TileTxH      => Px(106,  80);
    public static int EditPanelLg  => Px(520, 380);
    public static int EditPanelMd  => Px(440, 340);
    public static int EditPanelSm  => Px(380, 290);
    public static int EditPanelXs  => Px(360, 270);
}

public static class Session
{
    public static int    UserId     { get; set; }
    public static string Login      { get; set; }
    public static string UserName   { get; set; }
    public static string UserCategory { get; set; }

    // Litsenziya / tenant
    public static int    TenantId    { get; set; }
    public static bool   IsOnline    { get; set; }
    public static bool   ForceOffline { get; set; }

    // Obuna qoldiq kuni (Program.cs watchdog tomonidan yangilanadi).
    // int.MaxValue — hali aniqlanmagan (login bosqichidan oldin) degani.
    public static int LicenseDaysLeft { get; set; } = int.MaxValue;

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
