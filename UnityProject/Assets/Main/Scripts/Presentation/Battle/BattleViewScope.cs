using System;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Owns the battle view's GameObjects and pooled sprites, and disposes every one of
    /// them when the Battle scene is left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 3 and the Stage 08 acceptance line "ออก Battle แล้ว ECS world และ
    /// subscription ถูก dispose" apply to the view too: leaving the scene must release the
    /// session, the R3 subscriptions, the pooled objects and the generated textures. The
    /// type exists so that "what the view owns" is a list, not a habit — a view that
    /// acquires something without registering it here is what the EditMode leak test
    /// catches.
    /// </para>
    /// <para>
    /// Registration order does not matter; <see cref="Dispose"/> disposes in reverse and
    /// is idempotent.
    /// </para>
    /// </remarks>
    public sealed class BattleViewScope : IDisposable
    {
        private readonly System.Collections.Generic.List<IDisposable> _disposables =
            new System.Collections.Generic.List<IDisposable>(8);

        private bool _disposed;

        /// <summary>How many disposables were registered (a test reads this).</summary>
        public int Count => _disposables.Count;

        /// <summary>True once <see cref="Dispose"/> ran.</summary>
        public bool IsDisposed => _disposed;

        /// <summary>Takes ownership of <paramref name="disposable"/>.</summary>
        public T Own<T>(T disposable)
            where T : IDisposable
        {
            if (disposable is null)
            {
                throw new ArgumentNullException(nameof(disposable));
            }

            if (_disposed)
            {
                disposable.Dispose();
                return disposable;
            }

            _disposables.Add(disposable);
            return disposable;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                _disposables[i].Dispose();
            }

            _disposables.Clear();
        }
    }
}
