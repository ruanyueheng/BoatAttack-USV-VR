using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Storybook plaque behind a world TextMesh; fades in only near the camera (no per-frame allocation).</summary>
    [RequireComponent(typeof(TextMesh))]
    public sealed class FishingWorldSign : MonoBehaviour
    {
        public static readonly Color Ink = new Color32(38, 58, 72, 255);
        private static Sprite _plate;
        private static Material _material;
        private TextMesh _text;
        private MeshRenderer _textRenderer;
        private SpriteRenderer _plateRenderer;
        private float _near, _far, _alpha = -1;
        public float Alpha => Mathf.Max(0, _alpha);

        private void Start()
        {
            _text = GetComponent<TextMesh>(); _textRenderer = GetComponent<MeshRenderer>();
            if (_plate == null)
            {
                var texture = Resources.Load<Texture2D>("FishingV044/UI/SignPlate");
                if (texture == null) { enabled = false; return; }
                _plate = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
                _material = new Material(Shader.Find("Sprites/Default")) { name = "V044 sign plate" };
            }
            var go = new GameObject("Storybook sign plate"); go.transform.SetParent(transform, false);
            _plateRenderer = go.AddComponent<SpriteRenderer>(); _plateRenderer.sprite = _plate; _plateRenderer.sharedMaterial = _material;
            _plateRenderer.drawMode = SpriteDrawMode.Sliced; _plateRenderer.sortingOrder = -1;
            Refit();
            _near = 6 + _text.characterSize * 400; _far = _near + 3;
        }

        /// <summary>Resize the plate to the current text; call after changing TextMesh.text.</summary>
        public void Refit()
        {
            if (_plateRenderer == null) return;
            var bounds = _textRenderer.localBounds;
            float border = _text.characterSize * 3.2f, unit = border / .24f; // 24px border at 100ppu = .24 units
            var plate = _plateRenderer.transform;
            plate.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + .004f);
            plate.localScale = Vector3.one * unit;
            _plateRenderer.size = new Vector2(bounds.size.x + border * 2.2f, bounds.size.y + border * 1.6f) / unit;
        }
        private void LateUpdate()
        {
            var camera = Camera.main; if (camera == null) return;
            // 文字只在 -Z 面可读：玩家走到背面时翻转 180°，避免反字（不做持续 billboard，保留布景朝向）。
            if (Vector3.Dot(transform.forward, transform.position - camera.transform.position) < -.05f) transform.Rotate(0, 180, 0, Space.World);
            float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(_near, _far, Vector3.Distance(camera.transform.position, transform.position)));
            alpha = _alpha < 0 ? alpha : Mathf.MoveTowards(_alpha, alpha, Time.unscaledDeltaTime * 3);
            if (Mathf.Approximately(alpha, _alpha)) return;
            _alpha = alpha; bool visible = alpha > .01f;
            _textRenderer.enabled = _plateRenderer.enabled = visible;
            var ink = _text.color; ink.a = alpha; _text.color = ink;
            _plateRenderer.color = new Color(1, 1, 1, alpha);
        }
    }
}
