using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The numeric def ids <see cref="ProjectSurvive.Core.Domain.Combat.Battle.BattleEvent"/>
    /// carries, resolved from the string ids in <c>data/</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The battle contract has always said an event's payload is a <b>def id</b>
    /// (<c>int</c>, <c>-1</c> when unused) rather than a string, so no event allocates
    /// and presentation never touches config. Stage 02b left the mapping as an open
    /// question ("mapping จาก string id ของ config เป็นตัวเลข ยังไม่มีจริง"); this is
    /// the rule it asked for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>an id is its <b>1-based position in the table it is declared
    /// in</b>, in the order <c>IGameConfig</c> exposes (i.e. the file order of
    /// <c>data/tables/*.csv</c>), so 0 is free to mean "no def";</description></item>
    /// <item><description>the map is built <b>once, per factory</b>, from the config
    /// the factory was constructed with, and is immutable afterwards;</description></item>
    /// <item><description>an id that is not in the table resolves to <c>0</c>
    /// (unknown), never to a guess.</description></item>
    /// </list>
    /// <para>
    /// The mapping is derived, never persisted: inserting a row anywhere but the end of
    /// a table renumbers the defs after it, which is fine because a def id lives only
    /// inside one battle's event stream (a save file names string ids — Stage 12). The
    /// string ids stay the stable vocabulary.
    /// </para>
    /// </remarks>
    public sealed class BattleDefIds
    {
        /// <summary>An id that is not in any table.</summary>
        public const int Unknown = 0;

        private readonly Dictionary<string, int> _enemies;
        private readonly Dictionary<string, int> _skills;
        private readonly Dictionary<string, int> _items;
        private readonly Dictionary<string, int> _statuses;

        /// <summary>Builds the map from a config snapshot.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public BattleDefIds(IGameConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _enemies = Build(config.Enemies.Count, i => config.Enemies[i].Id);
            _skills = Build(config.Skills.Count, i => config.Skills[i].Id);
            _items = Build(config.Items.Count, i => config.Items[i].Id);
            _statuses = Build(config.Statuses.Count, i => config.Statuses[i].Id);
        }

        /// <summary>Def id of an enemy row, or <see cref="Unknown"/>.</summary>
        public int Enemy(string id) => Lookup(_enemies, id);

        /// <summary>Def id of a skill row, or <see cref="Unknown"/>.</summary>
        public int Skill(string id) => Lookup(_skills, id);

        /// <summary>Def id of an item row, or <see cref="Unknown"/>.</summary>
        public int Item(string id) => Lookup(_items, id);

        /// <summary>Def id of a status row, or <see cref="Unknown"/>.</summary>
        public int Status(string id) => Lookup(_statuses, id);

        /// <summary>The first enemy row's id, or empty when the table is empty.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string FirstEnemyId(IGameConfig config) =>
            config.Enemies.Count > 0 ? config.Enemies[0].Id : string.Empty;

        /// <summary>
        /// The first skill row whose behaviour is a projectile or a pierce. Used by the
        /// load benchmark so it never has to name a def that a data edit could rename.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public static string FirstProjectileSkillId(IGameConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            for (int i = 0; i < config.Skills.Count; i++)
            {
                SkillConfig skill = config.Skills[i];
                if (skill.Behavior == SkillBehavior.Projectile || skill.Behavior == SkillBehavior.Pierce)
                {
                    return skill.Id;
                }
            }

            return string.Empty;
        }

        private static Dictionary<string, int> Build(int count, Func<int, string> idAt)
        {
            var map = new Dictionary<string, int>(count, StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                // First row wins: a duplicated id in data is a validation error, and this
                // map must not depend on which duplicate was seen last.
                map[idAt(i)] = i + 1;
            }

            return map;
        }

        private static int Lookup(Dictionary<string, int> map, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Unknown;
            }

            return map.TryGetValue(id, out int defId) ? defId : Unknown;
        }
    }
}
