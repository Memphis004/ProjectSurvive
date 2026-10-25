using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using AnimationState = Spine.AnimationState;
using Object = UnityEngine.Object;

namespace MyClone.SpineSystem
{
	/// <summary>
	/// Rendering-agnostic half of a spine actor.
	/// Owns the skeleton data, the animation state machine, flip/gray/slot state and
	/// the mount lifecycle. Concrete subclasses only create/destroy the renderer.
	///
	/// Deliberately NOT copied from the game: the original UI subclass re-applied
	/// animations only to sub-skeletons, ignored the requested playback speed, and
	/// implemented neither DrawToBuffer nor SetShaderType. See
	/// notes/uispineactor/MYCLONE_DESIGN.md section 3.
	/// </summary>
	public abstract class SpineActorBase : MonoBehaviour
	{
		[Header("Spine")]
		[SerializeField] protected SkeletonDataAsset skeletonDataAsset;
		[SerializeField] protected string initialSkin;
		[SerializeField] protected string initialAnimation;
		[SerializeField] protected bool initialLoop = true;
		[SerializeField] protected bool initialFlipX;
		[SerializeField] protected bool initialFlipY;
		[SerializeField] protected bool initialGray;

		[Tooltip ("Character look composed from part skins (body/head/face). When not empty this overrides Initial Skin.")]
		[SerializeField] protected List<SkinPart> skinParts = new List<SkinPart> ();
		[Tooltip ("While composing skin parts, fill an empty slot automatically when the combined skin offers exactly one candidate attachment for it.")]
		[SerializeField] protected bool autoFillEmptySlots;

		protected SpineActorBase m_MountActor;

		bool m_Built;
		bool m_EmptySlotsReported;
		bool m_FlipX;
		bool m_FlipY;
		bool m_Gray;
		TrackEntry m_CurrentEntry;

		/// <summary>Raised once when a NON-looping track 0 animation reaches its end.</summary>
		public event Action<string> AnimationComplete;

		/// <summary>
		/// One part of a composed character look: "body" -> "body_1110901_1". Game skeletons
		/// store the look across several part skins (body_*, head_*, face_*), so a full
		/// character is a list of these, combined into one Spine.Skin at build time.
		/// </summary>
		[Serializable]
		public struct SkinPart
		{
			public string part;
			public string skin;
		}

		/// <summary>A group of variant skins sharing the same part key, in discovery order.</summary>
		public struct SkinGroupInfo
		{
			public string part;
			public List<Skin> skins;
		}

		const string CombinedSkinPrefix = "combined:";
		const string SkinNameSeparator = "+";

		public SkeletonDataAsset Data { get { return skeletonDataAsset; } }
		public string InitialSkin { get { return initialSkin; } }
		public string InitialAnimation { get { return initialAnimation; } }

		public bool IsBuilt { get { return m_Built && Skeleton != null && AnimationState != null; } }
		public bool IsGray { get { return m_Gray; } }
		public bool FlipX { get { return m_FlipX; } }
		public bool FlipY { get { return m_FlipY; } }
		public SpineActorBase MountActor { get { return m_MountActor; } }

		public abstract Skeleton Skeleton { get; }
		public abstract AnimationState AnimationState { get; }

		/// <summary>Transform that receives the flip scale (the spine component's transform).</summary>
		public abstract Transform SkeletonTransform { get; }

		protected virtual void Awake ()
		{
			m_FlipX = initialFlipX;
			m_FlipY = initialFlipY;
			m_Gray = initialGray;
		}

		/// <summary>
		/// Safety net for actors configured in the Inspector (or duplicated, where the
		/// renderer is not serialized): builds on the first frame when nothing built
		/// earlier. Factory-created actors are already built and skip this.
		/// </summary>
		protected virtual void Start ()
		{
			if (!IsBuilt && skeletonDataAsset != null)
				Build ();
		}

		/// <summary>Assigns data and defaults without building. Call <see cref="Build"/> afterwards.</summary>
		public void Configure (SkeletonDataAsset data, string skin = null, string animation = null, bool loop = true)
		{
			skeletonDataAsset = data;
			initialSkin = skin;
			initialAnimation = animation;
			initialLoop = loop;
		}

