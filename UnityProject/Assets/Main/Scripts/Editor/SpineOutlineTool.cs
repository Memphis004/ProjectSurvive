using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Puts a dark outline around the ghost-doctor, so the figure reads against the bright haze band
    /// the arena's middle is lit with (Stage 17b, follow-up).
    /// </summary>
    /// <remarks>        /// <para>
        /// The shader is the project's own, not spine's <c>Spine/Outline/Skeleton</c>: that one draws the
        /// outline and the body as two passes of one SubShader, and URP renders exactly one untagged pass
        /// per material — the figure came out as thin dark lines with no body at all (measured, and the
        /// reason <c>SpinePlayerOutline.shader</c> exists). The project's shader is one pass whose body
        /// half is Spine/Skeleton's Normal pass verbatim, so the figure's colour is unchanged, and whose
        /// ink is one screen pixel wide at any camera size.
        /// </para>
    /// <para>
    /// This is the one place <c>Shader.Find</c> is allowed — it runs in the Editor and writes an
    /// asset, and the actor carries a serialized material, never a shader name (ADR 0037 §3).
    /// </para>
    /// <para>
    /// The material is re-applied every run rather than skipped when it exists: the values here are
    /// the decision, and a hand-edited material would otherwise drift from it silently.
    /// </para>
    /// </remarks>
    public static class SpineOutlineTool
    {
        /// <summary>The player's outlined world material (one atlas page, so one material).</summary>
        public const string OutlineMaterialPath = SpineMaterialTool.MaterialFolder + "/SpinePlayer-World-Outline.mat";

        private const string OutlineShader = "ProjectSurvive/SpinePlayerOutline";

        /// <summary>
        /// Outline width in <b>screen</b> pixels: the shader takes its step from the UV derivatives,
        /// so the ink is one pixel wide whether the figure is drawn at 45 px in the arena or 300 px
        /// in a menu. 1.2 is a touch over one pixel — enough to separate the white robe from the
        /// haze, small enough not to close the gaps between an arm and the body.
        /// </summary>
        private const float OutlineWidth = 1.2f;

        /// <summary>
        /// How solid a texel has to be for the outline to treat it as part of the silhouette. Below
        /// this the atlas' own soft edges (a sleeve's fade, a hair tip) bleed into the ink.
        /// </summary>
        private const float EdgeCutoff = 0.25f;

        /// <summary>
        /// Ink, not black: a near-black with a touch of warmth, so the outline belongs to the lacquer
        /// and charcoal palette instead of reading as a printer's hairline.
        /// </summary>
        private static readonly Color OutlineColor = new Color(0.05f, 0.04f, 0.035f, 1f);

        [MenuItem("ProjectSurvive/Spine/4. Create player outline material", priority = 173)]
        public static void CreateOutlineMaterial()
        {
            var page = AssetDatabase.LoadAssetAtPath<Material>(SpineProfileTool.WorldMaterialPath);
            if (page == null)
            {
                Debug.LogError(
                    "[SpineOutlineTool] " + SpineProfileTool.WorldMaterialPath + " is missing: run " +
                    "ProjectSurvive/Spine/2. Create player profile first, so the outline can copy the " +
                    "atlas page's texture.");
                return;
            }

            var texture = page.HasProperty("_MainTex") ? page.GetTexture("_MainTex") : null;
            if (texture == null)
            {
                Debug.LogError(
                    "[SpineOutlineTool] " + SpineProfileTool.WorldMaterialPath + " carries no _MainTex, so " +
                    "the outline pass would sample nothing. Nothing was written.");
                return;
            }

            Shader shader = Shader.Find(OutlineShader);
            if (shader == null)
            {
                Debug.LogError(
                    "[SpineOutlineTool] shader '" + OutlineShader + "' is not in the project; check " +
                    "Assets/Main/Spine/Shaders/SpinePlayerOutline.shader.");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material == null)
            {
                EnsureFolder(SpineMaterialTool.MaterialFolder);
                material = new Material(shader) { name = "SpinePlayer-World-Outline" };
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetTexture("_MainTex", texture);
            material.SetColor("_OutlineColor", OutlineColor);
            material.SetFloat("_OutlineWidth", OutlineWidth);
            material.SetFloat("_Cutoff", EdgeCutoff);

            EditorUtility.SetDirty(material);

            // The outline is a world material, so it belongs to the profile the arena draws with.
            var profile = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(SpineProfileTool.BattleProfileAssetPath);
            if (profile == null)
            {
                Debug.LogError(
                    "[SpineOutlineTool] " + SpineProfileTool.BattleProfileAssetPath + " is missing, so the outline " +
                    "material was written but nothing points at it. Run ProjectSurvive/Spine/2. Create " +
                    "player profiles.");
            }
            else
            {
                profile.WorldMaterial = material;
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[SpineOutlineTool] " + OutlineMaterialPath + " <- '" + OutlineShader + "' over '" +
                texture.name + "', width " + OutlineWidth + " screen px, cutoff " + EdgeCutoff +
                ", colour " + ColorUtility.ToHtmlStringRGB(OutlineColor) +
                (profile == null ? "; the profile was not updated." : "; " + SpineProfileTool.BattleProfileAssetPath + " points at it."));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string current = "Assets";
            string[] segments = path.Split('/');
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
