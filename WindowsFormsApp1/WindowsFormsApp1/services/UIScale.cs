using System;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    // Kichik ekranlarda UI o'lchamlarini proporsional kamaytiradi.
    // Mos hisob sxemasi: 1366px = 100% (reference). Undan kichik = kamaytiradi. Undan katta = 100% (oshirmaydi).
    internal static class UIScale
    {
        static float F => Math.Min(1f, Screen.PrimaryScreen.WorkingArea.Width / 1366f);

        // Berilgan dizayn px ni scale qiladi. min = kamida shu qiymat.
        public static int Px(int designPx, int min = 0)
        {
            int scaled = (int)(designPx * F);
            return min > 0 ? Math.Max(min, scaled) : scaled;
        }

        // ── Asosiy komponentlar ──────────────────────────────────────────────
        public static int Sidebar    => Px(240, 180);   // Chap sidebar kengligi
        public static int CartPanel  => Px(380, 290);   // AddOrder: o'ng korzina paneli
        public static int CatPanel   => Px(200, 150);   // AddOrder: chap kategoriyalar paneli

        // ── Taom kartasi (foto rejim) ─────────────────────────────────────
        public static int TilePhW    => Px(170, 130);   // Foto karta kengligi
        public static int TilePhH    => Px(218, 165);   // Foto karta balandligi
        public static int TileImgH   => Px(138, 104);   // Rasm joyi balandligi
        public static int TilePhNamW => Px(148, 108);   // Nom label kengligi

        // ── Taom kartasi (matn rejim) ─────────────────────────────────────
        public static int TileTxW    => Px(200, 155);   // Matn karta kengligi
        public static int TileTxH    => Px(106, 80);    // Matn karta balandligi

        // ── Boshqa panellar ───────────────────────────────────────────────
        public static int EditPanelLg => Px(520, 380);  // SettingsPanel leftWrap
        public static int EditPanelMd => Px(440, 340);  // AddFood editPanel
        public static int EditPanelSm => Px(380, 290);  // userAdd editPanel
        public static int EditPanelXs => Px(360, 270);  // AddFoodCategory editPanel
    }
}
