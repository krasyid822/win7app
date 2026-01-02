using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Win7App
{
    /// <summary>
    /// Audio capture using WASAPI Loopback - Captures system audio output (what plays through speakers).
    /// Compatible with Windows 7+.
    /// </summary>
    public class AudioCapture : IDisposable
    {
        // COM GUIDs for WASAPI
        private static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        private static readonly Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
        private static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

        // COM Interfaces
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppEndpoint);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
            int GetBufferSize(out uint pNumBufferFrames);
            int GetStreamLatency(out long phnsLatency);
            int GetCurrentPadding(out uint pNumPaddingFrames);
            int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
            int GetMixFormat(out IntPtr ppDeviceFormat);
            int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
            int Start();
            int Stop();
            int Reset();
            int SetEventHandle(IntPtr eventHandle);
            int GetService(ref Guid riid, out IntPtr ppv);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            int ReleaseBuffer(uint numFramesRead);
            int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEXTENSIBLE
        {
            public WAVEFORMATEX Format;
            public ushort wValidBitsPerSample;
            public uint dwChannelMask;
            public Guid SubFormat;
        }

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        private const int AUDCLNT_SHAREMODE_SHARED = 0;
        private const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const int eRender = 0; // Audio output (speakers)
        private const int eConsole = 0;
        private const uint CLSCTX_ALL = 0x17;

        private IntPtr _deviceEnumerator = IntPtr.Zero;
        private IntPtr _device = IntPtr.Zero;
        private IntPtr _audioClient = IntPtr.Zero;
        private IntPtr _captureClient = IntPtr.Zero;
        private IntPtr _mixFormat = IntPtr.Zero;
        
        private Thread _captureThread;
        private volatile bool _isCapturing;
        private MemoryStream _audioStream;
        private object _lock = new object();
        
        private int _outputSampleRate;
        private int _outputChannels;
        private int _outputBitsPerSample;
        
        // Native format from device
        private int _nativeSampleRate;
        private int _nativeChannels;
        private int _nativeBitsPerSample;
        private int _nativeBlockAlign;

        public int SampleRate { get { return _outputSampleRate; } }
        public int Channels { get { return _outputChannels; } }
        public int BitsPerSample { get { return _outputBitsPerSample; } }

        public AudioCapture(int sampleRate = 44100, int channels = 2, int bitsPerSample = 16)
        {
            _outputSampleRate = sampleRate;
            _outputChannels = channels;
            _outputBitsPerSample = bitsPerSample;
            _audioStream = new MemoryStream();
        }

        public bool Start()
        {
            if (_isCapturing) return true;

            try
            {
                // Initialize COM
                CoInitializeEx(IntPtr.Zero, 0);

                // Create device enumerator
                Guid clsid = CLSID_MMDeviceEnumerator;
                Guid iid = IID_IMMDeviceEnumerator;
                int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_ALL, ref iid, out _deviceEnumerator);
                if (hr != 0) return false;

                // Get default audio output device (render device for loopback)
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)Marshal.GetObjectForIUnknown(_deviceEnumerator);
                hr = enumerator.GetDefaultAudioEndpoint(eRender, eConsole, out _device);
                if (hr != 0) return false;

                // Activate audio client
                IMMDevice device = (IMMDevice)Marshal.GetObjectForIUnknown(_device);
                Guid audioClientIid = IID_IAudioClient;
                hr = device.Activate(ref audioClientIid, (int)CLSCTX_ALL, IntPtr.Zero, out _audioClient);
                if (hr != 0) return false;

                // Get mix format
                IAudioClient audioClient = (IAudioClient)Marshal.GetObjectForIUnknown(_audioClient);
                hr = audioClient.GetMixFormat(out _mixFormat);
                if (hr != 0) return false;

                // Read native format
                WAVEFORMATEX wfx = (WAVEFORMATEX)Marshal.PtrToStructure(_mixFormat, typeof(WAVEFORMATEX));
                _nativeSampleRate = (int)wfx.nSamplesPerSec;
                _nativeChannels = wfx.nChannels;
                _nativeBitsPerSample = wfx.wBitsPerSample;
                _nativeBlockAlign = wfx.nBlockAlign;

                // Initialize in loopback mode (capture what's being played)
                long bufferDuration = 30 * 10000; // 30ms in 100ns units (very low latency)
                hr = audioClient.Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 
                    bufferDuration, 0, _mixFormat, IntPtr.Zero);
                if (hr != 0) return false;

                // Get capture client
                Guid captureClientIid = IID_IAudioCaptureClient;
                hr = audioClient.GetService(ref captureClientIid, out _captureClient);
                if (hr != 0) return false;

                // Start capturing
                hr = audioClient.Start();
                if (hr != 0) return false;

                _isCapturing = true;

                // Start capture thread
                _captureThread = new Thread(CaptureLoop);
                _captureThread.IsBackground = true;
                _captureThread.Start();

                return true;
            }
            catch
            {
                Stop();
                return false;
            }
        }

        private void CaptureLoop()
        {
            IAudioCaptureClient captureClient = null;
            try
            {
                captureClient = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(_captureClient);
            }
            catch
            {
                return;
            }

            while (_isCapturing)
            {
                try
                {
                    uint packetSize;
                    captureClient.GetNextPacketSize(out packetSize);

                    while (packetSize > 0 && _isCapturing)
                    {
                        IntPtr dataPtr;
                        uint numFrames;
                        uint flags;
                        ulong devicePos, qpcPos;

                        int hr = captureClient.GetBuffer(out dataPtr, out numFrames, out flags, out devicePos, out qpcPos);
                        if (hr == 0 && numFrames > 0)
                        {
                            int bytesToCopy = (int)(numFrames * _nativeBlockAlign);
                            byte[] buffer = new byte[bytesToCopy];
                            Marshal.Copy(dataPtr, buffer, 0, bytesToCopy);

                            // Convert to output format if needed
                            byte[] converted = ConvertAudio(buffer);

                            lock (_lock)
                            {
                                _audioStream.Write(converted, 0, converted.Length);
                                
                                // Limit buffer size (keep last 200ms for low latency)
                                int maxSize = (_outputSampleRate * _outputChannels * (_outputBitsPerSample / 8)) / 5;
                                if (_audioStream.Length > maxSize)
                                {
                                    byte[] remaining = _audioStream.ToArray();
                                    int keepFrom = remaining.Length - maxSize;
                                    _audioStream.SetLength(0);
                                    _audioStream.Write(remaining, keepFrom, maxSize);
                                }
                            }

                            captureClient.ReleaseBuffer(numFrames);
                        }

                        captureClient.GetNextPacketSize(out packetSize);
                    }

                    Thread.Sleep(5);
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
        }

        private byte[] ConvertAudio(byte[] input)
        {
            // If native format matches output, return as-is
            if (_nativeSampleRate == _outputSampleRate && 
                _nativeChannels == _outputChannels && 
                _nativeBitsPerSample == _outputBitsPerSample)
            {
                return input;
            }

            // Convert 32-bit float to 16-bit PCM (common WASAPI format)
            if (_nativeBitsPerSample == 32 && _outputBitsPerSample == 16)
            {
                int numSamples = input.Length / 4;
                byte[] output = new byte[numSamples * 2];

                for (int i = 0; i < numSamples; i++)
                {
                    float sample = BitConverter.ToSingle(input, i * 4);
                    // Clamp to [-1, 1]
                    if (sample > 1.0f) sample = 1.0f;
                    if (sample < -1.0f) sample = -1.0f;
                    short pcm = (short)(sample * 32767);
                    byte[] bytes = BitConverter.GetBytes(pcm);
                    output[i * 2] = bytes[0];
                    output[i * 2 + 1] = bytes[1];
                }

                input = output;
            }

            // Downmix stereo to mono if needed
            if (_nativeChannels == 2 && _outputChannels == 1)
            {
                int bytesPerSample = _outputBitsPerSample / 8;
                int numSamples = input.Length / (bytesPerSample * 2);
                byte[] mono = new byte[numSamples * bytesPerSample];

                for (int i = 0; i < numSamples; i++)
                {
                    if (bytesPerSample == 2)
                    {
                        short left = BitConverter.ToInt16(input, i * 4);
                        short right = BitConverter.ToInt16(input, i * 4 + 2);
                        short mixed = (short)((left + right) / 2);
                        byte[] bytes = BitConverter.GetBytes(mixed);
                        mono[i * 2] = bytes[0];
                        mono[i * 2 + 1] = bytes[1];
                    }
                }

                input = mono;
            }

            // Simple sample rate conversion (linear interpolation)
            if (_nativeSampleRate != _outputSampleRate)
            {
                int bytesPerSample = _outputBitsPerSample / 8 * _outputChannels;
                int inputSamples = input.Length / bytesPerSample;
                double ratio = (double)_outputSampleRate / _nativeSampleRate;
                int outputSamples = (int)(inputSamples * ratio);
                byte[] resampled = new byte[outputSamples * bytesPerSample];

                for (int i = 0; i < outputSamples; i++)
                {
                    double srcIdx = i / ratio;
                    int idx = (int)srcIdx;
                    if (idx >= inputSamples - 1) idx = inputSamples - 2;
                    if (idx < 0) idx = 0;

                    for (int ch = 0; ch < _outputChannels; ch++)
                    {
                        int offset = (idx * _outputChannels + ch) * 2;
                        int nextOffset = ((idx + 1) * _outputChannels + ch) * 2;
                        
                        if (offset + 1 < input.Length && nextOffset + 1 < input.Length)
                        {
                            short s1 = BitConverter.ToInt16(input, offset);
                            short s2 = BitConverter.ToInt16(input, nextOffset);
                            double frac = srcIdx - idx;
                            short interp = (short)(s1 + (s2 - s1) * frac);
                            
                            int outOffset = (i * _outputChannels + ch) * 2;
                            if (outOffset + 1 < resampled.Length)
                            {
                                byte[] bytes = BitConverter.GetBytes(interp);
                                resampled[outOffset] = bytes[0];
                                resampled[outOffset + 1] = bytes[1];
                            }
                        }
                    }
                }

                input = resampled;
            }

            return input;
        }

        public void Stop()
        {
            _isCapturing = false;

            if (_captureThread != null && _captureThread.IsAlive)
            {
                _captureThread.Join(1000);
            }

            try
            {
                if (_audioClient != IntPtr.Zero)
                {
                    IAudioClient client = (IAudioClient)Marshal.GetObjectForIUnknown(_audioClient);
                    client.Stop();
                }
            }
            catch { }

            if (_mixFormat != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_mixFormat);
                _mixFormat = IntPtr.Zero;
            }

            if (_captureClient != IntPtr.Zero)
            {
                Marshal.Release(_captureClient);
                _captureClient = IntPtr.Zero;
            }

            if (_audioClient != IntPtr.Zero)
            {
                Marshal.Release(_audioClient);
                _audioClient = IntPtr.Zero;
            }

            if (_device != IntPtr.Zero)
            {
                Marshal.Release(_device);
                _device = IntPtr.Zero;
            }

            if (_deviceEnumerator != IntPtr.Zero)
            {
                Marshal.Release(_deviceEnumerator);
                _deviceEnumerator = IntPtr.Zero;
            }
        }

        public byte[] GetAudioData()
        {
            lock (_lock)
            {
                byte[] data = _audioStream.ToArray();
                _audioStream.SetLength(0);
                return data;
            }
        }

        public void Dispose()
        {
            Stop();
            if (_audioStream != null)
            {
                _audioStream.Dispose();
                _audioStream = null;
            }
        }
    }
}
