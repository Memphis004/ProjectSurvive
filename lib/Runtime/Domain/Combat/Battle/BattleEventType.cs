using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// What happened in the battle this tick.
    /// </summary>
    /// <remarks>
    /// Events exist for **presentation only** — VFX, audio, HUD, floating
    /// numbers. Rules must never read the event stream to decide an outcome; they
    /// read simulation state. See <c>llmwiki/systems/battle-contract.md</c> for the
    /// meaning of each event's <c>DefId</c> / <c>Value</c>.
    /// </remarks>
    public enum BattleEventType
    {
        /// <summary>A wave's spawn window opened. <c>Value</c> = wave index.</summary>
        WaveStarted = 0,

        /// <summary>An enemy entered the field. <c>DefId</c> = enemy def.</summary>
        EnemySpawned = 1,

        /// <summary>An enemy died. <c>DefId</c> = enemy def; <c>Value</c> = kill count after this one.</summary>
        EnemyKilled = 2,

        /// <summary>A projectile was fired. <c>DefId</c> = skill def.</summary>
        ProjectileFired = 3,

        /// <summary>Damage landed. <c>Value</c> = damage; <c>Flags</c> may include <see cref="BattleEventFlags.Critical"/>.</summary>
        DamageDealt = 4,

        /// <summary>The player took damage. <c>Value</c> = damage; the player's remaining HP is in the HUD.</summary>
        PlayerDamaged = 5,

        /// <summary>The player was healed. <c>Value</c> = amount healed.</summary>
        PlayerHealed = 6,

        /// <summary>A status effect was applied. <c>DefId</c> = status def.</summary>
        StatusApplied = 7,

        /// <summary>A status effect expired. <c>DefId</c> = status def.</summary>
        StatusExpired = 8,

        /// <summary>A pickup dropped. <c>Value</c> = pickup kind.</summary>
        PickupSpawned = 9,

        /// <summary>A pickup was collected. <c>Value</c> = pickup kind.</summary>
        PickupCollected = 10,

        /// <summary>
        /// A telegraphed attack started. <c>DefId</c> = the enemy def that is attacking;
        /// <c>Value</c> = <see cref="TelegraphShape"/>; the footprint is on <c>X</c>,
        /// <c>Y</c>, <c>Radius</c>, <c>Length</c> and <c>Angle</c> (Stage 11b).
        /// </summary>
        TelegraphStarted = 11,

        /// <summary>
        /// A telegraphed attack resolved. Same payload as <see cref="TelegraphStarted"/>;
        /// emitted in the tick the stage ends, which is the tick the damage lands.
        /// </summary>
        TelegraphEnded = 12,

        /// <summary>A corpse was consumed. <c>Value</c> = HP the consumer regained.</summary>
        CorpseConsumed = 13,

        /// <summary>A boss changed phase. <c>Value</c> = new phase number.</summary>
        BossPhaseChanged = 14,

        /// <summary>The floor timer crossed a warning threshold. <c>Value</c> = seconds remaining.</summary>
        TimeWarning = 15,

        /// <summary>The battle ended. <c>Value</c> = (int)<see cref="BattleEndReason"/>.</summary>
        BattleEnded = 16,
    }

    /// <summary>Optional tags on a <see cref="BattleEvent"/>.</summary>
    [Flags]
    public enum BattleEventFlags : byte
    {
        /// <summary>No tags.</summary>
        None = 0,

        /// <summary>The hit was a critical.</summary>
        Critical = 1,

        /// <summary>The subject is an elite.</summary>
        Elite = 2,

        /// <summary>The subject is a boss.</summary>
        Boss = 4,
    }
}
