using Spine.Unity;
using UnityEngine;

namespace MyClone.SpineSystem
{
	/// <summary>
	/// Convenience creators. No asset loading here: callers own the SkeletonDataAsset
	/// (Resources, Addressables, or a test factory).
	/// </summary>
	public static class SpineActorFactory
	{
		public static SpineUiActor CreateUi (
			Transform parent,
			SkeletonDataAsset data,
			string skin = null,
			string animation = null,
			bool loop = true,
			string actorName = "SpineUiActor",
			bool build = true)
		{
			var go = new GameObject (actorName, typeof(RectTransform));
			if (parent != null)
				go.transform.SetParent (parent, false);

			var actor = go.AddComponent<SpineUiActor> ();
			actor.Configure (data, skin, animation, loop);
			if (build)
				actor.Build ();
			return actor;
		}

		public static SpineWorldActor CreateWorld (
			Transform parent,
			SkeletonDataAsset data,
			string skin = null,
			string animation = null,
			bool loop = true,
			string actorName = "SpineWorldActor",
			bool build = true)
		{
			var go = new GameObject (actorName);
			if (parent != null)
				go.transform.SetParent (parent, false);

			var actor = go.AddComponent<SpineWorldActor> ();
			actor.Configure (data, skin, animation, loop);
			if (build)
				actor.Build ();
			return actor;
		}
	}
}
