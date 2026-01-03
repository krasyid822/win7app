using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Win7App
{
    public class MjpegServer
    {
        private TcpListener _listener;
        private TcpListener _httpsListener;
        private Thread _serverThread;
        private Thread _httpsServerThread;
        private volatile bool _isRunning;
        private Screen _screenToCapture;
        private int _fps = 15;
        private long _quality = 50;
        private string _password = "";
        public string Password { get { return _password; } set { _password = value; } }
        
        private AudioCapture _audioCapture;
        private bool _audioEnabled = true;
        private bool _httpsEnabled = false;
        private int _httpPort;
        private int _httpsPort;
        private X509Certificate2 _sslCert;

        public event Action<string> LogMessage;

        public MjpegServer(int port)
        {
            _httpPort = port;
            _httpsPort = port + 1; // HTTPS on port+1 (e.g., 8081)
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start(Screen screen, string password, bool enableAudio = true, bool enableHttps = false)
        {
            if (_isRunning) return;

            _screenToCapture = screen;
            Password = password;
            _audioEnabled = enableAudio;
            _httpsEnabled = enableHttps;
            _isRunning = true;
            
            // Start HTTP
            _listener.Start();
            _serverThread = new Thread(ServerLoop);
            _serverThread.IsBackground = true;
            _serverThread.Start();

            // Start HTTPS if enabled
            if (_httpsEnabled)
            {
                try
                {
                    // Get or create SSL certificate
                    _sslCert = SslCertificateHelper.GetOrCreateCertificate(LogMessageSafe);
                    if (_sslCert != null)
                    {
                        // Log certificate info for debugging
                        if (LogMessage != null)
                        {
                            LogMessage(String.Format("SSL Cert: Subject={0}, HasPrivateKey={1}, Algo={2}",
                                _sslCert.Subject,
                                _sslCert.HasPrivateKey,
                                _sslCert.SignatureAlgorithm.FriendlyName));
                        }

                        // Inspect private key provider details to detect CNG vs CSP issues
                        try
                        {
                            var priv = _sslCert.PrivateKey;
                            if (priv != null)
                            {
                                System.Security.Cryptography.RSACryptoServiceProvider rsaCsp = priv as System.Security.Cryptography.RSACryptoServiceProvider;
                                if (rsaCsp != null)
                                {
                                    var info = rsaCsp.CspKeyContainerInfo;
                                    LogMessage(String.Format("PrivateKey: CSP Provider={0}, Container={1}, MachineKeySet={2}",
                                        info.ProviderName, info.KeyContainerName, info.MachineKeyStore));
                                }
                                else
                                {
                                    LogMessage(String.Format("PrivateKey type: {0}", priv.GetType().FullName));
                                    string tname = priv.GetType().Name;
                                    if (tname != null && (tname.Contains("Cng") || tname.Contains("NCrypt")))
                                    {
                                        LogMessage("PrivateKey is CNG - may not work on Windows 7");
                                    }
                                }
                            }
                            else
                            {
                                LogMessage("PrivateKey: null");
                            }
                        }
                        catch (Exception pe)
                        {
                            LogMessage(String.Format("PrivateKey inspect error: {0}", pe.Message));
                        }

                        if (!_sslCert.HasPrivateKey)
                        {
                            if (LogMessage != null) LogMessage("WARNING: Certificate has no private key! HTTPS will fail.");
                            if (LogMessage != null) LogMessage("Please click SSL button and choose 'NO' to regenerate certificate.");
                        }

                        _httpsListener = new TcpListener(IPAddress.Any, _httpsPort);
                        _httpsListener.Start();
                        _httpsServerThread = new Thread(HttpsServerLoop);
                        _httpsServerThread.IsBackground = true;
                        _httpsServerThread.Start();
                        if (LogMessage != null) LogMessage(String.Format("HTTPS server started on port {0}", _httpsPort));
                    }
                    else
                    {
                        if (LogMessage != null) LogMessage("HTTPS: Certificate not available, HTTPS disabled.");
                    }
                }
                catch (Exception ex)
                {
                    if (LogMessage != null) LogMessage(String.Format("HTTPS start failed: {0}", ex.Message));
                }
            }

            // Start audio capture
            if (_audioEnabled)
            {
                _audioCapture = new AudioCapture(22050, 1, 16);
                if (_audioCapture.Start())
                {
                    if (LogMessage != null) LogMessage("Audio capture started.");
                }
                else
                {
                    if (LogMessage != null) LogMessage("Audio capture failed - no device found.");
                }
            }

            if (LogMessage != null) LogMessage(String.Format("HTTP server started on port {0}", _httpPort));
        }

        private void LogMessageSafe(string msg)
        {
            if (LogMessage != null) LogMessage(msg);
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
            
            if (_httpsListener != null)
            {
                try
                {
                    _httpsListener.Stop();
                }
                catch { }
                _httpsListener = null;
            }
            
            if (_audioCapture != null)
            {
                _audioCapture.Stop();
                _audioCapture.Dispose();
                _audioCapture = null;
            }
            
            _sslCert = null;
            
            if (LogMessage != null) LogMessage("Server stopped.");
        }

        private static bool ValidateClientCertificate(object sender, X509Certificate certificate, 
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Accept all client certificates (we're not requiring client certs)
            return true;
        }

        private void ServerLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client, false));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch (SocketException)
                {
                    // Listener stopped
                }
                catch (Exception ex)
                {
                    if (LogMessage != null) LogMessage(String.Format("Error accepting client: {0}", ex.Message));
                }
            }
        }

        private void HttpsServerLoop()
        {
            while (_isRunning && _httpsListener != null)
            {
                try
                {
                    TcpClient client = _httpsListener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client, true));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch (SocketException)
                {
                    // Listener stopped
                }
                catch (Exception ex)
                {
                    if (LogMessage != null) LogMessage(String.Format("Error accepting HTTPS client: {0}", ex.Message));
                }
            }
        }

        private void HandleClient(TcpClient client, bool isHttps)
        {
            NetworkStream baseStream = null;
            Stream stream = null;
            
            try
            {
                baseStream = client.GetStream();
                stream = baseStream;
                
                // Wrap with SSL if HTTPS
                if (isHttps && _sslCert != null)
                {
                    // Peek first byte to check if it's TLS handshake
                    // TLS handshake starts with 0x16 (22)
                    byte[] peek = new byte[1];
                    int read = baseStream.Read(peek, 0, 1);
                    if (read == 0)
                    {
                        client.Close();
                        return;
                    }
                    
                    if (peek[0] != 0x16) // Not TLS - probably plain HTTP
                    {
                        // Read rest of HTTP request line
                        StringBuilder sb = new StringBuilder();
                        sb.Append((char)peek[0]);
                        
                        int b;
                        while ((b = baseStream.ReadByte()) != -1 && b != '\n')
                        {
                            sb.Append((char)b);
                            if (sb.Length > 1000) break;
                        }
                        
                        // Send redirect to HTTPS
                        string host = GetHostFromRequest(sb.ToString(), client);
                        string redirectUrl = String.Format("https://{0}:{1}/", host, _httpsPort);
                        
                        string response = String.Format(
                            "HTTP/1.1 301 Moved Permanently\r\n" +
                            "Location: {0}\r\n" +
                            "Content-Length: 0\r\n" +
                            "Connection: close\r\n\r\n",
                            redirectUrl);
                        
                        byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                        baseStream.Write(responseBytes, 0, responseBytes.Length);
                        baseStream.Flush();
                        
                        if (LogMessage != null) LogMessage(String.Format("Redirected HTTP to HTTPS: {0}", client.Client.RemoteEndPoint));
                        client.Close();
                        return;
                    }
                    
                    // It's TLS - create a stream that includes the peeked byte
                    PeekableStream peekStream = new PeekableStream(baseStream, peek[0]);
                    
                    try
                    {
                        SslStream sslStream = new SslStream(peekStream, false, 
                            new RemoteCertificateValidationCallback(ValidateClientCertificate));
                        
                        // Windows 7 with .NET 4.8 supports TLS 1.2 after enabling in registry
                        // Use TLS 1.2 as primary for modern browser compatibility
                        System.Security.Authentication.SslProtocols protocols = 
                            System.Security.Authentication.SslProtocols.Tls12 |   // TLS 1.2 - primary (modern browsers)
                            System.Security.Authentication.SslProtocols.Tls11 |   // TLS 1.1 - fallback 1
                            System.Security.Authentication.SslProtocols.Tls;      // TLS 1.0 - fallback 2
                        
                        sslStream.AuthenticateAsServer(_sslCert, false, protocols, false); // no CRL check
                        stream = sslStream;
                        
                        if (LogMessage != null) LogMessage(String.Format("HTTPS connected (TLS): {0}", client.Client.RemoteEndPoint));
                    }
                    catch (Exception sslEx)
                    {
                        // Log detailed SSL error info including stacktrace and Win32 codes
                        string errorDetail = sslEx.Message;
                        try { errorDetail += " | ExceptionFull: " + sslEx.ToString(); } catch { }
                        if (sslEx.InnerException != null)
                        {
                            try { errorDetail += " | Inner: " + sslEx.InnerException.Message; } catch { }
                            if (sslEx.InnerException.InnerException != null)
                            {
                                try { errorDetail += " | Inner2: " + sslEx.InnerException.InnerException.Message; } catch { }
                            }
                        }

                        // If inner exception is a Win32Exception, log native error code
                        try
                        {
                            var w32 = sslEx as System.ComponentModel.Win32Exception;
                            if (w32 == null && sslEx.InnerException is System.ComponentModel.Win32Exception)
                                w32 = (System.ComponentModel.Win32Exception)sslEx.InnerException;
                            if (w32 != null)
                            {
                                errorDetail += String.Format(" | Win32Error={0}", w32.NativeErrorCode);
                            }
                        }
                        catch { }

                        // Check certificate validity
                        string certInfo = "No cert";
                        if (_sslCert != null)
                        {
                            certInfo = String.Format("Cert: HasPK={0}, Algo={1}, Exp={2}", 
                                _sslCert.HasPrivateKey,
                                _sslCert.SignatureAlgorithm.FriendlyName,
                                _sslCert.NotAfter.ToString("yyyy-MM-dd"));
                        }

                        if (LogMessage != null) LogMessage(String.Format("SSL error: {0} | {1}", errorDetail, certInfo));
                        client.Close();
                        return;
                    }
                }
                else if (!isHttps)
                {
                    if (LogMessage != null) LogMessage(String.Format("HTTP client connected: {0}", client.Client.RemoteEndPoint));
                }
                
                StreamReader reader = new StreamReader(stream, Encoding.ASCII);
                StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);

                // Read HTTP Request Line
                string request = reader.ReadLine();
                if (string.IsNullOrEmpty(request)) return;

                string[] tokens = request.Split(' ');
                if (tokens.Length < 2) return;

                string method = tokens[0];
                string rawUrl = tokens[1];
                string url = rawUrl;
                string query = "";

                int qIndex = rawUrl.IndexOf('?');
                if (qIndex >= 0)
                {
                    url = rawUrl.Substring(0, qIndex);
                    query = rawUrl.Substring(qIndex + 1);
                }

                if (url == "/" || url == "/index.html")
                {
                    ServeHtml(writer);
                }
                else if (url == "/stream")
                {
                    if (CheckAuth(query))
                    {
                        ServeStream(stream, writer);
                    }
                    else
                    {
                        writer.WriteLine("HTTP/1.1 401 Unauthorized");
                        writer.WriteLine("WWW-Authenticate: Basic realm=\"Win7App\"");
                        writer.WriteLine();
                        writer.Flush();
                    }
                }
                else if (url == "/audio" || url == "/audio-chunk")
                {
                    if (CheckAuth(query))
                    {
                        ServeAudioChunk(stream, writer);
                    }
                    else
                    {
                        writer.WriteLine("HTTP/1.1 401 Unauthorized");
                        writer.WriteLine();
                        writer.Flush();
                    }
                }
                else if (url == "/touch")
                {
                    if (CheckAuth(query))
                    {
                        HandleTouchRequest(query, writer);
                    }
                    else
                    {
                        writer.WriteLine("HTTP/1.1 401 Unauthorized");
                        writer.WriteLine();
                        writer.Flush();
                    }
                }
                else if (url == "/manifest.json")
                {
                    ServeManifest(writer);
                }
                else if (url == "/sw.js")
                {
                    ServeServiceWorker(writer);
                }
                else if (url == "/offline" || url == "/offline.html")
                {
                    ServeOfflinePage(writer);
                }
                else if (url.StartsWith("/icon-"))
                {
                    ServeIcon(url, stream, writer);
                }
                else
                {
                    writer.WriteLine("HTTP/1.1 404 Not Found");
                    writer.WriteLine();
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                if (LogMessage != null) LogMessage(String.Format("Client error: {0}", ex.Message));
            }
            finally
            {
                client.Close();
                if (LogMessage != null) LogMessage("Client disconnected.");
            }
        }

        private bool CheckAuth(string query)
        {
            if (string.IsNullOrEmpty(Password)) return true;
            // Simple check: look for "auth=PASSWORD"
            return query.Contains("auth=" + Password);
        }

        private void HandleTouchRequest(string query, StreamWriter writer)
        {
            try
            {
                // Parse query: x=100&y=200&action=down|up|move&sw=1920&sh=1080
                var parms = ParseQuery(query);

                int x = int.Parse(parms["x"]);
                int y = int.Parse(parms["y"]);
                string action = parms.ContainsKey("action") ? parms["action"] : "click";
                int streamWidth = parms.ContainsKey("sw") ? int.Parse(parms["sw"]) : _screenToCapture.Bounds.Width;
                int streamHeight = parms.ContainsKey("sh") ? int.Parse(parms["sh"]) : _screenToCapture.Bounds.Height;

                // Calculate actual screen position
                int screenX = (int)((x / (double)streamWidth) * _screenToCapture.Bounds.Width);
                int screenY = (int)((y / (double)streamHeight) * _screenToCapture.Bounds.Height);

                int offsetX = _screenToCapture.Bounds.X;
                int offsetY = _screenToCapture.Bounds.Y;
                int screenW = _screenToCapture.Bounds.Width;
                int screenH = _screenToCapture.Bounds.Height;

                switch (action)
                {
                    case "down":
                        TouchInput.MoveMouse(screenX, screenY, screenW, screenH, offsetX, offsetY);
                        TouchInput.MouseDown();
                        break;
                    case "up":
                        TouchInput.MoveMouse(screenX, screenY, screenW, screenH, offsetX, offsetY);
                        TouchInput.MouseUp();
                        break;
                    case "move":
                        TouchInput.MoveMouse(screenX, screenY, screenW, screenH, offsetX, offsetY);
                        break;
                    case "click":
                        TouchInput.Click(screenX, screenY, screenW, screenH, offsetX, offsetY);
                        break;
                    case "rightclick":
                        TouchInput.RightClick(screenX, screenY, screenW, screenH, offsetX, offsetY);
                        break;
                }

                writer.WriteLine("HTTP/1.1 200 OK");
                writer.WriteLine("Content-Length: 2");
                writer.WriteLine("Access-Control-Allow-Origin: *");
                writer.WriteLine();
                writer.Write("OK");
                writer.Flush();
            }
            catch (Exception ex)
            {
                writer.WriteLine("HTTP/1.1 400 Bad Request");
                writer.WriteLine();
                writer.Write(ex.Message);
                writer.Flush();
            }
        }

        private Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query)) return result;

            foreach (var part in query.Split('&'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                {
                    result[kv[0]] = Uri.UnescapeDataString(kv[1]);
                }
            }
            return result;
        }

        private void ServeHtml(StreamWriter writer)
        {
            string html = @"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>Win7 Virtual Monitor</title>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover'>
    <meta name='description' content='Stream Windows 7 display to Android'>
    <meta name='theme-color' content='#1a1a2e' media='(prefers-color-scheme: dark)'>
    <meta name='theme-color' content='#1a1a2e' media='(prefers-color-scheme: light)'>
    <meta name='color-scheme' content='dark'>
    <meta name='apple-mobile-web-app-capable' content='yes'>
    <meta name='apple-mobile-web-app-status-bar-style' content='black-translucent'>
    <meta name='apple-mobile-web-app-title' content='Win7VM'>
    <meta name='mobile-web-app-capable' content='yes'>
    <meta name='application-name' content='Win7VM'>
    <meta name='msapplication-TileColor' content='#1a1a2e'>
    <meta name='msapplication-TileImage' content='/icon-144.png'>
    <meta name='format-detection' content='telephone=no'>
    <link rel='manifest' href='/manifest.json' crossorigin='use-credentials'>
    <link rel='apple-touch-icon' sizes='180x180' href='/icon-192.png'>
    <link rel='apple-touch-startup-image' href='/icon-512.png'>
    <link rel='icon' type='image/png' sizes='32x32' href='/icon-48.png'>
    <link rel='icon' type='image/png' sizes='192x192' href='/icon-192.png'>
    <link rel='icon' type='image/png' sizes='512x512' href='/icon-512.png'>
    <style>
        body, html { margin: 0; padding: 0; width: 100%; height: 100%; background: black; overflow: hidden; font-family: sans-serif; }
        #container { 
            width: 100%; height: 100%; 
            box-sizing: border-box; 
            display: none;
            justify-content: center; 
            align-items: center;
            padding: 0px;
        }
        #stream { 
            max-width: 100%; max-height: 100%; 
            width: auto; height: auto; 
            object-fit: contain; 
        }
        #controls {
            position: absolute; top: 20px; left: 20px;
            background: rgba(0, 0, 0, 0.85); color: white;
            padding: 15px; border-radius: 8px;
            display: none; z-index: 1000;
            max-height: 80vh; overflow-y: auto;
        }
        #login {
            position: absolute; top: 0; left: 0; width: 100%; height: 100%;
            background: #222; color: white;
            display: flex; flex-direction: column;
            justify-content: center; align-items: center;
            z-index: 2000;
        }
        .btn { 
            background: #555; color: white; border: 1px solid #777; 
            padding: 8px 15px; margin: 5px; font-size: 16px; cursor: pointer; 
        }
        .btn:active { background: #777; }
        input { padding: 8px; font-size: 16px; margin: 5px; }
        #floatBtn {
            position: fixed;
            width: 40px; height: 40px;
            border-radius: 50%;
            background: rgba(80, 80, 80, 0.4);
            border: 1px solid rgba(255,255,255,0.2);
            color: rgba(255,255,255,0.6);
            font-size: 18px;
            display: none;
            justify-content: center;
            align-items: center;
            z-index: 999;
            cursor: pointer;
            touch-action: none;
            user-select: none;
            transition: background 0.2s, color 0.2s;
        }
        #floatBtn:active { background: rgba(100, 100, 100, 0.7); color: white; }
    </style>
