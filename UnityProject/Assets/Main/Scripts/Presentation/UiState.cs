using System;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation.Audio;
using R3;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The UI-level state a View may render. Deliberately tiny.
    /// </summary>
    /// <remarks>
    /// <para>
    /// R3 carries <b>only</b> UI-level state here (AGENTS rule 10): a title
    /// string, a diagnostics line, the language, and a "ready" flag. It never
    /// carries per-entity positions or battle frames — those go through the
    /// battle contract's frame buffer, not through observables.
    /// </para>
    /// <para>
    /// Presenters write this; Views read it and assign text. Neither computes
    /// gameplay numbers (AGENTS rule 10) — text here always originated from the
    /// localization table.
    /// </para>
    /// </remarks>
    public sealed class UiState : IDisposable
    {
        /// <summary>Screen heading, e.g. the localized game title.</summary>
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// Caption of the title screen's start control ("เริ่มเล่น").
        /// </summary>
        /// <remarks>
        /// The caption is state like every other string here: it is written by
        /// <see cref="TitlePresenter"/> from the localization table and mirrored into a label by
        /// <see cref="TitleView"/> (AGENTS rules 5 and 10). Since Stage 10d the control it captions is
        /// also the way a run begins, which is why the title screen needs one at all.
        /// </remarks>
        public ReactiveProperty<string> StartLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// Secondary line. Used for build/diagnostics facts that are not
        /// player-facing content (version, config hash, smoke verdict), so it is
        /// the one string that is not required to come from localization.
        /// </summary>
        public ReactiveProperty<string> Diagnostics { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Language player-facing text is resolved in.</summary>
        public ReactiveProperty<Language> Language { get; } =
            new ReactiveProperty<Language>(ProjectSurvive.Core.Domain.Config.Language.Th);

        /// <summary>Master audio volume, 0..1 (Stage 12).</summary>
        public ReactiveProperty<float> MasterVolume { get; } = new ReactiveProperty<float>(1f);

        /// <summary>
        /// Music bus level, 0..1, relative to the master (Stage 13c-2a).
        /// </summary>
        /// <remarks>
        /// The <see cref="MasterVolume"/> is the engine listener's volume; this is what the music
        /// source multiplies by it. Two levels rather than one because a player who wants the
        /// ambience quieter should not also make the hits unhearable.
        /// </remarks>
        public ReactiveProperty<float> MusicVolume { get; } =
            new ReactiveProperty<float>(ProjectSurvive.Core.Application.Settings.GameSettings.DefaultMusicVolume);

        /// <summary>Effects bus level, 0..1, relative to the master (Stage 13c-2a).</summary>
        public ReactiveProperty<float> SfxVolume { get; } =
            new ReactiveProperty<float>(ProjectSurvive.Core.Application.Settings.GameSettings.DefaultSfxVolume);

        /// <summary>The battle speed a new floor starts at (1 or 2).</summary>
        public ReactiveProperty<float> DefaultBattleSpeed { get; } = new ReactiveProperty<float>(1f);

        /// <summary>
        /// True when the last save failed; a screen draws a warning from this rather than
        /// claiming the game is saved (Stage 12).
        /// </summary>
        public ReactiveProperty<bool> SaveFailed { get; } = new ReactiveProperty<bool>(false);

        /// <summary>Settings panel heading (localized).</summary>
        public ReactiveProperty<string> SettingsTitle { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The volume row's caption with its current value (localized).</summary>
        public ReactiveProperty<string> VolumeLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The music row's caption with its current value (localized, Stage 13c-2a).</summary>
        public ReactiveProperty<string> MusicLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The effects row's caption with its current value (localized, Stage 13c-2a).</summary>
        public ReactiveProperty<string> SfxLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The language row's caption with its current value (localized).</summary>
        public ReactiveProperty<string> LanguageLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The starting-speed row's caption with its current value (localized).</summary>
        public ReactiveProperty<string> SpeedLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The settings panel's close caption (localized).</summary>
        public ReactiveProperty<string> SettingsCloseLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The settings panel's open caption (localized).</summary>
        public ReactiveProperty<string> SettingsOpenLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// Captions of the "lower this level" and "raise this level" controls, shared by every row
        /// (Stage 13c-2a). Symbols, but still localization keys: AGENTS rule 5 does not carve out
        /// punctuation, and the glyph audit then measures them like any other label.
        /// </summary>
        public ReactiveProperty<string> DecreaseLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the "raise this level" control.</summary>
        public ReactiveProperty<string> IncreaseLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// Sound cues a screen wants played, as UI-level events (Stage 13c-1).
        /// </summary>
        /// <remarks>
        /// A presenter publishes "this command was accepted" here instead of touching an
        /// <see cref="AudioDirector"/>: the presenter stays free of scene objects (AGENTS rule 10),
        /// and the one thing that owns the audio device subscribes. Nothing in the game reads this
        /// channel, so a missing listener costs a sound and no state.
        /// </remarks>
        public Subject<AudioCue> Cues { get; } = new Subject<AudioCue>();

        /// <summary>True once the root scope has finished booting.</summary>
        public ReactiveProperty<bool> Ready { get; } = new ReactiveProperty<bool>(false);

        public void Dispose()
        {
            Title.Dispose();
            StartLabel.Dispose();
            Diagnostics.Dispose();
            Language.Dispose();
            MasterVolume.Dispose();
            MusicVolume.Dispose();
            SfxVolume.Dispose();
            DefaultBattleSpeed.Dispose();
            SaveFailed.Dispose();
            SettingsTitle.Dispose();
            VolumeLabel.Dispose();
            MusicLabel.Dispose();
            SfxLabel.Dispose();
            LanguageLabel.Dispose();
            SpeedLabel.Dispose();
            SettingsCloseLabel.Dispose();
            SettingsOpenLabel.Dispose();
            DecreaseLabel.Dispose();
            IncreaseLabel.Dispose();
            Cues.Dispose();
            Ready.Dispose();
        }
    }
}