		/// <summary>Destroys the previous renderer (if any) and creates a new one from <see cref="Data"/>.</summary>
		public void Build ()
		{
			if (skeletonDataAsset == null) {
				Debug.LogError("[SpineActorBase] Build called without a SkeletonDataAsset.", this);
				return;
			}

			m_EmptySlotsReported = false;
			Release ();
			CreateRenderer ();

			if (Skeleton == null || AnimationState == null) {
				Debug.LogError("[SpineActorBase] Renderer could not be created (invalid SkeletonDataAsset/materials).", this);
				return;
			}

			m_Built = true;
			AnimationState.Complete += HandleComplete;

			ApplyInitialSkin ();

			ApplyFlip ();
			ApplyGray (m_Gray);

			if (!string.IsNullOrEmpty (initialAnimation))
				Play (initialAnimation, initialLoop, 1f);
			else
				ApplyStateInternal ();
		}

		// ------------------------------------------------------------------
		// Initial skin resolution
		// ------------------------------------------------------------------

		/// <summary>
		/// Applies the configured look at build time, in priority order: skin parts
		/// (body/head/face composition), then the legacy Initial Skin (kept for old
		/// scenes; used only when no parts are set), then an automatic pick so an
		/// unconfigured multi-part skeleton still shows a whole character.
		/// </summary>
		void ApplyInitialSkin ()
		{
			if (skinParts != null && skinParts.Count > 0) {
				ComposeSkinParts ();
				return;
			}

			if (!string.IsNullOrEmpty (initialSkin) && SetSkin (initialSkin))
				return;
			// A legacy Initial Skin that no longer resolves (or renders nothing) falls
			// through to the automatic pick below instead of leaving an invisible actor.

			if (EffectiveSkinCanRender ())
				return;

			var groups = DetectSkinGroups (Skeleton.Data);
			if (groups.Count >= 2) {
				var names = new List<string> ();
				for (int i = 0; i < groups.Count; i++) {
					string name = FirstSkinWithAttachments (groups[i].skins);
					if (name != null)
						names.Add (name);
				}
				if (names.Count > 0) {
					Debug.LogWarning ("[SpineActorBase] No skin configured. Automatically combining the first skin " +
						"of each part group: " + string.Join (SkinNameSeparator, names.ToArray ()) + ".", this);
					SetSkins (names.ToArray ());
					return;
				}
			}

			var fallback = FindFirstSkinWithAttachments ();
			if (fallback != null) {
				Debug.LogWarning ("[SpineActorBase] Default skin has no attachments. " +
					"Automatically using skin '" + fallback.Name + "'.", this);
				SetSkin (fallback.Name);
				return;
			}

			WarnSkinHasNoAttachments ();
		}

		/// <summary>
		/// Groups non-default skins by the text before the first underscore ("body_1110901_1"
		/// -> "body"), in skeleton discovery order. Skins in one group share slots (variants
		/// of the same part, pick at most one); different groups cover separate slots and
		/// compose into one character.
		/// </summary>
		public static List<SkinGroupInfo> DetectSkinGroups (SkeletonData data)
		{
			var groups = new List<SkinGroupInfo> ();
			if (data == null)
				return groups;

			for (int i = 0; i < data.Skins.Count; i++) {
				var skin = data.Skins.Items[i];
				if (skin == null || skin.Name == "default")
					continue;

				string part = SkinPartKey (skin.Name);
				int index = -1;
				for (int g = 0; g < groups.Count; g++) {
					if (groups[g].part == part) {
						index = g;
						break;
					}
				}

				if (index < 0) {
					groups.Add (new SkinGroupInfo { part = part, skins = new List<Skin> { skin } });
				} else {
					var skins = groups[index].skins;
					skins.Add (skin);
					groups[index] = new SkinGroupInfo { part = part, skins = skins };
				}
			}
			return groups;
		}

		/// <summary>Group key of a skin name: text before the first '_' (whole name if none).</summary>
		public static string SkinPartKey (string skinName)
		{
			if (string.IsNullOrEmpty (skinName))
				return skinName;
			int underscore = skinName.IndexOf ('_');
			return underscore > 0 ? skinName.Substring (0, underscore) : skinName;
		}

		static string FirstSkinWithAttachments (List<Skin> skins)
		{
			for (int i = 0; i < skins.Count; i++)
				if (skins[i] != null && skins[i].Attachments.Count > 0)
					return skins[i].Name;
			return null;
		}

