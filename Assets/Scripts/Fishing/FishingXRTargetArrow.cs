using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>XR off-view target cue: a gilt star 1.5 m ahead, pushed to the 28° view edge toward the target, with "name · N 米"; hidden within 6 m.</summary>
    public sealed class FishingXRTargetArrow : MonoBehaviour
    {
        private const float Distance=1.5f,EdgeDegrees=28f;
        private FishingPrototype _c;
        private SpriteRenderer _star;
        private TextMesh _label;
        private float _alpha;
        private int _lastMetres=-1;
        public void Bind(FishingPrototype c)=>_c=c;
        private void LateUpdate()
        {
            var cam=Camera.main;if(_c==null || cam==null)return;
            if(_star==null)
            {
                var star=FishingUITheme.Star;
                _star=new GameObject("V044 XR target star").AddComponent<SpriteRenderer>();
                _star.sprite=Sprite.Create(star,new Rect(0,0,star.width,star.height),new Vector2(.5f,.5f),star.width/.06f);
                _star.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                _label=FishingV019Art.Sign("航点",_star.transform,new Vector3(0,-.055f,0),.0045f,Color.white);
            }
            if(_star.transform.parent!=cam.transform)_star.transform.SetParent(cam.transform,false);
            var target=_c.NavigationPosition;float metres=FishingPrototype.FlatDistance(_c.ExplorerPosition,target);
            var local=cam.transform.InverseTransformDirection(target-cam.transform.position);
            float angle=Vector3.Angle(Vector3.forward,local);
            bool show=metres>=6 && angle>EdgeDegrees && !_c.Paused && !_c.StoryBlocksGameplay;
            _alpha=Mathf.MoveTowards(_alpha,show?1:0,Time.unscaledDeltaTime*2.5f);
            _star.enabled=_alpha>.01f;_star.gameObject.SetActive(_star.enabled);
            if(!_star.enabled)return;
            var dir=Vector3.RotateTowards(Vector3.forward,local.normalized,EdgeDegrees*Mathf.Deg2Rad,0);
            _star.transform.localPosition=dir*Distance;
            _star.transform.localRotation=Quaternion.LookRotation(dir,Vector3.up);
            _star.color=new Color(.91f,.72f,.35f,_alpha);
            int rounded=Mathf.RoundToInt(metres);
            if(rounded!=_lastMetres)
            {
                _lastMetres=rounded;string name=_c.NavigationName;if(name.Length>8)name=name.Substring(0,7)+"…";
                _label.text=name+" · "+rounded+" 米";_label.GetComponent<FishingWorldSign>()?.Refit();
            }
        }
        private void OnDisable(){_alpha=0;if(_star!=null)_star.gameObject.SetActive(false);}
        private void OnDestroy(){if(_star!=null)Destroy(_star.gameObject);}
    }
}
