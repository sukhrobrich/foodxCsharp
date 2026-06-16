using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    // Admin/kassir/ofitsiant sahifalarida obuna qoldiq kunini bir xil
    // ko'rinishda ko'rsatish uchun (Session.LicenseDaysLeft asosida).
    internal static class LicenseDaysDisplay
    {
        static readonly Color C_Danger = Color.FromArgb(220, 38, 38);
        static readonly Color C_Warn   = Color.FromArgb(217, 119, 6);
        static readonly Color C_Muted  = Color.FromArgb(107, 114, 128);

        public static void Apply(Label lbl)
        {
            int d = Session.LicenseDaysLeft;
            if (d >= int.MaxValue - 1) { lbl.Text = ""; return; }

            if (d <= 1)
            {
                lbl.Text      = d < 0 ? "⚠ Obuna tugagan!" : "⚠ Obuna: " + d + " kun qoldi!";
                lbl.ForeColor = C_Danger;
            }
            else if (d <= 7)
            {
                lbl.Text      = "Obuna: " + d + " kun qoldi";
                lbl.ForeColor = C_Warn;
            }
            else
            {
                lbl.Text      = "Obuna: " + d + " kun";
                lbl.ForeColor = C_Muted;
            }
        }
    }
}
