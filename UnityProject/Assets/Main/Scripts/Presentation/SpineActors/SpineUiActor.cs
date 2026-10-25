using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using AnimationState = Spine.AnimationState;

namespace ProjectSurvive.Game.Presentation.SpineActors
{
	/// <summary>
	/// SkeletonGraphic / CanvasRenderer implementation of a spine actor.
	/// </summary>
	/// <remarks>
	/// Materials are per-actor instances of the two serialized references this component
	/// carries, so a keyword or colour set here never mutates a shared asset. Grayscale
	/// swaps to the gray material ("Spine/SkeletonGraphic Grayscale", created by
	/// ProjectSurvive/Spine/3. Create UI materials). Nothing is looked up by shader name
	/// at runtime: a stripped player build returns null for those lookups.
	/// </remarks>
	public class SpineUiActor : SpineActorBase
	{
		[Header("UI Renderer")]
		[SerializeField] Material normalMaterial;
		[SerializeField] Material grayMaterial;
		[SerializeField] bool canvasGroupCompatible = true;
		[SerializeField] bool tintBlack;
		[SerializeField] bool alwaysMultipleCanvasRenderers;
		[SerializeField] bool useClipping;
		[SerializeField] bool raycastTarget;
		[SerializeField] string[] separatorSlotNames = new string[0];

		SpineSkeletonGraphic m_Graphic;
		Material m_NormalInstance;
		Material m_GrayInstance;

		public SpineSkeletonGraphic Graphic { get { return m_Graphic; } }
		public override Skeleton Skeleton { get { return m_Graphic == null ? null : m_Graphic.Skeleton; } }
		public override AnimationState AnimationState { get { return m_Graphic == null ? null : m_Graphic.AnimationState; } }
		public override Transform SkeletonTransform { get { return m_Graphic == null ? null : m_Graphic.transform; } }

		public bool CanvasGroupCompatible {
			get { return canvasGroupCompatible; }
			set { canvasGroupCompatible = value; }
		}

		public bool TintBlack {
			get { return tintBlack; }
			set { tintBlack = value; }
		}

		public string[] SeparatorSlotNames { get { return separatorSlotNames; } }

		// ------------------------------------------------------------------
		// Renderer
		// ------------------------------------------------------------------

		const string RendererGameObjectName = "SpineGraphic";

		protected override void CreateRenderer ()
		{
			var go = new GameObject (RendererGameObjectName, typeof(RectTransform));
			go.transform.SetParent (transform, false);

			var graphic = go.AddComponent<SpineSkeletonGraphic> ();
			if (graphic == null) {
				Debug.LogError ("[SpineUiActor] Could not add SpineSkeletonGraphic.", this);
				SafeDestroy (go);
				return;
			}

			var normal = ResolveNormalMaterial ();
			if (normal == null) {
				SafeDestroy (go);
				return;
			}

			graphic.raycastTarget = raycastTarget;
			graphic.material = normal;
			graphic.skeletonDataAsset = skeletonDataAsset;

			var settings = graphic.MeshGenerator.settings;
			settings.useClipping = useClipping;
			settings.tintBlack = tintBlack;
			settings.canvasGroupTintBlack = canvasGroupCompatible && tintBlack;
			// The CanvasGroup-compatible variant without tint black needs straight alpha
			// vertex colours; every other combination keeps premultiplied alpha.
			settings.pmaVertexColors = !(canvasGroupCompatible && !tintBlack);
			graphic.MeshGenerator.settings = settings;

			graphic.allowMultipleCanvasRenderers = ShouldUseMultipleCanvasRenderers ();
			graphic.enableSeparatorSlots = separatorSlotNames != null && separatorSlotNames.Length > 0;
			if (graphic.enableSeparatorSlots)
				graphic.SetSeparatorSlotNames (separatorSlotNames);

			graphic.Initialize (false);

			if (!graphic.IsValid) {
				Debug.LogError ("[SpineUiActor] SkeletonGraphic failed to initialize. Check SkeletonDataAsset/atlas.", this);
				SafeDestroy (go);
				return;
			}

			m_Graphic = graphic;

			if (tintBlack)
				EnableTintBlackCanvasChannels ();

			graphic.Update (0f);
			graphic.UpdateMesh ();
		}

		protected override void DestroyRenderer ()
		{
			var tracked = m_Graphic != null ? m_Graphic.gameObject : null;

			DestroyOrphanRenderers (tracked);

			if (tracked != null) {
				SafeDestroy (tracked);
				m_Graphic = null;
			}

			DestroyMaterialInstances ();
		}

		/// <summary>
		/// The renderer GameObject and m_Graphic survive duplication/scene reload, but the
		/// m_Graphic field does not serialize, so the next Build creates a second child.
		/// This removes any stale renderer child that the tracked reference no longer owns.
		/// </summary>
		void DestroyOrphanRenderers (GameObject keep)
		{
			for (int i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild (i).gameObject;
				if (child == keep)
					continue;

				if (child.name == RendererGameObjectName || child.GetComponent<SkeletonGraphic> () != null) {
					child.SetActive (false); // stop the stale renderer drawing while Destroy is pending
					SafeDestroy (child);
				}
			}
		}

		bool ShouldUseMultipleCanvasRenderers ()
		{
			if (alwaysMultipleCanvasRenderers)
				return true;
			if (separatorSlotNames != null && separatorSlotNames.Length > 0)
				return true;

			var atlases = skeletonDataAsset == null ? null : skeletonDataAsset.atlasAssets;
			if (atlases == null || atlases.Length == 0)
				return false;

			if (atlases.Length > 1)
				return true;

			return atlases[0] != null && atlases[0].MaterialCount > 1;
		}

