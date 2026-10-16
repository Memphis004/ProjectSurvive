using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The battle contract, as tests any <see cref="IBattleSessionFactory"/> must
    /// pass. Derived (non-abstract) classes bind it to a concrete factory — today
    /// the fake, from Stage 07 the EcsLite simulation — so the real simulation is
    /// held to the rules the fake was proven against.
    /// </summary>
    /// <typeparam name="TFactory">The factory under test.</typeparam>
    public abstract class BattleSessionContractTests<TFactory>
        where TFactory : IBattleSessionFactory
    {
        /// <summary>The standard factory: a boss floor can still be survived to its timer.</summary>
        protected abstract TFactory CreateFactory();

        /// <summary>A factory that damages the player hard enough to die. Override when a factory has such a knob.</summary>
        protected virtual TFactory CreateFactoryWithPressure() => CreateFactory();

        /// <summary>A fresh setup. Override to change HP / floors / cheats.</summary>
        protected virtual BattleSetup CreateSetup(
            bool bossFloor,
            int currentHp = BattleTestFixtures.DefaultMaxHp,
            bool devCheatsAllowed = true) =>
            BattleTestFixtures.Setup(bossFloor, currentHp, devCheatsAllowed: devCheatsAllowed);

        /// <summary>
        /// A setup whose loadout carries casters, for the cooldown-frame cases (Stage 08b).
        /// </summary>
        /// <remarks>
        /// Two entries — a weapon and a familiar — built from the real rows in
        /// <c>data/tables/skill.csv</c>, so a published period tracks the table instead of a
        /// duplicated number. <paramref name="maxMp"/> is the lever for the MP case: the standard
        /// setup's 50 pays for both casts, and a pool below the cheapest cost in the table (3) makes
        /// every entry "ready but blocked", which is a premise of the test rather than a balance
        /// claim.
        /// </remarks>
        protected virtual BattleSetup CreateCasterSetup(int maxMp = BattleTestFixtures.DefaultMaxMp)
        {
            BattleSetup setup = CreateSetup(false);
            setup.Loadout.Stats.MaxMp = maxMp < 0 ? 0 : maxMp;
            setup.Loadout.Weapons = new[]
            {
                BattleTestFixtures.Weapon(BattleTestFixtures.WeaponSkillId, BattleTestFixtures.WeaponInstanceId),
            };
            setup.Loadout.Summons = new[]
            {
                BattleTestFixtures.Summon(BattleTestFixtures.SummonSkillId, BattleTestFixtures.SummonInstanceId),
            };

            return setup;
        }

        /// <summary>Steps until the session ends (or the guard trips), returning how many steps were run.</summary>
        private static int RunToEnd(IBattleSession session, int maxSteps = 6000)
        {
            int steps = 0;
            while (session.Phase == BattlePhase.Running && steps < maxSteps)
            {
                session.Step(PlayerInputFrame.Idle);
                steps++;
            }

            Assert.True(session.Phase != BattlePhase.Running, "the session did not end within the guard of " + maxSteps + " steps");
            return steps;
        }

        [Fact]
        public void Step_while_not_running_does_not_advance_the_tick()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            session.Step(PlayerInputFrame.Idle);
            session.Step(PlayerInputFrame.Idle);
            long tickBefore = session.Tick;

            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.Surrender()));
            Assert.Equal(BattlePhase.Aborted, session.Phase);

            for (int i = 0; i < 10; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            Assert.Equal(tickBefore, session.Tick);
        }

        [Fact]
        public void Surrender_aborts_with_the_surrendered_reason_and_locks_the_session()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.Surrender()));
            Assert.Equal(BattlePhase.Aborted, session.Phase);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Aborted, result.Outcome);
            Assert.Equal(BattleEndReason.Surrendered, result.EndReason);

            Assert.Equal(CommandResult.RejectedWrongPhase, session.Submit(BattleCommand.Surrender()));
            Assert.Equal(CommandResult.RejectedWrongPhase, session.Submit(BattleCommand.DevWinNow()));
        }

        [Fact]
        public void Dev_commands_are_rejected_when_cheats_are_disabled()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false, devCheatsAllowed: false));

            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevWinNow()));
            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevLoseNow()));
            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevKillAll()));
            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevGodMode(true)));

            // Still running: a rejected command changes nothing.
            Assert.Equal(BattlePhase.Running, session.Phase);
        }

        [Fact]
        public void Dev_god_mode_rejects_an_out_of_range_argument()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            Assert.Equal(CommandResult.RejectedInvalidArg, session.Submit(new BattleCommand(BattleCommandKind.DevGodMode, 5)));
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevGodMode(true)));
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevGodMode(false)));
        }

        [Fact]
        public void A_normal_floor_survived_to_the_timer_is_won()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            RunToEnd(session);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Won, result.Outcome);
            Assert.Equal(BattleEndReason.SurvivedAllWaves, result.EndReason);
        }

        [Fact]
        public void A_boss_floor_that_runs_out_of_time_is_lost_with_timeout()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(true));

            RunToEnd(session);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Lost, result.Outcome);
            Assert.Equal(BattleEndReason.Timeout, result.EndReason);
        }

        [Fact]
        public void Dev_win_now_wins_a_boss_floor_as_a_boss_kill()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(true));

            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevWinNow()));

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Won, result.Outcome);
            Assert.Equal(BattleEndReason.BossDefeated, result.EndReason);
        }

        /// <summary>
        /// The lose-now cheat stands for the player dying, so the HP is spent before the floor is
        /// finished. Everything downstream reads <c>FinalHp</c> — the run settles it, the summary
        /// screen prints it — and a defeat reported at full health is a summary that lies about
        /// the fight that just ended.
        /// </summary>
        [Fact]
        public void Dev_lose_now_reports_the_death_it_stands_for()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevLoseNow()));

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Lost, result.Outcome);
            Assert.Equal(BattleEndReason.PlayerDied, result.EndReason);
            Assert.Equal(0, result.FinalHp);
        }

        [Fact]
        public void TryGetResult_is_false_while_running_and_true_once_ended()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            Assert.False(session.TryGetResult(out _));
            session.Step(PlayerInputFrame.Idle);
            Assert.False(session.TryGetResult(out _));

            session.Submit(BattleCommand.Surrender());

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Aborted, result.Outcome);
        }

        [Fact]
        public void FinalHp_is_within_bounds_on_both_a_win_and_a_loss()
        {
            BattleSetup winSetup = CreateSetup(false);
            IBattleSession winner = CreateFactory().Create(winSetup);
            RunToEnd(winner);
            Assert.True(winner.TryGetResult(out BattleResult winResult));
            Assert.InRange(winResult.FinalHp, 0, winSetup.Loadout.Stats.MaxHp);

            BattleSetup lossSetup = CreateSetup(false, currentHp: 100);
            IBattleSession loser = CreateFactoryWithPressure().Create(lossSetup);
            RunToEnd(loser);
            Assert.True(loser.TryGetResult(out BattleResult lossResult));
            Assert.InRange(lossResult.FinalHp, 0, lossSetup.Loadout.Stats.MaxHp);
        }

        [Fact]
        public void Replay_round_trip_reproduces_the_state_hash()
        {
            BattleSetup setup = CreateSetup(false);
            var replay = new BattleReplay(setup);
            IBattleSession original = CreateFactory().Create(setup);

            for (int i = 0; i < 300; i++)
            {
                PlayerInputFrame input = PlayerInputFrame.FromAxis(i % 2 == 0 ? 1f : -1f, 0.5f);
                replay.RecordStep(input);
                original.Step(input);
            }

            uint expected = original.ComputeStateHash();

            // A different factory instance, same setup: the replay must land on the
            // same state, which is what makes a desync detectable.
            uint actual = replay.ReplayHash(CreateFactory());

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Event_sequence_numbers_are_continuous()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));
            for (int i = 0; i < 50; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            var buffer = new BattleEvent[256];
            long cursor = 0;
            int read = session.Events.Read(ref cursor, buffer, out _);

            Assert.True(read > 0, "expected some events after 50 ticks");
            for (int i = 0; i < read; i++)
            {
                Assert.Equal(i, buffer[i].Seq);
            }
        }

        [Fact]
        public void A_reader_that_falls_behind_is_told_how_much_it_dropped()
        {
            // Never read during the battle, so the ring overflows well before the
            // floor ends. State must still be correct after the loss.
            IBattleSession session = CreateFactory().Create(CreateSetup(true));
            RunToEnd(session);

            var buffer = new BattleEvent[BattleConstants.EventRingCapacity];
            long cursor = 0;
            int read = session.Events.Read(ref cursor, buffer, out int dropped);

            Assert.True(dropped > 0, "a reader that never read should be told it dropped events");
            Assert.Equal(buffer.Length, read);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattleEndReason.Timeout, result.EndReason);
            Assert.Equal(BattlePhase.Lost, result.Outcome);
        }

        [Fact]
        public void Entities_never_exceed_the_render_cap()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));
            session.Step(PlayerInputFrame.Idle);

            Assert.InRange(session.Frame.Entities.Length, 0, BattleConstants.MaxRenderEntities);
            Assert.InRange(session.Frame.PreviousEntities.Length, 0, BattleConstants.MaxRenderEntities);
        }

        /// <summary>
        /// The frame is a Prev/Cur pair so Presentation can interpolate between two
        /// ticks (Stage 08). The concrete obligation: after step N, the previous
        /// frame is the frame that step N-1 published.
        /// </summary>
        [Fact]
        public void The_previous_frame_is_the_one_the_tick_before_published()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            session.Step(PlayerInputFrame.Idle);

            // A reader must copy a span before the next Step - the contract says so,
            // and there is no other way to hold on to a frame's values.
            RenderEntity[] before = session.Frame.Entities.ToArray();
            long tickBefore = session.Frame.Tick;

            session.Step(PlayerInputFrame.Idle);
            IBattleFrame frame = session.Frame;

            Assert.Equal(tickBefore + 1, frame.Tick);
            Assert.Equal(before.Length, frame.PreviousEntities.Length);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.Equal(before[i], frame.PreviousEntities[i]);
            }
        }

        [Fact]
        public void Step_does_not_allocate_after_warm_up()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            for (int i = 0; i < 200; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
        }

        // -------------------------------------------------------------------
        // The cooldown frame (Stage 08b)
        // -------------------------------------------------------------------

        /// <summary>
        /// The frame's cooldown list is the loadout, entry for entry and in the same order, each entry
        /// carrying the board piece it came from.
        /// </summary>
        /// <remarks>
        /// The HUD draws a cooldown on a <i>piece on the board</i>, so "which piece" has to survive the
        /// trip through the loadout: this asserts the order (weapons, then familiars) and the instance id
        /// of each entry against the loadout it was built from.
        /// </remarks>
        [Fact]
        public void The_frames_cooldowns_are_the_loadout_entries_with_the_board_pieces_they_came_from()
        {
            BattleSetup setup = CreateCasterSetup();
            IBattleSession session = CreateFactory().Create(setup);

            session.Step(PlayerInputFrame.Idle);
            ReadOnlySpan<LoadoutCooldown> cooldowns = session.Frame.Cooldowns;

            int weapons = setup.Loadout.Weapons.Count;
            int summons = setup.Loadout.Summons.Count;
            Assert.Equal(weapons + summons, cooldowns.Length);

            for (int i = 0; i < weapons; i++)
            {
                Assert.Equal(setup.Loadout.Weapons[i].InstanceId, cooldowns[i].InstanceId);
            }

            for (int i = 0; i < summons; i++)
            {
                Assert.Equal(setup.Loadout.Summons[i].InstanceId, cooldowns[weapons + i].InstanceId);
            }
        }

        /// <summary>
        /// A cooldown runs down one tick per tick, never below zero, and only ever goes back up by
        /// resetting to the entry's full period — which is what a cast does.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the sentence the HUD is written against ("ติดคูลดาวน์เท่าไหร่"), and it is the shape
        /// both ends of the battle must produce: the EcsLite session from its casters' own clocks, the
        /// fake from its predictable cycle. The check is a walk over consecutive frames, so a cooldown
        /// that jumped, went negative, or reset to something other than the full period fails whether the
        /// step was one tick or twenty.
        /// </para>
        /// <para>
        /// Both a blocked and a casting entry are covered: an entry waiting for a target or for MP sits at
        /// <c>0</c> until it can cast, and then resets, which is exactly the 0 → period step. The last tick
        /// before a reset is 1 → full period rather than 1 → 0 → full period, because a tick is atomic:
        /// the clock reaches zero and the cast that resets it happen in the same tick, so the frame that
        /// ends it shows the reset.
        /// </para>
        /// </remarks>
        [Fact]
        public void A_cooldown_runs_down_one_tick_at_a_time_and_resets_to_its_full_period()
        {
            const int ticks = 90;

            BattleSetup setup = CreateCasterSetup();
            IBattleSession session = CreateFactory().Create(setup);

            var expectedPeriods = new int[setup.Loadout.Weapons.Count + setup.Loadout.Summons.Count];
            for (int i = 0; i < setup.Loadout.Weapons.Count; i++)
            {
                expectedPeriods[i] = Ticks(setup.Loadout.Weapons[i].CooldownSeconds);
            }

            for (int i = 0; i < setup.Loadout.Summons.Count; i++)
            {
                expectedPeriods[setup.Loadout.Weapons.Count + i] =
                    Ticks(setup.Loadout.Summons[i].CooldownSeconds);
            }

            var resets = new int[expectedPeriods.Length];
            LoadoutCooldown[] previous = Array.Empty<LoadoutCooldown>();

            for (int tick = 0; tick < ticks; tick++)
            {
                session.Step(PlayerInputFrame.Idle);

                LoadoutCooldown[] current = session.Frame.Cooldowns.ToArray();
                Assert.Equal(expectedPeriods.Length, current.Length);

                for (int i = 0; i < current.Length; i++)
                {
                    LoadoutCooldown entry = current[i];

                    Assert.Equal(expectedPeriods[i], entry.PeriodTicks);
                    Assert.InRange(entry.RemainingTicks, 0, entry.PeriodTicks);

                    if (previous.Length == 0)
                    {
                        continue;
                    }

                    int before = previous[i].RemainingTicks;
                    if (before > 1)
                    {
                        Assert.Equal(before - 1, entry.RemainingTicks);
                    }
                    else if (entry.RemainingTicks > before)
                    {
                        // The only way up is a cast, and a cast resets to the full period. It is
                        // observed from 1 (the tick in which the clock reached zero went on to cast)
                        // or from 0 (an entry that could not cast sits there until it can).
                        Assert.Equal(expectedPeriods[i], entry.RemainingTicks);
                        resets[i]++;
                    }
                }

                previous = current;
            }

            for (int i = 0; i < resets.Length; i++)
            {
                Assert.True(
                    resets[i] > 0,
                    "entry " + i + " never cast in " + ticks + " ticks of a " + expectedPeriods[i]
                    + "-tick cooldown, so the reset this case is about was never observed");
            }
        }

        /// <summary>
        /// A caster whose pool cannot pay for its cast sits at ready with the MP flag raised; with a pool
        /// that can, the flag is never raised.
        /// </summary>
        /// <remarks>
        /// "พร้อมร่ายแต่ติด MP" is state the HUD has to show and the view cannot compute (it would need the
        /// entry's MP cost). Both halves are asserted, and the exact reading is checked too: the flag is set
        /// if and only if the cooldown has elapsed and MP is below the cast's cost, compared against
        /// <c>HudState.Mp</c> — the same number the MP bar shows.
        /// </remarks>
        [Fact]
        public void A_ready_caster_the_player_cannot_afford_is_flagged_mana_short()
        {
            const int ticks = 30;

            // Below the cheapest cost in the table (3), so no entry can ever cast.
            BattleSetup poor = CreateCasterSetup(maxMp: 1);
            IBattleSession blocked = CreateFactory().Create(poor);

            for (int tick = 0; tick < ticks; tick++)
            {
                blocked.Step(PlayerInputFrame.Idle);

                IBattleFrame frame = blocked.Frame;
                ReadOnlySpan<LoadoutCooldown> cooldowns = frame.Cooldowns;
                Assert.True(cooldowns.Length > 0, "the case needs at least one caster");

                for (int i = 0; i < cooldowns.Length; i++)
                {
                    LoadoutCooldown entry = cooldowns[i];
                    int mpCost = i < poor.Loadout.Weapons.Count
                        ? poor.Loadout.Weapons[i].MpCost
                        : poor.Loadout.Summons[i - poor.Loadout.Weapons.Count].MpCost;

                    Assert.Equal(0, entry.RemainingTicks);
                    Assert.True(entry.ReadyButManaShort, "entry " + i + " should be waiting on MP");
                    Assert.Equal(entry.IsReady && mpCost > frame.Hud.Mp, entry.ReadyButManaShort);
                }
            }

            // The other half: with a pool that pays for every cast, the same entries are never flagged,
            // even on the frames where a cooldown has elapsed and a cast is being chosen instead.
            BattleSetup rich = CreateCasterSetup();
            IBattleSession paid = CreateFactory().Create(rich);

            for (int tick = 0; tick < ticks; tick++)
            {
                paid.Step(PlayerInputFrame.Idle);

                IBattleFrame frame = paid.Frame;
                ReadOnlySpan<LoadoutCooldown> cooldowns = frame.Cooldowns;
                for (int i = 0; i < cooldowns.Length; i++)
                {
                    Assert.False(
                        cooldowns[i].ReadyButManaShort,
                        "entry " + i + " was flagged short of MP while the pool paid for every cast");
                }
            }
        }

        /// <summary>
        /// Publishing the cooldown list is part of the tick, so it must not allocate either.
        /// </summary>
        /// <remarks>
        /// The loadout here is not empty on purpose: the other allocation case runs the standard setup, whose
        /// loadout has no casters at all, so this is the one that covers the cooldown publish path itself.
        /// </remarks>
        [Fact]
        public void Step_with_a_caster_loadout_does_not_allocate_after_warm_up()
        {
            IBattleSession session = CreateFactory().Create(CreateCasterSetup());

            for (int i = 0; i < 200; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }


            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
        }

        /// <summary>A resolved cooldown in seconds as ticks — the period the frame must report.</summary>
        private static int Ticks(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 1;
            }

            int ticks = (int)Math.Round(seconds * BattleConstants.TickRate);
            return ticks < 1 ? 1 : ticks;
        }
    }
}
