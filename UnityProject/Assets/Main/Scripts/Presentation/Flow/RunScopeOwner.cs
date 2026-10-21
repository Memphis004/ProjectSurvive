using System;
using ProjectSurvive.Core.Application.Run;
using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Flow
{
    /// <summary>
    /// Owns the <b>Run</b> scope: the one place a run is created, and the one place it is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scene cannot own a run. Before Stage 10a the camp's scope constructed a
    /// <c>RunSession</c> and the battle's scope built its own throwaway one, so walking from the
    /// camp into the arena created a second run — a different wallet, a different bag, a different
    /// shop — and walking back created a third. That is the leak the Stage 10 acceptance names
    /// ("ไม่มีเงินหรือไอเทมหายตอนเปลี่ยน Scene", "Camp ↔ Battle วน 20 รอบแล้วไม่มี presenter หรือ
    /// ECS world ซ้ำ"). This class is the fix: the run is created once, on demand, inside a child
    /// scope of the App scope, and destroyed only by <see cref="DisposeRun"/> (returning to the
    /// title) or by the app closing.
    /// </para>
    /// <para>
    /// <b>Who calls it.</b> A scene scope resolves this from the App scope and asks
    /// <see cref="RequireRun"/>; a scene opened on its own — the Editor's scene list, a
    /// direct-load PlayMode test, a <c>?scene=</c> handover — is a root scope and registers its own
    /// owner instead, so one scene is always playable without the boot flow.
    /// </para>
    /// <para>
    /// It is <see cref="IDisposable"/> because the container that holds it disposes it; a
    /// <c>RunSession</c> holds no OS resources, so dropping it is the whole of the work.
    /// </para>
    /// </remarks>
    public sealed class RunScopeOwner : IDisposable
    {
        /// <summary>
        /// The seed a run gets when nobody names one. A constant rather than a clock read, so two
        /// launches of the same build start the same run (AGENTS rule 6 keeps clocks out of the
        /// rules; this is the composition root refusing to invent one).
        /// </summary>
        public const uint DefaultSeed = 20261015u;

        private readonly LifetimeScope _owner;
        private uint _seed;

        private RunLifetimeScope _scope;
        private RunSession _run;

        /// <summary>Creates an owner under <paramref name="owner"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
        public RunScopeOwner(LifetimeScope owner, uint seed = DefaultSeed)
        {
            _owner = owner is null ? throw new ArgumentNullException(nameof(owner)) : owner;
            _seed = seed;
        }

        /// <summary>The seed a run created here is drawn from.</summary>
        public uint Seed => _seed;

        /// <summary>True while a run scope exists — including a run that has already ended.</summary>
        public bool HasRun => _run is not null;

        /// <summary>The live run, or null when there is none. Never creates one.</summary>
        public RunSession Current => _run;

        /// <summary>
        /// The run, created on first use. Idempotent: every caller in a session of play gets the
        /// same instance, which is what makes a wallet survive a scene change.
        /// </summary>
        /// <exception cref="InvalidOperationException">The owner scope has no built container.</exception>
        public RunSession RequireRun() => RequireRun(_seed);

        /// <summary>
        /// The run, created on first use from <paramref name="seed"/>. A checkpoint restore
        /// uses this overload because the seed is part of the save, not the scene's default
        /// (Stage 12). An already-created run is returned unchanged; the checkpoint's validation
        /// is what refuses a seed mismatch, so this can never rebuild a live run underneath it.
        /// </summary>
        public RunSession RequireRun(uint seed)
        {
            if (_run is not null)
            {
                return _run;
            }

            _seed = seed;

            if (_owner is null || _owner.Container is null)
            {
                throw new InvalidOperationException(
                    "RunScopeOwner has no built owner scope, so no run can be created.");
            }

            // Built parent-child by hand rather than through LifetimeScope.CreateChild: the seed
            // has to be set on the component before the child activates, because activation is
            // what runs Awake -> Build -> Configure, and Configure is where the seed is read.
            var scopeObject = new GameObject("RunLifetimeScope");
            scopeObject.SetActive(false);
            scopeObject.transform.SetParent(_owner.transform, worldPositionStays: false);

            RunLifetimeScope scope = scopeObject.AddComponent<RunLifetimeScope>();
            scope.Seed = _seed;
            scope.parentReference.Object = _owner;
            scopeObject.SetActive(true);

            // In play mode the activation above is what runs Awake -> Build, so this is a no-op. In
            // edit mode Awake is not a callback Unity makes (ShellSmokeTests builds its scope by
            // hand for the same reason), and a run that silently had no container would fail much
            // later, in whichever Resolve came first.
            if (scope.Container is null)
            {
                scope.Build();
            }

            _scope = scope;
            _run = scope.Container.Resolve<RunSession>();
            return _run;
        }

        /// <summary>
        /// Ends the run: the run scope and everything registered in it go away. Idempotent, so
        /// "go back to the title" twice is one disposal.
        /// </summary>
        /// <remarks>
        /// The child's <c>Dispose</c> destroys its GameObject, which is what stops a run scope from
        /// outliving the run the way a scene-scoped one would not have.
        /// </remarks>
        public void DisposeRun()
        {
            RunLifetimeScope scope = _scope;
            _scope = null;
            _run = null;

            if (scope is null)
            {
                return;
            }

            // DisposeCore releases the container; Dispose also destroys the GameObject. The
            // GameObject path is the one that matters in play mode (a leaked one would keep the
            // whole run reachable from the scene it was parented under).
            if (Application.isPlaying)
            {
                scope.Dispose();
            }
            else
            {
                scope.DisposeCore();
                UnityEngine.Object.DestroyImmediate(scope.gameObject);
            }
        }

        /// <inheritdoc />
        public void Dispose() => DisposeRun();
    }
}
