using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProjectSurvive.Core.Application.Save;
using ProjectSurvive.Core.Domain.Run;

namespace ProjectSurvive.Core.Infrastructure.Save
{
    /// <summary>
    /// The desktop (and Unity persistent-data) implementation of
    /// <see cref="ICheckpointStore"/>: one file per key under a directory, with the
    /// previous value kept as <c>key.bak</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file work is synchronous because Unity's WebGL filesystem is a synchronous
    /// in-memory mount; the methods still return tasks so the port has one shape for
    /// both targets. Durability is a separate step (<see cref="FlushAsync"/>), which the
    /// WebGL adapter overrides to flush IndexedDB.
    /// </para>
    /// <para>
    /// A write is temp-then-replace: a crash in the middle leaves the old primary intact
    /// and a stray <c>.tmp</c>, never a half-written save that looks valid.
    /// </para>
    /// </remarks>
    public class FileCheckpointStore : ICheckpointStore
    {
        private readonly string _root;

        /// <summary>Creates a store rooted at <paramref name="rootDirectory"/>.</summary>
        public FileCheckpointStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A checkpoint directory is required.", nameof(rootDirectory));
            }

            _root = rootDirectory;
            try
            {
                Directory.CreateDirectory(_root);
                IsAvailable = true;
            }
            catch (Exception exception)
            {
                IsAvailable = false;
                UnavailableReason = exception.GetType().Name + ": " + exception.Message;
            }
        }

        /// <summary>The directory this store writes to.</summary>
        public string RootDirectory => _root;

        /// <inheritdoc />
        public bool IsAvailable { get; protected set; }

        /// <inheritdoc />
        public string UnavailableReason { get; protected set; } = string.Empty;

        /// <inheritdoc />
        public Task<CheckpointStoreRead> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return Task.FromResult(CheckpointStoreRead.Failed(CheckpointError.StorageUnavailable));
            }

            string path = PathFor(key);
            try
            {
                if (!File.Exists(path))
                {
                    return Task.FromResult(CheckpointStoreRead.NotFound());
                }

                return Task.FromResult(CheckpointStoreRead.Ok(File.ReadAllBytes(path)));
            }
            catch (Exception)
            {
                return Task.FromResult(CheckpointStoreRead.Failed(CheckpointError.StorageUnavailable));
            }
        }

        /// <inheritdoc />
        public Task<CheckpointStoreWrite> WriteAsync(
            string key, byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (!IsAvailable)
            {
                return Task.FromResult(CheckpointStoreWrite.Failed(CheckpointError.StorageUnavailable));
            }

            string path = PathFor(key);
            string backup = path + ".bak";
            string temp = path + ".tmp";

            try
            {
                if (File.Exists(path))
                {
                    File.Copy(path, backup, overwrite: true);
                }

                File.WriteAllBytes(temp, bytes);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, destinationBackupFileName: null);
                    }
                    catch (Exception)
                    {
                        // Some virtual filesystems (Unity's WebGL mount among them) do not
                        // implement replace. The backup above already holds the old primary,
                        // so a copy is still a safe fallback.
                        File.Copy(temp, path, overwrite: true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }

                return Task.FromResult(CheckpointStoreWrite.Ok());
            }
            catch (Exception)
            {
                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch (Exception)
                {
                    // Best effort: the primary was not touched.
                }

                return Task.FromResult(CheckpointStoreWrite.Failed(CheckpointError.WriteFailed));
            }
        }

        /// <inheritdoc />
        public Task<CheckpointStoreWrite> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return Task.FromResult(CheckpointStoreWrite.Failed(CheckpointError.StorageUnavailable));
            }

            string path = PathFor(key);
            try
            {
                DeleteIfPresent(path);
                DeleteIfPresent(path + ".bak");
                DeleteIfPresent(path + ".tmp");
                return Task.FromResult(CheckpointStoreWrite.Ok());
            }
            catch (Exception)
            {
                return Task.FromResult(CheckpointStoreWrite.Failed(CheckpointError.DeleteFailed));
            }
        }

        /// <inheritdoc />
        public virtual Task<CheckpointStoreWrite> FlushAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CheckpointStoreWrite.Ok());

        /// <summary>Maps a logical key to a file path, refusing a key that escapes the root.</summary>
        protected string PathFor(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A checkpoint key is required.", nameof(key));
            }

            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';
                if (!allowed)
                {
                    throw new ArgumentException("Checkpoint key contains an illegal character: " + c, nameof(key));
                }
            }

            return Path.Combine(_root, key);
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
