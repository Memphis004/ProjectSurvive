using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Contracts.Save;
using ProjectSurvive.Core.Domain.Inventory;

namespace ProjectSurvive.Core.Infrastructure.Save
{
    /// <summary>
    /// The <see cref="IBackpackSnapshotCodec"/> adapter: maps the domain snapshot to the
    /// MessagePack DTO in <c>Contracts</c> and back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the documented direction for <c>Contracts</c> — "ใช้ผ่าน adapter ใน
    /// Infrastructure เท่านั้น" — and it is what keeps AGENTS rule 3 intact: <c>Domain</c>
    /// owns the snapshot as plain data and never sees a serializer, while the encoder
    /// lives in the layer that is allowed to know one.
    /// </para>
    /// <para>
    /// The enum mapping is explicit and numeric, so an illegal value coming back from
    /// bytes is caught here (with a clear exception) instead of silently becoming an
    /// out-of-range domain enum.
    /// </para>
    /// </remarks>
    public sealed class MessagePackBackpackSnapshotCodec : IBackpackSnapshotCodec
    {
        /// <summary>True when the generated (AOT-safe) formatter exists in this build.</summary>
        public static bool GeneratedFormatterIsPresent => BackpackSnapshotCodec.GeneratedFormatterIsPresent;

        /// <inheritdoc />
        public byte[] Encode(BackpackSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return BackpackSnapshotCodec.Serialize(ToDto(snapshot));
        }

        /// <inheritdoc />
        public BackpackSnapshot Decode(byte[] bytes)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return FromDto(BackpackSnapshotCodec.Deserialize(bytes));
        }

        /// <summary>Domain snapshot to DTO.</summary>
        public static BackpackSnapshotDto ToDto(BackpackSnapshot snapshot)
        {
            IReadOnlyList<PieceSnapshot> pieces = snapshot.Pieces;
            var dto = new BackpackSnapshotDto
            {
                SchemaVersion = snapshot.SchemaVersion,
                NextInstanceId = snapshot.NextInstanceId,
                MergeCount = snapshot.MergeCount,
                Pieces = new List<BackpackPieceDto>(pieces.Count),
            };

            for (int i = 0; i < pieces.Count; i++)
            {
                PieceSnapshot piece = pieces[i];
                dto.Pieces.Add(new BackpackPieceDto
                {
                    Kind = (int)piece.Kind,
                    InstanceId = piece.InstanceId,
                    DefId = piece.DefId,
                    Tier = piece.Tier,
                    Zone = (int)piece.Zone,
                    OriginX = piece.OriginX,
                    OriginY = piece.OriginY,
                    Rotation = (int)piece.Rotation,
                    ChestSlot = piece.ChestSlot,
                    VariantIds = ToVariantList(piece.VariantIds),
                });
            }

            return dto;
        }

        /// <summary>DTO to domain snapshot.</summary>
        public static BackpackSnapshot FromDto(BackpackSnapshotDto dto)
        {
            var pieces = new List<PieceSnapshot>(dto.Pieces.Count);
            for (int i = 0; i < dto.Pieces.Count; i++)
            {
                BackpackPieceDto piece = dto.Pieces[i];
                pieces.Add(new PieceSnapshot
                {
                    Kind = ToKind(piece.Kind),
                    InstanceId = piece.InstanceId,
                    DefId = piece.DefId,
                    Tier = piece.Tier,
                    Zone = ToZone(piece.Zone),
                    OriginX = piece.OriginX,
                    OriginY = piece.OriginY,
                    Rotation = ToRotation(piece.Rotation),
                    ChestSlot = piece.ChestSlot,
                    VariantIds = FromVariantList(piece.VariantIds),
                });
            }

            return new BackpackSnapshot
            {
                SchemaVersion = dto.SchemaVersion,
                NextInstanceId = dto.NextInstanceId,
                MergeCount = dto.MergeCount,
                Pieces = pieces,
            };
        }

        /// <summary>Domain variant history to a wire list (a copy, never the live list).</summary>
        private static List<string> ToVariantList(IReadOnlyList<string> variantIds)
        {
            var list = new List<string>(variantIds.Count);
            for (int i = 0; i < variantIds.Count; i++)
            {
                list.Add(variantIds[i] ?? string.Empty);
            }

            return list;
        }

        /// <summary>Wire list to the domain shape; a null entry cannot become a variant id.</summary>
        private static IReadOnlyList<string> FromVariantList(List<string>? variantIds)
        {
            if (variantIds is null || variantIds.Count == 0)
            {
                return System.Array.Empty<string>();
            }

            var list = new string[variantIds.Count];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = variantIds[i] ?? string.Empty;
            }

            return list;
        }

        private static PieceKind ToKind(int value) =>
            value is (int)PieceKind.Bag or (int)PieceKind.Item
                ? (PieceKind)value
                : throw new InvalidOperationException("Backpack snapshot carries an unknown piece kind: " + value);

        private static PieceZone ToZone(int value) =>
            value is (int)PieceZone.Board or (int)PieceZone.Chest
                ? (PieceZone)value
                : throw new InvalidOperationException("Backpack snapshot carries an unknown piece zone: " + value);

        private static Rotation ToRotation(int value) =>
            value is >= 0 and <= 3
                ? (Rotation)value
                : throw new InvalidOperationException("Backpack snapshot carries an unknown rotation: " + value);
    }
}
