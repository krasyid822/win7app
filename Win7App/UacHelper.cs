using System;
using System.Security.Principal;
using Microsoft.Win32;

namespace Win7App
{
    /// <summary>
    /// Helper class untuk mengatasi masalah UAC screen capture.
    /// 
    /// Masalah: UAC berjalan di "Secure Desktop" yang tidak bisa di-capture
    /// oleh aplikasi normal.
    /// 
    /// Solusi:
    /// 1. Jalankan aplikasi sebagai Administrator (via manifest)
    /// 2. Disable Secure Desktop (UAC tetap aktif, tapi di desktop biasa)
    /// </summary>
    public static class UacHelper
    {
        private const string UAC_REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string PROMPT_ON_SECURE_DESKTOP = "PromptOnSecureDesktop";
        private const string CONSENT_PROMPT_BEHAVIOR_ADMIN = "ConsentPromptBehaviorAdmin";

        /// <summary>
        /// Cek apakah aplikasi berjalan sebagai Administrator
        /// </summary>
        public static bool IsRunningAsAdmin()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cek apakah Secure Desktop aktif
        /// </summary>
        public static bool IsSecureDesktopEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(UAC_REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(PROMPT_ON_SECURE_DESKTOP, 1);
                        return Convert.ToInt32(value) == 1;
                    }
                }
            }
            catch
            {
            }
            return true; // Default: enabled
        }

        /// <summary>
        /// Disable Secure Desktop agar UAC muncul di desktop biasa
        /// Memerlukan hak Administrator
        /// 
        /// PERINGATAN: Ini mengurangi keamanan sistem sedikit,
        /// tapi UAC masih tetap aktif dan melindungi dari malware.
        /// </summary>
        public static bool DisableSecureDesktop()
        {
            if (!IsRunningAsAdmin())
            {
                return false;
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(UAC_REGISTRY_KEY, true))
                {
                    if (key != null)
                    {
                        // Disable secure desktop (UAC tetap aktif)
                        key.SetValue(PROMPT_ON_SECURE_DESKTOP, 0, RegistryValueKind.DWord);
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Enable kembali Secure Desktop
        /// </summary>
        public static bool EnableSecureDesktop()
        {
            if (!IsRunningAsAdmin())
            {
                return false;
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(UAC_REGISTRY_KEY, true))
                {
                    if (key != null)
                    {
                        key.SetValue(PROMPT_ON_SECURE_DESKTOP, 1, RegistryValueKind.DWord);
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Cek status UAC (apakah enabled)
        /// </summary>
        public static bool IsUacEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(UAC_REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("EnableLUA", 1);
                        return Convert.ToInt32(value) == 1;
                    }
                }
            }
            catch
            {
            }
            return true;
        }

        /// <summary>
        /// Dapatkan status lengkap UAC
        /// </summary>
        public static string GetUacStatus()
        {
            bool isAdmin = IsRunningAsAdmin();
            bool uacEnabled = IsUacEnabled();
            bool secureDesktop = IsSecureDesktopEnabled();

            string status = String.Format(
                "Admin: {0}, UAC: {1}, Secure Desktop: {2}",
                isAdmin ? "Ya" : "Tidak",
                uacEnabled ? "Aktif" : "Nonaktif",
                secureDesktop ? "Aktif" : "Nonaktif"
            );

            return status;
        }

        /// <summary>
        /// Dapatkan rekomendasi untuk capture UAC
        /// </summary>
        public static string GetRecommendation()
        {
            bool isAdmin = IsRunningAsAdmin();
            bool secureDesktop = IsSecureDesktopEnabled();

            if (!isAdmin)
            {
                return "Jalankan aplikasi sebagai Administrator untuk bisa capture layar UAC.";
            }

            if (secureDesktop)
            {
                return "Secure Desktop aktif. Klik 'Disable Secure Desktop' untuk bisa capture layar UAC.\n" +
                       "Catatan: UAC tetap aktif dan melindungi sistem.";
            }

            return "Konfigurasi OK! Layar UAC seharusnya bisa di-capture.";
        }
    }
}
