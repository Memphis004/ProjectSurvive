using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Simulation.Battle;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// The bot's in-battle policy: <b>kite</b> — run from whatever is close, while
    /// circling it and staying inside the arena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It reads nothing but <see cref="IBattleSession.Frame"/>, the same snapshot a
    /// Presenter gets, and it returns one <see cref="PlayerInputFrame"/> per tick. No
    /// randomness, no clock, no state of its own: the same frame and the same bot always
    /// produce the same input, which is what keeps a bot run reproducible from its seed.
    /// </para>
    /// <para>
    /// <b>These constants are bot tuning, not gameplay data.</b> They decide how a
    /// synthetic player flees and have no meaning inside the game — nothing in
    /// <c>lib/Runtime</c> reads them, and changing one changes the bot's skill rather than
    /// the game's balance. The GDD's own numbers all arrive through the config tables.
    /// </para>
    /// <para>
    /// The mixture is the classic one for this genre: a repulsion from every ghost inside
    /// <see cref="DangerRadius"/> (weighted by how close it is), a fixed-sense tangential
    /// term so the bot orbits instead of backing into a corner, and a centering term that
    /// grows near the rim so it never leaves the play area. The repulsion is summed before
    /// it is normalised, so two ghosts on opposite sides mostly cancel — which is the
    /// behaviour a human has too, and it is why the orbit term exists.
    /// </para>
    /// </remarks>
    internal sealed class KiteBot
    {
        /// <summary>How far away a ghost starts to matter, in world units.</summary>
        private const float DangerRadius = 9f;

        /// <summary>Weight of the "run away" term.</summary>
        private const float AwayWeight = 1f;

        /// <summary>Weight of the tangential "circle it" term.</summary>
        private const float OrbitWeight = 0.6f;

        /// <summary>Weight of the "get back inside" term.</summary>
        private const float CenterWeight = 2.4f;

        /// <summary>How close to the rim the bot starts steering inward, in world units.</summary>
        private const float EdgeMargin = 5f;

        /// <summary>One tick of movement.</summary>
        internal PlayerInputFrame Decide(IBattleSession session)
        {
            IBattleFrame frame = session.Frame;

            float playerX = 0f;
            float playerY = 0f;
            bool havePlayer = false;
            foreach (RenderEntity entity in frame.Entities)
            {
                if (entity.Kind == RenderEntityKind.Player)
                {
                    playerX = entity.X;
                    playerY = entity.Y;
                    havePlayer = true;
                    break;
                }
            }

            if (!havePlayer)
            {
                return PlayerInputFrame.Idle;
            }

            float awayX = 0f;
            float awayY = 0f;
            float orbitX = 0f;
            float orbitY = 0f;

            foreach (RenderEntity entity in frame.Entities)
            {
                if (entity.Kind != RenderEntityKind.Enemy)
                {
                    continue;
                }

                float dx = playerX - entity.X;
                float dy = playerY - entity.Y;
                float squared = (dx * dx) + (dy * dy);
                if (squared > DangerRadius * DangerRadius)
                {
                    continue;
                }

                float distance = (float)Math.Sqrt(squared);
                if (distance < 1e-4f)
                {
                    // Standing exactly on the player: pick a fixed direction rather than
                    // letting a division by zero decide.
                    dx = 1f;
                    dy = 0f;
                    distance = 1f;
                }

                float weight = 1f - (distance / DangerRadius);
                float unitX = dx / distance;
                float unitY = dy / distance;

                awayX += unitX * weight;
                awayY += unitY * weight;

                // A quarter turn counter-clockwise of the away vector: always the same
                // sense, so the bot cannot dither between two equally good directions.
                orbitX += -unitY * weight;
                orbitY += unitX * weight;
            }

            float directionX = (awayX * AwayWeight) + (orbitX * OrbitWeight);
            float directionY = (awayY * AwayWeight) + (orbitY * OrbitWeight);

            float radius = (float)Math.Sqrt((playerX * playerX) + (playerY * playerY));
            float limit = BattleArena.Radius - EdgeMargin;
            if (radius > limit && radius > 0f)
            {
                float urgency = 1f + ((radius - limit) / EdgeMargin);
                directionX += (-playerX / radius) * CenterWeight * urgency;
                directionY += (-playerY / radius) * CenterWeight * urgency;
            }

            if (directionX == 0f && directionY == 0f)
            {
                // Nothing to run from and no rim to avoid: drift, deterministically, so a
                // bot floor still produces a moving player (and therefore a moving frame).
                return PlayerInputFrame.FromAxis(1f, 0f);
            }

            return PlayerInputFrame.FromAxis(directionX, directionY);
        }
    }
}