		/// <summary>
		/// Heuristic for "can this skeleton show anything": the current skin plus the
		/// default skin (spine falls back to it during attachment lookup) hold at least
		/// one attachment entry between them.
		/// </summary>
		bool EffectiveSkinCanRender ()
		{
			var skin = Skeleton.Skin;
			var defaultSkin = Skeleton.Data.DefaultSkin;
			int entries = 0;
			if (skin != null && skin != defaultSkin)
				entries += skin.Attachments.Count;
			if (defaultSkin != null)
				entries += defaultSkin.Attachments.Count;
			return entries > 0;
		}

		Skin FindFirstSkinWithAttachments ()
		{
			var defaultSkin = Skeleton.Data.DefaultSkin;
			var skins = Skeleton.Data.Skins;
			for (int i = 0; i < skins.Count; i++) {
				var skin = skins.Items[i];
				if (skin == null || skin == defaultSkin || skin.Name == "default")
					continue;
				if (skin.Attachments.Count > 0)
					return skin;
			}
			return null;
		}

		void WarnSkinHasNoAttachments ()
		{
			var skin = Skeleton.Skin != null ? Skeleton.Skin : Skeleton.Data.DefaultSkin;
			Debug.LogWarning ("[SpineActorBase] Skin '" + (skin != null ? skin.Name : "(none)") +
				"' has no attachments: nothing will be visible. Pick another Initial Skin.", this);
		}

		/// <summary>Destroys the renderer but keeps this component (and the mount actor) alive.</summary>
		public void Release ()
		{
			if (m_Built && AnimationState != null)
				AnimationState.Complete -= HandleComplete;

			m_CurrentEntry = null;
			m_Built = false;
			DestroyRenderer ();
		}

		protected virtual void OnDestroy ()
		{
			Release ();
			DestroyMount ();
		}

		// ------------------------------------------------------------------
		// Renderer hooks
		// ------------------------------------------------------------------

		protected abstract void CreateRenderer ();
		protected abstract void DestroyRenderer ();
		protected abstract void ApplyGray (bool gray);
		protected abstract void ApplyStateInternal ();

		public abstract void SetColor (Color color);

		// ------------------------------------------------------------------
		// Animation
		// ------------------------------------------------------------------

		public bool HasAnimation (string animation)
		{
			if (!IsBuilt || string.IsNullOrEmpty (animation))
				return false;
			return Skeleton.Data.FindAnimation (animation) != null;
		}

		/// <summary>Plays track 0. Returns null if the animation does not exist.</summary>
		public TrackEntry Play (string animation, bool loop = true, float speed = 1f)
		{
			if (!IsBuilt) {
				Debug.LogWarning ("[SpineActorBase] Play before Build.", this);
				return null;
			}
			if (string.IsNullOrEmpty (animation) || Skeleton.Data.FindAnimation (animation) == null) {
				Debug.LogWarning ("[SpineActorBase] Animation not found: " + animation, this);
				return null;
			}

			var entry = AnimationState.SetAnimation (0, animation, loop);
			AnimationState.TimeScale = speed;
			m_CurrentEntry = entry;
			ApplyStateInternal ();
			return entry;
		}

		/// <summary>Freezes playback at the current time (timeScale 0).</summary>
		public void Stop ()
		{
			if (IsBuilt)
				AnimationState.TimeScale = 0f;
		}

		public void Pause ()
		{
			Stop ();
		}

		public void Resume (float speed = 1f)
		{
			SetSpeed (speed);
		}

		/// <summary>Global playback speed. Unlike the game's UI actor, the value is actually applied.</summary>
		public void SetSpeed (float speed)
		{
			if (IsBuilt)
				AnimationState.TimeScale = speed;
		}

		/// <summary>Jumps to a normalized position (0..1) inside the currently playing animation.</summary>
		public void SetProgress (float normalized)
		{
			if (!IsBuilt)
				return;

			var entry = AnimationState.GetCurrent (0);
			if (entry == null || entry.Animation == null)
				return;

			float duration = entry.AnimationEnd;
			if (duration <= 0f)
				return;

			float previousScale = AnimationState.TimeScale;
			string name = entry.Animation.Name;
			bool loop = entry.Loop;

			AnimationState.SetAnimation (0, name, loop);
			AnimationState.TimeScale = 1f;
			AnimationState.Update (Mathf.Clamp01 (normalized) * duration);
			AnimationState.TimeScale = previousScale;
			m_CurrentEntry = AnimationState.GetCurrent (0);
			ApplyStateInternal ();
		}

