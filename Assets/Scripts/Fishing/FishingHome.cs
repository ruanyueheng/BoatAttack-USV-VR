using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>One permanent furnished home, with walkable floors and a transactional sleep transition.</summary>
    public sealed partial class FishingHome : MonoBehaviour
    {
        private FishingPrototype _c;
        private Canvas _fade;
        private Image _black;
        private float _age;
        private bool _committed;
        private Transform _walk;
        private Material _walkMaterial;
        private Terrain _shoreTerrain;
        private TerrainData _shoreOriginal,_shoreCopy;
        private GameObject _oldCliff;
        private static readonly Rect ShoreClearArea=new Rect(-166,-28,42,50);
        private Rect _changedShoreArea;
        private readonly Transform[] _interactables=new Transform[12];
        private readonly Vector3[] _interactionPoints=new Vector3[12];
        private readonly RaycastHit[] _interactionHits=new RaycastHit[64];
        private readonly System.Collections.Generic.List<Light> _lamps=new System.Collections.Generic.List<Light>();
        public bool LightsOn { get; private set; }=true;
        public Transform House { get; private set; }
        public Transform Bed { get; private set; }
        public Vector3 LighthouseLens {get;private set;}
        public Vector3 Origin { get; private set; }
        public Vector3 Entry => Origin+new Vector3(0,.22f,-6.5f);
        public Vector3 WakePoint => Origin+new Vector3(-1.6f,.22f,2.5f);
        public Vector3 Berth => new Vector3(Origin.x,0,Origin.z-26);
        public Vector3 BoardwalkStart => new Vector3(Origin.x,1.05f,Origin.z-21);
        public Vector3 StationPosition(CraftStation kind)=>Origin+new Vector3(kind==CraftStation.Workbench?1.95f:kind==CraftStation.Furnace?3.55f:5.12f,.22f,.65f);
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
        public bool NearBed => ReachInteraction(0);
        public bool CanSleep => NearBed && !_c.Paused && !_c.StoryBlocksGameplay && !_c.WorldActionBusy && !_c.HasPendingCatch
            && !_c.ShopBlocksGameplay && !_c.InventoryOpen && !_c.JournalOpen && _c.Career.Calendar.Day<FishingCalendar.MaxDay;
        public void Build(FishingPrototype c)
        {
            _c=c;
            ClearOldShore();
            // Surveyed open water west of the harbor leaves shops and fishing grounds clear.
            Origin=new Vector3(-180,2.2f,10);
            House=new GameObject("回潮渔屋").transform;House.SetParent(transform,false);House.position=Origin;
            // The authored shell is already in metres; preserve its exact floor and door coordinates.
            FishingFarmWorld.Model("CottageShell",House,Vector3.zero,false);
            foreach(string name in new[]{"HomeLand","HomeLandscaping","HomeJetty"})
            {
                var island=FishingFarmWorld.Model(name,transform,Origin,name!="HomeJetty");island.name="V035 Home "+name;
                if(name=="HomeLand")foreach(var filter in island.GetComponentsInChildren<MeshFilter>())filter.gameObject.layer=10;
                if(name=="HomeLandscaping")LighthouseLens=FishingRealmWorld.Marker(island,"LighthouseLens").position;
            }
            FishingFarmWorld.Model("Lighthouse",transform,LighthouseLens-Vector3.up*10.2f,true).name="V044 painted lighthouse";
            DressGarden();
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
            Furnish("loungeSofa",new Vector3(-3.2f,.20f,-1.8f),2.05f,180,true,out _sofa);
            Place("rugRectangle",-3.0f,-3.25f,3.4f,0,.203f,false);Place("tableCoffee",-3.0f,-3.2f,1.5f);
            Furnish("radio",new Vector3(-3,.82f,-3.2f),.34f,0,false,out var radio);
            Furnish("lampRoundFloor",new Vector3(-5.2f,.20f,-.5f),1.75f,0,true,out var lightSwitch);
            Place("kitchenCabinetDrawer",2.0f,4.2f,1.55f);Place("kitchenSink",3.55f,4.2f,1.55f);
            Place("kitchenStove",5.0f,4.2f,1.55f);Place("kitchenFridgeSmall",5.12f,2.35f,1.5f);
            Furnish("desk",new Vector3(3.65f,.20f,-3.9f),2.15f,0,true,out var chartDesk);
            Place("books",3.2f,-3.9f,.65f,0,1.0f,false);Place("lampRoundTable",4.3f,-3.9f,.60f,0,1.0f,false);
            Place("pottedPlant",5.05f,-3.6f,1.6f);Place("pottedPlant",-5.1f,-6.5f,1.3f);
            Place("coatRackStanding",1.0f,-4.1f,1.8f);
            // A tall bookcase divides the sleeping area without an impassable doorway.
            Place("bookcaseOpen",-.8f,3.1f,2.1f,90);
            FishingArt.Shape("Cottage name board",PrimitiveType.Cube,House,new Vector3(0,3.55f,-5.23f),new Vector3(3.2f,.70f,.08f),c.dark);
            FishingV019Art.Sign("回潮渔屋\n灯会为归来的人亮着",House,new Vector3(0,3.55f,-5.29f),.023f,FishingUITheme.Light);
            FishingV019Art.Sign("E 躺下休息至清晨",House,new Vector3(-3.3f,1.5f,2.7f),.014f,Color.white);
            RegisterInteraction(0,Bed,new Vector3(-1.6f,.85f,2.5f));
            RegisterInteraction(1,radio,new Vector3(-3,1.0f,-3.2f));
            RegisterInteraction(2,chartDesk,new Vector3(3.65f,1.15f,-3.9f));
            RegisterInteraction(3,lightSwitch,new Vector3(-5.2f,1.3f,-.5f));
            BuildLeisure();
            BuildStorage();
            FishingV019Art.Sign("天气收音机",House,new Vector3(-3,1.12f,-3.32f),.008f,Color.white).transform.localRotation=Quaternion.Euler(0,180,0);
            FishingV019Art.Sign("航线书桌 · 查看海图",House,new Vector3(3.65f,1.65f,-3.9f),.012f,Color.white);
            FishingV019Art.Sign("室内灯 · 开关",House,new Vector3(-5.2f,2.05f,-.5f),.012f,Color.white);
            foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})
                FishingV019Art.Sign(FishingRecipes.StationName(kind),House,StationPosition(kind)-Origin+Vector3.up*1.6f,.014f,new Color(.96f,.86f,.58f));
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
            foreach(var p in new[]{new Vector3(-4.8f,1.7f,2.7f),new Vector3(-4.8f,1.9f,-1),new Vector3(4.3f,1.7f,-3.9f),new Vector3(3.6f,2.1f,4),new Vector3(0,2.68f,-.5f),new Vector3(0,2.68f,2.5f)})
            {
                var lamp=new GameObject("Warm home practical").AddComponent<Light>();lamp.transform.SetParent(House,false);lamp.transform.localPosition=p;
                lamp.type=LightType.Point;lamp.color=new Color(1,.72f,.43f);lamp.range=7;lamp.shadows=LightShadows.None;_lamps.Add(lamp);
            }
            var post=FishingV019Art.Sign("回潮渔屋 →",transform,start+Vector3.up*1.25f,.035f,new Color(.97f,.86f,.56f));post.transform.rotation=Quaternion.Euler(0,90,0);
            var go=new GameObject("Sleep fade",typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(transform,false);
            _fade=go.GetComponent<Canvas>();_fade.sortingOrder=250;_black=go.GetComponent<Image>();_black.color=Color.clear;_black.raycastTarget=false;go.SetActive(false);
            Physics.SyncTransforms();
            _c.WorkshopWorld.EnsureHomeStations();
            HideProxySurfaces();
            RelocateClearedStations();
        }

        private void ClearOldShore()
        {
            var old=GameObject.Find("Island Level/Environment Objects/LargeCliff03");if(old==null)return;
            var area=ShoreClearArea;
            foreach(var site in _c.Shore.Sites)if(site.Available)
            {
                if(area.Contains(new Vector2(site.Landing.x,site.Landing.z)))return;
                foreach(var task in site.Tasks)if(area.Contains(new Vector2(task.x,task.z)))return;
            }
            foreach(var terrain in Terrain.activeTerrains)
            {
                var p=terrain.transform.position;var size=terrain.terrainData.size;
                if(area.xMin<p.x || area.xMax>p.x+size.x || area.yMin<p.z || area.yMax>p.z+size.z)continue;
                _shoreTerrain=terrain;_shoreOriginal=terrain.terrainData;_shoreCopy=Instantiate(_shoreOriginal);_shoreCopy.name="V027 local shore clearance";
                int resolution=_shoreCopy.heightmapResolution-1;
                int x=Mathf.FloorToInt((area.xMin-p.x)/size.x*resolution),z=Mathf.FloorToInt((area.yMin-p.z)/size.z*resolution);
                int width=Mathf.CeilToInt(area.width/size.x*resolution)+2,depth=Mathf.CeilToInt(area.height/size.z*resolution)+2;
                // The cells beside an edited sample also change through terrain interpolation.
                _changedShoreArea=Rect.MinMaxRect(p.x+(x-1)*size.x/resolution,p.z+(z-1)*size.z/resolution,
                    p.x+(x+width)*size.x/resolution,p.z+(z+depth)*size.z/resolution);
                foreach(var removed in old.GetComponentsInChildren<Collider>())
                {
                    var bounds=removed.bounds;
                    _changedShoreArea.xMin=Mathf.Min(_changedShoreArea.xMin,bounds.min.x);_changedShoreArea.xMax=Mathf.Max(_changedShoreArea.xMax,bounds.max.x);
                    _changedShoreArea.yMin=Mathf.Min(_changedShoreArea.yMin,bounds.min.z);_changedShoreArea.yMax=Mathf.Max(_changedShoreArea.yMax,bounds.max.z);
                }
                var heights=_shoreCopy.GetHeights(x,z,width,depth);float seabed=Mathf.Clamp01((-4.5f-p.y)/size.y);
                for(int row=0;row<depth;row++)for(int col=0;col<width;col++)heights[row,col]=Mathf.Min(heights[row,col],seabed);
                _shoreCopy.SetHeights(x,z,heights);terrain.terrainData=_shoreCopy;
                var collider=terrain.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=_shoreCopy;terrain.Flush();
                _oldCliff=old;old.SetActive(false);break;
            }
        }
        internal void RelocateClearedStations()
        {
            var voyage=GetComponent<FishingWreckRouteClearance>();
            if(_shoreCopy==null && (voyage==null || !voyage.Cleared))return;
            var ground=transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();bool moved=false;
            foreach(var station in _c.WorkshopWorld.State.Stations)
            {
                // A rotated 0.7 by 0.55 metre half-footprint fits inside this 0.9 metre radius.
                var footprint=new Rect(station.x-.9f,station.z-.9f,1.8f,1.8f);
                if(station.permanent || !(_shoreCopy!=null && _changedShoreArea.Overlaps(footprint)
                    || voyage!=null && voyage.Cleared && FishingWreckRouteClearance.Area.Overlaps(footprint)))continue;
                var previous=new Vector3(station.x,station.y,station.z);
                if(_c.WorkshopWorld.FitStationFoot(previous,out var support,station.yaw) && support.y<=previous.y+.03f)continue;
                bool placed=false;
                for(int cell=0;cell<256 && !placed;cell++)
                {
                    float x=-24+cell%16*3,z=-15+cell/16*3;
                    if(Mathf.Abs(x)<8.5f && z>-10 && z<7)continue;
                    if(x>8 && x<33 && z>-13 && z<20)continue;
                    if(!ground.Raycast(new Ray(Origin+new Vector3(x,35,z),Vector3.down),out var hit,50)
                        ||hit.point.y<.5f||hit.normal.y<.9f)continue;
                    placed=_c.WorkshopWorld.RelocateAfterShoreChange(station.id,hit.point);
                }
                if(!placed)throw new InvalidOperationException("No safe coastal position for saved facility "+station.id);
                moved=true;
            }
            if(moved)_c.Save();
        }
        private void RegisterInteraction(int id,Transform root,Vector3 point){_interactables[id]=root;_interactionPoints[id]=Origin+point;}
        public Transform InteractionRoot(int id)=>id>=0 && id<_interactables.Length?_interactables[id]:null;
        public Vector3 InteractionPoint(int id)=>_interactionPoints[id];
        public bool ReachInteraction(int id)
        {
            if(id>=7 && id<12)return ReachOrchard(id-7);
            var root=InteractionRoot(id);
            if(_c==null || root==null || !root.gameObject.activeInHierarchy || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing)return false;
            var point=_interactionPoints[id];
            if(FishingPrototype.FlatDistance(_c.Shore.Position,point)>2.2f || Mathf.Abs(_c.Shore.Position.y-Origin.y-.20f)>.6f)return false;
            var from=_c.Shore.Position+Vector3.up*_c.Shore.EyeHeight;var delta=point-from;
            int hits=Physics.RaycastNonAlloc(from,delta.normalized,_interactionHits,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore);
            if(hits==_interactionHits.Length)return false;
            for(int i=0;i<hits;i++)
            {var hit=_interactionHits[i];
                if(!FishingShore.NonSolidSurface(hit.collider) && hit.collider!=_c.Shore.Body && !hit.transform.IsChildOf(root)
                    && !hit.transform.IsChildOf(_c.Player.transform))return false;
            }
            return true;
        }
        public bool TryTarget(out int id,out Transform root)
        {
            id=-1;root=null;float nearest=float.MaxValue;
            for(int i=0;i<_interactables.Length;i++)if(ReachInteraction(i))
            {
                var direction=_interactionPoints[i]-_c.viewCamera.transform.position;
                float alignment=Vector3.Dot(_c.viewCamera.transform.forward,direction.normalized);
                float distance=FishingPrototype.FlatDistance(_c.Shore.Position,_interactionPoints[i])+(1-alignment)*2;
                if(distance<nearest){nearest=distance;id=i;root=_interactables[i];}
            }
            return id>=0;
        }
        public string InteractionTitle(int id)
        {
            if(id>=7 && id<12)return OrchardTitle(id-7);
            string key=XRSettings.isDeviceActive?"A ":"E ";
            switch(id)
            {
                case 0:return _c.HasPendingCatch?"请先结算鱼获，再回床休息":key+"躺下休息至清晨";
                case 1:return key+"收听今明两天天气";
                case 2:return key+"在航线书桌查看海图";
                case 3:return key+(LightsOn?"关闭":"打开")+"室内灯";
                case 4:return key+"坐在沙发上休息";
                case 5:return key+(TelevisionOn?"关闭":"打开")+"潮汐放映机";
                case 6:return XRSettings.isDeviceActive?"握住箱盖向上掀 · A 亦可打开储物箱":key+"打开家园储物箱 · 存入 / 取出";
                default:return null;
            }
        }
        public bool InteractAt(int id)
        {
            if(!ReachInteraction(id) || _c.Paused || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.InventoryOpen || _c.JournalOpen || _c.WorldActionBlocksGameplay)return false;
            switch(id)
            {
                case 0:return Sleep();
                case 1:
                    _c.Notice("回潮天气台 · 现在："+BayClimate.Name(_c.Career.Calendar.Weather)+"；明日白昼："+BayClimate.Name(FishingCalendar.Forecast(_c.Career.Calendar.Day+1)),8);return true;
                case 2:
                    if(XRSettings.isDeviceActive){_c.XRNotebook.Open(12);return true;}
                    return _c.OpenHomeChart();
                case 3:
                    LightsOn=!LightsOn;UpdateLights();_c.Notice(LightsOn?"渔屋灯光已打开":"渔屋灯光已关闭",3);return true;
                case 4:return Sit();
                case 5:ToggleTelevision();return true;
                case 6:return OpenStorage();
                default:return id>=7 && id<12 && InteractOrchard(id-7);
            }
        }
        private void Solid(string name,Vector3 p,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(House,false);go.transform.localPosition=p;go.AddComponent<BoxCollider>().size=size;}
        private void Place(string name,float x,float z,float span,float yaw=0,float y=.20f,bool solid=true)
            =>Furnish(name,new Vector3(x,y,z),span,yaw,solid,out _);
        private void Furnish(string name,Vector3 p,float span,float yaw,bool solid,out Transform root)
        {
            string authored;
            switch(name)
            {
                case "bedDouble":authored="HomeBed";break;
                case "loungeSofa":authored="HomeSofa";break;
                case "desk":authored="HomeDesk";break;
                case "bookcaseOpen":authored="HomeBookcase";break;
                case "cabinetBedDrawer":case "kitchenCabinetDrawer":authored="HomeCabinet";break;
                case "kitchenSink":authored="HomeSink";break;
                case "kitchenStove":authored="HomeStove";break;
                case "kitchenFridgeSmall":authored="HomeFridge";break;
                case "tableCoffee":authored="HomeCoffeeTable";break;
                case "rugRectangle":authored="HomeRug";break;
                default:authored=null;break;
            }
            root=FishingV019Art.Spawn(authored??name+"View",House,p,span,yaw,authored==null?"FishingV023/Home":"FishingV035");
            if(authored!=null)FishingV035Materials.Apply(root);
            if(!solid)return;
            if(name=="bedDouble")
            {
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                return;
            }
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
            BeginSleepMotion();Sleeping=true;_age=0;_committed=false;_c.XRNotebook.Close();_c.StoryModalChanged();_fade.gameObject.SetActive(true);return true;
        }
        public void Tick(float seconds)
        {
            if(!Sleeping || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<0)return;
            float before=_age;_age+=Mathf.Min(seconds,.25f);
            if((before<.85f && _age>=.85f) || (before<4.45f && _age>=4.45f))_c.WorldAudio?.NotifyBedContact(Bed.position);
            if(!_committed && _age>=3.5f){_committed=true;_c.CompleteRest();}
            float opacity=_age<3.5f?Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.65f,3.4f,_age)):1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.8f,4.45f,_age));
            _black.color=new Color(.015f,.025f,.035f,opacity);
            if(XRSettings.isDeviceActive)
            {
                _fade.renderMode=RenderMode.WorldSpace;var rect=_fade.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1920,1080);rect.localScale=Vector3.one*.002f;
                _fade.transform.SetPositionAndRotation(_c.viewCamera.transform.position+_c.viewCamera.transform.forward*.30f,_c.viewCamera.transform.rotation);
            }
            else _fade.renderMode=RenderMode.ScreenSpaceOverlay;
            if(_age>=SleepDuration)CancelSleep();
        }
        public void CancelSleep()
        {
            if(!Sleeping)return;Sleeping=false;_fade.gameObject.SetActive(false);_c.Shore.SyncRider();_c.StoryModalChanged();
            _c.View.ResetLook();
        }
        private void OnDisable(){CancelSleep();StandUp(true);ResetOrchardMotion();}
        private void UpdateLights(){bool near=(_c.viewCamera.transform.position-Origin).sqrMagnitude<90*90;foreach(var lamp in _lamps){lamp.enabled=LightsOn && near;lamp.intensity=LightsOn?(lamp.type==LightType.Spot?Mathf.Lerp(2.2f,7.5f,1-_c.Career.Calendar.Daylight):Mathf.Lerp(.9f,1.9f,1-_c.Career.Calendar.Daylight)):0;}}
        private void Update(){if(_c!=null){UpdateLights();UpdateLeisure();UpdateOrchard();UpdateStorageAnimation();}}
        private void OnDestroy()
        {
            DisposeLeisure();
            DisposeOrchard();
            if(_walkMaterial!=null)Destroy(_walkMaterial);
            if(_shoreTerrain!=null && _shoreTerrain.terrainData==_shoreCopy)
            {_shoreTerrain.terrainData=_shoreOriginal;var collider=_shoreTerrain.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=_shoreOriginal;}
            if(_shoreCopy!=null)Destroy(_shoreCopy);if(_oldCliff!=null)_oldCliff.SetActive(true);
        }
    }
}
