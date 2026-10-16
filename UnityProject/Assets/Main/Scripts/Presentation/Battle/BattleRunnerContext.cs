using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// What a <see cref="BattleSimulationRunner"/> needs to start a floor, gathered by
    /// whoever owns the composition root.
    /// </summary>
    /// <remarks>
    /// A bundle rather than a nine-argument method, and rather than container injection into
    /// a <c>MonoBehaviour</c>: the shell's first Web build failed because VContainer needs a
    /// reflected constructor and IL2CPP strips one no IL call site reaches (see
    /// <c>llmwiki/gotchas/il2cpp-di-and-reflection.md</c>). Passing the resolved services
    /// explicitly keeps a real call site for every type the composition root touches.
    /// </remarks>
    public sealed class BattleRunnerContext
    {
        /// <summary>Which session implementation this scene runs — the fake or the real one.</summary>
        public IBattleSessionFactory Factory { get; set; }

        /// <summary>The floor to run, with its resolved loadout.</summary>
        public BattleSetup Setup { get; set; }

        /// <summary>The run's purse entering the floor.</summary>
        public int StartingBia { get; set; }

        /// <summary>Seed the cosmetic stream is derived from (the run seed).</summary>
        public uint VisualSeed { get; set; }

        /// <summary>Localization, for the HUD's captions and names.</summary>
        public LocalizationService Text { get; set; }

        /// <summary>Tables, for the floor's timeline and the loadout's spell names.</summary>
        public IGameConfig Config { get; set; }

        /// <summary>The HUD's reactive state.</summary>
        public Hud.BattleHudState HudState { get; set; }

        /// <summary>The HUD's presenter.</summary>
        public Hud.BattleHudPresenter Hud { get; set; }

        /// <summary>The 1×/2× control.</summary>
        public BattleSpeed Speed { get; set; }

        /// <summary>
        /// The run's bag, as the floor was departed from: the mini bag draws this board (Stage 08c).
        /// The same object the loadout was resolved from, so the HUD cannot show a build the battle
        /// is not running (ADR 0024).</summary>
        public Backpack Backpack { get; set; }
    }
}
