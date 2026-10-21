using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;

namespace ProjectSurvive.Core.Application.Save
{
    /// <summary>
    /// Reads and writes a run's checkpoint: schema/config validation, corrupt-payload
    /// fallback to the backup, and one writer at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Serialized writes.</b> Every save request gets a revision. A request that finds a
    /// newer request has arrived while it waited skips its own write, so an older payload
    /// can never land after a newer one — the race is resolved before the bytes touch
    /// storage, not by hoping the filesystem's ordering agrees.
    /// </para>
    /// <para>
    /// <b>Validation order is deliberate.</b> Schema first (a newer/older format is not
    /// interpreted at all), then the config hash, then the state's own bounds. Only after
    /// all three does <see cref="RunSession.RestoreCheckpoint"/> see the value, and it
    /// validates again because a checkpoint is untrusted input.
    /// </para>
    /// <para>
    /// A run that has ended deletes its checkpoint. That is why a reload at the Result
    /// screen starts a new run instead of resurrecting a finished one.
    /// </para>
    /// <para>
    /// <b>The awaits here capture the caller's context.</b> A flush can take real time on
    /// WebGL, and that player is single-threaded with no thread pool: a continuation marked
    /// <c>ConfigureAwait(false)</c> is scheduled on the default scheduler and simply never
    /// runs. Measured in Stage 12 — the checkpoint reached IndexedDB while every save call
    /// waited forever, with nothing to report. Capturing the context resumes the save on the
    /// player loop; a host with no context (the xUnit tests) still resumes inline.
    /// </para>
    /// </remarks>
    public sealed class CheckpointService
    {
        /// <summary>Storage key of the active run checkpoint.</summary>
        public const string CheckpointKey = "run-checkpoint";

        /// <summary>Storage key of the player settings.</summary>
        public const string SettingsKey = "settings";

        private readonly IGameConfig _config;
        private readonly IRunCheckpointCodec _codec;
        private readonly ICheckpointStore _store;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private long _requestedRevision;
        private long _writtenRevision;

        /// <summary>Creates the service.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CheckpointService(IGameConfig config, IRunCheckpointCodec codec, ICheckpointStore store)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>The revision of the last checkpoint that was actually written.</summary>
        public long WrittenRevision => Interlocked.Read(ref _writtenRevision);

        /// <summary>True when the backing store can be used at all.</summary>
        public bool IsStorageAvailable => _store.IsAvailable;

        /// <summary>Why storage is unavailable, or an empty string.</summary>
        public string StorageUnavailableReason => _store.UnavailableReason;

        /// <summary>
        /// Saves <paramref name="run"/>. A finished run deletes its checkpoint instead of
        /// storing one.
        /// </summary>
        public async Task<CheckpointWrite> SaveAsync(RunSession run, CancellationToken cancellationToken = default)
        {
            if (run is null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (!_store.IsAvailable)
            {
                return CheckpointWrite.Failure(CheckpointError.StorageUnavailable);
            }

            long revision = Interlocked.Increment(ref _requestedRevision);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                // A newer request arrived while this one waited: let the newer one win
                // rather than writing a state that has already been superseded.
                if (revision < Interlocked.Read(ref _requestedRevision))
                {
                    return CheckpointWrite.SupersededResult(revision);
                }

                if (run.IsRunOver)
                {
                    CheckpointStoreWrite deleted = await _store.DeleteAsync(CheckpointKey, cancellationToken);
                    if (!deleted.IsSuccess)
                    {
                        return CheckpointWrite.Failure(deleted.ErrorCode);
                    }

                    Interlocked.Exchange(ref _writtenRevision, revision);
                    return CheckpointWrite.Success(revision);
                }

                RunCheckpoint checkpoint;
                try
                {
                    checkpoint = run.CaptureCheckpoint();
                }
                catch (InvalidOperationException)
                {
                    return CheckpointWrite.Failure(CheckpointError.InvalidState);
                }

                checkpoint.ConfigHash = _config.ConfigHash;
                checkpoint.ConfigSchemaVersion = _config.SchemaVersion;

                byte[] bytes;
                try
                {
                    bytes = _codec.Encode(checkpoint);
                }
                catch (Exception)
                {
                    return CheckpointWrite.Failure(CheckpointError.InvalidState);
                }

                CheckpointStoreWrite written = await _store.WriteAsync(CheckpointKey, bytes, cancellationToken);
                if (!written.IsSuccess)
                {
                    return CheckpointWrite.Failure(written.ErrorCode);
                }

                CheckpointStoreWrite flushed = await _store.FlushAsync(cancellationToken);
                if (!flushed.IsSuccess)
                {
                    return CheckpointWrite.Failure(flushed.ErrorCode);
                }

                Interlocked.Exchange(ref _writtenRevision, revision);
                return CheckpointWrite.Success(revision);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Deletes the active checkpoint and its backup.</summary>
        public async Task<CheckpointWrite> DeleteAsync(CancellationToken cancellationToken = default)
        {
            long revision = Interlocked.Increment(ref _requestedRevision);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_store.IsAvailable)
                {
                    return CheckpointWrite.Failure(CheckpointError.StorageUnavailable);
                }

                CheckpointStoreWrite deleted = await _store.DeleteAsync(CheckpointKey, cancellationToken);
                if (!deleted.IsSuccess)
                {
                    return CheckpointWrite.Failure(deleted.ErrorCode);
                }

                Interlocked.Exchange(ref _writtenRevision, revision);
                return CheckpointWrite.Success(revision);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Loads and validates the active checkpoint.
        /// </summary>
        /// <remarks>
        /// A corrupt primary is retried against the backup. A config mismatch is
        /// <b>not</b> retried: the backup is almost certainly from the same build, and
        /// silently loading different data is worse than reporting the mismatch.
        /// </remarks>
        public async Task<CheckpointLoad> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!_store.IsAvailable)
            {
                return CheckpointLoad.Failure(CheckpointError.StorageUnavailable);
            }

            CheckpointStoreRead primary = await _store.ReadAsync(CheckpointKey, cancellationToken);
            if (!primary.IsSuccess)
            {
                return CheckpointLoad.Failure(primary.ErrorCode);
            }

            if (!primary.Found)
            {
                return CheckpointLoad.Failure(CheckpointError.NotFound);
            }

            CheckpointLoad decoded = TryDecode(primary.Bytes!, recoveredFromBackup: false);
            if (decoded.IsSuccess || decoded.ErrorCode != CheckpointError.Corrupt)
            {
                return decoded;
            }

            CheckpointStoreRead backup = await _store.ReadAsync(CheckpointKey + ".bak", cancellationToken);
            if (!backup.IsSuccess || !backup.Found)
            {
                return decoded;
            }

            return TryDecode(backup.Bytes!, recoveredFromBackup: true);
        }

