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
    /// The Result screen's layout, read from the scene the game ships and judged with the same
    /// <see cref="UiLayoutCheck"/> verdicts the จัดย่าม screen is judged with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 10b is the scene's own stage, and the GDD's UI acceptance is "ภาพหน้าจอไม่มี element
    /// ทับกัน" — no element on top of another. The camp has two checks for that (the PlayMode fixture
    /// and the Editor capture tool, which fail on overlapping panels); the summary screen is a plain
    /// child column with no runtime layout, so it is judged here instead: the same rule, over the
    /// rectangles the scene was actually authored with, in the Fast suite and without a WebGL build.
    /// </para>
    /// <para>
    /// The scene is opened <b>additively</b> and closed again, so the test never disturbs whatever
    /// scene the Editor has open and never has to ask a human about unsaved changes. Nothing here
    /// enters play mode: a <c>RectTransform</c>'s authored geometry is exactly what the screen is
    /// built from, and the proportions (which is all a CanvasScaler changes) are what the rules are
    /// about.
    /// </para>
    /// </remarks>
    public sealed class ResultScreenLayoutTests
    {
        private const string ResultScenePath = "Assets/Main/Scenes/Result.unity";

        /// <summary>The column every region of this screen is a child of.</summary>
        private const string SummaryName = "Summary";

        /// <summary>
        /// The smallest type size the GDD allows on this screen (Stage 09d: Thai needs 22 px at the
        /// 1920×1080 reference or its upper and lower marks stop being legible).
        /// </summary>
        private const float MinimumFontSize = 22f;

        /// <summary>
        /// The shortest line box a Thai line may be drawn in: a 32 px box holds the line and both of
        /// its marks, where a 22 px one clips them.
        /// </summary>
        private const float MinimumLineBox = 32f;

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
        /// Nothing on the summary screen is drawn on anything else: the headline, the four lines and
        /// the two buttons are pairwise disjoint, and every one of them is inside the column.
        /// </summary>
        [Test]
        public void No_two_regions_of_the_summary_screen_share_pixels()
        {
            ResultView view = OpenResultScene();
            RectTransform column = Column(view);

            List<(string Name, Rect Rect)> regions = Regions(view, column);
            Assert.That(regions.Count, Is.GreaterThanOrEqualTo(7), "the screen is missing one of its regions");

            var failures = new List<string>();
            Rect frame = column.rect;

            TestContext.WriteLine(
                "column " + frame + "; " + string.Join(
                    ", ", regions.ConvertAll(region => region.Name + " " + region.Rect)));

            for (int i = 0; i < regions.Count; i++)
            {
                // A rectangle with no area would satisfy every rule below by accident: two
                // zero-sized rects never overlap and always fit inside the column, so the check
                // would pass on a screen whose geometry had collapsed.
                Assert.That(
                    regions[i].Rect.width, Is.GreaterThan(0f),
                    regions[i].Name + " has no width: " + regions[i].Rect);
                Assert.That(
                    regions[i].Rect.height, Is.GreaterThan(0f),
                    regions[i].Name + " has no height: " + regions[i].Rect);

                if (!UiLayoutCheck.Contains(frame, regions[i].Rect))
                {
                    failures.Add(regions[i].Name + " leaves the summary column: " + regions[i].Rect);
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
                "the summary screen has " + failures.Count + " layout defect(s):\n" + string.Join("\n", failures));
        }

        /// <summary>
        /// Every string is drawn at or above the Thai floor, in a line box tall enough to hold its
        /// marks — the two numbers Stage 09d fixed for the whole game, checked on the screen Stage 10
        /// added.
        /// </summary>
        [Test]
        public void Every_label_is_drawn_at_the_thai_floor_or_above_it()
        {
            ResultView view = OpenResultScene();

            var serialized = new SerializedObject(view);
            string[] labels =
            {
                "_headlineLabel", "_floorLabel", "_killsLabel", "_biaLabel", "_hpLabel",
                "_restartLabel", "_titleLabel",
            };

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
        /// The screen keeps its order: the headline is the topmost thing, the four lines follow it in
        /// the order they are read, and the two buttons are below them all — so a line swapped with a
        /// button, or a heading moved under a button, is a failure rather than a coincidence of
        /// rectangles that happen to be disjoint.
        /// </summary>
        [Test]
        public void The_headline_the_lines_and_the_buttons_are_in_reading_order()
        {
            ResultView view = OpenResultScene();
            RectTransform column = Column(view);
            List<(string Name, Rect Rect)> regions = Regions(view, column);

            float Top(string name) => regions.Find(region => region.Name == name).Rect.yMax;

            Assert.That(Top("Headline"), Is.GreaterThan(Top("FloorLine")), "the headline is not above the floor line");
            Assert.That(Top("FloorLine"), Is.GreaterThan(Top("KillsLine")), "the kills line is not below the floor line");
            Assert.That(Top("KillsLine"), Is.GreaterThan(Top("BiaLine")), "the bia line is not below the kills line");
            Assert.That(Top("BiaLine"), Is.GreaterThan(Top("HpLine")), "the HP line is not below the bia line");
            Assert.That(Top("HpLine"), Is.GreaterThan(Top("RestartButton")), "the restart button is not below the lines");
            Assert.That(Top("RestartButton"), Is.GreaterThan(Top("TitleButton")), "the title button is not the last control");
        }

        // ------------------------------------------------------------------
        // Reading the shipped scene
        // ------------------------------------------------------------------

        /// <summary>The screen's seven pressable/readable regions, as rectangles in the column's space.</summary>
        private static List<(string Name, Rect Rect)> Regions(ResultView view, RectTransform column)
        {
            var serialized = new SerializedObject(view);

            var regions = new List<(string, Rect)>();
            foreach ((string Field, string Name) measured in new[]
            {
                (Field: "_headlineLabel", Name: "Headline"),
                (Field: "_floorLabel", Name: "FloorLine"),
                (Field: "_killsLabel", Name: "KillsLine"),
                (Field: "_biaLabel", Name: "BiaLine"),
                (Field: "_hpLabel", Name: "HpLine"),
                (Field: "_restartButton", Name: "RestartButton"),
                (Field: "_titleButton", Name: "TitleButton"),
            })
            {
                var component = serialized.FindProperty(measured.Field).objectReferenceValue as Component;
                Assert.That(component, Is.Not.Null, measured.Field + " is not wired");
                regions.Add((measured.Name, Rect(column, (RectTransform)component.transform)));
            }

            return regions;
        }

        /// <summary>
        /// One rectangle, in the units of <paramref name="frame"/>: the corners are taken in world
        /// space and read back through the column's transform, so the answer does not depend on the
        /// canvas' pixel size — the only thing a CanvasScaler changes.
        /// </summary>
        private static Rect Rect(RectTransform frame, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector3 a = frame.InverseTransformPoint(corners[0]);
            Vector3 b = frame.InverseTransformPoint(corners[2]);

            return new Rect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        /// <summary>The column the summary screen's regions are children of.</summary>
        private static RectTransform Column(ResultView view)
        {
            for (int i = 0; i < view.transform.childCount; i++)
            {
                var child = view.transform.GetChild(i) as RectTransform;
                if (child is not null && child.name == SummaryName)
                {
                    return child;
                }
            }

            Assert.Fail("the Result scene has no '" + SummaryName + "' column");
            return null;
        }

        /// <summary>
        /// The Result scene, loaded additively on top of whatever is open (and closed in
        /// <see cref="TearDown"/>), with its view found.
        /// </summary>
        private ResultView OpenResultScene()
        {
            Scene loaded = FindLoadedResultScene();
            if (!loaded.IsValid())
            {
                loaded = EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Additive);
            }

            _scene = loaded;
            Assert.That(_scene.IsValid() && _scene.isLoaded, "the Result scene could not be opened");

            ResultView view = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<ResultView>(includeInactive: true);
                if (view is not null)
                {
                    break;
                }
            }

            Assert.That(view, Is.Not.Null, "the Result scene has no ResultView");
            return view;
        }

        private static Scene FindLoadedResultScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == ResultScenePath)
                {
                    return scene;
                }
            }

            return default;
        }
    }
}