		/// <summary>Manual tick for tests, cutscenes and custom update loops.</summary>
		public void Tick (float deltaTime)
		{
			if (!IsBuilt)
				return;
			AnimationState.Update (deltaTime);
			ApplyStateInternal ();
		}

		void HandleComplete (TrackEntry entry)
		{
			if (entry == null || entry != m_CurrentEntry || entry.Loop)
				return;

			var handler = AnimationComplete;
			if (handler != null)
				handler (entry.Animation.Name);
		}

		// ------------------------------------------------------------------
		// Flip
		// ------------------------------------------------------------------

		public virtual void SetFlipX (bool flip)
		{
			m_FlipX = flip;
			ApplyFlip ();
			if (m_MountActor != null)
				m_MountActor.SetFlipX (flip);
		}

		public virtual void SetFlipY (bool flip)
		{
			m_FlipY = flip;
			ApplyFlip ();
			if (m_MountActor != null)
				m_MountActor.SetFlipY (flip);
		}

		protected void ApplyFlip ()
		{
			var target = SkeletonTransform;
			if (target == null)
				return;
			target.localScale = new Vector3 (m_FlipX ? -1f : 1f, m_FlipY ? -1f : 1f, 1f);
		}

		// ------------------------------------------------------------------
		// Slots / skins
		// ------------------------------------------------------------------

		/// <summary>
		/// Direct attachment swap by name (the game does the same, without validation).
		/// Returns false (and logs) when the slot or attachment does not exist.
		/// </summary>
		public bool SetSlotAttachment (string slotName, string attachmentName)
		{
			if (!IsBuilt) {
				Debug.LogWarning ("[SpineActorBase] SetSlotAttachment before Build.", this);
				return false;
			}

			var slot = Skeleton.FindSlot (slotName);
			if (slot == null) {
				Debug.LogWarning ("[SpineActorBase] Slot not found: " + slotName, this);
				return false;
			}

			if (attachmentName == null) {
				slot.Attachment = null;
			} else {
				var attachment = Skeleton.GetAttachment (slot.Data.Index, attachmentName);
				if (attachment == null) {
					Debug.LogWarning ("[SpineActorBase] Attachment not found: " + attachmentName + " on slot " + slotName, this);
					return false;
				}
				slot.Attachment = attachment;
			}

			ApplyStateInternal ();
			return true;
		}

		public bool ClearSlot (string slotName)
		{
			return SetSlotAttachment (slotName, null);
		}

		public string GetSlotAttachment (string slotName)
		{
			if (!IsBuilt)
				return null;

			var slot = Skeleton.FindSlot (slotName);
			return (slot == null || slot.Attachment == null) ? null : slot.Attachment.Name;
		}

		public bool SetSkin (string skinName)
		{
			if (!IsBuilt || string.IsNullOrEmpty (skinName))
				return false;

			if (Skeleton.Data.FindSkin (skinName) == null) {
				Debug.LogWarning ("[SpineActorBase] Skin not found: " + skinName, this);
				return false;
			}

			Skeleton.SetSkin (skinName);
			Skeleton.SetSlotsToSetupPose ();
			ApplyStateInternal ();

			if (!EffectiveSkinCanRender ())
				WarnSkinHasNoAttachments ();
			return true;
		}

		/// <summary>Current part list (copy; edit through <see cref="SetSkinPart"/>/<see cref="SetSkinParts"/>).</summary>
		public SkinPart[] GetSkinParts ()
		{
			return skinParts != null ? skinParts.ToArray () : new SkinPart[0];
		}

		/// <summary>
		/// Replaces one part ("head" -> "head_1110901_2") and recomposes the combined
		/// skin immediately when built. The other parts keep their skins.
		/// </summary>
		public void SetSkinPart (string part, string skin)
		{
			if (string.IsNullOrEmpty (part))
				return;

			if (skinParts == null)
				skinParts = new List<SkinPart> ();

			int index = -1;
			for (int i = 0; i < skinParts.Count; i++) {
				if (skinParts[i].part == part) {
					index = i;
					break;
				}
			}
			var entry = new SkinPart { part = part, skin = skin ?? "" };
			if (index >= 0)
				skinParts[index] = entry;
			else
				skinParts.Add (entry);

			if (IsBuilt)
				ComposeSkinParts ();
		}

