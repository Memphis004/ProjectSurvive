namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The knobs of <see cref="EcsBattleSimulation"/> that exist so a test or the
    /// load benchmark can put a specific amount of work on the simulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same contract as <c>FakeBattleOptions</c> (Stage 02b): these are <b>not</b>
    /// gameplay data. The shipped game constructs the factory with the defaults, and
    /// a default <see cref="EcsBattleOptions"/> must reproduce exactly the battle the
    /// real floor tables describe. A value here never changes a damage number, a
    /// cooldown or a spawn count <i>authored in <c>data/</c></i> — it changes how much
    /// synthetic load the world is asked to carry.
    /// </para>
    /// <para>
    /// <see cref="LoadTestEnemies"/> and <see cref="LoadTestProjectiles"/> are what the
    /// Stage 07 acceptance benchmark ("~300 ศัตรู + 500 กระสุน") uses: they seed the
    /// world with real enemy and real projectile entities, which the real systems then
    /// steer, hashed and collided like any other tick. Anything they spawn is
    /// re-seeded each tick so the measurement is a steady state rather than a spike
    /// that decays as the load is killed.
    /// </para>
    /// </remarks>
    public sealed class EcsBattleOptions
    {
        /// <summary>When true, the load generator tops the world up before every tick.</summary>
        public bool MaintainLoadTestEntities { get; set; } = true;

        /// <summary>Ghosts the load generator keeps alive. 0 (default) disables it.</summary>
        public int LoadTestEnemies { get; set; }

        /// <summary>Projectiles the load generator keeps alive. 0 (default) disables it.</summary>
        public int LoadTestProjectiles { get; set; }

        /// <summary>
        /// Enemy id the load generator spawns. Empty selects the first enemy row in the
        /// table, so a benchmark never has to name a def and a data rename cannot break it.
        /// </summary>
        public string LoadTestEnemyId { get; set; } = string.Empty;

        /// <summary>
        /// Skill id the load generator's projectiles use. Empty selects the first
        /// projectile-behaviour skill in the table, for the same reason.
        /// </summary>
        public string LoadTestProjectileSkillId { get; set; } = string.Empty;

        /// <summary>
        /// Ticks before the load generator retires the batch it spawned so the world
        /// churns instead of reaching a steady state. 0 (default) never retires: the load
        /// stays put, which is what the benchmark wants (a constant amount of work) and
        /// what the shipped game gets.
        /// </summary>
        /// <remarks>
        /// A non-zero value mirrors <c>FakeBattleOptions.EntityLifetimeTicks</c>, so a
        /// test that needs a battle generating a lot of events (the fake's dummy pump
        /// does this) can ask the real simulation for the same kind of churn through its
        /// own real pipeline — the retirements go through the normal death path, so a
        /// load ghost is killed, dropped and rewarded like any other. It is not a
        /// balance knob: nothing here changes how a raised ghost behaves.
        /// </remarks>
        public int LoadTestEntityLifetimeTicks { get; set; }
    }
}
