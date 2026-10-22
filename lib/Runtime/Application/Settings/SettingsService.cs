using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectSurvive.Core.Application.Save;
using ProjectSurvive.Core.Domain.Run;

namespace ProjectSurvive.Core.Application.Settings
{
    /// <summary>Turns <see cref="GameSettings"/> into bytes and back.</summary>
    public interface ISettingsCodec
    {
        byte[] Encode(GameSettings settings);

        GameSettings Decode(byte[] bytes);
    }

    /// <summary>
    /// Loads and saves the player's settings. A missing or unreadable settings file
    /// degrades to the defaults; it never blocks the game from starting.
    /// </summary>
    /// <remarks>
    /// The awaits capture the caller's context on purpose: a WebGL flush is the one wait here
    /// that can yield, and that player has no thread pool to run a <c>ConfigureAwait(false)</c>
    /// continuation on. See <c>CheckpointService</c> for the measurement.
    /// </remarks>
    public sealed class SettingsService
    {
        /// <summary>The schema this build writes.</summary>
        /// <remarks>
        /// 2 added the music and effects buses (Stage 13c-2a). A build still reads 1, because the
        /// two new keys are absent there and decode as 0, which is a legal volume: the version is
        /// what tells the codec to fill in the defaults instead of muting an existing player.
        /// </remarks>
        public const int SchemaVersion = 2;

        /// <summary>The oldest schema this build still reads.</summary>
        public const int OldestReadableSchemaVersion = 1;

        private readonly ICheckpointStore _store;
        private readonly ISettingsCodec _codec;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private GameSettings _current = new GameSettings();

        public SettingsService(ICheckpointStore store, ISettingsCodec codec)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        /// <summary>The settings in effect. Always a valid object, even before a load.</summary>
        public GameSettings Current => _current.Clone();

        /// <summary>
        /// Reads the stored settings. Missing, corrupt and unreadable-version settings fall back
        /// to the defaults; storage being unavailable is reported so a screen can warn.
        /// </summary>
        public async Task<SettingsLoad> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!_store.IsAvailable)
            {
                return SettingsLoad.Failure(CheckpointError.StorageUnavailable);
            }

            CheckpointStoreRead read = await _store.ReadAsync(CheckpointService.SettingsKey, cancellationToken);
            if (!read.IsSuccess)
            {
                return SettingsLoad.Failure(read.ErrorCode);
            }

            if (!read.Found)
            {
                return SettingsLoad.Defaults();
            }

            try
            {
                _current = _codec.Decode(read.Bytes!);
            }
            catch (Exception)
            {
                return SettingsLoad.Defaults();
            }

            if (!_current.IsValid)
            {
                _current = new GameSettings();
                return SettingsLoad.Defaults();
            }

            return SettingsLoad.Success(_current.Clone());
        }

        /// <summary>Applies a setting in memory. The caller persists it with <see cref="SaveAsync"/>.</summary>
        public void Apply(Action<GameSettings> change)
        {
            if (change is null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            change(_current);
        }

        /// <summary>Writes the current settings.</summary>
        public async Task<CheckpointWrite> SaveAsync(CancellationToken cancellationToken = default)
        {
            if (!_store.IsAvailable)
            {
                return CheckpointWrite.Failure(CheckpointError.StorageUnavailable);
            }

            byte[] bytes;
            try
            {
                bytes = _codec.Encode(_current);
            }
            catch (Exception)
            {
                return CheckpointWrite.Failure(CheckpointError.WriteFailed);
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                CheckpointStoreWrite written = await _store.WriteAsync(
                    CheckpointService.SettingsKey, bytes, cancellationToken);
                if (!written.IsSuccess)
                {
                    return CheckpointWrite.Failure(written.ErrorCode);
                }

                CheckpointStoreWrite flushed = await _store.FlushAsync(cancellationToken);
                return flushed.IsSuccess
                    ? CheckpointWrite.Success(1)
                    : CheckpointWrite.Failure(flushed.ErrorCode);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    /// <summary>Outcome of loading settings.</summary>
    public readonly struct SettingsLoad
    {
        private SettingsLoad(GameSettings settings, string errorCode, bool found)
        {
            Settings = settings;
            ErrorCode = errorCode;
            Found = found;
        }

        public GameSettings Settings { get; }

        public string ErrorCode { get; }

        /// <summary>True when a stored file was used; false when defaults were returned.</summary>
        public bool Found { get; }

        public bool IsSuccess => ErrorCode.Length == 0;

        public static SettingsLoad Success(GameSettings settings) => new SettingsLoad(settings, string.Empty, true);

        public static SettingsLoad Defaults() => new SettingsLoad(new GameSettings(), string.Empty, false);

        public static SettingsLoad Failure(string errorCode) => new SettingsLoad(new GameSettings(), errorCode, false);
    }
}