		/// <summary>Replaces the whole part list and recomposes when built.</summary>
		public void SetSkinParts (params SkinPart[] parts)
		{
			if (skinParts == null)
				skinParts = new List<SkinPart> ();
			skinParts.Clear ();
			if (parts != null)
				skinParts.AddRange (parts);

			if (IsBuilt)
				ComposeSkinParts ();
		}

		/// <summary>Recomposes the combined skin from the current part list (no state change).</summary>
		public void ReapplySkinParts ()
		{
			if (IsBuilt)
				ComposeSkinParts ();
		}

		/// <summary>
		/// Combines several named skins into one and applies it. Skins are added in the
		/// given order; a later skin wins when two provide the same slot+attachment name.
		/// Missing names log a warning and are skipped. Does not touch the part list.
		/// Returns the combined skin, or null when nothing could be applied.
		/// </summary>
		public Skin SetSkins (params string[] skinNames)
		{
			if (!IsBuilt) {
				Debug.LogWarning ("[SpineActorBase] SetSkins before Build.", this);
				return null;
			}
			if (skinNames == null || skinNames.Length == 0)
				return null;

			var names = new List<string> ();
			var sources = new List<Skin> ();
			for (int i = 0; i < skinNames.Length; i++) {
				string name = skinNames[i];
				if (string.IsNullOrEmpty (name))
					continue;

				var source = Skeleton.Data.FindSkin (name);
				if (source == null) {
					Debug.LogWarning ("[SpineActorBase] skin '" + name + "' not found, skipping.", this);
					continue;
				}
				names.Add (name);
				sources.Add (source);
			}

			return ApplyCombined (names, sources);
		}

		// ------------------------------------------------------------------
		// Combined skin composition (SpineActorBase.SetSkins / skin parts)
		// ------------------------------------------------------------------

		/// <summary>Composes from the serialized part list; missing skins are warned and skipped.</summary>
		Skin ComposeSkinParts ()
		{
			var names = new List<string> ();
			var sources = new List<Skin> ();
			for (int i = 0; i < skinParts.Count; i++) {
				string name = skinParts[i].skin;
				if (string.IsNullOrEmpty (name))
					continue;

				var source = Skeleton.Data.FindSkin (name);
				if (source == null) {
					Debug.LogWarning ("[SpineActorBase] skin '" + name + "' (part '" + skinParts[i].part +
						"') not found, skipping.", this);
					continue;
				}
				names.Add (name);
				sources.Add (source);
			}
			return ApplyCombined (names, sources);
		}

		/// <summary>
		/// Builds one Spine.Skin named "combined:&lt;names&gt;", adding each source in order
		/// (later parts win on slot+attachment-name collisions, with a warning), applies it
		/// with a setup-pose reset, then reports (or auto-fills) slots left empty.
		/// </summary>
		Skin ApplyCombined (List<string> names, List<Skin> sources)
		{
			if (names.Count == 0) {
				Debug.LogWarning ("[SpineActorBase] No usable skins to combine.", this);
				return null;
			}

			var combined = new Skin (CombinedSkinPrefix + string.Join (SkinNameSeparator, names.ToArray ()));
			var owners = new Dictionary<string, string> ();
			for (int i = 0; i < sources.Count; i++) {
				foreach (var partEntry in sources[i].GetAttachments ()) {
					string key = partEntry.SlotIndex + "|" + partEntry.Name;
					string previous;
					if (owners.TryGetValue (key, out previous))
						Debug.LogWarning ("[SpineActorBase] combined skin: slot '" + SlotName (partEntry.SlotIndex) +
							"' attachment '" + partEntry.Name + "' already provided by '" + previous +
							"'; later skin '" + names[i] + "' wins", this);
					owners[key] = names[i];
				}
				combined.AddSkin (sources[i]);
			}

			// Reset first: Skeleton.SetSkin(Skin) merges with the currently active skin.
			Skeleton.SetSkin ((Skin)null);
			Skeleton.SetSkin (combined);
			Skeleton.SetSlotsToSetupPose ();
			ReportOrAutoFillEmptySlots (combined);
			ApplyStateInternal ();
			return combined;
		}

		string SlotName (int slotIndex)
		{
			var slots = Skeleton.Slots;
			return slotIndex >= 0 && slotIndex < slots.Count ? slots.Items[slotIndex].Data.Name : "?" + slotIndex;
		}

