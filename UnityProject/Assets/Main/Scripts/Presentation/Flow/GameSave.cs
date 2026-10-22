using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Save;
using ProjectSurvive.Core.Application.Settings;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Flow
{
    /// <summary>
    /// Owns the run checkpoint and the player settings for the whole app (Stage 12).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boot calls <see cref="InitializeAsync"/> once. It loads the settings (and applies
    /// them) and reads the active checkpoint. A valid checkpoint is resumed by
    /// <see cref="TryResume"/>, which creates the run with the <b>saved</b> seed and lets
    /// <see cref="RunSession.RestoreCheckpoint"/> validate the rest. A corrupt, older, newer
    /// or config-mismatched checkpoint is reported and left alone; the game opens the title
    /// screen instead of silently starting a different run.
    /// </para>
    /// <para>
    /// Saves are delegated to <see cref="CheckpointService"/>, which serializes writes and
    /// therefore keeps an older payload from landing after a newer one. The camp debounces
    /// requests; departure awaits <see cref="SaveAsync"/> before the scene changes, so the
    /// floor that starts is the floor the checkpoint describes.
    /// </para>
    /// <para>
    /// A save failure updates <see cref="UiState.SaveFailed"/> and the published report. Nothing
    /// here ever claims success when the store said no (AGENTS rule 7 and the stage's storage
    /// acceptance).
    /// </para>
    /// </remarks>
    public sealed class GameSave
    {
        private readonly CheckpointService _checkpoints;
        private readonly SettingsService _settings;
        private readonly UiState _ui;
        private readonly LocalizationService _text;

        private CheckpointLoad _pending;
        private bool _lastSaveAttempted;
        private bool _hasSavedCheckpoint;

        public GameSave(
            CheckpointService checkpoints,
            SettingsService settings,
            UiState ui,
            LocalizationService text)
        {
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>True when storage can be read and written.</summary>
        public bool StorageAvailable => _checkpoints.IsStorageAvailable;

        /// <summary>Why storage is unavailable, or an empty string.</summary>
        public string StorageUnavailableReason => _checkpoints.StorageUnavailableReason;

        /// <summary>
        /// True when storage holds a run: either one a previous session left (loaded at boot) or one
        /// this session has written.
        /// </summary>
        public bool HasCheckpoint => _pending.IsSuccess || _hasSavedCheckpoint;

        /// <summary>True when the last load found a corrupt primary and used its backup.</summary>
        public bool RecoveredFromBackup => _pending.RecoveredFromBackup;

        /// <summary>True once a checkpoint was restored into a run.</summary>
        public bool Resumed { get; private set; }

        /// <summary>The last failure code, or an empty string.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>True when the last write was attempted and failed.</summary>
        public bool LastSaveFailed => _lastSaveAttempted && LastError.Length > 0;

        /// <summary>The settings currently in effect.</summary>
        public GameSettings Settings => _settings.Current;

        /// <summary>Loads settings and the active checkpoint. Call once, from Boot.</summary>
        public async UniTask InitializeAsync(CancellationToken token)
        {
            SettingsLoad settingsLoad = await _settings.LoadAsync(token);
            Apply(settingsLoad.Settings);
            if (!settingsLoad.IsSuccess && settingsLoad.ErrorCode != CheckpointError.NotFound)
            {
                LastError = settingsLoad.ErrorCode;
                _ui.SaveFailed.Value = true;
            }

            _pending = await _checkpoints.LoadAsync(token);
            if (_pending.IsSuccess)
            {
                LastError = string.Empty;
                _ui.SaveFailed.Value = false;
            }
            else if (_pending.ErrorCode != CheckpointError.NotFound)
            {
                // A bad save is a reason to tell the player, never a reason to start a run
                // from half a save.
                LastError = _pending.ErrorCode;
                _ui.SaveFailed.Value = true;
                Debug.LogWarning("Checkpoint was not resumed: " + _pending.ErrorCode);
            }

            PublishReport();
        }

        /// <summary>
        /// Restores the loaded checkpoint into a fresh run. Returns false when there is no
        /// checkpoint or the run refused it, and leaves the run at Title in that case.
        /// </summary>
        public bool TryResume(RunScopeOwner owner)
        {
            if (owner is null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (!_pending.IsSuccess || _pending.Checkpoint is null)
            {
                return false;
            }

            RunSession run = owner.RequireRun(_pending.Checkpoint.Seed);
            Result restored = run.RestoreCheckpoint(_pending.Checkpoint);
            if (restored.IsFailure)
            {
                LastError = restored.Error.Code;
                _ui.SaveFailed.Value = true;
                Debug.LogError("Checkpoint was refused by the run: " + restored.Error.Code);
                PublishReport();
                return false;
            }

            Resumed = true;
            _pending = default;
            _hasSavedCheckpoint = true;
            LastError = string.Empty;
            _ui.SaveFailed.Value = false;
            PublishReport();
            return true;
        }

        /// <summary>
        /// Saves the run, or deletes the checkpoint when the run has ended. Awaited before
        /// the camp enters Battle.
        /// </summary>
        public async UniTask<CheckpointWrite> SaveAsync(RunSession run, CancellationToken token = default)
        {
            if (run is null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            _lastSaveAttempted = true;
            CheckpointWrite written;
            try
            {
                written = await _checkpoints.SaveAsync(run, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                written = CheckpointWrite.Failure(CheckpointError.WriteFailed);
                Debug.LogException(exception);
            }

            if (written.IsSuccess)
            {
                // A finished run deleted its checkpoint instead of storing one.
                _hasSavedCheckpoint = !run.IsRunOver;
            }

            if (written.IsSuccess || written.Superseded)
            {
                LastError = string.Empty;
                _ui.SaveFailed.Value = false;
            }
            else
            {
                LastError = written.ErrorCode;
                _ui.SaveFailed.Value = true;
                Debug.LogWarning("Checkpoint was not saved: " + written.ErrorCode);
            }

            PublishReport();
            return written;
        }

        /// <summary>Deletes the active checkpoint (a run that ended).</summary>
        public async UniTask DeleteAsync(CancellationToken token = default)
        {
            _lastSaveAttempted = true;
            CheckpointWrite deleted = await _checkpoints.DeleteAsync(token);
            if (deleted.IsSuccess)
            {
                _hasSavedCheckpoint = false;
                LastError = string.Empty;
                _ui.SaveFailed.Value = false;
            }
            else
            {
                LastError = deleted.ErrorCode;
                _ui.SaveFailed.Value = true;
            }

            PublishReport();
        }

        /// <summary>Marks the run over so the next save deletes instead of storing it.</summary>
        public UniTask FlushRunEndAsync(RunSession run, CancellationToken token = default) =>
            SaveAsync(run, token);

        /// <summary>Publishes the persistence verdict a browser test reads.</summary>
        public void PublishReport()
        {
            var writer = new JsonReportWriter();
            writer.BeginObject()
                .Write("storageAvailable", StorageAvailable)
                .Write("storageReason", StorageUnavailableReason)
                .Write("checkpointFound", HasCheckpoint)
                .Write("recoveredFromBackup", RecoveredFromBackup)
                .Write("resumed", Resumed)
                .Write("lastSaveAttempted", _lastSaveAttempted)
                .Write("lastSaveOk", _lastSaveAttempted && LastError.Length == 0)
                .Write("error", LastError)
                .Write("language", _ui.Language.Value.ToString())
                .Write("volume", _ui.MasterVolume.Value)
                .Write("defaultSpeed", _ui.DefaultBattleSpeed.Value)
                // Stage 13b: the camp's first-run checklist flag, as the settings file now carries
                // it. Published so a browser test can see that finishing the checklist reached
                // storage, which is the only way that fact survives a reload.
                .Write("tutorialCompleted", _settings.Current.TutorialCompleted)
                .EndObject();

            WebGlBridge.Publish(WebGlBridge.PersistenceGlobal, writer.ToString());
        }

        private void Apply(GameSettings settings)
        {
            _ui.MasterVolume.Value = settings.MasterVolume;
            _ui.Language.Value = settings.Language;
            _ui.DefaultBattleSpeed.Value = settings.DefaultBattleSpeed;
            _text.Language = settings.Language;
            AudioListener.volume = settings.MasterVolume;
        }
    }
}
