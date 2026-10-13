using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>What kind of thing a <see cref="RenderEntity"/> is, for view pooling.</summary>
    public enum RenderEntityKind : byte
    {
        /// <summary>Unused slot.</summary>
        None = 0,

        /// <summary>The player.</summary>
        Player = 1,

        /// <summary>An enemy ghost.</summary>
        Enemy = 2,

        /// <summary>A projectile.</summary>
        Projectile = 3,

        /// <summary>A pickup.</summary>
        Pickup = 4,
    }

    /// <summary>Optional tags on a <see cref="RenderEntity"/>.</summary>
    [Flags]
    public enum RenderEntityFlags : byte
    {
        /// <summary>No tags.</summary>
        None = 0,

        /// <summary>Elite enemy.</summary>
        Elite = 1,

        /// <summary>Boss enemy.</summary>
        Boss = 2,

        /// <summary>Facing left (mirror the sprite).</summary>
        FacingLeft = 4,
    }

    /// <summary>
    /// One entity in the render snapshot of a frame.
    /// </summary>
    /// <remarks>
    /// This is a *presentation* view of simulation state, produced once per tick.
    /// Presentation interpolates towards it; it must never write back, and it must
    /// never be used to decide a rule. <see cref="RotationY"/> is a yaw in degrees
    /// for the 2.5D sprite (billboard style).
    /// </remarks>
    public readonly struct RenderEntity
    {
        /// <summary>Creates a render entity.</summary>
        public RenderEntity(
            EntityRef entity,
            RenderEntityKind kind,
            float x,
            float y,
            float rotationY = 0f,
            float scale = 1f,
            float healthPercent = 1f,
            RenderEntityFlags flags = RenderEntityFlags.None)
        {
            Entity = entity;
            Kind = kind;
            X = x;
            Y = y;
            RotationY = rotationY;
            Scale = scale;
            HealthPercent = healthPercent;
            Flags = flags;
        }

        /// <summary>Handle to the simulation entity.</summary>
        public EntityRef Entity { get; }

        /// <summary>What kind of thing this is.</summary>
        public RenderEntityKind Kind { get; }

        /// <summary>World X.</summary>
        public float X { get; }

        /// <summary>World Y.</summary>
        public float Y { get; }

        /// <summary>Yaw in degrees.</summary>
        public float RotationY { get; }

        /// <summary>Uniform visual scale.</summary>
        public float Scale { get; }

        /// <summary>Remaining health as a fraction in <c>[0, 1]</c> (health bars, hit flash).</summary>
        public float HealthPercent { get; }

        /// <summary>Optional tags.</summary>
        public RenderEntityFlags Flags { get; }
    }
}
