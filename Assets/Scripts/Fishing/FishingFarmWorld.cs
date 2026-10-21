using System;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public enum FarmWork { Wait,Till,Plant,Water,Harvest,Refill,Seeds,Sell,TakeCan,ReturnCan }
    [DefaultExecutionOrder(260)]
    public sealed partial class FishingFarmWorld : MonoBehaviour
    {
        private FishingPrototype _c;
        private readonly Transform[] _targets=new Transform[16];
        private readonly Transform[,] _crops=new Transform[FishingFarm.PlotCount,3];
        private readonly GameObject[] _soil=new GameObject[FishingFarm.PlotCount];
        private readonly Renderer[][] _soilSurfaces=new Renderer[FishingFarm.PlotCount][];
        private Material _dry,_wet;
        private Transform _hoe,_can;
        private int _actionPlot=-1;
        private FarmWork _actionWork;
        public Transform Root {get;private set;}
        public bool Nearby=>_c!=null && _c.Ashore && FishingPrototype.FlatDistance(_c.Shore.Position,Root.position+new Vector3(19,0,0))<22;
        public string Status=>"回潮菜园 · 水壶"+(CarryingCan?"在手中 ":"在井边 ")+_c.Career.Farm.Water+" / "+FishingFarm.WaterCapacity+" · 种子 "+_c.Career.Backpack.Count(FieldItem.TurnipSeeds)+" · 萝卜 "+_c.Career.Backpack.Count(FieldItem.Turnip);
        public Transform Target(int id)=>id>=0 && id<_targets.Length?_targets[id]:null;
        public Vector3 Aim(int id)=>Target(id).position+Vector3.up*(id<12?.38f:id==12?1.0f:id==15?0:.9f);
        public static Vector3 PlotOffset(int id)=>new Vector3(14+id%4*3.6f,.06f,-5+id/4*5);
        public void Build(FishingPrototype c)
        {
            _c=c;Root=new GameObject("V035 home garden").transform;Root.SetParent(transform,false);Root.position=c.Home.Origin;
            var fixtures=Model("GardenFixtures",Root,Vector3.zero,true);
            var basis=Resources.Load<Material>("FishingV035/Coastal");
            _dry=new Material(basis){name="Garden dry earth"};_dry.SetColor("_BaseColor",new Color(.37f,.25f,.15f));
            _wet=new Material(basis){name="Garden watered earth"};_wet.SetColor("_BaseColor",new Color(.20f,.13f,.08f));
            for(int i=0;i<FishingFarm.PlotCount;i++)
            {
                var p=Model("FarmPlot",Root,PlotOffset(i),false);p.name="菜畦 "+(i+1);_targets[i]=p;
                var soil=Model("FarmSoil",p,Vector3.zero,false);_soil[i]=soil.gameObject;_soilSurfaces[i]=soil.GetComponentsInChildren<Renderer>();
                for(int stage=0;stage<3;stage++)_crops[i,stage]=Model("Turnip"+stage,p,Vector3.zero,false);
            }
            foreach(var pair in new[]{(12,new Vector3(10.2f,0,-8)),(13,new Vector3(10.2f,0,-3.8f)),(14,new Vector3(10.2f,0,.5f))})
            {
                var t=new GameObject(pair.Item1==12?"海风水井":pair.Item1==13?"种子工作台":"收获木箱").transform;t.SetParent(Root,false);t.localPosition=pair.Item2;_targets[pair.Item1]=t;
            }
            FishingV019Art.Sign("回潮菜园\n翻土播种 → 井边拿壶打水 → 提壶浇灌\n每天浇水 · 三夜成熟 · 空壶放回井边",Root,new Vector3(9,1.7f,-11.3f),.021f,new Color(.98f,.91f,.72f));
            _hoe=Model("GardenHoe",Root,Vector3.zero,false);_hoe.gameObject.SetActive(false);
            BuildWateringCan(fixtures);
            Refresh();
        }
        internal static Transform Model(string name,Transform parent,Vector3 p,bool solid)
        {
            var source=Resources.Load<GameObject>("FishingV035/"+name);if(source==null)throw new InvalidOperationException("Missing V035 home asset: "+name);
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;Instantiate(source,t,false);
            bool leaves=name=="FlowerPatch" || name=="MeadowPatch" || name=="IvyDrape" || name=="FernClump" || name.StartsWith("Turnip",StringComparison.Ordinal);
            FishingV035Materials.Apply(t,leaves);
            if(solid)foreach(var f in t.GetComponentsInChildren<MeshFilter>())f.gameObject.AddComponent<MeshCollider>().sharedMesh=Resources.Load<Mesh>("FishingV035/"+name+"Collision")??f.sharedMesh;
            return t;
        }
        public bool TryTarget(out int id,out Transform root)
        {
            id=-1;root=null;
            if(_c==null || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing)return false;
            float nearest=2.25f;
            for(int i=0;i<_targets.Length;i++)
            {
                var p=_targets[i].position;float d=FishingPrototype.FlatDistance(_c.Shore.Position,p);
                if(d>=nearest || Mathf.Abs(p.y-_c.Shore.Position.y)>(i==15?1f:.6f))continue;
                var direction=Aim(i)-_c.viewCamera.transform.position;
                if(Vector3.Dot(_c.viewCamera.transform.forward,direction.normalized)<.1f)continue;
                if(Physics.Linecast(_c.viewCamera.transform.position,Aim(i),out var hit,~(1<<2),QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(Root) && !hit.transform.IsChildOf(_c.Shore.Actor))continue;
                nearest=d;id=i;root=_targets[i];
            }
            return id>=0;
        }
        public FarmWork Work(int id)
        {
            if(id==12)return FarmWork.Refill;if(id==13)return FarmWork.Seeds;if(id==14)return FarmWork.Sell;if(id==15)return CarryingCan?FarmWork.ReturnCan:FarmWork.TakeCan;
            var p=_c.Career.Farm.Plot(id);return p==null?FarmWork.Wait:!p.tilled?FarmWork.Till:!p.planted?FarmWork.Plant:_c.Career.Farm.Ready(id)?FarmWork.Harvest:p.wateredDay!=_c.Career.Farm.Day?FarmWork.Water:FarmWork.Wait;
        }
        public string Hint(int id)
        {
            string key=_c.DesktopDisplay?"E ":"右手柄 A ";var work=Work(id);
            if(id==12)return key+(CarryingCan?"摇井绳打水 · 灌满手中的水壶":"先拿起井边木架上的水壶");
            if(id==15)return key+(CarryingCan?"把水壶放回井边木架":"拿起井边水壶 · "+_c.Career.Farm.Water+" / "+FishingFarm.WaterCapacity);
            if(id==13)return key+(_c.Career.Farm.StarterClaimed?"购买萝卜种子 ×6 · 12 海币":"领取首次种子 ×12");
            if(id==14)return key+"出售背包里的萝卜 · 每颗 18 海币";
            string action=work==FarmWork.Till?"翻土":work==FarmWork.Plant?"播种海风萝卜":work==FarmWork.Water?(CarryingCan?"倾壶浇水":"先去井边拿起水壶"):work==FarmWork.Harvest?"收获萝卜与种子":"今日已浇水 · 生长 "+_c.Career.Farm.Plot(id).growth+" / 3 夜";
            return key+"菜畦 "+(id+1)+" · "+action;
        }
        public bool InteractAt(int id)
        {
            if(_c.Paused || _c.WorldActionBlocksGameplay || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.InventoryOpen || _c.JournalOpen)return false;
            var work=Work(id);
            if(work==FarmWork.Water || work==FarmWork.Refill || work==FarmWork.TakeCan || work==FarmWork.ReturnCan)return BeginCanWork(id,work);
            if(CarryingCan && id<12){_c.Notice("先把水壶放回井边木架，再腾出双手照料作物。",4);return false;}
            return CommitAt(id,work);
        }
        internal void PresentAction(int id,FarmWork work){_actionPlot=id;_actionWork=work;}
        internal bool CommitAt(int id,FarmWork expected)
        {
            if(Target(id)==null || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing || FishingPrototype.FlatDistance(_c.Shore.Position,Target(id).position)>2.25f || Work(id)!=expected)return false;
            var farm=_c.Career.Farm;bool ok;string message;
            switch(expected)
            {
                case FarmWork.Till:ok=farm.Till(id);message="土已翻好 · 再按一次播种";break;
                case FarmWork.Plant:ok=farm.Plant(id,_c.Career.Backpack);message=ok?"种子已种下 · 记得每天浇水":"需要海风萝卜种子 · 到种子工作台领取或购买";break;
                case FarmWork.Water:ok=CarryingCan && _canCommit && farm.WaterPlot(id);message=ok?"浇水完成 · 睡过一夜后成长":!CarryingCan?"先拿起井边木架上的水壶":"水壶空了 · 到菜园水井补水";break;
                case FarmWork.Harvest:ok=farm.Harvest(id,_c.Career.Backpack);message=ok?"收获海风萝卜 ×1、种子 ×1":"背包需容纳萝卜和种子 · 作物会留在地里";break;
                case FarmWork.Refill:ok=CarryingCan && _canCommit;if(ok)farm.Refill();message=ok?"井水已灌满水壶 · 提到菜畦边浇水":"先拿起井边木架上的水壶";break;
                case FarmWork.Seeds:ok=farm.StarterClaimed?_c.Career.BuyFarmSeeds():farm.ClaimStarter(_c.Career.Backpack);message=ok?"种子已放入背包":"背包空间或海币不足 · 首次种子可免费领取";break;
                case FarmWork.Sell:int value=_c.Career.SellTurnips();ok=value>0;message=ok?"售出萝卜 · +"+value+" 海币":"没有可出售的萝卜，或海币已达上限";break;
                default:_c.Notice("今日已浇水 · 休息至明晨查看生长",4);return false;
            }
            _c.Notice(message,5);
            if(ok){_c.Save();Refresh();_c.Effects?.Burst(Target(id).position+Vector3.up*.2f,expected==FarmWork.Water?new Color(.45f,.8f,1):new Color(.85f,.73f,.42f),false);}
            return ok;
        }
        public void Refresh()
        {
            if(_c==null)return;
            for(int i=0;i<FishingFarm.PlotCount;i++)
            {
                var p=_c.Career.Farm.Plot(i);_soil[i].SetActive(p.tilled);
                foreach(var r in _soilSurfaces[i])r.sharedMaterial=p.wateredDay==_c.Career.Calendar.Day?_wet:_dry;
                for(int s=0;s<3;s++)_crops[i,s].gameObject.SetActive(p.planted && s==Mathf.Clamp(p.growth-1,0,2));
            }
        }
        private void Update(){if(_c!=null){TickCanWork();if(_c.Career.Farm.AdvanceDay(_c.Career.Calendar.Day))Refresh();}}
        private void PresentSoilTool()
        {
            if(_c==null)return;
            bool active=!Busy && _c.WorldActionBusy && _c.Actions.TargetId=="farm:"+_actionPlot && !_c.Paused && _c.Hands.Right.Visible;
            _hoe.gameObject.SetActive(active && _actionWork==FarmWork.Till);
            if(active && _actionWork==FarmWork.Till)
            {
                var palm=_c.Hands.Right.Palm.position;
                var rotation=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0)*Quaternion.Euler(35,0,-12);
                var grip=new Vector3(.036f,0,0);var handRotation=Quaternion.LookRotation(Vector3.forward,Vector3.right);
                _hoe.SetPositionAndRotation(palm-rotation*grip,rotation);
                _c.Hands.SetToolTarget(false,_hoe,grip,handRotation,.8f,fingers:new FishingFingerPose(.7f,.8f,.82f,.85f,.7f));
                _c.Hands.Right.SetGripSurface(_hoe.TransformPoint(Vector3.down*.12f),_hoe.TransformPoint(Vector3.up*.14f),.018f);
                _c.Hands.Right.SetForearmRoll(.65f);
            }
        }
        private void OnDestroy(){if(_dry!=null)Destroy(_dry);if(_wet!=null)Destroy(_wet);DestroyCanPresentation();}
    }
}
