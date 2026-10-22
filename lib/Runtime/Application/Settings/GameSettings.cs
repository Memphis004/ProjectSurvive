using System;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Application.Settings
{
    /// <summary>
    /// The player's three settings (Stage 12): master volume, language and the battle
    /// speed a new floor starts at.
    /// </summary>
    /// <remarks>
    /// Plain values, validated on every setter. The UI layer mirrors them into R3; this
    /// type is the application state behind that mirror, so a settings screen never has
    /// to invent a range or a language id.
    /// </remarks>
    public sealed class GameSettings
    {
        /// <summary>Lowest selectable volume.</summary>
        public const float MinVolume = 0f;

        /// <summary>Highest selectable volume.</summary>
        public const float MaxVolume = 1f;

        private float _masterVolume = 1f;
        private Language _language = Language.Th;
        private float _defaultBattleSpeed = 1f;
        private bool _tutorialCompleted;

        /// <summary>Master audio volume, clamped to <see cref="MinVolume"/>..<see cref="MaxVolume"/>.</summary>
        public float MasterVolume => _masterVolume;

        /// <summary>The language player-facing text is resolved in.</summary>
        public Language Language => _language;

        /// <summary>The multiplier a battle starts at (one of 1 or 2).</summary>
        public float DefaultBattleSpeed => _defaultBattleSpeed;

        /// <summary>
        /// True once the first-run checklist has been finished (Stage 13b). A setting rather than
        /// run state: onboarding belongs to the player, not to the run that happened to teach it,
        /// and no game rule reads it — it only decides whether the camp explains itself.
        /// </summary>
        public bool TutorialCompleted => _tutorialCompleted;

        /// <summary>Sets the volume, clamped rather than refused: a slider's endpoints are honest.</summary>
        public void SetVolume(float value)
        {
            if (float.IsNaN(value))
            {
                return;
            }

            _masterVolume = value < MinVolume ? MinVolume : value > MaxVolume ? MaxVolume : value;
        }

        /// <summary>Sets the language to one the localization table has.</summary>
        public void SetLanguage(Language language)
        {
            if (language is Language.Th or Language.En)
            {
                _language = language;
            }
        }

        /// <summary>Sets the starting battle speed; anything but 1 or 2 is ignored.</summary>
        public void SetDefaultBattleSpeed(float value)
        {
            if (value == 1f || value == 2f)
            {
                _defaultBattleSpeed = value;
            }
        }

        /// <summary>
        /// Records that the first-run checklist is finished. One-way on purpose: there is no
        /// "show the tutorial again" control in the prototype, so nothing clears it here.
        /// </summary>
        public void SetTutorialCompleted(bool completed) => _tutorialCompleted = completed;

        /// <summary>A copy, so a caller cannot mutate the state the service holds.</summary>
        public GameSettings Clone() => new GameSettings
        {
            _masterVolume = _masterVolume,
            _language = _language,
            _defaultBattleSpeed = _defaultBattleSpeed,
            _tutorialCompleted = _tutorialCompleted,
        };

        /// <inheritdoc />
        public override string ToString() =>
            "volume " + _masterVolume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + " language " + _language + " speed " + _defaultBattleSpeed
            + " tutorial " + (_tutorialCompleted ? "done" : "pending");

        /// <summary>True when the values are inside their documented ranges.</summary>
        public bool IsValid =>
            !float.IsNaN(_masterVolume) && _masterVolume >= MinVolume && _masterVolume <= MaxVolume
            && _language is Language.Th or Language.En
            && (_defaultBattleSpeed == 1f || _defaultBattleSpeed == 2f);
    }
}
