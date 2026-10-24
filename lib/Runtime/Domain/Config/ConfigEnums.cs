namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>
    /// Domain mirrors of the Luban enums in <c>data/Defines/schema.xml</c>.
    /// </summary>
    /// <remarks>
    /// These exist so <c>Domain</c> and <c>Application</c> never reference the
    /// generated <c>ProjectSurvive.Config</c> types (AGENTS rule 3 / ADR 0006).
    /// The numeric values deliberately match the data schema; the Infrastructure
    /// adapter is the single place that maps one to the other, and a guard test
    /// asserts the two stay in step.
    /// </remarks>
    public enum Element
    {
        None = 0,
        Fire = 1,
        Curse = 2,
    }

    /// <summary>
    /// What an offering does when the battle spends one of its uses (Stage 11a).
    /// Values match <c>data/Defines/schema.xml</c>; the amount and use count are
    /// columns beside it, never code constants.
    /// </summary>
    public enum OfferingEffect
    {
        None = 0,
        Heal = 1,
        Bia = 2,
    }

    public enum ItemCategory
    {
        Bag = 1,
        ArcaneWeapon = 2,
        Charm = 3,
        SpellTome = 4,
        Offering = 5,
        Familiar = 6,
    }

    /// <summary>Cast behaviour of an arcane weapon or familiar.</summary>
    public enum SkillBehavior
    {
        Projectile = 1,
        Pierce = 2,
        Aura = 3,
        Melee = 4,
    }

    public enum EnemyRole
    {
        Filler = 1,
        Fast = 2,
        Elite = 3,
        Boss = 4,
    }

    public enum MovePattern
    {
        Chase = 1,
        Charge = 2,
        Stationary = 3,
    }

    /// <summary>
    /// The telegraphed attack a ghost runs (Stage 11b), from the enemy row.
    /// </summary>
    /// <remarks>
    /// Values match <c>data/Defines/schema.xml</c>. The enum names the <i>shape of the
    /// state machine</i>, never one of its numbers: range, telegraph time, damage,
    /// radius, length, drain window and retreat all come from the columns beside it.
    /// </remarks>
    public enum EnemyAttackPattern
    {
        /// <summary>Contact damage only.</summary>
        None = 0,

        /// <summary>
        /// A dash along a line followed by a second attack at the landing point: Krasue's
        /// drain aura, or Krahang's gale (Stage 16). The payload is the row's.
        /// </summary>
        DashFollowUp = 1,

        /// <summary>Nang Tani: a ring around herself, then vines along a line.</summary>
        RingVines = 2,
    }

    /// <summary>
    /// How a <c>synergy</c> rule decides that two pieces are connected.
    /// </summary>
    /// <remarks>
    /// <see cref="SameRow"/> and <see cref="SameColumn"/> are the two halves of the GDD's
    /// "แถวเดียวกัน" (the same line) condition; which one a rule uses is authored per row in
    /// <c>data/tables/synergy.csv</c> (ADR 0015). Values are the data schema's numbers and
    /// must stay in step with <c>data/Defines/schema.xml</c>.
    /// </remarks>
    public enum SynergyKind
    {
        AdjacentPair = 1,
        SameRow = 2,
        BagArea = 3,
        SameColumn = 4,
    }

    public enum SynergyStat
    {
        Cooldown = 1,
        Damage = 2,
        AttackSpeed = 3,
        Debuff = 4,
    }

    public enum Language
    {
        Th = 1,
        En = 2,
    }
}
