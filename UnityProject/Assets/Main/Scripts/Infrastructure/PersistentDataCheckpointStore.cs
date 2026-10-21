using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ProjectSurvive.Core.Application.Save;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using UnityEngine;

namespace ProjectSurvive.Game.Infrastructure
{
    /// <summary>
    /// The shell's <see cref="ICheckpointStore"/>: files under
    /// <c>Application.persistentDataPath</c>, which Unity backs with IndexedDB on WebGL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file work is the engine-free <see cref="FileCheckpointStore"/>'s. What this adds is
    /// the platform half: on WebGL it asks the page whether IndexedDB exists, and
    /// <see cref="FlushAsync"/> waits for Emscripten's <c>FS.syncfs</c> to finish writing the
    /// IDBFS mount. That is the "wait for the save before entering Battle" half of Stage 12,
    /// and it is why the game does not need an unload callback.
    /// </para>
    /// <para>
    /// <b>The flush is polled, not called back.</b> <c>FS.syncfs</c> finishes on a browser
    /// event, and a <see cref="Task"/> completed there did not resume the awaiting code in the
    /// WebGL player — the write reached IndexedDB (the page ack arrived) while every caller
    /// waited forever, with no exception to report. So the page only records the outcome and
    /// <see cref="Pump"/> reads it once per frame from a Unity update, where the awaiting
    /// continuation is allowed to run inline. That is the same reason the rest of the game's
    /// async work is driven by the player loop.
    /// </para>
    /// <para>
    /// The jslib function names and <c>FS.syncfs</c> were checked against the installed Unity
    /// 6000.3.9f1 WebGL support files (<c>BuildTools/lib/FileSystem.js</c> and
    /// <c>BuildTools/prejs/IdbFs.js</c>), not guessed.
    /// </para>
    /// </remarks>
    public sealed class PersistentDataCheckpointStore : FileCheckpointStore
    {
        private const int FlushIdle = 0;
        private const int FlushPending = 1;
        private const int FlushDone = 2;
        private const int FlushFailed = 3;

        /// <summary>
        /// How long a flush may stay in flight before it is reported as failed. IndexedDB can be
        /// blocked mid-write (a browser policy, a quota error the page never reports); a save that
        /// waited forever would also keep the camp from ever departing.
        /// </summary>
        private const float FlushTimeoutSeconds = 10f;

        private readonly object _flushGate = new object();
        private TaskCompletionSource<CheckpointStoreWrite>? _flush;
        private float _flushElapsed;
        private volatile bool _flushPending;

        /// <summary>Creates the store under Unity's persistent data directory.</summary>
        public PersistentDataCheckpointStore()
            : base(Application.persistentDataPath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (ProjectSurvive_StorageAvailable() == 0)
            {
                IsAvailable = false;
                UnavailableReason = "IndexedDB is not available";
            }

            var receiverObject = new GameObject("ProjectSurviveSaveFlush");
            UnityEngine.Object.DontDestroyOnLoad(receiverObject);
            receiverObject.AddComponent<WebGlFlushReceiver>().Bind(this);
#endif
        }

        /// <inheritdoc />
        public override Task<CheckpointStoreWrite> FlushAsync(CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return Task.FromResult(CheckpointStoreWrite.Failed(CheckpointError.StorageUnavailable));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(CheckpointStoreWrite.Failed(CheckpointError.WriteFailed));
            }

            lock (_flushGate)
            {
                if (_flush is not null)
                {
                    // One flush in flight; a second caller waits on the same one.
                    return _flush.Task;
                }

                // No RunContinuationsAsynchronously: it would hand the continuation to a thread
                // pool this player does not have. The default inlines the await when the store
                // completes the task from `Pump`, which runs inside a frame.
                _flush = new TaskCompletionSource<CheckpointStoreWrite>();
                _flushElapsed = 0f;
                _flushPending = true;
            }

            try
            {
                ProjectSurvive_FlushBegin();
            }
            catch (Exception)
            {
                Complete(CheckpointStoreWrite.Failed(CheckpointError.WriteFailed));
            }

            return _flush?.Task
                ?? Task.FromResult(CheckpointStoreWrite.Failed(CheckpointError.WriteFailed));
#else
            return base.FlushAsync(cancellationToken);
#endif
        }

        /// <summary>
        /// Advances a flush that is in flight. The receiver calls this once per frame; when nothing
        /// is being written it costs one boolean read.
        /// </summary>
        internal void Pump(float deltaSeconds)
        {
            if (!_flushPending)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            int state = ProjectSurvive_FlushPoll();
            if (state == FlushPending || state == FlushIdle)
            {
                _flushElapsed += deltaSeconds;
                if (_flushElapsed >= FlushTimeoutSeconds)
                {
                    Complete(CheckpointStoreWrite.Failed(CheckpointError.WriteFailed));
                }

                return;
            }

            Complete(state == FlushDone
                ? CheckpointStoreWrite.Ok()
                : CheckpointStoreWrite.Failed(CheckpointError.WriteFailed));
#else
            Complete(CheckpointStoreWrite.Ok());
#endif
        }

        private void Complete(CheckpointStoreWrite result)
        {
            TaskCompletionSource<CheckpointStoreWrite>? pending;
            lock (_flushGate)
            {
                pending = _flush;
                _flush = null;
                _flushPending = false;
            }

            pending?.TrySetResult(result);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int ProjectSurvive_StorageAvailable();

        [DllImport("__Internal")]
        private static extern void ProjectSurvive_FlushBegin();

        [DllImport("__Internal")]
        private static extern int ProjectSurvive_FlushPoll();
#endif
    }

    /// <summary>
    /// Pumps a store's pending IndexedDB flush. It exists only in a WebGL player and lives for the
    /// whole session, so a save that spans a scene change still completes.
    /// </summary>
    public sealed class WebGlFlushReceiver : MonoBehaviour
    {
        private PersistentDataCheckpointStore? _store;

        /// <summary>Points the receiver at the store it pumps.</summary>
        public void Bind(PersistentDataCheckpointStore store) => _store = store;

        private void Update()
        {
            _store?.Pump(Time.unscaledDeltaTime);
        }
    }
}
