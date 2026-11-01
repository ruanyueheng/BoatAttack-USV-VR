using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public static class FishingV019Art
    {
        public static Transform Spawn(string name, Transform parent, Vector3 point, float span, float yaw = 0, string folder = "FishingV019")
        {
            var source = Resources.Load<GameObject>(folder + "/" + name);
            if (source == null) throw new System.InvalidOperationException("Missing art: " + folder + "/" + name);
            var root = new GameObject(name).transform; root.SetParent(parent, false);
            var model = Object.Instantiate(source, root, false).transform;
            var bounds = new Bounds(); bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = root.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            if (first || bounds.size.sqrMagnitude < .0001f) throw new System.InvalidOperationException("Empty v19 model: " + name);
            float scale = span / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            model.localScale *= scale;
            model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            root.localPosition = point; root.localRotation = Quaternion.Euler(0, yaw, 0);
            FishingPaintedArt.Apply(root, folder + "/" + name);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.On;
            return root;
        }
        public static TextMesh Sign(string text, Transform parent, Vector3 point, float size, Color color)
        {
            var label = new GameObject("Sign " + text).AddComponent<TextMesh>(); label.transform.SetParent(parent, false);
            label.transform.localPosition = point; FishingXRText.Bind(label, text); label.font = FishingUIFont.Font;
            label.fontSize = 64; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter; label.color = color;
            FishingWorldText.Apply(label);
            // V044：浅色说明牌改为绘本标签牌（淡纸底 + 细金边、深墨字），靠近时淡入。
            if (color.grayscale > .6f) { label.color = FishingWorldSign.Ink; label.gameObject.AddComponent<FishingWorldSign>(); }
            return label;
        }
    }
}
