using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Read-only, boat-relative world-space instrument preview; not an XR loader or tracked rig.</summary>
    public sealed class FishingWorldInstruments : MonoBehaviour
    {
        public FishingPrototype Controller;
        public const float ReferenceDistance = 2.4f;
        private Canvas _canvas;
        private Text _status, _hint;
        private RectTransform _fill;
        private float _nextText, _height, _heightVelocity;
        private bool _wasVisible;
        private void Start()
        {
            var root = new GameObject("V03 world instruments (read-only)", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(440, 160); rect.localScale = Vector3.one * 0.0022f;
            // Opaque, softly colored instrument; no full-screen head-locked overlay or raycaster.
            var plate = root.AddComponent<Image>(); plate.color = new Color(0.025f, 0.075f, 0.10f, 1f); plate.raycastTarget = false;
            _status = TextAt(root.transform, "TIDE & FIN", new Vector2(20, -12), new Vector2(400, 65), 26);
            _hint = TextAt(root.transform, "", new Vector2(20, -110), new Vector2(400, 35), 18);
            var track = new GameObject("Tension track", typeof(RectTransform), typeof(Image)); track.transform.SetParent(root.transform, false);
            var t = track.GetComponent<RectTransform>(); t.anchorMin = t.anchorMax = new Vector2(0, 1); t.pivot = new Vector2(0, 1); t.anchoredPosition = new Vector2(20, -88); t.sizeDelta = new Vector2(400, 10);
            track.GetComponent<Image>().color = new Color(0.12f, 0.23f, 0.27f); track.GetComponent<Image>().raycastTarget = false;
            var bar = new GameObject("Tension", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(track.transform, false);
            _fill = bar.GetComponent<RectTransform>(); _fill.anchorMin = _fill.anchorMax = new Vector2(0, 0.5f); _fill.pivot = new Vector2(0, 0.5f); _fill.sizeDelta = new Vector2(0, 10);
            bar.GetComponent<Image>().color = new Color(0.32f, 0.9f, 0.77f); bar.GetComponent<Image>().raycastTarget = false;
        }
        private static Text TextAt(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject("Instrument label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.text = value; text.color = new Color(0.9f, 0.96f, 0.93f); text.raycastTarget = false; return text;
        }
        private void LateUpdate()
        {
            var c = Controller; if (c == null || c.Player == null || _canvas == null) return;
            bool visible = !c.Ashore && (c.WorldInstruments || XRSettings.isDeviceActive);
            _canvas.gameObject.SetActive(visible);
            if (!visible) { _wasVisible = false; return; }
            if (!_wasVisible) { _height = c.Player.transform.position.y; _heightVelocity = 0; _nextText = 0; }
            _wasVisible = true;
            if (!c.Paused)
            {
                var boat = c.Player.transform;
                var yaw = Quaternion.Euler(0, boat.eulerAngles.y + (c.Anchored ? 66 : 0), 0);
                if (Time.deltaTime > 0f) _height = Mathf.SmoothDamp(_height, boat.position.y, ref _heightVelocity, 2.5f);
                _canvas.transform.SetPositionAndRotation(new Vector3(boat.position.x, _height + 1.15f, boat.position.z) + yaw * new Vector3(-0.35f, 0, ReferenceDistance), yaw);
            }
            _fill.sizeDelta = new Vector2(c.Session.Tension * 400, 10);
            if (Time.unscaledTime < _nextText) return; _nextText = Time.unscaledTime + 0.1f;
            string hold = "HOLD " + c.Career.Count + "/" + c.Career.Capacity;
            _status.text = c.Anchored ? "FISHING  /  " + c.Session.Phase.ToString().ToUpperInvariant() + "\nLINE " + (c.Session.Tension * 100).ToString("0") + "%   " + hold : "TIDE & FIN   /   HELM\n" + (c.Speed * 3.6f).ToString("0") + " km/h    " + hold;
            _hint.text = c.Paused ? "PAUSED" : c.Anchored && c.Session.Phase == CatchPhase.Fighting ? BeatHint(c.Session.Beat) : "HARBOR " + c.HarborDistance.ToString("0") + "m";
        }
        private static string BeatHint(FishBeat beat)
        {
            switch (beat)
            {
                case FishBeat.Rest: return "REST - REEL IN";
                case FishBeat.Windup: return "WINDUP - PREPARE TO RELEASE";
                case FishBeat.Leap: return "LEAP - RELEASE NOW";
                default: return "SURGE - EASE THE LINE";
            }
        }
    }
}
