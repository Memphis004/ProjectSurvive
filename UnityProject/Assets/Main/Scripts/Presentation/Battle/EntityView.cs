using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// One pooled visual: a GameObject with a sprite renderer, reused for every entity
    /// that needs one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A view never creates or destroys anything after construction, and it is shown and
    /// hidden by toggling <see cref="SpriteRenderer.enabled"/> rather than the GameObject's
    /// active state. <c>SetActive</c> on a thousand objects a frame walks the transform
    /// hierarchy; flipping a renderer flag does not, and this scene has nothing else on a
    /// view that would need <c>OnEnable</c>/<c>OnDisable</c>.
    /// </para>
    /// <para>
    /// The renderer is only written when what it shows actually changed
    /// (<see cref="ApplyLook"/>), so a mostly-static field of ghosts costs a transform
    /// write per frame instead of a material property update per frame.
    /// </para>
    /// </remarks>
    public sealed class EntityView
    {
        private Sprite _sprite;
        private Color _color = Color.white;
        private bool _facingLeft;
        private bool _visible;

        internal EntityView(GameObject gameObject)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            Renderer = gameObject.GetComponent<SpriteRenderer>();
            Renderer.sprite = null;
            Renderer.enabled = false;
        }

        /// <summary>The pooled object.</summary>
        public GameObject GameObject { get; }

        /// <summary>Its transform.</summary>
        public Transform Transform { get; }

        /// <summary>Its sprite renderer.</summary>
        public SpriteRenderer Renderer { get; }

        /// <summary>The entity this view currently stands for.</summary>
        public EntityRef Entity { get; internal set; }

        /// <summary>The kind of thing it currently stands for.</summary>
        public RenderEntityKind Kind { get; internal set; }

        /// <summary>True while the view is handed out by the pool.</summary>
        public bool InUse { get; internal set; }

        /// <summary>Sets what the view shows, skipping the write when nothing changed.</summary>
        internal void ApplyLook(Sprite sprite, Color color, bool facingLeft)
        {
            bool changed = !ReferenceEquals(_sprite, sprite)
                || _color != color
                || _facingLeft != facingLeft
                || !_visible;

            if (!changed)
            {
                return;
            }

            _sprite = sprite;
            _color = color;
            _facingLeft = facingLeft;
            _visible = true;

            Renderer.enabled = true;
            Renderer.sprite = sprite;
            Renderer.color = color;
        }

        /// <summary>Places the view. The mirror is applied as a negative X scale.</summary>
        internal void Place(Vector3 position, Quaternion rotation, float scale)
        {
            Transform.SetPositionAndRotation(position, rotation);
            Transform.localScale = new Vector3(_facingLeft ? -scale : scale, scale, 1f);
        }

        /// <summary>Hides the view without touching the GameObject's active state.</summary>
        /// <remarks>
        /// The renderer is compared with <c>!= null</c> rather than <c>is not null</c> because a
        /// destroyed Unity object is not a null reference — it is a live C# object whose native
        /// half is gone, and only Unity's own operator reports that. This path runs while a scene
        /// is being torn down, where Unity destroys the scene's objects in no promised order, so
        /// by the time the view is released its renderer may already be gone. Leaving the Battle
        /// scene used to log <c>MissingReferenceException</c> for exactly that reason.
        /// </remarks>
        internal void Hide()
        {
            if (!_visible)
            {
                return;
            }

            _visible = false;

            if (Renderer != null)
            {
                Renderer.enabled = false;
            }

            Entity = EntityRef.None;
        }
    }
}
