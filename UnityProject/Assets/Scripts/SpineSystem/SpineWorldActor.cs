using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using AnimationState = Spine.AnimationState;

namespace MyClone.SpineSystem
{
	/// <summary>
	/// MeshRenderer / SkeletonAnimation implementation of a spine actor.
	/// Lives in world space (no Canvas), tinted via skeleton color and faded via
	/// per-renderer material overrides.
	/// </summary>
	public class SpineWorldActor : SpineActorBase
	{
		[Header("World Renderer")]
		[SerializeField] string sortingLayerName = "Default";
		[SerializeField] int sortingOrder;
		[SerializeField] bool createSortingGroup = true;

		SkeletonAnimation m_Animation;
		readonly List<Material> m_GrayMaterials = new List<Material> ();

		public SkeletonAnimation Animation { get { return m_Animation; } }
		public override Skeleton Skeleton { get { return m_Animation == null ? null : m_Animation.Skeleton; } }
		public override AnimationState AnimationState { get { return m_Animation == null ? null : m_Animation.AnimationState; } }
		public override Transform SkeletonTransform { get { return m_Animation == null ? null : m_Animation.transform; } }

		public int SortingOrder {
			get { return sortingOrder; }
			set {
				sortingOrder = value;
				ApplySorting ();
			}
		}

		public string SortingLayerName {
			get { return sortingLayerName; }
			set {
				sortingLayerName = value;
				ApplySorting ();
			}
		}

		// ------------------------------------------------------------------
		// Renderer
		// ------------------------------------------------------------------

		protected override void CreateRenderer ()
		{
			var animation = SkeletonAnimation.NewSkeletonAnimationGameObject (skeletonDataAsset);
			if (animation == null) {
				Debug.LogError ("[SpineWorldActor] Could not create SkeletonAnimation.", this);
				return;
			}

			animation.transform.SetParent (transform, false);
			animation.Initialize (false);

			if (!animation.valid) {
				Debug.LogError ("[SpineWorldActor] SkeletonAnimation failed to initialize. Check SkeletonDataAsset/atlas.", this);
				SafeDestroy (animation.gameObject);
				return;
			}

			m_Animation = animation;
			ApplySorting ();
			animation.Update (0f);
			animation.LateUpdate ();
		}

		protected override void DestroyRenderer ()
		{
			var tracked = m_Animation != null ? m_Animation.gameObject : null;

			DestroyOrphanRenderers (tracked);

			if (tracked != null) {
				SafeDestroy (tracked);
				m_Animation = null;
			}

			DestroyGrayMaterials ();
		}

		/// <summary>
		/// m_Animation does not serialize, so a duplicate or scene reload leaves the old
		/// SkeletonAnimation child behind and the next Build stacks a second one. Remove
		/// any stale renderer child the tracked reference no longer owns. Mount roots
		/// ("SpineMount") carry no SkeletonRenderer, so they are never touched here.
		/// </summary>
		void DestroyOrphanRenderers (GameObject keep)
		{
			for (int i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild (i).gameObject;
				if (child == keep)
					continue;

				if (child.GetComponent<SkeletonRenderer> () != null) {
					child.SetActive (false); // stop the stale renderer drawing while Destroy is pending
					SafeDestroy (child);
				}
			}
		}

		void ApplySorting ()
		{
			if (m_Animation == null || !createSortingGroup)
				return;

			var group = m_Animation.GetComponent<SortingGroup> ();
			if (group == null)
				group = m_Animation.gameObject.AddComponent<SortingGroup> ();

			group.sortingLayerName = sortingLayerName;
			group.sortingOrder = sortingOrder;
		}

		// ------------------------------------------------------------------
		// Gray / color
		// ------------------------------------------------------------------

