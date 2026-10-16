using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// Turns the upgrade variants a piece carries into the number one stat ends up with
    /// (Stage 09f: "LoadoutResolver ใส่ตัวปรับค่าลงใน CombatLoadoutSnapshot").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The order is the rule, not the data's.</b> Every <c>Add</c> modifier is summed,
    /// every <c>Percent</c> modifier is summed, every <c>Multiply</c> is folded, and then the
    /// three are applied as <c>((base + add) * (1 + percent/100)) * factors</c>. Applying them
    /// in row order instead would make the result depend on how the CSV rows happen to be
    /// sorted, which a later content edit could then change by accident.
    /// </para>
    /// <para>
    /// <see cref="Include"/> is separate from <see cref="Apply"/> so that a stat shared by the
    /// whole board — max HP, move speed — combines every piece's modifiers <i>before</i> the
    /// base is touched: two "+20%" upgrades are +40%, not +44%. One instance is reused and
    /// allocates nothing, which keeps the loadout recomputation (once per bag revision) out
    /// of the GC's way.
    /// </para>
    /// <para>
    /// A <b>step whose id is empty or unknown contributes nothing</b> — empty is the normal
    /// case for a step with no chooser, and an unknown id cannot come from validated data
    /// (<c>Backpack.TryValidate</c> rejects it). Neither is an error here: this type resolves
    /// numbers, and the data pipeline is where a bad id is a failure.
    /// </para>
    /// </remarks>
    public sealed class UpgradeAccumulator
    {
        private float _flatAdd;
        private float _percentAdd;
        private float _factor = 1f;
        private bool _touched;

        /// <summary>True when at least one modifier contributed to the last accumulation.</summary>
        public bool Touched => _touched;

        /// <summary>Drops every modifier collected so far.</summary>
        public void Reset()
        {
            _flatAdd = 0f;
            _percentAdd = 0f;
            _factor = 1f;
            _touched = false;
        }

        /// <summary>Collects every modifier of <paramref name="stat"/> on one piece's step history.</summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public void Include(IGameConfig config, IReadOnlyList<string> variantIds, UpgradeStat stat)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (variantIds is null)
            {
                throw new ArgumentNullException(nameof(variantIds));
            }

            for (int step = 0; step < variantIds.Count; step++)
            {
                string variantId = variantIds[step];
                if (string.IsNullOrEmpty(variantId)
                    || !config.TryGetUpgradeVariant(variantId, out UpgradeVariantConfig variant))
                {
                    continue;
                }

                IReadOnlyList<UpgradeModifierConfig> modifiers = variant.Modifiers;
                for (int i = 0; i < modifiers.Count; i++)
                {
                    UpgradeModifierConfig modifier = modifiers[i];
                    if (modifier.Stat != stat)
                    {
                        continue;
                    }

                    switch (modifier.Method)
                    {
                        case UpgradeMethod.Add:
                            _flatAdd += modifier.Magnitude;
                            break;
                        case UpgradeMethod.Percent:
                            _percentAdd += modifier.Magnitude;
                            break;
                        case UpgradeMethod.Multiply:
                            _factor *= modifier.Magnitude;
                            break;
                    }

                    _touched = true;
                }
            }
        }

        /// <summary>The value <paramref name="baseValue"/> ends at, or the base value untouched.</summary>
        public float Apply(float baseValue) =>
            _touched ? ((baseValue + _flatAdd) * (1f + (_percentAdd / 100f))) * _factor : baseValue;

        /// <summary>Resets, includes one piece's modifiers and applies them in one call.</summary>
        public float Resolve(
            float baseValue, IGameConfig config, IReadOnlyList<string> variantIds, UpgradeStat stat)
        {
            Reset();
            Include(config, variantIds, stat);
            return Apply(baseValue);
        }
    }
}