		/// <summary>
		/// After composing, slots with no attachment mean the combined skin does not carry
		/// the slot's setup attachment name. Report every such slot once per Build together
		/// with the attachment candidates the combined skin DOES offer for it (name mismatch,
		/// never auto-corrected) — unless Auto Fill Empty Slots is on and a slot has exactly
		/// one candidate.
		/// </summary>
		void ReportOrAutoFillEmptySlots (Skin combined)
		{
			var problems = new List<string> ();
			int autoFilled = 0;
			var slots = Skeleton.Slots;

			for (int i = 0; i < slots.Count; i++) {
				var slot = slots.Items[i];
				if (slot.Attachment != null)
					continue;

				var candidates = new List<string> ();
				foreach (var partEntry in combined.GetAttachments ())
					if (partEntry.SlotIndex == i && partEntry.Attachment != null)
						candidates.Add (partEntry.Name);

				if (autoFillEmptySlots && candidates.Count == 1) {
					slot.Attachment = combined.GetAttachment (i, candidates[0]);
					autoFilled++;
					continue;
				}

				problems.Add (candidates.Count > 0
					? slot.Data.Name + " (candidates: " + string.Join (", ", candidates.ToArray ()) +
						" - none matches setup attachment '" + slot.Data.AttachmentName + "')"
					: slot.Data.Name + " (no candidates in the combined skin)");
			}

			if (autoFilled > 0) {
				Debug.Log ("[SpineActorBase] Auto-filled " + autoFilled + " empty slot(s) from their single candidate.", this);
				ApplyStateInternal ();
			}

			if (problems.Count > 0 && !m_EmptySlotsReported) {
				m_EmptySlotsReported = true;
				Debug.LogWarning ("[SpineActorBase] Combined skin leaves " + problems.Count + " empty slot(s): " +
					string.Join ("; ", problems.ToArray ()), this);
			}
		}

		public void SetGray (bool gray)
		{
			m_Gray = gray;
			if (IsBuilt)
				ApplyGray (gray);
			if (m_MountActor != null)
				m_MountActor.SetGray (gray);
		}

		// ------------------------------------------------------------------
		// Mount
		// ------------------------------------------------------------------

		/// <summary>
		/// Mounts a second actor as a child. The instance is cached: unmounting hides it,
		/// matching the original behaviour (UnMount never destroys the mount actor).
		/// </summary>
		public SpineActorBase Mount (SkeletonDataAsset data, string skin = null)
		{
			if (data == null) {
				Debug.LogError ("[SpineActorBase] Mount called without data.", this);
				return null;
			}

			if (m_MountActor == null)
				m_MountActor = CreateMountActor ();

			if (m_MountActor == null)
				return null;

			m_MountActor.Configure (data, skin);
			m_MountActor.Build ();
			if (!m_MountActor.IsBuilt)
				return null;

			m_MountActor.gameObject.SetActive (true);
			m_MountActor.SetFlipX (m_FlipX);
			m_MountActor.SetFlipY (m_FlipY);
			m_MountActor.SetGray (m_Gray);
			return m_MountActor;
		}

		/// <summary>Hides the mount actor without destroying it.</summary>
		public void UnMount ()
		{
			if (m_MountActor != null)
				m_MountActor.gameObject.SetActive (false);
		}

		/// <summary>Destroys the cached mount actor.</summary>
		public void DestroyMount ()
		{
			if (m_MountActor == null)
				return;

			m_MountActor.Release ();
			SafeDestroy (m_MountActor.gameObject);
			m_MountActor = null;
		}

		/// <summary>Keeps the mount anchored to a bone of this skeleton.</summary>
		public bool MountToBone (string boneName)
		{
			if (m_MountActor == null || string.IsNullOrEmpty (boneName))
				return false;
			return AttachMountToBone (boneName);
		}

		protected abstract SpineActorBase CreateMountActor ();
		protected abstract bool AttachMountToBone (string boneName);

		// ------------------------------------------------------------------
		// Helpers
		// ------------------------------------------------------------------

		protected static void SafeDestroy (Object obj)
		{
			if (obj == null)
				return;

			if (Application.isPlaying)
				Object.Destroy (obj);
			else
				Object.DestroyImmediate (obj);
		}
	}
}