        private CheckpointLoad TryDecode(byte[] bytes, bool recoveredFromBackup)
        {
            RunCheckpoint checkpoint;
            try
            {
                checkpoint = _codec.Decode(bytes);
            }
            catch (Exception)
            {
                return CheckpointLoad.Failure(CheckpointError.Corrupt);
            }

            if (checkpoint is null || checkpoint.Backpack is null || checkpoint.ShopSlots is null)
            {
                return CheckpointLoad.Failure(CheckpointError.Corrupt);
            }

            if (checkpoint.SchemaVersion > RunCheckpointFormat.SchemaVersion)
            {
                return CheckpointLoad.Failure(CheckpointError.SchemaTooNew);
            }

            if (checkpoint.SchemaVersion < RunCheckpointFormat.MinimumSchemaVersion)
            {
                return CheckpointLoad.Failure(CheckpointError.SchemaTooOld);
            }

            if (!string.Equals(checkpoint.ConfigHash, _config.ConfigHash, StringComparison.Ordinal)
                || checkpoint.ConfigSchemaVersion != _config.SchemaVersion)
            {
                return CheckpointLoad.Failure(CheckpointError.ConfigMismatch);
            }

            if (string.IsNullOrEmpty(checkpoint.RunId)
                || checkpoint.FloorIndex < 1
                || checkpoint.Hp < 0
                || checkpoint.Bia < 0
                || checkpoint.ShopSlots.Count == 0)
            {
                return CheckpointLoad.Failure(CheckpointError.InvalidState);
            }

            return CheckpointLoad.Success(checkpoint, recoveredFromBackup);
        }
    }

    /// <summary>Outcome of a checkpoint load.</summary>
    public readonly struct CheckpointLoad
    {
        private CheckpointLoad(RunCheckpoint? checkpoint, string errorCode, bool recoveredFromBackup)
        {
            Checkpoint = checkpoint;
            ErrorCode = errorCode;
            RecoveredFromBackup = recoveredFromBackup;
        }

        public RunCheckpoint? Checkpoint { get; }

        public string ErrorCode { get; }

        public bool RecoveredFromBackup { get; }

        public bool IsSuccess => Checkpoint is not null;

        public static CheckpointLoad Success(RunCheckpoint checkpoint, bool recoveredFromBackup) =>
            new CheckpointLoad(checkpoint, string.Empty, recoveredFromBackup);

        public static CheckpointLoad Failure(string errorCode) =>
            new CheckpointLoad(null, errorCode, false);
    }

    /// <summary>Outcome of a checkpoint write or delete.</summary>
    public readonly struct CheckpointWrite
    {
        private CheckpointWrite(bool isSuccess, bool superseded, long revision, string errorCode)
        {
            IsSuccess = isSuccess;
            Superseded = superseded;
            Revision = revision;
            ErrorCode = errorCode;
        }

        public bool IsSuccess { get; }

        /// <summary>True when a newer save request won and this one wrote nothing.</summary>
        public bool Superseded { get; }

        public long Revision { get; }

        public string ErrorCode { get; }

        public static CheckpointWrite Success(long revision) => new CheckpointWrite(true, false, revision, string.Empty);

        public static CheckpointWrite SupersededResult(long revision) =>
            new CheckpointWrite(false, true, revision, string.Empty);

        public static CheckpointWrite Failure(string errorCode) => new CheckpointWrite(false, false, 0, errorCode);
    }
}
