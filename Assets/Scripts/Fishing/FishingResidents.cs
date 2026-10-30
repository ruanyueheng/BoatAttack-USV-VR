using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed class FishingResidents : MonoBehaviour
    {
        private FishingPrototype _c;
        private readonly Transform[] _people = new Transform[6];
        private readonly AudioSource[] _voices = new AudioSource[6];
        private readonly int[] _storyIndex = new int[6];
        private readonly bool[] _asked = new bool[6];
        private bool _asking,_keeperThanked;
        private float _spokenAt;
        private int _voicedLength;
        private float _voicedSeconds;
        private readonly FishingCashierConversation _cashier=new FishingCashierConversation();
        private int _homeNavigationFrame=-1;
        public int ChoiceCount=>ActiveId==4?_cashier.ChoiceCount:3;
        public FishingResidentBrain Brain(int id)=>id>=0 && id<6 && _people[id]!=null?_people[id].GetComponent<FishingResidentBrain>():null;
        public AudioSource VoiceSource(int id)=>id>=0 && id<6?_voices[id]:null;
        /// <summary>Short delivery cue for the name plate (e.g. "笑着"); empty when neutral.</summary>
        public string Mood=>!Open?"":_asking?"认真地问":Speech.StartsWith("嘿嘿")||Speech.Contains("！")?"笑着":Speech.StartsWith("嘘")?"压低声音":"";
        /// <summary>Text still typing or the voice still speaking (UI keeps its typing dots until both finish).</summary>
        public bool Typing=>Open && (VisibleSpeech.Length<Speech.Length || (VoiceSource(ActiveId)!=null && VoiceSource(ActiveId).isPlaying));
        /// <summary>The resident is asking a question; choices 0/1 are replies.</summary>
        public bool Asking=>Open && _asking;
        public CashierTopic CashierTopic=>_cashier.Topic;
        public string TopicTitle=>ActiveId==4?_cashier.Title:"回潮海的故事";
        public int ActiveId { get; private set; } = -1;
        public bool Open => ActiveId >= 0;
        public string Speech { get; private set; } = "";
        // The voiced paragraph types at the recording's pace so subtitles follow the voice; the rest at 42 chars/s.
        public string VisibleSpeech
        {
            get
            {
                float age=Time.unscaledTime-_spokenAt;
                int shown=_voicedLength>0 && age<_voicedSeconds?Mathf.FloorToInt(_voicedLength*age/_voicedSeconds):_voicedLength+Mathf.FloorToInt((age-_voicedSeconds)*42);
                return Speech.Substring(0,Mathf.Clamp(shown,0,Speech.Length));
            }
        }
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
            _people[4] = c.TackleShop.Cashier.transform;Voice(4);
            // A real porch floor keeps the guide reachable on the same approach as the shop.
            var porch = c.TackleShop.transform.TransformPoint(new Vector3(-2.0f, .08f, -5.6f));
            if (!c.Shore.Ground(porch, out var harborFeet) || !c.Shore.Clearance(harborFeet))
                throw new System.InvalidOperationException("Harbor guide has no clear porch footing");
            Create(5, harborFeet);
        }
        private void Create(int id, Vector3 feet)
        {
            // v044: skinned humanoid residents (Tools/Art/build_v044_residents.py), animated by FishingResidentBrain.
            var prefab=Resources.Load<GameObject>("FishingV044/Residents/Resident"+id);
            if(prefab==null)throw new System.InvalidOperationException("Missing animated resident "+id+"; run Tools/Art/build_v044_residents.py");
            // Logical body (facing, collider, brain) wraps the animated model so the brain can correct a backwards avatar.
            var t=new GameObject().transform;t.SetParent(transform,false);t.SetPositionAndRotation(feet,Quaternion.Euler(0,180,0));
            Instantiate(prefab,t,false).name="Body";
            t.name = "Resident " + id + " " + FishingLore.ResidentName(id); _people[id] = t;
            var body = t.gameObject.AddComponent<CapsuleCollider>(); body.radius = .25f; body.height = 1.65f; body.center = Vector3.up * .825f;
            // Hip-hung props floated off the animated bones (wrong bone scale/axes); identity now comes from each resident's work. Only the planted shovel stays.
            if (id == 2) FishingV019Art.Spawn("tool-shovel", t, new Vector3(.55f, 0, -.25f), 1.15f, 12);
            t.gameObject.AddComponent<FishingResidentBrain>().Build(_c,id,Voice(id));
            _homeFeet[id]=feet;
        }
        // ---- v044 居民的一天：按游戏时间换驻足点；节日傍晚聚到广场并互相闲聊。 ----
        private readonly Vector3[] _homeFeet=new Vector3[6];
        /// <summary>Festival gathering centre; FishingLife sets it to its plaza (default: harbor porch).</summary>
        public Vector3 Plaza { get; set; }
        /// <summary>Validation: stop clock-driven station changes so a test never straddles a schedule boundary.</summary>
        public bool FreezeSchedule { get; set; }
        private float _scheduleAt,_chatAt;private int _chatTurn;
        public static bool FestivalEvening(FishingCalendar cal)=>FishingFestival.On(cal.Day)>=0 && cal.Hour>=16 && cal.Hour<21;
        /// <summary>Station name for a resident at an hour: home / break / dock / festival.</summary>
        public static string StationAt(int id,float hour,bool festivalEvening)
            =>festivalEvening?"festival":id==5 && hour>=19 && hour<23?"dock":hour>=12 && hour<14?"break":"home";
        // Arm-span clearance (0.5 m at chest height) so gestures don't sink into counters, shelves or railings.
        private bool Spot(Vector3 seed,int id,out Vector3 feet)
        {
            if(!_c.Shore.Ground(seed+Vector3.up*.3f,out feet))return false;
            foreach(var col in Physics.OverlapCapsule(feet+Vector3.up*.55f,feet+Vector3.up*1.35f,.5f,~(1<<4|1<<5),QueryTriggerInteraction.Ignore))
                if(col!=_c.Shore.Body && !col.transform.IsChildOf(_c.Shore.Actor) && !col.transform.IsChildOf(_people[id]) && !FishingShore.NonSolidSurface(col))return false;
            // Reserve queued destinations as well as occupied spots: off-screen residents have not teleported there yet.
            for(int other=0;other<_people.Length;other++)
            {
                if(other==id || _people[other]==null)continue;
                var target=Brain(other)?.ScheduledFeet??_people[other].position;
                if(Mathf.Abs(target.y-feet.y)<1.8f && FishingPrototype.FlatDistance(target,feet)<1.4f)return false;
            }
            return true;
        }
        /// <summary>Validation: send a resident to a named station now (home/break/dock/festival).</summary>
        public void SendTo(int id,string station,bool instant){_forced[id]=station;_forcedInstant=instant;Schedule();_forced[id]=null;}
        private readonly string[] _forced=new string[6];private bool _forcedInstant;
        private void Schedule()
        {
            var cal=_c.Career.Calendar;bool fest=FestivalEvening(cal);
            for(int id=0;id<6;id++)
            {
                var brain=Brain(id);if(brain==null || id==4)continue;
                string station=_forced[id]??StationAt(id,cal.Hour,fest);if(_forced[id]==null && FreezeSchedule)continue;var home=_homeFeet[id];var feet=home;var facing=Quaternion.Euler(0,180,0);string[] work=null;
                if(station=="festival")
                {
                    // Ring around the plaza, >=1.2 m apart, facing the centre; falls back to home if the ground is cluttered.
                    var centre=Plaza!=Vector3.zero?Plaza:_c.TackleShop.transform.TransformPoint(new Vector3(0,.08f,-13)); // open beach in front of the shop until FishingLife sets its plaza
                    int participant=id==5?4:id;
                    for(int k=0;k<48;k++){var seed=centre+Quaternion.Euler(0,participant*72+k%12*30,0)*Vector3.forward*(3.2f+k/12*.6f);if(Spot(seed,id,out feet))break;feet=home;}
                    facing=Quaternion.LookRotation(Flat(centre-feet));work=new[]{"Idle","FoldArms","Talk"};
                }
                else if(station=="break"){if(!Spot(home+Quaternion.Euler(0,id*60,0)*Vector3.forward*3.5f,id,out feet))feet=home;work=new[]{"FoldArms","Drink","Idle"};}
                else if(station=="dock")
                {
                    var shop=_c.TackleShop;
                    foreach(float side in new[]{0f,-1.2f,1.2f,-2.4f,2.4f})
                        if(Spot(shop.LandingPosition+shop.transform.right*side,id,out feet))break;else feet=home;
                    facing=Quaternion.LookRotation(Flat(shop.mooringPoint.position-feet));work=new[]{"Lantern","Idle"};
                }
                else if(cal.Night){work=new[]{"Lantern","Idle"};station="night";}
                brain.MoveTo(station,feet,facing,work,_forced[id]!=null && _forcedInstant);
            }
        }
        private static Vector3 Flat(Vector3 v){v.y=0;return v.sqrMagnitude>.01f?v:Vector3.forward;}
        // Scripted two-person exchanges for festival evenings: (speaker,line) then (listener,reply).
        private static readonly int[,] Chats={{0,1,1,1},{0,2,2,1},{1,2,3,1},{2,2,5,1}};
        /// <summary>Start festival exchange k (0..3) now; validation and scripted scenes.</summary>
        public void StartChat(int k){k=Mathf.Clamp(k,0,Chats.GetLength(0)-1);StartCoroutine(Chat(Chats[k,0],Chats[k,1],Chats[k,2],Chats[k,3]));}
        private System.Collections.IEnumerator Chat(int a,int la,int b,int lb)
        {
            var A=Brain(a);var B=Brain(b);if(A==null || B==null || A.Station!="festival" || B.Station!="festival")yield break;
            A.FacePartner(B.transform.position);B.FacePartner(A.transform.position);
            A.ShowBark("chat_"+la);var clip=FishingResidentLines.Clip(a+"/chat_"+la);
            yield return new WaitForSeconds((clip!=null?clip.length:2)+.5f);
            if(B!=null && !(_c.DialogueOpen && ActiveId==b))B.ShowBark("chat_"+lb);
        }
        private AudioSource Voice(int id)
        {
            var go=new GameObject("Voice",typeof(AudioSource));go.transform.SetParent(_people[id],false);go.transform.localPosition=Vector3.up*1.55f;
            var source=go.GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;
            source.minDistance=1.5f;source.maxDistance=14;source.dopplerLevel=0;source.priority=64;source.spread=30;
            _c.RegisterAudioSource(source,FishingAudioCategory.Voice,.5f); // voice lines are -16 LUFS, ~6dB hotter than effects
            return _voices[id]=source;
        }
        /// <summary>
        /// 潮商·阿汛 for the sea market ship (sea-events). Stands at <paramref name="local"/> on <paramref name="deck"/>, idles,
        /// looks at the player and speaks voiced lines via brain.ShowBark("hail_1"/"hail_2"/"deal_1"/"deal_2"/"bye"/"season_N").
        /// </summary>
        public static FishingResidentBrain SpawnMerchant(FishingPrototype c,Transform deck,Vector3 local,Quaternion rotation)
        {
            var prefab=Resources.Load<GameObject>("FishingV044/Residents/Resident6");
            if(prefab==null)throw new System.InvalidOperationException("Missing merchant model; run Tools/Art/build_v044_residents.py");
            var t=new GameObject().transform;t.SetParent(deck,false);Instantiate(prefab,t,false).name="Body";t.localPosition=local;t.localRotation=rotation;t.name="潮商·阿汛";
            var go=new GameObject("Voice",typeof(AudioSource));go.transform.SetParent(t,false);go.transform.localPosition=Vector3.up*1.55f;
            var source=go.GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;
            source.minDistance=1.5f;source.maxDistance=14;source.dopplerLevel=0;source.priority=64;source.spread=30;
            c.RegisterAudioSource(source,FishingAudioCategory.Voice,.5f);
            var brain=t.gameObject.AddComponent<FishingResidentBrain>();brain.Build(c,6,source);return brain;
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
                if (!InRange(i)) continue;
                float distance = Vector3.Distance(_c.ExplorerPosition, _people[i].position);
                if (distance < best) { best = distance; id = i; }
            }
            return id >= 0;
        }
        public bool Talk(int id)
        {
            if (_c.Paused || _c.StoryBlocksGameplay || _c.InventoryOpen || _c.JournalOpen || _c.HasPendingCatch || _c.WorldActionBusy
                || _c.Shore.Swimming || _c.Shore.Climbing || _c.Realms.Transitioning || !InRange(id)) return false;
            if (_c.ShopOpen && id != 4) return false;
            bool first=!_c.Career.Lore.Met(id);
            _c.TackleShop.Close(); _c.Career.Lore.Meet(id); if(id==5)_c.Career.AcceptMayorship(); _c.Save();
            ActiveId = id; _asking=false; _c.StoryModalChanged();
            if(id==4){_cashier.Begin(first);Say(_cashier.Speech(_c.Career,XRSettings.isDeviceActive));}else Say(Greeting(id,first));
            if (XRSettings.isDeviceActive) _c.XRNotebook.Open(8);
            return true;
        }
        public string Choice(int choice) => ActiveId==4?_cashier.Choice(choice):_asking?(choice==0?Line(ActiveId,"opt_ask1"):choice==1?Line(ActiveId,"opt_ask2"):"换个话题"):choice == 0 ? "聊聊这里的故事" : choice == 1 ? (ActiveId == 5 ? "汇报航记 / 补领遗物" : _c.Career.Lore.Cleared(ActiveId) && !_c.Career.Lore.RewardClaimed(ActiveId)?"领取遗物与家园蓝图":"询问秘境线索") : "航海与探索建议";
        public void Choose(int choice)
        {
            if (!Open || choice < 0 || choice >= ChoiceCount || !_c.Ashore || _c.Paused || !InRange(ActiveId)) return;
            if(ActiveId==4)
            {
                var action=_cashier.Choose(choice);
                if(action==CashierConversationAction.BrowseGoods){BrowseCashierGoods();return;}
                if(action==CashierConversationAction.NavigateHome){Close();_homeNavigationFrame=Time.frameCount;return;}
                Say(_cashier.Speech(_c.Career,XRSettings.isDeviceActive));return;
            }
            int id = ActiveId;bool xr=XRSettings.isDeviceActive;
            if(_asking)
            {
                _asking=false;
                if(choice<2){Say(Line(id,choice==0?"ask_r1":"ask_r2"));if(choice==0)Brain(id)?.React("Nod");}
                else Say(Line(id,"tip"));
                return;
            }
            if (choice == 0) Say(Story(id));
            else if (choice == 1)
            {
                if (id == 5)
                {
                    int pay = _c.Career.CompleteEchoVoyage();
                    if (pay > 0) { _c.Save(); _c.Regions.Show("潮汐回响 · 航路重生", "四段记忆已经归港  ·  +180 金币", 3); }
                    string gifts=ClaimPendingGifts();
                    string summary=pay > 0 ? Line(5,"echo_done")+"\n\n航路奖励 +180 金币。"
                        : _c.Career.Lore.Finale ? Line(5,"echo_finale")
                        : Line(5,"echo_progress")+"\n\n已带回 " + _c.Career.Lore.ClearCount + " / 4 段回响。";
                    Say(summary+"\n\n"+gifts+"\n"+_c.Career.MainObjective);
                }
                else if(id<4 && _c.Career.Lore.Cleared(id) && !_c.Career.Lore.RewardClaimed(id))Say(Line(id,"gift")+"\n\n"+_c.Voyage.ClaimRealmReward(id));
                else Say(id == 4 ? "这里可以买钓竿、鱼线和卷线器升级，也能升级船舱、载重和操舵。\n\n探索用品有镐、猎矛、望远镜、提灯和绳索；制作材料与远航口粮在补给分类里。船舶升级立即生效，工具购入背包后"+(xr?"用B查看，点选换入快捷位；Y换装备，右扳机使用。":"按I装备、F使用。")+"\n\n点“查看商品”可看价格。金币不足或背包满时不会扣款。" : Line(id,"clue")+"\n\n"+FishingLore.TrialHint(id) + "\n\n岛上潮纹石门通向" + FishingLore.ShrineName(id) + "。机关答错不收费，随时可以从入口返回。");
            }
            else Say((id==4?"":Line(id,"tip")+"\n\n")+(id == 0 ? "风铃岛的瞭望绳梯可以攀登。"+(xr?"靠近按A，左摇杆前后控制上下，到顶或到底后离开。":"靠近绳梯按E，W/S控制上下，空格离开。")+"登高后能看见那片月牙海湾。"
                : id == 1 ? (xr?"离岸后用左摇杆游动，靠近低岸继续向前可上岸；体力不足会变慢，停下就会恢复。":"离岸可以游泳，Shift加速会消耗体力。靠近低岸，朝岸边走并按空格就能上岸；体力不足会变慢，停下就会恢复。")
                : id == 2 ? "打捞台的绳梯通往高处。沉潮机库里那只货箱有三处位置：起点、中间、承重台。别把它推过头。"
                : id == 3 ? "星轨圣所的望远镜能辨认远处轮廓。留意石台刻痕，再仔细看天边和海面；找到相应景物后稳住镜头，让模糊的轮廓慢慢清晰。"
                : id == 4 ? (xr?"先升级操舵会让靠岸更轻松。左扳机制动，左摇杆后拉倒航；按X下船前先降至5.4公里每小时以下。B → 地图可以设置四岛航点。":"先升级操舵会让靠岸更轻松。Shift刹车，S倒航；G下船前先把航速降到5.4公里每小时以下。J日志能直接设置四岛航点。")
                : "捕鱼、委托、旧航标与秘境共享金币。你可以按任意顺序探索；R随时救援回港，鱼获和已完成的故事会保留。"));
        }
        private static string Line(int id,string key)=>FishingResidentLines.Text(id,key);
        private void Say(string text)
        {
            Speech = text; _spokenAt = Time.unscaledTime;_voicedLength=0;_voicedSeconds=0;
            var clip=FishingResidentLines.Clip(FishingResidentLines.VoiceKey(text));var source=VoiceSource(ActiveId);
            if(clip!=null && source!=null)
            {
                source.Stop();source.clip=clip;source.Play();
                int cut=text.IndexOf("\n\n",System.StringComparison.Ordinal);_voicedLength=cut<0?text.Length:cut;_voicedSeconds=Mathf.Max(.5f,clip.length-.3f);
            }
            else _c.WorldAudio?.Play("UIConfirm",_c.ExplorerPosition,.26f);
        }
        /// <summary>Recording key of the current speech's first paragraph, or null when it is not voiced.</summary>
        public string SpeechVoiceKey=>FishingResidentLines.VoiceKey(Speech);
        public bool BrowseCashierGoods()
        {
            if(!Open || ActiveId!=4 || _c.Paused || !InRange(4))return false;
            Close();
            if(XRSettings.isDeviceActive){_c.XRNotebook.Open(4);return _c.XRNotebook.StorePage;}
            return _c.ToggleShop();
        }
        public void Reveal() { if (Open) _spokenAt = Time.unscaledTime - 100; }
        public void Close()
        {
            if (!Open) return;
            int id=ActiveId;
            ActiveId = -1; _asking=false; _c.XRNotebook?.Close(); _c.StoryModalChanged();
            // A short voiced goodbye while the player is still close; the brain then returns to work.
            var bye=id==4?null:FishingResidentLines.Clip(id+"/bye");var source=VoiceSource(id);
            if(bye!=null && source!=null && _c.Ashore && Vector3.Distance(_c.ExplorerPosition,_people[id].position)<4){source.Stop();source.clip=bye;source.Play();}
        }
        private string Greeting(int id,bool first)
        {
            if(id==4)return "";
            var lore=_c.Career.Lore;int stage=FishingResidentLines.Stage(lore);
            if(!first && FestivalEvening(_c.Career.Calendar) && FishingResidentLines.Has(id,"fest"))return Line(id,"fest");
            if(!first && id==5 && !_keeperThanked && _c.Career.Lore.KeeperFlag(FishingTide.ArrivedBit+5)){_keeperThanked=true;return Line(5,"keeper");}
            if(!first && id==5 && _c.Career.Calendar.Night && Brain(5)?.Station=="dock")return Line(5,"night");
            string text=Line(id,first?"greet_first":id<4 && lore.Cleared(id) && stage<3?"greet_own":"greet_s"+stage);
            return id==5?text+"\n\n"+_c.Career.MainObjective:text;
        }
        // Each ask moves the talk on: story beats in order, one question per visit, the closing beat once all lamps are lit.
        private string Story(int id)
        {
            if(id==4)return "";
            var beats=FishingResidentLines.Stage(_c.Career.Lore)>=3?new[]{"story_end","story_a","story_b","story_c"}:new[]{"story_a","story_b","story_c"};
            int n=_storyIndex[id]++;
            if(n==1 && !_asked[id] && FishingResidentLines.Has(id,"ask")){_asked[id]=true;_asking=true;return Line(id,"ask");}
            var text=Line(id,beats[(_asked[id] && n>1?n-1:n)%beats.Length]);
            return id==5?text+"\n\n"+_c.Career.MainObjective:text;
        }
        private string ClaimPendingGifts()
        {
            if(!_c.Career.Lore.ChartFound)return "先回家阅读航线书桌上的秘密旧海图，守望者会为你保留遗物。";
            int claimed=0,pending=0;
            for(int i=0;i<4;i++)if(_c.Career.Lore.Cleared(i) && !_c.Career.Lore.RewardClaimed(i))
            {if(_c.Career.ClaimShrineReward(i))claimed++;else pending++;}
            if(claimed>0)_c.Save();
            return claimed>0?"已补领 "+claimed+" 份四境遗物、材料与永久设施蓝图。"+(pending>0?"还有 "+pending+" 份礼包待领，请为背包留出空位。":"")
                :pending>0?"背包空间不足，待领礼包完整保留。":"已领取的遗物和设施蓝图会一直保留。";
        }
        private void LateUpdate()
        {
            if(_homeNavigationFrame>=0 && Time.frameCount>_homeNavigationFrame)
            {
                _homeNavigationFrame=-1;
                if(_c!=null && !_c.Paused && !_c.StoryBlocksGameplay && !_c.InventoryOpen)_c.NavigateHome();
            }
            // v044: no whole-body swivel toward the player; FishingResidentBrain owns head look-at and facing.
            if(_c==null || _c.Paused || _c.Career==null)return;
            if(!FreezeSchedule && Time.time>_scheduleAt){_scheduleAt=Time.time+2;Schedule();}
            if(FestivalEvening(_c.Career.Calendar) && Time.time>_chatAt && !_c.DialogueOpen)
            {
                _chatAt=Time.time+Random.Range(18f,30f);int k=_chatTurn++%Chats.GetLength(0);
                var a=Brain(Chats[k,0]);
                if(a!=null && Vector3.Distance(a.transform.position,_c.ExplorerPosition)<FishingResidentBrain.BarkRange)StartCoroutine(Chat(Chats[k,0],Chats[k,1],Chats[k,2],Chats[k,3]));
            }
        }
    }
}
