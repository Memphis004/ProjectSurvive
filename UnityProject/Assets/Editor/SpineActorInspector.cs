using System;
using System.Collections.Generic;
using MyClone.SpineSystem;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyClone.EditorTools
{
	/// <summary>
	/// Inspector for SpineActorBase and every subclass.
	///
	/// Skin selection: when the SkeletonDataAsset holds two or more part groups
	/// (body_*/head_*/face_* — grouped by the text before the first '_'), the single
	/// Initial Skin field is replaced by one dropdown per part ("Body Skin", "Head Skin",
	/// ...) listing "(none)" and that group's skins. Values persist in the serialized
	/// skinParts list; the legacy Initial Skin string stays for single-group skeletons.
	/// Changing any skin field rebuilds automatically in edit mode and recomposes live
	/// in play mode. Stored names missing from the skeleton show "(missing)" plus a red
	/// help box.
	///
	/// Also: Rebuild button (edit + play mode), validation help boxes, a read-only debug
	/// footer (combined skin, attachment count, empty slots, meshes, materials, bounds)
	/// and Reset/Randomize buttons for quick part mixing.
	/// </summary>
	[CustomEditor (typeof (SpineActorBase), editorForChildClasses: true)]
	public class SpineActorInspector : UnityEditor.Editor
	{
		public override void OnInspectorGUI ()
		{
			bool changed = DrawConfigFields ();
			DrawValidation ();

			EditorGUILayout.Space ();
			if (GUILayout.Button ("Rebuild"))
				RebuildAll ();

			EditorGUILayout.Space ();
			foreach (var item in targets)
				DrawDebugInfo ((SpineActorBase)item);

			if (changed) {
				serializedObject.ApplyModifiedProperties ();
				RebuildAfterConfigChange ();
			}
		}

		/// <summary>
		/// Draws all serialized fields, replacing Initial Skin with per-part dropdowns when
		/// the skeleton has two or more part groups. Returns true when a skin field changed.
		/// </summary>
		bool DrawConfigFields ()
		{
			var names = GetSkeletonNames ();
			bool useParts = names != null && names.Groups != null && names.Groups.Count >= 2;
			bool changed = false;

			serializedObject.Update ();
			var iterator = serializedObject.GetIterator ();
			bool enterChildren = true;
			while (iterator.NextVisible (enterChildren)) {
				enterChildren = false;

				switch (iterator.name) {
				case "m_Script":
					using (new EditorGUI.DisabledScope (true))
						EditorGUILayout.PropertyField (iterator, true);
					break;
				case "skinParts":
					break; // drawn through the part dropdowns / hidden
				case "initialSkin":
					if (useParts) {
						changed |= DrawPartPopups (serializedObject.FindProperty ("skinParts"), names);
					} else {
						DrawNamePopup (iterator, "Initial Skin", names != null ? names.Skins : null);
					}
					break;
				default:
					EditorGUILayout.PropertyField (iterator, true);
					break;
				}
			}

			serializedObject.ApplyModifiedProperties ();
			return changed;
		}

		/// <summary>
		/// One dropdown per part group plus Reset/Randomize helpers. Reads and writes the
		/// serialized skinParts list ("part" -> "skin"); a legacy Initial Skin value seeds
		/// the matching group until the first change, then it is cleared.
		/// </summary>
		bool DrawPartPopups (SerializedProperty partsProperty, SkeletonNames names)
		{
			var parts = ReadParts (partsProperty);
			string legacy = serializedObject.FindProperty ("initialSkin") != null
				? serializedObject.FindProperty ("initialSkin").stringValue
				: null;
			bool seedFromLegacy = parts.Count == 0 && !string.IsNullOrEmpty (legacy);

			bool changed = false;
			foreach (var group in names.Groups) {
				string current = "";
				for (int i = 0; i < parts.Count; i++) {
					if (parts[i].part == group.Part) {
						current = parts[i].skin;
						break;
					}
				}
				if (current.Length == 0 && seedFromLegacy && Array.IndexOf (group.Skins, legacy) >= 0)
					current = legacy;

				// Options: (none), the group's skins, plus the stored value when missing.
				var values = new List<string> { "" };
				var displays = new List<string> { "(none)" };
				values.AddRange (group.Skins);
				displays.AddRange (group.Skins);
				bool missing = current.Length > 0 && Array.IndexOf (group.Skins, current) < 0;
				if (missing) {
					values.Add (current);
					displays.Add (current + " (missing)");
				}

				int selected = values.IndexOf (current);
				if (selected < 0)
					selected = 0;

				int next = EditorGUILayout.Popup (PartLabel (group.Part), selected, displays.ToArray ());
				if (next == selected)
					continue;

				changed = true;
				current = values[next];
				int partIndex = -1;
				for (int i = 0; i < parts.Count; i++) {
					if (parts[i].part == group.Part) {
						partIndex = i;
						break;
					}
				}
				var entry = new SpineActorBase.SkinPart { part = group.Part, skin = current };
				if (partIndex >= 0)
					parts[partIndex] = entry;
				else
					parts.Add (entry);
			}

			if (changed) {
				WriteParts (partsProperty, parts);
				var initialSkinProperty = serializedObject.FindProperty ("initialSkin");
				if (initialSkinProperty != null && !string.IsNullOrEmpty (initialSkinProperty.stringValue))
					initialSkinProperty.stringValue = ""; // parts own the look now
			}

			DrawPartButtons (partsProperty, names);
			return changed;
		}

		/// <summary>"Body Skin" style label: capitalized part key + " Skin".</summary>
		static string PartLabel (string part)
		{
			if (string.IsNullOrEmpty (part))
				return "Skin";
			return char.ToUpperInvariant (part[0]) + part.Substring (1) + " Skin";
		}

		void DrawPartButtons (SerializedProperty partsProperty, SkeletonNames names)
		{
			EditorGUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Reset Parts To First Available")) {
				var parts = new List<SpineActorBase.SkinPart> ();
				foreach (var group in names.Groups)
					parts.Add (new SpineActorBase.SkinPart { part = group.Part, skin = group.Skins[0] });
				WriteParts (partsProperty, parts);
				ClearLegacyInitialSkin ();
				serializedObject.ApplyModifiedProperties ();
				RebuildAfterConfigChange ();
			}
			if (GUILayout.Button ("Randomize Parts")) {
				var parts = new List<SpineActorBase.SkinPart> ();
				foreach (var group in names.Groups)
					parts.Add (new SpineActorBase.SkinPart {
						part = group.Part,
						skin = group.Skins[UnityEngine.Random.Range (0, group.Skins.Length)]
					});
				WriteParts (partsProperty, parts);
				ClearLegacyInitialSkin ();
				serializedObject.ApplyModifiedProperties ();
				RebuildAfterConfigChange ();
			}
			EditorGUILayout.EndHorizontal ();
		}

		void ClearLegacyInitialSkin ()
		{
			var initialSkinProperty = serializedObject.FindProperty ("initialSkin");
			if (initialSkinProperty != null && !string.IsNullOrEmpty (initialSkinProperty.stringValue)) {
				initialSkinProperty.stringValue = "";
			}
		}

		static List<SpineActorBase.SkinPart> ReadParts (SerializedProperty partsProperty)
		{
			var parts = new List<SpineActorBase.SkinPart> ();
			if (partsProperty == null)
				return parts;

			for (int i = 0; i < partsProperty.arraySize; i++) {
				var element = partsProperty.GetArrayElementAtIndex (i);
				var partProperty = element.FindPropertyRelative ("part");
				var skinProperty = element.FindPropertyRelative ("skin");
				parts.Add (new SpineActorBase.SkinPart {
					part = partProperty != null ? partProperty.stringValue : null,
					skin = skinProperty != null ? skinProperty.stringValue : null
				});
			}
			return parts;
		}

		static void WriteParts (SerializedProperty partsProperty, List<SpineActorBase.SkinPart> parts)
		{
			if (partsProperty == null) {
				Debug.LogError ("[SpineActorInspector] SpineActorBase no longer has a skinParts field; parts were not saved.");
				return;
			}

			partsProperty.arraySize = parts.Count;
			for (int i = 0; i < parts.Count; i++) {
				var element = partsProperty.GetArrayElementAtIndex (i);
				element.FindPropertyRelative ("part").stringValue = parts[i].part;
				element.FindPropertyRelative ("skin").stringValue = parts[i].skin;
			}
		}

		/// <summary>Popup over a string property with a "(empty)"/"(missing)" escape hatch.</summary>
		static void DrawNamePopup (SerializedProperty property, string label, string[] validNames)
		{
			if (validNames == null) {
				// No skeleton to feed the popup from: plain text field.
				EditorGUILayout.PropertyField (property, new GUIContent (label));
				return;
			}

			var values = new List<string> { "" };
			var displays = new List<string> { "(empty)" };
			values.AddRange (validNames);
			displays.AddRange (validNames);

			int selected = values.IndexOf (property.stringValue);
			if (selected < 0 && !string.IsNullOrEmpty (property.stringValue)) {
				values.Add (property.stringValue);
				displays.Add (property.stringValue + " (missing)");
				selected = values.Count - 1;
			}
			if (selected < 0)
				selected = 0;

			int next = EditorGUILayout.Popup (label, selected, displays.ToArray ());
			if (next != selected)
				property.stringValue = values[next];
		}

		class SkeletonGroup
		{
			public string Part;
			public string[] Skins;
		}

		class SkeletonNames
		{
			public string[] Skins;
			public string[] Animations;
			public List<SkeletonGroup> Groups;
		}

		SkeletonNames GetSkeletonNames ()
		{
			var actor = target as SpineActorBase;
			var data = actor != null ? actor.Data : null;
			if (data == null)
				return null;

			var skeletonData = data.GetSkeletonData (true);
			if (skeletonData == null)
				return null;

			var result = new SkeletonNames ();

			var skins = new List<string> ();
			var skinList = skeletonData.Skins;
			for (int i = 0; i < skinList.Count; i++) {
				var skin = skinList.Items[i];
				if (skin != null)
					skins.Add (skin.Name);
			}
			result.Skins = skins.ToArray ();

			var animations = new List<string> ();
			var animationList = skeletonData.Animations;
			for (int i = 0; i < animationList.Count; i++) {
				var animation = animationList.Items[i];
				if (animation != null)
					animations.Add (animation.Name);
			}
			result.Animations = animations.ToArray ();

			result.Groups = new List<SkeletonGroup> ();
			foreach (var group in SpineActorBase.DetectSkinGroups (skeletonData)) {
				var groupSkins = new List<string> ();
				for (int i = 0; i < group.skins.Count; i++)
					groupSkins.Add (group.skins[i].Name);
				result.Groups.Add (new SkeletonGroup { Part = group.part, Skins = groupSkins.ToArray () });
			}
			return result;
		}

		// ------------------------------------------------------------------
		// Rebuild
		// ------------------------------------------------------------------

		void RebuildAll ()
		{
			foreach (var item in targets) {
				var actor = (SpineActorBase)item;
				actor.Build ();
				MarkSceneDirty (actor);
			}
		}

		/// <summary>Applies a config change without a full rebuild dialog: rebuild in edit
		/// mode (and mark the scene), recompose live in play mode.</summary>
		void RebuildAfterConfigChange ()
		{
			foreach (var item in targets) {
				var actor = (SpineActorBase)item;
				if (Application.isPlaying) {
					actor.ReapplySkinParts ();
				} else {
					actor.Build ();
					MarkSceneDirty (actor);
				}
			}
		}

		static void MarkSceneDirty (Component component)
		{
			if (Application.isPlaying)
				return;

			var scene = component.gameObject.scene;
			if (scene.IsValid ())
				EditorSceneManager.MarkSceneDirty (scene);
		}

		// ------------------------------------------------------------------
		// Validation
		// ------------------------------------------------------------------

		void DrawValidation ()
		{
			foreach (var item in targets)
				DrawValidation ((SpineActorBase)item);
		}

		static void DrawValidation (SpineActorBase actor)
		{
			var data = actor.Data;
			if (data == null) {
				EditorGUILayout.HelpBox (
					"Skeleton Data Asset is not assigned: the actor cannot build.", MessageType.Error);
				return;
			}

			var skeletonData = data.GetSkeletonData (true);
			if (skeletonData == null) {
				EditorGUILayout.HelpBox (
					"Skeleton Data Asset '" + data.name + "' could not be loaded " +
					"(missing skeleton .bytes/atlas assets).", MessageType.Warning);
				return;
			}

			if (!string.IsNullOrEmpty (actor.InitialAnimation) &&
				skeletonData.FindAnimation (actor.InitialAnimation) == null)
				EditorGUILayout.HelpBox (
					"Initial Animation '" + actor.InitialAnimation + "' does not exist in '" +
					data.name + "'.", MessageType.Error);

			// Part skins: every stored name must exist; only the missing ones are reported.
			var parts = actor.GetSkinParts ();
			for (int i = 0; i < parts.Length; i++) {
				if (string.IsNullOrEmpty (parts[i].skin))
					continue;
				if (skeletonData.FindSkin (parts[i].skin) == null)
					EditorGUILayout.HelpBox (
						"Skin Part '" + parts[i].part + "': '" + parts[i].skin + "' does not exist in '" +
						data.name + "'.", MessageType.Error);
			}

			// Legacy Initial Skin is validated only while it is the active mode.
			if (parts.Length == 0 && !string.IsNullOrEmpty (actor.InitialSkin) &&
				skeletonData.FindSkin (actor.InitialSkin) == null)
				EditorGUILayout.HelpBox (
					"Initial Skin '" + actor.InitialSkin + "' does not exist in '" +
					data.name + "'.", MessageType.Error);

			DrawUiMaterialValidation (actor, data);
		}

		static void DrawUiMaterialValidation (SpineActorBase actor, SkeletonDataAsset data)
		{
			var materialProperty = new SerializedObject (actor).FindProperty ("normalMaterial");
			if (materialProperty == null)
				return; // SpineWorldActor has no UI material

			var material = materialProperty.objectReferenceValue as Material;
			if (material == null)
				return; // empty is fine: the actor builds one from the atlas shader

			if (MaterialMatchesAtlas (material, data))
				return;

			EditorGUILayout.HelpBox (
				"Normal Material's main texture is not one of the atlas pages of '" + data.name +
				"'. The actor will render with the wrong texture.", MessageType.Warning);
		}

		/// <summary>
		/// Compares the material's main texture against every page material of the
		/// skeleton's atlases. With no page textures at all the check is skipped
		/// instead of guessing.
		/// </summary>
		static bool MaterialMatchesAtlas (Material material, SkeletonDataAsset data)
		{
			var pageTextures = new HashSet<Texture> ();
			var atlases = data.atlasAssets;
			if (atlases != null) {
				foreach (var atlas in atlases) {
					if (atlas == null)
						continue;
					foreach (var pageMaterial in atlas.Materials) {
						if (pageMaterial != null && pageMaterial.mainTexture != null)
							pageTextures.Add (pageMaterial.mainTexture);
					}
				}
			}

			if (pageTextures.Count == 0)
				return true;

			return pageTextures.Contains (material.mainTexture);
		}

		// ------------------------------------------------------------------
		// Debug footer
		// ------------------------------------------------------------------

		static void DrawDebugInfo (SpineActorBase actor)
		{
			EditorGUILayout.LabelField ("Debug", EditorStyles.boldLabel);

			if (!actor.IsBuilt) {
				EditorGUILayout.HelpBox (
					"Not built. Press Rebuild or enter Play mode (Start builds automatically).",
					MessageType.Info);
				return;
			}

			var skin = actor.Skeleton.Skin != null ? actor.Skeleton.Skin : actor.Skeleton.Data.DefaultSkin;
			EditorGUILayout.LabelField ("Is Built", "true");
			EditorGUILayout.LabelField ("Current Skin",
				skin != null ? skin.Name + " (" + skin.Attachments.Count + " attachments)" : "(none)");

			// A composed skin is named "combined:a+b+c"; list its parts readably.
			if (skin != null && skin.Name.StartsWith ("combined:", StringComparison.Ordinal))
				EditorGUILayout.LabelField ("Combined From",
					skin.Name.Substring ("combined:".Length).Replace ("+", ", "));

			var emptySlots = new List<string> ();
			var slots = actor.Skeleton.Slots;
			for (int i = 0; i < slots.Count; i++) {
				var slot = slots.Items[i];
				if (slot.Attachment == null)
					emptySlots.Add (slot.Data.Name);
			}
			EditorGUILayout.LabelField ("Empty Slots",
				emptySlots.Count == 0 ? "(none)" : DescribeNames (emptySlots));

			var entry = actor.AnimationState.GetCurrent (0);
			EditorGUILayout.LabelField ("Current Animation",
				entry != null && entry.Animation != null ? entry.Animation.Name : "(none)");

			var uiActor = actor as SpineUiActor;
			if (uiActor != null)
				DrawUiDebugInfo (uiActor);
			else
				DrawWorldDebugInfo ((SpineWorldActor)actor);
		}

		static void DrawUiDebugInfo (SpineUiActor actor)
		{
			var renderers = actor.GetComponentsInChildren<CanvasRenderer> ();
			EditorGUILayout.LabelField ("Canvas Renderers (meshes)", renderers.Length.ToString ());

			var materials = new HashSet<Material> ();
			var textures = new HashSet<Texture> ();
			for (int i = 0; i < renderers.Length; i++) {
				var material = renderers[i].GetMaterial ();
				if (material == null)
					continue;
				materials.Add (material);
				if (material.mainTexture != null)
					textures.Add (material.mainTexture);
			}
			EditorGUILayout.LabelField ("Materials", JoinNames (materials));
			EditorGUILayout.LabelField ("Textures", JoinTextures (textures));

			var mesh = actor.Graphic.GetLastMesh ();
			EditorGUILayout.LabelField ("Vertices",
				mesh != null ? mesh.vertexCount.ToString () : "0");
			if (mesh != null && mesh.vertexCount > 0) {
				mesh.RecalculateBounds ();
				EditorGUILayout.LabelField ("Mesh Bounds (local)",
					"center " + mesh.bounds.center + ", size " + mesh.bounds.size);
			}

			var rectTransform = actor.Graphic.rectTransform;
			var rect = rectTransform.rect;
			EditorGUILayout.LabelField ("RectTransform On Canvas",
				rect.width + " x " + rect.height + " at world " + rectTransform.position);
		}

		static void DrawWorldDebugInfo (SpineWorldActor actor)
		{
			var animation = actor.Animation;
			if (animation == null)
				return;

			var meshFilter = animation.GetComponent<MeshFilter> ();
			var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
			EditorGUILayout.LabelField ("Sub Meshes",
				mesh != null ? mesh.subMeshCount.ToString () : "0");
			EditorGUILayout.LabelField ("Vertices",
				mesh != null ? mesh.vertexCount.ToString () : "0");

			var meshRenderer = animation.GetComponent<MeshRenderer> ();
			var materials = new HashSet<Material> ();
			var textures = new HashSet<Texture> ();
			if (meshRenderer != null) {
				foreach (var material in meshRenderer.sharedMaterials) {
					if (material == null)
						continue;
					materials.Add (material);
					if (material.mainTexture != null)
						textures.Add (material.mainTexture);
				}
			}
			EditorGUILayout.LabelField ("Materials", JoinNames (materials));
			EditorGUILayout.LabelField ("Textures", JoinTextures (textures));

			if (mesh != null && mesh.vertexCount > 0)
				EditorGUILayout.LabelField ("Mesh Bounds (local)",
					"center " + mesh.bounds.center + ", size " + mesh.bounds.size);
			if (meshRenderer != null)
				EditorGUILayout.LabelField ("World Bounds",
					"center " + meshRenderer.bounds.center + ", size " + meshRenderer.bounds.size);
		}

		static string JoinNames (HashSet<Material> materials)
		{
			if (materials.Count == 0)
				return "(none)";

			var names = new List<string> ();
			foreach (var material in materials)
				names.Add (material.name);
			return string.Join (", ", names.ToArray ());
		}

		static string JoinTextures (HashSet<Texture> textures)
		{
			if (textures.Count == 0)
				return "(none)";

			var names = new List<string> ();
			foreach (var texture in textures)
				names.Add (texture.name);
			return string.Join (", ", names.ToArray ());
		}

		/// <summary>Joins names, capping the list so the footer stays readable.</summary>
		static string DescribeNames (List<string> names)
		{
			const int cap = 12;
			var shown = names.Count > cap ? names.GetRange (0, cap) : names;
			string text = string.Join (", ", shown.ToArray ());
			return names.Count > cap ? text + ", ... (+" + (names.Count - cap) + " more)" : text;
		}
	}
}
