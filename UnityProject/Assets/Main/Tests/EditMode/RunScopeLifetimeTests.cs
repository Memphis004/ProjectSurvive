using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.Flow;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 10a, without a player loop: the three scopes and their three lifetimes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The PlayMode fixture proves the whole loop through real scenes; this one proves the rule the
    /// loop rests on, in milliseconds: the App scope owns exactly one <b>Run</b> scope, a scene
    /// scope <i>inherits</i> the run instead of building its own, and disposing the run scope is
    /// what ends a run — with a later run being a new one rather than the old one resumed.
    /// </para>
    /// <para>
    /// It builds the scopes the way the game does (a real <see cref="RootLifetimeScope"/> component,
    /// a real child scope), rather than a hand-rolled container, so a registration mistake in the
    /// composition root fails here rather than in a build.
    /// </para>
    /// </remarks>
    public sealed class RunScopeLifetimeTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // Edit mode rejects Object.Destroy, and a scope's own Dispose calls it, so the scopes
            // are released explicitly first (the same order ShellSmokeTests uses).
            var scopes = Object.FindObjectsByType<RootLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] is null)
                {
                    continue;
                }

                scopes[i].StopPersisting();
                scopes[i].DisposeCore();
            }

            var runs = Object.FindObjectsByType<RunLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < runs.Length; i++)
            {
                if (runs[i] is null)
                {
                    continue;
                }

                runs[i].DisposeCore();
            }

            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] is not null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        /// <summary>The run is created once per run, and disposed only by the owner.</summary>
        [Test]
        public void The_run_is_created_once_and_disposed_only_by_its_owner()
        {
            RootLifetimeScope app = NewScope();
            RunScopeOwner owner = app.Container.Resolve<RunScopeOwner>();

            Assert.That(owner.HasRun, Is.False, "a fresh shell already had a run");
            Assert.That(owner.Current, Is.Null);

            RunSession first = owner.RequireRun();
            Assert.That(first, Is.Not.Null);
            Assert.That(first.FlowState, Is.EqualTo(RunFlowState.Title), "a new run does not start at the title");

            // The whole point of the run scope: asking twice gives the same run, so a scene that
            // loads later edits the wallet the previous scene left behind.
            Assert.That(owner.RequireRun(), Is.SameAs(first), "a second ask built a second run");
            Assert.That(owner.HasRun, Is.True);
            Assert.That(CountRuns(), Is.EqualTo(1), "there is not exactly one run scope");

            owner.DisposeRun();

            Assert.That(owner.HasRun, Is.False, "the owner still reports a run");
            Assert.That(owner.Current, Is.Null, "the owner still hands out the disposed run");
            Assert.That(CountRuns(), Is.EqualTo(0), "the run scope outlived DisposeRun");
        }

        /// <summary>
        /// A new run after a disposal is a <b>new</b> run: same seed, same starting purse, and none
        /// of the previous run's state.
        /// </summary>
        [Test]
        public void A_run_after_a_disposal_is_a_new_run_and_not_the_old_one()
        {
            RootLifetimeScope app = NewScope();
            RunScopeOwner owner = app.Container.Resolve<RunScopeOwner>();

            RunSession first = owner.RequireRun();
            Assert.That(first.StartRun().IsSuccess, Is.True);
            Assert.That(first.Purchase(0).IsSuccess, Is.True, "the fixture expected a rentable first shelf");
            int spent = first.Wallet.Bia;
            int chestUsed = first.ChestUsed;
            Assert.That(chestUsed, Is.EqualTo(1));

            owner.DisposeRun();

            RunSession second = owner.RequireRun();

            Assert.That(second, Is.Not.SameAs(first), "the disposed run came back");
            Assert.That(second.FlowState, Is.EqualTo(RunFlowState.Title), "the new run resumed the old one's phase");
            Assert.That(second.Wallet.Bia, Is.GreaterThan(spent), "the new run did not start with a fresh purse");
            Assert.That(second.ChestUsed, Is.EqualTo(0), "the new run inherited the old run's chest");
            Assert.That(second.Seed, Is.EqualTo(first.Seed), "the seed is the shell's, not the run's");
        }

        /// <summary>
        /// A scene scope reads the run from the parent chain. This is what "RunSession ไม่ถูกสร้างใหม่
        /// ทุกครั้งที่กลับ Camp" means in code: the camp's own scope resolves the App scope's run.
        /// </summary>
        [Test]
        public void A_scene_scope_inherits_the_run_of_the_app_scope()
        {
            RootLifetimeScope app = NewScope();
            RunScopeOwner appOwner = app.Container.Resolve<RunScopeOwner>();

            // Built the way VContainer builds a scene scope under a persistent parent: the parent
            // reference is set, and the scope is built by hand because edit mode does not run Awake.
            CampSceneScope camp = CreateChild<CampSceneScope>(app, "CampSceneScope");
            Assert.That(camp.Parent, Is.SameAs(app), "the camp scope is not a child of the app scope");

            // The scene's base registrations are skipped when it has a parent, so the run owner and
            // every shell service are the parent's — one config, one clock, one run.
            Assert.That(
                camp.Container.Resolve<RunScopeOwner>(), Is.SameAs(appOwner),
                "the scene scope built its own run owner");

            // The run scope is a sibling of the scene scope (both are children of the App scope),
            // so a scene reaches the run through its owner rather than by resolving RunSession —
            // which is exactly what CampSceneScope and BattleSceneDirector do.
            RunSession run = camp.Container.Resolve<RunScopeOwner>().RequireRun();
            Assert.That(run, Is.SameAs(appOwner.RequireRun()), "the scene scope is not looking at the app's run");

            // And the camp screen itself got that run, which is the user-visible half.
            CampPresenter presenter = camp.Container.Resolve<CampPresenter>();
            Assert.That(presenter.Run, Is.SameAs(run), "the camp presenter is editing another run");
        }

        private RootLifetimeScope NewScope()
        {
            var scopeObject = new GameObject("EditModeAppScope");
            _objects.Add(scopeObject);

            RootLifetimeScope scope = scopeObject.AddComponent<RootLifetimeScope>();

            // Awake does not run outside play mode, so the scope is built explicitly. It has no
            // parent, so this is the App scope: the shell plus the run owner.
            scope.Build();
            return scope;
        }

        private TScope CreateChild<TScope>(RootLifetimeScope parent, string name)
            where TScope : LifetimeScope
        {
            var childObject = new GameObject(name);
            _objects.Add(childObject);
            childObject.transform.SetParent(parent.transform, worldPositionStays: false);

            var child = childObject.AddComponent<TScope>();
            child.parentReference.Object = parent;
            child.Build();
            return child;
        }

        private static int CountRuns() =>
            Object.FindObjectsByType<RunLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }
}