</head>
<body>
    <div id='login'>
        <h2>Authentication Required</h2>
        <input type='password' id='passInput' placeholder='Enter Password' />
        <button class='btn' onclick='doLogin()'>Connect</button>
    </div>

    <div id='container'>
        <img id='stream' />
        <audio id='audioPlayer' preload='none' playsinline></audio>
    </div>

    <div id='floatBtn'>⚙</div>

    <div id='controls'>
        <div style='display:flex;justify-content:space-between;align-items:center;margin-bottom:5px'>
            <div>
                <h3 style='margin:0'>Settings</h3>
                <p style='font-size:11px;color:#888;margin:2px 0 0 0'>Drag the ? button to move it</p>
            </div>
            <button onclick='toggleControls()' style='background:#d33;color:white;border:none;width:32px;height:32px;border-radius:50%;font-size:18px;cursor:pointer;font-weight:bold'>&times;</button>
        </div>
        <hr style='border-color:#444'>
        <h4>Fullscreen</h4>
        <button class='btn' id='btnFullscreen' onclick='toggleFullscreen()'>Enter Fullscreen</button>
        <p id='fullscreenStatus'>Mode: Normal</p>
        <hr style='border-color:#444'>
        <h4>Safe Area</h4>
        <div>
            <button class='btn' onclick='adjust(5)'>Shrink (-)</button>
            <button class='btn' onclick='adjust(-5)'>Expand (+)</button>
        </div>
        <p>Current Padding: <span id='val'>0</span>px</p>
        <hr style='border-color:#444'>
        <h4>Stay Awake</h4>
        <button class='btn' id='btnWake' onclick='toggleWakeLock()'>Enable Stay Awake</button>
        <p id='wakeStatus'>Screen: Normal</p>
        <hr style='border-color:#444'>
        <h4>Audio</h4>
        <button class='btn' id='btnAudio' onclick='toggleAudio()'>Enable Audio</button>
        <p id='audioStatus'>Audio: Off</p>
        <hr style='border-color:#444'>
        <h4>Install App</h4>
        <button class='btn' id='btnInstall' onclick='installPWA()' style='display:none'>Add to Home Screen</button>
        <p id='pwaStatus'>PWA: Checking...</p>
    </div>

    <script>
        // Fullscreen toggle
        function toggleFullscreen() {
            var btn = document.getElementById('btnFullscreen');
            var status = document.getElementById('fullscreenStatus');
            
            if (!document.fullscreenElement && !document.webkitFullscreenElement) {
                var elem = document.documentElement;
                if (elem.requestFullscreen) {
                    elem.requestFullscreen();
                } else if (elem.webkitRequestFullscreen) {
                    elem.webkitRequestFullscreen();
                }
                btn.innerText = 'Exit Fullscreen';
                status.innerText = 'Mode: Fullscreen';
            } else {
                if (document.exitFullscreen) {
                    document.exitFullscreen();
                } else if (document.webkitExitFullscreen) {
                    document.webkitExitFullscreen();
                }
                btn.innerText = 'Enter Fullscreen';
                status.innerText = 'Mode: Normal';
            }
        }
        
        // Update fullscreen button on change
        document.addEventListener('fullscreenchange', function() {
            var btn = document.getElementById('btnFullscreen');
            var status = document.getElementById('fullscreenStatus');
            if (document.fullscreenElement) {
                btn.innerText = 'Exit Fullscreen';
                status.innerText = 'Mode: Fullscreen';
            } else {
                btn.innerText = 'Enter Fullscreen';
                status.innerText = 'Mode: Normal';
            }
        });
        document.addEventListener('webkitfullscreenchange', function() {
            var btn = document.getElementById('btnFullscreen');
            var status = document.getElementById('fullscreenStatus');
            if (document.webkitFullscreenElement) {
                btn.innerText = 'Exit Fullscreen';
                status.innerText = 'Mode: Fullscreen';
            } else {
                btn.innerText = 'Enter Fullscreen';
                status.innerText = 'Mode: Normal';
            }
        });

        // Wake Lock for Stay Awake - works on HTTP and HTTPS
        var wakeLock = null;
        var wakeLockEnabled = false;
        var noSleepVideo = null;
        var noSleepInterval = null;
        
        // Create NoSleep video element
        function createNoSleepVideo() {
            var video = document.createElement('video');
            video.setAttribute('playsinline', '');
            video.setAttribute('webkit-playsinline', '');
            video.setAttribute('muted', '');
            video.muted = true;
            video.loop = true;
            video.style.cssText = 'position:fixed;left:-100px;top:-100px;width:1px;height:1px;';
            
            // WebM video that works better on Android
            var webm = 'data:video/webm;base64,GkXfo59ChoEBQveBAULygQRC84EIQoKEd2VibUKHgQRChYECGFOAZwH/////////FUmpZpkq17GDD0JATYCGQ2hyb21lV0GGQ2hyb21lFlSua7+uvdeBAXPFh7Z2aWR0aGV1AZJUsyuBACK1nIN1bmR1oXZpc29yQZJESTTJiEAASU1YQYCAVYBUA4BAfwEVQ7Z1c2VkgQKGcXVpdAVBc3R3YXZlbnRkbmRzAAA=';
            // MP4 fallback
            var mp4 = 'data:video/mp4;base64,AAAAIGZ0eXBtcDQyAAAAAG1wNDJpc29tYXZjMQAAAc1tb292AAAAbG12aGQAAAAA1NIHqtTSB6oAAAPwAAACrgABAAABAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAACN3RyYWsAAABcdGtoZAAAAAHU0geq1NIHqgAAAAEAAAAAAAKuAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAAgAAAAIAAAAAACRlZHRzAAAAHGVsc3QAAAAAAAAAAQAAAq4AAAAAAAEAAAAAAa9tZGlhAAAAIG1kaGQAAAAA1NIHqtTSB6oAAAAeAAAAHgVXAAAAAAAtaGRscgAAAAAAAAAAdmlkZQAAAAAAAAAAAAAAAFZpZGVvSGFuZGxlcgAAAAFabWluZgAAABR2bWhkAAAAAQAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAAAAAAAAABAAAADHVybCAAAAABAAAAGnN0YmwAAACuc3RzZAAAAAAAAAABAAAAnmF2YzEAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAACAAIASAAAAEgAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABj//wAAADRhdmNDAWQACv/hABdnZAAKrNlBsJaEAAADAAQAAAMACg8WLZYBAAVo6+PLIsAAAAAYc3R0cwAAAAAAAAABAAAADwAAB9AAAAAUCHN0c3MAAAAAAAAAAQAAAAEAAABIY3R0cwAAAAAAAAAPAAAAAQAAD6ABAAAAFHNkdHAAAAAAIBAQGBAAAAAcc3RzYwAAAAAAAAABAAAAAQAAAA8AAAABAAAARHNaegAAAAAAAAAAAAAPAAADMgAAAAsAAAALAAAACwAAAAsAAAALAAAACwAAAAsAAAALAAAACwAAAAsAAAALAAAACwAAAAsAAAAUc3RjbwAAAAAAAAABAAAAMAAAAGJ1ZHRhAAAAWm1ldGEAAAAAAAAAIWhkbHIAAAAAAAAAAG1kaXJhcHBsAAAAAAAAAAAAAAAALGlsc3QAAAAkqXRvbwAAABxkYXRhAAAAAQAAAABMYXZmNTguNzYuMTAw';
            
            // Try WebM first (better for Android), fallback to MP4
            video.src = webm;
            video.addEventListener('error', function() {
                video.src = mp4;
            });
            
            document.body.appendChild(video);
            return video;
        }
        
        async function toggleWakeLock() {
            var btn = document.getElementById('btnWake');
            var status = document.getElementById('wakeStatus');
            
            if (!wakeLockEnabled) {
                var success = false;
                
                // Method 1: Try native Wake Lock API (HTTPS only)
                if ('wakeLock' in navigator && location.protocol === 'https:') {
                    try {
                        wakeLock = await navigator.wakeLock.request('screen');
                        wakeLock.addEventListener('release', function() {
                            if (wakeLockEnabled) {
                                // Try to re-acquire
                                toggleWakeLock().then(function(){}).catch(function(){});
                            }
                        });
                        success = true;
                        status.innerText = 'Stay Awake: ON';
                        console.log('[WakeLock] Native API active');
                    } catch(e) {
                        console.log('[WakeLock] Native API failed:', e.message);
                    }
                }
                
                // Method 2: Video trick (works on HTTP)
                if (!success) {
                    try {
                        if (!noSleepVideo) {
                            noSleepVideo = createNoSleepVideo();
                        }
                        
                        // Play video
                        var playPromise = noSleepVideo.play();
                        if (playPromise !== undefined) {
                            await playPromise;
                        }
                        
                        // Keep playing with interval (some browsers pause hidden videos)
                        noSleepInterval = setInterval(function() {
                            if (noSleepVideo && noSleepVideo.paused && wakeLockEnabled) {
                                noSleepVideo.play().catch(function(){});
                            }
                        }, 15000);
                        
                        success = true;
                        status.innerText = 'Stay Awake: ON (video)';
                        console.log('[WakeLock] Video method active');
                    } catch(e) {
                        console.log('[WakeLock] Video method failed:', e.message);
                        status.innerText = 'Error: ' + e.message;
                    }
                }
                
                if (success) {
                    wakeLockEnabled = true;
                    btn.innerText = 'Disable Stay Awake';
                    btn.style.background = '#060';
                }
            } else {
                // Disable
                wakeLockEnabled = false;
                
                if (wakeLock) {
                    try { wakeLock.release(); } catch(e) {}
                    wakeLock = null;
                }
                
                if (noSleepInterval) {
                    clearInterval(noSleepInterval);
                    noSleepInterval = null;
                }
                
                if (noSleepVideo) {
                    noSleepVideo.pause();
                }
                
                status.innerText = 'Screen: Normal';
                btn.innerText = 'Enable Stay Awake';
                btn.style.background = '#555';
                console.log('[WakeLock] Disabled');
            }
        }
        
        // Re-acquire wake lock on visibility change
        document.addEventListener('visibilitychange', async function() {
            if (!wakeLockEnabled) return;
            
            if (document.visibilityState === 'visible') {
                // Try to re-acquire native wake lock
                if ('wakeLock' in navigator && location.protocol === 'https:' && wakeLock === null) {
                    try {
                        wakeLock = await navigator.wakeLock.request('screen');
                        console.log('[WakeLock] Re-acquired on visibility');
                    } catch(e) {}
                }
                
                // Resume video if paused
                if (noSleepVideo && noSleepVideo.paused) {
                    noSleepVideo.play().catch(function(){});
                }
            }
        });

        // Check if running as installed PWA
        var isStandalone = window.matchMedia('(display-mode: standalone)').matches || 
                          window.navigator.standalone === true;

        // PWA Install handling
        var pwaStatusEl = document.getElementById('pwaStatus');
        var btnInstall = document.getElementById('btnInstall');
        var deferredPrompt = null;

        // Register Service Worker
        if ('serviceWorker' in navigator) {
            var isSecure = location.protocol === 'https:' || location.hostname === 'localhost' || location.hostname === '127.0.0.1';
            if (isSecure) {
                navigator.serviceWorker.register('/sw.js').then(function(reg) {
                    console.log('[PWA] Service Worker registered');
                    if (isStandalone) {
                        pwaStatusEl.innerText = 'App Mode ✓';
                        pwaStatusEl.style.color = '#4CAF50';
                    } else {
                        pwaStatusEl.innerText = 'PWA Ready';
                    }
                }).catch(function(err) {
                    console.error('[PWA] SW Error:', err);
                    pwaStatusEl.innerText = 'SW Error';
                });
            } else {
                pwaStatusEl.innerText = 'Use HTTPS (8081)';
                pwaStatusEl.style.color = '#FFA500';
            }
        } else {
            pwaStatusEl.innerText = 'Not supported';
        }

        // Capture install prompt
        window.addEventListener('beforeinstallprompt', function(e) {
            console.log('[PWA] beforeinstallprompt fired!');
            e.preventDefault();
            deferredPrompt = e;
            btnInstall.style.display = 'block';
            pwaStatusEl.innerText = 'Tap Install!';
            pwaStatusEl.style.color = '#4CAF50';
        });

        // Detect when app is installed
        window.addEventListener('appinstalled', function(e) {
            console.log('[PWA] App installed!');
            pwaStatusEl.innerText = 'Installed ✓';
            pwaStatusEl.style.color = '#4CAF50';
            btnInstall.style.display = 'none';
            deferredPrompt = null;
        });

        function installPWA() {
            if (deferredPrompt) {
                deferredPrompt.prompt();
                deferredPrompt.userChoice.then(function(result) {
                    console.log('[PWA] Choice:', result.outcome);
                    deferredPrompt = null;
                    btnInstall.style.display = 'none';
                });
            } else if (isStandalone) {
                alert('Already installed!');
            } else {
                // Manual instruction
                var isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent);
                if (isIOS) {
                    alert('iOS: Tap Share → Add to Home Screen');
                } else {
                    alert('Tap browser menu (⋮) → Install app');
                }
            }
        }

        var padding = 0;
        var container = document.getElementById('container');
        var valLabel = document.getElementById('val');
        var controls = document.getElementById('controls');
        var loginDiv = document.getElementById('login');
        var streamImg = document.getElementById('stream');
        var passInput = document.getElementById('passInput');
        var audioPlayer = document.getElementById('audioPlayer');
        var audioEnabled = false;

        // Check stored password on load
        var storedPass = localStorage.getItem('win7app_pass');
        if (storedPass) {
            tryConnect(storedPass);
        }

        function doLogin() {
            var p = passInput.value;
            if (p) {
                localStorage.setItem('win7app_pass', p);
                tryConnect(p);
            }
        }

        function tryConnect(p) {
            currentPass = p;
            streamImg.onload = function() {
                // Success
                loginDiv.style.display = 'none';
                container.style.display = 'flex';
                document.getElementById('floatBtn').style.display = 'flex';
                
                // Auto-enable Stay Awake dan Audio
                setTimeout(function() { if (!wakeLockEnabled) toggleWakeLock(); }, 500);
                setTimeout(function() { if (!audioEnabled) toggleAudio(); }, 1000);
            };
            streamImg.onerror = function() {
                // Fail
                loginDiv.style.display = 'flex';
                container.style.display = 'none';
                document.getElementById('floatBtn').style.display = 'none';
            };
            streamImg.src = '/stream?auth=' + encodeURIComponent(p) + '&t=' + new Date().getTime();
        }

        var audioInterval = null;
        var audioContext = null;
        var nextPlayTime = 0;
        
        function toggleAudio() {
            var btn = document.getElementById('btnAudio');
            var status = document.getElementById('audioStatus');
            
            if (!audioEnabled) {
                status.innerText = 'Audio: Starting...';
                btn.disabled = true;
                
                var p = localStorage.getItem('win7app_pass') || '';
                audioEnabled = true;
                
                // Use Web Audio API for low latency
                try {
                    audioContext = new (window.AudioContext || window.webkitAudioContext)({
                        sampleRate: 44100,
                        latencyHint: 'interactive'
                    });
                } catch(e) {
                    audioContext = new (window.AudioContext || window.webkitAudioContext)();
                }
                
                nextPlayTime = 0;
                
                function fetchAndPlay() {
                    if (!audioEnabled) return;
                    
                    fetch('/audio-chunk?auth=' + encodeURIComponent(p) + '&t=' + Date.now())
                        .then(function(r) { return r.arrayBuffer(); })
                        .then(function(data) {
                            if (!audioEnabled || data.byteLength < 50) {
                                setTimeout(fetchAndPlay, 50);
                                return;
                            }
                            
                            // Parse WAV and decode
                            audioContext.decodeAudioData(data, function(buffer) {
                                var source = audioContext.createBufferSource();
                                source.buffer = buffer;
                                source.connect(audioContext.destination);
                                
                                var now = audioContext.currentTime;
                                if (nextPlayTime < now) {
                                    nextPlayTime = now;
                                }
                                
                                source.start(nextPlayTime);
                                nextPlayTime += buffer.duration;
                                
                                // Fetch next chunk slightly before current ends
                                var delay = Math.max(10, (nextPlayTime - now - 0.05) * 1000);
                                setTimeout(fetchAndPlay, delay);
                                
                                status.innerText = 'Audio: Playing';
                                btn.disabled = false;
                            }, function() {
                                setTimeout(fetchAndPlay, 50);
                            });
                        })
                        .catch(function() {
                            if (audioEnabled) setTimeout(fetchAndPlay, 100);
                        });
                }
                
                fetchAndPlay();
                btn.innerText = 'Disable Audio';
                btn.style.background = '#060';
                
            } else {
                // Disable audio
                audioEnabled = false;
                if (audioInterval) {
                    clearInterval(audioInterval);
                    audioInterval = null;
                }
                try {
                    if (audioContext) {
                        audioContext.close();
                        audioContext = null;
                    }
                    if (audioPlayer) {
                        audioPlayer.pause();
                        audioPlayer.src = '';
                    }
                } catch(e) {}
                status.innerText = 'Audio: Off';
                btn.innerText = 'Enable Audio';
                btn.style.background = '#555';
            }
        }

        function adjust(delta) {
            padding += delta;
            if (padding < 0) padding = 0;
            container.style.padding = padding + 'px';
            valLabel.innerText = padding;
            localStorage.setItem('win7app_padding', padding);
        }

        // Restore padding from storage
        var storedPadding = localStorage.getItem('win7app_padding');
        if (storedPadding) {
            padding = parseInt(storedPadding) || 0;
            container.style.padding = padding + 'px';
            valLabel.innerText = padding;
        }

        function toggleControls() {
            controls.style.display = (controls.style.display === 'block') ? 'none' : 'block';
        }

        // Touch Input - send to Windows
        var currentPass = localStorage.getItem('win7app_pass') || '';
        var touchEnabled = true;
        var isTouching = false;

        function sendTouch(action, x, y) {
            if (!touchEnabled) return;
            var rect = streamImg.getBoundingClientRect();
            var relX = x - rect.left;
            var relY = y - rect.top;
            
            // Calculate position relative to actual image (not padded area)
            var imgW = streamImg.naturalWidth;
            var imgH = streamImg.naturalHeight;
            var dispW = rect.width;
            var dispH = rect.height;

            // Scale to original resolution
            var scaledX = Math.round((relX / dispW) * imgW);
            var scaledY = Math.round((relY / dispH) * imgH);

            var url = '/touch?auth=' + encodeURIComponent(currentPass) +
                      '&x=' + scaledX + '&y=' + scaledY +
                      '&action=' + action +
                      '&sw=' + imgW + '&sh=' + imgH;

            var xhr = new XMLHttpRequest();
            xhr.open('GET', url, true);
            xhr.send();
        }

        streamImg.addEventListener('touchstart', function(e) {
            if (controls.style.display === 'block') return;
            e.preventDefault();
            isTouching = true;
            var t = e.touches[0];
            sendTouch('down', t.clientX, t.clientY);
        }, { passive: false });

        streamImg.addEventListener('touchmove', function(e) {
            if (controls.style.display === 'block') return;
            e.preventDefault();
            if (isTouching) {
                var t = e.touches[0];
                sendTouch('move', t.clientX, t.clientY);
            }
        }, { passive: false });

        streamImg.addEventListener('touchend', function(e) {
            if (controls.style.display === 'block') return;
            e.preventDefault();
            if (isTouching) {
                var t = e.changedTouches[0];
                sendTouch('up', t.clientX, t.clientY);
                isTouching = false;
            }
        }, { passive: false });

        // Mouse events for desktop testing
        streamImg.addEventListener('mousedown', function(e) {
            if (controls.style.display === 'block') return;
            isTouching = true;
            sendTouch('down', e.clientX, e.clientY);
        });

        streamImg.addEventListener('mousemove', function(e) {
            if (controls.style.display === 'block') return;
            if (isTouching) {
                sendTouch('move', e.clientX, e.clientY);
            }
        });

        streamImg.addEventListener('mouseup', function(e) {
            if (controls.style.display === 'block') return;
            if (isTouching) {
                sendTouch('up', e.clientX, e.clientY);
                isTouching = false;
            }
        });

        // Floating button for menu
        var floatBtn = document.getElementById('floatBtn');
        var isDragging = false;
        var dragStartX, dragStartY, btnStartX, btnStartY;
        var hasMoved = false;
        
        // Load saved position
        var savedX = localStorage.getItem('floatBtn_x');
        var savedY = localStorage.getItem('floatBtn_y');
        if (savedX && savedY) {
            floatBtn.style.left = savedX + 'px';
            floatBtn.style.top = savedY + 'px';
        } else {
            floatBtn.style.right = '20px';
            floatBtn.style.bottom = '80px';
        }
        
        function startDrag(x, y) {
            isDragging = true;
            hasMoved = false;
            dragStartX = x;
            dragStartY = y;
            var rect = floatBtn.getBoundingClientRect();
            btnStartX = rect.left;
            btnStartY = rect.top;
            floatBtn.style.right = 'auto';
            floatBtn.style.bottom = 'auto';
        }
        
        function moveDrag(x, y) {
            if (!isDragging) return;
            var dx = x - dragStartX;
            var dy = y - dragStartY;
            if (Math.abs(dx) > 5 || Math.abs(dy) > 5) hasMoved = true;
            var newX = Math.max(0, Math.min(window.innerWidth - 50, btnStartX + dx));
            var newY = Math.max(0, Math.min(window.innerHeight - 50, btnStartY + dy));
            floatBtn.style.left = newX + 'px';
            floatBtn.style.top = newY + 'px';
        }
        
        function endDrag() {
            if (isDragging) {
                isDragging = false;
                // Save position
                localStorage.setItem('floatBtn_x', parseInt(floatBtn.style.left));
                localStorage.setItem('floatBtn_y', parseInt(floatBtn.style.top));
                if (!hasMoved) {
                    toggleControls();
                }
            }
        }
        
        // Touch events
        floatBtn.addEventListener('touchstart', function(e) {
            e.preventDefault();
            var touch = e.touches[0];
            startDrag(touch.clientX, touch.clientY);
        }, {passive: false});
        
        floatBtn.addEventListener('touchmove', function(e) {
            e.preventDefault();
            var touch = e.touches[0];
            moveDrag(touch.clientX, touch.clientY);
        }, {passive: false});
        
        floatBtn.addEventListener('touchend', function(e) {
            e.preventDefault();
            endDrag();
        }, {passive: false});
        
        // Mouse events
        floatBtn.addEventListener('mousedown', function(e) {
            startDrag(e.clientX, e.clientY);
        });
        
        document.addEventListener('mousemove', function(e) {
            moveDrag(e.clientX, e.clientY);
        });
        
        document.addEventListener('mouseup', function(e) {
            endDrag();
        });
        
        floatBtn.addEventListener('click', function(e) {
            if (!hasMoved) toggleControls();
        });
    </script>
