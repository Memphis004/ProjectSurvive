using System;
using System.IO;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Photographs a real floor's telegraphed warning at 1920×1080 (Stage 11b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why an Editor capture and not a browser shot.</b> The Stage 11b gate is a picture of
    /// the floor warning at 1920×1080, and the warning is drawn by the scene's own
    /// <see cref="BattleViewScene"/> — the shipped <c>TelegraphPool</c>, the shipped
    /// <c>BattleRenderBridge</c>, the shipped camera. Building that view here and stepping a
    /// real floor through it photographs the code that runs in the game; the alternative (a
    /// hand-placed decal) would prove only that a sprite can be drawn.
    /// </para>
    /// <para>
    /// <b>The floor is chosen for its ghosts.</b> <c>floor.forest_3</c> is the first floor whose
    /// waves include Krasue, whose attack is a warning line followed by a dash. The capture steps
    /// the floor until the view reports a live warning, lets the decal reach full strength, and
    /// renders the scene camera into a 1920×1080 target. Nothing about the fight is staged: the
    /// ghost, its position and its warning all come from the simulation.
    /// </para>
    /// <para>
    /// The run's HUD is a separate overlay canvas and is not part of the camera render, so the
    /// picture is the arena — floor, ghosts, player, warning — at the gate's size.
    /// </para>
    /// </remarks>
    public static class TelegraphCaptureTool
    {
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";
        private const string ArtifactDirectory = "artifacts/ui";
        private const string ArtifactName = "stage11b-telegraph-1920x1080";

        /// <summary>Floor whose waves include Krasue, the dash-and-drain ghost.</summary>
        private const string FloorId = "floor.forest_3";

        private const uint Seed = 20261020u;

        /// <summary>Ticks the capture may step before it gives up (40 s of floor).</summary>
        private const int TickCeiling = 1200;

        /// <summary>Renders after the first live warning, so the decal is at full strength.</summary>
        private const int WarmFrames = 8;

        private static readonly Vector2Int Size = new Vector2Int(1920, 1080);

        /// <summary>One rendered frame of a normal-speed floor; the tick the simulation runs at.</summary>
        private const float FrameSeconds = 1f / BattleConstants.TickRate;

        /// <summary>The menu entry: open Battle, play a floor, capture its warning.</summary>
        [MenuItem("ProjectSurvive/Stage 11b/Capture telegraph (1920x1080)", priority = 134)]
        public static void Capture()
        {
            if (!EnsureBattleScene(out string error))
            {
                Debug.LogError("[Stage 11b] " + error);
                return;
            }

            IGameConfig config = ResourcesGameConfigLoader.Load();
            var runner = UnityEngine.Object.FindFirstObjectByType<BattleSimulationRunner>(
                FindObjectsInactive.Include);
            if (runner == null)
            {
                Debug.LogError("[Stage 11b] the Battle scene has no BattleSimulationRunner");
                return;
            }

            BattleViewSettings settings = ReadSettings(runner);
            if (settings == null || settings.Camera == null || settings.ViewRoot == null)
            {
                Debug.LogError("[Stage 11b] the Battle scene's view settings are not wired");
                return;
            }

            int childrenBefore = settings.ViewRoot.childCount;
            BattleViewScene view = null;

            try
            {
                var hudState = new BattleHudState();
                var text = new LocalizationService(config);
                var speed = new BattleSpeed();
                var presenter = new BattleHudPresenter(hudState, text, config, speed);

                // The overlay HUD needs the scene's VContainer scope, which only exists in Play
                // Mode. The picture is the arena, so the capture drives the same view with the
                // overlay wiring left out; every world-space part — pool, bridge, camera,
                // telegraph decals — is the shipped one.
                view = new BattleViewScene(ArenaOnly(settings), hudState, presenter, speed, Seed);

                BattleSetup setup = BuildSetup(config);
                IBattleSession session = view.Begin(
                    BattleFactoryBinding.Create(config), setup, startingBia: 0);

                int tick = StepUntilWarning(view, session);
                if (tick < 0)
                {
                    Debug.LogError(
                        "[Stage 11b] no warning appeared within " + TickCeiling
                        + " ticks on " + FloorId);
                    return;
                }

                // The repository root is two levels above Assets/, and every other artifact this
                // project keeps lives there rather than inside UnityProject/.
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                string directory = Path.Combine(root, ArtifactDirectory);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, ArtifactName + ".png");

                Capture(view, settings.Camera, path);

                File.WriteAllText(
                    Path.Combine(directory, ArtifactName + ".json"),
                    "{\n"
                    + "  \"stage\": \"11b\",\n"
                    + "  \"floor\": \"" + FloorId + "\",\n"
                    + "  \"seed\": " + Seed + ",\n"
                    + "  \"tick\": " + tick + ",\n"
                    + "  \"warningsOnScreen\": " + view.ActiveTelegraphs + ",\n"
                    + "  \"size\": \"" + Size.x + "x" + Size.y + "\",\n"
                    + "  \"note\": \"the floor is played by the shipped BattleViewScene; the picture is the arena camera render without the HUD overlay\"\n"
                    + "}\n");

                Debug.Log(
                    "[Stage 11b] telegraph captured at tick " + tick + " with "
                    + view.ActiveTelegraphs + " warning(s) on screen -> " + path);
            }
            finally
            {
                view?.Dispose();
                DestroyCreatedChildren(settings.ViewRoot, childrenBefore);
            }
        }

        private static int StepUntilWarning(BattleViewScene view, IBattleSession session)
        {
            for (int i = 0; i < TickCeiling; i++)
            {
                if (session.Phase != BattlePhase.Running)
                {
                    return -1;
                }

                session.Step(PlayerInputFrame.Idle);
                view.AcceptTick(session.Frame);
                view.DrainEvents(session.Events);
                view.Render(1f, FrameSeconds);

                if (view.ActiveTelegraphs == 0)
                {
                    continue;
                }

                // Let the decal finish its fade-in before the shutter, so the shot shows the
                // warning as a player sees it rather than its first transparent frame.
                for (int frame = 0; frame < WarmFrames; frame++)
                {
                    view.Render(1f, FrameSeconds);
                }

                return (int)session.Tick;
            }

            return -1;
        }

        private static void Capture(BattleViewScene view, Camera camera, string path)
        {
            view.Render(1f, FrameSeconds);

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(Size.x, Size.y, 24, RenderTextureFormat.ARGB32);

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var texture = new Texture2D(Size.x, Size.y, TextureFormat.RGB24, mipChain: false);
                texture.ReadPixels(new Rect(0f, 0f, Size.x, Size.y), 0, 0);
                texture.Apply();

                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// A floor-3 departure with a real weapon: the player has to be able to fight back for
        /// the waves to reach the ghost that warns, and the HP pool keeps an idle body alive long
        /// enough to be photographed.
        /// </summary>
        private static BattleSetup BuildSetup(IGameConfig config)
        {
            // No weapon on purpose: a fire bolt kills a 26 HP Krasue long before it closes to
            // its 9-unit range, and a floor with nothing to shoot is a floor whose ghosts
            // actually reach the warning they are here to photograph. A healthy HP pool keeps
            // the idle body alive while they do.
            return new BattleSetup
            {
                Seed = Seed,
                StageId = FloorId,
                DifficultyId = "difficulty.normal",
                FloorIndex = 3,
                IsBossFloor = false,
                CurrentHp = 400,
                DevCheatsAllowed = true,
                Loadout = new CombatLoadoutSnapshot
                {
                    Stats = new PlayerStatBlock
                    {
                        MaxHp = 400,
                        MaxMp = config.Constant.MaxMp,
                        MoveSpeed = config.Constant.MoveSpeed,
                        PickupRadius = config.Constant.PickupRadius,
                        FocusPerSecond = config.Constant.FocusPerSecond,
                    },
                },
            };
        }

        /// <summary>The scene's world wiring, with the overlay HUD and mini bag left out.</summary>
        private static BattleViewSettings ArenaOnly(BattleViewSettings settings)
        {
            return new BattleViewSettings
            {
                ViewRoot = settings.ViewRoot,
                Camera = settings.Camera,
                Font = settings.Font,
                Projection = settings.Projection,
                Hud = null,
            };
        }

        /// <summary>
        /// The scene's own wiring. <see cref="BattleViewSettings"/> is a plain serializable
        /// class held inline by the runner, so it is read off the field rather than through a
        /// <see cref="SerializedProperty"/> (which only addresses object references).
        /// </summary>
        private static BattleViewSettings ReadSettings(BattleSimulationRunner runner)
        {
            System.Reflection.FieldInfo field = typeof(BattleSimulationRunner).GetField(
                "_viewSettings",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            return field?.GetValue(runner) as BattleViewSettings;
        }

        private static bool EnsureBattleScene(out string error)
        {
            error = string.Empty;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "leave Play Mode first; the capture opens the Battle scene in Edit Mode";
                return false;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                error = "the capture was cancelled at the save prompt";
                return false;
            }

            EditorSceneManager.OpenScene(BattleScenePath);
            return true;
        }

        private static void DestroyCreatedChildren(Transform root, int childrenBefore)
        {
            for (int i = root.childCount - 1; i >= childrenBefore; i--)
            {
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
            }
        }
    }
}
