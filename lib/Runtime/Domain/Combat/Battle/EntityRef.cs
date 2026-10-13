using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// A stable handle to an entity inside one battle session.
    /// </summary>
    /// <remarks>
    /// <see cref="Index"/> names a slot and <see cref="Version"/> is bumped every
    /// time the slot is reused, so a handle held across a recycle is detectable
    /// instead of silently pointing at a different entity (the EcsLite
    /// liveness lesson, see <c>llmwiki/gotchas/ecslite-pool-refs.md</c>).
    /// <see cref="None"/> is the all-zero value, so a default
    /// <see cref="EntityRef"/> never refers to a live entity.
    /// </remarks>
    public readonly struct EntityRef : IEquatable<EntityRef>
    {
        /// <summary>An invalid reference.</summary>
        public static readonly EntityRef None = default;

        /// <summary>Creates a reference. Slot indices start at 1, so 0 means "none".</summary>
        public EntityRef(int index, int version)
        {
            Index = index;
            Version = version;
        }

        /// <summary>1-based slot index; 0 when this is <see cref="None"/>.</summary>
        public int Index { get; }

        /// <summary>Generation of the slot; 0 when this is <see cref="None"/>.</summary>
        public int Version { get; }

        /// <summary>True when this reference identifies a live entity.</summary>
        public bool IsValid => Index > 0 && Version > 0;

        /// <inheritdoc />
        public bool Equals(EntityRef other) => Index == other.Index && Version == other.Version;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is EntityRef other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Index * 397) ^ Version;

        /// <inheritdoc />
        public override string ToString() => IsValid ? "#" + Index + "v" + Version : "#none";

        public static bool operator ==(EntityRef left, EntityRef right) => left.Equals(right);

        public static bool operator !=(EntityRef left, EntityRef right) => !left.Equals(right);
    }
}
