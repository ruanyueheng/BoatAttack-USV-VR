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
            if(!State.Initialized)
            {
                var site=_c.Shore.Sites[0];
                if(site.Available)
                {
                    foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace})
                        for(int attempt=0;attempt<48;attempt++)
                        {
                            if(!_c.FieldWorld.TryLocation(site.Landing,5+attempt/4,(kind==CraftStation.Workbench?125:235)+attempt*47,out var feet))continue;
                            if(!FitStationFoot(feet,out feet) || BlocksAccess(feet) || !_c.Shore.SafeCoastalFooting(feet) || !StationSpaceClear(feet))continue;
                            int id=State.Place(null,kind,feet.x,feet.y+.02f,feet.z,0,true);
                            if(id>=0)Spawn(State.Station(id));Physics.SyncTransforms();break;
                        }
                    State.FinishInitialization();
                }
            }
            foreach(var station in State.Stations)if(!_stations.ContainsKey(station.id))Spawn(station);
            Physics.SyncTransforms();
            EnsurePublicForge();
        }
        private void EnsurePublicForge()
        {
            if(State.Stations.Any(s=>s.kind==CraftStation.Forge && s.permanent) || !_c.Shore.Sites[0].Available)return;
            var landing=_c.Shore.Sites[0].Landing;
            // Scan actual shore heights, rather than constraining the forge to the
            // landing's narrow height band. This also leaves each old station's apron clear.
            for(int ring=2;ring<=24;ring+=2)for(int step=0;step<36;step++)
            {
                var probe=landing+Quaternion.Euler(0,step*10,0)*Vector3.forward*ring;probe.y=0;
                if(!_c.Shore.Ground(probe,out var feet) || Mathf.Abs(feet.y-landing.y)>3f || !_c.Shore.Clearance(feet))continue;
                if(_c.Shore.Sites.Any(s=>s.Available && FishingPrototype.FlatDistance(feet,s.Berth)<6f))continue;
                if(State.Stations.Any(s=>s.permanent && FishingPrototype.FlatDistance(feet,new Vector3(s.x,s.y,s.z))<3.5f))continue;
                if(!FitStationFoot(feet,out feet) || BlocksAccess(feet) || !_c.Shore.SafeCoastalFooting(feet) || !StationSpaceClear(feet))continue;
                int id=State.Place(null,CraftStation.Forge,feet.x,feet.y+.02f,feet.z,0,true);
                if(id<0)return;Spawn(State.Station(id));Physics.SyncTransforms();return;
            }
            Debug.LogWarning("[FORGE] No safe public forge footing found; the craftable forge is still available at the workbench.");
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
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.up*.5f;collider.size=new Vector3(1.3f,1,.95f);
            _stations.Add(s.id,root);
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
            bool done=State.Tick(Time.deltaTime);
            if(Time.time>=_senseAt)
            {
                Sense();_senseAt=Time.time+.15f;
                foreach(var n in _nodes)if(n.Root!=null)n.Root.localScale=Vector3.one*(State.ResourceReady(n.Id)?1:.32f);
                foreach(var fire in _fires){bool burning=State.Station(fire.Key).remaining>0;fire.Value.intensity=burning?1.8f:0;var coals=_stations[fire.Key].Find("Hot coals");if(coals!=null)coals.gameObject.SetActive(burning);}
            }
            if(done || Time.time>=_saveAt){_saveAt=Time.time+10;_c.Save();}
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
        public bool TryInteractionTarget(out int stationId,out ResourceNode resource,bool refresh=true)
        {
            if(refresh)Sense();stationId=StationIsClosest?_nearStation:-1;resource=stationId<0?_nearNode:null;
            return stationId>=0 || resource!=null;
        }
        public bool TryHarvestTarget(out ResourceNode resource){Sense();resource=_nearNode;return resource!=null;}
        public bool OpenStation(int id)
        {
            if(_c.Paused || !ReachStation(id))return false;
            SelectedStation=id;_c.OpenCrafting();return true;
        }
        public bool HarvestNode(int id,FieldItem expectedItem,FieldItem expectedTool)
        {
            if(_c.Paused || !_c.Ashore || _c.Career.Backpack.Equipped!=expectedTool)return false;
            ResourceNode node=null;foreach(var candidate in _nodes)if(candidate.Id==id){node=candidate;break;}
            if(node==null || node.Item!=expectedItem || node.Root==null || !node.Root.gameObject.activeInHierarchy || !Reach(node.Feet))return false;
            bool pick=expectedTool==FieldItem.StonePickaxe || expectedTool==FieldItem.IronPickaxe;
            if(NeedsPick(node.Item) && !pick){_c.Notice("矿石需要手持石镐或铁镐：I 查看配方，1–9 装备。",4);return false;}
            int amount=expectedTool==FieldItem.IronPickaxe && (node.Item==FieldItem.Stone || NeedsPick(node.Item))?4:3;
            if(!State.Harvest(_c.Career.Backpack,node.Id,node.Item,amount)){_c.Notice(State.ResourceReady(node.Id)?"背包已满，资源没有消耗。":"这处资源正在恢复，换一处采集。",3);return false;}
            _c.Effects?.Burst(node.Root.position+Vector3.up*.3f,new Color(.8f,.75f,.5f));_c.Save();_c.Notice("采集 "+FishingBackpack.Name(node.Item)+" ×"+amount+" · I → 制作",4);return true;
        }
        public bool HarvestNearest()
        {
            if(_c.Paused || !_c.Ashore || !TryHarvestTarget(out var node)){_c.Notice("靠近可采集的木堆、草丛或矿石，再按 E。",3);return false;}
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
        private bool FitStationFoot(Vector3 seed,out Vector3 feet,float yaw=0)
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
            if(id<0){_c.Notice("无法放置：设施已达 32 个上限或物品不足。",3);return false;}
            Spawn(State.Station(id));Physics.SyncTransforms();_c.Save();_c.Notice("已放置"+FishingBackpack.Name(expectedItem)+" · 靠近按 E 使用",4);return true;
        }
        public bool PlaceEquipped()=>TryPlacement(1.02f,out var feet,out var yaw) && PlaceAt(feet,yaw,_c.Career.Backpack.Equipped);
        public bool Craft(string recipe)
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)))return false;
            bool ok=FishingRecipes.Craft(_c.Career.Backpack,recipe,Context);if(ok){_c.Save();_c.NotifyWorkshopTransaction();_c.Notice("已制作 "+FishingBackpack.Name(FishingRecipes.Find(recipe).Output),3);}else _c.Notice("材料不足、背包已满，或需要靠近对应的制作设施。",4);return ok;
        }
        public bool Smelt(string recipe,int count,FieldItem fuel)
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)) || Context!=CraftStation.Furnace)return false;
            bool ok=State.Start(_c.Career.Backpack,SelectedStation,recipe,count,fuel);if(ok){_c.Save();_c.NotifyWorkshopTransaction();}else _c.Notice("检查材料、燃料，并先领取炉内成品。",4);return ok;
        }
        public bool Collect()
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)) || Context!=CraftStation.Furnace)return false;
            bool ok=State.Collect(_c.Career.Backpack,SelectedStation);if(ok){_c.Save();_c.NotifyWorkshopTransaction();}else _c.Notice("没有成品或背包已满；成品会留在炉内。",4);return ok;
        }
        public bool Remove()
        {
            if(!(_c.InventoryOpen || (_c.XRNotebook!=null && _c.XRNotebook.IsOpen)) || !ReachStation(SelectedStation))return false;
            if(!State.Remove(_c.Career.Backpack,SelectedStation)){_c.Notice("公共设施不可拆；先领取成品并清空熔炉，背包需留空格。",4);return false;}
            var root=_stations[SelectedStation];root.gameObject.SetActive(false);Destroy(root.gameObject);_stations.Remove(SelectedStation);_fires.Remove(SelectedStation);SelectedStation=-1;_c.Save();return true;
        }
        private void OnApplicationQuit(){if(_c!=null && _c.Career!=null)_c.Save();}
        private void OnDestroy(){foreach(var mesh in _meshes.Values)if(mesh!=null)Destroy(mesh);if(_palette!=null)Destroy(_palette);}
    }
}
