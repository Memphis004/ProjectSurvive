namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The <b>structural</b> bounds of the battle world — the shape of the field and
    /// of an entity's body, never a balance number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 5 puts every <i>balance</i> number in <c>data/</c> and it stays
    /// there: HP, damage, cooldown, MP cost, move speed, range, pickup radius,
    /// duration and spawn counts all arrive through
    /// <see cref="ProjectSurvive.Core.Domain.Config.IGameConfig"/>. What lives here is
    /// the geometry and the memory bounds a simulation needs in order to exist at
    /// all — how big the field is, how thick a ghost is, how many entities may be in
    /// flight. Those are not things a designer balances; a different value changes
    /// nothing a player can feel except by accident, and the same choice was already
    /// accepted for the Stage 02b fake
    /// (<c>FakeBattleSession</c>'s orbit constants).
    /// </para>
    /// <para>
    /// The one number a reader might expect here and will not find is the arena
    /// <i>radius as a gameplay constraint</i>: the field is a disc of
    /// <see cref="Radius"/> because a headless prototype needs a finite plane, and the
    /// player and the ghosts are kept inside it. Stage 11 (balance) owns whether that
    /// is the right size, and it may move to <c>data/</c> then.
    /// </para>
    /// </remarks>
    public static class BattleArena
    {
        /// <summary>Radius of the playable disc, in world units. Ghosts walk in from its edge.</summary>
        public const float Radius = 40f;

        /// <summary>Where on the ring an enemy spawns, as a fraction of the radius —
        /// slightly inside the bound so the clamp never fights the spawn.</summary>
        public const float SpawnRadiusFactor = 0.95f;

        /// <summary>Collision radius of the player, in world units.</summary>
        public const float PlayerRadius = 0.4f;

        /// <summary>Collision radius of a filler ghost, in world units.</summary>
        public const float EnemyRadius = 0.45f;

        /// <summary>Body radius multiplier for an elite ghost.</summary>
        public const float EliteRadiusScale = 1.4f;

        /// <summary>Body radius multiplier for a boss.</summary>
        public const float BossRadiusScale = 2f;

        /// <summary>Collision radius of a projectile, in world units.</summary>
        public const float ProjectileRadius = 0.15f;

        /// <summary>Collision radius of a dropped pickup, in world units.</summary>
        public const float PickupRadius = 0.3f;

        /// <summary>Edge length of one spatial-hash cell, in world units.</summary>
        public const float SpatialCellSize = 4f;

        /// <summary>Angular spread between the projectiles of a multi-shot cast, in degrees.</summary>
        public const float MultiShotFanDegrees = 8f;

        /// <summary>
        /// Largest number of enemies that may be alive at once. A wave whose spawns
        /// would cross this waits instead: the cap is a memory bound, and the frame
        /// the presentation reads is capped separately
        /// (<see cref="ProjectSurvive.Core.Domain.Combat.Battle.BattleConstants.MaxRenderEntities"/>).
        /// </summary>
        public const int MaxEnemies = 512;

        /// <summary>Largest number of projectiles that may be alive at once.</summary>
        public const int MaxProjectiles = 1024;

        /// <summary>Largest number of uncollected pickups on the field at once.</summary>
        public const int MaxPickups = 256;

        /// <summary>Largest number of damage applications one tick may queue.</summary>
        public const int MaxHitsPerTick = 2048;

        /// <summary>Largest number of drops one tick may queue.</summary>
        public const int MaxDropsPerTick = 256;

        /// <summary>Entity slots the ECS world is created with, so a busy tick never grows it.</summary>
        public const int EntityCapacity = 2048;

        /// <summary>
        /// Per-component dense capacity. Sized above the widest entity count a tick can
        /// hold (the enemy cap plus the projectile cap) so no pool resizes mid-tick, and
        /// chosen so the largest component's array stays under the large-object threshold
        /// — the bot simulation builds and releases thousands of sessions.
        /// </summary>
        public const int ComponentCapacity = 1280;

        /// <summary>Recycled-slot capacity of the world and of a component pool.</summary>
        public const int RecycledCapacity = 1024;

        /// <summary>
        /// Components a single entity may hold before the world reallocates its raw
        /// entity table. Sized above the widest entity (an enemy) so no spawn ever
        /// reallocates mid-tick.
        /// </summary>
        public const int EntityComponentsCapacity = 16;

        /// <summary>Registered component types and filters the world is created with.</summary>
        public const int PoolCapacity = 32;

        /// <summary>Filters the world creates before it grows its filter table.</summary>
        public const int FilterCapacity = 32;

        /// <summary>Dense capacity of a battle filter.</summary>
        public const int FilterDenseCapacity = 1536;

        /// <summary>Fixed tick length in seconds, from the contract's tick rate.</summary>
        public const float TickDeltaSeconds =
            1f / ProjectSurvive.Core.Domain.Combat.Battle.BattleConstants.TickRate;

        /// <summary>Radians in a full turn.</summary>
        public const float Tau = 6.2831855f;

        /// <summary>Radians per degree, for the constants quoted in degrees.</summary>
        public const float DegreesToRadians = 0.017453292f;

        /// <summary>Radians-to-degrees factor, for the render snapshot's yaw.</summary>
        public const float RadiansToDegrees = 57.29578f;
    }
}
