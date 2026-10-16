using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// <see cref="BattleFrameBuffer"/> as the one <see cref="IBattleFrame"/>: the
    /// Prev/Cur double buffer, the single HUD snapshot, the render cap and zero
    /// allocation.
    /// </summary>
    /// <remarks>
    /// The tests here are the unit-level half. The other half lives in
    /// <c>BattleSessionContractTests</c>, which asserts the same behaviour through
    /// <see cref="IBattleSession"/> so the Stage 07 EcsLite session is bound by it
    /// too (ADR 0012).
    /// </remarks>
    public sealed class BattleFrameBufferTests
    {
        private static RenderEntity Render(float x) =>
            new RenderEntity(new EntityRef((int)x + 1, 1), RenderEntityKind.Enemy, x, 0f);

        [Fact]
        public void The_first_frame_has_no_previous_entities()
        {
            var buffer = new BattleFrameBuffer(4);
            buffer.BeginFrame(0);
            buffer.Add(Render(0));
            buffer.Add(Render(1));
            buffer.Publish();

            Assert.Equal(0, buffer.Tick);
            Assert.Equal(2, buffer.Entities.Length);
            Assert.Equal(0, buffer.PreviousEntities.Length);
        }

        [Fact]
        public void Publish_swaps_current_and_previous_every_tick()
        {
            var buffer = new BattleFrameBuffer(4);

            buffer.BeginFrame(0);
            buffer.Add(Render(0));
            buffer.Add(Render(1));
            buffer.Publish();

            buffer.BeginFrame(1);
            buffer.Add(Render(2));
            buffer.Publish();

            // Tick 1 is current; tick 0 is still readable as the other end of the
            // interpolation pair, untouched by the tick that came after it.
            Assert.Equal(1, buffer.Tick);
            Assert.Equal(1, buffer.Entities.Length);
            Assert.Equal(2f, buffer.Entities[0].X);

            Assert.Equal(2, buffer.PreviousEntities.Length);
            Assert.Equal(0f, buffer.PreviousEntities[0].X);
            Assert.Equal(1f, buffer.PreviousEntities[1].X);

            buffer.BeginFrame(2);
            buffer.Add(Render(3));
            buffer.Add(Render(4));
            buffer.Add(Render(5));
            buffer.Publish();

            // Two slots only: Prev is the immediately preceding tick, not history.
            Assert.Equal(3, buffer.Entities.Length);
            Assert.Equal(1, buffer.PreviousEntities.Length);
            Assert.Equal(2f, buffer.PreviousEntities[0].X);
        }

        [Fact]
        public void A_frame_in_progress_is_not_published_until_publish_ran()
        {
            var buffer = new BattleFrameBuffer(4);
            buffer.BeginFrame(0);
            buffer.Add(Render(0));
            buffer.Publish();

            buffer.BeginFrame(1);
            buffer.Add(Render(7));
            buffer.Add(Render(8));

            // Readers see the last completed frame, never a half-built one. There is
            // no earlier frame yet, so Prev is empty rather than the tick being
            // written.
            Assert.Equal(0, buffer.Tick);
            Assert.Equal(1, buffer.Entities.Length);
            Assert.Equal(0f, buffer.Entities[0].X);
            Assert.Equal(0, buffer.PreviousEntities.Length);

            buffer.Publish();

            Assert.Equal(1, buffer.Tick);
            Assert.Equal(2, buffer.Entities.Length);
            Assert.Equal(7f, buffer.Entities[0].X);
        }

        [Fact]
        public void Hud_is_one_snapshot_overwritten_each_tick()
        {
            var buffer = new BattleFrameBuffer(2);

            buffer.BeginFrame(0);
            buffer.SetHud(new HudState { Hp = 100 });
            buffer.Publish();

            Assert.Equal(100, buffer.Hud.Hp);

            // Not a Prev/Cur pair: a HUD is never interpolated, so the new tick
            // simply overwrites the value.
            buffer.BeginFrame(1);
            buffer.SetHud(new HudState { Hp = 90 });
            buffer.Publish();

            Assert.Equal(90, buffer.Hud.Hp);
        }

        [Fact]
        public void Capacity_is_clamped_to_the_render_limit_and_add_stops_at_it()
        {
            Assert.Equal(BattleConstants.MaxRenderEntities, new BattleFrameBuffer(int.MaxValue).Capacity);
            Assert.Equal(0, new BattleFrameBuffer(-1).Capacity);

            // The frame is a view: surplus entities are dropped, never buffered by
            // growing the slot (which would allocate on the hot path).
            var buffer = new BattleFrameBuffer(2);
            buffer.BeginFrame(0);
            for (int i = 0; i < 10; i++)
            {
                buffer.Add(Render(i));
            }

            buffer.Publish();

            Assert.Equal(2, buffer.Entities.Length);
            Assert.Equal(0f, buffer.Entities[0].X);
            Assert.Equal(1f, buffer.Entities[1].X);
        }

        [Fact]
        public void Publishing_and_reading_do_not_allocate()
        {
            var buffer = new BattleFrameBuffer(64);
            var entity = Render(1f);
            var hud = new HudState { Hp = 100 };
            var cooldown = new LoadoutCooldown(7, 3, 27);

            // Warm up JIT.
            for (int i = 0; i < 10_000; i++)
            {
                buffer.BeginFrame(i);
                buffer.SetHud(hud);
                buffer.Add(entity);
                buffer.AddCooldown(cooldown);
                buffer.Publish();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            int seen = 0;
            for (int i = 0; i < 10_000; i++)
            {
                buffer.BeginFrame(i);
                buffer.SetHud(hud);
                buffer.Add(entity);
                buffer.AddCooldown(cooldown);
                buffer.Publish();

                // Reading the spans is where a naive implementation would allocate;
                // the count is used after the loop so the reads cannot be elided.
                seen += buffer.Entities.Length;
                seen += buffer.PreviousEntities.Length;
                seen += buffer.Cooldowns.Length;
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.True(seen > 0);
            Assert.Equal(0L, after - before);
        }

        // -------------------------------------------------------------------
        // The cooldown list (Stage 08b)
        // -------------------------------------------------------------------

        /// <summary>
        /// The cooldown list is published like the entities: a reader holding a frame sees the finished
        /// list while the next one is being written, never a partially filled one.
        /// </summary>
        /// <remarks>
        /// This is what the unpublished-slot write buys. A list overwritten in place would pass every
        /// other test in this file — it would just be wrong for the one reader the contract cares about,
        /// the HUD that reads a frame between ticks while the following tick is already building.
        /// </remarks>
        [Fact]
        public void A_cooldown_list_is_not_visible_until_publish_ran()
        {
            var buffer = new BattleFrameBuffer(BattleConstants.MaxRenderEntities);

            buffer.BeginFrame(0);
            buffer.AddCooldown(new LoadoutCooldown(11, 5, 27));
            buffer.AddCooldown(new LoadoutCooldown(22, 0, 45, LoadoutCooldownFlags.ManaShort));
            buffer.Publish();

            Assert.Equal(2, buffer.Cooldowns.Length);
            Assert.Equal(11, buffer.Cooldowns[0].InstanceId);
            Assert.Equal(5, buffer.Cooldowns[0].RemainingTicks);
            Assert.True(buffer.Cooldowns[1].ReadyButManaShort);

            // The next frame is built entry by entry, in the slot this reader is not on.
            buffer.BeginFrame(1);
            buffer.AddCooldown(new LoadoutCooldown(33, 1, 18));

            Assert.Equal(2, buffer.Cooldowns.Length);
            Assert.Equal(11, buffer.Cooldowns[0].InstanceId);
            Assert.Equal(22, buffer.Cooldowns[1].InstanceId);

            buffer.AddCooldown(new LoadoutCooldown(44, 2, 18));
            buffer.Publish();

            Assert.Equal(2, buffer.Cooldowns.Length);
            Assert.Equal(33, buffer.Cooldowns[0].InstanceId);
            Assert.Equal(44, buffer.Cooldowns[1].InstanceId);
        }

        /// <summary>The cooldown list stops at the contract's bound, like the entity span stops at the render cap.</summary>
        [Fact]
        public void The_cooldown_list_stops_at_the_loadout_cap()
        {
            var buffer = new BattleFrameBuffer(BattleConstants.MaxRenderEntities);

            buffer.BeginFrame(0);
            for (int i = 0; i < BattleConstants.MaxLoadoutEntries + 10; i++)
            {
                buffer.AddCooldown(new LoadoutCooldown(i + 1, 0, 27));
            }

            buffer.Publish();

            Assert.Equal(BattleConstants.MaxLoadoutEntries, buffer.Cooldowns.Length);
            Assert.Equal(1, buffer.Cooldowns[0].InstanceId);
            Assert.Equal(BattleConstants.MaxLoadoutEntries, buffer.Cooldowns[BattleConstants.MaxLoadoutEntries - 1].InstanceId);
        }
    }
}
