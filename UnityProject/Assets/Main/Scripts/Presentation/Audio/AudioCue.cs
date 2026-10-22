using System;

namespace ProjectSurvive.Game.Presentation.Audio
{
    /// <summary>
    /// Every sound the placeholder build can make. The values are the <b>index</b> into
    /// <see cref="AudioCueTable.Names"/> and into the clips authored on <see cref="AudioDirector"/>,
    /// so the asset pipeline and the enum cannot drift apart silently — a missing clip is caught by
    /// an EditMode test rather than by a quiet game (Stage 13c-1).
    /// </summary>
    public enum AudioCue
    {
        /// <summary>A weapon's hit landed on an enemy.</summary>
        Hit = 0,

        /// <summary>The player took damage.</summary>
        Hurt = 1,

        /// <summary>An enemy died.</summary>
        Kill = 2,

        /// <summary>A pickup was collected.</summary>
        Pickup = 3,

        /// <summary>A telegraphed attack started charging.</summary>
        Telegraph = 4,

        /// <summary>The boss changed phase.</summary>
        BossPhase = 5,

        /// <summary>An item was rented from a shelf.</summary>
        Rent = 6,

        /// <summary>A piece was placed on the board.</summary>
        Place = 7,

        /// <summary>A piece was turned.</summary>
        Rotate = 8,

        /// <summary>A merge was confirmed.</summary>
        Merge = 9,

        /// <summary>A command was refused (the run's own typed error).</summary>
        Refuse = 10,

        /// <summary>The camp's ambience. Loops; one at a time.</summary>
        MusicCamp = 11,

        /// <summary>The arena's music. Loops; one at a time.</summary>
        MusicBattle = 12,
    }

    /// <summary>
    /// The names of the clips <c>tools/gen-audio.py</c> writes, in <see cref="AudioCue"/> order.
    /// </summary>
    /// <remarks>
    /// One table, two consumers: the Editor builder loads the assets by these names, and the test
    /// that proves every cue is audible walks the same list. The generator's dictionary is the
    /// third copy, which is why the test checks the <i>assets</i> rather than the script.
    /// </remarks>
    public static class AudioCueTable
    {
        /// <summary>Asset file names (without extension), indexed by <see cref="AudioCue"/>.</summary>
        public static readonly string[] Names =
        {
            "sfx-hit",
            "sfx-hurt",
            "sfx-kill",
            "sfx-pickup",
            "sfx-telegraph",
            "sfx-boss-phase",
            "sfx-rent",
            "sfx-place",
            "sfx-rotate",
            "sfx-merge",
            "sfx-refuse",
            "music-camp",
            "music-battle",
        };

        /// <summary>How many cues the game knows.</summary>
        public static int Count => Names.Length;

        /// <summary>True for the cues that own the music source instead of a voice.</summary>
        public static bool IsMusic(AudioCue cue) =>
            cue == AudioCue.MusicCamp || cue == AudioCue.MusicBattle;

        /// <summary>The asset name for a cue, or an empty string when the value is not a cue.</summary>
        public static string NameOf(AudioCue cue)
        {
            int index = (int)cue;
            return index >= 0 && index < Names.Length ? Names[index] : string.Empty;
        }

        /// <summary>Throws when <paramref name="clipCount"/> cannot index every cue.</summary>
        public static void ValidateClipCount(int clipCount)
        {
            if (clipCount != Names.Length)
            {
                throw new InvalidOperationException(
                    "AudioDirector has " + clipCount + " clips but the cue table names "
                    + Names.Length + "; the clips must be assigned in AudioCue order.");
            }
        }
    }
}
