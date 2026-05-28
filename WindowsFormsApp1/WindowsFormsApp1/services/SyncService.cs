using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    internal static class SyncService
    {
        private static System.Threading.Timer _timer;
        private static System.Threading.Timer _downloadTimer;

        public static void Start()
        {
            // Har 60 soniyada online/offline holatini tekshiradi va upload qiladi
            _timer = new System.Threading.Timer(Tick, null,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(60));

            // Har 1 soatda centraldan localga ma'lumot yuklab oladi (zahira)
            _downloadTimer = new System.Threading.Timer(DownloadTick, null,
                TimeSpan.FromMinutes(2),   // ishga tushgandan 2 daqiqa keyin birinchi download
                TimeSpan.FromHours(1));
        }

        public static void Stop()
        {
            if (_timer        != null) _timer.Dispose();
            if (_downloadTimer != null) _downloadTimer.Dispose();
        }

        // ── Upload: local → central (online bo'lganda) ────────────────────────
        private static void Tick(object state)
        {
            if (Session.ForceOffline) return;

            bool wasOnline = Session.IsOnline;
            bool nowOnline = dbconnect.CheckCentral();

            if (wasOnline == nowOnline) return;

            Session.IsOnline = nowOnline;

            Form form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (form == null || form.IsDisposed) return;

            if (nowOnline)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    SyncEngine.SyncResult upload = SyncEngine.SyncAll();
                    SyncEngine.SyncResult download = SyncEngine.DownloadAll();

                    form.BeginInvoke(new Action(delegate
                    {
                        if (upload.Errors > 0)
                            MessageBox.Show(
                                "Server bilan ulanish tiklandi, lekin sinxronizatsiyada xatolik:\n" + upload.LastError,
                                "FoodX — Sinxronizatsiya xatosi",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else if (upload.Synced > 0)
                            MessageBox.Show(
                                "Ulanish tiklandi!\n" + upload.Synced + " ta oflayn yozuv markaziy serverga yuklandi.",
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
                form.BeginInvoke(new Action(delegate
                {
                    MessageBox.Show(
                        "Server bilan ulanish uzildi.\nDastur oflayn rejimda davom etadi.\nMa'lumotlar mahalliy bazaga yoziladi.",
                        "FoodX — Oflayn",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
            }
        }

        // ── Download: central → local (har 1 soatda, zahira sifatida) ─────────
        private static void DownloadTick(object state)
        {
            if (!Session.IsOnline || Session.ForceOffline) return;
            SyncEngine.DownloadAll();
        }
    }
}
