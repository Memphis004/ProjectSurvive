using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProjectSurvive.Core.Application.Settings;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation.Flow;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The settings screen's logic: volume, language and the battle's starting speed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Language changes are applied in one place and are <b>immediate</b>: the presenter writes
    /// <see cref="UiState.Language"/>, which every subscribed label mirrors, and updates the
    /// localization service in the same call, so the next <c>Get</c> is in the new language. The
    /// switch is R3-level UI state (AGENTS rule 10) and carries no game rule.
    /// </para>
    /// <para>
    /// Persistence is fire-and-forget with a cancellation token owned by the presenter, which
    /// outlives every scene. A failed write sets <see cref="UiState.SaveFailed"/>; the settings
    /// themselves still apply for this session.
    /// </para>
    /// </remarks>
    public sealed class SettingsPresenter : IDisposable
    {
        private readonly SettingsService _settings;
        private readonly UiState _ui;
        private readonly LocalizationService _text;
        private readonly GameSave _save;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        public SettingsPresenter(SettingsService settings, UiState ui, LocalizationService text, GameSave save)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            ApplyToUi(_settings.Current);
        }

        /// <summary>Pushes the stored values into the observable UI state.</summary>
        public void Refresh() => ApplyToUi(_settings.Current);

        /// <summary>Changes the master volume by <paramref name="delta"/> and saves it.</summary>
        public void AdjustVolume(float delta)
        {
            float value = Mathf.Clamp01(_settings.Current.MasterVolume + delta);
            _settings.Apply(settings => settings.SetVolume(value));
            ApplyToUi(_settings.Current);
            RequestSave();
        }

        /// <summary>Switches Thai ↔ English immediately and saves the choice.</summary>
        public void ToggleLanguage()
        {
            Language next = _settings.Current.Language == Language.Th ? Language.En : Language.Th;
            _settings.Apply(settings => settings.SetLanguage(next));
            ApplyToUi(_settings.Current);
            RequestSave();
        }

        /// <summary>Switches the battle's starting speed between 1× and 2×.</summary>
        public void ToggleSpeed()
        {
            float next = _settings.Current.DefaultBattleSpeed == 1f ? 2f : 1f;
            _settings.Apply(settings => settings.SetDefaultBattleSpeed(next));
            ApplyToUi(_settings.Current);
            RequestSave();
        }

        /// <summary>Writes the current settings and updates the persistence report.</summary>
        public UniTaskVoid Save()
        {
            SaveAsync(_lifetime.Token).Forget();
            return default;
        }

        private async UniTaskVoid SaveAsync(CancellationToken token)
        {
            try
            {
                await _settings.SaveAsync(token);
                _save.PublishReport();
            }
            catch (OperationCanceledException)
            {
                // The app is going away; the in-memory settings are already applied.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void RequestSave() => Save();

        private void ApplyToUi(GameSettings settings)
        {
            _ui.MasterVolume.Value = settings.MasterVolume;
            _ui.Language.Value = settings.Language;
            _ui.DefaultBattleSpeed.Value = settings.DefaultBattleSpeed;
            _text.Language = settings.Language;
            AudioListener.volume = settings.MasterVolume;

            _ui.SettingsTitle.Value = _text.Get("ui.settings.title");
            _ui.SettingsOpenLabel.Value = _text.Get("ui.settings.open");
            _ui.SettingsCloseLabel.Value = _text.Get("ui.settings.close");

            int percent = (int)Math.Round(settings.MasterVolume * 100f, MidpointRounding.AwayFromZero);
            _ui.VolumeLabel.Value = string.Format(
                CultureInfo.InvariantCulture, _text.Get("ui.settings.volume.format"), percent);

            string language = _text.Get(
                settings.Language == Language.Th ? "ui.settings.language.th" : "ui.settings.language.en");
            _ui.LanguageLabel.Value = string.Format(
                CultureInfo.InvariantCulture, _text.Get("ui.settings.language.format"), language);

            string speed = settings.DefaultBattleSpeed == 1f ? "1x" : "2x";
            _ui.SpeedLabel.Value = string.Format(
                CultureInfo.InvariantCulture, _text.Get("ui.settings.speed.format"), speed);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
