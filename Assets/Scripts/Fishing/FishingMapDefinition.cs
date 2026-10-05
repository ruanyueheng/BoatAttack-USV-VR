using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Measured coverage envelope, never a playable boundary, depth chart or safe route.</summary>
    [CreateAssetMenu(menuName = "Fishing/Terrain map definition")]
    public sealed class FishingMapDefinition : ScriptableObject
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public Texture2D texture;
        [Tooltip("World metres: min X, min Z, max X, max Z. Texture bottom is south (-Z).")]
        public Vector4 worldXZBounds;
        public float seaLevel;
        public string sourceScene;
        public string bakedUtc;
        public bool includesInitializedScenery;
        public int terrainCount;
        public int staticRendererCount;
        public int terrainVertexCount;
        public Vector2 metersPerTexel;
        [TextArea(2, 5)] public string captureNotes;
        [Tooltip("Captured source paths for reproducibility. Inactive distance-culled scenery is included explicitly.")]
        public string[] sources;

        public FishingMapMath.BoundsXZ Bounds => new FishingMapMath.BoundsXZ(
            worldXZBounds.x, worldXZBounds.y, worldXZBounds.z, worldXZBounds.w);
        public bool IsUsable => version == CurrentVersion && texture != null && Bounds.Valid;
    }
}
