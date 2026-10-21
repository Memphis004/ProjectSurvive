using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Contracts.Save;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Infrastructure.Save
{
    /// <summary>
    /// The <see cref="IRunCheckpointCodec"/> adapter: domain checkpoint to MessagePack DTO
    /// and back.
    /// </summary>
    /// <remarks>
    /// The mapping is explicit and numeric for every enum, and it validates what bytes can
    /// bring back (an unknown piece kind, a negative price, an all-zero RNG state). Those
    /// checks belong here rather than in the run: a corrupt payload is a serialization
    /// failure, while a run's restore failure is an invalid-state failure.
    /// </remarks>
    public sealed class MessagePackRunCheckpointCodec : IRunCheckpointCodec
    {
        /// <summary>True when the generated (AOT-safe) formatter exists in this build.</summary>
        public static bool GeneratedFormatterIsPresent => RunCheckpointCodec.GeneratedFormatterIsPresent;

        /// <inheritdoc />
        public byte[] Encode(RunCheckpoint checkpoint)
        {
            if (checkpoint is null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            return RunCheckpointCodec.Serialize(ToDto(checkpoint));
        }

        /// <inheritdoc />
        public RunCheckpoint Decode(byte[] bytes)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return FromDto(RunCheckpointCodec.Deserialize(bytes));
        }

        /// <summary>Domain checkpoint to DTO.</summary>
        public static RunCheckpointDto ToDto(RunCheckpoint checkpoint)
        {
            var dto = new RunCheckpointDto
            {
                SchemaVersion = checkpoint.SchemaVersion,
                ConfigHash = checkpoint.ConfigHash ?? string.Empty,
                ConfigSchemaVersion = checkpoint.ConfigSchemaVersion,
                RunId = checkpoint.RunId ?? string.Empty,
                Seed = checkpoint.Seed,
                ShopRandom = ToDto(checkpoint.Random.Shop),
                BattleRandom = ToDto(checkpoint.Random.Battle),
                VisualRandom = ToDto(checkpoint.Random.Visual),
                FloorIndex = checkpoint.FloorIndex,
                Hp = checkpoint.Hp,
                Bia = checkpoint.Bia,
                FloorsCleared = checkpoint.FloorsCleared,
                GhostsDefeated = checkpoint.GhostsDefeated,
                LastSettlementId = checkpoint.LastSettlementId,
                RefreshCount = checkpoint.RefreshCount,
                NextBattleSeed = checkpoint.NextBattleSeed,
                HasNextBattleSeed = checkpoint.HasNextBattleSeed,
                Backpack = MessagePackBackpackSnapshotCodec.ToDto(checkpoint.Backpack),
                ShopSlots = new List<ShopSlotDto>(checkpoint.ShopSlots.Count),
                BackpackRevision = checkpoint.BackpackRevision,
            };

            for (int i = 0; i < checkpoint.ShopSlots.Count; i++)
            {
                ShopSlotState slot = checkpoint.ShopSlots[i];
                dto.ShopSlots.Add(new ShopSlotDto
                {
                    Index = slot.Index,
                    OfferedId = slot.OfferedId ?? string.Empty,
                    Kind = (int)slot.Kind,
                    Price = slot.Price,
                    IsPurchased = slot.IsPurchased,
                    IsLocked = slot.IsLocked,
                });
            }

            return dto;
        }

        /// <summary>DTO to domain checkpoint.</summary>
        public static RunCheckpoint FromDto(RunCheckpointDto dto)
        {
            if (dto is null)
            {
                throw new InvalidOperationException("Checkpoint payload was null.");
            }

            if (dto.ShopSlots is null || dto.Backpack is null)
            {
                throw new InvalidOperationException("Checkpoint payload is missing its bag or shop.");
            }

            var slots = new List<ShopSlotState>(dto.ShopSlots.Count);
            for (int i = 0; i < dto.ShopSlots.Count; i++)
            {
                ShopSlotDto slot = dto.ShopSlots[i];
                if (slot is null || slot.Price < 0)
                {
                    throw new InvalidOperationException("Checkpoint carries an invalid shop slot.");
                }

                slots.Add(new ShopSlotState(
                    slot.Index,
                    slot.OfferedId ?? string.Empty,
                    ToKind(slot.Kind),
                    slot.Price,
                    slot.IsPurchased,
                    slot.IsLocked));
            }

            var random = new RandomStreamsState(
                dto.Seed,
                FromDto(dto.ShopRandom, "shop"),
                FromDto(dto.BattleRandom, "battle"),
                FromDto(dto.VisualRandom, "visual"));

            return new RunCheckpoint
            {
                SchemaVersion = dto.SchemaVersion,
                ConfigHash = dto.ConfigHash ?? string.Empty,
                ConfigSchemaVersion = dto.ConfigSchemaVersion,
                RunId = dto.RunId ?? string.Empty,
                Seed = dto.Seed,
                Random = random,
                FloorIndex = dto.FloorIndex,
                Hp = dto.Hp,
                Bia = dto.Bia,
                FloorsCleared = dto.FloorsCleared,
                GhostsDefeated = dto.GhostsDefeated,
                LastSettlementId = dto.LastSettlementId,
                RefreshCount = dto.RefreshCount,
                NextBattleSeed = dto.NextBattleSeed,
                HasNextBattleSeed = dto.HasNextBattleSeed,
                Backpack = MessagePackBackpackSnapshotCodec.FromDto(dto.Backpack),
                ShopSlots = slots,
                BackpackRevision = dto.BackpackRevision,
            };
        }

        private static RandomStreamStateDto ToDto(Xoshiro128State state) =>
            new RandomStreamStateDto { S0 = state.S0, S1 = state.S1, S2 = state.S2, S3 = state.S3 };

        private static Xoshiro128State FromDto(RandomStreamStateDto? dto, string name)
        {
            if (dto is null)
            {
                throw new InvalidOperationException("Checkpoint is missing its " + name + " RNG state.");
            }

            var state = new Xoshiro128State(dto.S0, dto.S1, dto.S2, dto.S3);
            if (state.IsZero)
            {
                throw new InvalidOperationException("Checkpoint's " + name + " RNG state is all zero.");
            }

            return state;
        }

        private static PieceKind ToKind(int value) =>
            value is (int)PieceKind.None or (int)PieceKind.Bag or (int)PieceKind.Item
                ? (PieceKind)value
                : throw new InvalidOperationException("Checkpoint carries an unknown piece kind: " + value);
    }
}
