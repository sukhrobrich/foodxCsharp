using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    // SystemSounds faqat WAV o'ynay oladi — bildirishnoma uchun mp3 fayl
    // (sounds\notif.mp3) kerak bo'lgani uchun winmm.dll (MCI) orqali ijro etiladi.
    internal static class NotificationSound
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, IntPtr returnBuffer, int returnLength, IntPtr hwndCallback);

        const string ALIAS = "foodxNotif";

        public static void Play()
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "sounds", "notif.mp3");
                if (!File.Exists(path)) return;

                mciSendString("close " + ALIAS, IntPtr.Zero, 0, IntPtr.Zero);
                mciSendString(string.Format("open \"{0}\" type mpegvideo alias {1}", path, ALIAS), IntPtr.Zero, 0, IntPtr.Zero);
                mciSendString("play " + ALIAS, IntPtr.Zero, 0, IntPtr.Zero);
            }
            catch { }
        }
    }
}
