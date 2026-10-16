using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// The title screen's layout, read from the scene the game ships and judged with the same
    /// <see cref="UiLayoutCheck"/> verdicts the จัดย่าม and summary screens are judged with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 10d put a control on the first screen of the game, and the screen's own acceptance is
    /// about where it is: \"ตัวหนังสือ ≥ 22 px ที่ 1080p และไม่ทับข้อความ build/config\". The Editor
    /// capture tool measures the same three regions in pixels at both GDD sizes; this test holds the
    /// rule in the Fast suite and without a screenshot, over the rectangles the scene was actually
    /// authored with.
    /// </para>
    /// <para>
    /// The scene is opened <b>additively</b> and closed again, so the test never disturbs whatever
    /// scene the Editor has open and never has to ask a human about unsaved changes. Nothing here
    /// enters play mode: a <c>RectTransform</c>'s authored geometry is exactly what the screen is built
    /// from, and the three regions are anchored to the canvas' centre, so the layout in canvas units
    /// does not depend on the size the Game view happens to have.
    /// </para>
    /// </remarks>
    public sealed class TitleScreenLayoutTests
    {
        private const string TitleScenePath = "Assets/Main/Scenes/Title.unity";

        /// <summary>
        /// The canvas every region of this screen is laid out in, at the reference resolution every
        /// canvas in this project is scaled from (1920×1080, <c>ProjectSetup.CreateCanvas</c>).
        /// </summary>
        private static readonly Rect CanvasFrame = new Rect(-960f, -540f, 1920f, 1080f);

        /// <summary>
        /// The smallest type size the GDD allows on this screen (Stage 09d: Thai needs 22 px at the
        /// 1920×1080 reference or its upper and lower marks stop being legible).
        /// </summary>
        private const float MinimumFontSize = 22f;

        /// <summary>The shortest line box a Thai line may be drawn in: 22 px clips the marks.</summary>
        private const float MinimumLineBox = 32f;

        /// <summary>
        /// The smallest a control may be and still be a control a player can hit
        /// (<c>ProjectSetup.CreateUiButton</c> authors 400×76 for both the camp's and the summary
        /// screen's buttons; 44 px is the conventional touch-target floor and is what is asserted here
        /// so that a control that shrank is caught without pinning one screen's exact size).
        /// </summary>
        private const float MinimumControlHeight = 44f;

        private Scene _scene;

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, removeScene: true);
            }
        }

        /// <summary>
        /// The heading, the build/config line and the start control share no pixels, and all three are
        /// on screen.
        /// </summary>
        [Test]
        public void No_two_regions_of_the_title_screen_share_pixels()
        {
            TitleView view = OpenTitleScene();
            List<(string Name, Rect Rect)> regions = Regions(view);

            Assert.That(regions.Count, Is.EqualTo(3), "the screen is missing one of its regions");

            TestContext.WriteLine(
                "canvas " + CanvasFrame + "; " + string.Join(
                    ", ", regions.ConvertAll(region => region.Name + " " + region.Rect)));

            var failures = new List<string>();

            for (int i = 0; i < regions.Count; i++)
            {
                // A rectangle with no area would satisfy every rule below by accident.
                Assert.That(
                    regions[i].Rect.width, Is.GreaterThan(0f),
                    regions[i].Name + " has no width: " + regions[i].Rect);
                Assert.That(
                    regions[i].Rect.height, Is.GreaterThan(0f),
                    regions[i].Name + " has no height: " + regions[i].Rect);

                if (!UiLayoutCheck.Contains(CanvasFrame, regions[i].Rect))
                {
                    failures.Add(regions[i].Name + " leaves the canvas: " + regions[i].Rect);
                }

                for (int j = i + 1; j < regions.Count; j++)
                {
                    if (!UiLayoutCheck.IsAllowed(
                            new UiRegion(regions[i].Name, regions[i].Rect, UiRegionKind.Control),
                            new UiRegion(regions[j].Name, regions[j].Rect, UiRegionKind.Control)))
                    {
                        failures.Add(
                            regions[i].Name + " and " + regions[j].Name + " overlap: "
                            + regions[i].Rect + " / " + regions[j].Rect);
                    }
                }
            }

            Assert.That(
                failures, Is.Empty,
                "the title screen has " + failures.Count + " layout defect(s):\n" + string.Join("\n", failures));
        }

        /// <summary>
        /// Every string is drawn at or above the Thai floor in a line box tall enough to hold its marks —
        /// the two numbers Stage 09d fixed for the whole game, checked on the screen Stage 10d added a
        /// control to.
        /// </summary>
        [Test]
        public void Every_label_is_drawn_at_the_thai_floor_or_above_it()
        {
            TitleView view = OpenTitleScene();

            var serialized = new SerializedObject(view);
            string[] labels = { "_titleLabel", "_diagnosticsLabel", "_startLabel" };

            var failures = new List<string>();
            foreach (string field in labels)
            {
                var label = serialized.FindProperty(field).objectReferenceValue as TMP_Text;
                Assert.That(label, Is.Not.Null, field + " is not wired to a label");

                if (label.fontSize < MinimumFontSize)
                {
                    failures.Add(field + " is " + label.fontSize + " px");
                }

                var rect = (RectTransform)label.transform;
                if (rect.rect.height < MinimumLineBox)
                {
                    failures.Add(field + " sits in a " + rect.rect.height + " px line box");
                }
            }

            Assert.That(failures, Is.Empty, "below the floor: " + string.Join(", ", failures));
        }

        /// <summary>
        /// The start control is a control: it has a pointer target, it is enabled, and it is big enough
        /// to press — a button the player cannot hit is not \"กดได้ด้วยเมาส์\" whatever its caption says.
        /// </summary>
        [Test]
        public void The_start_control_can_be_pressed_with_a_pointer()
        {
            TitleView view = OpenTitleScene();
            var button = new SerializedObject(view).FindProperty("_startButton").objectReferenceValue as Button;

            Assert.That(button, Is.Not.Null, "the Title scene has no start button");
            Assert.That(button.interactable, Is.True, "the start control is not interactable");
            Assert.That(button.targetGraphic, Is.Not.Null, "the start control has no graphic to aim at");
            Assert.That(button.targetGraphic.raycastTarget, Is.True, "the start control's graphic ignores the pointer");

            var rect = (RectTransform)button.transform;
            Assert.That(
                rect.rect.height, Is.GreaterThanOrEqualTo(MinimumControlHeight),
                "the start control is " + rect.rect.height + " px tall, below the " + MinimumControlHeight
                + " px control floor");

            TestContext.WriteLine("start control " + rect.rect);
        }

        /// <summary>
        /// The screen keeps its order: the game's name, then the build/config line, then the control —
        /// so a control moved onto the build line, or a heading moved under the button, is a failure
        /// rather than a coincidence of rectangles that happen to be disjoint.
        /// </summary>
        [Test]
        public void The_heading_the_build_line_and_the_control_are_in_reading_order()
        {
            TitleView view = OpenTitleScene();
            List<(string Name, Rect Rect)> regions = Regions(view);

            float Top(string name) => regions.Find(region => region.Name == name).Rect.yMax;

            Assert.That(Top("TitleLabel"), Is.GreaterThan(Top("DiagnosticsLabel")), "the heading is not above the build line");
            Assert.That(Top("DiagnosticsLabel"), Is.GreaterThan(Top("StartButton")), "the start control is not below the build line");
        }

        // ------------------------------------------------------------------
        // Reading the shipped scene
        // ------------------------------------------------------------------

        /// <summary>The screen's three regions, as rectangles in the canvas' own units.</summary>
        private static List<(string Name, Rect Rect)> Regions(TitleView view)
        {
            var serialized = new SerializedObject(view);
            var frame = (RectTransform)view.transform;

            var regions = new List<(string, Rect)>();
            foreach ((string Field, string Name) measured in new[]
            {
                (Field: "_titleLabel", Name: "TitleLabel"),
                (Field: "_diagnosticsLabel", Name: "DiagnosticsLabel"),
                (Field: "_startButton", Name: "StartButton"),
            })
            {
                var component = serialized.FindProperty(measured.Field).objectReferenceValue as Component;
                Assert.That(component, Is.Not.Null, measured.Field + " is not wired");
                regions.Add((measured.Name, RectIn(frame, (RectTransform)component.transform)));
            }

            return regions;
        }

        /// <summary>
        /// One rectangle, in the units of <paramref name="frame"/>: the corners are taken in world space
        /// and read back through the canvas' transform, so the answer does not depend on the canvas'
        /// pixel size — the only thing a CanvasScaler changes.
        /// </summary>
        private static Rect RectIn(RectTransform frame, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector3 a = frame.InverseTransformPoint(corners[0]);
            Vector3 b = frame.InverseTransformPoint(corners[2]);

            return new Rect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        /// <summary>
        /// The Title scene, loaded additively on top of whatever is open (and closed in
        /// <see cref="TearDown"/>), with its view found.
        /// </summary>
        private TitleView OpenTitleScene()
        {
            Scene loaded = FindLoadedTitleScene();
            if (!loaded.IsValid())
            {
                loaded = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);
            }

            _scene = loaded;
            Assert.That(_scene.IsValid() && _scene.isLoaded, "the Title scene could not be opened");

            TitleView view = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<TitleView>(includeInactive: true);
                if (view is not null)
                {
                    break;
                }
            }

            Assert.That(view, Is.Not.Null, "the Title scene has no TitleView");
            return view;
        }

        private static Scene FindLoadedTitleScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == TitleScenePath)
                {
                    return scene;
                }
            }

            return default;
        }
    }
}
