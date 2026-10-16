using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using UnityEngine;
using VContainer;

// Aliased rather than imported: the simulation's namespace also declares a `Lifetime`
// component, which would collide with VContainer's lifetime enum in every registration.
using EcsBattleSessionFactory = ProjectSurvive.Core.Simulation.Battle.EcsBattleSessionFactory;

namespace ProjectSurvive.Game.Battle
{
    /// <summary>
    /// The Battle scene's composition root: the one place that decides <i>which</i> battle the
    /// presentation is looking at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the assembly AGENTS rule 3 exists to keep small. The presentation layer may not
    /// reference <c>ProjectSurvive.Core.Simulation</c> — its battle vocabulary is
    /// <c>Domain.Combat.Battle</c> and nothing else — so something has to be allowed to name
    /// both the port and its implementations, and that something is this scope. It is the same
    /// shape as the shell's <c>RootLifetimeScope</c>: the only place a port meets an adapter.
    /// </para>
    /// <para>
    /// <see cref="_useDevBattle"/> is the switch the Stage 08 prompt asks for ("พัฒนา UI กับ
    /// Fake ได้ก่อนต่อเข้ากับ sim จริง (สลับผ่าน LifetimeScope)"). With it on, the HUD, the
    /// render bridge, the pools, the input and the runner run unchanged against the Stage 02b
    /// dev session; with it off, the same objects run against the EcsLite battle. Nothing else in
    /// the scene changes, which is the strongest available statement that the presentation depends
    /// on the contract and not on the simulation. The dev session is reached through
    /// <c>DevBattleSessionBinding</c> in the Application layer rather than by name, because the
    /// run and the scene flow are only allowed to see <c>IBattleSessionFactory</c> (AGENTS rule 3,
    /// enforced by the Stage 06 architecture test).
    /// </para>
    /// <para>
    /// Since Stage 10a this is a <b>Scene</b> scope: in the normal flow it is a child of the App
    /// scope and registers nothing but the factory, the camera-independent state and the HUD's
    /// presenter. The run, the bag codec and every shell service come from the parent chain — the
    /// scene no longer builds a run of its own, which is what makes the same scene serve floor 1
    /// through floor 5 with the wallet the camp handed it.
    /// </para>
    /// </remarks>
    public sealed class BattleSceneScope : RootLifetimeScope
    {
        [Tooltip("Drive the presentation from the Stage 02b dev session instead of the EcsLite battle, to develop the screen without a live simulation.")]
        [SerializeField] private bool _useDevBattle;

        [Tooltip("When the scene is opened without a camp (?scene=Battle), rent and place one extra piece so the floor is not just the starting knife.")]
        [SerializeField] private bool _equipSecondWeapon = true;

        /// <summary>
        /// True when this scene should equip one extra piece before departing — see
        /// <see cref="BattleRunSource"/>.
        /// </summary>
        public bool EquipSecondWeapon => _equipSecondWeapon;

        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            // Every registration is an explicit factory, never a reflected constructor: the
            // first Web build failed on a constructor only reflection could see
            // (llmwiki/gotchas/il2cpp-di-and-reflection.md).
            builder.Register<IBattleSessionFactory>(
                resolver => _useDevBattle
                    ? DevBattleSessionBinding.CreateFactory(resolver.Resolve<IGameConfig>())
                    : new EcsBattleSessionFactory(resolver.Resolve<IGameConfig>()),
                Lifetime.Singleton);

            builder.Register(_ => new BattleSpeed(), Lifetime.Singleton);
            builder.Register(_ => new BattleHudState(), Lifetime.Singleton);

            // The dev menu's logic (Stage 10c). Registered here rather than in the shell because it
            // is a control of this screen; it is only ever *shown* where DevCheats.Allowed says so,
            // and every command it sends is refused by the floor otherwise.
            builder.Register(
                resolver => new DevMenuPresenter(resolver.Resolve<IGameConfig>()),
                Lifetime.Singleton);
            builder.Register(
                resolver => new BattleHudPresenter(
                    resolver.Resolve<BattleHudState>(),
                    resolver.Resolve<Core.Application.Text.LocalizationService>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<BattleSpeed>()),
                Lifetime.Singleton);
        }
    }
}
