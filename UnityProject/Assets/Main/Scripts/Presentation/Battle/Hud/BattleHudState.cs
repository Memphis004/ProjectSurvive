using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// Everything the battle HUD shows, as reactive values a View mirrors into widgets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10 draws this line hard: "R3 ส่งเฉพาะสถานะระดับ UI ห้ามส่งตำแหน่งทุก entity".
    /// So every member here is a <b>scalar, a string or a small static model</b> — HP, MP, bia,
    /// seconds, progress, a localized caption — and there is deliberately no observable that
    /// carries an entity, a position or a frame. Entities reach the screen through the pooled
    /// views the render bridge drives, never through a subscription, which is why a floor with
    /// 300 ghosts costs the same number of subscriptions as a floor with one.
    /// </para>
    /// <para>
    /// Both halves of a readout are here: the <i>caption</i> ("ชีวิต", "เบี้ย") which comes from
    /// localization, and the <i>value</i> which the presenter formats from numbers. The View
    /// therefore only assigns — it composes no sentence and computes no rule, exactly like
    /// <c>TitleView</c> (AGENTS rule 10).
    /// </para>
    /// <para>
    /// Writes go through the <see cref="Set{T}"/> helpers, which skip an unchanged value: R3's
    /// <c>ReactiveProperty</c> publishes on assignment, and a HUD that republished 60 times a
    /// second with the same HP would reformat the same text 60 times a second for nothing.
    /// </para>
    /// </remarks>
    public sealed class BattleHudState : IDisposable
    {
        // --- captions (localized) ---
        public ReactiveProperty<string> FloorName { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> HpCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> ManaCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> BiaCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> TimeCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> WaveCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> BossCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> EliteCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> KillsCaption { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The mini ย่าม's caption ("ย่าม"), written once per floor from the localization table.</summary>
        public ReactiveProperty<string> BagCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> StatusCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> TimelineCaption { get; } = new ReactiveProperty<string>(string.Empty);

        // --- values (formatted by the presenter) ---
        public ReactiveProperty<string> HpValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> ManaValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> BiaValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> TimeValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> WaveValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> KillsValue { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Which floor of the run this is, localized ("ชั้น 2"), written once per floor.</summary>
        public ReactiveProperty<string> FloorIndex { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// The line the status panel shows while a boss's possession has the controls turned
        /// around ("ถูกปอบสิง · ทิศควบคุมสลับ"), or empty when they are normal (Stage 11c).
        /// </summary>
        /// <remarks>
        /// A string rather than a flag plus a caption, because the panel has exactly one thing to say:
        /// the localized line while the frame's <c>ControlFlipTicks</c> is above zero, and nothing
        /// otherwise. The text is looked up once, when the floor begins, so a possession costs one
        /// equality check per frame and no formatting at all.
        /// </remarks>
        public ReactiveProperty<string> PossessionText { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> StatusLine { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> SpeedLabel { get; } = new ReactiveProperty<string>("1x");

        // --- bars and flags ---
        public ReactiveProperty<float> HealthPercent { get; } = new ReactiveProperty<float>(0f);

        public ReactiveProperty<float> ManaPercent { get; } = new ReactiveProperty<float>(0f);

        public ReactiveProperty<float> FloorProgress { get; } = new ReactiveProperty<float>(0f);

        public ReactiveProperty<float> BossHealthPercent { get; } = new ReactiveProperty<float>(0f);

        public ReactiveProperty<bool> HasBoss { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> Ended { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<float> Speed { get; } = new ReactiveProperty<float>(1f);

        /// <summary>
        /// Set when the battle raised its timer warning. It is <i>not</i> derived from the
        /// clock: the warning threshold is a floor rule and the frame deliberately does not
        /// restate it, so the HUD learns about it the way every other presentation reaction
        /// does — from the event stream.
        /// </summary>
        public ReactiveProperty<bool> TimeWarning { get; } = new ReactiveProperty<bool>(false);

        /// <summary>
        /// The floor's spawn markers (wave windows, elite and boss arrivals). Static for the
        /// floor and set once, so it is a plain list rather than a subscription: the timeline
        /// bar draws the same markers for sixty seconds.
        /// </summary>
        public IReadOnlyList<BattleTimelineMarker> Timeline { get; private set; } =
            Array.Empty<BattleTimelineMarker>();

        /// <summary>How many times the marker list has been replaced (a test reads this).</summary>
        public int TimelineRevision { get; private set; }

        /// <summary>Replaces the marker list (once per floor) and bumps <see cref="TimelineRevision"/>.</summary>
        public void SetTimeline(IReadOnlyList<BattleTimelineMarker> markers)
        {
            Timeline = markers ?? Array.Empty<BattleTimelineMarker>();
            TimelineRevision++;
        }

        /// <summary>Assigns an int without republishing an unchanged value.</summary>
        public static void Set(ReactiveProperty<int> property, int value)
        {
            if (property.Value != value)
            {
                property.Value = value;
            }
        }

        /// <summary>Assigns a float without republishing an (approximately) unchanged value.</summary>
        public static void Set(ReactiveProperty<float> property, float value)
        {
            if (!Mathf.Approximately(property.Value, value))
            {
                property.Value = value;
            }
        }

        /// <summary>Assigns a bool without republishing an unchanged value.</summary>
        public static void Set(ReactiveProperty<bool> property, bool value)
        {
            if (property.Value != value)
            {
                property.Value = value;
            }
        }

        /// <summary>Assigns a string without republishing an unchanged value.</summary>
        public static void Set(ReactiveProperty<string> property, string value)
        {
            string text = value ?? string.Empty;
            if (!string.Equals(property.Value, text, StringComparison.Ordinal))
            {
                property.Value = text;
            }
        }

        /// <summary>Disposes every subscription point. Called when the scene is left.</summary>
        public void Dispose()
        {
            FloorName.Dispose();
            HpCaption.Dispose();
            ManaCaption.Dispose();
            BiaCaption.Dispose();
            TimeCaption.Dispose();
            WaveCaption.Dispose();
            BossCaption.Dispose();
            EliteCaption.Dispose();
            KillsCaption.Dispose();
            BagCaption.Dispose();
            StatusCaption.Dispose();
            TimelineCaption.Dispose();
            HpValue.Dispose();
            ManaValue.Dispose();
            BiaValue.Dispose();
            TimeValue.Dispose();
            WaveValue.Dispose();
            KillsValue.Dispose();
            FloorIndex.Dispose();
            PossessionText.Dispose();
            StatusLine.Dispose();
            SpeedLabel.Dispose();
            HealthPercent.Dispose();
            ManaPercent.Dispose();
            FloorProgress.Dispose();
            BossHealthPercent.Dispose();
            HasBoss.Dispose();
            Ended.Dispose();
            Speed.Dispose();
            TimeWarning.Dispose();
        }
    }
}
