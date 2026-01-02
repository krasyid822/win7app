using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Win7App
{
    public static class SslCertificateHelper
    {
        private const string CERT_SUBJECT = "CN=Win7VirtualMonitor";
        private const string CERT_FRIENDLY_NAME = "Win7 Virtual Monitor SSL";
        private const string CERT_PASSWORD = "win7vm2026";
        private static readonly string CERT_FILE_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Win7App_cert.pfx");

        public static X509Certificate2 GetOrCreateCertificate(Action<string> log)
        {
            try
            {
                // 1. Coba load dari file dulu
                if (File.Exists(CERT_FILE_PATH))
                {
                    try
                    {
                        X509Certificate2 cert = new X509Certificate2(CERT_FILE_PATH, CERT_PASSWORD,
                            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
                        if (cert.NotAfter > DateTime.Now && cert.HasPrivateKey)
                        {
                            log(String.Format("Loaded SSL certificate: HasPrivateKey={0}", cert.HasPrivateKey));
                            return cert;
                        }
                        log("Certificate expired or no private key, creating new one...");
                    }
                    catch (Exception ex)
                    {
                        log(String.Format("Failed to load cert file: {0}", ex.Message));
                    }
                    // Delete invalid cert file
                    try { File.Delete(CERT_FILE_PATH); } catch { }
                }

                // 2. Buat certificate baru via PowerShell
                log("Creating new self-signed SSL certificate...");
                X509Certificate2 newCert = CreateCertificateWithPowerShell(log);
                if (newCert != null && newCert.HasPrivateKey) 
                {
                    log(String.Format("Certificate ready: HasPrivateKey={0}", newCert.HasPrivateKey));
                    return newCert;
                }

                // 3. Fallback: OpenSSL style via makecert
                newCert = CreateCertificateWithMakeCert(log);
                if (newCert != null && newCert.HasPrivateKey) 
                {
                    log(String.Format("Certificate from makecert: HasPrivateKey={0}", newCert.HasPrivateKey));
                    return newCert;
                }

                log("Could not create certificate with private key. HTTPS will be disabled.");
                return null;
            }
            catch (Exception ex)
            {
                log(String.Format("Certificate error: {0}", ex.Message));
                return null;
            }
        }

        private static X509Certificate2 CreateCertificateWithPowerShell(Action<string> log)
        {
            try
            {
                string tempPfxPath = Path.Combine(Path.GetTempPath(), "win7vm_temp_cert.pfx");
                
                // Hapus file temp lama
                try { File.Delete(tempPfxPath); } catch { }
                
                // Gunakan PowerShell dengan Import-Module PKI terlebih dahulu
                string psScript = String.Format(
                    "Import-Module PKI -ErrorAction SilentlyContinue; " +
                    "$cert = New-SelfSignedCertificate -Subject '{0}' -DnsName 'localhost','Win7VirtualMonitor' " +
                    "-CertStoreLocation 'Cert:\\CurrentUser\\My' " +
                    "-KeyAlgorithm RSA -KeyLength 2048 " +
                    "-NotAfter (Get-Date).AddYears(10) " +
                    "-KeyExportPolicy Exportable " +
                    "-KeySpec KeyExchange; " +
                    "$pwd = ConvertTo-SecureString -String '{1}' -Force -AsPlainText; " +
                    "Export-PfxCertificate -Cert $cert -FilePath '{2}' -Password $pwd",
                    CERT_SUBJECT, CERT_PASSWORD, tempPfxPath.Replace("\\", "\\\\"));

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = String.Format("-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"", psScript);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                log("Trying PowerShell PKI module...");
                Process proc = Process.Start(psi);
                proc.WaitForExit(30000);
                string error = proc.StandardError.ReadToEnd();

                if (File.Exists(tempPfxPath))
                {
                    X509Certificate2 cert = new X509Certificate2(tempPfxPath, CERT_PASSWORD,
                        X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
                    
                    if (cert.HasPrivateKey)
                    {
                        File.Copy(tempPfxPath, CERT_FILE_PATH, true);
                        try { File.Delete(tempPfxPath); } catch { }
                        log(String.Format("Certificate created via PowerShell: HasPrivateKey={0}", cert.HasPrivateKey));
                        return cert;
                    }
                }
                
                if (!string.IsNullOrEmpty(error) && error.Length > 10)
                    log(String.Format("PowerShell: {0}", error.Substring(0, Math.Min(100, error.Length))));
            }
            catch (Exception ex)
            {
                log(String.Format("PowerShell failed: {0}", ex.Message));
            }
            return null;
        }

        private static X509Certificate2 CreateCertificateWithCertreq(Action<string> log)
        {
            try
            {
                string tempDir = Path.GetTempPath();
                string infPath = Path.Combine(tempDir, "win7vm_cert.inf");
                string cerPath = Path.Combine(tempDir, "win7vm_cert.cer");
                string pvkPath = Path.Combine(tempDir, "win7vm_cert.pvk");
                string pfxPath = Path.Combine(tempDir, "win7vm_cert.pfx");

                // Cleanup old files
                try { File.Delete(infPath); } catch { }
                try { File.Delete(cerPath); } catch { }
                try { File.Delete(pfxPath); } catch { }

                // Create INF file for certreq
                string inf = @"[Version]
Signature=""$Windows NT$""

[NewRequest]
Subject = ""CN=Win7VirtualMonitor""
KeySpec = 1
KeyLength = 2048
Exportable = TRUE
MachineKeySet = FALSE
SMIME = FALSE
PrivateKeyArchive = FALSE
UserProtected = FALSE
UseExistingKeySet = FALSE
ProviderName = ""Microsoft RSA SChannel Cryptographic Provider""
ProviderType = 12
RequestType = Cert
KeyUsage = 0xa0
";
                File.WriteAllText(infPath, inf);

                // Run certreq to create certificate
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "certreq.exe";
                psi.Arguments = String.Format("-new \"{0}\" \"{1}\"", infPath, cerPath);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                log("Trying certreq...");
                Process proc = Process.Start(psi);
                proc.WaitForExit(30000);

                if (File.Exists(cerPath))
                {
                    // Export from store to PFX using certutil
                    // First find the cert thumbprint
                    X509Certificate2 tempCert = new X509Certificate2(cerPath);
                    string thumbprint = tempCert.Thumbprint;

                    // Export using certutil
                    psi = new ProcessStartInfo();
                    psi.FileName = "certutil.exe";
                    psi.Arguments = String.Format("-user -exportpfx -p \"{0}\" My {1} \"{2}\"", 
                        CERT_PASSWORD, thumbprint, pfxPath);
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;

                    proc = Process.Start(psi);
                    proc.WaitForExit(30000);

                    if (File.Exists(pfxPath))
                    {
                        X509Certificate2 cert = new X509Certificate2(pfxPath, CERT_PASSWORD,
                            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
                        
                        if (cert.HasPrivateKey)
                        {
                            File.Copy(pfxPath, CERT_FILE_PATH, true);
                            log(String.Format("Certificate created via certreq: HasPrivateKey={0}", cert.HasPrivateKey));
                            
                            // Cleanup
                            try { File.Delete(infPath); } catch { }
                            try { File.Delete(cerPath); } catch { }
                            try { File.Delete(pfxPath); } catch { }
                            
                            return cert;
                        }
                    }
                }
                
                // Cleanup
                try { File.Delete(infPath); } catch { }
                try { File.Delete(cerPath); } catch { }
                
                log("certreq did not create usable certificate.");
            }
            catch (Exception ex)
            {
                log(String.Format("certreq failed: {0}", ex.Message));
            }
            return null;
        }

        private static X509Certificate2 CreateCertificateWithMakeCert(Action<string> log)
        {
            try
            {
                string makecertPath = FindExecutable("makecert.exe");
                string pvk2pfxPath = FindExecutable("pvk2pfx.exe");

                if (string.IsNullOrEmpty(makecertPath))
                {
                    log("makecert.exe not found, trying certreq...");
                    return CreateCertificateWithCertreq(log);
                }

                string tempDir = Path.GetTempPath();
                string certFile = Path.Combine(tempDir, "win7vm.cer");
                string pvkFile = Path.Combine(tempDir, "win7vm.pvk");
                string pfxFile = Path.Combine(tempDir, "win7vm.pfx");

                // Cleanup old files
                try { File.Delete(certFile); } catch { }
                try { File.Delete(pvkFile); } catch { }
                try { File.Delete(pfxFile); } catch { }

                // Create certificate dengan makecert
                string makecertArgs = String.Format(
                    "-r -pe -n \"{0}\" -ss my -sr currentuser -sky exchange -a sha256 -len 2048 " +
                    "-cy end -eku 1.3.6.1.5.5.7.3.1 -sv \"{1}\" \"{2}\"",
                    CERT_SUBJECT, pvkFile, certFile);

                log("Running makecert...");
                RunProcess(makecertPath, makecertArgs, log);

                if (File.Exists(certFile) && File.Exists(pvkFile))
                {
                    // Convert ke PFX
                    if (!string.IsNullOrEmpty(pvk2pfxPath))
                    {
                        string pvk2pfxArgs = String.Format(
                            "-pvk \"{0}\" -spc \"{1}\" -pfx \"{2}\" -po \"{3}\"",
                            pvkFile, certFile, pfxFile, CERT_PASSWORD);
                        
                        RunProcess(pvk2pfxPath, pvk2pfxArgs, log);

                        if (File.Exists(pfxFile))
                        {
                            X509Certificate2 cert = new X509Certificate2(pfxFile, CERT_PASSWORD,
                                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                            File.Copy(pfxFile, CERT_FILE_PATH, true);
                            log("Certificate created via makecert.");
                            
                            // Cleanup
                            try { File.Delete(certFile); } catch { }
                            try { File.Delete(pvkFile); } catch { }
                            try { File.Delete(pfxFile); } catch { }
                            
                            return cert;
                        }
                    }
                }
                
                log("makecert failed to create certificate.");
            }
            catch (Exception ex)
            {
                log(String.Format("makecert error: {0}", ex.Message));
            }
            return null;
        }

        private static string FindExecutable(string name)
        {
            string[] searchPaths = new string[]
            {
                @"C:\Program Files (x86)\Windows Kits\10\bin\x86",
                @"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x86",
                @"C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x86",
                @"C:\Program Files (x86)\Windows Kits\8.1\bin\x86",
                @"C:\Program Files (x86)\Microsoft SDKs\Windows\v7.1A\Bin",
                @"C:\Program Files\Microsoft SDKs\Windows\v7.1\Bin"
            };

            foreach (string dir in searchPaths)
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static void RunProcess(string exe, string args, Action<string> log)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                Process proc = Process.Start(psi);
                proc.WaitForExit(30000);
            }
            catch (Exception ex)
            {
                log(String.Format("Process error: {0}", ex.Message));
            }
        }

        public static X509Certificate2 GetCertificate()
        {
            if (File.Exists(CERT_FILE_PATH))
            {
                try
                {
                    return new X509Certificate2(CERT_FILE_PATH, CERT_PASSWORD,
                        X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                }
                catch { }
            }
            return null;
        }
    }
}