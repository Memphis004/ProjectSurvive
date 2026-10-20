using System.Collections.Generic;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Spends an offering's use when the player's HP falls to the row's own threshold, and
    /// reports the uses that are left in <see cref="BattleContext.ConsumableUses"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The condition, the amount and the number of uses are all data (Stage 11a): the
    /// system only compares HP with the threshold the loadout carries and applies the
    /// effect. One use is spent per threshold crossing, so a heal that lifts the player
    /// back above the line does not burn the rest of the stack on the next tick; a Bia
    /// offering that does not change HP is therefore spent once per crossing, not once per
    /// tick.
    /// </para>
    /// <para>
    /// Nothing allocates here. <c>_wasBelow</c> is sized at session start and the loop
    /// reads the loadout's own entries.
    /// </para>
    /// </remarks>
    public sealed class ConsumableSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Health> _healths = null!;
        private bool[] _wasBelow = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _healths = systems.GetWorld().GetPool<Health>();

            int count = _context.Setup.Loadout.Consumables.Count;
            _wasBelow = new bool[count];
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            int player = _context.PlayerEntity;
            if (!_context.IsAlive(player))
            {
                return;
            }

            IReadOnlyList<ConsumableLoadout> consumables = _context.Setup.Loadout.Consumables;
            int[] usesLeft = _context.ConsumableUses;
            if (consumables.Count == 0 || usesLeft.Length == 0)
            {
                return;
            }

            ref Health health = ref _healths.Get(player);
            if (health.Hp <= 0)
            {
                return;
            }

            int maxHp = _context.MaxHp;
            if (maxHp <= 0)
            {
                return;
            }

            for (int i = 0; i < consumables.Count && i < usesLeft.Length; i++)
            {
                ConsumableLoadout entry = consumables[i];
                if (usesLeft[i] <= 0 || entry.AutoUseHpPercent <= 0)
                {
                    continue;
                }

                bool below = ((long)health.Hp * 100L) <= ((long)maxHp * entry.AutoUseHpPercent);
                if (!below)
                {
                    _wasBelow[i] = false;
                    continue;
                }

                if (_wasBelow[i])
                {
                    continue;
                }

                _wasBelow[i] = true;

                if (entry.EffectAmount <= 0)
                {
                    continue;
                }

                usesLeft[i]--;

                if (entry.Effect == OfferingEffect.Heal)
                {
                    int room = maxHp - health.Hp;
                    int healed = entry.EffectAmount < room ? entry.EffectAmount : room;
                    if (healed > 0)
                    {
                        health.Hp += healed;
                        _context.Emit(BattleEventType.PlayerHealed, _context.Ref(player), -1, healed);

                        // A heal that lifted the player back over the line re-arms the entry, so
                        // the next fall spends another use instead of waiting for a tick that
                        // happens to observe the player above the threshold.
                        if (((long)health.Hp * 100L) > ((long)maxHp * entry.AutoUseHpPercent))
                        {
                            _wasBelow[i] = false;
                        }
                    }
                }
                else if (entry.Effect == OfferingEffect.Bia)
                {
                    _context.CurrencyEarned += entry.EffectAmount;
                    _context.Emit(
                        BattleEventType.PickupCollected,
                        _context.Ref(player),
                        _context.DefIds.Item(entry.ItemId),
                        entry.EffectAmount);
                }
            }
        }
    }
}