		void EnableTintBlackCanvasChannels ()
		{
			var canvas = GetComponentInParent<Canvas> ();
			if (canvas == null)
				return;

			canvas.additionalShaderChannels |=
				AdditionalCanvasShaderChannels.TexCoord1 |
				AdditionalCanvasShaderChannels.TexCoord2;
		}

		// ------------------------------------------------------------------
		// Materials
		// ------------------------------------------------------------------

		/// <summary>
		/// Assigns the material this actor draws with (and the one it fades to for gray).
		/// Both are serialized references on purpose: <c>Shader.Find</c> returns null in a
		/// player build whose shader was stripped, so nothing here is looked up by name.
		/// Assigning while built rebuilds the renderer.
		/// </summary>
		public void SetMaterials (Material normal, Material gray = null)
		{
			normalMaterial = normal;
			if (gray != null)
				grayMaterial = gray;

			if (IsBuilt)
				Build ();
		}

		Material ResolveNormalMaterial ()
		{
			if (m_NormalInstance != null)
				return m_NormalInstance;

			if (normalMaterial == null) {
				Debug.LogError (
					"[SpineUiActor] No normal material assigned. Assign one with SetMaterials " +
					"(ProjectSurvive/Spine/3. Create UI materials writes the TintBlack and non-TintBlack pair). " +
					"There is no shader-name fallback: it would return null in a stripped player build.",
					this);
				return null;
			}

			m_NormalInstance = new Material (normalMaterial);
			m_NormalInstance.name = name + " (Normal)";
			ConfigureMaterial (m_NormalInstance);
			return m_NormalInstance;
		}

		Material ResolveGrayMaterial ()
		{
			if (m_GrayInstance != null)
				return m_GrayInstance;

			if (grayMaterial == null) {
				Debug.LogError (
					"[SpineUiActor] Grayscale requested but no gray material assigned. " +
					"Assign one with SetMaterials; the actor keeps drawing its normal material.",
					this);
				return ResolveNormalMaterial ();
			}

			m_GrayInstance = new Material (grayMaterial);
			m_GrayInstance.name = name + " (Gray)";
			ConfigureMaterial (m_GrayInstance);
			return m_GrayInstance;
		}

		void ConfigureMaterial (Material material)
		{
			if (material == null)
				return;

			if (canvasGroupCompatible)
				material.EnableKeyword ("_CANVAS_GROUP_COMPATIBLE");
			else
				material.DisableKeyword ("_CANVAS_GROUP_COMPATIBLE");
		}

		void DestroyMaterialInstances ()
		{
			if (m_NormalInstance != null) {
				SafeDestroy (m_NormalInstance);
				m_NormalInstance = null;
			}
			if (m_GrayInstance != null) {
				if (m_GrayInstance != m_NormalInstance)
					SafeDestroy (m_GrayInstance);
				m_GrayInstance = null;
			}
		}

		protected override void ApplyGray (bool gray)
		{
			if (m_Graphic == null)
				return;
			m_Graphic.material = gray ? ResolveGrayMaterial () : ResolveNormalMaterial ();
		}

		public override void SetColor (Color color)
		{
			if (m_Graphic != null)
				m_Graphic.color = color;
		}

		// ------------------------------------------------------------------
		// State
		// ------------------------------------------------------------------

		protected override void ApplyStateInternal ()
		{
			if (m_Graphic == null || !m_Graphic.IsValid)
				return;

			m_Graphic.Update (0f);
			m_Graphic.UpdateMesh ();
		}

		// ------------------------------------------------------------------
		// UI extras
		// ------------------------------------------------------------------

		/// <summary>
		/// Splits the mesh into separately sorted parts at the given slots.
		/// Requires multiple canvas renderers, which is enabled automatically.
		/// </summary>
		public void SetSeparatorSlots (params string[] slotNames)
		{
			separatorSlotNames = slotNames ?? new string[0];

			if (m_Graphic == null)
				return;

			m_Graphic.allowMultipleCanvasRenderers = true;
			m_Graphic.enableSeparatorSlots = separatorSlotNames.Length > 0;
			m_Graphic.SetSeparatorSlotNames (separatorSlotNames);
			m_Graphic.UpdateMesh ();
		}

		/// <summary>Resizes the RectTransform to the current mesh bounds.</summary>
		public bool MatchRectTransformToBounds ()
		{
			return m_Graphic != null && m_Graphic.UpdateRectTransformSize ();
		}

		// ------------------------------------------------------------------
		// Mount
		// ------------------------------------------------------------------

		protected override SpineActorBase CreateMountActor ()
		{
			var go = new GameObject ("SpineMount", typeof(RectTransform));
			go.transform.SetParent (transform, false);

			var mount = go.AddComponent<SpineUiActor> ();
			mount.normalMaterial = normalMaterial;
			mount.grayMaterial = grayMaterial;
			mount.canvasGroupCompatible = canvasGroupCompatible;
			mount.tintBlack = tintBlack;
			mount.alwaysMultipleCanvasRenderers = alwaysMultipleCanvasRenderers;
			mount.useClipping = useClipping;
			mount.raycastTarget = false;
			return mount;
		}

		protected override bool AttachMountToBone (string boneName)
		{
			if (m_Graphic == null || m_MountActor == null)
				return false;

			var follower = m_MountActor.GetComponent<BoneFollowerGraphic> ();
			if (follower == null)
				follower = m_MountActor.gameObject.AddComponent<BoneFollowerGraphic> ();

			follower.skeletonGraphic = m_Graphic;
			follower.followBoneRotation = false;
			follower.followSkeletonFlip = false;
			follower.followLocalScale = false;
			follower.followXYPosition = true;
			follower.followZPosition = false;
			return follower.SetBone (boneName);
		}
	}
}
