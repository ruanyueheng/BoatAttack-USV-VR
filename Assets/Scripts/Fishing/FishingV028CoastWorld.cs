using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingV028CoastWorld CoastWorld {get;private set;}
        internal void RefreshBarbecueHold()=>RefreshHold();
    }

    /// <summary>The home grill is a view of a saved transaction; animations never own fish or rewards.</summary>
    public sealed class FishingV028CoastWorld : MonoBehaviour
    {
        private FishingPrototype _c;
        private Material _palette;
        private readonly List<UnityEngine.Object> _owned=new List<UnityEngine.Object>();
        private GameObject _raw,_cooked,_find;
        private string _presentedFish="";
        private Vector3 _findStart;
        private float _transfer,_findAge,_saveClock;
        private Light _embers;
        private TextMesh _sign;
        public Transform Grill {get;private set;}
        public Transform Cooler {get;private set;}
        public bool MenuOpen {get;private set;}
        public bool NearGrill=>TryTarget(out _);
        public Vector3 CookingPoint=>Grill.position+Vector3.up*.97f;
        public string InteractionTitle=>(XRSettings.isDeviceActive?"A ":"E ")+(_c.Career.BarbecueReady?"领取香烤海鱼":"鱼箱 / 海风烧烤");
        public void Build(FishingPrototype c)
        {
            enabled=false;
            _c=c;_palette=new Material(c.PaletteShader){name="V028 coastal props",enableInstancing=true};
            _palette.SetFloat("_Gloss",.08f);_palette.SetFloat("_Unlit",.12f);_owned.Add(_palette);
            Grill=Model("Barbecue",transform).transform;Grill.name="海风烧烤架 · 从鱼箱取鱼";
            bool fitted=false;Vector3 coolerFloor=Vector3.zero;
            for(int i=0;i<16 && !fitted;i++)
            {
                // The V027 lawn slopes east of x=7.8; keep the actual feet on its level edge.
                var seed=c.Home.Origin+new Vector3(7.4f+i%4*.25f,0,-6.6f+i/4*.3f);
                if(!TryFooting(seed,new Vector2(.625f,.39f),out var floor)
                    || !TryFooting(floor+new Vector3(-1.6f,0,.05f),new Vector2(.48f,.325f),out coolerFloor))continue;
                var apron=new Vector3(floor.x,0,floor.z-1.6f);
                if(!c.Shore.Ground(apron,out apron) || Mathf.Abs(apron.y-floor.y)>.65f || !c.Shore.Clearance(apron)
                    || !c.Shore.Clearance(floor) || !c.Shore.Clearance(coolerFloor))continue;
                fitted=true;Grill.position=floor+Vector3.up*.015f;
            }
            if(!fitted)throw new InvalidOperationException("V028 barbecue has no complete dry footing beside the home.");
            var obstacle=Grill.gameObject.AddComponent<BoxCollider>();obstacle.center=new Vector3(0,.49f,0);obstacle.size=new Vector3(1.55f,.98f,.76f);
            Cooler=Model("FishCooler",transform).transform;Cooler.name="渔屋鱼箱 · 与船上鱼箱共用库存";Cooler.position=coolerFloor+Vector3.up*.015f;
            var box=Cooler.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,.3f,0);box.size=new Vector3(.85f,.6f,.56f);
            _cooked=Model("GrilledFish",Grill);_cooked.name="Whole grilled sea fish · scored skin and lemon";
            _cooked.transform.localPosition=new Vector3(0,.97f,0);_cooked.SetActive(false);
            var lamp=new GameObject("Charcoal warmth",typeof(Light));lamp.transform.SetParent(Grill,false);lamp.transform.localPosition=Vector3.up*.77f;
            _embers=lamp.GetComponent<Light>();_embers.color=new Color(1,.46f,.13f);_embers.type=LightType.Point;_embers.range=2.8f;_embers.shadows=LightShadows.None;
            var sign=new GameObject("Barbecue instructions",typeof(TextMesh));sign.transform.SetParent(Grill,false);sign.transform.localPosition=new Vector3(-.7f,1.5f,.32f);
            _sign=sign.GetComponent<TextMesh>();_sign.font=FishingUIFont.Font;_sign.fontSize=34;_sign.characterSize=.034f;_sign.anchor=TextAnchor.MiddleLeft;_sign.color=FishingUITheme.Ink;
            sign.GetComponent<MeshRenderer>().sharedMaterial=FishingUIFont.Font.material;
            RefreshFishView();enabled=true;
        }
        private bool TryFooting(Vector3 seed,Vector2 half,out Vector3 floor)
        {
            seed.y=0;if(!_c.Shore.Ground(seed,out floor))return false;
            foreach(float x in new[]{-half.x,half.x})foreach(float z in new[]{-half.y,half.y})
                if(!_c.Shore.Ground(new Vector3(floor.x+x,0,floor.z+z),out var foot) || Mathf.Abs(foot.y-floor.y)>.18f)return false;
            return true;
        }
        private GameObject Model(string name,Transform parent)
        {
            var source=Resources.Load<GameObject>("FishingV029/"+name) ?? Resources.Load<GameObject>("FishingV028/Coast/"+name);
            if(source==null)throw new InvalidOperationException("Missing V028 coast model: "+name);
            var result=Instantiate(source,parent,false);result.name=name;
            foreach(var renderer in result.GetComponentsInChildren<Renderer>()){renderer.sharedMaterial=_palette;renderer.shadowCastingMode=ShadowCastingMode.Off;}
            return result;
        }
        public bool TryTarget(out Transform root)
        {
            root=Grill;
            if(_c==null || Grill==null || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing)return false;
            var point=Grill.position+new Vector3(0,.75f,-.3f);
            if(FishingPrototype.FlatDistance(_c.Shore.Position,Grill.position)>2.65f || Mathf.Abs(_c.Shore.Position.y-Grill.position.y)>.65f)return false;
            var from=_c.Shore.Position+Vector3.up*_c.Shore.EyeHeight;var delta=point-from;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore))
                if(hit.collider!=_c.Shore.Body && !hit.transform.IsChildOf(Grill) && !hit.transform.IsChildOf(Cooler)
                    && !FishingShore.NonSolidSurface(hit.collider) && !hit.transform.IsChildOf(_c.Player.transform))return false;
            return true;
        }
        public void OpenMenu()
        {
            if(!NearGrill || _c.Paused || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.HasPendingCatch)return;
            if(XRSettings.isDeviceActive){_c.XRNotebook.Open(17);return;}
            MenuOpen=true;_c.StoryModalChanged();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void CloseMenu(){if(!MenuOpen)return;MenuOpen=false;_c.StoryModalChanged();}
        public bool TickMenuInput(Keyboard keyboard,bool focused,bool xr)
        {
            if(!MenuOpen)return false;
            if(xr || !focused || !_c.Ashore || FishingPrototype.FlatDistance(_c.Shore.Position,Grill.position)>3.2f){CloseMenu();return true;}
            if(keyboard!=null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame))CloseMenu();
            return true;
        }
        public bool StartRoasting(string id)
        {
            if(!NearGrill || _c.Paused || _c.ShopBlocksGameplay || (_c.StoryBlocksGameplay && !MenuOpen) || !_c.Career.BeginBarbecue(id))return false;
            _transfer=1;_c.RefreshBarbecueHold();_c.Save();RefreshFishView();
            _c.Notice("从鱼箱取出海鱼，放上烤架 · 炭烤18秒；收起面板后继续烤制。",6);return true;
        }
        public bool Collect()
        {
            if(!NearGrill || _c.Paused || (_c.StoryBlocksGameplay && !MenuOpen))return false;
            if(!_c.Career.CollectGrilledFish()){_c.Notice(_c.Career.BarbecueReady?"背包已满，烤鱼留在架上；整理背包后再取。":"海鱼还在烤制，收起面板，听一会儿海风。",4);return false;}
            _c.Save();RefreshFishView();_c.FieldWorld.ShowPickupPreview(FieldItem.GrilledFish);
            _c.Notice("香烤海鱼已装入背包 · 选入快捷栏后使用，恢复50%体力。",6);return true;
        }
        private void RefreshFishView()
        {
            var data=_c.Career.Barbecue;string id=data.fish?.id??"";
            if(_presentedFish!=id)
            {
                if(_raw!=null)Destroy(_raw);_raw=null;_presentedFish=id;
                if(data.fish!=null)
                {
                    _raw=FishingArt.Fish(Grill,_owned,_c.PaletteShader,data.fish.kind,data.fish.rare);
                    var filter=_raw.GetComponent<MeshFilter>();float length=filter.sharedMesh.bounds.size.z;
                    _raw.transform.localScale=Vector3.one*(.56f/Mathf.Max(.25f,length));_raw.transform.localRotation=Quaternion.Euler(0,90,90);
                    _raw.transform.localPosition=new Vector3(0,1.05f,0);
                }
            }
            bool cooking=data.fish!=null;
            if(_raw!=null)_raw.SetActive(cooking && !_c.Career.BarbecueReady);
            _cooked.SetActive(_c.Career.BarbecueReady);
            _embers.intensity=cooking?Mathf.Lerp(.35f,.55f,Mathf.Sin(Time.time*4)*.5f+.5f):.06f;
            _sign.text=cooking?(_c.Career.BarbecueReady?"香烤海鱼已熟\nE / A 领取":Mathf.CeilToInt(data.remaining)+" 秒 · 海风炭烤\n收起面板后继续"):"海风烧烤\nE / A 从鱼箱取鱼";
            _sign.color=FishingUITheme.Ink;
            _sign.gameObject.SetActive((_c.ExplorerPosition-Grill.position).sqrMagnitude<18*18);
            if(_sign.gameObject.activeSelf)_sign.transform.rotation=Quaternion.LookRotation(_sign.transform.position-_c.viewCamera.transform.position);
        }
        public void ShowSeaFind(FieldItem item)
        {
            if(_find!=null)Destroy(_find);_find=Model(item.ToString(),transform);_findAge=0;
            _findStart=_c.Ashore?_c.Shore.Position+Vector3.up*1.2f+_c.Shore.Actor.forward*.7f:_c.LandingTarget;_find.transform.position=_findStart;
            _c.Effects?.Burst(_find.transform.position,item==FieldItem.SeaTreasure?new Color(1,.78f,.3f):new Color(.65f,.8f,.8f));
        }
        private void Update()
        {
            if(_c==null)return;
            bool paused=_c.Paused || _c.StoryBlocksGameplay || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen);
            if(!paused)
            {
                var data=_c.Career.Barbecue;
                bool ready=_c.Career.TickBarbecue(Time.deltaTime);
                if(data.fish!=null && data.remaining>0)
                {_saveClock+=Time.deltaTime;if(ready || _saveClock>=2){_saveClock=0;_c.Save();if(ready)_c.Notice("渔屋飘来烤鱼香 · 海风烧烤架上的鱼已经熟了。",6);}}
                if(_transfer>0)_transfer=Mathf.Max(0,_transfer-Time.deltaTime);
                if(_find!=null)
                {
                    _findAge+=Time.deltaTime;float u=Mathf.SmoothStep(0,1,Mathf.Clamp01(_findAge/.8f));
                    var end=_c.Ashore?_c.Shore.Position+Vector3.up*1.2f+_c.Shore.Actor.forward*.7f:_c.BoatPoint(new Vector3(-.6f,1.5f,.55f));
                    _find.transform.position=Vector3.Lerp(_findStart,end,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.3f;
                    _find.transform.rotation=Quaternion.Euler(8,_c.viewCamera.transform.eulerAngles.y+_findAge*12,0);
                    if(_findAge>=4.5f){Destroy(_find);_find=null;}
                }
            }
            RefreshFishView();
            if(_raw!=null && _transfer>0)
            {
                float u=1-_transfer;
                _raw.transform.position=Vector3.Lerp(Cooler.position+Vector3.up*.7f,CookingPoint+Vector3.up*.08f,Mathf.SmoothStep(0,1,u))+Vector3.up*Mathf.Sin(u*Mathf.PI)*.35f;
            }
        }
        private void OnDisable(){if(MenuOpen)CloseMenu();}
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
