namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Structural bounds of the battle view: how many pooled views exist, and the eye
    /// candy constants that are not balance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view counts mirror the simulation's own caps
    /// (<c>BattleArena.MaxEnemies</c> 512, <c>MaxProjectiles</c> 1024,
    /// <c>MaxPickups</c> 256) plus the player. Presentation is not allowed to reference
    /// the <c>ProjectSurvive.Core.Simulation</c> assembly — that is the whole point of
    /// the seam (AGENTS rule 3) — so the numbers are written out here and a
    /// <c>UnityProject</c> test fails if the simulation's caps ever grow past them.
    /// They are <b>memory bounds</b>, exactly as in Stage 07: they decide how large a
    /// pool is, never how strong anything is.
    /// </para>
    /// <para>
    /// A frame publishes at most <c>BattleConstants.MaxRenderEntities</c> (1024) entities,
    /// so the pools (1 + 512 + 1024 + 256 = 1793) are larger than any frame can fill.
    /// That is deliberate: the pool is warmed once at scene start and must never create
    /// an object while the floor is running.
    /// </para>
    /// </remarks>
    public static class BattleViewCaps
    {
        /// <summary>Pooled player views (the prototype has one player).</summary>
        public const int PlayerViews = 1;

        /// <summary>Pooled ghost views (<c>BattleArena.MaxEnemies</c>).</summary>
        public const int EnemyViews = 512;

        /// <summary>Pooled projectile views (<c>BattleArena.MaxProjectiles</c>).</summary>
        public const int ProjectileViews = 1024;

        /// <summary>Pooled pickup views (<c>BattleArena.MaxPickups</c>).</summary>
        public const int PickupViews = 256;

        /// <summary>
        /// Radius of the arena floor, in world units. Mirrors the simulation's
        /// <c>BattleArena.Radius</c> (40); a Unity test asserts the pair, and the floor
        /// sprite is sized from it.
        /// </summary>
        public const float ArenaRadius = 40f;

        /// <summary>
        /// Visual scale of an elite ghost. Must equal the simulation's
        /// <c>BattleArena.EliteRadiusScale</c> so the sprite matches the body that
        /// actually collides; a Unity test asserts the pair.
        /// </summary>
        public const float EliteScale = 1.4f;

        /// <summary>
        /// Visual scale of the boss. Must equal the simulation's
        /// <c>BattleArena.BossRadiusScale</c>; a Unity test asserts the pair.
        /// </summary>
        public const float BossScale = 2f;

        /// <summary>Pooled floating damage numbers on screen at once.</summary>
        public const int DamageNumbers = 48;

        /// <summary>How long a floating damage number lives, in seconds (cosmetic).</summary>
        public const float DamageNumberLifetimeSeconds = 0.75f;

        /// <summary>How far a floating damage number rises, in world units (cosmetic).</summary>
        public const float DamageNumberRise = 1.6f;

        /// <summary>Sideways jitter of a floating damage number, in world units (cosmetic).</summary>
        public const float DamageNumberJitter = 0.5f;

        /// <summary>In-plane lean of a moving billboard, in degrees (cosmetic).</summary>
        public const float HeadingLeanDegrees = 12f;

        /// <summary>Sorting order of the HUD's world-space layer (above every actor).</summary>
        public const int FloatingTextSortingOrder = 9000;
    }
}
