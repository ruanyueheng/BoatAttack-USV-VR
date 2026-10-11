using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed class FishingRegionPresentation : MonoBehaviour
    {
        private FishingPrototype _c;
        private int _region = int.MinValue;
        private float _shownAt = -20, _candidateAt;
        private int _candidate = int.MinValue;
        private CanvasGroup _group;
        private Text _xrText;
        private AudioSource _audio;
        private readonly AudioClip[] _chimes = new AudioClip[3];
        public string Title { get; private set; } = "";
        public string Subtitle { get; private set; } = "";
        public int Theme { get; private set; }
        public float Age => Time.unscaledTime - _shownAt;
        public float Alpha => Mathf.SmoothStep(0, 1, Mathf.Clamp01(Age / 1.1f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((6.2f - Age) / 1.2f));
        public void Build(FishingPrototype c)
        {
            _c = c; _audio = gameObject.AddComponent<AudioSource>(); _audio.spatialBlend = 0; _audio.volume = .16f;
            for (int n = 0; n < 3; n++)
            {
                var clip = AudioClip.Create("Tide chime " + n, 13230, 1, 44100, false); var samples = new float[13230];
                float frequency = n == 0 ? 523.25f : n == 1 ? 659.25f : 783.99f;
                for (int i = 0; i < samples.Length; i++) { float t = i / 44100f; samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2) * Mathf.Exp(-t * 14) * Mathf.Min(1, t * 90); }
                clip.SetData(samples, 0); _chimes[n] = clip;
            }
            var root = new GameObject("Region inscription XR", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup)); root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = c.viewCamera;
            var rt = root.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(840, 150); rt.localScale = Vector3.one * .0016f;
            _group = root.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; _group.alpha = 0;
            var text = new GameObject("Region calligraphy", typeof(RectTransform), typeof(Text)); text.transform.SetParent(root.transform, false);
            var rect = text.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            _xrText = text.GetComponent<Text>(); _xrText.font = FishingUIFont.Font; _xrText.fontSize = 32; _xrText.alignment = TextAnchor.MiddleCenter;
            _xrText.color = new Color(.98f, .89f, .64f); _xrText.raycastTarget = false;
        }
        public void Chime(int note)
        {
            if (_c.QuietMode || _c.Paused) return;
            _audio.PlayOneShot(_chimes[Mathf.Abs(note) % 3]);
        }
        public void Show(string title, string subtitle, int theme)
        {
            Title = title; Subtitle = subtitle; Theme = theme; _shownAt = Time.unscaledTime; Chime(theme);
            _xrText.text = title + "\n<size=20>" + subtitle + "</size>";
            var camera = _c.viewCamera.transform; var direction = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector3.forward;
            _group.transform.SetPositionAndRotation(camera.position + direction * 2.1f + Vector3.up * .18f, Quaternion.LookRotation(direction));
        }
        public void Tick()
        {
            _group.alpha = XRSettings.isDeviceActive && !_c.DialogueOpen && !_c.Realms.Transitioning ? Alpha : 0;
            if (_c.Realms.Inside || _c.Realms.Transitioning || _c.DialogueOpen || _c.Paused) return;
            if(_c.Acceptance!=null && _c.Acceptance.FreezeScenario)return;
            int region = -1;
            if (_c.TackleShop.Inside) region = 4;
            else
            {
                for (int i = 0; i < 4; i++)
                    if (FishingPrototype.FlatDistance(_c.ExplorerPosition, FishingIslandWorld.Center(i)) < (_region == i ? 105 : 80)) { region = i; break; }
                if (region < 0 && FishingPrototype.FlatDistance(_c.ExplorerPosition, _c.harbor) < 40) region = 5;
                if (region < 0 && _c.Ashore && _c.Shore.HasSurveySite) region = 6 + _c.Shore.CurrentSite;
                if (region < 0 && _c.Anchored) region = 10 + _c.Spot;
            }
            if (region != _candidate) { _candidate = region; _candidateAt = Time.unscaledTime; }
            if (region == _region || Time.unscaledTime - _candidateAt < .75f) return;
            _region = region;
            Show(region < 0 ? "回潮海" : region < 4 ? FishingAdventure.Name(region) : region == 4 ? "潮汐钓具店" : region == 5 ? "潮汐渔港"
                : region < 10 ? FishingExploration.SiteName(region - 6) : FishingGrounds.Name(region - 10),
                region < 0 ? "自由航行 · 让风带你去新的岸边" : region < 4 ? FishingLore.Subtitle(region) : region == 4 ? "远航的准备，从一盏灯开始" : region == 5 ? "灯火常在 · 每一段航程都能归来" : "海岸札记 · 探索与发现", region);
        }
        private void OnDestroy() { foreach (var clip in _chimes) if (clip != null) Destroy(clip); }
    }
}
