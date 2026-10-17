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
        public bool Sailing { get; private set; }
        public float Fade=>Sailing?Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.6f,4.5f,_age)):0;
        public Transform Boat=>_boat;
        public void Build(FishingPrototype c){_c=c;}
        public void Open()
        {
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
                }
                var rider=new GameObject("Captain preparing to sail");rider.transform.SetParent(_boat,false);
                rider.transform.localPosition=new Vector3(0,.25f,-.25f);
                _actor=rider.AddComponent<FishingCharacter>();_actor.Build();_actor.Pose(false,0,false,true,false,true);
            }
            // The home's open south water gives a readable lighthouse silhouette and a clear exit to the right.
            _origin=_c.Home.Origin+new Vector3(13,0,-39);
            _origin.y=.12f;
            _boat.gameObject.SetActive(true);_age=0;_clock=0;Sailing=false;Present(0);
        }
        public void Sail(){if(_boat==null || Sailing)return;Sailing=true;_age=0;_c.SaveSettings();}
        public void Close()
        {
            if(_boat!=null)_boat.gameObject.SetActive(false);Sailing=false;
            _c.viewCamera.transform.SetPositionAndRotation(_cameraPosition,_cameraRotation);_c.viewCamera.fieldOfView=_cameraFov;_c.View.ResetLook();
        }
        private void LateUpdate()
        {
            if(_c==null || !_c.StartMenuOpen)return;
            float dt=Application.isFocused || _c.Automated?Mathf.Min(Time.unscaledDeltaTime,.05f):0;
            if(Sailing)_age+=dt;_clock+=dt;Present(_clock);
            if(Sailing && _age>=4.6f)_c.FinishTitleVoyage();
        }
        public void Present(float time)
        {
            if(_boat==null)return;
            float travel=Sailing?Mathf.Pow(Mathf.Clamp01(_age/4),1.7f)*45:0;
            _boat.position=_origin+new Vector3(travel,Mathf.Sin(time*1.1f)*.075f,travel*.12f);
            _boat.rotation=Quaternion.Euler(Mathf.Sin(time*.7f)*1.1f,80,Mathf.Sin(time*.9f)*1.5f);
            _actor.Pose(false,0,false,true,false,true);
            var camera=_c.viewCamera;var position=_origin+new Vector3(11,4.9f,-13);
            var target=_origin+new Vector3(-5,2.8f,8);
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.fieldOfView=49;
        }
    }
}
