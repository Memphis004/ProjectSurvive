using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// The two quads the arena is made of. One function builds them so the prefab, the floor and the
    /// shadow layer cannot drift into three slightly different origins.
    /// </summary>
    public static class ArenaGeometry
    {
        /// <summary>
        /// A unit quad whose pivot is the middle of its bottom edge. This is a card's origin: the
        /// point that stands on the ground, which is what lets the layout place a tree by its base
        /// and scale it vertically without moving it.
        /// </summary>
        public static Mesh CreateBasePivotedQuad()
        {
            return Create(
                "ps-arena-card-quad",
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(-0.5f, 1f, 0f));
        }

        /// <summary>
        /// A unit quad centred on its origin. Used for ground decals, the floor and the instanced
        /// blob shadows, all of which are symmetric around the point they are placed at.
        /// </summary>
        public static Mesh CreateCenteredQuad()
        {
            return Create(
                "ps-arena-centered-quad",
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f));
        }

        private static Mesh Create(string name, Vector3 bottomLeft, Vector3 bottomRight, Vector3 topRight, Vector3 topLeft)
        {
            var mesh = new Mesh
            {
                name = name,
                vertices = new[] { bottomLeft, bottomRight, topRight, topLeft },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
