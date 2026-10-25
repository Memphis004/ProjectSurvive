using Spine.Unity;

namespace MyClone.SpineSystem
{
	/// <summary>
	/// Minimal patch over stock spine-unity 3.8 <see cref="SkeletonGraphic"/>.
	///
	/// The game ships a forked SkeletonGraphic with a public
	/// SetSerparatorSlotNames(...) and a rect-size helper. Stock 3.8 keeps
	/// separatorSlotNames protected and only reads it inside Initialize(), so runtime
	/// separator setup is impossible without a subclass. This class adds exactly that
	/// and nothing else.
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

		/// <summary>Equivalent of the game's UpdateRectTransformSize().</summary>
		public bool UpdateRectTransformSize ()
		{
			return MatchRectTransformWithBounds ();
		}
	}
}
