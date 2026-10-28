using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Photographs floor 5 with Pob in it, once before its possession lands and once while the
    /// player's controls are reversed (Stage 11c).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the pictures have to prove.</b> The stage's acceptance asks for a shot of the boss
    /// floor and of the state where the movement input is flipped, because "the HUD tells the
    /// player" is a claim about a screen. Both come from one run of the real battle: the floor is
    /// the run's fifth, the boss is the shipped one, the reversal is the rule's, and the line under
    /// the bars is the presenter's own localized string read off the frame's <c>ControlFlipTicks</c>.
    /// </para>
    /// <para>
    /// <b>Why the pair, and why a pixel count.</b> A single shot of a reversed control proves
    /// nothing on its own — it looks like any other HUD. The two frames are consecutive, so the only
    /// difference between them is the possession; the tool then counts the pixels that changed
    /// inside the possession line's own rectangle and fails if that count is zero. A picture that
    /// silently lost its warning is exactly the defect an eye can miss in a small crop.
    /// </para>
    /// <para>
    /// The tool borrows the scene's overlay canvases for the shot (Screen Space - Overlay cannot be
    /// rendered into a target by a camera) and puts them back exactly as it found them. Nothing is
    /// saved into the scene.
    /// </para>
    /// </remarks>
    public static class BossCaptureTool
    {
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";
        private const string ArtifactDirectory = "artifacts/ui";
        private const string ArtifactName = "stage11c-boss-possession";

        /// <summary>How far a channel has to move for a pixel to count as changed (8-bit steps).</summary>
        private const int ChangedPixelThreshold = 8;

        private static readonly Vector2Int Size = new Vector2Int(1920, 1080);
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>The menu entry: play floor 5 and capture the boss before and during a possession.</summary>
        [MenuItem("ProjectSurvive/Stage 11c/Capture boss possession (1920x1080)", priority = 135)]
        public static void Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Stage 11c] leave Play Mode first; the capture opens the Battle scene in Edit Mode");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogError("[Stage 11c] the capture was cancelled at the save prompt");
                return;
            }

            EditorSceneManager.OpenScene(BattleScenePath);

            Camera camera = Camera.main;
            var hud = UnityEngine.Object.FindFirstObjectByType<BattleHudView>(FindObjectsInactive.Include);

            if (camera == null || hud == null)
            {
                Debug.LogError("[Stage 11c] the Battle scene is missing its camera or HUD");
                return;
            }

            BattleCaptureState state = null;
            Taken? before = null;
            Taken? flipped = null;

            try
            {
                if (!BattleCaptureState.TryBuildBoss(hud, out state, out string error))
                {
                    Debug.LogError("[Stage 11c] the boss floor could not be started: " + error);
                    return;
                }

                state.StepToBossPossession();

                if (state.BossPossessionTick < 0)
                {
                    Debug.LogError(
                        "[Stage 11c] the boss never possessed the player on floor 5; nothing was captured");
                    return;
                }

                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                string directory = Path.Combine(root, ArtifactDirectory);
                Directory.CreateDirectory(directory);

                before = Write(camera, state, BattleShot.BossBeforePossession, directory, "-before");
                flipped = Write(camera, state, BattleShot.BossPossessed, directory, "-flipped");

                int changed = CountChangedPixels(before.Value, flipped.Value, out int changedAnywhere);

                File.WriteAllText(
                    Path.Combine(directory, ArtifactName + ".json"),
                    "{\n"
                    + "  \"stage\": \"11c\",\n"
                    + "  \"floor\": \"" + state.StageId + "\",\n"
                    + "  \"tick\": " + state.BossPossessionTick + ",\n"
                    + "  \"boss\": \"" + state.BossHp + "/" + state.BossMaxHp + "\",\n"
                    + "  \"bossDistance\": " + state.BossDistance.ToString("0.00", CultureInfo.InvariantCulture) + ",\n"
                    + "  \"controlFlipTicks\": " + state.ControlFlipTicks + ",\n"
                    + "  \"godMode\": \"" + state.GodModeResult + "\",\n"
                    + "  \"possessionLineRect\": \"" + flipped.Value.LabelRect + "\",\n"
                    + "  \"possessionLineChangedPixels\": " + changed + ",\n"
                    + "  \"changedPixelsAnywhere\": " + changedAnywhere + ",\n"
                    + "  \"size\": \"" + Size.x + "x" + Size.y + "\",\n"
                    + "  \"before\": \"" + Path.GetFileName(before.Value.Path) + "\",\n"
                    + "  \"flipped\": \"" + Path.GetFileName(flipped.Value.Path) + "\",\n"
                    + "  \"note\": \"floor 5 of a real run, played by the shipped simulation; the two frames are consecutive and differ only by the possession. DevGodMode was on (the Editor is allowed it) so the platform could photograph the fight without racing the player's death.\"\n"
                    + "}\n");

                if (changed <= 0)
                {
                    Debug.LogError(
                        "[Stage 11c] the possession line drew nothing inside " + flipped.Value.LabelRect
                        + "; the HUD does not say the controls are reversed");
                    return;
                }

                Debug.Log("[Stage 11c] boss possession captured at tick " + state.BossPossessionTick
                    + " (" + state.BossHp + "/" + state.BossMaxHp + " HP, " + changed
                    + " pixel(s) changed in the possession line, "
                    + state.ControlFlipTicks + " flip tick(s) left) -> " + flipped.Value.Path);
            }
            finally
            {
                before?.Image.DestroyNow();
                flipped?.Image.DestroyNow();

                state?.Dispose();
            }
        }

        /// <summary>One rendered shot: where it was written, its pixels, and the line's rectangle.</summary>
        private readonly struct Taken
        {
            internal Taken(string path, Texture2D image, Rect labelRect)
            {
                Path = path;
                Image = image;
                LabelRect = labelRect;
            }

            internal string Path { get; }

            internal Texture2D Image { get; }

            internal Rect LabelRect { get; }
        }

        /// <summary>Renders one shot with the HUD on top of the arena and keeps its pixels.</summary>
        private static Taken Write(
            Camera camera, BattleCaptureState state, BattleShot shot, string directory, string suffix)
        {
            string fileName = ArtifactName + suffix + "-" + Size.x + "x" + Size.y + ".png";
            string path = Path.Combine(directory, fileName);

            var borrowed = new List<(Canvas Canvas, RenderMode Mode, Camera Camera, float Plane)>();
            RenderTexture target = null;
            Texture2D image = null;
            Rect labelRect = default;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                target = RenderTexture.GetTemporary(Size.x, Size.y, 24, RenderTextureFormat.ARGB32);

                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (canvas.rootCanvas != canvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        continue;
                    }

                    borrowed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 1f;
                }

                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();

                // The state goes on screen after the canvas has been laid out at this size, so the
                // picture is of the numbers it claims.
                state.Apply(shot);
                Canvas.ForceUpdateCanvases();

                labelRect = ScreenRectOf(state.PossessionLabel, camera);

                camera.Render();

                image = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, false);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, Size.x, Size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;

                for (int i = 0; i < borrowed.Count; i++)
                {
                    borrowed[i].Canvas.renderMode = borrowed[i].Mode;
                    borrowed[i].Canvas.worldCamera = borrowed[i].Camera;
                    borrowed[i].Canvas.planeDistance = borrowed[i].Plane;
                }

                Canvas.ForceUpdateCanvases();

                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
            }

            return new Taken(path, image, labelRect);
        }

        /// <summary>
        /// Pixels inside the possession line's rectangle that differ between two shots, and pixels
        /// that differ anywhere.
        /// </summary>
        /// <remarks>
        /// The second number is what makes the first mean something: the pair is two consecutive
        /// frames, so everything outside the line should be either identical or a HUD number that
        /// moved — and the line's own rectangle has to be where most of the change is.
        /// </remarks>
        private static int CountChangedPixels(in Taken before, in Taken after, out int changedAnywhere)
        {
            Rect rect = after.LabelRect;
            int minX = Mathf.Max(0, Mathf.FloorToInt(rect.xMin));
            int minY = Mathf.Max(0, Mathf.FloorToInt(rect.yMin));
            int maxX = Mathf.Min(Size.x, Mathf.CeilToInt(rect.xMax));
            int maxY = Mathf.Min(Size.y, Mathf.CeilToInt(rect.yMax));
            bool measurable = rect.width > 0f && rect.height > 0f;

            int changed = 0;
            changedAnywhere = 0;

            for (int y = 0; y < Size.y; y++)
            {
                for (int x = 0; x < Size.x; x++)
                {
                    Color32 a = before.Image.GetPixel(x, y);
                    Color32 b = after.Image.GetPixel(x, y);

                    if (Mathf.Abs(a.r - b.r) <= ChangedPixelThreshold
                        && Mathf.Abs(a.g - b.g) <= ChangedPixelThreshold
                        && Mathf.Abs(a.b - b.b) <= ChangedPixelThreshold)
                    {
                        continue;
                    }

                    changedAnywhere++;

                    if (measurable && x >= minX && x < maxX && y >= minY && y < maxY)
                    {
                        changed++;
                    }
                }
            }

            return changed;
        }

        /// <summary>A rect transform's rectangle in the pixels of the target being rendered.</summary>
        private static Rect ScreenRectOf(RectTransform rect, Camera camera)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return default;
            }

            rect.GetWorldCorners(Corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);

            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        /// <summary>Destroys a texture in the mode the Editor is in.</summary>
        private static void DestroyNow(this Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