		protected override void ApplyGray (bool gray)
		{
			if (m_Animation == null)
				return;

			var overrides = m_Animation.CustomMaterialOverride;
			overrides.Clear ();
			DestroyGrayMaterials ();

			if (!gray) {
				m_Animation.LateUpdate ();
				return;
			}

			var grayShader = Shader.Find ("Spine/Special/Skeleton Grayscale");
			if (grayShader == null) {
				Debug.LogError ("[SpineWorldActor] Shader 'Spine/Special/Skeleton Grayscale' not found.", this);
				return;
			}

			var atlases = skeletonDataAsset == null ? null : skeletonDataAsset.atlasAssets;
			if (atlases == null)
				return;

			for (int i = 0; i < atlases.Length; i++) {
				var atlas = atlases[i];
				if (atlas == null)
					continue;

				foreach (var source in atlas.Materials) {
					if (source == null)
						continue;

					var grayMaterial = new Material (source);
					grayMaterial.name = source.name + " (Gray)";
					grayMaterial.shader = grayShader;
					overrides[source] = grayMaterial;
					m_GrayMaterials.Add (grayMaterial);
				}
			}

			m_Animation.LateUpdate ();
		}

		void DestroyGrayMaterials ()
		{
			for (int i = 0; i < m_GrayMaterials.Count; i++) {
				if (m_GrayMaterials[i] != null)
					SafeDestroy (m_GrayMaterials[i]);
			}
			m_GrayMaterials.Clear ();
		}

		public override void SetColor (Color color)
		{
			if (m_Animation == null)
				return;

			Skeleton.SetColor (color);
			ApplyStateInternal ();
		}

		// ------------------------------------------------------------------
		// State
		// ------------------------------------------------------------------

		protected override void ApplyStateInternal ()
		{
			if (m_Animation == null || !m_Animation.valid)
				return;

			m_Animation.Update (0f);
			m_Animation.LateUpdate ();
		}

		// ------------------------------------------------------------------
		// Render state helpers (world-only, UI has no equivalent in the original)
		// ------------------------------------------------------------------

		public void SetRenderQueue (int renderQueue)
		{
			if (m_Animation == null)
				return;

			var renderers = m_Animation.GetComponentsInChildren<MeshRenderer> ();
			for (int i = 0; i < renderers.Length; i++) {
				var materials = renderers[i].materials;
				for (int j = 0; j < materials.Length; j++) {
					if (materials[j] != null)
						materials[j].renderQueue = renderQueue;
				}
			}
		}

		public void SetZTest (CompareFunction compare)
		{
			if (m_Animation == null)
				return;

			var renderers = m_Animation.GetComponentsInChildren<MeshRenderer> ();
			for (int i = 0; i < renderers.Length; i++) {
				var materials = renderers[i].materials;
				for (int j = 0; j < materials.Length; j++) {
					if (materials[j] != null && materials[j].HasProperty ("_ZTest"))
						materials[j].SetInt ("_ZTest", (int)compare);
				}
			}
		}

		// ------------------------------------------------------------------
		// Mount
		// ------------------------------------------------------------------

		protected override SpineActorBase CreateMountActor ()
		{
			var go = new GameObject ("SpineMount");
			go.transform.SetParent (transform, false);

			var mount = go.AddComponent<SpineWorldActor> ();
			mount.sortingLayerName = sortingLayerName;
			mount.sortingOrder = sortingOrder;
			mount.createSortingGroup = createSortingGroup;
			return mount;
		}

		protected override bool AttachMountToBone (string boneName)
		{
			if (m_Animation == null || m_MountActor == null)
				return false;

			var follower = m_MountActor.GetComponent<BoneFollower> ();
			if (follower == null)
				follower = m_MountActor.gameObject.AddComponent<BoneFollower> ();

			follower.skeletonRenderer = m_Animation;
			follower.followBoneRotation = false;
			follower.followSkeletonFlip = false;
			follower.followLocalScale = false;
			follower.followXYPosition = true;
			follower.followZPosition = false;
			return follower.SetBone (boneName);
		}
	}
}
