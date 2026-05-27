using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    // Har 60 soniyada serverga ulanishni tekshiradi.
    // Oflayn→online o'tishda sinxronizatsiyani avtomatik ishga tushiradi.
    internal static class SyncService
    {
        private static System.Threading.Timer _timer;

        public static void Start()
        {
            _timer = new Timer(Tick, null,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(60));
        }

        public static void Stop() => _timer?.Dispose();

        private static void Tick(object state)
        {
            bool wasOnline = Session.IsOnline;
            bool nowOnline = dbconnect.CheckCentral();

            if (wasOnline == nowOnline) return;

            Session.IsOnline = nowOnline;

            var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (form == null || form.IsDisposed) return;

            if (nowOnline)
            {
                // Oflayn→Online: sinxronizatsiyani fon threadda ishlatamiz
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    var result = SyncEngine.SyncAll();

                    form.BeginInvoke(new Action(() =>
                    {
                        if (result.Errors > 0)
                            MessageBox.Show(
                                $"Server bilan ulanish tiklandi, lekin sinxronizatsiyada xatolik:\n{result.LastError}",
                                "FoodX — Sinxronizatsiya xatosi",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else if (result.Synced > 0)
                            MessageBox.Show(
                                $"Ulanish tiklandi!\n{result.Synced} ta oflayn yozuv markaziy serverga yuklandi.",
                                "FoodX — Sinxronizatsiya tugadi",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show(
                                "Server bilan ulanish tiklandi.\nDastur onlayn rejimda ishlaydi.",
                                "FoodX — Onlayn",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                });
            }
            else
            {
                form.BeginInvoke(new Action(() =>
                    MessageBox.Show(
                        "Server bilan ulanish uzildi.\nDastur oflayn rejimda davom etadi.\nMa'lumotlar mahalliy bazaga yoziladi.",
                        "FoodX — Oflayn",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            }
        }
    }
}
