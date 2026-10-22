#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;

namespace ProjectSurvive.Game.Presentation.Flow
{
    /// <summary>
    /// The development harness' one door into the run owner (Stage 14).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A browser test has no way to name the seed its run should draw from, because the seed is
    /// decided when the <see cref="RunScopeOwner"/> is constructed and the page cannot reach a
    /// constructor. This is the smallest seam that fixes that: it replaces the seed the
    /// <b>next</b> run is created from, and it refuses while a run exists.
    /// </para>
    /// <para>
    /// The refusal is the important half. A live run's seed is part of its identity — the
    /// checkpoint validates it, the shop, battle and visual streams are derived from it, and its
    /// replay is reproducible only from it. Replacing it under a live run would make every one
    /// of those quietly wrong, so the method throws instead of pretending.
    /// </para>
    /// <para>
    /// Compiled into a development build and the Editor only (the file is behind
    /// <c>DEVELOPMENT_BUILD || UNITY_EDITOR</c>), which is why <see cref="RunScopeOwner"/> is a
    /// partial type and a release player has no <c>TestOnlySetSeed</c> member at all.
    /// </para>
    /// </remarks>
    public sealed partial class RunScopeOwner
    {
        /// <summary>
        /// Sets the seed the next run is created from. Harness-only: nothing in the game calls it.
        /// </summary>
        /// <exception cref="InvalidOperationException">A run (or a run scope) already exists.</exception>
        public void TestOnlySetSeed(uint seed)
        {
            if (_run is not null || _scope is not null)
            {
                throw new InvalidOperationException(
                    "TestOnlySetSeed refuses while a run exists: the seed is part of the run's identity.");
            }

            _seed = seed;
        }
    }
}
#endif
