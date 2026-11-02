using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingLifeWorld
    {
        public const int PhotoWidth = 320, PhotoHeight = 180, AlbumLimit = 36, JpegQuality = 80;
        public bool Viewfinding { get; private set; }
        public bool VrRaised { get; private set; }
        public int PhotosTaken { get; private set; }
        public int LastEntry { get; private set; } = -1;
        public string LastPhotoPath { get; private set; } = "";
        /// <summary>Test hooks: when set they replace XR tracking (world poses).</summary>
        public Pose? DebugRightHand, DebugLeftHand;
        public static bool ForceVr;
        public static bool Vr => ForceVr || XRSettings.isDeviceActive;
        private Camera _photoCamera;
        private RenderTexture _film;
        private Transform _prop, _frame;
        private bool _wasFirstPerson;
        private float _flash = -10;
        private readonly FishingPressEdge _vrShutter = new FishingPressEdge();
        private AudioSource _shutter;

        private void BuildCamera()
        {
            var b = new FishingLifeMesh();
            Color leather = new Color(.13f, .12f, .11f), brass = new Color(.78f, .6f, .32f), glass = new Color(.18f, .26f, .32f), chrome = new Color(.72f, .72f, .7f);
            b.Box(new Vector3(0, 0, 0), new Vector3(.12f, .07f, .045f), leather);
            b.Box(new Vector3(0, .042f, 0), new Vector3(.124f, .014f, .048f), brass);
            b.Box(new Vector3(-.035f, .056f, -.002f), new Vector3(.03f, .016f, .028f), brass); // viewfinder hump
            b.Cylinder(new Vector3(.034f, .049f, 0), new Vector3(.034f, .06f, 0), .009f, .009f, chrome, 10); // wind knob
            b.Cylinder(new Vector3(-.006f, .049f, .008f), new Vector3(-.006f, .055f, .008f), .005f, .005f, chrome, 8); // shutter button
            b.Cylinder(new Vector3(.01f, 0, .022f), new Vector3(.01f, 0, .05f), .025f, .023f, leather, 14);
            b.Cylinder(new Vector3(.01f, 0, .05f), new Vector3(.01f, 0, .056f), .024f, .024f, brass, 14);
            b.Cylinder(new Vector3(.01f, 0, .055f), new Vector3(.01f, 0, .057f), .018f, .018f, glass, 14);
            _prop = MeshObject("Old film camera (hand prop)", b.Finish("Old film camera"), _palette, _root);
            _prop.gameObject.SetActive(false);
            var f = new FishingLifeMesh(); Color gilt = new Color(.93f, .78f, .45f);
            float w = .064f, h = w * PhotoHeight / PhotoWidth, t = .0025f;
            f.Box(new Vector3(0, h, 0), new Vector3(2 * w + t, t, t), gilt); f.Box(new Vector3(0, -h, 0), new Vector3(2 * w + t, t, t), gilt);
            f.Box(new Vector3(w, 0, 0), new Vector3(t, 2 * h, t), gilt); f.Box(new Vector3(-w, 0, 0), new Vector3(t, 2 * h, t), gilt);
            _frame = MeshObject("VR viewfinder frame", f.Finish("Viewfinder frame"), _glow, _root); _frame.gameObject.SetActive(false);

            var go = new GameObject("Life photo camera", typeof(Camera)); go.transform.SetParent(_root, false);
            _photoCamera = go.GetComponent<Camera>(); _photoCamera.CopyFrom(_c.viewCamera); _photoCamera.enabled = false;
            _photoCamera.cullingMask &= ~(1 << 5); _photoCamera.stereoTargetEye = StereoTargetEyeMask.None; _photoCamera.fieldOfView = 55;
            _film = Own(new RenderTexture(PhotoWidth, PhotoHeight, 24) { name = "Life photo film", antiAliasing = 2 }); _film.Create();
            _photoCamera.targetTexture = _film;
            _shutter = gameObject.AddComponent<AudioSource>(); _shutter.playOnAwake = false; _shutter.spatialBlend = 0; _shutter.volume = .5f;
            _shutter.clip = Own(ShutterClip());
            _c.RegisterAudioSource(_shutter, FishingAudioCategory.Effects, .5f);
        }
        private static AudioClip ShutterClip()
        {
            const int rate = 22050; var data = new float[(int)(rate * .11f)]; var rng = new System.Random(7);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate, click = t < .012f ? 1 : t > .06f && t < .072f ? .7f : 0;
                data[i] = (float)(rng.NextDouble() * 2 - 1) * click * Mathf.Exp(-((t % .06f) * 140)) * .8f;
            }
            var clip = AudioClip.Create("Film shutter", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        public void EnterViewfinder()
        {
            if (Viewfinding || !CanUseCamera) return;
            Viewfinding = true; _wasFirstPerson = _c.FirstPerson;
            if (!_c.FirstPerson) _c.ToggleView();
            _c.Shore.ResetMoveInput();
        }
        public void ExitViewfinder()
        {
            if (!Viewfinding) return;
            Viewfinding = false;
            if (!_wasFirstPerson && _c.FirstPerson) _c.ToggleView();
        }

        // ---------- VR: raise the right hand to the eye ----------
        private bool RightHand(out Vector3 p, out Quaternion q)
        {
            if (DebugRightHand.HasValue) { p = DebugRightHand.Value.position; q = DebugRightHand.Value.rotation; return true; }
            return FishingXRTracking.TryPose(_c.viewCamera, XRNode.RightHand, out p, out q);
        }
        /// <summary>Hand within 22 cm of the eye and in front of it.</summary>
        public static bool RaisedToEye(Vector3 eye, Vector3 eyeForward, Vector3 hand)
        {
            var d = hand - eye; return d.magnitude <= .22f && Vector3.Dot(d, eyeForward) > .02f;
        }
        private void UpdateVrCamera()
        {
            bool raised = false;
            // Equipped binoculars own the near-eye gesture and trigger; never replace them with a film camera.
            if (Vr && CanUseCamera && !_c.InspectingItem && !_c.Hands.HoldingRod && !_c.Anchored
                && _c.Career.Backpack.Equipped != FieldItem.HuntingRifle && _c.Career.Backpack.Equipped != FieldItem.Binoculars
                && RightHand(out var p, out var q))
            {
                var eye = _c.viewCamera.transform;
                raised = RaisedToEye(eye.position, eye.forward, p);
                bool near = raised || (p - eye.position).magnitude < .38f && Vector3.Dot(p - eye.position, eye.forward) > 0;
                _prop.gameObject.SetActive(near && !raised);
                if (near) _prop.SetPositionAndRotation(p, q);
                _frame.gameObject.SetActive(raised);
                if (raised) _frame.SetPositionAndRotation(eye.position + eye.forward * .2f, eye.rotation);
                bool trigger = false;
                if (!DebugRightHand.HasValue) InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.triggerButton, out trigger);
                if (_vrShutter.Step(raised, trigger || _debugShutter)) TakePhoto();
            }
            else { _prop.gameObject.SetActive(false); _frame.gameObject.SetActive(false); _vrShutter.Step(false, false); }
            _debugShutter = false;
            VrRaised = raised;
        }
        private bool _debugShutter;
        /// <summary>Automation: press the VR shutter for one frame (needs a released frame first, like the real trigger).</summary>
        public void DebugPressShutter() => _debugShutter = true;

        // ---------- Photo ----------
        public string AlbumDirectory => _c.AlbumDirectory;
        public string TakePhoto()
        {
            var eye = _c.viewCamera.transform;
            _photoCamera.transform.SetPositionAndRotation(eye.position, eye.rotation);
            _photoCamera.fieldOfView = Vr ? 55 : _c.viewCamera.fieldOfView;
            int entry = Subject(_photoCamera);
            bool propWasOn = _prop.gameObject.activeSelf, frameWasOn = _frame.gameObject.activeSelf;
            _prop.gameObject.SetActive(false); _frame.gameObject.SetActive(false);
            var previous = RenderTexture.active; Texture2D image = null; string path = "";
            try
            {
                _photoCamera.Render(); RenderTexture.active = _film;
                image = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0); image.Apply();
                path = SaveToAlbum(image.EncodeToJPG(JpegQuality), entry);
            }
            catch (Exception ex) { Debug.LogWarning("[LIFE] Photo not saved: " + ex.Message); }
            finally { RenderTexture.active = previous; if (image != null) Destroy(image); _prop.gameObject.SetActive(propWasOn); _frame.gameObject.SetActive(frameWasOn); }
            _shutter.Play(); _flash = Time.unscaledTime; PhotosTaken++; LastEntry = entry; LastPhotoPath = path;
            if (Vr) Haptic();
            int pay = entry >= 0 ? _c.Career.RecordPhoto(entry) : 0;
            if (pay > 0)
            {
                _c.Save(); _c.Effects?.Celebrate(_c.ExplorerPosition + eye.forward * 1.5f);
                if (!Vr) _c.Notice("图鉴新插图 · " + FishingPhotoCodex.Name(entry) + "  +" + pay + " 金币", 5);
            }
            else if (!Vr) _c.Notice(path.Length == 0 ? "照片没能冲洗出来（存储不可用）。" : entry >= 0 ? "已存入相册 · " + FishingPhotoCodex.Name(entry) : "已存入相册。", 3);
            _albumDirty = true;
            return path;
        }
        private static void Haptic()
        {
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (hand.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) FishingXRInput.Pulse(hand,.35f, .05f);
        }
        private string SaveToAlbum(byte[] jpg, int entry)
        {
            var dir = AlbumDirectory; Directory.CreateDirectory(dir);
            var files = AlbumFiles(dir);
            int next = files.Count == 0 ? 0 : files.Max(f => f.sequence) + 1;
            // Oldest photos roll off so one run never exceeds AlbumLimit × ~20 KB.
            for (int i = 0; i <= files.Count - AlbumLimit; i++) File.Delete(Path.Combine(dir, files[i].name));
            var path = Path.Combine(dir, FishingPhotoCodex.FileName(next, _c.Career.Calendar.Day, entry));
            File.WriteAllBytes(path, jpg); return path;
        }
        public static List<(string name, int sequence, int day, int entry)> AlbumFiles(string dir)
        {
            var list = new List<(string, int, int, int)>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;
            foreach (var file in Directory.GetFiles(dir, "p*.jpg"))
            {
                var name = Path.GetFileName(file);
                if (FishingPhotoCodex.TryParse(name, out int s, out int d, out int e)) list.Add((name, s, d, e));
            }
            list.Sort((a, b) => a.Item2.CompareTo(b.Item2)); return list;
        }

        // ---------- Album textures (loaded lazily for the journal page) ----------
        private bool _albumDirty = true;
        private readonly List<(Texture2D tex, int day, int entry)> _album = new List<(Texture2D, int, int)>();
        public IReadOnlyList<(Texture2D tex, int day, int entry)> Album
        {
            get
            {
                if (!_albumDirty) return _album;
                _albumDirty = false;
                foreach (var a in _album) if (a.tex != null) Destroy(a.tex);
                _album.Clear();
                foreach (var f in AlbumFiles(AlbumDirectory))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = f.name, wrapMode = TextureWrapMode.Clamp };
                    try { if (tex.LoadImage(File.ReadAllBytes(Path.Combine(AlbumDirectory, f.name)), true)) { _album.Add((tex, f.day, f.entry)); continue; } }
                    catch (IOException) { }
                    Destroy(tex);
                }
                return _album;
            }
        }
        public void RefreshAlbum() => _albumDirty = true;

        // ---------- Subjects ----------
        private struct Candidate { public int entry; public Vector3 point; public float range; public Transform owner; }
        private readonly List<Candidate> _candidates = new List<Candidate>(24);
        private void Add(int entry, Transform t, Vector3 offset, float range) { if (t != null && t.gameObject.activeInHierarchy) _candidates.Add(new Candidate { entry = entry, point = t.position + offset, range = range, owner = t }); }
        /// <summary>Best codex entry in frame: an un-photographed one first, otherwise any visible one, else -1.</summary>
        public int Subject(Camera cam)
        {
            _candidates.Clear();
            var sea = _c.SeaEvents;
            if (sea != null)
            {
                if (sea.Whale != null) Add(FishingPhotoCodex.Whale, sea.Whale, Vector3.up, 80);
                if (sea.Dolphins != null) foreach (var d in sea.Dolphins) Add(FishingPhotoCodex.Dolphin, d, Vector3.zero, 40);
            }
            if (_c.HasPendingCatch && _c.Career.PendingCatch.fish.rare) Add(FishingPhotoCodex.RareFish, _c.PendingFishTransform, Vector3.zero, 3);
            for (int i = 0; i < 6; i++) Add(FishingPhotoCodex.Resident + i, _c.Residents.Person(i), Vector3.up * 1.35f, 12);
            for (int i = 0; i < 4; i++) Add(FishingPhotoCodex.Realm + i, _c.Realms.Portal(i), Vector3.up * 1.5f, 45);
            if (_c.Home != null) _candidates.Add(new Candidate { entry = FishingPhotoCodex.Lighthouse, point = _c.Home.LighthouseLens, range = 120, owner = _c.Home.transform });
            if (FestivalShown >= 0) _candidates.Add(new Candidate { entry = FishingPhotoCodex.Festival, point = Plaza + Vector3.up * 3, range = 25, owner = _festivalRoot });
            int seen = -1;
            if (sea != null && sea.Shown == SeaEventKind.Glow && !_c.Ashore && Vector3.Dot(cam.transform.forward, Vector3.down) > .17f) seen = PickNew(FishingPhotoCodex.GlowSea, seen);
            foreach (var cand in _candidates)
            {
                var vp = cam.WorldToViewportPoint(cand.point);
                if (vp.z < .3f || vp.z > cand.range || vp.x < .15f || vp.x > .85f || vp.y < .15f || vp.y > .85f || Blocked(cam.transform.position, cand)) continue;
                seen = PickNew(cand.entry, seen);
            }
            return seen;
        }
        private int PickNew(int entry, int current) => current < 0 || (_c.Career.Lore.PhotoFlag(current) && !_c.Career.Lore.PhotoFlag(entry)) ? entry : current;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private bool Blocked(Vector3 eye, Candidate cand)
        {
            var dir = cand.point - eye; float dist = dir.magnitude - .6f; if (dist <= 0) return false;
            int n = Physics.RaycastNonAlloc(eye, dir / (dist + .6f), _hits, dist, ~(1 << 5), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = _hits[i].collider.transform;
                if (cand.owner != null && t.IsChildOf(cand.owner)) continue;
                if (t.IsChildOf(_c.Shore.Actor) || t.IsChildOf(_c.Player.transform)) continue;
                return true;
            }
            return false;
        }

        // ---------- Desktop viewfinder overlay ----------
        private void OnGUI()
        {
            if (_c == null) return;
            float flash = 1 - (Time.unscaledTime - _flash) / .25f;
            if (!Viewfinding && flash <= 0) return;
            GUI.depth = -50;
            float w = Screen.width, h = Screen.height, fh = Mathf.Min(h * .78f, w * .78f * PhotoHeight / PhotoWidth), fw = fh * PhotoWidth / PhotoHeight;
            var r = new Rect((w - fw) * .5f, (h - fh) * .5f, fw, fh);
            var old = GUI.color;
            if (Viewfinding)
            {
                GUI.color = new Color(0, 0, 0, .55f);
                GUI.DrawTexture(new Rect(0, 0, w, r.y), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, r.yMax, w, h - r.yMax), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, r.y, r.x, fh), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax, r.y, w - r.xMax, fh), Texture2D.whiteTexture);
                GUI.color = FishingUITheme.Gold; float s = fh * .08f, t = Mathf.Max(2, h / 450);
                foreach (var c in new[] { new Vector2(r.x, r.y), new Vector2(r.xMax, r.y), new Vector2(r.x, r.yMax), new Vector2(r.xMax, r.yMax) })
                {
                    float sx = c.x > w * .5f ? -1 : 1, sy = c.y > h * .5f ? -1 : 1;
                    GUI.DrawTexture(new Rect(Mathf.Min(c.x, c.x + sx * s), c.y - (sy < 0 ? t : 0), s, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(c.x - (sx < 0 ? t : 0), Mathf.Min(c.y, c.y + sy * s), t, s), Texture2D.whiteTexture);
                }
                GUI.color = new Color(1, 1, 1, .7f);
                GUI.DrawTexture(new Rect(w * .5f - s * .3f, h * .5f - t * .5f, s * .6f, t * .5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w * .5f - t * .25f, h * .5f - s * .3f, t * .5f, s * .6f), Texture2D.whiteTexture);
                var style = new GUIStyle(GUI.skin.label) { font = FishingUIFont.Font, fontSize = Mathf.RoundToInt(h / 900 * 17), alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(.97f, .92f, .82f);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, r.yMax + 6, w, h - r.yMax - 6), "左键 / 空格  拍照      P / 右键 / Esc  收起相机      相册：J → 相册图鉴", style);
                int film = AlbumLimit - Mathf.Min(AlbumLimit, Album.Count);
                GUI.Label(new Rect(r.x, r.y - h * .045f, fw, h * .04f), "胶片相机  ·  " + _c.Career.Calendar.DateLabel + "  ·  相册余量 " + film + " 张（满后覆盖最旧）", style);
            }
            if (flash > 0) { GUI.color = new Color(1, 1, 1, flash * .8f); GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture); }
            GUI.color = old;
        }
    }
}