</body>
</html>";

            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Content-Length: " + html.Length);
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Write(html);
            writer.Flush();
        }

        private void ServeStream(Stream stream, StreamWriter writer)
        {
            string boundary = "boundary";
            
            // Write MJPEG Header
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: multipart/x-mixed-replace; boundary=" + boundary);
            writer.WriteLine();
            writer.Flush();

            while (_isRunning)
            {
                byte[] jpegBytes = ScreenCapture.CaptureScreenToJpeg(_screenToCapture, _quality);

                if (jpegBytes != null)
                {
                    // Write Frame Header
                    writer.WriteLine("--" + boundary);
                    writer.WriteLine("Content-Type: image/jpeg");
                    writer.WriteLine("Content-Length: " + jpegBytes.Length);
                    writer.WriteLine();
                    writer.Flush();

                    // Write Frame Data
                    stream.Write(jpegBytes, 0, jpegBytes.Length);
                    stream.Flush();

                    // Write NewLine
                    writer.WriteLine();
                    writer.Flush();
                }

                Thread.Sleep(1000 / _fps);
            }
        }

        private void ServeAudioChunk(Stream stream, StreamWriter writer)
        {
            if (_audioCapture == null)
            {
                writer.WriteLine("HTTP/1.1 503 Service Unavailable");
                writer.WriteLine("Content-Length: 0");
                writer.WriteLine();
                writer.Flush();
                return;
            }

            int sampleRate = _audioCapture.SampleRate;
            int channels = _audioCapture.Channels;
            int bitsPerSample = _audioCapture.BitsPerSample;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            
            // Target 100ms chunk for low latency
            int targetSize = (byteRate * 100) / 1000;
            int minSize = (byteRate * 50) / 1000; // 50ms minimum
            
            byte[] audioData = null;
            int attempts = 0;
            
            // Wait for enough data (max 60ms wait)
            while (attempts < 12 && _isRunning)
            {
                audioData = _audioCapture.GetAudioData();
                if (audioData != null && audioData.Length >= targetSize)
                    break;
                if (audioData != null && audioData.Length >= minSize && attempts > 6)
                    break;
                Thread.Sleep(5);
                attempts++;
            }
            
            // If no data, send small silence
            if (audioData == null || audioData.Length == 0)
            {
                audioData = new byte[minSize];
            }
            
            // Create complete WAV file
            byte[] wavFile = CreateCompleteWav(audioData, sampleRate, channels, bitsPerSample);

            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: audio/wav");
            writer.WriteLine(String.Format("Content-Length: {0}", wavFile.Length));
            writer.WriteLine("Cache-Control: no-cache");
            writer.WriteLine("Access-Control-Allow-Origin: *");
            writer.WriteLine();
            writer.Flush();
            
            stream.Write(wavFile, 0, wavFile.Length);
            stream.Flush();
        }

        private byte[] CreateSilenceWav(int durationMs, int sampleRate, int channels, int bitsPerSample)
        {
            int bytesPerSecond = sampleRate * channels * (bitsPerSample / 8);
            int dataSize = (bytesPerSecond * durationMs) / 1000;
            byte[] silence = new byte[dataSize]; // Zero-filled = silence
            return CreateCompleteWav(silence, sampleRate, channels, bitsPerSample);
        }

        private byte[] CreateCompleteWav(byte[] audioData, int sampleRate, int channels, int bitsPerSample)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int blockAlign = channels * bitsPerSample / 8;
                int dataSize = audioData.Length;
                int fileSize = 36 + dataSize;

                // RIFF header
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(fileSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                
                // fmt chunk
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16); // Subchunk1Size (PCM)
                bw.Write((short)1); // AudioFormat (PCM)
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)blockAlign);
                bw.Write((short)bitsPerSample);
                
                // data chunk
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);
                bw.Write(audioData);

                return ms.ToArray();
            }
        }

        private void WriteChunk(Stream stream, byte[] data)
        {
            try
            {
                string sizeHex = data.Length.ToString("X") + "\r\n";
                byte[] sizeBytes = Encoding.ASCII.GetBytes(sizeHex);
                stream.Write(sizeBytes, 0, sizeBytes.Length);
                stream.Write(data, 0, data.Length);
                byte[] crlf = Encoding.ASCII.GetBytes("\r\n");
                stream.Write(crlf, 0, crlf.Length);
                stream.Flush();
            }
            catch { }
        }

        private byte[] CreateWavHeader(int sampleRate, int channels, int bitsPerSample)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int blockAlign = channels * bitsPerSample / 8;

                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(0xFFFFFFFF); // File size (unknown for streaming)
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16); // Subchunk1Size (PCM)
                bw.Write((short)1); // AudioFormat (PCM)
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)blockAlign);
                bw.Write((short)bitsPerSample);
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(0xFFFFFFFF); // Data size (unknown for streaming)

                return ms.ToArray();
            }
        }

        private void ServeManifest(StreamWriter writer)
        {
            // Simplified manifest - Chrome Android requires clean JSON
            string manifest = @"{
    ""name"": ""Win7 Virtual Monitor"",
    ""short_name"": ""Win7VM"",
    ""description"": ""Stream Windows display to your device"",
    ""start_url"": ""/"",
    ""scope"": ""/"",
    ""display"": ""standalone"",
    ""orientation"": ""any"",
    ""background_color"": ""#000000"",
    ""theme_color"": ""#1a1a2e"",
    ""icons"": [
        {
            ""src"": ""/icon-192.png"",
            ""sizes"": ""192x192"",
            ""type"": ""image/png""
        },
        {
            ""src"": ""/icon-512.png"",
            ""sizes"": ""512x512"",
            ""type"": ""image/png""
        }
    ]
}";
            byte[] manifestBytes = Encoding.UTF8.GetBytes(manifest);
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: application/manifest+json; charset=utf-8");
            writer.WriteLine(String.Format("Content-Length: {0}", manifestBytes.Length));
            writer.WriteLine("Cache-Control: public, max-age=86400");
            writer.WriteLine("Access-Control-Allow-Origin: *");
            writer.WriteLine();
            writer.Flush();
            writer.BaseStream.Write(manifestBytes, 0, manifestBytes.Length);
            writer.BaseStream.Flush();
        }

        private void ServeIcon(string url, Stream stream, StreamWriter writer)
        {
            // Generate PNG icon dynamically - support all sizes
            int size = 192; // default
            bool maskable = url.Contains("maskable");
            
            // Parse size from URL (e.g., /icon-48.png, /icon-512-maskable.png)
            if (url.Contains("512")) size = 512;
            else if (url.Contains("384")) size = 384;
            else if (url.Contains("192")) size = 192;
            else if (url.Contains("144")) size = 144;
            else if (url.Contains("128")) size = 128;
            else if (url.Contains("96")) size = 96;
            else if (url.Contains("72")) size = 72;
            else if (url.Contains("48")) size = 48;
            
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                
                if (maskable)
                {
                    // Maskable icon needs safe zone (inner 80%)
                    g.Clear(Color.FromArgb(26, 26, 46)); // #1a1a2e
                    int padding = size / 10;
                    int innerSize = size - padding * 2;
                    
                    // Draw monitor icon
                    using (Brush brush = new SolidBrush(Color.FromArgb(0, 150, 255)))
                    {
                        int monW = innerSize * 2 / 3;
                        int monH = innerSize / 2;
                        int monX = padding + (innerSize - monW) / 2;
                        int monY = padding + innerSize / 4;
                        
                        // Monitor frame
                        g.FillRectangle(brush, monX, monY, monW, monH);
                        
                        // Screen (inner)
                        using (Brush black = new SolidBrush(Color.FromArgb(40, 40, 60)))
                        {
                            int border = Math.Max(2, size / 48);
                            g.FillRectangle(black, monX + border, monY + border, monW - border * 2, monH - border * 3);
                        }
                        
                        // Stand
                        int standW = monW / 4;
                        int standH = innerSize / 8;
                        g.FillRectangle(brush, monX + (monW - standW) / 2, monY + monH, standW, standH);
                        
                        // Base
                        int baseW = monW / 2;
                        int baseH = Math.Max(2, size / 64);
                        g.FillRectangle(brush, monX + (monW - baseW) / 2, monY + monH + standH, baseW, baseH);
                    }
                    
                    // Draw "7" text
                    float fontSize = Math.Max(8, size / 6f);
                    using (Font font = new Font("Arial", fontSize, FontStyle.Bold))
                    using (Brush white = new SolidBrush(Color.White))
                    {
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("7", font, white, size / 2, size / 2 - size / 20, sf);
                    }
                }
                else
                {
                    // Regular icon with transparent background
                    g.Clear(Color.Transparent);
                    
                    // Draw rounded rect background
                    int radius = Math.Max(4, size / 8);
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(26, 26, 46)))
                    {
                        FillRoundedRect(g, bgBrush, 0, 0, size, size, radius);
                    }
                    
                    int padding = size / 8;
                    int innerSize = size - padding * 2;
                    
                    // Draw monitor
                    using (Brush brush = new SolidBrush(Color.FromArgb(0, 150, 255)))
                    {
                        int monW = innerSize * 2 / 3;
                        int monH = innerSize / 2;
                        int monX = padding + (innerSize - monW) / 2;
                        int monY = padding + innerSize / 5;
                        
                        g.FillRectangle(brush, monX, monY, monW, monH);
                        
                        using (Brush black = new SolidBrush(Color.FromArgb(40, 40, 60)))
                        {
                            int border = Math.Max(2, size / 64);
                            g.FillRectangle(black, monX + border, monY + border, monW - border * 2, monH - border * 3);
                        }
                        
                        int standW = monW / 4;
                        int standH = innerSize / 10;
                        g.FillRectangle(brush, monX + (monW - standW) / 2, monY + monH, standW, standH);
                        
                        int baseW = monW / 2;
                        int baseH = Math.Max(2, size / 64);
                        g.FillRectangle(brush, monX + (monW - baseW) / 2, monY + monH + standH, baseW, baseH);
                    }
                    
                    // "7" text - scale font size appropriately
                    float fontSize = Math.Max(8, size / 5f);
                    using (Font font = new Font("Arial", fontSize, FontStyle.Bold))
                    using (Brush white = new SolidBrush(Color.White))
                    {
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("7", font, white, size / 2, size / 2 - size / 16, sf);
                    }
                }
                
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] pngData = ms.ToArray();
                    
                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine("Content-Type: image/png");
                    writer.WriteLine(String.Format("Content-Length: {0}", pngData.Length));
                    writer.WriteLine("Cache-Control: public, max-age=604800"); // 1 week for icons
                    writer.WriteLine("Access-Control-Allow-Origin: *");
                    writer.WriteLine();
                    writer.Flush();
                    stream.Write(pngData, 0, pngData.Length);
                    stream.Flush();
                }
            }
        }

        private void FillRoundedRect(Graphics g, Brush brush, int x, int y, int w, int h, int r)
        {
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(x, y, r * 2, r * 2, 180, 90);
                path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
                path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
                path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }

        private void ServeOfflinePage(StreamWriter writer)
        {
            string offlineHtml = @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>Win7VM - Offline</title>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta name='theme-color' content='#1a1a2e'>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            min-height: 100vh; 
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
            display: flex; 
            flex-direction: column;
            justify-content: center; 
            align-items: center; 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            color: white;
            padding: 20px;
            text-align: center;
        }
        .icon { font-size: 64px; margin-bottom: 20px; opacity: 0.8; }
        h1 { font-size: 24px; margin-bottom: 10px; color: #0096ff; }
        p { color: #888; margin-bottom: 20px; max-width: 300px; line-height: 1.5; }
        .btn {
            background: #0096ff;
            color: white;
            border: none;
            padding: 12px 24px;
            border-radius: 8px;
            font-size: 16px;
            cursor: pointer;
            transition: background 0.2s;
        }
        .btn:hover { background: #0077cc; }
        .status { margin-top: 20px; font-size: 12px; color: #666; }
    </style>
</head>
<body>
    <div class='icon'>📡</div>
    <h1>You're Offline</h1>
    <p>Unable to connect to Win7 Virtual Monitor server. Please check your network connection and try again.</p>
    <button class='btn' onclick='location.reload()'>Retry Connection</button>
    <div class='status' id='status'>Checking connection...</div>
    <script>
        // Auto-retry when back online
        window.addEventListener('online', function() {
            document.getElementById('status').innerText = 'Connection restored! Reloading...';
            setTimeout(function() { location.reload(); }, 1000);
        });
        
        // Update status
        if (navigator.onLine) {
            document.getElementById('status').innerText = 'Network available - Server may be unreachable';
        } else {
            document.getElementById('status').innerText = 'No network connection';
        }
        
        // Periodic check
        setInterval(function() {
            fetch('/?ping=' + Date.now(), { method: 'HEAD', cache: 'no-store' })
                .then(function() { location.reload(); })
                .catch(function() {});
        }, 5000);
    </script>
</body>
</html>";
            byte[] htmlBytes = Encoding.UTF8.GetBytes(offlineHtml);
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: text/html; charset=utf-8");
            writer.WriteLine(String.Format("Content-Length: {0}", htmlBytes.Length));
            writer.WriteLine("Cache-Control: public, max-age=86400");
            writer.WriteLine();
            writer.Flush();
            writer.BaseStream.Write(htmlBytes, 0, htmlBytes.Length);
            writer.BaseStream.Flush();
        }

        private void ServeServiceWorker(StreamWriter writer)
        {
            // Simplified service worker for PWA install compatibility
            string sw = @"const CACHE_NAME = 'win7vm-v6';

// Install event
self.addEventListener('install', (event) => {
    console.log('[SW] Installing...');
    self.skipWaiting();
});

// Activate event
self.addEventListener('activate', (event) => {
    console.log('[SW] Activating...');
    event.waitUntil(self.clients.claim());
});

// Fetch event - REQUIRED for PWA install prompt
self.addEventListener('fetch', (event) => {
    // Only handle GET requests
    if (event.request.method !== 'GET') return;
    
    // Skip streaming endpoints
    const url = new URL(event.request.url);
    if (url.pathname === '/stream' || 
        url.pathname === '/audio' ||
        url.pathname === '/touch') {
        return;
    }
    
    // Network first strategy
    event.respondWith(
        fetch(event.request)
            .then((response) => {
                return response;
            })
            .catch(() => {
                // Return offline page for navigation requests
                if (event.request.mode === 'navigate') {
                    return caches.match('/offline');
                }
                return new Response('Offline', { status: 503 });
            })
    );
});";
            byte[] swBytes = Encoding.UTF8.GetBytes(sw);
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: application/javascript; charset=utf-8");
            writer.WriteLine(String.Format("Content-Length: {0}", swBytes.Length));
            writer.WriteLine("Cache-Control: no-cache, no-store, must-revalidate");
            writer.WriteLine("Service-Worker-Allowed: /");
            writer.WriteLine("Access-Control-Allow-Origin: *");
            writer.WriteLine();
            writer.Flush();
            writer.BaseStream.Write(swBytes, 0, swBytes.Length);
            writer.BaseStream.Flush();
        }

        private string GetHostFromRequest(string requestLine, TcpClient client)
        {
            // Try to get host from HTTP request, fallback to client IP
            try
            {
                IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
                IPEndPoint local = client.Client.LocalEndPoint as IPEndPoint;
                if (local != null)
                {
                    return local.Address.ToString();
                }
            }
            catch { }
            return "localhost";
        }
    }

    // Helper stream that prepends a peeked byte
    internal class PeekableStream : Stream
    {
        private Stream _inner;
        private byte _peekedByte;
        private bool _hasPeeked;

        public PeekableStream(Stream inner, byte peekedByte)
        {
            _inner = inner;
            _peekedByte = peekedByte;
            _hasPeeked = true;
        }

        public override bool CanRead { get { return _inner.CanRead; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return _inner.CanWrite; } }
        public override long Length { get { return _inner.Length; } }
        public override long Position 
        { 
            get { return _inner.Position; } 
            set { _inner.Position = value; } 
        }

        public override void Flush() { _inner.Flush(); }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_hasPeeked && count > 0)
            {
                buffer[offset] = _peekedByte;
                _hasPeeked = false;
                if (count == 1) return 1;
                int read = _inner.Read(buffer, offset + 1, count - 1);
                return read + 1;
            }
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
        }
    }
}