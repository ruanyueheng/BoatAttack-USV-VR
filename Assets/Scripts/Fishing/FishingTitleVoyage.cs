using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Decorative actors share the real world, never the player's physics or save position.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class FishingTitleVoyage : MonoBehaviour
    {
        private FishingPrototype _c;
        private Transform _boat;
        private FishingCharacter _actor;
        private Vector3 _origin,_cameraPosition;
        private Quaternion _cameraRotation;
        private float _cameraFov,_age,_clock;
        private bool _open;
        private Transform _lighthouseSweep;
        private Quaternion _lighthouseRotation;
        private readonly List<Decoration> _decorations = new List<Decoration>();
        private readonly List<Decoration> _boatDecorations = new List<Decoration>();
        private sealed class Decoration
        {
            public Transform Source, Visual;
            public MeshRenderer Renderer;
            public bool WasEnabled;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public float Phase;
        }
        public int DecorativeCount => _decorations.Count + _boatDecorations.Count;
        public bool Sailing { get; private set; }
        public float Fade=>Sailing?Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.6f,4.5f,_age)):0;
        public Transform Boat=>_boat;
        public void Build(FishingPrototype c){_c=c;}
        public void Open()
        {
            if (_open || _c == null || _c.viewCamera == null) return;
            _open = true;
            _cameraPosition=_c.viewCamera.transform.position;_cameraRotation=_c.viewCamera.transform.rotation;_cameraFov=_c.viewCamera.fieldOfView;
            if(_boat==null)
            {
                _boat=new GameObject("V032 title boat · visual only").transform;_boat.SetParent(transform,false);
                foreach(var source in _c.Player.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=source.GetComponent<MeshRenderer>();if(renderer==null || source.sharedMesh==null || !renderer.enabled)continue;
                    var go=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(_boat,false);
                    go.transform.localPosition=_c.Player.transform.InverseTransformPoint(source.transform.position);
                    go.transform.localRotation=Quaternion.Inverse(_c.Player.transform.rotation)*source.transform.rotation;
                    go.transform.localScale=source.transform.lossyScale;
                    go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;go.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                    string detail = source.name.ToLowerInvariant();
                    if (_boatDecorations.Count < 12 && (detail.Contains("flag") || detail.Contains("rope") || detail.Contains("lantern")))
                        _boatDecorations.Add(new Decoration { Visual = go.transform, Position = go.transform.localPosition,
                            Rotation = go.transform.localRotation, Scale = go.transform.localScale, Phase = _boatDecorations.Count * 1.7f });
                }
                var rider=new GameObject("Captain preparing to sail");rider.transform.SetParent(_boat,false);
                rider.transform.localPosition=new Vector3(0,.25f,-.25f);
                _actor=rider.AddComponent<FishingCharacter>();_actor.Build();_actor.Pose(false,0,false,true,false,true);
            }
            // The home's open south water gives a readable lighthouse silhouette and a clear exit to the right.
            _origin=_c.Home.Origin+new Vector3(13,0,-39);
            _origin.y=.12f;
            PrepareDecorations();
            _lighthouseSweep=_c.Nightscape!=null?_c.Nightscape.LighthouseSweep:null;
            if(_lighthouseSweep!=null)_lighthouseRotation=_lighthouseSweep.rotation;
            _boat.gameObject.SetActive(true);_age=0;_clock=0;Sailing=false;Present(0);
        }
        public void Sail(){if(_boat==null || Sailing)return;Sailing=true;_age=0;_c.SaveSettings();}
        public void Close(bool preserveLook = false)
        {
            if (!_open) return;
            _open = false;
            RestoreDecorations();
            if(_lighthouseSweep!=null)_lighthouseSweep.rotation=_lighthouseRotation;
            if(_boat!=null)_boat.gameObject.SetActive(false);Sailing=false;
            if (_c == null || _c.viewCamera == null) return;
            _c.viewCamera.transform.SetPositionAndRotation(_cameraPosition,_cameraRotation);_c.viewCamera.fieldOfView=_cameraFov;
            if (!preserveLook) _c.View?.ResetLook();
            else _c.View?.ResetInput();
        }
        private void PrepareDecorations()
        {
            if (_decorations.Count == 0)
            {
                // 仅开菜单时扫描一次；原物件的 Transform/Collider 始终不动，摆动副本只有网格。
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    foreach (var source in root.GetComponentsInChildren<MeshFilter>())
                    {
                        if (_decorations.Count >= 24) break;
                        if (source.sharedMesh == null || source.transform.IsChildOf(_boat) || source.transform.IsChildOf(_c.Player.transform)) continue;
                        var renderer = source.GetComponent<MeshRenderer>();
                        if (renderer == null || !renderer.enabled || renderer.isPartOfStaticBatch || source.GetComponent<TextMesh>() != null) continue;
                        string name = source.name.ToLowerInvariant();
                        var ancestor = source.transform.parent;
                        for (int depth = 0; depth < 3 && ancestor != null; depth++, ancestor = ancestor.parent)
                            name += "/" + ancestor.name.ToLowerInvariant();
                        bool flora = name.Contains("plant") || name.Contains("flower") || name.Contains("potted") || name.Contains("palm");
                        bool hanging = name.Contains("flag") || name.Contains("windbell") || name.Contains("hanging");
                        if ((!flora && !hanging) || FishingPrototype.FlatDistance(source.transform.position, _origin) > 95) continue;
                        var visual = new GameObject("Title breeze · " + source.name, typeof(MeshFilter), typeof(MeshRenderer));
                        visual.transform.SetParent(transform, false); visual.layer = source.gameObject.layer;
                        visual.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                        var copy = visual.GetComponent<MeshRenderer>(); copy.sharedMaterials = renderer.sharedMaterials;
                        copy.shadowCastingMode = renderer.shadowCastingMode; copy.receiveShadows = renderer.receiveShadows;
                        _decorations.Add(new Decoration { Source = source.transform, Renderer = renderer, Visual = visual.transform,
                            Position = source.transform.position, Rotation = source.transform.rotation, Scale = source.transform.lossyScale,
                            Phase = _decorations.Count * 1.618f });
                    }
                    if (_decorations.Count >= 24) break;
                }
            }
            foreach (var decoration in _decorations)
            {
                if (decoration.Source == null || decoration.Renderer == null || decoration.Visual == null) continue;
                decoration.Position = decoration.Source.position; decoration.Rotation = decoration.Source.rotation;
                decoration.Scale = decoration.Source.lossyScale; decoration.WasEnabled = decoration.Renderer.enabled;
                decoration.Visual.SetPositionAndRotation(decoration.Position, decoration.Rotation);
                decoration.Visual.localScale = decoration.Scale; decoration.Visual.gameObject.SetActive(decoration.WasEnabled && decoration.Source.gameObject.activeInHierarchy);
                if (decoration.Visual.gameObject.activeSelf) decoration.Renderer.enabled = false;
            }
        }
        private void AnimateDecorations(float time)
        {
            foreach (var decoration in _decorations)
            {
                if (decoration.Visual == null || !decoration.Visual.gameObject.activeSelf) continue;
                decoration.Visual.SetPositionAndRotation(decoration.Position, decoration.Rotation *
                    Quaternion.Euler(Mathf.Sin(time * .65f + decoration.Phase) * 1.3f, 0, Mathf.Sin(time * .85f + decoration.Phase) * 1.7f));
            }
            foreach (var decoration in _boatDecorations)
            {
                decoration.Visual.localRotation = decoration.Rotation * Quaternion.Euler(0, 0, Mathf.Sin(time * 1.4f + decoration.Phase) * 3);
            }
        }
        private void RestoreDecorations()
        {
            foreach (var decoration in _decorations)
            {
                if (decoration.Renderer != null) decoration.Renderer.enabled = decoration.WasEnabled;
                if (decoration.Visual != null) decoration.Visual.gameObject.SetActive(false);
            }
            foreach (var decoration in _boatDecorations)
                if (decoration.Visual != null) decoration.Visual.localRotation = decoration.Rotation;
        }
        private void OnDisable() => Close(true);
        private void LateUpdate()
        {
            if(_c==null || !_open || !_c.StartMenuOpen)return;
            float dt=Application.isFocused || _c.Automated?Mathf.Min(Time.unscaledDeltaTime,.05f):0;
            if(Sailing)_age+=dt;_clock+=dt;Present(_clock);
            if(Sailing && _age>=4.6f)_c.FinishTitleVoyage();
        }
        public void Present(float time)
        {
            if(_boat==null || !_open || !FishingMapMath.Finite(time))return;
            float travel=Sailing?Mathf.Pow(Mathf.Clamp01(_age/4),1.7f)*45:0;
            _boat.position=_origin+new Vector3(travel,Mathf.Sin(time*1.1f)*.075f,travel*.12f);
            _boat.rotation=Quaternion.Euler(Mathf.Sin(time*.7f)*1.1f,80,Mathf.Sin(time*.9f)*1.5f);
            _actor.SampleTitlePose(Application.isFocused || _c.Automated?Time.unscaledDeltaTime:0);
            AnimateDecorations(time);
            if(_lighthouseSweep!=null)_lighthouseSweep.rotation=Quaternion.AngleAxis(time*9,Vector3.up)*_lighthouseRotation;
            var camera=_c.viewCamera;var position=_origin+new Vector3(11,4.9f,-13);
            var target=_origin+new Vector3(-5,2.8f,8);
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.fieldOfView=49;
        }
    }
}
