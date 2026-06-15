/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib.WzProperties;
using NAudio.Wave;
using System;
using System.Diagnostics;

namespace HaSharedLibrary
{
    /// <summary>
    /// Streams WAV or MP3 audio from a WzBinaryProperty.
    /// Audio playback requires a platform-specific backend (NAudio.WinMM on Windows,
    /// or a cross-platform library such as OpenAL/PortAudio on macOS/Linux).
    /// This stub maintains the public interface; actual playback is a TODO.
    /// </summary>
    public class WzSoundResourceStreamer
    {
        private readonly WzBinaryProperty sound;
        private bool repeat;
        private float volume = 0.5f;
        private int position = 0;

        public bool Disposed { get; private set; } = false;

        public WzSoundResourceStreamer(WzBinaryProperty sound, bool repeat)
        {
            this.sound = sound;
            this.repeat = repeat;
            Debug.WriteLine("[WzSoundResourceStreamer] Audio playback not yet implemented for this platform.");
        }

        public void Play()   { /* TODO: cross-platform audio backend */ }
        public void Pause()  { /* TODO */ }
        public void Stop()   { /* TODO */ }

        public void Dispose() { Disposed = true; }

        public bool Repeat
        {
            get => repeat;
            set => repeat = value;
        }

        public int Length => sound.Length / 1000;

        public float Volume
        {
            get => volume;
            set { if (value >= 0f && value <= 1f) volume = value; }
        }

        public int Position
        {
            get => position;
            set => position = value;
        }
    }
}
