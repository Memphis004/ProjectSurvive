using System;
using System.Collections.Generic;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>One fixed piece of the yard that the seeded layout does not draw.</summary>
    /// <remarks>
    /// A lamp or a sign has to stand where it lights or names something, so it is placed rather than
    /// rolled. The struct is data only: <c>CampEnvironmentBuilder</c> turns it into a card, the
    /// validation below turns it into an assertion.
    /// </remarks>
    public readonly struct CampFixture
    {
        public CampFixture(string styleId, float x, float z, float yaw)
        {
            StyleId = styleId;
            X = x;
            Z = z;
            Yaw = yaw;
        }

        /// <summary>The style in <c>CampTheme.FixtureStyles</c> this fixture is built from.</summary>
        public string StyleId { get; }

        /// <summary>World X.</summary>
        public float X { get; }

        /// <summary>World Z (the camera looks along +Z).</summary>
        public float Z { get; }

        /// <summary>Turn away from square-on, in degrees.</summary>
        public float Yaw { get; }
    }

    /// <summary>A warm pool of light on the ground, at a fixed spot.</summary>
    public readonly struct CampLightPool
    {
        public CampLightPool(float x, float z, float width, float depth)
        {
            X = x;
            Z = z;
            Width = width;
            Depth = depth;
        }

        public float X { get; }
        public float Z { get; }
        public float Width { get; }
        public float Depth { get; }
    }

    /// <summary>
    /// ศาลาพัก's yard: the one deterministic function from the camp theme to the pieces that surround
    /// the sala, plus the rules that keep the yard walkable and the report honest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The layout itself is <see cref="ArenaLayout.Build"/> — the same generator the arena uses, with
    /// the camp's numbers and its own fixed seed, so "seed เดิมได้ลานเดิม" is a property of the same
    /// code path and not of a second implementation. Only two camp-specific adjustments are made
    /// here, and both are filters rather than rules:
    /// </para>
    /// <list type="bullet">
    /// <item>centre grass stops at <see cref="GrassRadius"/>, so no tuft grows in the band where the
    /// shelves, the stall and the pot stand — the arena lets its centre grass reach the whole
    /// disc because it has no furniture in it;</item>
    /// <item>the arena's big starting dirt patch is dropped and a smaller one is placed at the
    /// origin instead: at the camp's scale the arena's 11 × 8 patch would cover the whole sala
    /// floor rather than mark the middle of it.</item>
    /// </list>
    /// <para>
    /// <b>Everything tall stands outside the walk rectangle.</b> <see cref="Validate"/> is the
    /// acceptance rule "ไม่มีของสูงในพื้นที่เดิน" as a function: a standing piece higher than
    /// <see cref="ArenaTheme.CenterMaxHeight"/> must not overlap the rectangle the player may walk in,
    /// and a fixed lamp or sign must not either.
    /// </para>
    /// </remarks>
    public static class CampYard
    {
        /// <summary>The yard's seed. Fixed: the same camp is built on every run and every machine.</summary>
        public const uint YardSeed = 20261025u;

        /// <summary>How far from the sala's centre the low grass may grow.</summary>
        public const float GrassRadius = 9.2f;

        /// <summary>
        /// The clearing's frame: only the sides and the strip behind the sala keep a standing card.
        /// </summary>
        /// <remarks>
        /// A full ring is wrong for this camera. The camp's orthographic rig looks over roughly 11
        /// world units of ground toward the viewer and 13 away from them (CampWorldView's follow
        /// camera at orthographic size 6.6), so a ring at radius 10 stands <i>between the camera and
        /// the player</i> and fills the lower half of the frame with shrubs. The yard therefore keeps
        /// the generator's pieces only where they can be a frame: at |x| ≥ 9.5 for the two sides, and
        /// at z ≥ 5.4 for the strip behind the sala, never nearer than z = −4 (which would be a bush
        /// in front of the player) and never further than z = 9 (which the camera cannot see at all).
        /// The centre's grass and the decals are unaffected: those are what a player walks over.
        /// </remarks>
        public const float FrameSideX = 9.5f;

        /// <summary>
        /// The strip behind the sala where a card may stand at any x. It starts one row behind the
        /// shelf boards (z = 4.0) so the sala has a backdrop rather than an empty horizon.
        /// </summary>
        public const float FrameBackZ = 4.8f;

        /// <summary>Nothing stands nearer to the camera than this row.</summary>
        public const float FrameNearZ = -4f;

        /// <summary>
        /// Nothing stands further than this row: past it the sala's backdrop is off screen.
        /// </summary>
        /// <remarks>
        /// The camera's far ground edge is about 24 units in front of it and the camera sits about
        /// 10.5 behind the player, so a card at z = 12 still lands inside the frame when the player
        /// is at the shelf row — and a sala with nothing behind it reads as a table in a field.
        /// </remarks>
        public const float FrameFarZ = 12f;

        /// <summary>How deep a standing card's footprint is, in world units (its base line).</summary>
        public const float FootprintDepth = 0.5f;

        /// <summary>The two lamps that flank the shelf row.</summary>
        public static readonly CampFixture[] Fixtures =
        {
            new CampFixture("lamp", -6.4f, 3.8f, 0f),
            new CampFixture("lamp", 6.4f, 3.8f, 0f),
            new CampFixture("sign", -9.4f, 4.1f, 7f),
        };

        /// <summary>
        /// The warm pools: one under each shelf station, one at the เซียมซี cylinder, one at the
        /// incense pot, one under the stall and one in the middle of the yard.
        /// </summary>
        public static readonly CampLightPool[] LightPools =
        {
            new CampLightPool(-4.5f, 3.15f, 3.0f, 2.0f),
            new CampLightPool(-1.5f, 3.15f, 3.0f, 2.0f),
            new CampLightPool(1.5f, 3.15f, 3.0f, 2.0f),
            new CampLightPool(4.5f, 3.15f, 3.0f, 2.0f),
            new CampLightPool(-7.2f, 0.4f, 2.6f, 2.6f),
            new CampLightPool(7.6f, 2.2f, 2.2f, 2.2f),
            new CampLightPool(-7.0f, 4.5f, 4.4f, 3.2f),
            new CampLightPool(0f, 0.2f, 5.2f, 3.4f),
        };

        /// <summary>Where the yard's own origin patch is, and how big (a fraction of the dirt style).</summary>
        public const float OriginPatchScale = 0.75f;

        /// <summary>Builds the yard's seeded pieces for the camp theme.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
        public static ArenaPlacement[] Build(CampTheme theme)
        {
            if (theme is null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            ArenaPlacement[] raw = ArenaLayout.Build(theme, YardSeed);
            var kept = new List<ArenaPlacement>(raw.Length + 1);
            int dirt = IndexOf(theme.DecalStyles, "dirt");

            for (int i = 0; i < raw.Length; i++)
            {
                ArenaPlacement placement = raw[i];

                // Low grass is allowed in the yard, not in the furniture band behind it.
                if (placement.Zone == ArenaCardZone.Center && placement.Radius > GrassRadius)
                {
                    continue;
                }

                // A standing card is part of the frame or it is not built: see FrameSideX.
                if (!placement.IsDecal && !IsFrame(placement.X, placement.Z))
                {
                    continue;
                }

                // The arena's starting patch, at the scale the camp cannot afford (see the remarks).
                if (placement.IsDecal && placement.Radius < 0.05f)
                {
                    continue;
                }

                kept.Add(placement);
            }

            if (dirt >= 0 && theme.DecalStyles[dirt] is { } style)
            {
                float scale = OriginPatchScale;
                kept.Add(new ArenaPlacement(
                    ArenaCardZone.Decal, dirt, style.Id, 0f, 0f,
                    style.Width * scale, 0.02f, scale, 0f));
            }

            return kept.ToArray();
        }

        /// <summary>
        /// Every rule the yard has to obey, on top of the layout's own
        /// (<see cref="ArenaLayout.Validate"/>). An empty list means the yard is legal.
        /// </summary>
        public static List<string> Validate(
            CampTheme theme, IReadOnlyList<ArenaPlacement> placements, Rect walkArea)
        {
            if (theme is null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            var errors = new List<string>();

            if (placements is not null)
            {
                for (int i = 0; i < placements.Count; i++)
                {
                    ArenaPlacement placement = placements[i];
                    if (placement.IsDecal || placement.Height <= theme.CenterMaxHeight + 0.001f)
                    {
                        continue;
                    }

                    if (Footprint(placement.X, placement.Z, placement.Width).Overlaps(walkArea))
                    {
                        errors.Add(
                            placement.StyleId + " is " + placement.Height.ToString("0.00")
                            + " tall and stands in the walking area at (" + placement.X.ToString("0.00")
                            + ", " + placement.Z.ToString("0.00") + ")");
                    }
                }
            }

            for (int i = 0; i < Fixtures.Length; i++)
            {
                CampFixture fixture = Fixtures[i];
                ArenaCardStyle style = StyleOf(theme.FixtureStyles, fixture.StyleId);
                if (style is null)
                {
                    errors.Add("the fixture style '" + fixture.StyleId + "' is not in the camp theme");
                    continue;
                }

                if (Footprint(fixture.X, fixture.Z, style.Width).Overlaps(walkArea))
                {
                    errors.Add(
                        "the fixture '" + fixture.StyleId + "' at (" + fixture.X.ToString("0.00") + ", "
                        + fixture.Z.ToString("0.00") + ") stands in the walking area");
                }
            }

            return errors;
        }

        /// <summary>True for a spot the clearing's frame may occupy (its two sides or its back).</summary>
        public static bool IsFrame(float x, float z) =>
            z >= FrameNearZ
            && z <= FrameFarZ
            && (Mathf.Abs(x) >= FrameSideX || z >= FrameBackZ);

        /// <summary>The ground rectangle a standing piece occupies (X by a thin band of Z).</summary>
        public static Rect Footprint(float x, float z, float width) =>
            Rect.MinMaxRect(
                x - (width * 0.5f), z - (FootprintDepth * 0.5f),
                x + (width * 0.5f), z + (FootprintDepth * 0.5f));

        /// <summary>One style of a set by its stable id, or null.</summary>
        public static ArenaCardStyle StyleOf(ArenaCardStyle[] styles, string id)
        {
            if (styles is null)
            {
                return null;
            }

            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] is not null && styles[i].Id == id)
                {
                    return styles[i];
                }
            }

            return null;
        }

        private static int IndexOf(ArenaCardStyle[] styles, string id)
        {
            if (styles is null)
            {
                return -1;
            }

            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] is not null && styles[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
