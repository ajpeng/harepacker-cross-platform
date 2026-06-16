/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib.WzProperties;
using NAudio.Wave;
using OpenTK.Audio.OpenAL;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary
{
    /// <summary>
    /// Streams WAV or MP3 audio from a WzBinaryProperty using OpenAL (cross-platform).
    /// OpenAL is built-in on macOS, available via libopenal1 on Linux, and via the
    /// OpenAL Soft redistributable on Windows.
    /// </summary>
    public class WzSoundResourceStreamer
    {
        private readonly WzBinaryProperty _sound;
        private bool _repeat;
        private float _volume = 0.5f;
        private int _positionMs = 0;

        private ALDevice _device;
        private ALContext _context;
        private int _source;
        private int _buffer;
        private bool _openAlInitialized;
        private CancellationTokenSource _cts;

        public bool Disposed { get; private set; } = false;

        public WzSoundResourceStreamer(WzBinaryProperty sound, bool repeat)
        {
            _sound = sound;
            _repeat = repeat;
            InitOpenAL();
        }

        private void InitOpenAL()
        {
            try
            {
                _device = ALC.OpenDevice(null);
                if (_device.Handle == IntPtr.Zero) return;
                _context = ALC.CreateContext(_device, (int[])null);
                ALC.MakeContextCurrent(_context);

                _source = AL.GenSource();
                _buffer = AL.GenBuffer();

                byte[] wavBytes = _sound.GetBytesForWAVPlayback();
                if (wavBytes == null || wavBytes.Length == 0) return;

                byte[] pcm;
                int sampleRate;
                ALFormat alFormat;
                DecodeToPcm(wavBytes, out pcm, out sampleRate, out alFormat);

                if (pcm != null && pcm.Length > 0)
                {
                    AL.BufferData(_buffer, alFormat, pcm, sampleRate);
                    AL.Source(_source, ALSourcei.Buffer, _buffer);
                    AL.Source(_source, ALSourcef.Gain, _volume);
                    _openAlInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WzSoundResourceStreamer] OpenAL init failed: {ex.Message}");
                _openAlInitialized = false;
            }
        }

        private static void DecodeToPcm(byte[] wavBytes, out byte[] pcm, out int sampleRate, out ALFormat alFormat)
        {
            pcm = null; sampleRate = 44100; alFormat = ALFormat.Mono16;
            try
            {
                using var ms = new MemoryStream(wavBytes);
                using var reader = new WaveFileReader(ms);
                var fmt = reader.WaveFormat;
                sampleRate = fmt.SampleRate;

                // Only handle standard 16-bit or 8-bit PCM — other encodings (IMA ADPCM, MP3-in-WAV)
                // require NAudio.WinMM conversion which isn't available in NAudio.Core.
                if (fmt.Encoding != WaveFormatEncoding.Pcm)
                {
                    Debug.WriteLine($"[WzSoundResourceStreamer] Unsupported encoding: {fmt.Encoding}");
                    return;
                }

                using var ms2 = new MemoryStream();
                reader.CopyTo(ms2);
                byte[] raw = ms2.ToArray();

                if (fmt.BitsPerSample == 16)
                {
                    pcm = raw;
                    alFormat = fmt.Channels == 1 ? ALFormat.Mono16 : ALFormat.Stereo16;
                }
                else if (fmt.BitsPerSample == 8)
                {
                    // Convert 8-bit unsigned PCM to 16-bit signed PCM
                    pcm = new byte[raw.Length * 2];
                    for (int i = 0; i < raw.Length; i++)
                    {
                        short s = (short)((raw[i] - 128) * 256);
                        pcm[i * 2] = (byte)(s & 0xFF);
                        pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                    }
                    alFormat = fmt.Channels == 1 ? ALFormat.Mono16 : ALFormat.Stereo16;
                }
                else
                {
                    Debug.WriteLine($"[WzSoundResourceStreamer] Unsupported bit depth: {fmt.BitsPerSample}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WzSoundResourceStreamer] PCM decode failed: {ex.Message}");
            }
        }

        public void Play()
        {
            if (!_openAlInitialized) return;
            try
            {
                AL.Source(_source, ALSourceb.Looping, _repeat);
                AL.Source(_source, ALSourcef.Gain, _volume);
                AL.SourcePlay(_source);
            }
            catch (Exception ex) { Debug.WriteLine($"[WzSoundResourceStreamer] Play failed: {ex.Message}"); }
        }

        public void Pause()
        {
            if (!_openAlInitialized) return;
            try { AL.SourcePause(_source); }
            catch (Exception ex) { Debug.WriteLine($"[WzSoundResourceStreamer] Pause failed: {ex.Message}"); }
        }

        public void Stop()
        {
            if (!_openAlInitialized) return;
            try { AL.SourceStop(_source); }
            catch (Exception ex) { Debug.WriteLine($"[WzSoundResourceStreamer] Stop failed: {ex.Message}"); }
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            _cts?.Cancel();
            try
            {
                if (_openAlInitialized)
                {
                    AL.SourceStop(_source);
                    AL.DeleteSource(_source);
                    AL.DeleteBuffer(_buffer);
                    _openAlInitialized = false;
                }
                if (_context.Handle != IntPtr.Zero)
                {
                    ALC.MakeContextCurrent(ALContext.Null);
                    ALC.DestroyContext(_context);
                }
                if (_device.Handle != IntPtr.Zero)
                    ALC.CloseDevice(_device);
            }
            catch (Exception ex) { Debug.WriteLine($"[WzSoundResourceStreamer] Dispose error: {ex.Message}"); }
        }

        public bool Repeat
        {
            get => _repeat;
            set
            {
                _repeat = value;
                if (_openAlInitialized)
                    try { AL.Source(_source, ALSourceb.Looping, value); }
                    catch { }
            }
        }

        public int Length => _sound.Length / 1000;

        public float Volume
        {
            get => _volume;
            set
            {
                if (value < 0f || value > 1f) return;
                _volume = value;
                if (_openAlInitialized)
                    try { AL.Source(_source, ALSourcef.Gain, value); }
                    catch { }
            }
        }

        public int Position
        {
            get
            {
                if (!_openAlInitialized) return _positionMs;
                try
                {
                    AL.GetSource(_source, ALGetSourcei.SampleOffset, out int sampleOff);
                    return sampleOff; // caller interprets as ms
                }
                catch { return _positionMs; }
            }
            set => _positionMs = value;
        }
    }
}
