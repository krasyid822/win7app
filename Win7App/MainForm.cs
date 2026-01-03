using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Win7App
{
    public partial class MainForm : Form
    {
        private MjpegServer _server;
        private const string APP_NAME = "Win7VirtualMonitor";
        private const string REGISTRY_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private string _settingsPath;
        private List<string> _serverUrls = new List<string>();
        private bool _qrVisible = false;

        public MainForm()
        {
            InitializeComponent();
            _server = new MjpegServer(8080);
            _server.LogMessage += Log;
            _settingsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Win7App_settings.ini");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Enable TLS 1.2 for Windows 7 compatibility
            SslCertificateHelper.EnableTls12(Log);
            
            RefreshScreens();
            LoadSettings();
            UpdateIPList();
            UpdateUacStatus();
            
            // Auto-start jika setting aktif
            if (chkAutoStart.Checked)
            {
                buttonStart_Click(this, EventArgs.Empty);
            }
        }

        private void RefreshScreens()
        {
            comboScreens.Items.Clear();
            foreach (var screen in Screen.AllScreens)
            {
                comboScreens.Items.Add(screen.DeviceName + (screen.Primary ? " (Primary)" : ""));
            }
            if (comboScreens.Items.Count > 0)
                comboScreens.SelectedIndex = 0;
        }

        private void UpdateIPList()
        {
            StringBuilder sb = new StringBuilder();
            _serverUrls.Clear();
            
            try
            {
                string hostName = Dns.GetHostName();
                IPAddress[] addresses = Dns.GetHostAddresses(hostName);
                bool first = true;
                string firstIp = "";

                foreach (IPAddress ip in addresses)
                {
                    // Hanya tampilkan IPv4
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (first)
                        {
                            firstIp = ip.ToString();
                        }
                        
                        // Add HTTP URL
                        string httpUrl = String.Format("http://{0}:8080", ip.ToString());
                        _serverUrls.Add(httpUrl);
                        
                        // Add HTTPS URL if enabled
                        if (chkEnableHttps.Checked)
                        {
                            string httpsUrl = String.Format("https://{0}:8081", ip.ToString());
                            _serverUrls.Add(httpsUrl);
                        }
                        
                        first = false;
                    }
                }

                if (string.IsNullOrEmpty(firstIp))
                {
                    sb.Append("Server: (tidak ada IP ditemukan)");
                }
                else
                {
                    sb.Append(String.Format("HTTP: {0}:8080", firstIp));
                    if (chkEnableHttps.Checked)
                    {
                        sb.Append(String.Format("  |  HTTPS: {0}:8081", firstIp));
                    }
                }
            }
            catch (Exception)
            {
                sb.Append("Server: (error mendapatkan IP)");
            }

            labelIPs.Text = sb.ToString();
        }

        private void buttonQR_Click(object sender, EventArgs e)
        {
            _qrVisible = !_qrVisible;
            
            if (_qrVisible)
            {
                GenerateQRCodes();
                panelQR.Visible = true;
                panelQR.Height = 130;
                textLog.Top = panelQR.Bottom + 6;
                textLog.Height = this.ClientSize.Height - textLog.Top - 12;
                buttonQR.Text = "Hide QR";
            }
            else
            {
                panelQR.Controls.Clear();
                panelQR.Visible = false;
                panelQR.Height = 0;
                textLog.Top = 180;
                textLog.Height = this.ClientSize.Height - textLog.Top - 12;
                buttonQR.Text = "Show QR";
            }
        }

        private void GenerateQRCodes()
        {
            panelQR.Controls.Clear();
            
            foreach (string url in _serverUrls)
            {
                Panel itemPanel = new Panel();
                itemPanel.Width = 130;
                itemPanel.Height = 120;
                itemPanel.Margin = new Padding(5);

                PictureBox pb = new PictureBox();
                pb.Width = 100;
                pb.Height = 100;
                pb.Location = new Point(15, 0);
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                
                try
                {
                    pb.Image = QRCodeGenerator.GenerateQRCode(url, 100);
                }
                catch
                {
                    pb.BackColor = Color.Gray;
                }
                
                Label lbl = new Label();
                lbl.Text = url.Replace("http://", "").Replace(":8080", "");
                lbl.Location = new Point(0, 102);
                lbl.Width = 130;
                lbl.TextAlign = ContentAlignment.MiddleCenter;
                lbl.Font = new Font(lbl.Font.FontFamily, 7);

                itemPanel.Controls.Add(pb);
                itemPanel.Controls.Add(lbl);
                panelQR.Controls.Add(itemPanel);
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (comboScreens.SelectedIndex < 0) return;

            var screen = Screen.AllScreens[comboScreens.SelectedIndex];
            string password = textPassword.Text;
            bool enableAudio = chkEnableAudio.Checked;
            bool enableHttps = chkEnableHttps.Checked;
            _server.Start(screen, password, enableAudio, enableHttps);
            
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;
            comboScreens.Enabled = false;
            textPassword.Enabled = false;
            chkEnableAudio.Enabled = false;
            chkEnableHttps.Enabled = false;
            labelStatus.Text = "Status: Running";
            
            UpdateIPList();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            _server.Stop();

            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
            comboScreens.Enabled = true;
            textPassword.Enabled = true;
            chkEnableAudio.Enabled = true;
            chkEnableHttps.Enabled = true;
            labelStatus.Text = "Status: Stopped";
        }

        private void chkAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void chkStartWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey(REGISTRY_RUN_KEY, true);
                if (rk == null) return;

                if (chkStartWithWindows.Checked)
                {
                    string exePath = Application.ExecutablePath;
                    rk.SetValue(APP_NAME, "\"" + exePath + "\"");
                    Log("Ditambahkan ke Windows Startup.");
                }
                else
                {
                    rk.DeleteValue(APP_NAME, false);
                    Log("Dihapus dari Windows Startup.");
                }
                rk.Close();
            }
            catch (Exception ex)
            {
                Log(String.Format("Error mengatur startup: {0}", ex.Message));
            }

            SaveSettings();
        }

        private void LoadSettings()
        {
            // Set defaults dulu
            chkEnableHttps.Checked = true;
            chkEnableAudio.Checked = true;
            
            try
            {
                if (System.IO.File.Exists(_settingsPath))
                {
                    string[] lines = System.IO.File.ReadAllLines(_settingsPath);
                    bool hasHttpsSetting = false;
                    bool hasAudioSetting = false;
                    
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();
                            
                            if (key == "AutoStart")
                                chkAutoStart.Checked = value == "1";
                            else if (key == "Password")
                                textPassword.Text = value;
                            else if (key == "ScreenIndex")
                            {
                                int idx = 0;
                                if (int.TryParse(value, out idx) && idx < comboScreens.Items.Count)
                                    comboScreens.SelectedIndex = idx;
                            }
                            else if (key == "EnableAudio")
                            {
                                chkEnableAudio.Checked = value == "1";
                                hasAudioSetting = true;
                            }
                            else if (key == "EnableHttps")
                            {
                                chkEnableHttps.Checked = value == "1";
                                hasHttpsSetting = true;
                            }
                        }
                    }
                    
                    // Jika setting tidak ada di file, tetap default true
                    if (!hasHttpsSetting) chkEnableHttps.Checked = true;
                    if (!hasAudioSetting) chkEnableAudio.Checked = true;
                }

                // Check registry for StartWithWindows
                RegistryKey rk = Registry.CurrentUser.OpenSubKey(REGISTRY_RUN_KEY, false);
                if (rk != null)
                {
                    object val = rk.GetValue(APP_NAME);
                    chkStartWithWindows.Checked = (val != null);
                    rk.Close();
                }
            }
            catch (Exception ex)
            {
                Log(String.Format("Error memuat settings: {0}", ex.Message));
            }
        }

        private void SaveSettings()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(String.Format("AutoStart={0}", chkAutoStart.Checked ? "1" : "0"));
                sb.AppendLine(String.Format("Password={0}", textPassword.Text));
                sb.AppendLine(String.Format("ScreenIndex={0}", comboScreens.SelectedIndex));
                sb.AppendLine(String.Format("EnableAudio={0}", chkEnableAudio.Checked ? "1" : "0"));
                sb.AppendLine(String.Format("EnableHttps={0}", chkEnableHttps.Checked ? "1" : "0"));
                System.IO.File.WriteAllText(_settingsPath, sb.ToString());
            }
            catch (Exception ex)
            {
                Log(String.Format("Error menyimpan settings: {0}", ex.Message));
            }
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(Log), message);
                return;
            }
            textLog.AppendText(String.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine));
        }

        private void UpdateUacStatus()
        {
            bool isAdmin = UacHelper.IsRunningAsAdmin();
            bool secureDesktop = UacHelper.IsSecureDesktopEnabled();

            if (!isAdmin)
            {
                // Check if running from auto-startup
                bool isAutoStartup = chkStartWithWindows.Checked;
                if (isAutoStartup)
                {
                    labelUacStatus.Text = "Auto-start tanpa Admin.\nKlik untuk restart sebagai Admin.";
                }
                else
                {
                    labelUacStatus.Text = "Tidak sebagai Admin.\nKlik untuk restart sebagai Admin.";
                }
                labelUacStatus.ForeColor = System.Drawing.Color.Red;
                buttonUacToggle.Enabled = true;  // Enable button to allow restart as admin
                buttonUacToggle.Text = "Run Admin";
            }
            else if (secureDesktop)
            {
                labelUacStatus.Text = "Secure Desktop: AKTIF\nKlik untuk capture layar UAC.";
                labelUacStatus.ForeColor = System.Drawing.Color.DarkOrange;
                buttonUacToggle.Enabled = true;
                buttonUacToggle.Text = "Disable Secure";
            }
            else
            {
                labelUacStatus.Text = "Secure Desktop: NONAKTIF\nLayar UAC bisa di-capture!";
                labelUacStatus.ForeColor = System.Drawing.Color.Green;
                buttonUacToggle.Enabled = true;
                buttonUacToggle.Text = "Enable Secure";
            }

            // Log status
            Log(UacHelper.GetUacStatus());
        }

        private void buttonUacToggle_Click(object sender, EventArgs e)
        {
            bool isAdmin = UacHelper.IsRunningAsAdmin();
            
            // If not admin, restart app as admin
            if (!isAdmin)
            {
                var result = MessageBox.Show(
                    "Aplikasi akan di-restart dengan hak Administrator.\n\n" +
                    "Ini diperlukan untuk:\n" +
                    "• Capture layar UAC\n" +
                    "• Mengubah pengaturan Secure Desktop\n\n" +
                    "Lanjutkan?",
                    "Restart sebagai Administrator",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Stop server if running
                        if (_server != null)
                        {
                            _server.Stop();
                        }
                        
                        // Save settings before restart
                        SaveSettings();
                        
                        // Restart as admin
                        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                        startInfo.FileName = Application.ExecutablePath;
                        startInfo.UseShellExecute = true;
                        startInfo.Verb = "runas";  // Request admin privileges
                        
                        System.Diagnostics.Process.Start(startInfo);
                        
                        // Close current instance
                        Application.Exit();
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED - user declined UAC
                        {
                            Log("User membatalkan permintaan Administrator.");
                            MessageBox.Show(
                                "Permintaan Administrator dibatalkan.\n\n" +
                                "Fitur capture layar UAC tidak akan berfungsi.",
                                "Dibatalkan",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            Log(String.Format("Error restart as admin: {0}", ex.Message));
                            MessageBox.Show(
                                "Gagal restart sebagai Administrator:\n" + ex.Message,
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
                return;
            }
            
            // Already admin - toggle secure desktop
            bool secureDesktop = UacHelper.IsSecureDesktopEnabled();

            if (secureDesktop)
            {
                // Disable secure desktop
                var result = MessageBox.Show(
                    "Ini akan menonaktifkan Secure Desktop untuk UAC.\n\n" +
                    "UAC tetap aktif dan melindungi sistem.\n" +
                    "Hanya tampilannya yang akan di desktop biasa\n" +
                    "sehingga bisa di-capture untuk streaming.\n\n" +
                    "Lanjutkan?",
                    "Disable Secure Desktop",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    if (UacHelper.DisableSecureDesktop())
                    {
                        Log("Secure Desktop dinonaktifkan. Layar UAC bisa di-capture.");
                        MessageBox.Show(
                            "Secure Desktop berhasil dinonaktifkan.\n\n" +
                            "Sekarang layar UAC bisa dilihat di Android!",
                            "Sukses",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        Log("Gagal menonaktifkan Secure Desktop.");
                        MessageBox.Show(
                            "Gagal menonaktifkan Secure Desktop.\n" +
                            "Pastikan aplikasi berjalan sebagai Administrator.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                // Enable secure desktop
                if (UacHelper.EnableSecureDesktop())
                {
                    Log("Secure Desktop diaktifkan kembali.");
                    MessageBox.Show(
                        "Secure Desktop berhasil diaktifkan kembali.\n\n" +
                        "UAC akan muncul di desktop terpisah.",
                        "Sukses",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    Log("Gagal mengaktifkan Secure Desktop.");
                }
            }

            UpdateUacStatus();
        }

        private void buttonExportCert_Click(object sender, EventArgs e)
        {
            string certPath = SslCertificateHelper.GetPublicCertificatePath();
            bool certInTrustedRoot = SslCertificateHelper.IsCertificateInTrustedRoot();
            
            string status = certInTrustedRoot ? "✓ Terinstall" : "⚠ Belum terinstall";
            string message = String.Format(
                "SSL Certificate: {0}\n\n" +
                "Pilih aksi:\n" +
                "• YES = Install certificate ke Windows\n" +
                "• NO = Regenerate certificate (fix SSL error)\n" +
                "• Cancel = Export untuk Android",
                status);

            DialogResult result = MessageBox.Show(
                message,
                "SSL Certificate",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                // Install certificate
                Log("Installing SSL certificate...");
                bool success = SslCertificateHelper.ForceReinstallCertificate(Log);
                
                MessageBox.Show(
                    success ? "Certificate berhasil diinstall!" : "Gagal install certificate.",
                    success ? "Success" : "Error",
                    MessageBoxButtons.OK,
                    success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            else if (result == DialogResult.No)
            {
                // Regenerate
                if (MessageBox.Show(
                    "Regenerate certificate?\n\nIni akan membuat certificate baru.",
                    "Konfirmasi",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Log("Regenerating SSL certificate...");
                    bool success = SslCertificateHelper.RegenerateCertificate(Log);
                    
                    MessageBox.Show(
                        success ? "Certificate baru dibuat!\nRestart aplikasi." : "Gagal regenerate. Lihat log.",
                        success ? "Success" : "Error",
                        MessageBoxButtons.OK,
                        success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }
            }
            else if (result == DialogResult.Cancel)
            {
                // Export for Android
                if (!System.IO.File.Exists(certPath))
                {
                    Log("Generating SSL certificate...");
                    SslCertificateHelper.GetOrCreateCertificate(Log);
                }

                if (System.IO.File.Exists(certPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + certPath + "\"");
                    
                    MessageBox.Show(
                        "Copy file .cer ke Android.\n\n" +
                        "Di Android:\n" +
                        "Settings > Security > Install certificate\n\n" +
                        "File: " + certPath,
                        "Export untuk Android",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        private void buttonExportLog_Click(object sender, EventArgs e)
        {
            try
            {
                // Create log content with timestamp
                string logContent = String.Format(
                    "=== Win7App Log Export ===\r\n" +
                    "Date: {0}\r\n" +
                    "Computer: {1}\r\n" +
                    "OS: {2}\r\n" +
                    ".NET: {3}\r\n" +
                    "========================\r\n\r\n{4}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Environment.MachineName,
                    Environment.OSVersion.ToString(),
                    Environment.Version.ToString(),
                    textLog.Text);

                // Show save dialog
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*";
                saveDialog.DefaultExt = "txt";
                saveDialog.FileName = String.Format("Win7App_log_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                
                // Try to default to Desktop or Documents
                try
                {
                    saveDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                catch
                {
                    saveDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, logContent, Encoding.UTF8);
                    
                    Log(String.Format("Log exported to: {0}", saveDialog.FileName));
                    
                    // Ask to open folder
                    if (MessageBox.Show(
                        "Log berhasil disimpan!\n\n" + saveDialog.FileName + "\n\nBuka folder?",
                        "Export Log",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + saveDialog.FileName + "\"");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Gagal export log:\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
