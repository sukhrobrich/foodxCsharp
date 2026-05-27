using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    // Fon monitori: har 60 soniyada markaziy serverga ulanishni tekshiradi.
    // Online↔offline holatini Session.IsOnline orqali yangilaydi.
    internal static class SyncService
    {
        private static Timer _timer;

        public static void Start()
        {
            _timer = new Timer(Tick, null,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(60));
        }

        public static void Stop()
        {
            _timer?.Dispose();
        }

        private static void Tick(object state)
        {
            bool wasOnline  = Session.IsOnline;
            bool nowOnline  = dbconnect.CheckCentral();

            if (wasOnline == nowOnline) return;

            Session.IsOnline = nowOnline;

            var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (form == null || form.IsDisposed) return;

            form.BeginInvoke(new Action(() =>
            {
                if (nowOnline)
                    MessageBox.Show(
                        "Server bilan ulanish tiklandi.\nDastur onlayn rejimda ishlaydi.",
                        "FoodX — Onlayn", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show(
                        "Server bilan ulanish uzildi.\nDastur oflayn rejimda davom etadi.\nMa'lumotlar mahalliy bazaga yoziladi.",
                        "FoodX — Oflayn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }));
        }
    }
}
