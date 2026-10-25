using Spine.Unity;

namespace ProjectSurvive.Game.Presentation.SpineActors
{
	/// <summary>
	/// Minimal patch over stock spine-unity 3.8 <see cref="SkeletonGraphic"/>.
	///
	/// Stock 3.8 keeps <c>separatorSlotNames</c> protected and only reads it inside
	/// <c>Initialize()</c>, so a separator slot cannot be set after the graphic exists
	/// without a subclass. This class adds exactly that and a rect-size helper, and
	/// nothing else.
	/// </summary>
	public class SpineSkeletonGraphic : SkeletonGraphic
	{
		/// <summary>Sets separator slot names and re-applies them if the skeleton is already valid.</summary>
		public void SetSeparatorSlotNames (params string[] slotNames)
		{
			separatorSlotNames = slotNames ?? new string[0];

			if (IsValid)
				ReapplySeparatorSlotNames ();
		}

		public string[] GetSeparatorSlotNames ()
		{
			return separatorSlotNames;
		}

		/// <summary>Resizes the RectTransform to the current mesh bounds.</summary>
		public bool UpdateRectTransformSize ()
		{
			return MatchRectTransformWithBounds ();
		}
	}
}
