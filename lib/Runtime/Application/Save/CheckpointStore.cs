using System.Threading;
using System.Threading.Tasks;

namespace ProjectSurvive.Core.Application.Save
{
    /// <summary>
    /// Where checkpoints and settings are persisted. The port is async because the Web
    /// target's IndexedDB flush completes later, even though the desktop file adapter
    /// answers immediately.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A store reports <see cref="IsAvailable"/> rather than pretending a failed write
    /// worked: a browser with storage disabled (private mode, a blocked IndexedDB, a full
    /// quota) must surface a failure, and the game must not claim it saved.
    /// </para>
    /// <para>
    /// The implementation owns the backup rotation: writing <c>key</c> first moves the
    /// current file to <c>key.bak</c>, so a corrupt primary can still fall back to the
    /// previous good save. The service reads <c>key.bak</c> through the same port.
    /// </para>
    /// </remarks>
    public interface ICheckpointStore
    {
        /// <summary>True when the backing storage can be read and written.</summary>
        bool IsAvailable { get; }

        /// <summary>Why <see cref="IsAvailable"/> is false, or an empty string.</summary>
        string UnavailableReason { get; }

        /// <summary>Reads one key. A missing file is <see cref="CheckpointStoreRead.NotFound"/>, not an error.</summary>
        Task<CheckpointStoreRead> ReadAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>Writes one key, rotating the previous value to the backup slot.</summary>
        Task<CheckpointStoreWrite> WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default);

        /// <summary>Deletes one key and its backup. Missing files are success.</summary>
        Task<CheckpointStoreWrite> DeleteAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Waits until the writes above are durable. On the Web target this is the
        /// IndexedDB flush; on desktop it is a completed task.
        /// </summary>
        Task<CheckpointStoreWrite> FlushAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Outcome of reading one key.</summary>
    public readonly struct CheckpointStoreRead
    {
        private CheckpointStoreRead(bool found, byte[]? bytes, string errorCode)
        {
            Found = found;
            Bytes = bytes;
            ErrorCode = errorCode;
        }

        /// <summary>True when the key exists and its bytes were read.</summary>
        public bool Found { get; }

        /// <summary>The bytes, or null when not found or the read failed.</summary>
        public byte[]? Bytes { get; }

        /// <summary>A stable failure code, or an empty string.</summary>
        public string ErrorCode { get; }

        public bool IsSuccess => ErrorCode.Length == 0;

        public static CheckpointStoreRead Ok(byte[] bytes) => new CheckpointStoreRead(true, bytes, string.Empty);

        public static CheckpointStoreRead NotFound() => new CheckpointStoreRead(false, null, string.Empty);

        public static CheckpointStoreRead Failed(string errorCode) => new CheckpointStoreRead(false, null, errorCode);
    }

    /// <summary>Outcome of a write, delete or flush.</summary>
    public readonly struct CheckpointStoreWrite
    {
        private CheckpointStoreWrite(bool isSuccess, string errorCode)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
        }

        public bool IsSuccess { get; }

        public string ErrorCode { get; }

        public static CheckpointStoreWrite Ok() => new CheckpointStoreWrite(true, string.Empty);

        public static CheckpointStoreWrite Failed(string errorCode) => new CheckpointStoreWrite(false, errorCode);
    }
}
