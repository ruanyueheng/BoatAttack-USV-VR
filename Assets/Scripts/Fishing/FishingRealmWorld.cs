using System;
using System.Collections;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed class FishingRealmWorld : MonoBehaviour
    {
        public const string RootName = "V019 Tide sanctuaries";
        private FishingPrototype _c;
        private readonly Transform[] _portals = new Transform[4], _rooms = new Transform[4], _devices = new Transform[5];
        private Vector3 _returnFeet;
        private Quaternion _returnRotation;
        private float _nextUse;
        private Transform _gate;
        private Material _lightMaterial;
        private readonly LineRenderer[,] _beams=new LineRenderer[4,3];
        private readonly Transform[] _saplings=new Transform[3];
        private Transform _waterGauge;
        private Transform _cargoBridge,_cargoBarrier;
        private float _demoAge;
        private int _demoNote;
        public int Island { get; private set; } = -1;
        public bool Inside => Island >= 0;
        public bool Transitioning { get; private set; }
        public float Blackout { get; private set; }
        public FishingShrineTrial Trial { get; private set; }
        public Transform Room => Inside ? _rooms[Island] : null;
        public Transform Portal(int island) => island >= 0 && island < 4 ? _portals[island] : null;
        public Transform Device(int id) => id >= 0 && id < 5 ? _devices[id] : null;
        public void Build(FishingPrototype c)
        {
            _c = c; Transform root = null;
            _lightMaterial=new Material(c.PaletteShader){name="V032 sanctuary light paths"};_lightMaterial.SetFloat("_Unlit",1);_lightMaterial.SetColor("_BaseColor",new Color(1.3f,1.2f,.8f));
            foreach (var go in gameObject.scene.GetRootGameObjects()) if (go.name == RootName) root = go.transform;
            if (root == null) throw new InvalidOperationException("Install v19 sanctuary scene first.");
            for (int i = 0; i < 4; i++)
            {
                _rooms[i] = root.Find("Realm " + i); _rooms[i].gameObject.SetActive(false);
                FishingV019Art.Sign(FishingLore.Inscription(i),_rooms[i],new Vector3(-5,2.3f,-7.5f),.052f,new Color(.95f,.84f,.58f));
                _portals[i] = c.Islands.WorldRoot.GetChild(i).Find("V019 island character/Portal");
                if (_portals[i] == null) throw new InvalidOperationException("Missing shrine door " + i);
                BuildTrialScenery(i);
            }
        }
        public bool TryTarget(out int id, out Transform target)
        {
            id = -1; target = null;
            if (_c == null || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing) return false;
            float best = 2.75f;
            for (int i = 0; i < (Inside ? 5 : 4); i++)
            {
                var t = Inside ? _devices[i] : _portals[i]; if (t == null || !t.gameObject.activeInHierarchy) continue;
                float distance = Vector3.Distance(_c.ExplorerPosition, t.position);
                if (distance >= best || !_c.Shore.ClearSight(_c.ExplorerPosition + Vector3.up * 1.4f, t.position + Vector3.up * 1.4f)) continue;
                best = distance; target = t; id = Inside ? i : i + 8;
            }
            return id >= 0;
        }
        public string NearbyHint
        {
            get
            {
                if (!TryTarget(out int id, out _)) return null;
                return id >= 8 ? "E 进入 " + FishingLore.ShrineName(id - 8) : id == 4 ? "E 返回岛屿" : id == 3 ? "E 读取潮汐回响"
                    : Island == 0 ? "E 敲响「" + (id == 0 ? "风" : id == 1 ? "潮" : "星") + "」风铃"
                    : Island == 1 ? "E 转动反光镜 · 朝"+FishingAdventure.DirectionName(Trial.State(id))+" · 生长 "+(Trial.Growth*100).ToString("0")+"%"
                    : Island == 2 ? (id == 0 ? "E 推动货箱 · 第 " + Trial.State(0) + " / 2 格 · 水位 "+(Trial.WaterLevel*100).ToString("0")+"%" : "E 水闸："+new[]{"关闭","排水","注水"}[Trial.State(1)])
                    : "E 联动星盘 · 朝" + FishingAdventure.DirectionName(Trial.State(id))+" · 同时带动下一盘";
            }
        }
        public bool Interact()
        {
            if (_c.Paused || _c.InventoryOpen || _c.JournalOpen || _c.ShopBlocksGameplay || _c.StoryBlocksGameplay || _c.WorldActionBusy
                || !TryTarget(out int id, out _)) return false;
            if (id >= 8) return Enter(id - 8);
            if (id == 4) return Exit();
            if (id == 3)
            {
                if (!Trial.Ready && !_c.Career.Lore.Cleared(Island)) { _c.Notice(FishingLore.TrialHint(Island), 7); return false; }
                int pay = _c.Career.CompleteShrine(Island);
                if (pay == 0 && !_c.Career.Lore.Cleared(Island)) { _c.Notice("金币已达上限；回响保留，腾出余额后可再次领取。", 5); return false; }
                _c.Save(); Refresh();
                _c.Regions.Show(FishingLore.ShrineName(Island) + " · 回响重现", pay > 0 ? "+90 金币  ·  航记已收录" : "航记已收录  ·  可再次自由探索", Island);
                _c.Notice(FishingLore.Echo(Island), 12); return true;
            }
            if (Time.time < _nextUse) return false;
            _nextUse = Time.time + .4f;
            bool ok = Trial.Use(id); Refresh();
            _c.Notice(Island == 0 ? (ok ? "清音回应 · " + Trial.Progress + " / 3 · 留一拍，再奏下一声" : "音序或节奏未合，风铃重归寂静。听星、风、潮。")
                : !ok && Island==2?"先排水至15%以下，货箱才推得动；抵达承重台后无需再推。"
                : Trial.Ready ? "机关共鸣，回响之门已经打开。" : NearbyHint + " · " + FishingLore.TrialHint(Island), 5);
            _c.Regions.Chime(id); return ok;
        }
        public bool Enter(int island)
        {
            if (island < 0 || island >= 4 || Inside || Transitioning || !_c.Ashore || _c.Paused || _c.HasPendingCatch || _c.WorldActionBusy
                || _c.InventoryOpen || _c.JournalOpen || _c.ShopBlocksGameplay || _c.DialogueOpen || _c.Shore.Swimming || _c.Shore.Climbing
                || !TryTarget(out int target, out _) || target != island + 8) return false;
            _returnFeet = _c.Shore.Position; _returnRotation = _c.Shore.Actor.rotation;
            StartCoroutine(Travel(island)); return true;
        }
        public bool Exit()
        {
            if (!Inside || Transitioning || _c.Paused || _c.DialogueOpen || _c.WorldActionBusy || _c.InventoryOpen || _c.JournalOpen
                || !TryTarget(out int id, out _) || id != 4) return false;
            StartCoroutine(Travel(-1)); return true;
        }
        private IEnumerator Travel(int destination)
        {
            Transitioning = true; _c.StoryModalChanged(); _c.TackleShop.Close(); _c.XRNotebook.Close();
            for (float t = 0; t < .28f; t += Time.unscaledDeltaTime) { Blackout = Mathf.Clamp01(t / .28f); yield return null; }
            Blackout = 1;
            int old = Island; bool moved;
            if (destination >= 0)
            {
                _rooms[destination].gameObject.SetActive(true); Physics.SyncTransforms();
                var feet = _rooms[destination].position + new Vector3(0, 0, -11);
                moved = _c.Shore.Relocate(feet, Quaternion.identity);
                if (moved)
                {
                    Island = destination; Trial = new FishingShrineTrial(destination); _nextUse = 0;_demoAge=0;_demoNote=0;
                    for (int i = 0; i < 5; i++) _devices[i] = Room.Find("Device " + i);
                    _gate = Room.Find("Echo gate"); Refresh();
                }
                else _rooms[destination].gameObject.SetActive(false);
            }
            else
            {
                moved = _c.Shore.Relocate(_returnFeet, _returnRotation);
                if (moved) { Island = -1; Trial = null; _rooms[old].gameObject.SetActive(false); }
            }
            _c.View.ResetLook(); _c.Shore.ResetMoveInput(); _c.LowerFieldTools();
            for (float t = 0; t < .42f; t += Time.unscaledDeltaTime) { Blackout = 1 - Mathf.Clamp01(t / .42f); yield return null; }
            Blackout = 0; Transitioning = false; _c.StoryModalChanged();
            if (!moved) { _c.Notice("入口落脚点被遮挡；已保留当前位置，R可安全救援。", 6); yield break; }
            _c.Regions.Show(Inside ? FishingLore.ShrineName(Island) : FishingAdventure.Name(old), Inside ? "潮汐守望者留下的记忆  ·  读懂铭文，唤醒回响" : FishingLore.Subtitle(old), Inside ? Island : old);
            _c.Notice(Inside ? "秘境入口可随时返回 · E操作机关 · J查看航记线索。" : "海风归来 · 已返回岛屿", 5);
        }
        public void Abort()
        {
            StopAllCoroutines();
            for (int i = 0; i < 4; i++) if (_rooms[i] != null) _rooms[i].gameObject.SetActive(false);
            Island = -1; Trial = null; Blackout = 0; Transitioning = false;
        }
        public void Tick()
        {
            if (!Inside || Transitioning || _c.Paused) return;
            bool ready=Trial.Ready;Trial.Tick(Time.deltaTime);
            if(ready!=Trial.Ready)Refresh();
            _demoAge+=Time.deltaTime;
            if(Island==0 && _demoNote<3 && _demoAge>=.7f+_demoNote*1.25f)
            {_c.Regions.Chime(new[]{2,0,1}[_demoNote]);_demoNote++;}
            if (Island == 2)
            {
                var p = new Vector3(-3, 0, -5 + Trial.State(0) * 2.5f);
                _devices[0].localPosition = Vector3.MoveTowards(_devices[0].localPosition, p, Time.deltaTime * 7);
            }
            PresentTrial();
        }
        private void Refresh()
        {
            bool open = Trial.Ready || _c.Career.Lore.Cleared(Island); _gate.gameObject.SetActive(!open);
            for (int i = 0; i < 3; i++)
            {
                var t = _devices[i]; if (t == null) continue;
                var signal = t.Find("Signal"); if (signal != null) signal.gameObject.SetActive(Island == 3 ? Trial.State(i) == (i == 0 ? 1 : i == 1 ? 3 : 2) : Island==1?Trial.State(i)==i+1:Trial.State(i) > 0);
                if (Island == 3) t.Find("Star pointer").localRotation = Quaternion.Euler(0, Trial.State(i) * 90, 0);
            }
            if (Island == 1)
            {
                var bridge = Room.Find("Light bridge"); bridge.gameObject.SetActive(open);
                Room.Find("Crossing barrier").gameObject.SetActive(!open);
            }
            if(Island==2){_cargoBarrier.gameObject.SetActive(!open);_cargoBridge.GetComponent<BoxCollider>().enabled=open;}
        }
        private void BuildTrialScenery(int island)
        {
            var room=_rooms[island];
            if(island==0 || island==1 || island==3)room.Find("Sanctuary ceiling").gameObject.SetActive(false);
            for(int i=0;i<3;i++)
            {
                var line=FishingArt.Line("V032 "+(island==0?"wind score":island==1?"reflected sunlight":island==2?"hydraulic gauge":"linked constellation")+" "+i,room,_lightMaterial,.045f,false);
                line.positionCount=2;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;_beams[island,i]=line;
                if(island==1)
                {
                    _saplings[i]=FishingV019Art.Spawn("tree_oak",room,new Vector3((i-1)*3,0,2.2f),1.1f);
                    var mirror=room.Find("Device "+i);FishingArt.Shape("Rotating mirror",PrimitiveType.Cube,mirror,new Vector3(0,1.5f,0),new Vector3(.8f,.65f,.08f),_c.cream);
                }
                if(island==3)
                {
                    for(int j=0;j<9;j++)
                    {
                        float a=j*2.399963f+i;FishingArt.Shape("Constellation pearl",PrimitiveType.Sphere,room,new Vector3(Mathf.Cos(a)*(2+i*2),5.5f,Mathf.Sin(a)*6),Vector3.one*.065f,_lightMaterial);
                    }
                }
            }
            if(island==2)
            {
                FishingArt.Shape("Water gauge backing",PrimitiveType.Cube,room,new Vector3(-7,1.4f,-3),new Vector3(1,2.8f,.3f),_c.dark);
                _waterGauge=FishingArt.Shape("Visible hydraulic water level",PrimitiveType.Cube,room,new Vector3(-7,1.4f,-3.2f),new Vector3(.66f,2.5f,.08f),_c.teal).transform;
                _cargoBridge=FishingArt.Shape("Hydraulic floating crossing",PrimitiveType.Cube,room,new Vector3(0,-.75f,3.8f),new Vector3(3,.24f,3.6f),_c.wood,true).transform;
                _cargoBridge.GetComponent<BoxCollider>().enabled=false;
                _cargoBarrier=new GameObject("Hydraulic crossing safety gate").transform;_cargoBarrier.SetParent(room,false);_cargoBarrier.localPosition=new Vector3(0,1.2f,3.8f);
                var barrier=_cargoBarrier.gameObject.AddComponent<BoxCollider>();barrier.size=new Vector3(19.7f,2.4f,.18f);
                for(int j=0;j<5;j++)FishingArt.Shape("Hydraulic gate slat",PrimitiveType.Cube,_cargoBarrier,new Vector3((j-2)*.6f,0,0),new Vector3(.09f,2.3f,.12f),_c.teal);
                FishingV019Art.Sign("排水 → 推箱 → 注水",room,new Vector3(-5,3.7f,-5),.04f,new Color(.95f,.84f,.58f));
            }
        }
        private void PresentTrial()
        {
            if(Island==2)
            {
                _waterGauge.localScale=new Vector3(.66f,Mathf.Max(.02f,2.5f*Trial.WaterLevel),.08f);
                _waterGauge.localPosition=new Vector3(-7,.15f+1.25f*Trial.WaterLevel,-3.2f);
                _cargoBridge.localPosition=new Vector3(0,Mathf.Lerp(-.75f,.12f,Trial.WaterLevel),3.8f);
            }
            for(int i=0;i<3;i++)
            {
                var line=_beams[Island,i];
                if(Island==1)
                {
                    var mirror=_devices[i].Find("Rotating mirror");mirror.localRotation=Quaternion.Euler(0,Trial.State(i)*90,0);
                    var start=_devices[i].localPosition+Vector3.up*1.5f;var seed=new Vector3((i-1)*3,.5f,2.2f);
                    bool lit=Trial.State(i)==i+1;line.SetPosition(0,start);line.SetPosition(1,lit?seed:start+Quaternion.Euler(0,Trial.State(i)*90,0)*Vector3.forward*3);
                    line.startColor=line.endColor=lit?new Color(1,.88f,.5f):new Color(.30f,.40f,.30f);
                    _saplings[i].localScale=Vector3.one*(1+Trial.Growth*1.5f);
                }
                else if(Island==0)
                {
                    var p=_devices[i].localPosition;float pulse=.5f+.5f*Mathf.Sin(_demoAge*Mathf.PI*2/1.25f);
                    line.SetPosition(0,p+new Vector3(-.7f,.06f,0));line.SetPosition(1,p+new Vector3(.7f,.06f,0));line.widthMultiplier=.03f+pulse*.06f;
                    line.startColor=line.endColor=Trial.State(i)>0?new Color(.65f,1,.8f):new Color(.8f,.64f,.34f);
                }
                else if(Island==3)
                {
                    var p=_devices[i].localPosition+Vector3.up*2.5f;var next=_devices[(i+1)%3].localPosition+Vector3.up*2.5f;
                    line.SetPosition(0,p);line.SetPosition(1,next);line.startColor=line.endColor=Trial.Ready?new Color(.6f,1,.85f):new Color(.55f,.58f,1);
                }
                else line.enabled=false;
            }
        }
        private void OnDestroy(){if(_lightMaterial!=null)Destroy(_lightMaterial);}
        private void OnDisable() { if (_c != null) Abort(); }
    }
}
