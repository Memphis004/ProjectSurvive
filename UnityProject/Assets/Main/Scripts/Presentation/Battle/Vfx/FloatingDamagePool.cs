using System;
using System.Collections.Generic;
using System.Globalization;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Shared;
using TMPro;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Vfx
{
    /// <summary>
    /// The pool of floating damage numbers: one preallocated label per possible number,
    /// never an <c>Instantiate</c> during a fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A damage number is the classic allocation trap — one per hit, a string per hit, a
    /// GameObject per hit — so it is pooled like everything else on the hot path
    /// (AGENTS rule 8) and driven from <see cref="Tick"/> by the runner rather than by a
    /// <c>MonoBehaviour.Update</c> per number, which would be one managed call per number per
    /// frame.
    /// </para>
    /// <para>
    /// Its randomness comes from the <b>visual</b> stream
    /// (<see cref="RandomStreams.Visual"/>), never from the battle stream and never from
    /// <c>UnityEngine.Random</c>: the jitter of a label may not be able to change a battle,
    /// and a replay must not depend on how many numbers were on screen.
    /// </para>
    /// <para>
    /// The text of small numbers is cached, because <c>int.ToString()</c> allocates and a
    /// floor-1 wave deals damage dozens of times a second.
    /// </para>
    /// </remarks>
    public sealed class FloatingDamagePool : IBattleEventListener, IDisposable
    {
        /// <summary>Numbers up to this value are formatted once, at warm-up.</summary>
        private const int CachedTextLimit = 200;

        private sealed class Number
        {
            internal GameObject GameObject;
            internal TextMeshPro Text;
            internal float Remaining;
            internal Vector3 Origin;
            internal Vector3 Drift;
            internal Color Colour;
        }

        private readonly Number[] _numbers = new Number[BattleViewCaps.DamageNumbers];
        private readonly string[] _cachedText = new string[CachedTextLimit + 1];
        private readonly IRandomSource _visual;
        private readonly float _pitch;
        private int _next;

        private bool _disposed;

        /// <summary>Creates and warms the pool.</summary>
        public FloatingDamagePool(Transform root, TMP_FontAsset font, IRandomSource visual, float billboardPitch)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _visual = visual ?? throw new ArgumentNullException(nameof(visual));
            _pitch = billboardPitch;

            for (int i = 0; i < _cachedText.Length; i++)
            {
                _cachedText[i] = i.ToString(CultureInfo.InvariantCulture);
            }

            for (int i = 0; i < _numbers.Length; i++)
            {
                _numbers[i] = Create(root, font, i);
            }
        }

        /// <summary>Numbers currently alive.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Labels the pool owns (constant after construction).</summary>
        public int Capacity => _numbers.Length;

        /// <summary>
        /// Turns a damage event into a label. Registered on
        /// <see cref="BattleEventChannel.Vfx"/>; it only reads <c>X</c>/<c>Y</c>/<c>Value</c>
        /// and never decides anything.
        /// </summary>
        public void OnBattleEvent(in BattleEvent battleEvent)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.DamageDealt:
                    Emit(battleEvent.X, battleEvent.Y, battleEvent.Value, EnemyDamageColour);
                    break;
                case BattleEventType.PlayerDamaged:
                    Emit(battleEvent.X, battleEvent.Y, battleEvent.Value, PlayerDamageColour);
                    break;
                default:
                    break;
            }
        }

        /// <summary>Shows one number at a simulation position.</summary>
        public void Emit(float simX, float simY, int amount, Color colour)
        {
            if (amount <= 0 || _disposed)
            {
                return;
            }

            Number number = Next();

            number.Remaining = BattleViewCaps.DamageNumberLifetimeSeconds;
            number.Origin = BattleWorldMap.ToWorld(simX, simY) + new Vector3(0f, 0.8f, 0f);
            number.Colour = colour;

            // Cosmetic jitter and a slightly varied rise, from the visual stream only.
            number.Drift = new Vector3(
                (_visual.NextFloat() * 2f - 1f) * BattleViewCaps.DamageNumberJitter,
                BattleViewCaps.DamageNumberRise * (0.75f + (_visual.NextFloat() * 0.5f)),
                (_visual.NextFloat() * 2f - 1f) * BattleViewCaps.DamageNumberJitter * 0.5f);

            number.Text.text = Text(amount);
            number.Text.color = number.Colour;
            number.GameObject.transform.SetPositionAndRotation(number.Origin, Quaternion.Euler(_pitch, 0f, 0f));
            number.GameObject.SetActive(true);
            ActiveCount++;
        }

        /// <summary>Advances every live number. Called once per rendered frame.</summary>
        public void Tick(float deltaSeconds)
        {
            if (ActiveCount == 0 || !(deltaSeconds > 0f))
            {
                return;
            }

            for (int i = 0; i < _numbers.Length; i++)
            {
                Number number = _numbers[i];
                if (number.Remaining <= 0f)
                {
                    continue;
                }

                number.Remaining -= deltaSeconds;
                if (number.Remaining <= 0f)
                {
                    Retire(number);
                    continue;
                }

                if (number.GameObject == null)
                {
                    // The scene was destroyed under the pool (a test or an editor teardown order
                    // Unity does not promise). Nothing is left to move.
                    continue;
                }

                float progress = 1f - (number.Remaining / BattleViewCaps.DamageNumberLifetimeSeconds);
                number.GameObject.transform.position = number.Origin + (number.Drift * progress);

                Color fade = number.Colour;
                fade.a = number.Colour.a * (1f - (progress * progress));
                number.Text.color = fade;
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

            for (int i = 0; i < _numbers.Length; i++)
            {
                Number number = _numbers[i];

                // Unity's own == operator: a torn-down scene has already destroyed these labels.
                if (number is null || number.GameObject == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(number.GameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(number.GameObject);
                }

                number.GameObject = null;
            }

            ActiveCount = 0;
        }

        private static Color EnemyDamageColour => new Color(1f, 0.93f, 0.72f, 1f);

        private static Color PlayerDamageColour => new Color(1f, 0.45f, 0.42f, 1f);

        private Number Next()
        {
            // Round-robin rather than a free list: a number's slot is only reused after every
            // other slot was, which is what keeps a burst of hits from overwriting itself.
            for (int attempt = 0; attempt < _numbers.Length; attempt++)
            {
                Number candidate = _numbers[_next];
                _next = (_next + 1) % _numbers.Length;
                if (candidate.Remaining <= 0f)
                {
                    return candidate;
                }
            }

            // Every slot is busy: take the next one anyway and let it restart. Losing one
            // number is a cosmetic loss, and the pool never grows.
            Number forced = _numbers[_next];
            _next = (_next + 1) % _numbers.Length;
            Retire(forced);
            return forced;
        }

        private void Retire(Number number)
        {
            number.Remaining = 0f;
            if (number.GameObject != null && number.GameObject.activeSelf)
            {
                number.GameObject.SetActive(false);
            }

            if (ActiveCount > 0)
            {
                ActiveCount--;
            }
        }

        private string Text(int amount) =>
            amount >= 0 && amount <= CachedTextLimit
                ? _cachedText[amount]
                : amount.ToString(CultureInfo.InvariantCulture);

        private Number Create(Transform root, TMP_FontAsset font, int index)
        {
            var gameObject = new GameObject("damage-number-" + index.ToString(CultureInfo.InvariantCulture));
            gameObject.transform.SetParent(root, worldPositionStays: false);

            TextMeshPro text = gameObject.AddComponent<TextMeshPro>();
            if (font is not null)
            {
                text.font = font;
            }

            text.fontSize = 4f;
            text.alignment = TextAlignmentOptions.Center;
            text.sortingOrder = BattleViewCaps.FloatingTextSortingOrder;
            text.text = string.Empty;
            gameObject.SetActive(false);

            return new Number { GameObject = gameObject, Text = text };
        }
    }
}
