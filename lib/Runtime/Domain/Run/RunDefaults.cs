namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// The run's starting loadout — the one piece of prototype content that is a
    /// rule rather than a table row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>numbers</b> of the start are in <c>data/</c> and stay there:
    /// <c>economy.start_bia</c> (15) is read by <c>RunSession</c>, and the bag and
    /// board sizes come from <c>constant</c>. What is here is only the two
    /// <i>ids</i> the run opens with (\"ย่ามผ้าดิบ + ยันต์เพลิงกาลี\"),
    /// because no table describes a starting kit.
    /// </para>
    /// <para>
    /// Recorded as a deliberate gap rather than invented as columns: the shop,
    /// the bag and the balance tables are all in scope for Stages 07–13, and the
    /// right home for a starting kit is a content decision, not a UI one. When a
    /// table does exist, this class becomes the place it is read into and nothing
    /// else moves.
    /// </para>
    /// </remarks>
    public static class RunDefaults
    {
        /// <summary>
        /// The difficulty every prototype floor is fought on. The prototype has a single
        /// difficulty (GDD "ยังไม่ทำ: หลายด่านและระดับความยาก"); the field exists on
        /// <c>BattleSetup</c> so a later stage can vary it without changing the run.
        /// </summary>
        public const string DifficultyId = "difficulty.normal";

        /// <summary>The bag the run opens with (<c>bag.raw_cotton_bag</c>, a 3x3 bag).</summary>
        public const string InitialBagId = "bag.raw_cotton_bag";

        /// <summary>The arcane weapon the run opens with (<c>item.kali_flame_yant</c>).</summary>
        public const string InitialWeaponId = "item.kali_flame_yant";

        /// <summary>Board column the starting bag is laid out at.</summary>
        public const int InitialBagX = 0;

        /// <summary>Board row the starting bag is laid out at.</summary>
        public const int InitialBagY = 0;

        /// <summary>Board column the starting weapon is laid out at (inside the starting bag).</summary>
        public const int InitialWeaponX = 0;

        /// <summary>Board row the starting weapon is laid out at (inside the starting bag).</summary>
        public const int InitialWeaponY = 0;
    }
}
