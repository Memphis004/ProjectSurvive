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

        [Tooltip("Font used by the world-space damage numbers. Must be the baked Thai font.")]
        public TMP_FontAsset Font;

        [Tooltip("Orthographic or perspective; decided by measurement in Stage 08 (ADR 0018).")]
        public BattleProjection Projection = BattleProjection.Orthographic;

        [Tooltip("The card forest, its day/night lighting profile and the blob shadows (Stage 08e). Null in a fixture that has no arena.")]
        public Environment.ArenaEnvironmentView Environment;

        [Tooltip("The ghost-doctor's figure (Stage 17b; whichever backend Stage 17f selected). When it is assigned and builds, it draws the player instead of a pooled sprite; null in a fixture that has no figure.")]
        public MonoBehaviour PlayerFigure;
    }
}
