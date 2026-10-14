using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>Which consumer of the event stream a listener belongs to.</summary>
    /// <remarks>
    /// Each channel owns its own cursor into <see cref="IBattleEventSource"/>. They are
    /// separate so a slow consumer (a damage number that had to be pooled, an audio voice
    /// that was already playing) cannot cost another one its events, and so
    /// "the VFX dropped 4 events" is a fact rather than a guess.
    /// </remarks>
    public enum BattleEventChannel
    {
        /// <summary>The HUD: wave banners, the boss bar appearing, the end banner.</summary>
        Hud = 0,

        /// <summary>Visual effects: floating damage numbers, hit flashes.</summary>
        Vfx = 1,

        /// <summary>Audio cues. Silent in Stage 08, but it reads at its own pace.</summary>
        Audio = 2,
    }

    /// <summary>Something that reacts to a battle event.</summary>
    public interface IBattleEventListener
    {
        /// <summary>Handles one event. Must not touch simulation state.</summary>
        void OnBattleEvent(in BattleEvent battleEvent);
    }

    /// <summary>
    /// Reads the battle's event stream once per tick and fans it out to the channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Events are for presentation only ([battle-contract.md §5]): nothing here decides an
    /// outcome, and losing an event may not distort one. A reader that falls behind the ring
    /// is told how many events it lost; the pump accumulates that per channel, logs it in a
    /// development build, and moves on — the alternative (blocking, or replaying a backlog)
    /// would turn a slow frame into a wrong frame.
    /// </para>
    /// <para>
    /// The pump owns one event buffer, reused for every channel read, so a tick costs no
    /// allocation.
    /// </para>
    /// </remarks>
    public sealed class BattleEventPump : IDisposable
    {
        private const int BufferCapacity = 256;

        private struct Lane
        {
            internal long Cursor;
            internal long Dropped;
            internal long Consumed;
            internal bool Started;
        }

        private readonly Lane[] _lanes = new Lane[3];
        private readonly BattleEvent[] _buffer = new BattleEvent[BufferCapacity];
        private readonly List<IBattleEventListener>[] _listeners;

        private bool _disposed;

        /// <summary>Creates a pump with three empty channels.</summary>
        public BattleEventPump()
        {
            _listeners = new List<IBattleEventListener>[3];
            for (int i = 0; i < _listeners.Length; i++)
            {
                _listeners[i] = new List<IBattleEventListener>(4);
            }
        }

        /// <summary>Registers a listener on a channel.</summary>
        public void AddListener(BattleEventChannel channel, IBattleEventListener listener)
        {
            if (listener is null)
            {
                return;
            }

            _listeners[(int)channel].Add(listener);
        }

        /// <summary>Events read on <paramref name="channel"/> since the scene started.</summary>
        public long Consumed(BattleEventChannel channel) => _lanes[(int)channel].Consumed;

        /// <summary>Events lost on <paramref name="channel"/> since the scene started.</summary>
        public long Dropped(BattleEventChannel channel) => _lanes[(int)channel].Dropped;

        /// <summary>Events lost on every channel, for a single "the view fell behind" signal.</summary>
        public long DroppedTotal =>
            _lanes[(int)BattleEventChannel.Hud].Dropped
            + _lanes[(int)BattleEventChannel.Vfx].Dropped
            + _lanes[(int)BattleEventChannel.Audio].Dropped;

        /// <summary>
        /// Reads everything unread on <paramref name="source"/> and dispatches it. Call once
        /// per tick, after the session stepped.
        /// </summary>
        public void Drain(IBattleEventSource source)
        {
            if (source is null || _disposed)
            {
                return;
            }

            long oldest = source.OldestSeq;

            for (int laneIndex = 0; laneIndex < _lanes.Length; laneIndex++)
            {
                ref Lane lane = ref _lanes[laneIndex];

                if (!lane.Started)
                {
                    // A new reader starts at the oldest event still in the ring; anything the
                    // session published before the scene existed is not this reader's loss.
                    lane.Cursor = oldest;
                    lane.Started = true;
                }

                int count = source.Read(ref lane.Cursor, _buffer, out int dropped);
                lane.Dropped += dropped;

                if (dropped > 0)
                {
                    ReportDropped((BattleEventChannel)laneIndex, dropped);
                }

                if (count == 0)
                {
                    continue;
                }

                lane.Consumed += count;

                List<IBattleEventListener> listeners = _listeners[laneIndex];
                for (int i = 0; i < listeners.Count; i++)
                {
                    IBattleEventListener listener = listeners[i];
                    for (int e = 0; e < count; e++)
                    {
                        listener.OnBattleEvent(in _buffer[e]);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _listeners.Length; i++)
            {
                _listeners[i].Clear();
            }
        }

        private static void ReportDropped(BattleEventChannel channel, int dropped)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // A dev-build diagnostic, never a gameplay reaction: dropping a VFX event may
            // not move HP, currency or a spawn, and the HUD's numbers come from the frame.
            Debug.LogWarning(
                "[ProjectSurvive] battle event channel " + channel + " dropped " + dropped
                + " event(s); the view fell behind the ring.");
#endif
        }
    }
}
