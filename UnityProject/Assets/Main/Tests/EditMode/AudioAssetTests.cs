using System;
using System.IO;
using NUnit.Framework;
using ProjectSurvive.Game.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 13c-1 acceptance for the placeholder audio: every cue has a clip, every clip is
    /// audible, and the import settings are the ones the Web build needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clips are synthesised by <c>tools/gen-audio.py</c>, so this file is the check that the
    /// script's output and the game's cue table still agree. A rename in either place is caught
    /// here, not by a silent battle.
    /// </para>
    /// <para>
    /// "Audible" is measured from the <b>source file</b>, not through <see cref="AudioClip"/>: the
    /// WAV is the artefact the generator owns, so reading its own samples checks the thing under
    /// test, and a generator bug that produced an all-zero buffer fails on RMS rather than passing a
    /// length check.
    /// </para>
    /// </remarks>
    public sealed class AudioAssetTests
    {
        private const string AudioFolder = "Assets/Main/Audio";

        /// <summary>Every effect is shorter than this; the music is longer.</summary>
        private const float EffectLengthLimitSeconds = 1.2f;

        /// <summary>A loop shorter than this is a blip, not a track.</summary>
        private const float MusicLengthLimitSeconds = 3f;

        /// <summary>The rate <c>tools/gen-audio.py</c> writes and the importer preserves.</summary>
        private const int GeneratedSampleRate = 22050;

        [Test]
        public void TheCueTableNamesExactlyAsManyClipsAsThereAreCues()
        {
            Assert.AreEqual(
                Enum.GetValues(typeof(AudioCue)).Length,
                AudioCueTable.Count,
                "AudioCue and AudioCueTable.Names disagree; the index mapping is broken.");
        }

        [Test]
        public void EveryCueHasItsClipInTheAudioFolder()
        {
            for (int i = 0; i < AudioCueTable.Count; i++)
            {
                string path = PathOf((AudioCue)i);
                Assert.IsTrue(File.Exists(path), "Missing clip for cue " + (AudioCue)i + " at " + path
                    + ". Run tools/gen-audio.py.");

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(clip, path + " is not imported as an AudioClip.");
            }
        }

        [Test]
        public void EveryClipIsAudiblePcmAtTheGeneratedRate()
        {
            for (int i = 0; i < AudioCueTable.Count; i++)
            {
                AudioCue cue = (AudioCue)i;
                Wav wav = Wav.Read(PathOf(cue));

                Assert.AreEqual(GeneratedSampleRate, wav.SampleRate, cue + " is not at the generated rate.");
                Assert.AreEqual(1, wav.Channels, cue + " is not mono.");
                Assert.AreEqual(16, wav.BitsPerSample, cue + " is not 16-bit PCM.");

                Assert.Greater(wav.Peak, 0.2f, cue + " peaks below 0.2: it would be inaudible.");
                Assert.Greater(wav.Rms, 0.01f, cue + " is (almost) silent: RMS " + wav.Rms
                    + ". A generator change made it noise-free.");
            }
        }

        [Test]
        public void EffectsAreShortAndMusicIsALongLoop()
        {
            for (int i = 0; i < AudioCueTable.Count; i++)
            {
                AudioCue cue = (AudioCue)i;
                Wav wav = Wav.Read(PathOf(cue));

                if (AudioCueTable.IsMusic(cue))
                {
                    Assert.Greater(
                        wav.Seconds, MusicLengthLimitSeconds,
                        cue + " is too short to be the ambience it claims to be.");
                }
                else
                {
                    Assert.Less(
                        wav.Seconds, EffectLengthLimitSeconds,
                        cue + " is longer than an effect should be; the mix will smear.");
                }
            }
        }

        [Test]
        public void TheImportSettingsAreTheOnesTheWebBuildNeeds()
        {
            // Recorded from Unity 6.3's "Audio in Web": both load types are supported on WebGL.
            // The compression *format* is the platform's business — the manual states a Web build
            // imports clips as AAC regardless — so it is not pinned here.
            for (int i = 0; i < AudioCueTable.Count; i++)
            {
                AudioCue cue = (AudioCue)i;
                string path = PathOf(cue);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;

                Assert.IsNotNull(importer, "No AudioImporter at " + path);
                Assert.IsTrue(importer.forceToMono, path + " should be imported as mono.");
                Assert.AreEqual(
                    AudioSampleRateSetting.PreserveSampleRate,
                    importer.defaultSampleSettings.sampleRateSetting,
                    path + " must keep the generated rate instead of being resampled.");

                // Decompress-on-load for both kinds: the Web manual supports it, and a
                // CompressedInMemory loop played again after a scene change grew the managed heap
                // in the project's own 20-visit memory test (Stage 13c-1).
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType,
                    path + " has the wrong load type.");

                // The default (non-platform) format is ours to choose; the WebGL override is
                // normalised by Unity to AAC on its own (manual: "Unity WebGL imports AudioClip
                // files in the AAC format"), so asserting it here would test Unity, not us.
                Assert.AreEqual(
                    AudioCueTable.IsMusic(cue) ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM,
                    importer.defaultSampleSettings.compressionFormat,
                    path + " has the wrong authored compression format.");
            }
        }

        [Test]
        public void TheTableRefusesAClipCountThatCannotIndexEveryCue()
        {
            Assert.DoesNotThrow(() => AudioCueTable.ValidateClipCount(AudioCueTable.Count));
            Assert.Throws<InvalidOperationException>(
                () => AudioCueTable.ValidateClipCount(AudioCueTable.Count - 1));
        }

        [Test]
        public void OnlyTheAmbienceCuesAreMusic()
        {
            Assert.IsTrue(AudioCueTable.IsMusic(AudioCue.MusicCamp));
            Assert.IsTrue(AudioCueTable.IsMusic(AudioCue.MusicBattle));
            Assert.IsFalse(AudioCueTable.IsMusic(AudioCue.Hit));
            Assert.IsFalse(AudioCueTable.IsMusic(AudioCue.Refuse));
            Assert.AreEqual(string.Empty, AudioCueTable.NameOf((AudioCue)99));
        }

        private static string PathOf(AudioCue cue) =>
            AudioFolder + "/" + AudioCueTable.NameOf(cue) + ".wav";

        /// <summary>Just enough of a RIFF reader to measure the generated 16-bit mono PCM.</summary>
        private readonly struct Wav
        {
            private Wav(int sampleRate, int channels, int bitsPerSample, float seconds, float peak, float rms)
            {
                SampleRate = sampleRate;
                Channels = channels;
                BitsPerSample = bitsPerSample;
                Seconds = seconds;
                Peak = peak;
                Rms = rms;
            }

            internal int SampleRate { get; }

            internal int Channels { get; }

            internal int BitsPerSample { get; }

            internal float Seconds { get; }

            internal float Peak { get; }

            internal float Rms { get; }

            internal static Wav Read(string path)
            {
                byte[] bytes = File.ReadAllBytes(path);

                Assert.AreEqual('R', (char)bytes[0], path + " is not a RIFF file.");
                Assert.AreEqual('W', (char)bytes[8], path + " is not a WAVE file.");

                int sampleRate = 0;
                int channels = 0;
                int bits = 0;
                int dataOffset = -1;
                int dataLength = 0;

                int cursor = 12;
                while (cursor + 8 <= bytes.Length)
                {
                    string id = new string(new[]
                    {
                        (char)bytes[cursor], (char)bytes[cursor + 1],
                        (char)bytes[cursor + 2], (char)bytes[cursor + 3],
                    });
                    int size = BitConverter.ToInt32(bytes, cursor + 4);

                    if (id == "fmt ")
                    {
                        channels = BitConverter.ToInt16(bytes, cursor + 10);
                        sampleRate = BitConverter.ToInt32(bytes, cursor + 12);
                        bits = BitConverter.ToInt16(bytes, cursor + 22);
                    }
                    else if (id == "data")
                    {
                        dataOffset = cursor + 8;
                        dataLength = size;
                        break;
                    }

                    cursor += 8 + size + (size & 1);
                }

                Assert.GreaterOrEqual(dataOffset, 0, path + " has no data chunk.");
                Assert.AreEqual(16, bits, path + " is not 16-bit PCM.");

                int frameCount = dataLength / (2 * Mathf.Max(1, channels));
                float peak = 0f;
                double sumOfSquares = 0d;

                for (int frame = 0; frame < frameCount; frame++)
                {
                    short sample = BitConverter.ToInt16(bytes, dataOffset + (frame * 2 * channels));
                    float value = Mathf.Abs(sample / 32767f);
                    peak = Mathf.Max(peak, value);
                    sumOfSquares += (double)value * value;
                }

                float rms = frameCount == 0 ? 0f : Mathf.Sqrt((float)(sumOfSquares / frameCount));
                float seconds = sampleRate <= 0 ? 0f : frameCount / (float)sampleRate;
                return new Wav(sampleRate, channels, bits, seconds, peak, rms);
            }
        }
    }
}
