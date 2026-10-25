using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Creates the UI materials a <see cref="Presentation.SpineActors.SpineUiActor"/> draws with,
    /// under <c>Assets/Main/Spine/Materials</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one place <c>Shader.Find</c> is allowed: it runs in the Editor, where every shader
    /// in the project is loaded, and its result is written into an asset. The actor then carries a
    /// serialized material, never a shader name — a name lookup returns null in a player build whose
    /// shader was stripped, which is the Editor-green/player-broken class of bug this stage exists
    /// to avoid.
    /// </para>
    /// <para>
    /// Nothing is overwritten: a material with the target name is left exactly as it is, and the log
    /// says so.
    /// </para>
    /// </remarks>
    public static class SpineMaterialTool
    {
        public const string MaterialFolder = "Assets/Main/Spine/Materials";

        private const string NormalShader = "Spine/SkeletonGraphic";
        private const string TintBlackShader = "Spine/SkeletonGraphic Tint Black";
        private const string GrayscaleShader = "Spine/SkeletonGraphic Grayscale";

        [MenuItem("ProjectSurvive/Spine/3. Create UI materials", priority = 172)]
        public static void CreateUiMaterials()
        {
            EnsureFolder();

            int written = 0;
            written += Create("SpineGraphic-Normal", NormalShader);
            written += Create("SpineGraphic-TintBlack", TintBlackShader);
            written += Create("SpineGraphic-Grayscale", GrayscaleShader);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SpineMaterialTool] " + written + " material(s) written under " + MaterialFolder + ".");
        }

        /// <summary>Creates one material; returns 1 when it was written, 0 when it was skipped or the shader is missing.</summary>
        private static int Create(string name, string shaderName)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                Debug.Log("[SpineMaterialTool] " + path + " already exists; it was not overwritten.");
                return 0;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[SpineMaterialTool] shader '" + shaderName + "' is not in the project, so " + name +
                    " was not created. Check Assets/Spine/Runtime/spine-unity/Shaders.");
                return 0;
            }

            var material = new Material(shader);
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            Debug.Log("[SpineMaterialTool] " + path + " <- shader '" + shaderName + "'");
            return 1;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(MaterialFolder))
            {
                return;
            }

            var segments = new List<string>(MaterialFolder.Split('/'));
            string current = segments[0];
            for (int i = 1; i < segments.Count; i++)
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
