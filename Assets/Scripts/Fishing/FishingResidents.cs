using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed class FishingResidents : MonoBehaviour
    {
        private FishingPrototype _c;
        private readonly Transform[] _people = new Transform[6];
        private float _spokenAt;
        public int ActiveId { get; private set; } = -1;
        public bool Open => ActiveId >= 0;
        public string Speech { get; private set; } = "";
        public string VisibleSpeech => Speech.Substring(0, Mathf.Min(Speech.Length, Mathf.FloorToInt((Time.unscaledTime - _spokenAt) * 42)));
        public Transform Person(int id) => id >= 0 && id < 6 ? _people[id] : null;
        public void Build(FishingPrototype c)
        {
            _c = c;
            for (int i = 0; i < 4; i++)
            {
                var p = FishingIslandWorld.Center(i) + new Vector3(7, 1.35f, 17);
                if (!c.Shore.Ground(p, out var feet)) throw new System.InvalidOperationException("NPC has no island footing " + i);
                Create(i, feet);
            }
            _people[4] = c.TackleShop.Cashier.transform;
            // A real porch floor keeps the guide reachable on the same approach as the shop.
            var porch = c.TackleShop.transform.TransformPoint(new Vector3(-2.0f, .08f, -5.6f));
            if (!c.Shore.Ground(porch, out var harborFeet) || !c.Shore.Clearance(harborFeet))
                throw new System.InvalidOperationException("Harbor guide has no clear porch footing");
            Create(5, harborFeet);
        }
        private void Create(int id, Vector3 feet)
        {
            var t = FishingV019Art.Spawn("Resident" + id, transform, feet, id == 0 || id == 1 ? 1.70f : 1.78f, 180);
            t.name = "Resident " + id + " " + FishingLore.ResidentName(id); _people[id] = t;
            var body = t.gameObject.AddComponent<CapsuleCollider>(); body.radius = .25f; body.height = 1.65f; body.center = Vector3.up * .825f;
            if (id == 0) FishingV019Art.Spawn("WindBell", t, new Vector3(-.43f, .74f, .03f), .22f);
            if (id == 1) FishingV019Art.Spawn("bag", t, new Vector3(.38f, .66f, -.14f), .35f);
            if (id == 2) FishingV019Art.Spawn("tool-shovel", t, new Vector3(.45f, 0, -.05f), 1.15f, 12);
            if (id == 3) FishingV019Art.Spawn("StarPrism", t, new Vector3(-.43f, .76f, .12f), .22f);
        }
        public bool InRange(int id)
        {
            if (!_c.Ashore || id < 0 || id >= 6 || _people[id] == null) return false;
            if (id == 4) return _c.TackleShop.NearCounter;
            var person = _people[id];
            if (Vector3.Distance(_c.ExplorerPosition, person.position) > 3.1f) return false;
            var face = person.position + Vector3.up * 1.45f;
            var delta = face - (_c.ExplorerPosition + Vector3.up * 1.4f);
            foreach (var hit in Physics.RaycastAll(face - delta, delta.normalized, delta.magnitude, ~(1 << 4), QueryTriggerInteraction.Ignore))
                if (hit.collider != _c.Shore.Body && !hit.transform.IsChildOf(person) && !FishingShore.NonSolidSurface(hit.collider)
                    && !hit.transform.IsChildOf(_c.Player.transform)) return false;
            return true;
        }
        public bool TryNearby(out int id)
        {
            id = -1; float best = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                if (i == 4 || !InRange(i)) continue;
                float distance = Vector3.Distance(_c.ExplorerPosition, _people[i].position);
                if (distance < best) { best = distance; id = i; }
            }
            return id >= 0;
        }
        public bool Talk(int id)
        {
            if (_c.Paused || _c.InventoryOpen || _c.JournalOpen || _c.HasPendingCatch || _c.WorldActionBusy || _c.Realms.Transitioning || !InRange(id)) return false;
            if (_c.ShopOpen && id != 4) return false;
            _c.TackleShop.Close(); _c.Career.Lore.Meet(id); _c.Save();
            ActiveId = id; _c.StoryModalChanged(); Say(Greeting(id));
            if (XRSettings.isDeviceActive) _c.XRNotebook.Open(8);
            return true;
        }
        public string Choice(int choice) => choice == 0 ? "聊聊这里的故事" : choice == 1 ? (ActiveId == 5 ? "汇报四段潮汐回响" : ActiveId == 4 ? "介绍商品与装备" : "询问秘境线索") : "航海与探索建议";
        public void Choose(int choice)
        {
            if (!Open || choice < 0 || choice > 2 || !_c.Ashore || _c.Paused || !InRange(ActiveId)) return;
            int id = ActiveId;
            if (choice == 0) Say(Story(id));
            else if (choice == 1)
            {
                if (id == 5)
                {
                    int pay = _c.Career.CompleteEchoVoyage();
                    if (pay > 0) { _c.Save(); _c.Regions.Show("潮汐回响 · 航路重生", "四段记忆已经归港  ·  +180 金币", 3); }
                    Say(pay > 0 ? "四段回响都回来了。风、林、潮、星，终于又连成了一条回家的路。\n\n收下这180金币，替我把灯火带到更远的海面。以后也随时欢迎回来。"
                        : _c.Career.Lore.Finale ? "你的航记已经记在港口的名册上。海面上还有鱼群、宝箱和旧航标。去走走吧，回家的灯会一直亮着。"
                        : "你带回了 " + _c.Career.Lore.ClearCount + " / 4 段回响。每座探索岛都有一扇刻着潮纹的门。把四段记忆带回这里，我们再一起连起航路。");
                }
                else Say(id == 4 ? "左侧陈列航海器材：罗盘、绳索、望远镜和提灯。右侧有食物与制作材料。\n\n船舶升级会立即装到船上；工具进背包后，按I移入快捷栏，再按F使用。金币不足或背包满时不会扣款。" : FishingLore.TrialHint(id) + "\n\n岛上潮纹石门通向" + FishingLore.ShrineName(id) + "。机关答错不收费，随时可以从入口返回。");
            }
            else Say(id == 0 ? "风铃岛的瞭望绳梯可以攀登。靠近绳梯按E，W/S控制上下，空格离开。登高后能看见那片月牙海湾。"
                : id == 1 ? "离岸可以游泳，Shift加速会消耗体力。靠近低岸，朝岸边走并按空格就能上岸；体力不足会变慢，停下就会恢复。"
                : id == 2 ? "打捞台的绳梯通往高处。沉潮机库里那只货箱有三处位置：起点、中间、承重台。别把它推过头。"
                : id == 3 ? "星轨圣所的方向是绝对方位。初始朝北，每按一次E顺时针转90度。朝向正确的星盘会亮起青光。"
                : id == 4 ? "先升级操舵会让靠岸更轻松。Shift刹车，S倒航；G下船前先把航速降到5.4公里每小时以下。J日志能直接设置四岛航点。"
                : "捕鱼、委托、旧航标与秘境共享金币。你可以按任意顺序探索；R随时救援回港，鱼获和已完成的故事会保留。");
        }
        private void Say(string text) { Speech = text; _spokenAt = Time.unscaledTime; }
        public void Reveal() { if (Open) _spokenAt = Time.unscaledTime - 100; }
        public void Close()
        {
            if (!Open) return;
            ActiveId = -1; _c.XRNotebook?.Close(); _c.StoryModalChanged();
        }
        private string Greeting(int id) => id == 0 ? "听，风铃在唱。你是新来的领航员吧？我叫铃音，替这座岛修补风暴留下的声音。"
            : id == 1 ? "先把脚步放轻。潮林里的小生命都在听海。你可以叫我青禾。"
            : id == 2 ? "又一个敢靠近沉船的人！我叫阿砾，今天刚从旧船里捞出一只没有生锈的罗盘。"
            : id == 3 ? "白昼也能看见星星，只是需要记住它们的位置。我是望星，记录每一条失落航路。"
            : id == 4 ? "欢迎回来，我是汐禾。你想听听用品的来历，还是准备给船和钓具添点新装备？"
            : "新领航员，欢迎靠港。我是林舟。你修过的灯、带回的鱼，还有岛上拾到的故事，我都替你记着。";
        private string Story(int id) => id == 0 ? "过去，守望者把风铃挂在海湾最窄的岩拱间。不同的声音告诉远航的渔人，家就在前方。\n\n风暴把音序打乱了。如果你愿意，到风之回廊替我们重新奏响它们。"
            : id == 1 ? "潮林曾为四座岛提供木材。旧守望者后来立下一条规矩：采走一棵树，要为下一代留下三棵。\n\n森光庭院保存着那份约定。光与阴影缺一不可。"
            : id == 2 ? "沙洲上的旧船叫归潮号。人们只记得它在风暴里沉没，却忘了船长先让所有人上了救生艇。\n\n沉潮机库里保留着最后一次装卸的记录。那只货箱，或许能告诉我们他留下了什么。"
            : id == 3 ? "四岛的信标不是孤立的灯。风、林、潮与星曾组成一个环，把迷失的船引回渔港。\n\n星轨圣所保存着航路的最后一段记忆。你可以从任何一座岛开始。"
            : id == 4 ? "这间店以前只修渔网。风暴之后，岛上的人把各自的物资送来，才有了如今的海湾百货。\n\n每一卷绳索、每一盏提灯，都曾陪人走过一段海路。"
            : "这片海叫回潮海。很久以前，四位守望者分别守护风、林、潮与星，把彼此的灯连成航路。\n\n风暴毁了灯，却没毁掉岛上的人。取回四段潮汐回响，新的故事就从你开始。";
        private void LateUpdate()
        {
            if (_c == null || _c.Paused) return;
            for (int i = 0; i < 6; i++)
            {
                if (i == 4 || _people[i] == null || !_c.Ashore || Vector3.Distance(_people[i].position, _c.ExplorerPosition) > 6) continue;
                var direction = Vector3.ProjectOnPlane(_c.ExplorerPosition - _people[i].position, Vector3.up);
                if (direction.sqrMagnitude > .2f) _people[i].rotation = Quaternion.Slerp(_people[i].rotation, Quaternion.LookRotation(direction), Time.deltaTime * 2.2f);
            }
        }
    }
}
