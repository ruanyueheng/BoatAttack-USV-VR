using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed class FishingVoyageWorld : MonoBehaviour
    {
        private FishingPrototype _c;
        private Transform _sealedChart, _chest, _lid;
        private Quaternion _lidClosed;
        private float _lidAngle;
        private Material _chestPaint;
        private FishingCareer _shownCareer;
        private float _opening;
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        public Transform TreasureChest => _chest;
        public Vector3 TreasureFocus { get; private set; }
        public Transform TreasureLid => _lid;
        public Quaternion TreasureLidOpenRotation=>_lidClosed*Quaternion.Euler(_lidAngle,0,0);
        public Vector3 TreasureApproach { get; private set; }
        private bool Context => _c != null && !_c.Paused && !_c.StartMenuOpen && !_c.InventoryOpen && !_c.JournalOpen
            && !_c.StoryBlocksGameplay && !_c.ShopBlocksGameplay && !_c.WorldActionBusy && !_c.HasPendingCatch
            && _c.Ashore && !_c.Shore.Swimming && !_c.Shore.Climbing;

        public void Build(FishingPrototype c)
        {
            _c = c;
            var paper = new GameObject("Sealed village appointment and old sea chart").transform;
            paper.SetParent(transform, false); paper.position = c.Home.InteractionPoint(2) + new Vector3(-.18f,.025f,0);
            FishingArt.Shape("Old chart envelope", PrimitiveType.Cube, paper, Vector3.zero, new Vector3(.32f,.012f,.22f), c.cream);
            FishingArt.Shape("Tide wax seal", PrimitiveType.Sphere, paper, new Vector3(.03f,.018f,-.01f), new Vector3(.045f,.013f,.045f), c.orange);
            _sealedChart = paper;
            BuildTreasure();
            _shownCareer = c.Career; _opening = c.Career.PirateKing ? 1 : 0; PoseLid();
        }

        private void BuildTreasure()
        {
            Vector3 feet = Vector3.zero, approach = Vector3.zero; bool found = false;
            foreach (var offset in new[] { new Vector3(2,0,-11), new Vector3(-2,0,-12), new Vector3(1,0,-7), new Vector3(0,0,-4) })
            {
                var candidate = FishingCoastalPresentation.Islets[0] + offset;
                if (!_c.Shore.Ground(candidate, out feet) || feet.y < .8f
                    || !_c.Shore.Ground(candidate + Vector3.back * 1.8f, out approach) || !_c.Shore.Clearance(approach)) continue;
                bool supported = true;
                foreach (float x in new[] { -.9f, .9f }) foreach (float z in new[] { -.65f, .65f })
                    if (!_c.Shore.Ground(candidate + new Vector3(x,0,z), out var corner) || Mathf.Abs(corner.y - feet.y) > .65f) supported = false;
                if (!supported) continue;
                found = true; break;
            }
            if (!found) throw new InvalidOperationException("The White Sail Cove treasure needs supported interior ground and a clear standing approach.");
            TreasureApproach = approach;
            _chest = new GameObject("Grand voyage treasure at White Sail Cove").transform;
            _chest.SetParent(transform, false); _chest.position = feet;
            float top = feet.y + .85f;
            foreach (float x in new[] { -.8f, .8f }) foreach (float z in new[] { -.52f, .52f })
            {
                if (!_c.Shore.Ground(feet + new Vector3(x,-feet.y,z), out var ground)) throw new InvalidOperationException("Treasure table leg lost its ground support.");
                float height = top - ground.y;
                FishingArt.Shape("Solid treasure table leg", PrimitiveType.Cube, _chest,
                    new Vector3(x, ground.y - feet.y + height * .5f, z), new Vector3(.14f,height,.14f), _c.wood, true);
            }
            FishingArt.Shape("Solid treasure table top", PrimitiveType.Cube, _chest, Vector3.up * .85f, new Vector3(2,.16f,1.45f), _c.wood, true);
            var source=Resources.Load<GameObject>("FishingFeedbackTools/HomeChest");
            if(source==null)throw new InvalidOperationException("The final treasure needs the shared hinged chest asset.");
            var crate=Instantiate(source,_chest).transform;crate.name="Grand treasure chest body";
            _chestPaint=new Material(_c.PaletteShader){name="Grand treasure chest vertex colours"};
            foreach(var r in crate.GetComponentsInChildren<Renderer>())r.sharedMaterial=_chestPaint;
            foreach(var child in crate.GetComponentsInChildren<Transform>())if(child.name.Equals("lid",StringComparison.OrdinalIgnoreCase)){_lid=child;break;}
            if(_lid==null)throw new InvalidOperationException("The treasure chest must retain its authored lid hinge.");
            Bounds ChestBounds(bool includeLid)
            {
                Bounds bounds=default;bool initialized=false;
                foreach(var r in crate.GetComponentsInChildren<Renderer>())
                {if(!includeLid && r.transform.IsChildOf(_lid))continue;if(!initialized){bounds=r.bounds;initialized=true;}else bounds.Encapsulate(r.bounds);}
                return bounds;
            }
            var closedBounds=ChestBounds(true);crate.localScale*=1.65f/Mathf.Max(.01f,closedBounds.size.x);closedBounds=ChestBounds(true);
            crate.position+=new Vector3(_chest.position.x-closedBounds.center.x,_chest.position.y+.97f-closedBounds.min.y,_chest.position.z-closedBounds.center.z);
            TreasureFocus=ChestBounds(true).center;
            var bodyBounds=ChestBounds(false);var body=new GameObject("Solid treasure chest body").AddComponent<BoxCollider>();body.transform.SetParent(_chest,false);body.center=_chest.InverseTransformPoint(bodyBounds.center);body.size=bodyBounds.size;
            var lidMesh=_lid.GetComponentInChildren<MeshFilter>();var lidCollider=lidMesh.gameObject.AddComponent<BoxCollider>();lidCollider.center=lidMesh.sharedMesh.bounds.center;lidCollider.size=lidMesh.sharedMesh.bounds.size;
            _lidClosed=_lid.localRotation;_lidAngle=-Mathf.Sign(lidMesh.sharedMesh.bounds.center.z)*105;
            FishingV019Art.Sign("风 · 林 · 潮 · 星", _chest, new Vector3(0,.78f,-.77f), .009f, new Color(.94f,.83f,.58f));
            var staticParts=new List<GameObject>();
            foreach(var renderer in _chest.GetComponentsInChildren<MeshRenderer>())
                if(!renderer.transform.IsChildOf(_lid) && renderer.GetComponent<MeshFilter>()!=null && renderer.GetComponent<TextMesh>()==null)staticParts.Add(renderer.gameObject);
            StaticBatchingUtility.Combine(staticParts.ToArray(),_chest.gameObject);
            Physics.SyncTransforms();
        }

        private void LateUpdate()
        {
            if (_c == null || _c.Career == null) return;
            if (!ReferenceEquals(_shownCareer, _c.Career))
            { _shownCareer = _c.Career; _opening = _c.Career.PirateKing ? 1 : 0; }
            if (_sealedChart != null) _sealedChart.gameObject.SetActive(!_c.Career.Lore.ChartFound);
            if (!_c.Paused && !_c.StoryBlocksGameplay)
                _opening = Mathf.MoveTowards(_opening, _c.Career.PirateKing ? 1 : 0, Time.deltaTime / 1.15f);
            PoseLid();
        }
        private void PoseLid()
        { if (_lid != null) _lid.localRotation = _lidClosed*Quaternion.Euler(_lidAngle*Mathf.SmoothStep(0,1,_opening),0,0); }

        private bool NearTreasure()
        {
            if (_chest == null || !_c.Ashore || _c.Realms.Inside || Vector3.Distance(_c.ExplorerPosition, _chest.position + Vector3.up) > 2.9f) return false;
            var from = _c.ExplorerPosition + Vector3.up * 1.4f;
            var delta = _chest.position + Vector3.up * 1.35f - from;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, _hits, delta.magnitude, ~(1 << 4), QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (_hits[i].collider != _c.Shore.Body && !_hits[i].transform.IsChildOf(_chest)
                    && !_hits[i].transform.IsChildOf(_c.Player.transform) && !FishingShore.NonSolidSurface(_hits[i].collider)) return false;
            return true;
        }
        private bool NearChart() => !_c.Career.Lore.ChartFound && _c.Home.ReachInteraction(2);
        public string NearbyHint
        {
            get
            {
                if (_c == null || !_c.Ashore) return null;
                string key = XRSettings.isDeviceActive ? "A " : "E ";
                if (NearChart()) return key + "阅读村长任命函与秘密旧海图";
                if (NearTreasure()) return key + (_c.Career.PirateKing ? "重读大航海宝藏铭文" : "查看四境封印的宝藏箱");
                if (_c.Realms.Inside && _c.Realms.TryTarget(out int id, out _) && id == 3
                    && _c.Career.Lore.Cleared(_c.Realms.Island) && !_c.Career.Lore.RewardClaimed(_c.Realms.Island))
                    return key + "补领四境遗物、材料与永久设施蓝图";
                return null;
            }
        }
        public bool TryInteract()
        {
            if (!Context) return false;
            if (NearChart()) { ReadChart(); return true; }
            if (NearTreasure()) { OpenTreasure(); return true; }
            if (_c.Realms.Inside && _c.Realms.TryTarget(out int id, out _) && id == 3)
            {
                if (_c.Career.Lore.Cleared(_c.Realms.Island) && !_c.Career.Lore.RewardClaimed(_c.Realms.Island))
                { ClaimRealmReward(_c.Realms.Island); return true; }
            }
            return false;
        }
        private void ReadChart()
        {
            _c.Career.AcceptMayorship(); bool first = _c.Career.DiscoverChart(); _c.Save();
            if (first) _c.Prologue.Queue(FishingPrologue.Sequence.Chart); // 发现旧海图过场
            _c.Home.InteractAt(2);
            _c.Notice("祖父沈望潮的旧海图被撕成四页，指向风、林、潮、星四座灯塔。J → 世界与秘境查看进展。", 12);
        }
        public string ClaimRealmReward(int island)
        {
            if (!_c.Career.Lore.Cleared(island)) return "这里的图页尚未取回。";
            if (!_c.Career.Lore.ChartFound) { const string missing = "先回家阅读航线书桌上的秘密旧海图，遗物会在这里为你保留。"; _c.Notice(missing,8); return missing; }
            if (_c.Career.Lore.RewardClaimed(island)) return "遗物已经领取，" + FishingLore.BlueprintName(island) + "永久保留。";
            if (!_c.Career.ClaimShrineReward(island))
            { const string full = "背包空间不足，遗物与材料整包保留。腾出空间后可在此处或向本岛守望者补领。"; _c.Notice(full,8); return full; }
            _c.Save(); _c.FieldWorld.ShowPickupPreview(FishingLore.Relic(island));
            var reward = FishingCareer.ShrineReward(island);
            string message = "获得 " + FishingBackpack.Name(reward[0].item) + " ×1、" + FishingBackpack.Name(reward[1].item) + " ×" + reward[1].count
                + "；永久解锁" + FishingLore.BlueprintName(island) + "："+FishingLore.BlueprintEffect(island)+"。";
            _c.Notice(message,12); _c.WorldAudio?.Play("StorageMove", _c.ExplorerPosition,.45f);
            return message;
        }
        public bool CraftCompass()
        {
            if (!(_c.InventoryOpen || _c.XRNotebook.IsOpen) || _c.WorkshopWorld.Context != CraftStation.Workbench) return false;
            bool ok = _c.Career.CraftVoyageCompass(_c.WorkshopWorld.Context);
            if (ok) { _c.Save(); _c.WorldAudio?.Play("CraftFinish", _c.ExplorerPosition,.45f); _c.Notice(_c.Career.MainObjective,10); }
            else _c.Notice("需领取四境遗物，并在工作台备齐铁锭 ×2、玻璃 ×2。已组装的罗盘可在背包或家园箱取回。",8);
            return ok;
        }
        private void OpenTreasure()
        {
            if (_c.Career.PirateKing)
            { _c.Letter.Show("守灯人的航海日志", "沈望潮", "给下一位村长：\n\n灯没有灭，只是睡着了。风会等人，林会留荫，潮会让路，星会指向家。\n\n他们叫我海贼王，其实我只想让每条船都回得来。灯芯交给你了。"); return; }
            if (_c.Career.OpenGrandTreasure() == 0)
            { _c.Notice(_c.Career.Lore.ClearCount < 4 ? "四枚印记仍不完整。风、林、潮、星的图页会指出开启的方法。" : _c.Career.Backpack.Count(FieldItem.VoyageCompass) == 0 ? "锁孔像一面罗盘。组装远航罗盘并随身带来，再试一试。" : "金币余额已近上限；宝藏和罗盘为你保留，先到商店准备村庄建设物资。",10); return; }
            _c.Save(); _c.WorldAudio?.Play("MechanismClick", _chest.position,.5f);
            _c.Prologue.Queue(FishingPrologue.Sequence.Finale); // 宝藏终章过场
            _c.Regions.Show("大航海宝藏 · 海贼王", "+1200 金币 · 永久称号已记录",3);
            _c.Notice("宝藏开启！远航罗盘嵌入古锁，海贼王的名号属于你。+1200 金币；回村建设、收藏与自由远航仍可继续。",12);
        }
        public static string CompactObjective(FishingCareer c)
        {
            if (!c.Lore.MayorAccepted) return "主线 · 与林舟交谈，接任回潮村村长";
            if (!c.Lore.ChartFound) return "主线 · 家中书桌：寻找秘密旧航海图";
            if (c.Lore.ClearCount < 4) return "主线 · 四境失落图页 " + c.Lore.ClearCount + " / 4 · J 查看";
            if (c.Lore.RewardedShrines != 15) return "主线 · 四境遗物与设施蓝图待领 · J 查看";
            if (c.PirateKing) return "海贼王 · 重建回潮村，继续收藏与远航";
            return c.Backpack.Count(FieldItem.VoyageCompass) > 0 ? "主线 · 白帆拱湾内侧：大航海宝藏" : "主线 · 取回或在工作台组装远航罗盘";
        }
        private void OnDestroy(){if(_chestPaint!=null)Destroy(_chestPaint);}
    }
}
