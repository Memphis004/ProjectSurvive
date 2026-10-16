using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Simulation.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Battle.Vfx;
using R3;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 08 acceptance, in EditMode: the presentation reads the battle contract and nothing
    /// else, copies the frame before it can be overwritten, pools everything it draws, and is
    /// driven by the bag the player actually built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The battle's own rules are proven in <c>lib/Tests~</c>; these tests are about the seam.
    /// Each one is a sentence from the GDD's Stage 08 acceptance list turned into something that
    /// fails when it stops being true: "Presentation อ่านเฉพาะ IBattleFrame / IBattleEventSource",
    /// "span ของ Entities/PreviousEntities ถูก copy ก่อน Step/Submit ครั้งถัดไป",
    /// "ไม่ Instantiate/Destroy ต่อเนื่องหลัง pool warm-up",
    /// "เปลี่ยนของในย่ามแล้วพฤติกรรม Battle เปลี่ยนจริง",
    /// "HUD ไม่ subscribe ตำแหน่งทุก entity".
    /// </para>
    /// <para>
    /// The simulation assembly <i>is</i> referenced from here, on purpose: a test may look behind
    /// the seam to observe the world that must not leak through it. What the presentation itself
    /// may reference is asserted from its asmdef, below.
    /// </para>
    /// </remarks>
    public sealed class BattlePresentationTests
    {
        /// <summary>The run seed the Battle scene ships, so these tests play the shipped floor.</summary>
        private const uint Seed = 20261015u;

        /// <summary>
        /// A whole floor-1 run (1800 ticks at 30 Hz). Long enough that the wave reaches the player,
        /// the equipped weapon fires and ghosts die, which is what makes "a different bag plays a
        /// different floor" measurable rather than a claim about the first twenty seconds, when
        /// nothing has happened yet on either build.
        /// </summary>
        private const int LongRunTicks = 1800;

        private readonly List<UnityEngine.Object> _temporary = new List<UnityEngine.Object>();
        private readonly List<IDisposable> _owned = new List<IDisposable>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                _owned[i]?.Dispose();
            }

            _owned.Clear();

            for (int i = _temporary.Count - 1; i >= 0; i--)
            {
                if (_temporary[i] is not null)
                {
                    UnityEngine.Object.DestroyImmediate(_temporary[i]);
                }
            }

            _temporary.Clear();
        }

        // -------------------------------------------------------------------
        // The frame: copied before the next step can overwrite it
        // -------------------------------------------------------------------

        [Test]
        public void Snapshot_copies_the_prev_cur_pair_and_survives_the_next_publish()
        {
            var buffer = new BattleFrameBuffer();
            PublishEntity(buffer, tick: 1, new EntityRef(3, 1), simulationY: 0f);
            PublishEntity(buffer, tick: 2, new EntityRef(3, 1), simulationY: 5f);

            var snapshot = new BattleFrameSnapshot();
            snapshot.CopyFrom(buffer);

            Assert.That(snapshot.HasFrame, Is.True);
            Assert.That(snapshot.Tick, Is.EqualTo(2));
            Assert.That(snapshot.CurrentCount, Is.EqualTo(1));
            Assert.That(snapshot.PreviousCount, Is.EqualTo(1), "both ends of the interpolation pair are copied");
            Assert.That(snapshot.Current[0].Y, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(snapshot.Previous[0].Y, Is.EqualTo(0f).Within(0.0001f));

            // The next tick writes the slot the frame published into. That is precisely why the
            // snapshot exists: a render reading the spans directly would end up describing a tick
            // it was never handed.
            PublishEntity(buffer, tick: 3, new EntityRef(3, 1), simulationY: 40f);

            Assert.That(buffer.Entities[0].Y, Is.EqualTo(40f).Within(0.0001f), "the buffer reused its slot, as designed");
            Assert.That(snapshot.Tick, Is.EqualTo(2), "the snapshot changed under the next tick");
            Assert.That(snapshot.Current[0].Y, Is.EqualTo(5f).Within(0.0001f), "the snapshot is reading the reused slot");
            Assert.That(snapshot.Previous[0].Y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Snapshot_pairs_only_the_entity_that_still_owns_its_slot()
        {
            var buffer = new BattleFrameBuffer();
            PublishEntity(buffer, tick: 1, new EntityRef(9, 1), simulationY: 1f);
            PublishEntity(buffer, tick: 2, new EntityRef(9, 2), simulationY: 2f);

            var snapshot = new BattleFrameSnapshot();
            snapshot.CopyFrom(buffer);

            Assert.That(
                snapshot.TryGetPrevious(new EntityRef(9, 1), out RenderEntity previous),
                Is.True,
                "the previous tick's version is what it published");
            Assert.That(previous.Y, Is.EqualTo(1f).Within(0.0001f));

            // Slot 9 now holds a different entity. Interpolating it from the dead one's position
            // would slide a fresh ghost in from wherever its predecessor died.
            Assert.That(
                snapshot.TryGetPrevious(new EntityRef(9, 3), out _),
                Is.False,
                "a recycled slot must not be interpolated from its previous owner");
        }

        // -------------------------------------------------------------------
        // The pool: nothing is created after warm-up
        // -------------------------------------------------------------------

        [Test]
        public void Pool_warms_up_once_and_hands_the_same_view_back()
        {
            using var art = new BattlePlaceholderArt();
            var pool = new EntityViewPool(NewRoot(), art);

            pool.Warmup();

            Assert.That(pool.WarmupComplete, Is.True);
            Assert.That(
                pool.CreatedTotal,
                Is.EqualTo(
                    BattleViewCaps.PlayerViews + BattleViewCaps.EnemyViews
                    + BattleViewCaps.ProjectileViews + BattleViewCaps.PickupViews),
                "the pool is warmed to every view the caps allow");
            Assert.That(pool.CreatedAfterWarmup, Is.Zero);

            EntityView first = pool.Acquire(RenderEntityKind.Enemy);
            Assert.That(first.InUse, Is.True);
            pool.Release(first);

            EntityView second = pool.Acquire(RenderEntityKind.Enemy);
            Assert.That(second, Is.SameAs(first), "a released view comes back out of the pool");
            Assert.That(pool.CreatedAfterWarmup, Is.Zero, "a floor must not create a view after warm-up");
            Assert.That(pool.InUseCount, Is.EqualTo(1));

            pool.Dispose();
        }

        [Test]
        public void View_caps_mirror_the_simulations_structural_bounds()
        {
            // Presentation may not reference the simulation assembly, so these numbers are written
            // out a second time. A second copy of a number drifts; this is what catches it.
            Assert.That(BattleViewCaps.ArenaRadius, Is.EqualTo(BattleArena.Radius));
            Assert.That(BattleViewCaps.EliteScale, Is.EqualTo(BattleArena.EliteRadiusScale));
            Assert.That(BattleViewCaps.BossScale, Is.EqualTo(BattleArena.BossRadiusScale));
            Assert.That(BattleViewCaps.EnemyViews, Is.GreaterThanOrEqualTo(BattleArena.MaxEnemies));
            Assert.That(BattleViewCaps.ProjectileViews, Is.GreaterThanOrEqualTo(BattleArena.MaxProjectiles));
            Assert.That(BattleViewCaps.PickupViews, Is.GreaterThanOrEqualTo(BattleArena.MaxPickups));

            int pooled = BattleViewCaps.PlayerViews + BattleViewCaps.EnemyViews
                + BattleViewCaps.ProjectileViews + BattleViewCaps.PickupViews;
            Assert.That(
                pooled,
                Is.GreaterThanOrEqualTo(BattleConstants.MaxRenderEntities),
                "a frame publishes at most MaxRenderEntities, so the pools must cover a whole one");
        }

        // -------------------------------------------------------------------
        // The bridge: reuse, and interpolation between the two ticks
        // -------------------------------------------------------------------

        [Test]
        public void Bridge_reuses_one_view_per_entity_and_interpolates_between_ticks()
        {
            using var art = new BattlePlaceholderArt();
            Transform root = NewRoot();
            var pool = new EntityViewPool(root, art);
            pool.Warmup();

            var bridge = new BattleRenderBridge(pool, art);
            var snapshot = new BattleFrameSnapshot();
            var buffer = new BattleFrameBuffer();

            PublishEntity(buffer, tick: 1, new EntityRef(7, 1), simulationY: 0f);
            snapshot.CopyFrom(buffer);
            bridge.ApplySnapshot(snapshot);

            Assert.That(bridge.BoundCount, Is.EqualTo(1));
            int createdAfterFirstTick = pool.CreatedTotal;

            PublishEntity(buffer, tick: 2, new EntityRef(7, 1), simulationY: 10f);
            snapshot.CopyFrom(buffer);
            bridge.ApplySnapshot(snapshot);

            Assert.That(bridge.BoundCount, Is.EqualTo(1));
            Assert.That(
                pool.CreatedTotal,
                Is.EqualTo(createdAfterFirstTick),
                "the second tick reuses the view it already bound");

            bridge.Render(0.5f);

            Transform view = FindVisibleView(root);
            Assert.That(view, Is.Not.Null, "the bound entity has no visible view");
            Assert.That(view.position.z, Is.EqualTo(5f).Within(0.001f), "the view sits halfway between the two ticks");

            // The entity is gone from the next tick, so its view goes back to the pool.
            PublishEntity(buffer, tick: 3, new EntityRef(7, 1), simulationY: 12f, present: false);
            snapshot.CopyFrom(buffer);
            bridge.ApplySnapshot(snapshot);

            Assert.That(bridge.BoundCount, Is.Zero);
            Assert.That(pool.InUseCount, Is.Zero);

            bridge.Dispose();
            pool.Dispose();
        }

        [Test]
        public void Further_rows_sort_behind_and_a_bolt_sorts_over_the_ghost_it_hits()
        {
            Assert.That(
                BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, 10f),
                Is.LessThan(BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, -10f)),
                "a sprite further into the arena draws behind one nearer the camera");

            Assert.That(
                BattleWorldMap.SortingOrder(RenderEntityKind.Projectile, 0f),
                Is.GreaterThan(BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, 0f)));

            Assert.That(
                BattleWorldMap.SortingOrder(RenderEntityKind.Player, 0f),
                Is.GreaterThan(BattleWorldMap.SortingOrder(RenderEntityKind.Pickup, 0f)));

            Assert.That(
                BattleWorldMap.GroundSortingOrder,
                Is.LessThan(BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, 40f)),
                "the floor stays under every actor, however far away it is");

            Assert.That(BattleWorldMap.ToWorld(2f, -3f), Is.EqualTo(new Vector3(2f, 0f, -3f)));
        }

        // -------------------------------------------------------------------
        // The HUD: scalars only, and localized
        // -------------------------------------------------------------------

        [Test]
        public void Hud_state_carries_scalars_and_never_an_entity()
        {
            PropertyInfo[] properties = typeof(BattleHudState)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            Assert.That(
                properties.Length,
                Is.GreaterThan(20),
                "one property per readout; a HUD that lost half of them would pass the loop below");

            foreach (PropertyInfo property in properties)
            {
                Type type = property.PropertyType;

                // The floor's spawn windows are the one collection: a short list of normalized
                // windows, set once per floor, carrying no entity and no position.
                if (type == typeof(IReadOnlyList<BattleTimelineMarker>))
                {
                    continue;
                }

                Type valueType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReactiveProperty<>)
                    ? type.GetGenericArguments()[0]
                    : type;

                Assert.That(
                    valueType == typeof(string) || valueType == typeof(int) || valueType == typeof(float)
                    || valueType == typeof(bool),
                    Is.True,
                    property.Name + " is a " + valueType + ", which is not a scalar a HUD should carry");

                Assert.That(
                    type.Namespace is null || !type.Namespace.StartsWith("ProjectSurvive.Core", StringComparison.Ordinal),
                    Is.True,
                    property.Name + " carries a core type; the HUD reads scalars, never state");
            }
        }

        [Test]
        public void Hud_captions_are_localized_and_the_floor_number_comes_from_the_setup()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            var text = new LocalizationService(config);

            Assert.That(BuildSetup(config, equipSecondWeapon: true, out BattleSetup setup, out string error), Is.True, error);

            var state = new BattleHudState();
            var speed = new BattleSpeed();
            var presenter = new BattleHudPresenter(state, text, config, speed);

            presenter.Begin(setup, startingBia: 15);

            var captions = new[]
            {
                ("hud.hp", state.HpCaption.Value),
                ("hud.mp", state.ManaCaption.Value),
                ("hud.bia", state.BiaCaption.Value),
                ("hud.time", state.TimeCaption.Value),
                ("hud.wave", state.WaveCaption.Value),
                ("hud.boss", state.BossCaption.Value),
                ("hud.elite", state.EliteCaption.Value),
                ("hud.kills", state.KillsCaption.Value),
                ("hud.bag", state.BagCaption.Value),
                ("hud.status", state.StatusCaption.Value),
                ("hud.timeline", state.TimelineCaption.Value),
            };

            foreach ((string key, string shown) in captions)
            {
                Assert.That(text.TryGet(key, out string expected), Is.True, "missing localization row " + key);
                Assert.That(expected, Is.Not.Empty);
                Assert.That(shown, Is.EqualTo(expected), key + " was not localized");
            }

            Assert.That(state.FloorName.Value, Is.EqualTo(text.Get("floor.forest_1.name")));
            Assert.That(state.BiaValue.Value, Is.EqualTo("15"), "the HUD shows the purse the player walked in with");

            // The floor's number is the setup's own, formatted with the table's format row — the HUD
            // never counts floors of its own (Stage 08c moved it under the floor's name at the top).
            Assert.That(setup.FloorIndex, Is.GreaterThan(0));
            Assert.That(
                state.FloorIndex.Value,
                Is.EqualTo(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    text.Get("hud.floor.format"), setup.FloorIndex)),
                "the floor's number was not formatted from the tables");

            // Stage 08c removed the equipped-spell line: the bag itself is on screen as the mini ย่าม,
            // and what it draws — the pieces, their tiers and their cooldown plates — is asserted by
            // BattleMiniBagTests against the same bag this setup was resolved from.

            Assert.That(state.Timeline.Count, Is.EqualTo(1), "floor 1 has one spawn window");

            presenter.Dispose();
            speed.Dispose();
            state.Dispose();
        }

        // -------------------------------------------------------------------
        // The VFX: a damage event becomes a number, and the pool never grows
        // -------------------------------------------------------------------

        /// <summary>
        /// Requirement 6: "floating damage number แบบ pool". A hit must produce a label, the
        /// label must live its lifetime and no longer, and a wave's worth of hits must recycle
        /// the pool rather than create anything (AGENTS rule 8: no allocation on the hot path).
        /// </summary>
        [Test]
        public void A_damage_event_becomes_a_pooled_number_and_the_pool_never_grows()
        {
            var root = new GameObject("vfx-root");
            _temporary.Add(root);

            var pool = new FloatingDamagePool(root.transform, null, new RandomStreams(Seed).Visual, 50f);
            _owned.Add(pool);

            int capacity = pool.Capacity;
            Assert.That(capacity, Is.EqualTo(BattleViewCaps.DamageNumbers));
            Assert.That(pool.ActiveCount, Is.Zero, "a warmed pool shows nothing until something is hit");

            pool.OnBattleEvent(Hit(BattleEventType.DamageDealt, 1f, 2f, 7));
            pool.OnBattleEvent(Hit(BattleEventType.PlayerDamaged, 0f, 0f, 3));

            Assert.That(pool.ActiveCount, Is.EqualTo(2), "each hit must put one number on screen");

            // An event the pool does not draw (and a hit that dealt nothing) adds nothing.
            pool.OnBattleEvent(Hit(BattleEventType.EnemyKilled, 1f, 1f, 0));
            pool.OnBattleEvent(Hit(BattleEventType.WaveStarted, 0f, 0f, 5));

            Assert.That(pool.ActiveCount, Is.EqualTo(2), "only damage is drawn");

            // A wave's worth of hits: the pool recycles, it does not grow.
            for (int i = 0; i < BattleViewCaps.DamageNumbers * 8; i++)
            {
                pool.OnBattleEvent(Hit(BattleEventType.DamageDealt, i * 0.25f, i * 0.1f, 1 + (i % 200)));
            }

            Assert.That(pool.Capacity, Is.EqualTo(capacity), "the number pool grew during a fight");
            Assert.That(pool.ActiveCount, Is.LessThanOrEqualTo(capacity));

            // A number that has lived its lifetime is gone, and ticking an empty pool is free.
            pool.Tick(BattleViewCaps.DamageNumberLifetimeSeconds + 0.01f);
            Assert.That(pool.ActiveCount, Is.Zero, "a number outlived its lifetime");

            pool.Tick(1f);
            Assert.That(pool.ActiveCount, Is.Zero);
        }

        [Test]
        public void Speed_control_toggles_between_one_and_two_and_the_presenter_labels_it()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            var text = new LocalizationService(config);
            var state = new BattleHudState();
            var speed = new BattleSpeed();
            var presenter = new BattleHudPresenter(state, text, config, speed);

            Assert.That(state.SpeedLabel.Value, Is.EqualTo("1x"));
            Assert.That(state.Speed.Value, Is.EqualTo(1f).Within(0.001f));

            presenter.ToggleSpeed();

            Assert.That(speed.Multiplier.Value, Is.EqualTo(2f).Within(0.001f));
            Assert.That(state.SpeedLabel.Value, Is.EqualTo("2x"));
            Assert.That(state.Speed.Value, Is.EqualTo(2f).Within(0.001f));

            presenter.ToggleSpeed();

            Assert.That(speed.Multiplier.Value, Is.EqualTo(1f).Within(0.001f), "the speeds wrap");
            Assert.That(state.SpeedLabel.Value, Is.EqualTo("1x"));

            presenter.Dispose();
            speed.Dispose();
            state.Dispose();
        }

        [Test]
        public void Timeline_marks_the_elite_window_and_the_boss_window_from_the_tables()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();

            Assert.That(config.TryGetFloor("floor.forest_1", out FloorConfig floor1), Is.True);
            BattleTimelineMarker[] markers1 = BattleTimelineModel.Build(config, floor1);
            Assert.That(markers1, Has.Length.EqualTo(1));
            Assert.That(markers1[0].Kind, Is.EqualTo(BattleTimelineMarkerKind.Normal));

            Assert.That(config.TryGetFloor("floor.forest_3", out FloorConfig floor3), Is.True);
            BattleTimelineMarker[] markers3 = BattleTimelineModel.Build(config, floor3);
            Assert.That(
                markers3.Any(marker => marker.Kind == BattleTimelineMarkerKind.Elite),
                Is.True,
                "floor 3 spawns an elite wave in the tables");

            Assert.That(config.TryGetFloor("floor.forest_5", out FloorConfig floor5), Is.True);
            BattleTimelineMarker[] markers5 = BattleTimelineModel.Build(config, floor5);
            Assert.That(
                markers5.Any(marker => marker.Kind == BattleTimelineMarkerKind.Boss),
                Is.True,
                "floor 5 is the boss floor");

            foreach (BattleTimelineMarker marker in markers5)
            {
                Assert.That(marker.Start01, Is.InRange(0f, 1f));
                Assert.That(marker.End01, Is.InRange(0f, 1f));
                Assert.That(marker.End01, Is.GreaterThanOrEqualTo(marker.Start01));
            }
        }

        // -------------------------------------------------------------------
        // The bag drives the battle
        // -------------------------------------------------------------------

        [Test]
        public void The_bag_the_player_builds_changes_the_loadout_and_the_battle()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();

            Assert.That(BuildSetup(config, equipSecondWeapon: false, out BattleSetup plain, out string plainError), Is.True, plainError);
            Assert.That(BuildSetup(config, equipSecondWeapon: true, out BattleSetup equipped, out string equippedError), Is.True, equippedError);

            TestContext.WriteLine("loadout, starting knife only: " + Describe(plain.Loadout));
            TestContext.WriteLine("loadout, knife + the piece rented at camp: " + Describe(equipped.Loadout));

            Assert.That(
                Describe(equipped.Loadout),
                Is.Not.EqualTo(Describe(plain.Loadout)),
                "the piece rented at camp never reached the resolved CombatLoadoutSnapshot");
            Assert.That(equipped.StageId, Is.EqualTo(plain.StageId), "both runs play the same floor");

            // The loadout is what the simulation reads, so the same seed now has to play a
            // different floor: "เปลี่ยนของในย่ามแล้วพฤติกรรม Battle เปลี่ยนจริง", measured on the
            // fighting rather than on the data structure.
            var factory = new EcsBattleSessionFactory(config);
            string plainShape = PlayAndDescribe(factory, plain, out ViewWork plainWork);
            string equippedShape = PlayAndDescribe(factory, equipped, out ViewWork equippedWork);
            Assert.That(plainWork.MaxBoundViews, Is.Zero, "nothing was rendered in this comparison");
            Assert.That(equippedWork.MaxBoundViews, Is.Zero);

            TestContext.WriteLine("battle, knife only: " + plainShape);
            TestContext.WriteLine("battle, knife + rented piece: " + equippedShape);

            Assert.That(
                equippedShape,
                Is.Not.EqualTo(plainShape),
                "the extra piece changed the bag but not the battle");
        }

        // -------------------------------------------------------------------
        // Rendering is not allowed to change the battle
        // -------------------------------------------------------------------

        [Test]
        public void A_rendered_floor_hashes_the_same_as_an_unrendered_one()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            Assert.That(BuildSetup(config, equipSecondWeapon: true, out BattleSetup setup, out string error), Is.True, error);

            var factory = new EcsBattleSessionFactory(config);
            string unrendered = PlayAndDescribe(factory, setup, out ViewWork none);

            ViewFixture fixture = BuildViewFixture(config);
            string rendered = PlayAndDescribe(factory, setup, fixture.View, out ViewWork work);

            Assert.That(
                rendered,
                Is.EqualTo(unrendered),
                "drawing the floor — floating damage numbers included — changed what the battle did");

            // Non-vacuous: the rendered run has to have actually drawn, or the equality above
            // would only say that two identical runs agree with each other.
            Assert.That(none.MaxBoundViews, Is.Zero, "the unrendered run bound views to something");
            Assert.That(work.MaxBoundViews, Is.GreaterThan(0), "the view never bound a single entity");
            Assert.That(
                fixture.View.Events.Consumed(BattleEventChannel.Vfx),
                Is.GreaterThan(0),
                "the VFX channel was never fed, so no damage number could have been drawn");
            Assert.That(
                fixture.View.Pool.CreatedAfterWarmup,
                Is.Zero,
                "the rendered run created a view after warm-up");
        }

        // -------------------------------------------------------------------
        // The seam: the presentation assembly, and the scene it ships
        // -------------------------------------------------------------------

        [Test]
        public void Presentation_may_not_reference_the_simulation_assembly()
        {
            string scriptsRoot = Path.Combine(RepositoryRoot(), "UnityProject", "Assets", "Main", "Scripts");
            string presentationRoot = Path.Combine(scriptsRoot, "Presentation");
            string battleRoot = Path.Combine(scriptsRoot, "Battle");

            string presentationAsmdef = File.ReadAllText(
                Path.Combine(presentationRoot, "ProjectSurvive.Game.Presentation.asmdef"));
            Assert.That(
                presentationAsmdef,
                Does.Not.Contain("ProjectSurvive.Core.Simulation"),
                "Stage 08 work item: Presentation may see Domain.Combat.Battle, never the simulation assembly");

            // The composition root is where the port meets its adapter, so its assembly may name
            // the simulation. The presentation may not — not even for a dev switch.
            foreach (string file in Directory.GetFiles(presentationRoot, "*.cs", SearchOption.AllDirectories))
            {
                // Comments are stripped first: a class is allowed to *explain* the rule it obeys,
                // and "this must not reference the simulation" is exactly what the comment says.
                string source = StripComments(File.ReadAllText(file));
                Assert.That(
                    source.Contains("ProjectSurvive.Core.Simulation") || source.Contains("Leopotam.EcsLite"),
                    Is.False,
                    file + " names the simulation; presentation reads IBattleFrame / IBattleEventSource only");
            }

            Assert.That(
                Directory.GetFiles(battleRoot, "*.cs", SearchOption.AllDirectories).Length,
                Is.GreaterThan(0),
                "the composition-root assembly exists and owns the only place the port is bound");
        }

        [Test]
        public void The_battle_scene_exists_and_is_in_the_build_list()
        {
            const string scenePath = "Assets/Main/Scenes/Battle.unity";

            Assert.That(
                File.Exists(Path.Combine(RepositoryRoot(), "UnityProject", scenePath)),
                Is.True,
                "the Battle scene is missing: run ProjectSurvive/Stage 08/3. Create Battle scene");

            Assert.That(
                EditorBuildSettings.scenes.Any(scene => string.Equals(scene.path, scenePath, StringComparison.Ordinal)),
                Is.True,
                scenePath + " is not in the build settings, so the run loop cannot reach it");
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static void PublishEntity(
            BattleFrameBuffer buffer, long tick, EntityRef entity, float simulationY, bool present = true)
        {
            buffer.BeginFrame(tick);
            buffer.SetHud(new HudState { Hp = 10, MaxHp = 10, FloorDurationTicks = 1800 });
            if (present)
            {
                buffer.Add(new RenderEntity(entity, RenderEntityKind.Enemy, 0f, simulationY));
            }

            buffer.Publish();
        }

        private static bool BuildSetup(
            IGameConfig config, bool equipSecondWeapon, out BattleSetup setup, out string error)
        {
            // The scene's standalone bootstrap (Stage 10a): a run with this fixture's seed, played
            // by the real Application commands. The point of the fixture is unchanged — the setup
            // comes from the real tables and the real resolver, not from hand-written numbers.
            var run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), Seed);
            return BattleRunSource.TryStartFrom(run, config, equipSecondWeapon, out setup, out _, out error);
        }

        /// <summary>Everything about a resolved build that decides how the floor plays.</summary>
        private static string Describe(CombatLoadoutSnapshot loadout)
        {
            var description = new StringBuilder(128);
            description.Append("hp=").Append(loadout.Stats.MaxHp)
                .Append(" mp=").Append(loadout.Stats.MaxMp)
                .Append(" dmg=").Append(loadout.Stats.DamageMultiplier.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" speed=").Append(loadout.Stats.MoveSpeed.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" focus=").Append(loadout.Stats.FocusPerSecond.ToString("0.###", CultureInfo.InvariantCulture));

            for (int i = 0; i < loadout.Weapons.Count; i++)
            {
                WeaponLoadout weapon = loadout.Weapons[i];
                description.Append(" w:").Append(weapon.SkillId).Append('@').Append(weapon.Tier)
                    .Append('/').Append(weapon.Damage).Append('x').Append(weapon.ProjectileCount);
            }

            for (int i = 0; i < loadout.Summons.Count; i++)
            {
                SummonLoadout summon = loadout.Summons[i];
                description.Append(" s:").Append(summon.SkillId).Append('@').Append(summon.Tier);
            }

            return description.ToString();
        }

        /// <summary>How much drawing a run did, so an "it drew nothing" run cannot pass.</summary>
        private readonly struct ViewWork
        {
            internal ViewWork(int maxBoundViews) => MaxBoundViews = maxBoundViews;

            /// <summary>Most entities the view held at one time.</summary>
            internal int MaxBoundViews { get; }
        }

        /// <summary>
        /// Runs a floor with no input and describes what it did: the state hash, the kills, the
        /// damage its events reported and the phase it ended in. Two runs sharing a seed and an
        /// input must agree on all of it; two runs with different bags must not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The event cursor starts at the ring's oldest entry, exactly as the presentation's pump
        /// does, so this measures the floor rather than the moment the reader attached.
        /// </para>
        /// <para>
        /// When <paramref name="view"/> is given, the frame is copied and drawn every tick — the
        /// same three calls <c>BattleSimulationRunner.Update</c> makes — so a presentation that
        /// reached back into the simulation would show up as a different hash. The view is not
        /// asked to <c>Begin</c> a floor of its own: it draws the session this method owns.
        /// </para>
        /// </remarks>
        private static string PlayAndDescribe(
            IBattleSessionFactory factory, BattleSetup setup, out ViewWork work)
            => PlayAndDescribe(factory, setup, null, out work);

        /// <inheritdoc cref="PlayAndDescribe(IBattleSessionFactory, BattleSetup, out ViewWork)"/>
        private static string PlayAndDescribe(
            IBattleSessionFactory factory, BattleSetup setup, BattleViewScene view, out ViewWork work)
        {
            IBattleSession session = factory.Create(setup);
            var events = new BattleEvent[256];
            long cursor = session.Events.OldestSeq;
            int damage = 0;
            int consumed = 0;
            int maxBoundViews = 0;

            try
            {
                for (int tick = 0; tick < LongRunTicks; tick++)
                {
                    session.Step(PlayerInputFrame.Idle);

                    int count = session.Events.Read(ref cursor, events, out _);
                    for (int i = 0; i < count; i++)
                    {
                        if (events[i].Type == BattleEventType.DamageDealt)
                        {
                            damage += events[i].Value;
                        }
                    }

                    consumed += count;

                    if (view is not null)
                    {
                        view.AcceptTick(session.Frame);
                        view.DrainEvents(session.Events);
                        view.Render(0.5f, 1f / 60f);
                        maxBoundViews = Math.Max(maxBoundViews, view.BoundViews);
                    }
                }

                work = new ViewWork(maxBoundViews);
                return "hash=" + session.ComputeStateHash()
                    + " kills=" + session.Frame.Hud.Kills
                    + " damage=" + damage
                    + " events=" + consumed
                    + " phase=" + session.Phase;
            }
            finally
            {
                session.Dispose();
            }
        }

        /// <summary>
        /// A Battle scene without a scene: a view root, a camera posed by the rig, and the view.
        /// </summary>
        /// <remarks>
        /// The view is handed to <see cref="PlayAndDescribe"/> rather than asked to begin a floor
        /// of its own: what is under test is the drawing half, and a second session would only be
        /// a second thing to keep in step.
        /// </remarks>
        private ViewFixture BuildViewFixture(IGameConfig config)
        {
            var root = new GameObject("BattleViewRoot");
            _temporary.Add(root);

            var cameraObject = new GameObject("BattleViewCamera");
            _temporary.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            new BattleCameraRig(camera, BattleProjection.Orthographic).SnapTo(Vector3.zero);

            var state = new BattleHudState();
            var speed = new BattleSpeed();
            var presenter = new BattleHudPresenter(state, new LocalizationService(config), config, speed);

            var settings = new BattleViewSettings
            {
                ViewRoot = root.transform,
                Camera = camera,
                Projection = BattleProjection.Orthographic,
            };

            var fixture = new ViewFixture(
                new BattleViewScene(settings, state, presenter, speed, Seed), presenter, state, speed);
            _owned.Add(fixture);
            return fixture;
        }

        private Transform NewRoot()
        {
            var root = new GameObject("ViewRoot");
            _temporary.Add(root);
            return root.transform;
        }

        private static Transform FindVisibleView(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer is not null && renderer.enabled)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>One "something was hit" event, in the shape the session publishes.</summary>
        private static BattleEvent Hit(BattleEventType type, float x, float y, int amount) =>
            new BattleEvent(
                seq: 0,
                type: type,
                entity: new EntityRef(1, 1),
                value: amount,
                x: x,
                y: y);

        private static string RepositoryRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>
        /// Drops // and /* */ comments, so a scan for forbidden names reads code rather than
        /// prose. Mirrors what the Core architecture tests do for the same reason.
        /// </summary>
        private static string StripComments(string source)
        {
            string withoutBlocks = System.Text.RegularExpressions.Regex.Replace(
                source, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(withoutBlocks, @"//[^\n]*", string.Empty);
        }

        /// <summary>What one Battle view needs disposing.</summary>
        private sealed class ViewFixture : IDisposable
        {
            private readonly BattleHudPresenter _presenter;
            private readonly BattleHudState _state;
            private readonly BattleSpeed _speed;

            internal ViewFixture(BattleViewScene view, BattleHudPresenter presenter, BattleHudState state, BattleSpeed speed)
            {
                View = view;
                _presenter = presenter;
                _state = state;
                _speed = speed;
            }

            internal BattleViewScene View { get; }

            public void Dispose()
            {
                View.Dispose();
                _presenter.Dispose();
                _speed.Dispose();
                _state.Dispose();
            }
        }
    }
}
