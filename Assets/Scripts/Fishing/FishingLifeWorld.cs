using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>V044 life features: film camera + album, VR palm chart, seasonal festival decorations.</summary>
    public sealed partial class FishingLifeWorld : MonoBehaviour
    {
        private FishingPrototype _c;
        private Transform _root;
        private Material _palette, _glow;
        private readonly List<Object> _owned = new List<Object>();
        public FishingXRBinoculars Binoculars {get;private set;}

        public void Build(FishingPrototype c)
        {
            _c = c;
            _root = new GameObject("V044 life (camera, palm chart, festival)").transform; _root.SetParent(transform, false);
            _palette = Own(new Material(c.PaletteShader) { name = "Life vertex palette" });
            _glow = Own(new Material(Resources.Load<Shader>("FishingNightSky")) { name = "Festival lantern glow" });
            _glow.SetFloat("_Solid", 1); _glow.SetFloat("_Visibility", 1); _glow.SetColor("_Tint", Color.white);
            BuildCamera(); BuildPalmMap(); BuildFestival();
            Binoculars=gameObject.AddComponent<FishingXRBinoculars>();Binoculars.Build(c);
        }
        private T Own<T>(T asset) where T : Object { _owned.Add(asset); return asset; }
        private void OnDestroy()
        {
            foreach (var photo in _album) if (photo.tex != null) Destroy(photo.tex);
            foreach (var o in _owned) if (o != null) Destroy(o);
        }

        private Transform MeshObject(string name, Mesh mesh, Material material, Transform parent)
        {
            Own(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Desktop viewfinder is modal: true consumes the whole frame (no casting, shooting or walking).</summary>
        public bool Tick(Keyboard k, Mouse m, bool focused, bool xr)
        {
            TickFestival();
            if (xr) { if (Viewfinding) ExitViewfinder(); return false; }
            if (!Viewfinding)
            {
                if (focused && !_c.Automated && k != null && k.pKey.wasPressedThisFrame && CanUseCamera) { EnterViewfinder(); return true; }
                return false;
            }
            if (!CanUseCamera || (!focused && !_c.Automated)) { ExitViewfinder(); return false; }
            if (_c.Automated) return true;
            if ((k != null && (k.pKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) || (m != null && m.rightButton.wasPressedThisFrame)) ExitViewfinder();
            else if ((m != null && m.leftButton.wasPressedThisFrame) || (k != null && k.spaceKey.wasPressedThisFrame)) TakePhoto();
            return true;
        }
        public bool CanUseCamera => _c != null && !_c.Paused && !_c.StoryBlocksGameplay && !_c.InventoryOpen && !_c.JournalOpen && !_c.WorldActionBusy
            && !_c.XRNotebook.IsOpen && (_c.Prologue == null || !_c.Prologue.Open);

        private void LateUpdate()
        {
            if (_c == null) return;
            UpdateVrCamera(_c.Automated || Application.isFocused);
            UpdatePalmMap();
            AnimateFestival();
        }
    }
}
