using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingWorkshopWorld : MonoBehaviour
    {
        public sealed class ResourceNode {public int Id;public FieldItem Item;public Transform Root;public Vector3 Feet;}
        private FishingPrototype _c;
        private readonly List<ResourceNode> _nodes=new List<ResourceNode>();
        private readonly Dictionary<int,Transform> _stations=new Dictionary<int,Transform>();
        private readonly Dictionary<int,Light> _fires=new Dictionary<int,Light>();
        private readonly Dictionary<FieldItem,Mesh> _meshes=new Dictionary<FieldItem,Mesh>();
        private Material _palette;private float _senseAt,_saveAt;
        private ResourceNode _nearNode;private int _nearStation=-1;
        public IReadOnlyList<ResourceNode> Nodes=>_nodes;
        public int SelectedStation { get; private set; }=-1;
        public FishingWorkshop State=>_c.Career.Workshop;
        public CraftStation Context
        {
            get {var s=State.Station(SelectedStation);return s!=null && ReachStation(s.id)?s.kind:CraftStation.Hand;}
        }
        public string ActionTitle
        {
            get
            {
                if(StationIsClosest){var s=State.Station(_nearStation);return "E 打开"+FishingRecipes.StationName(s.kind)+(s.ready>0?" · 有成品待领取":"");}
                if(_nearNode==null)return null;
                return State.ResourceReady(_nearNode.Id)?(_nearNode.Item==FieldItem.Coal?"E 开采山脚煤矿":"E 采集 "+FishingBackpack.Name(_nearNode.Item))+(NeedsPick(_nearNode.Item)?" · 需手持石镐 / 铁镐":"")
                    :FishingBackpack.Name(_nearNode.Item)+" · "+State.ResourceWait(_nearNode.Id).ToString("0")+" 秒后恢复";
            }
        }
        public void Build(FishingPrototype c)
        {
            _c=c;_palette=new Material(c.PaletteShader){name="Original coastal workshop palette",enableInstancing=true};
            RefreshFromCareer();
            var types=new[]{FieldItem.Driftwood,FieldItem.Stone,FieldItem.Fiber,FieldItem.Clay,FieldItem.IronOre,FieldItem.Coal,FieldItem.Sand,FieldItem.Stone};
            for(int site=0;site<c.Shore.Sites.Length;site++)
            {
                if(!c.Shore.Sites[site].Available)continue;
                for(int j=0;j<16;j++)
                {
                    int id=site*16+j;var item=types[j%8];
                    Vector3 feet;
                    if(item==FieldItem.Coal)
                    {if(!FindCoalFoot(c.Shore.Sites[site].Landing,j,out feet)){Debug.LogWarning("[COAL] No safe mountain-edge vein at site "+site+" slot "+j);continue;}}
                    else if(!FindResourceFoot(c.Shore.Sites[site].Landing,j,out feet))continue;
                    var root=MeshObject("Resource "+id+" / "+(item==FieldItem.Coal?"山脚煤矿":FishingBackpack.Name(item)),item);root.position=feet;
                    root.localRotation=Quaternion.Euler(0,id*47,0);_nodes.Add(new ResourceNode{Id=id,Item=item,Feet=feet,Root=root});
                }
            }
            Debug.Log("[WORKSHOP] "+_nodes.Count+" renewable resources; "+_stations.Count+" saved stations.");
        }
        public void RefreshFromCareer()
        {
            foreach(var station in _stations.Values){station.gameObject.SetActive(false);Destroy(station.gameObject);}
            _stations.Clear();_fires.Clear();SelectedStation=-1;_nearStation=-1;_nearNode=null;
            foreach(var station in State.Stations)if(!_stations.ContainsKey(station.id))Spawn(station);
            Physics.SyncTransforms();
            foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})EnsurePublicStation(kind);
            if(_c.Shore.Sites[0].Available && new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge}.All(HasPublicStation))State.FinishInitialization();
            EnsureHomeStations();
        }
        private bool HasPublicStation(CraftStation kind)=>State.Stations.Any(s=>s.kind==kind && s.permanent
            && FishingPrototype.FlatDistance(new Vector3(s.x,s.y,s.z),_c.Shore.Sites[0].Landing)<45f);
        private void EnsurePublicStation(CraftStation kind)
        {
            if(!_c.Shore.Sites[0].Available || HasPublicStation(kind))return;
            var landing=_c.Shore.Sites[0].Landing;
            // Retry each missing facility on every load; old initialization could commit after a failed search.
            for(int ring=2;ring<=24;ring+=2)for(int step=0;step<36;step++)
            {
                var probe=landing+Quaternion.Euler(0,step*10,0)*Vector3.forward*ring;probe.y=0;
                if(!_c.Shore.Ground(probe,out var feet) || Mathf.Abs(feet.y-landing.y)>3f || !_c.Shore.Clearance(feet))continue;
                if(_c.Shore.Sites.Any(s=>s.Available && FishingPrototype.FlatDistance(feet,s.Berth)<6f))continue;
                if(State.Stations.Any(s=>s.permanent && FishingPrototype.FlatDistance(feet,new Vector3(s.x,s.y,s.z))<3.5f))continue;
                if(!FitStationFoot(feet,out feet) || BlocksAccess(feet) || !_c.Shore.SafeCoastalFooting(feet) || !StationSpaceClear(feet))continue;
                if(_nodes.Any(n=>FishingPrototype.FlatDistance(feet,n.Feet)<3f))continue;
                if(!_c.Shore.Ground(feet+Vector3.back*1.25f,out var apron) || Mathf.Abs(apron.y-feet.y)>.4f || !_c.Shore.Clearance(apron))continue;
                int id=State.Place(null,kind,feet.x,feet.y+.02f,feet.z,0,true);
                if(id<0)return;Spawn(State.Station(id));Physics.SyncTransforms();return;
            }
            Debug.LogWarning("[WORKSHOP] No safe public "+kind+" footing found; placement will retry on the next load.");
        }
        public void EnsureHomeStations()
        {
            if(_c.Home==null || _c.Home.House==null)return;
            foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})
            {
                var feet=_c.Home.StationPosition(kind);
                var existing=State.Stations.FirstOrDefault(s=>s.kind==kind && s.permanent && Vector3.Distance(new Vector3(s.x,s.y,s.z),feet)<.2f);
                if(existing!=null){var root=StationRoot(existing.id);if(root!=null)root.SetParent(_c.Home.House,true);continue;}
                int id=State.Place(null,kind,feet.x,feet.y,feet.z,0,true);
                if(id<0){Debug.LogWarning("[WORKSHOP] Could not install home "+kind);continue;}
                Spawn(State.Station(id));
            }
            Physics.SyncTransforms();
        }
        private bool FindResourceFoot(Vector3 seed,int index,out Vector3 feet)
        {
            feet=seed;
            for(int attempt=0;attempt<12;attempt++)
            {
                if(!_c.FieldWorld.TryLocation(seed,3+(index/4)*2+attempt*.4f,index*137.5f+attempt*31,out feet))continue;
                bool apart=true;foreach(var n in _nodes)if(Vector3.Distance(feet,n.Feet)<1.1f){apart=false;break;}
                // Leave a clear interaction apron around benches/furnaces. A resource must
                // not steal E while the player is standing at the furnace door.
                foreach(var station in _stations.Values)if(Vector3.Distance(feet,station.position)<3f){apart=false;break;}
                foreach(var pickup in _c.FieldWorld.Pickups)if(Vector3.Distance(feet,pickup.Feet)<1.2f){apart=false;break;}
                foreach(var site in _c.Shore.Sites)foreach(var task in site.Tasks)if(Vector3.Distance(feet,task)<1.1f){apart=false;break;}
                if(apart && _c.Shore.SafeCoastalFooting(feet))return true;
            }
            return false;
        }
        private Transform MeshObject(string name,FieldItem item)
        {
            if(!_meshes.TryGetValue(item,out var mesh)){mesh=FishingWorkshopArt.Make(item);_meshes.Add(item,mesh);}
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=_palette;return go.transform;
        }
        private void Spawn(WorkshopStationData s)
        {
            var root=MeshObject(FishingRecipes.StationName(s.kind)+" #"+s.id,FishingRecipes.StationItem(s.kind));
            root.SetPositionAndRotation(new Vector3(s.x,s.y,s.z),Quaternion.Euler(0,s.yaw,0));
            if(_c.Home!=null && s.permanent && Vector3.Distance(root.position,_c.Home.StationPosition(s.kind))<.2f)root.SetParent(_c.Home.House,true);
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.up*.5f;collider.size=new Vector3(1.3f,1,.95f);
            _stations.Add(s.id,root);
            if(s.permanent && FishingPrototype.FlatDistance(root.position,_c.Shore.Sites[0].Landing)<45)
            {
                var sign=new GameObject("West bay facility sign",typeof(TextMesh));sign.transform.SetParent(root,false);
                sign.transform.localPosition=new Vector3(0,1.75f,-.05f);sign.transform.localRotation=Quaternion.Euler(0,180,0);
                var label=sign.GetComponent<TextMesh>();label.font=FishingUIFont.Font;label.fontSize=40;label.characterSize=.023f;
                label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=FishingUITheme.Ink;
                FishingXRText.Bind(label,FishingRecipes.StationName(s.kind)+"\nE 制作 / 加工");
                sign.GetComponent<MeshRenderer>().sharedMaterial=FishingUIFont.Font.material;
            }
            if(s.kind==CraftStation.Forge)
            {
                collider.center=Vector3.up*.65f;collider.size=new Vector3(1.3f,1.3f,.95f);
                var ember=new GameObject("Forge ember glow",typeof(Light)).GetComponent<Light>();ember.transform.SetParent(root,false);
                ember.transform.localPosition=new Vector3(.42f,.92f,.08f);ember.type=LightType.Point;ember.color=new Color(1,.34f,.08f);ember.range=1.6f;ember.intensity=.45f;ember.shadows=LightShadows.None;
            }
            if(s.kind==CraftStation.Furnace)
            {
                var light=new GameObject("Furnace embers",typeof(Light)).GetComponent<Light>();light.transform.SetParent(root,false);light.transform.localPosition=new Vector3(0,.35f,-.45f);
                light.color=new Color(1,.33f,.05f);light.range=2.6f;light.intensity=0;light.shadows=LightShadows.None;_fires.Add(s.id,light);
                var coals=FishingArt.Shape("Hot coals",PrimitiveType.Sphere,root,new Vector3(0,.24f,-.2f),new Vector3(.42f,.07f,.4f),_c.orange);coals.SetActive(false);
            }
        }
        private bool Reach(Vector3 target,Transform ignore=null)
        {
            if(!_c.Ashore || Vector3.Distance(_c.Shore.Actor.position,target)>2.8f)return false;
            var from=_c.Shore.Actor.position+Vector3.up*_c.Shore.EyeHeight;var d=target+Vector3.up*.7f-from;
            foreach(var h in Physics.RaycastAll(from,d.normalized,d.magnitude,~(1<<4),QueryTriggerInteraction.Ignore))
                if(!FishingShore.NonSolidSurface(h.collider) && h.collider!=_c.Shore.Body && !h.transform.IsChildOf(_c.Player.transform) && (ignore==null || !h.transform.IsChildOf(ignore)))return false;
            return true;
        }
        public bool ReachStation(int id)=>_stations.TryGetValue(id,out var root) && root!=null && root.gameObject.activeInHierarchy && Reach(root.position,root);
        private bool StationIsClosest => _nearStation>=0 && _stations.TryGetValue(_nearStation,out var root) && root!=null && (_nearNode==null ||
            Vector3.Distance(_c.Shore.Actor.position,root.position)<=Vector3.Distance(_c.Shore.Actor.position,_nearNode.Feet));
        public void Sense()
        {
            _nearNode=null;_nearStation=-1;if(!_c.Ashore)return;float closest=3;
            foreach(var s in _stations)if(ReachStation(s.Key))
            {float d=Vector3.Distance(_c.Shore.Actor.position,s.Value.position);if(d<closest){closest=d;_nearStation=s.Key;}}
            closest=3;
            foreach(var node in _nodes)if(node.Root!=null && node.Root.gameObject.activeInHierarchy && Reach(node.Feet))
            {float d=Vector3.Distance(_c.Shore.Actor.position,node.Feet);if(d<closest){closest=d;_nearNode=node;}}
        }
        private void Update()
        {
            if(_c==null || _c.Paused || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen))return;
            bool progressing=State.HasTimedWork;
            bool done=State.Tick(Time.deltaTime);
            if(Time.time>=_senseAt)
            {
                Sense();_senseAt=Time.time+.15f;
                foreach(var n in _nodes)if(n.Root!=null)n.Root.localScale=Vector3.one*(State.ResourceReady(n.Id)?1:.32f);
                foreach(var fire in _fires){bool burning=State.Station(fire.Key).remaining>0;fire.Value.intensity=burning?1.8f:0;var coals=_stations[fire.Key].Find("Hot coals");if(coals!=null)coals.gameObject.SetActive(burning);}
                foreach(var station in _stations.Values)
                {
                    var sign=station.Find("West bay facility sign");if(sign==null)continue;
                    sign.gameObject.SetActive((_c.ExplorerPosition-sign.position).sqrMagnitude<32*32);
                    if(sign.gameObject.activeSelf){sign.rotation=Quaternion.LookRotation(sign.position-_c.viewCamera.transform.position);sign.GetComponent<TextMesh>().color=FishingUITheme.Ink;}
                }
            }
            if(done || (progressing && Time.time>=_saveAt)){_saveAt=Time.time+10;_c.Save();}
        }
        public bool Interact()
        {
            if(_c.Paused)return false;Sense();
            if(StationIsClosest){SelectedStation=_nearStation;_c.OpenCrafting();return true;}
            if(_nearNode!=null){HarvestNearest();return true;}return false;
        }
        public void SelectNearby(){Sense();SelectedStation=_nearStation;}
        private static bool NeedsPick(FieldItem item)=>item==FieldItem.IronOre || item==FieldItem.Coal;
        public Transform StationRoot(int id)=>_stations.TryGetValue(id,out var root)?root:null;
        internal bool RelocateAfterShoreChange(int id,Vector3 seed)
        {
            var station=State.Station(id);var root=StationRoot(id);
            if(station==null || station.permanent || root==null || !FitStationFoot(seed,out var feet,station.yaw)
                || !StationSpaceClear(feet,station.yaw) || BlocksAccess(feet))return false;
            feet.y+=.02f;if(!State.Relocate(id,feet.x,feet.y,feet.z,station.yaw))return false;
            root.position=feet;Physics.SyncTransforms();return true;
        }
        public bool TryInteractionTarget(out int stationId,out ResourceNode resource,bool refresh=true)
        {
            if(refresh)Sense();stationId=StationIsClosest?_nearStation:-1;resource=stationId<0?_nearNode:null;
            return stationId>=0 || resource!=null;
        }
        public bool TryHarvestTarget(out ResourceNode resource){Sense();resource=_nearNode;return resource!=null;}
        public bool TryHarvestTarget(Vector3 from,float reach,out ResourceNode resource)
        {
            resource=null;float nearest=reach;
            if(!_c.Ashore || _c.Paused)return false;
            foreach(var node in _nodes)
            {
                if(!ResourceContact(node,from,reach,out float distance) || distance>=nearest)continue;
                resource=node;nearest=distance;
            }
            return resource!=null;
        }
        private bool ResourceContact(ResourceNode node,Vector3 from,float reach,out float distance)
        {
            distance=float.MaxValue;
            if(node==null || node.Root==null || !node.Root.gameObject.activeInHierarchy || Vector3.Distance(_c.Shore.Actor.position,node.Feet)>2.8f)return false;
            var renderer=node.Root.GetComponent<Renderer>();if(renderer!=null && renderer.bounds.SqrDistance(from)>reach*reach)return false;
            var contact=FishingContactGeometry.ClosestPoint(node.Root,from);distance=Vector3.Distance(from,contact);
            return distance<=reach && _c.Shore.ClearSight(from,contact,node.Root) && _c.Shore.ClearSight(_c.viewCamera.transform.position,contact,node.Root);
        }
        private bool TrackedResourceContact(ResourceNode node,FieldItem tool)
        {
            if(!_c.Hands.TryControllerPalm(false,out var right,out _) || !_c.Hands.TryControllerPalm(true,out var left,out _)
                || !Application.isFocused || _c.XRSeat.HeadBlocked || _c.XRNotebook.BlocksGameplay || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.WorldActionBusy)return false;
            if(!FishingBackpack.IsGatherTool(tool))return ResourceContact(node,right,FishingXRGrab.Reach,out _) || ResourceContact(node,left,FishingXRGrab.Reach,out _);
            var held=_c.FieldWorld.HeldTool;
            return held!=null && held.gameObject.activeInHierarchy && _c.FieldWorld.PresentedItem==tool
                && ResourceContact(node,held.TransformPoint(FishingFieldWorld.WorkToolTip(tool)),.10f,out _)
                && _c.Shore.ClearSight(held.position,held.TransformPoint(FishingFieldWorld.WorkToolTip(tool)),node.Root);
        }
        public bool OpenStation(int id)
        {
            if(_c.Paused || !ReachStation(id))return false;
            SelectedStation=id;_c.OpenCrafting();return true;
        }
        public bool HarvestNode(int id,FieldItem expectedItem,FieldItem expectedTool)
        {
            if(_c.Paused || !_c.Ashore || _c.Career.Backpack.Equipped!=expectedTool)return false;
            ResourceNode node=null;foreach(var candidate in _nodes)if(candidate.Id==id){node=candidate;break;}
            if(node==null || node.Item!=expectedItem || node.Root==null || !node.Root.gameObject.activeInHierarchy)return false;
            if(UnityEngine.XR.XRSettings.isDeviceActive && !_c.Automated){if(!TrackedResourceContact(node,expectedTool))return false;}
            else if(!Reach(node.Feet))return false;
            bool pick=expectedTool==FieldItem.StonePickaxe || expectedTool==FieldItem.IronPickaxe;
            if(NeedsPick(node.Item) && !pick){_c.Notice(UnityEngine.XR.XRSettings.isDeviceActive?"矿石需要石镐或铁镐：B 查看制作配方，Y 换装备；镐尖贴近矿石后按 A 或右扳机。":"矿石需要手持石镐或铁镐：I 查看配方，1–9 装备。",4);return false;}
            if(!FishingBackpack.ToolMatchesResource(expectedTool,node.Item))return false;
            int amount=(expectedTool==FieldItem.Axe || expectedTool==FieldItem.Shovel)?4:expectedTool==FieldItem.IronPickaxe && (node.Item==FieldItem.Stone || NeedsPick(node.Item))?4:3;
            if(!State.Harvest(_c.Career.Backpack,node.Id,node.Item,amount)){_c.Notice(State.ResourceReady(node.Id)?"背包已满，资源没有消耗。":"这处资源正在恢复，换一处采集。",3);return false;}
            _c.ActionAudio?.Harvest(node.Item,node.Root.position+Vector3.up*.3f);
            _c.Effects?.Burst(node.Root.position+Vector3.up*.3f,new Color(.8f,.75f,.5f));_c.Save();_c.Notice("采集 "+FishingBackpack.Name(node.Item)+" ×"+amount+(UnityEngine.XR.XRSettings.isDeviceActive?" · B → 制作":" · I → 制作"),4);return true;
        }
        public bool HarvestNearest()
        {
            if(_c.Paused || !_c.Ashore || !TryHarvestTarget(out var node)){_c.Notice(UnityEngine.XR.XRSettings.isDeviceActive?"靠近可采资源，将右手或镐尖贴近目标后按 A。":"靠近可采集的木堆、草丛或矿石，再按 E。",3);return false;}
            return HarvestNode(node.Id,node.Item,_c.Career.Backpack.Equipped);
        }
        private bool BlocksAccess(Vector3 feet)
        {
            var shop=_c.TackleShop;
            if(shop!=null && shop.Available)
            {
                var p=shop.transform.InverseTransformPoint(feet);
                float deckZ=shop.transform.InverseTransformPoint(shop.LandingPosition).z;
                if((Mathf.Abs(p.x)<7 && p.z>-8 && p.z<6) || (Mathf.Abs(p.x)<3.2f && p.z>deckZ-4 && p.z<-7))return true;
            }
            foreach(var site in _c.Shore.Sites)if(site.Available)
            {
                // 保护整条栈桥，而不仅是岸端的一个点；公共设施同样遵守此限制。
                var path=Vector3.ProjectOnPlane(site.Landing-site.Berth,Vector3.up);
                float t=Mathf.Clamp01(Vector3.Dot(feet-site.Berth,path)/Mathf.Max(.001f,path.sqrMagnitude));
                if(FishingPrototype.FlatDistance(feet,site.Berth+path*t)<2)return true;
                foreach(var task in site.Tasks)if(Vector3.Distance(feet,task)<2)return true;
            }
            return false;
        }
        internal bool FitStationFoot(Vector3 seed,out Vector3 feet,float yaw=0)
        {
            feet=seed;float low=seed.y,high=seed.y;
            foreach(var offset in new[]{new Vector3(-.7f,0,-.55f),new Vector3(.7f,0,-.55f),new Vector3(-.7f,0,.55f),new Vector3(.7f,0,.55f)})
            {
                if(!_c.Shore.Ground(seed+Quaternion.Euler(0,yaw,0)*offset,out var floor))return false;
                low=Mathf.Min(low,floor.y);high=Mathf.Max(high,floor.y);
            }
            if(high-low>.3f)return false;
            feet.y=high;return true;
        }
        private static bool StationSpaceClear(Vector3 feet,float yaw=0)
        {
            foreach(var collider in Physics.OverlapBox(feet+Vector3.up*.75f,new Vector3(.72f,.65f,.57f),Quaternion.Euler(0,yaw,0),~(1<<4),QueryTriggerInteraction.Ignore))
                if(!FishingShore.NonSolidSurface(collider))return false;
            return true;
        }
        private LineRenderer _placementPreview;
        public void SetPlacementPreview(Vector3 feet,float yaw,bool visible)
        {
            if(!visible){if(_placementPreview!=null)_placementPreview.gameObject.SetActive(false);return;}
            if(_placementPreview==null)
            {
                var go=new GameObject("设施落位轮廓预览");go.transform.SetParent(transform,false);
                _placementPreview=go.AddComponent<LineRenderer>();_placementPreview.useWorldSpace=false;_placementPreview.loop=true;
                _placementPreview.positionCount=4;_placementPreview.startWidth=_placementPreview.endWidth=.025f;_placementPreview.sharedMaterial=_c.teal;
                _placementPreview.SetPositions(new[]{new Vector3(-.7f,0,-.55f),new Vector3(.7f,0,-.55f),new Vector3(.7f,0,.55f),new Vector3(-.7f,0,.55f)});
            }
            _placementPreview.gameObject.SetActive(true);_placementPreview.transform.SetPositionAndRotation(feet+Vector3.up*.04f,Quaternion.Euler(0,yaw,0));
        }
        public bool TryPlacement(float forwardDistance,out Vector3 feet,out float yaw)
        {
            feet=Vector3.zero;yaw=0;if(_c.Paused || !_c.Ashore)return false;
            var item=_c.Career.Backpack.Equipped;if(!FishingRecipes.IsStation(item))return false;
            var forward=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,Vector3.up).normalized;
            yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            var seed=_c.Shore.Actor.position+forward*forwardDistance;
            if(!_c.Shore.Ground(seed,out feet) || !ValidPlacement(feet,yaw,out feet))return false;
            return true;
        }
        private bool ValidPlacement(Vector3 seed,float yaw,out Vector3 feet)
        {
            feet=seed;
            if(!FitStationFoot(seed,out feet,yaw) || Mathf.Abs(feet.y-_c.Shore.Actor.position.y)>.45f || !_c.Shore.SafeCoastalFooting(feet) || !_c.Shore.CanSeeTask(feet))
            {_c.Notice("前方需要平整、可见、干燥的岸面。",3);return false;}
            if(!StationSpaceClear(feet,yaw)){_c.Notice("放置空间被占用，请转向空地。",3);return false;}
            if(BlocksAccess(feet)){_c.Notice("请避开登岸通道、商店与勘测台。",3);return false;}
            return true;
        }
        public bool PlaceAt(Vector3 feet,float yaw,FieldItem expectedItem)
        {
            if(_c.Paused || !_c.Ashore || _c.Career.Backpack.Equipped!=expectedItem || !FishingRecipes.IsStation(expectedItem))return false;
            if(!_c.Shore.Ground(feet,out var ground) || !ValidPlacement(ground,yaw,out var checkedFeet) || Vector3.Distance(checkedFeet,feet)>.1f)return false;
            int id=State.Place(_c.Career.Backpack,FishingRecipes.StationKind(expectedItem),feet.x,feet.y+.02f,feet.z,yaw);
            if(id<0){_c.Notice("无法放置：自建设施已达 "+FishingWorkshop.MaxPlayerStations+" 个上限或物品不足。",3);return false;}
            Spawn(State.Station(id));Physics.SyncTransforms();_c.WorldAudio?.Play(expectedItem==FieldItem.Workbench?"ImpactWood":"ImpactStone",feet,.5f);
            _c.Save();_c.Notice("已放置"+FishingBackpack.Name(expectedItem)+(UnityEngine.XR.XRSettings.isDeviceActive?" · 靠近按 A 使用":" · 靠近按 E 使用"),4);return true;
        }
        public bool PlaceEquipped()=>TryPlacement(1.02f,out var feet,out var yaw) && PlaceAt(feet,yaw,_c.Career.Backpack.Equipped);
        public bool Craft(string recipe)
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)))return false;
            var context=Context;
            bool ok=FishingRecipes.Craft(_c.Career.Backpack,recipe,context);
            if(ok)
            {
                var root=StationRoot(SelectedStation);var at=root==null?_c.ExplorerPosition+Vector3.up*.8f:root.position+Vector3.up*.6f;
                _c.WorldAudio?.Play(context==CraftStation.Forge?"ForgeStrike":context==CraftStation.Workbench?"WorkbenchAssemble":"CraftFinish",at,.48f);
                _c.Save();_c.NotifyWorkshopTransaction();_c.Notice("已制作 "+FishingBackpack.Name(FishingRecipes.Find(recipe).Output),3);
            }
            else {_c.WorldAudio?.Play("UIReject",_c.ExplorerPosition,.3f);_c.Notice("材料不足、背包已满，或需要靠近对应的制作设施。",4);}
            return ok;
        }
        public bool Smelt(string recipe,int count,FieldItem fuel)
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)) || Context!=CraftStation.Furnace)return false;
            bool ok=State.Start(_c.Career.Backpack,SelectedStation,recipe,count,fuel);
            if(ok){_c.WorldAudio?.Play("UIConfirm",_c.ExplorerPosition,.3f);_c.Save();_c.NotifyWorkshopTransaction();}
            else {_c.WorldAudio?.Play("UIReject",_c.ExplorerPosition,.3f);_c.Notice("检查材料、燃料，并先领取炉内成品。",4);}return ok;
        }
        public bool Collect()
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)) || Context!=CraftStation.Furnace)return false;
            bool ok=State.Collect(_c.Career.Backpack,SelectedStation);
            if(ok){_c.WorldAudio?.Play("StorageMove",_c.ExplorerPosition,.45f);_c.Save();_c.NotifyWorkshopTransaction();}
            else {_c.WorldAudio?.Play("UIReject",_c.ExplorerPosition,.3f);_c.Notice("没有成品或背包已满；成品会留在炉内。",4);}return ok;
        }
        public bool Remove()
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)) || !ReachStation(SelectedStation))return false;
            if(!State.Remove(_c.Career.Backpack,SelectedStation)){_c.Notice("公共设施不可拆；先领取成品并清空熔炉，背包需留空格。",4);return false;}
            var root=_stations[SelectedStation];_c.WorldAudio?.Play("StorageMove",root.position,.4f);
            root.gameObject.SetActive(false);Destroy(root.gameObject);_stations.Remove(SelectedStation);_fires.Remove(SelectedStation);SelectedStation=-1;_c.Save();return true;
        }
        private void OnApplicationQuit(){if(_c!=null && _c.Career!=null)_c.Save();}
        private void OnDestroy(){foreach(var mesh in _meshes.Values)if(mesh!=null)Destroy(mesh);if(_palette!=null)Destroy(_palette);}
    }
}
