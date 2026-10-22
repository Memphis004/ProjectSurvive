using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.Audio;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Turns the battle's event stream into sound (Stage 13c-1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It sits on <see cref="BattleEventChannel.Audio"/>, the channel the pump reserved for exactly
    /// this in Stage 08, so a burst of hits that the VFX channel is still drawing cannot cost the
    /// sound its events — and vice versa. It reads only the event's type: no rule is consulted, and
    /// nothing here can change an outcome (the event contract's own rule).
    /// </para>
    /// <para>
    /// The mapping is deliberately coarse. A placeholder cue should say "something landed", not
    /// "a variant-3 arcane weapon with +20 % from a tome landed": the numbers are data, and a sound
    /// per variant would be a content decision this stage has no authority to make.
    /// </para>
    /// </remarks>
    public sealed class BattleAudioListener : IBattleEventListener
    {
        private readonly AudioDirector _audio;

        /// <summary>Creates a listener that plays through <paramref name="audio"/>.</summary>
        public BattleAudioListener(AudioDirector audio)
        {
            _audio = audio;
        }

        /// <summary>Events this listener has asked the director to play.</summary>
        public long CuesRequested { get; private set; }

        /// <inheritdoc />
        public void OnBattleEvent(in BattleEvent battleEvent)
        {
            if (_audio is null)
            {
                return;
            }

            switch (battleEvent.Type)
            {
                case BattleEventType.DamageDealt:
                    Play(AudioCue.Hit);
                    break;
                case BattleEventType.PlayerDamaged:
                    Play(AudioCue.Hurt);
                    break;
                case BattleEventType.EnemyKilled:
                    Play(AudioCue.Kill);
                    break;
                case BattleEventType.PickupCollected:
                    Play(AudioCue.Pickup);
                    break;
                case BattleEventType.TelegraphStarted:
                    Play(AudioCue.Telegraph);
                    break;
                case BattleEventType.BossPhaseChanged:
                    Play(AudioCue.BossPhase);
                    break;
                default:
                    // Spawns, projectiles, status ticks and the end banner are silent: they are
                    // either too frequent to be a cue or already said by the music stopping.
                    break;
            }
        }

        private void Play(AudioCue cue)
        {
            CuesRequested++;
            _audio.Play(cue);
        }
    }
}
