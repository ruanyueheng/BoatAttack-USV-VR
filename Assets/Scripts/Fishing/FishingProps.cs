using System;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Authored metre-scale props, sharing one vertex-colour material. No gameplay colliders.</summary>
    public static class FishingProps
    {
        public static GameObject Spawn(string asset, Transform parent, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>("FishingProps/" + asset);
            var material = Resources.Load<Material>("FishingProps/HarborPalette");
            if (prefab == null || material == null)
                throw new InvalidOperationException("Missing authored fishing prop or palette: " + asset);
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = asset;
            instance.transform.localPosition = position;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
            return instance;
        }
    }
}
