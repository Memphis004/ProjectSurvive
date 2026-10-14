using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// What one floor's settlement did to the run (GDD: \"CompleteFloor ให้รางวัลได้
    /// ครั้งเดียว (settlement ID) พร้อมฟื้น HP 15%\").
    /// </summary>
    /// <remarks>
    /// Returned by the run's battle-result command and echoed to the UI. The
    /// <see cref="SettlementId"/> is the guard behind \"ให้รางวัลได้ครั้งเดียว\": the
    /// run only settles while it is in <see cref="RunFlowState.Battle"/>, and a
    /// second attempt in the same settlement is refused (AGENTS rule 7) instead of
    /// paying the reward twice.
    /// </remarks>
    public readonly struct FloorSettlement
    {
        /// <summary>Creates a settlement report; the run does this, tests may too.</summary>
        public FloorSettlement(
            int settlementId,
            int floorIndex,
            BattlePhase outcome,
            BattleEndReason endReason,
            int rewardBia,
            int healedHp,
            int hp,
            bool runCompleted)
        {
            SettlementId = settlementId;
            FloorIndex = floorIndex;
            Outcome = outcome;
            EndReason = endReason;
            RewardBia = rewardBia;
            HealedHp = healedHp;
            Hp = hp;
            RunCompleted = runCompleted;
        }

        /// <summary>Unique, monotonically increasing id of this settlement.</summary>
        public int SettlementId { get; }

        /// <summary>The 1-based floor that was settled.</summary>
        public int FloorIndex { get; }

        /// <summary>The battle's outcome.</summary>
        public BattlePhase Outcome { get; }

        /// <summary>Why the battle ended.</summary>
        public BattleEndReason EndReason { get; }

        /// <summary>Bia credited from the battle (the fake pays nothing; Run owns the economy).</summary>
        public int RewardBia { get; }

        /// <summary>HP restored by the clear heal; 0 on a defeat.</summary>
        public int HealedHp { get; }

        /// <summary>The run's HP after the settlement.</summary>
        public int Hp { get; }

        /// <summary>True when this settlement ended the run (last floor cleared, or a defeat).</summary>
        public bool RunCompleted { get; }

        /// <summary>True when the floor was cleared.</summary>
        public bool IsWin => Outcome == BattlePhase.Won;

        /// <inheritdoc />
        public override string ToString() =>
            "#" + SettlementId + " floor " + FloorIndex + " " + Outcome + " hp=" + Hp + " +bia=" + RewardBia;
    }
}
