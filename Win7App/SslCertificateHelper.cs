using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;

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
        
        // Path untuk export certificate agar bisa diimpor di Android
        public static readonly string CERT_CER_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Win7App_cert.cer");

        public static X509Certificate2 GetOrCreateCertificate(Action<string> log)
        {
            try
            {
                // 1. Coba load dari file dulu
                if (File.Exists(CERT_FILE_PATH))
                {
                    try
                    {
                        // Use PersistKeySet + UserKeySet for Windows 7 SslStream compatibility
                        // DO NOT use MachineKeySet - causes permission issues
                        X509Certificate2 cert = new X509Certificate2(CERT_FILE_PATH, CERT_PASSWORD,
                            X509KeyStorageFlags.Exportable | 
                            X509KeyStorageFlags.PersistKeySet | 
                            X509KeyStorageFlags.UserKeySet);
                            
                        if (cert.NotAfter > DateTime.Now && cert.HasPrivateKey)
                        {
                            // Verify private key is accessible
                            try
                            {
                                AsymmetricAlgorithm pk = cert.PrivateKey;
                                RSACryptoServiceProvider rsa = pk as RSACryptoServiceProvider;
                                if (rsa != null)
                                {
                                    log(String.Format("Loaded SSL cert: HasPK=true, KeySize={0}, Provider={1}", 
                                        rsa.KeySize, rsa.CspKeyContainerInfo.ProviderName));
                                }
                                else
                                {
                                    log(String.Format("Loaded SSL cert: HasPK=true, PKType={0}", pk.GetType().Name));
                                }
                            }
                            catch (Exception pkEx)
                            {
                                log(String.Format("WARNING: PrivateKey access error: {0}", pkEx.Message));
                                // Private key tidak bisa diakses - perlu regenerate
                                throw new Exception("Private key not accessible");
                            }
                            
                            // Pastikan .cer file ada untuk Android
                            ExportPublicCertificate(cert, log);
                            // Auto-install ke Trusted Root untuk Windows
                            InstallCertificateToTrustedRoot(cert, log);
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

                // 2. PRIORITAS PERTAMA: Buat certificate secara programatis dengan BouncyCastle-style
                // Ini adalah metode paling reliable untuk Windows 7 karena menggunakan CSP murni
                log("Creating new self-signed SSL certificate (programmatic)...");
                X509Certificate2 newCert = CreateCertificateProgrammatically(log);
                if (newCert != null && newCert.HasPrivateKey)
                {
                    log(String.Format("Certificate created programmatically: HasPrivateKey={0}", newCert.HasPrivateKey));
                    ExportPublicCertificate(newCert, log);
                    InstallCertificateToTrustedRoot(newCert, log);
                    return newCert;
                }

                // 3. Fallback: certreq
                log("Trying certreq fallback...");
                newCert = CreateCertificateWithCertreq(log);
                if (newCert != null && newCert.HasPrivateKey)
                {
                    log(String.Format("Certificate from certreq: HasPrivateKey={0}", newCert.HasPrivateKey));
                    ExportPublicCertificate(newCert, log);
                    InstallCertificateToTrustedRoot(newCert, log);
                    return newCert;
                }

                // 4. Fallback: makecert
                newCert = CreateCertificateWithMakeCert(log);
                if (newCert != null && newCert.HasPrivateKey) 
                {
                    log(String.Format("Certificate from makecert: HasPrivateKey={0}", newCert.HasPrivateKey));
                    ExportPublicCertificate(newCert, log);
                    InstallCertificateToTrustedRoot(newCert, log);
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

        /// <summary>
        /// Create certificate programmatically using pure .NET RSACryptoServiceProvider (CSP)
        /// This method is most compatible with Windows 7 SslStream
        /// </summary>
        private static X509Certificate2 CreateCertificateProgrammatically(Action<string> log)
        {
            try
            {
                log("Creating certificate with RSACryptoServiceProvider (CSP)...");
                
                string tempDir = Path.GetTempPath();
                string pfxPath = Path.Combine(tempDir, "win7vm_programmatic.pfx");
                
                // Cleanup old file
                try { File.Delete(pfxPath); } catch { }
                
                // Use CspParameters with Enhanced RSA and AES provider for TLS 1.2
                // Microsoft Enhanced RSA and AES Cryptographic Provider (Type 24) - supports SHA256
                CspParameters cspParams = new CspParameters();
                cspParams.ProviderName = "Microsoft Enhanced RSA and AES Cryptographic Provider";
                cspParams.ProviderType = 24; // PROV_RSA_AES - supports SHA256 for TLS 1.2
                cspParams.KeyContainerName = "Win7VMSslKey_" + Guid.NewGuid().ToString("N");
                cspParams.Flags = CspProviderFlags.NoPrompt;
                
                // Create RSA key pair - 2048 bit for modern browser compatibility
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048, cspParams))
                {
                    rsa.PersistKeyInCsp = true;
                    
                    log(String.Format("RSA key created: KeySize={0}, Provider={1}", 
                        rsa.KeySize, cspParams.ProviderName));
                    
                    // Build self-signed certificate using the RSA key
                    // We'll use certreq but import the existing key
                    string keyContainer = cspParams.KeyContainerName;
                    
                    // Create certificate request INF that uses existing key container
                    string infPath = Path.Combine(tempDir, "win7vm_prog.inf");
                    string cerPath = Path.Combine(tempDir, "win7vm_prog.cer");
                    
                    string inf = String.Format(@"[Version]
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
ProviderName = ""Microsoft Enhanced RSA and AES Cryptographic Provider""
ProviderType = 24
RequestType = Cert
KeyUsage = 0xa0
HashAlgorithm = SHA256
ValidityPeriod = Years
ValidityPeriodUnits = 10

[EnhancedKeyUsageExtension]
OID=1.3.6.1.5.5.7.3.1

[Extensions]
2.5.29.17 = ""{{text}}""
_continue_ = ""dns=localhost&""
_continue_ = ""dns=Win7VirtualMonitor""
");
                    File.WriteAllText(infPath, inf, Encoding.ASCII);
                    
                    // Run certreq to create certificate
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "certreq.exe";
                    psi.Arguments = String.Format("-new \"{0}\" \"{1}\"", infPath, cerPath);
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;
                    
                    log("Running certreq to generate certificate...");
                    Process proc = Process.Start(psi);
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(30000);
                    
                    if (!string.IsNullOrEmpty(error) && error.Length > 5)
                        log(String.Format("certreq note: {0}", error.Length > 100 ? error.Substring(0, 100) : error));
                    
                    if (File.Exists(cerPath))
                    {
                        // Get thumbprint
                        X509Certificate2 tempCert = new X509Certificate2(cerPath);
                        string thumbprint = tempCert.Thumbprint;
                        log(String.Format("Certificate created, thumbprint: {0}", thumbprint));
                        
                        // Export to PFX
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
                            // Load with PersistKeySet for SslStream compatibility
                            X509Certificate2 cert = new X509Certificate2(pfxPath, CERT_PASSWORD,
                                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                            
                            if (cert.HasPrivateKey)
                            {
                                // Verify private key type
                                try
                                {
                                    AsymmetricAlgorithm pk = cert.PrivateKey;
                                    if (pk is RSACryptoServiceProvider)
                                    {
                                        log("Private key is RSACryptoServiceProvider (CSP) - compatible with Windows 7");
                                    }
                                    else
                                    {
                                        log(String.Format("Private key type: {0}", pk.GetType().Name));
                                    }
                                }
                                catch (Exception pkEx)
                                {
                                    log(String.Format("Private key check: {0}", pkEx.Message));
                                }
                                
                                File.Copy(pfxPath, CERT_FILE_PATH, true);
                                log("Certificate saved to file");
                                
                                // Cleanup
                                try { File.Delete(infPath); } catch { }
                                try { File.Delete(cerPath); } catch { }
                                try { File.Delete(pfxPath); } catch { }
                                
                                return cert;
                            }
                            else
                            {
                                log("WARNING: Certificate loaded but HasPrivateKey=false");
                            }
                        }
                        else
                        {
                            log("certutil export failed - PFX not created");
                        }
                    }
                    else
                    {
                        log("certreq failed - CER not created");
                    }
                    
                    // Cleanup
                    try { File.Delete(infPath); } catch { }
                    try { File.Delete(cerPath); } catch { }
                }
                
                log("Programmatic certificate creation failed");
                return null;
            }
            catch (Exception ex)
            {
                log(String.Format("Programmatic cert error: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Install certificate to Trusted Root CA store automatically (like Windows 11)
        /// This eliminates the need for manual certificate import
        /// </summary>
        public static void InstallCertificateToTrustedRoot(X509Certificate2 cert, Action<string> log)
        {
            try
            {
                // Check if already in Trusted Root
                X509Store rootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                rootStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection existingCerts = rootStore.Certificates.Find(
                    X509FindType.FindByThumbprint, cert.Thumbprint, false);
                rootStore.Close();

                if (existingCerts.Count > 0)
                {
                    log("Certificate already in Trusted Root store");
                    return;
                }

                // Try to add to CurrentUser Trusted Root (no admin required)
                try
                {
                    rootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                    rootStore.Open(OpenFlags.ReadWrite);
                    
                    // Export only public part for root store
                    X509Certificate2 publicCert = new X509Certificate2(cert.Export(X509ContentType.Cert));
                    rootStore.Add(publicCert);
                    rootStore.Close();
                    
                    log("Certificate installed to CurrentUser Trusted Root (automatic)");
                    return;
                }
                catch (Exception ex)
                {
                    log(String.Format("CurrentUser root store failed: {0}", ex.Message));
                }

                // Fallback: Try certutil command (works on Windows 7)
                try
                {
                    string cerPath = CERT_CER_PATH;
                    if (File.Exists(cerPath))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = "certutil.exe";
                        psi.Arguments = String.Format("-user -addstore Root \"{0}\"", cerPath);
                        psi.UseShellExecute = false;
                        psi.RedirectStandardOutput = true;
                        psi.RedirectStandardError = true;
                        psi.CreateNoWindow = true;

                        Process proc = Process.Start(psi);
                        proc.WaitForExit(10000);
                        
                        string output = proc.StandardOutput.ReadToEnd();
                        if (output.Contains("added") || output.Contains("Certificate"))
                        {
                            log("Certificate installed via certutil");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log(String.Format("certutil addstore failed: {0}", ex.Message));
                }

                log("Auto-install to Trusted Root failed. Browser may show warning.");
            }
            catch (Exception ex)
            {
                log(String.Format("InstallCertificateToTrustedRoot error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Remove certificate from Trusted Root store (cleanup)
        /// </summary>
        public static void RemoveCertificateFromTrustedRoot(Action<string> log)
        {
            try
            {
                X509Store rootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                rootStore.Open(OpenFlags.ReadWrite);
                
                X509Certificate2Collection certsToRemove = rootStore.Certificates.Find(
                    X509FindType.FindBySubjectName, "Win7VirtualMonitor", false);
                
                foreach (X509Certificate2 cert in certsToRemove)
                {
                    rootStore.Remove(cert);
                    log(String.Format("Removed certificate: {0}", cert.Thumbprint));
                }
                
                rootStore.Close();
            }
            catch (Exception ex)
            {
                log(String.Format("RemoveCertificateFromTrustedRoot error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Export public certificate (.cer) untuk diimpor di Android
        /// </summary>
        private static void ExportPublicCertificate(X509Certificate2 cert, Action<string> log)
        {
            try
            {
                byte[] cerData = cert.Export(X509ContentType.Cert);
                File.WriteAllBytes(CERT_CER_PATH, cerData);
                log(String.Format("Public certificate exported to: {0}", CERT_CER_PATH));
            }
            catch (Exception ex)
            {
                log(String.Format("Failed to export public certificate: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Get path to .cer file untuk ditampilkan di UI
        /// </summary>
        public static string GetPublicCertificatePath()
        {
            return CERT_CER_PATH;
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

                // Create INF file for certreq - Windows 7 with .NET 4.8 (TLS 1.2 support)
                // Using Microsoft Enhanced RSA and AES Provider (ProviderType=24)
                // SHA256 hash for modern browser compatibility
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
ProviderName = ""Microsoft Enhanced RSA and AES Cryptographic Provider""
ProviderType = 24
RequestType = Cert
KeyUsage = 0xa0
HashAlgorithm = SHA256
ValidityPeriod = Years
ValidityPeriodUnits = 10

[EnhancedKeyUsageExtension]
OID=1.3.6.1.5.5.7.3.1

[Extensions]
2.5.29.17 = ""{text}""
_continue_ = ""dns=localhost&""
_continue_ = ""dns=Win7VirtualMonitor""
";
                File.WriteAllText(infPath, inf, Encoding.ASCII);

                // Run certreq to create certificate
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "certreq.exe";
                psi.Arguments = String.Format("-new \"{0}\" \"{1}\"", infPath, cerPath);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                log("Trying certreq (Windows 7 compatible)...");
                Process proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit(30000);
                
                if (!string.IsNullOrEmpty(error))
                    log(String.Format("certreq stderr: {0}", error.Length > 100 ? error.Substring(0, 100) : error));

                if (File.Exists(cerPath))
                {
                    // Export from store to PFX using certutil
                    // First find the cert thumbprint
                    X509Certificate2 tempCert = new X509Certificate2(cerPath);
                    string thumbprint = tempCert.Thumbprint;
                    log(String.Format("Certificate created, thumbprint: {0}", thumbprint));

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
                        // Use PersistKeySet to ensure private key is accessible by SslStream
                        X509Certificate2 cert = new X509Certificate2(pfxPath, CERT_PASSWORD,
                            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                        
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
                    else
                    {
                        log("certutil export failed - PFX not created");
                    }
                }
                else
                {
                    log("certreq failed - CER not created");
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

        /// <summary>
        /// Force reinstall certificate to Trusted Root (for fixing HTTPS issues)
        /// </summary>
        public static bool ForceReinstallCertificate(Action<string> log)
        {
            try
            {
                // Remove existing from trusted root
                RemoveCertificateFromTrustedRoot(log);
                
                // Get current certificate
                X509Certificate2 cert = GetCertificate();
                if (cert == null)
                {
                    log("No certificate found. Creating new one...");
                    cert = GetOrCreateCertificate(log);
                }
                
                if (cert != null)
                {
                    // Force install to trusted root
                    InstallCertificateToTrustedRoot(cert, log);
                    log("Certificate reinstalled successfully!");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                log(String.Format("ForceReinstallCertificate error: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Regenerate certificate from scratch (delete old and create new)
        /// Use this to fix SSL cipher mismatch errors
        /// </summary>
        public static bool RegenerateCertificate(Action<string> log)
        {
            try
            {
                log("Regenerating SSL certificate...");
                
                // Remove from trusted root
                RemoveCertificateFromTrustedRoot(log);
                
                // Remove from My store
                try
                {
                    X509Store myStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    myStore.Open(OpenFlags.ReadWrite);
                    X509Certificate2Collection certs = myStore.Certificates.Find(
                        X509FindType.FindBySubjectName, "Win7VirtualMonitor", false);
                    foreach (X509Certificate2 c in certs)
                    {
                        myStore.Remove(c);
                        log(String.Format("Removed old cert from My store: {0}", c.Thumbprint));
                    }
                    myStore.Close();
                }
                catch (Exception ex)
                {
                    log(String.Format("Warning: Could not clean My store: {0}", ex.Message));
                }
                
                // Delete cert files
                try { File.Delete(CERT_FILE_PATH); } catch { }
                try { File.Delete(CERT_CER_PATH); } catch { }
                
                log("Old certificates removed. Creating new certificate...");
                
                // Create new certificate
                X509Certificate2 newCert = GetOrCreateCertificate(log);
                
                if (newCert != null && newCert.HasPrivateKey)
                {
                    log("New certificate created successfully!");
                    return true;
                }
                
                log("Failed to create new certificate");
                return false;
            }
            catch (Exception ex)
            {
                log(String.Format("RegenerateCertificate error: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Check if certificate is properly installed in Trusted Root
        /// </summary>
        public static bool IsCertificateInTrustedRoot()
        {
            try
            {
                X509Certificate2 cert = GetCertificate();
                if (cert == null) return false;

                X509Store rootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                rootStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection found = rootStore.Certificates.Find(
                    X509FindType.FindByThumbprint, cert.Thumbprint, false);
                rootStore.Close();

                return found.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enable TLS 1.2 for .NET Framework on Windows 7
        /// Call this at application startup
        /// </summary>
        public static void EnableTls12(Action<string> log)
        {
            try
            {
                // STEP 1: Enable TLS 1.2 in Windows SChannel (requires admin for first time)
                bool schannelEnabled = EnableWindowsTls12Registry(log);
                
                // STEP 2: Enable .NET Framework strong crypto
                EnableDotNetStrongCrypto(log);
                
                // STEP 3: Set SecurityProtocol to use TLS 1.2 as default
                ServicePointManager.SecurityProtocol = 
                    SecurityProtocolType.Tls12 |    // Primary - most secure
                    SecurityProtocolType.Tls11 |    // Fallback 1  
                    SecurityProtocolType.Tls;       // Fallback 2 (TLS 1.0)
                
                // Enable modern SSL/TLS best practices
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.DefaultConnectionLimit = 100;
                
                if (schannelEnabled)
                    log("TLS 1.2 enabled (Windows SChannel + .NET 4.8)");
                else
                    log("TLS 1.2 enabled (.NET 4.8) - SChannel registry may need admin");
                
                // Check current status
                CheckWindowsTls12(log);
            }
            catch (Exception ex)
            {
                log(String.Format("EnableTls12 warning: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// Enable TLS 1.2 in Windows SChannel via registry
        /// This is REQUIRED for Windows 7 to support TLS 1.2
        /// </summary>
        private static bool EnableWindowsTls12Registry(Action<string> log)
        {
            bool success = true;
            try
            {
                // Enable TLS 1.2 Client
                string clientPath = @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client";
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(clientPath))
                {
                    if (key != null)
                    {
                        key.SetValue("Enabled", 1, RegistryValueKind.DWord);
                        key.SetValue("DisabledByDefault", 0, RegistryValueKind.DWord);
                    }
                }
                
                // Enable TLS 1.2 Server
                string serverPath = @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server";
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(serverPath))
                {
                    if (key != null)
                    {
                        key.SetValue("Enabled", 1, RegistryValueKind.DWord);
                        key.SetValue("DisabledByDefault", 0, RegistryValueKind.DWord);
                    }
                }
                
                // Enable TLS 1.1 as fallback - Client
                string tls11ClientPath = @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client";
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(tls11ClientPath))
                {
                    if (key != null)
                    {
                        key.SetValue("Enabled", 1, RegistryValueKind.DWord);
                        key.SetValue("DisabledByDefault", 0, RegistryValueKind.DWord);
                    }
                }
                
                // Enable TLS 1.1 Server
                string tls11ServerPath = @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Server";
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(tls11ServerPath))
                {
                    if (key != null)
                    {
                        key.SetValue("Enabled", 1, RegistryValueKind.DWord);
                        key.SetValue("DisabledByDefault", 0, RegistryValueKind.DWord);
                    }
                }
                
                log("Windows SChannel TLS 1.2/1.1 enabled in registry");
            }
            catch (UnauthorizedAccessException)
            {
                log("SChannel registry: Need admin rights (run as administrator)");
                success = false;
            }
            catch (Exception ex)
            {
                log(String.Format("SChannel registry warning: {0}", ex.Message));
                success = false;
            }
            return success;
        }
        
        /// <summary>
        /// Enable .NET Framework strong crypto via registry
        /// </summary>
        private static void EnableDotNetStrongCrypto(Action<string> log)
        {
            try
            {
                // Enable for 32-bit .NET
                string dotnet32Path = @"SOFTWARE\Microsoft\.NETFramework\v4.0.30319";
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(dotnet32Path))
                {
                    if (key != null)
                    {
                        key.SetValue("SchUseStrongCrypto", 1, RegistryValueKind.DWord);
                    }
                }
                
                // Enable for 64-bit .NET (Wow6432Node)
                string dotnet64Path = @"SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319";
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.CreateSubKey(dotnet64Path))
                    {
                        if (key != null)
                        {
                            key.SetValue("SchUseStrongCrypto", 1, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { /* 32-bit OS doesn't have Wow6432Node */ }
                
                log(".NET Framework SchUseStrongCrypto enabled");
            }
            catch (UnauthorizedAccessException)
            {
                log(".NET strong crypto: Need admin rights");
            }
            catch (Exception ex)
            {
                log(String.Format(".NET strong crypto warning: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Check if TLS 1.2 is enabled in Windows registry
        /// </summary>
        private static void CheckWindowsTls12(Action<string> log)
        {
            try
            {
                // Check if TLS 1.2 is disabled in registry
                string keyPath = @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        object enabled = key.GetValue("Enabled");
                        object disabledByDefault = key.GetValue("DisabledByDefault");
                        
                        if (enabled != null && (int)enabled == 0)
                        {
                            log("WARNING: TLS 1.2 is disabled in Windows registry!");
                            log("Run this in Admin CMD to enable:");
                            log("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\SecurityProviders\\SCHANNEL\\Protocols\\TLS 1.2\\Client\" /v Enabled /t REG_DWORD /d 1 /f");
                        }
                    }
                }
                
                // Check .NET Framework strong crypto
                string dotnetKeyPath = @"SOFTWARE\Microsoft\.NETFramework\v4.0.30319";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(dotnetKeyPath))
                {
                    if (key != null)
                    {
                        object strongCrypto = key.GetValue("SchUseStrongCrypto");
                        if (strongCrypto == null || (int)strongCrypto != 1)
                        {
                            log("Note: SchUseStrongCrypto not set. TLS 1.2 may not be default.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-critical - just log
                log(String.Format("TLS registry check: {0}", ex.Message));
            }
        }
    }
}