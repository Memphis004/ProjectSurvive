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
    /// <remarks>
    /// <b>Stage 11d: two engagements.</b> The kiting policy above is the ranged / summoner
    /// answer. A melee build gets <see cref="Engagement.Close"/>: the bot walks onto the
    /// nearest ghost and orbits it at arm's length instead of leaving, because a knife with a
    /// 1.5-unit reach is unusable from outside that ring — measuring it while fleeing would
    /// report "the knife is weak" when the truth is "the bot never swung it".
    /// </remarks>
    internal sealed class KiteBot
    {
        /// <summary>How far away a ghost starts to matter, in world units.</summary>
        private const float DangerRadius = 9f;

        /// <summary>
        /// The ring a closing bot tries to hold around its target, in world units: beyond its
        /// own weapon it walks in, inside <see cref="EngageRange"/> minus a margin it backs
        /// out, and in between it circles. The knife reaches 1.5 (+ the ghost's radius), a
        /// ghost's touch reaches 0.85, so this band is the one where the bot swings without
        /// being touched.
        /// </summary>
        private const float EngageRange = 1.7f;

        /// <summary>How far inside the ring a closing bot will let the target come before it backs out.</summary>
        private const float EngageSlack = 0.35f;

        /// <summary>Beyond this a closing bot simply walks at the nearest ghost.</summary>
        private const float EngageChaseRange = 20f;

        private readonly Engagement _engagement;

        /// <summary>The default kiting bot (Stage 07 behaviour, unchanged).</summary>
        internal KiteBot()
            : this(Engagement.Kite)
        {
        }

        /// <summary>A bot that plays the way <paramref name="engagement"/> describes.</summary>
        internal KiteBot(Engagement engagement)
        {
            _engagement = engagement;
        }

        /// <summary>Weight of the "run away" term.</summary>
        private const float AwayWeight = 1f;

        /// <summary>Weight of the tangential "circle it" term.</summary>
        private const float OrbitWeight = 0.6f;

        /// <summary>Weight of the "get back inside" term.</summary>
        private const float CenterWeight = 2.4f;

        /// <summary>How close to the rim the bot starts steering inward, in world units.</summary>
        private const float EdgeMargin = 5f;

        /// <summary>How many ghosts have to be near a closing bot before it stops swinging.</summary>
        private const int CrowdLimit = 6;

        /// <summary>How near a ghost has to be to count as part of a crowd, in world units.</summary>
        private const float CrowdRadius = 3f;

        /// <summary>Below this fraction of max HP a closing bot stops trading and runs.</summary>
        private const float DisengageHpPercent = 0.45f;

        /// <summary>One tick of movement.</summary>
        internal PlayerInputFrame Decide(IBattleSession session)
        {
            return _engagement == Engagement.Close ? DecideClose(session) : Kite(session.Frame);
        }

        /// <summary>The kiting policy: run from what is close, orbit it, stay inside the arena.</summary>
        private static PlayerInputFrame Kite(IBattleFrame frame)
        {

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

        /// <summary>How many ghosts are within <see cref="CrowdRadius"/> of a point.</summary>
        private static int CrowdCount(IBattleFrame frame, float x, float y)
        {
            int count = 0;
            float limit = CrowdRadius * CrowdRadius;

            foreach (RenderEntity entity in frame.Entities)
            {
                if (entity.Kind != RenderEntityKind.Enemy)
                {
                    continue;
                }

                float dx = entity.X - x;
                float dy = entity.Y - y;
                if (((dx * dx) + (dy * dy)) <= limit)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The close-range policy: walk at the nearest ghost, orbit it once inside arm's
        /// length, and never leave the arena.
        /// </summary>
        /// <remarks>
        /// "Nearest" is taken in frame order with a strict comparison, so two equidistant
        /// ghosts resolve to the first one and the bot stays deterministic. Once inside
        /// <see cref="EngageRange"/> the bot circles the target rather than standing still:
        /// standing in a crowd is how a melee run dies to contact damage it never answered.
        /// </remarks>
        private static PlayerInputFrame DecideClose(IBattleSession session)
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

            float targetX = 0f;
            float targetY = 0f;
            float best = EngageChaseRange * EngageChaseRange;
            bool haveTarget = false;

            foreach (RenderEntity entity in frame.Entities)
            {
                if (entity.Kind != RenderEntityKind.Enemy)
                {
                    continue;
                }

                float dx = entity.X - playerX;
                float dy = entity.Y - playerY;
                float squared = (dx * dx) + (dy * dy);
                if (squared < best)
                {
                    best = squared;
                    targetX = entity.X;
                    targetY = entity.Y;
                    haveTarget = true;
                }
            }

            float directionX;
            float directionY;

            if (!haveTarget)
            {
                // Nothing on the floor: walk to the middle so the next wave arrives.
                directionX = -playerX;
                directionY = -playerY;
            }
            else
            {
                float dx = targetX - playerX;
                float dy = targetY - playerY;
                float distance = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (distance < 1e-4f)
                {
                    dx = 1f;
                    dy = 0f;
                    distance = 1f;
                }

                // A knife run trades damage, so it plays in two states: swing while there
                // is health to spend, run when there is not — or when the pile is so deep
                // that standing in it is suicide rather than a trade (Stage 11d).
                int maxHp = frame.Hud.MaxHp;
                bool low = maxHp > 0 && frame.Hud.Hp < maxHp * DisengageHpPercent;
                if (low || CrowdCount(frame, playerX, playerY) > CrowdLimit)
                {
                    return Kite(frame);
                }

                if (distance > EngageRange)
                {
                    directionX = dx;
                    directionY = dy;
                }
                else if (distance < EngageRange - EngageSlack)
                {
                    // Too deep: back out to the ring rather than trading touches.
                    directionX = -dx;
                    directionY = -dy;
                }
                else
                {
                    // In the band: circle the target (a quarter turn of the vector to it).
                    directionX = -dy;
                    directionY = dx;
                }
            }

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
                return PlayerInputFrame.FromAxis(-1f, 0f);
            }

            return PlayerInputFrame.FromAxis(directionX, directionY);
        }
    }
}
