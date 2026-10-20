using System;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// What a bot run is trying to build, as an ordered list of item ids to rent first
    /// (Stage 11d).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the instrument, not the game.</b> A balance report that only ever plays
    /// the cheapest-affordable build answers one question — "does the greedy shopper clear
    /// the floor" — and the GDD's floors are supposed to be passable by more than one
    /// loadout. The three plans below are the three shapes the shipped items actually make:
    /// a ranged caster (<c>fire_bolt</c> + <c>thread_pierce</c>), a close-range brawler
    /// (<c>knife_thrust</c> + <c>takrut_ward</c>) and a summoner (<c>curse_wave</c> from the
    /// kuman thong familiar). Nothing in <c>lib/Runtime</c> reads these ids; the bot only
    /// chooses which shop slot to rent, through the same <c>RunSession.Purchase</c> a
    /// player uses.
    /// </para>
    /// <para>
    /// The order matters: the first id the shop offers (and the run can afford) is bought
    /// first, so a run commits to its weapon early and merges it later. When none of the
    /// preferred pieces is on offer, the bot falls back to the cheapest affordable slot, so
    /// a run with an unlucky shop still buys something — the plan is a preference, not a
    /// refusal to play.
    /// </para>
    /// </remarks>
    /// <summary>
    /// How the bot stands and moves while fighting (Stage 11d).
    /// </summary>
    /// <remarks>
    /// A 1.5-unit knife cannot be used by a bot that runs away: the weapon would never be
    /// in range and the build would be measured as "bad" when the truth is "played wrong".
    /// So engagement is part of the plan, and a melee run closes on the nearest ghost while
    /// a ranged or summoner run keeps its distance — the same way a player would.
    /// </remarks>
    internal enum Engagement
    {
        /// <summary>Run from what is close, orbit it, stay inside the arena.</summary>
        Kite = 0,

        /// <summary>Walk onto the nearest ghost and stay in reach while the weapon swings.</summary>
        Close = 1,
    }

    internal sealed class BuildPlan
    {
        /// <summary>The default plan: no preference, cheapest affordable first (Stage 07).</summary>
        internal static readonly BuildPlan Default = new BuildPlan(
            "default", Array.Empty<string>(), Engagement.Kite);

        /// <summary>Ranged: a fire bolt and a piercing thread, then a bag to hold them.</summary>
        internal static readonly BuildPlan Ranged = new BuildPlan(
            "ranged", new[] { "item.kali_flame_yant", "item.sai_sin_thread", "item.spell_tome" },
            Engagement.Kite);

        /// <summary>Close range: the mor knife and the takrut ward's aura, plus a charm.</summary>
        internal static readonly BuildPlan Melee = new BuildPlan(
            "melee",
            new[] { "item.mor_knife", "item.takrut_tone", "item.phra_amulet", "item.red_soda" },
            Engagement.Close);

        /// <summary>Summoner: the kuman thong familiar, then the aura weapon and a charm.</summary>
        internal static readonly BuildPlan Familiar = new BuildPlan(
            "familiar", new[] { "item.kuman_thong", "item.takrut_tone", "item.waist_yant" },
            Engagement.Kite);

        private BuildPlan(string name, string[] preferences, Engagement engagement)
        {
            Name = name;
            Preferences = preferences;
            Engagement = engagement;
        }

        /// <summary>The plan's name, as it appears in the CSV and on the command line.</summary>
        internal string Name { get; }

        /// <summary>Item ids in the order the bot wants them; empty for the default plan.</summary>
        internal string[] Preferences { get; }

        /// <summary>How a build with these items has to be played to use them.</summary>
        internal Engagement Engagement { get; }

        /// <summary>The plan a command line asked for, or null when the name is not one.</summary>
        internal static BuildPlan? ByName(string name)
        {
            if (string.Equals(name, Default.Name, StringComparison.Ordinal))
            {
                return Default;
            }

            if (string.Equals(name, Ranged.Name, StringComparison.Ordinal))
            {
                return Ranged;
            }

            if (string.Equals(name, Melee.Name, StringComparison.Ordinal))
            {
                return Melee;
            }

            if (string.Equals(name, Familiar.Name, StringComparison.Ordinal))
            {
                return Familiar;
            }

            return null;
        }
    }
}
