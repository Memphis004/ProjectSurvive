using System;
using TMPro;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// What the Battle scene's driver needs from the scene: the objects the view parents
    /// itself under, the camera it drives, the HUD it fills, and the font and projection
    /// chosen in the inspector.
    /// </summary>
    /// <remarks>
    /// A serializable settings object rather than a dozen <c>[SerializeField]</c> fields on
    /// the driver, because the same wiring is read by the runner and by tests, and because a
    /// missing reference is easier to see as one object in the inspector. Every reference is
    /// authored by <c>ProjectSetup</c> through the Editor API (AGENTS rule 11) — the scene is
    /// never hand-written YAML.
    /// </remarks>
    [Serializable]
    public sealed class BattleViewSettings
    {
        [Tooltip("Parent of every pooled view and of the generated damage numbers.")]
        public Transform ViewRoot;

        [Tooltip("The 2.5D camera this scene drives.")]
        public Camera Camera;

        [Tooltip("The HUD the frame is mirrored into.")]
        public Hud.BattleHudView Hud;

        [Tooltip("The mini ย่าม at the top left: the bag's own box, one box per piece, and the cooldown plates (Stage 08c).")]
        public Hud.BattleMiniBagView MiniBag;

        [Tooltip("Font used by the world-space damage numbers. Must be the baked Thai font.")]
        public TMP_FontAsset Font;

        [Tooltip("Orthographic or perspective; decided by measurement in Stage 08 (ADR 0018).")]
        public BattleProjection Projection = BattleProjection.Orthographic;
    }
}
