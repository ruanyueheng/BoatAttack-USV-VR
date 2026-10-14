using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>One permanent furnished home, with walkable floors and a transactional sleep transition.</summary>
    public sealed class FishingHome : MonoBehaviour
    {
        private FishingPrototype _c;
        private Canvas _fade;
        private Image _black;
        private float _age;
        private bool _committed;
        private Transform _walk;
        private Material _walkMaterial;
        private readonly System.Collections.Generic.List<Light> _lamps=new System.Collections.Generic.List<Light>();
        public Transform House { get; private set; }
        public Transform Bed { get; private set; }
        public Vector3 Origin { get; private set; }
        public Vector3 Entry => Origin+new Vector3(0,.22f,-6.5f);
        public Vector3 WakePoint => Origin+new Vector3(-1.6f,.22f,2.5f);
        public Vector3 Berth => new Vector3(Origin.x+3,0,Origin.z-26);
        public Vector3 BoardwalkStart => new Vector3(Origin.x,1.05f,Origin.z-21);
        public Vector3 ApproachPosition
        {
            get
            {
                var local=_walk.InverseTransformPoint(_c.Shore.Position);
                bool onWalk=Mathf.Abs(local.x)<.65f && Mathf.Abs(local.z)<.55f && Mathf.Abs(local.y*_walk.lossyScale.y)<.6f;
                return Inside || onWalk?Entry:BoardwalkStart;
            }
        }
        public bool Inside => _c.Ashore && Mathf.Abs(_c.Shore.Position.x-Origin.x)<5.9f && Mathf.Abs(_c.Shore.Position.z-Origin.z)<4.9f && Mathf.Abs(_c.Shore.Position.y-Origin.y-.20f)<.6f;
        public bool Sleeping { get; private set; }
        public bool NearBed => _c.Ashore && !_c.Shore.Swimming && !_c.Shore.Climbing
            && FishingPrototype.FlatDistance(_c.Shore.Position,WakePoint)<2.2f && Mathf.Abs(_c.Shore.Position.y-WakePoint.y)<.6f;
        public bool CanSleep => NearBed && !_c.Paused && !_c.StoryBlocksGameplay && !_c.WorldActionBusy && !_c.HasPendingCatch
            && !_c.ShopBlocksGameplay && !_c.InventoryOpen && !_c.JournalOpen && _c.Career.Calendar.Day<FishingCalendar.MaxDay;
        public void Build(FishingPrototype c)
        {
            _c=c;
            // Surveyed open water west of the harbor leaves shops and fishing grounds clear.
            Origin=new Vector3(-180,2.2f,10);
            House=new GameObject("回潮渔屋").transform;House.SetParent(transform,false);House.position=Origin;
            // The authored shell is already in metres; preserve its exact floor and door coordinates.
            var shell=Resources.Load<GameObject>("FishingV023/Home/HouseShellView");
            if(shell==null)throw new InvalidOperationException("Missing V023 home art; run FishingV023Assets.Build.");
            UnityEngine.Object.Instantiate(shell,House,false);
            Solid("Home floor",new Vector3(0,.10f,0),new Vector3(12,.20f,10));
            Solid("Porch floor",new Vector3(0,.10f,-6.5f),new Vector3(13,.20f,3));
            foreach(float side in new[]{-1f,1f})
            {
                Solid("Side wall",new Vector3(side*6,1.70f,0),new Vector3(.18f,3.10f,10));
                Solid("Front wall",new Vector3(side*3.75f,1.70f,-5),new Vector3(4.5f,3.10f,.18f));
            }
            Solid("Rear wall",new Vector3(0,1.70f,5),new Vector3(12,3.10f,.18f));
            Solid("Door lintel",new Vector3(0,3,-5),new Vector3(3.0f,.22f,.2f));
            Furnish("bedDouble",new Vector3(-3.3f,.20f,2.7f),3.0f,0,true,out var bed);Bed=bed;
            Place("cabinetBedDrawer",-5.0f,2.7f,1.15f);Place("lampRoundTable",-5.0f,2.7f,.60f,0,1.07f,false);
            Place("bookcaseOpen",-5.20f,4.2f,2.2f,90);Place("books",-4.6f,4.2f,.6f,0,.20f,false);
            Place("loungeSofa",-3.2f,-1.8f,3.1f,180);Place("loungeChair",-5.1f,-3.3f,1.45f,100);
            Place("rugRectangle",-3.0f,-3.25f,3.4f,0,.203f,false);Place("tableCoffee",-3.0f,-3.2f,1.5f);
            Place("radio",-3.0f,-3.2f,.34f,0,.82f,false);Place("lampRoundFloor",-5.2f,-.5f,1.75f);
            Place("kitchenCabinetDrawer",2.0f,4.2f,1.55f);Place("kitchenSink",3.55f,4.2f,1.55f);
            Place("kitchenStove",5.0f,4.2f,1.55f);Place("kitchenFridgeSmall",5.12f,2.35f,1.5f);
            Place("tableRound",2.7f,1.2f,1.6f);Place("chair",1.5f,1.2f,1.0f,90);Place("chair",3.8f,1.2f,1.0f,-90);
            Place("desk",3.65f,-3.9f,2.15f);Place("chairDesk",3.65f,-2.65f,1.18f,180);
            Place("books",3.2f,-3.9f,.65f,0,1.0f,false);Place("lampRoundTable",4.3f,-3.9f,.60f,0,1.0f,false);
            Place("pottedPlant",5.05f,-3.6f,1.6f);Place("pottedPlant",-5.1f,-6.5f,1.3f);
            Place("coatRackStanding",1.0f,-4.1f,1.8f);Place("bathroomSink",5.1f,.0f,1.15f,90);Place("toilet",5.1f,-1.1f,1.0f,90);
            // A tall bookcase divides the sleeping area without an impassable doorway.
            Place("bookcaseOpen",-.8f,3.1f,2.1f,90);
            FishingV019Art.Sign("回潮渔屋\n灯会为归来的人亮着",House,new Vector3(0,2.66f,-5.20f),.023f,new Color(.96f,.86f,.58f));
            FishingV019Art.Sign("E 躺下休息至清晨",House,new Vector3(-3.3f,1.5f,2.7f),.014f,Color.white);
            var start=BoardwalkStart;var end=Origin+new Vector3(0,.20f,-8);
            _walkMaterial=new Material(c.PaletteShader){name="V023 boardwalk cedar"};_walkMaterial.SetColor("_BaseColor",new Color(.54f,.34f,.18f));_walkMaterial.SetFloat("_Gloss",.05f);
            FishingArt.Shape("Home foundation",PrimitiveType.Cube,House,new Vector3(0,-1.15f,0),new Vector3(12,2.5f,10),_walkMaterial,true);
            FishingArt.Shape("Porch foundation",PrimitiveType.Cube,House,new Vector3(0,-1.15f,-6.5f),new Vector3(13,2.5f,3),_walkMaterial,true);
            FishingArt.Shape("Home landing foundation",PrimitiveType.Cube,transform,BoardwalkStart-Vector3.up*.73f,new Vector3(4,1.3f,3),_walkMaterial,true);
            FishingArt.Shape("Home boarding platform",PrimitiveType.Cube,transform,BoardwalkStart-Vector3.up*.08f,new Vector3(4,.16f,3),_walkMaterial,true);
            var delta=end-start;var walk=FishingArt.Shape("Home boardwalk",PrimitiveType.Cube,transform,(start+end)*.5f,new Vector3(2.6f,.14f,delta.magnitude+.2f),_walkMaterial,true);
            walk.transform.rotation=Quaternion.LookRotation(delta,Vector3.up);
            _walk=walk.transform;
            var support=FishingArt.Shape("Boardwalk foundation",PrimitiveType.Cube,transform,walk.transform.position-walk.transform.up*1.27f,new Vector3(2.6f,2.4f,delta.magnitude+.2f),_walkMaterial,true);
            support.transform.rotation=walk.transform.rotation;
            foreach(float side in new[]{-1f,1f})
            {
                var rail=FishingArt.Shape("Boardwalk handrail",PrimitiveType.Cube,transform,(start+end)*.5f+walk.transform.right*(side*1.32f)+Vector3.up*.90f,new Vector3(.09f,.10f,delta.magnitude),_walkMaterial,true);rail.transform.rotation=walk.transform.rotation;
                for(int j=0;j<=10;j++)FishingArt.Shape("Boardwalk baluster",PrimitiveType.Cube,transform,Vector3.Lerp(start,end,j/10f)+walk.transform.right*(side*1.32f)+Vector3.up*.45f,new Vector3(.075f,.90f,.075f),_walkMaterial,true);
            }
            foreach(var p in new[]{new Vector3(-4.8f,1.7f,2.7f),new Vector3(-4.8f,1.9f,-1),new Vector3(4.3f,1.7f,-3.9f),new Vector3(3.6f,2.1f,4)})
            {
                var lamp=new GameObject("Warm home practical").AddComponent<Light>();lamp.transform.SetParent(House,false);lamp.transform.localPosition=p;
                lamp.type=LightType.Point;lamp.color=new Color(1,.72f,.43f);lamp.range=7;lamp.shadows=LightShadows.None;_lamps.Add(lamp);
            }
            var post=FishingV019Art.Sign("回潮渔屋 →",transform,start+Vector3.up*1.25f,.035f,new Color(.97f,.86f,.56f));post.transform.rotation=Quaternion.Euler(0,90,0);
            var go=new GameObject("Sleep fade",typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(transform,false);
            _fade=go.GetComponent<Canvas>();_fade.sortingOrder=250;_black=go.GetComponent<Image>();_black.color=Color.clear;_black.raycastTarget=false;go.SetActive(false);
            Physics.SyncTransforms();
        }
        private void Solid(string name,Vector3 p,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(House,false);go.transform.localPosition=p;go.AddComponent<BoxCollider>().size=size;}
        private void Place(string name,float x,float z,float span,float yaw=0,float y=.20f,bool solid=true)
            =>Furnish(name,new Vector3(x,y,z),span,yaw,solid,out _);
        private void Furnish(string name,Vector3 p,float span,float yaw,bool solid,out Transform root)
        {
            root=FishingV019Art.Spawn(name+"View",House,p,span,yaw,"FishingV023/Home");
            if(!solid)return;
            var bounds=new Bounds();bool first=true;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var q=root.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                    if(first){bounds=new Bounds(q,Vector3.zero);first=false;}else bounds.Encapsulate(q);
                }
            }
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
        }
        public bool Sleep()
        {
            if(!CanSleep)return false;
            Sleeping=true;_age=0;_committed=false;_c.XRNotebook.Close();_c.StoryModalChanged();_fade.gameObject.SetActive(true);return true;
        }
        public void Tick(float seconds)
        {
            if(!Sleeping || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<0)return;
            _age+=Mathf.Min(seconds,.25f);
            if(!_committed && _age>=1.2f){_committed=true;_c.CompleteRest();}
            float opacity=_age<1.2f?Mathf.Clamp01(_age/1.1f):1-Mathf.Clamp01((_age-1.4f)/1.0f);
            _black.color=new Color(.015f,.025f,.035f,opacity);
            if(XRSettings.isDeviceActive)
            {
                _fade.renderMode=RenderMode.WorldSpace;var rect=_fade.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1920,1080);rect.localScale=Vector3.one*.002f;
                _fade.transform.SetPositionAndRotation(_c.viewCamera.transform.position+_c.viewCamera.transform.forward*.30f,_c.viewCamera.transform.rotation);
            }
            else _fade.renderMode=RenderMode.ScreenSpaceOverlay;
            if(_age>=2.5f)CancelSleep();
        }
        public void CancelSleep()
        {
            if(!Sleeping)return;Sleeping=false;_fade.gameObject.SetActive(false);_c.StoryModalChanged();
            _c.View.ResetLook();
        }
        private void OnDisable()=>CancelSleep();
        private void Update(){foreach(var lamp in _lamps)lamp.intensity=Mathf.Lerp(.45f,1.3f,1-_c.Career.Calendar.Daylight);}
        private void OnDestroy(){if(_walkMaterial!=null)Destroy(_walkMaterial);}
    }
}
