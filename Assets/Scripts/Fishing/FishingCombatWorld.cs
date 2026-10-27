using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Object=UnityEngine.Object;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingCombatWorld Combat { get; private set; }
    }

    /// <summary>Optional encounters stay on the marked reef island; home and story routes remain safe.</summary>
    public sealed class FishingCombatWorld : MonoBehaviour
    {
        private sealed class Enemy
        {
            public Transform Root;
            public Collider Hitbox;
            public TextMesh Label;
            public int Health,Maximum;
            public bool Boss,Defeated,Struck;
            public float Clock,FootOffset;
            public int Phase;
            public Vector3 Dash;
            public LineRenderer Ring;
        }
        private FishingPrototype _c;
        private FishingCareer _career;
        private readonly List<Object> _owned=new List<Object>();
        private readonly Enemy[] _enemies=new Enemy[2];
        private Transform _board,_trophy;
        private TextMesh _boardText;
        private Material _warning;
        private float _hurtUntil,_spearReady;
        public int Health { get; private set; }=100;
        public int GuardianHealth=>_enemies[0]?.Health??0;
        public bool Challenging { get; private set; }
        public Vector3 Arena=>FishingCoastalPresentation.Islets[1];
        public bool InDangerZone=>_c!=null && _c.Ashore && !_c.Shore.Swimming && !_c.Shore.Climbing && !_c.Realms.Inside && FishingPrototype.FlatDistance(_c.ExplorerPosition,Arena)<23;
        public string Status=>Challenging?"礁蟹守卫 · "+_enemies[0].Health+" / "+_enemies[0].Maximum+"   生命 "+Health+" / 100"
            :InDangerZone && _c.Career.Calendar.Night?"礁影危险区 · 夜间有礁蟹出没   生命 "+Health+" / 100":"";
        private bool Active=>_c!=null && !_c.Paused && (_c.Automated || Application.isFocused) && !_c.InventoryOpen && !_c.JournalOpen
            && !_c.ShopBlocksGameplay && !_c.StoryBlocksGameplay && !_c.WorldActionBlocksGameplay
            && !_c.DockConsole.BlocksGameplay && !_c.XRNotebook.BlocksGameplay
            && (!XRSettings.isDeviceActive || (!_c.XRSeat.HeadBlocked && FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out _,out _)));

        public void Build(FishingPrototype c)
        {
            _c=c;_career=c.Career;
            var p=Arena+new Vector3(0,FishingCoastalPresentation.Height(0,15,1),15);
            if(c.Shore.Ground(p,out var boardFloor))p=boardFloor;
            _board=FishingArt.Shape("礁影试炼告示 · 自选挑战",PrimitiveType.Cube,transform,p+Vector3.up*.85f,new Vector3(1.6f,1.1f,.12f),c.wood,true).transform;
            _boardText=Text(_board,"萤火沙洲 · 礁影试炼\n白天可自选挑战守卫；夜间外礁有小蟹\n红圈蓄力时离开落点，使用猎枪或猎矛",new Vector3(0,0,.52f),.012f);
            _boardText.transform.localRotation=Quaternion.Euler(0,180,0);
            var boardScale=_board.localScale;_boardText.transform.localScale=new Vector3(1/boardScale.x,1/boardScale.y,1/boardScale.z);
            _warning=new Material(c.PaletteShader);_warning.SetFloat("_Unlit",1);_warning.SetColor("_BaseColor",new Color(1,.22f,.08f));_owned.Add(_warning);
            for(int i=0;i<_enemies.Length;i++)_enemies[i]=CreateEnemy(i==0);
            var trophy=FishingArt.Fish(transform,_owned,c.PaletteShader,FishKind.Crab,true);
            trophy.name="家园 · 礁蟹守卫纪念像";_trophy=trophy.transform;
            _trophy.position=c.Home.Origin+new Vector3(2,1.55f,4.2f);_trophy.localScale=Vector3.one*.8f;
            _trophy.gameObject.SetActive(false);
            Physics.SyncTransforms();
        }
        private TextMesh Text(Transform parent,string value,Vector3 local,float scale)
        {
            var go=new GameObject("Encounter label",typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.localPosition=local;
            var text=go.GetComponent<TextMesh>();text.text=value;text.font=FishingUIFont.Font;text.fontSize=48;text.characterSize=scale;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(1,.94f,.75f);
            if(text.font!=null)go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            return text;
        }
        private Enemy CreateEnemy(bool boss)
        {
            var go=FishingArt.Fish(transform,_owned,_c.PaletteShader,FishKind.Crab,boss);go.name=boss?"礁蟹守卫":"夜行礁蟹";
            var bounds=go.GetComponent<MeshFilter>().sharedMesh.bounds;
            float scale=(boss?3.6f:1.25f)/Mathf.Max(.1f,Mathf.Max(bounds.size.x,bounds.size.z));go.transform.localScale=Vector3.one*scale;
            var collider=go.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;collider.isTrigger=true;
            var enemy=new Enemy{Root=go.transform,Hitbox=collider,Boss=boss,Maximum=boss?450:75,FootOffset=-bounds.min.y*scale};enemy.Health=enemy.Maximum;
            enemy.Label=Text(go.transform,boss?"礁蟹守卫":"夜行礁蟹",new Vector3(0,bounds.max.y+.3f/scale,0),.03f/scale);
            var ring=new GameObject("蓄力危险圈",typeof(LineRenderer)).GetComponent<LineRenderer>();ring.transform.SetParent(transform,false);
            ring.sharedMaterial=_warning;ring.useWorldSpace=true;ring.loop=true;ring.positionCount=48;ring.startWidth=ring.endWidth=.07f;ring.enabled=false;enemy.Ring=ring;
            go.SetActive(false);return enemy;
        }
        private bool NearBoard=>Active && _c.Ashore && !_c.Shore.Swimming && !_c.Shore.Climbing && !_c.Realms.Inside
            && FishingPrototype.FlatDistance(_c.ExplorerPosition,_board.position)<2.5f
            && _c.Shore.ClearSight(_c.Shore.Character.EyePosition,_board.position+Vector3.up*.65f);
        public string NearbyHint=>!NearBoard?null:_c.Career.GuardianRewardPending?"礁蟹守卫 · 领取战利品":_c.Career.NightRewardPending?"礁影试炼 · 领取礁甲"
            :Challenging?"礁影试炼 · 结束本次挑战":_c.Career.CanChallengeGuardian?"礁影试炼 · 自选挑战守卫":"守卫正在休息 · 稍后再挑战";
        public bool TryInteract()
        {
            if(!NearBoard)return false;
            if(_c.Career.GuardianRewardPending){ClaimBoss();return true;}
            if(_c.Career.NightRewardPending){ClaimNight();return true;}
            if(Challenging){EndChallenge("已结束试炼，装备和物品全部保留。");return true;}
            if(!_c.Career.CanChallengeGuardian){_c.Notice("守卫休息约两分钟；可先钓鱼、探索或整理战利品。",4);return true;}
            Health=100;Challenging=true;Spawn(_enemies[0],new Vector3(0,0,1));
            _c.Notice("礁蟹守卫苏醒 · 红圈蓄力后冲撞，侧移躲开；猎矛近战或猎枪射击。返回告示可结束试炼。",7);return true;
        }
        private void Spawn(Enemy enemy,Vector3 offset)
        {
            enemy.Health=enemy.Maximum;enemy.Defeated=false;enemy.Clock=0;enemy.Phase=0;enemy.Struck=false;
            var p=Arena+offset;p.y=FishingCoastalPresentation.Height(offset.x,offset.z,1);
            if(_c.Shore.Ground(p,out var floor))p=floor;
            enemy.Root.position=p+Vector3.up*enemy.FootOffset;enemy.Root.rotation=Quaternion.identity;enemy.Root.gameObject.SetActive(true);enemy.Hitbox.enabled=true;enemy.Ring.enabled=false;
            enemy.Label.text=(enemy.Boss?"礁蟹守卫":"夜行礁蟹")+"  "+enemy.Health+" / "+enemy.Maximum;
        }
        public bool IsTarget(Collider target)
        {foreach(var e in _enemies)if(e!=null && e.Root.gameObject.activeInHierarchy && !e.Defeated && e.Hitbox==target)return true;return false;}
        public bool Hit(Collider target,int damage,Vector3 eye,Vector3 contact)
        {
            if(!ReferenceEquals(_career,_c.Career) || !Active || !InDangerZone || damage<=0 || !_c.Shore.ClearSight(eye,contact))return false;
            foreach(var e in _enemies)
            {
                if(e.Hitbox!=target || !e.Root.gameObject.activeInHierarchy || e.Defeated)continue;
                e.Health=Mathf.Max(0,e.Health-Mathf.Min(100,damage));_c.WorldAudio?.Play("AnimalHit",contact,.5f);
                e.Label.text=(e.Boss?"礁蟹守卫":"夜行礁蟹")+"  "+e.Health+" / "+e.Maximum;
                if(e.Health==0)
                {
                    e.Defeated=true;e.Hitbox.enabled=false;e.Ring.enabled=false;e.Root.gameObject.SetActive(false);
                    if(e.Boss){Challenging=false;_c.Career.MarkGuardianDefeated();_c.Save();ClaimBoss();}
                    else {_c.Career.MarkNightCreatureDefeated();_c.Save();ClaimNight();}
                }
                return true;
            }
            return false;
        }
        private void ClaimNight()
        {
            if(_c.Career.ClaimNightCreatureReward()){_c.Save();_c.Notice("夜行礁蟹掉落 · 礁甲碎片 ×1，可制作诱捕笼。",4);}
            else _c.Notice("礁甲已保留 · 整理背包后到试炼告示补领。",4);
        }
        private void ClaimBoss()
        {
            if(_c.Career.ClaimGuardianReward())
            {_c.Save();_c.Notice(_c.Career.GuardianWins==1?"守卫首胜 · 守卫珠 ×1、礁甲 ×6、120币；工作台可制作家园纪念像。":"试炼完成 · 礁甲 ×2、20币。",7);}
            else _c.Notice("战利品已保留 · 整理背包后到试炼告示领取。",5);
        }
        public bool TrySpear()
        {
            if(!Active || !InDangerZone || _c.Career.Backpack.Equipped!=FieldItem.HuntingSpear || Time.time<_spearReady)return false;
            var eye=_c.Shore.Character.EyePosition;
            var forward=XRSettings.isDeviceActive && _c.Hands.TryControllerPalm(false,out var palm,out var hand)?hand*Vector3.forward:_c.viewCamera.transform.forward;
            foreach(var e in _enemies)
            {
                var target=e.Root.position+Vector3.up*.3f;var delta=target-eye;
                if(!e.Root.gameObject.activeInHierarchy || e.Defeated || delta.magnitude>2.4f || Vector3.Dot(forward,delta.normalized)<.7f)continue;
                _spearReady=Time.time+.65f;_c.Hands.ReachGesture(target);return Hit(e.Hitbox,60,eye,target);
            }
            return false;
        }
        private void Update()
        {
            if(_c==null)return;
            if(!ReferenceEquals(_career,_c.Career))
            {_career=_c.Career;EndChallenge(null);_spearReady=_hurtUntil=0;}
            bool trophyOwned=_c.Career.Backpack.Count(FieldItem.GuardianTrophy)+_c.Career.HomeStorage.Count(FieldItem.GuardianTrophy)>0;
            _trophy.gameObject.SetActive(trophyOwned);
            if(!Active)return;
            _c.Career.TickCombat(Time.deltaTime,false);
            if(!InDangerZone)
            {
                if(Challenging)EndChallenge("已离开试炼区，本次挑战结束。");
                _enemies[1].Root.gameObject.SetActive(false);_enemies[1].Ring.enabled=false;Health=100;return;
            }
            var night=_enemies[1];
            if(!_c.Career.Calendar.Night || Challenging){night.Root.gameObject.SetActive(false);night.Ring.enabled=false;}
            else if(!night.Root.gameObject.activeSelf && _c.Career.CanChallengeNight)Spawn(night,new Vector3(12,0,1));
            foreach(var e in _enemies)if(e.Root.gameObject.activeSelf && !e.Defeated)TickEnemy(e,Mathf.Min(Time.deltaTime,.05f));
        }
        private void TickEnemy(Enemy e,float dt)
        {
            e.Clock+=dt;var p=e.Root.position;var player=_c.ExplorerPosition;var delta=Vector3.ProjectOnPlane(player-p,Vector3.up);
            e.Label.transform.rotation=Quaternion.LookRotation(_c.viewCamera.transform.position-e.Label.transform.position);
            float windup=e.Boss && e.Health<e.Maximum/2?.6f:.95f;
            if(e.Phase==0)
            {
                if(delta.sqrMagnitude>2.5f*2.5f)Move(e,delta.normalized*dt*(e.Boss?1.5f:1.1f));
                if(e.Clock>1.8f && delta.sqrMagnitude<9*9){e.Phase=1;e.Clock=0;e.Dash=Vector3.ClampMagnitude(delta,7);e.Struck=false;}
            }
            else if(e.Phase==1)
            {
                e.Ring.enabled=true;var center=p+e.Dash;
                for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;var mark=center+new Vector3(Mathf.Cos(a)*1.9f,0,Mathf.Sin(a)*1.9f);if(_c.Shore.Ground(mark,out var floor))mark.y=floor.y+.06f;e.Ring.SetPosition(i,mark);}
                if(e.Clock>=windup){e.Phase=2;e.Clock=0;}
            }
            else if(e.Phase==2)
            {
                Move(e,e.Dash*dt/.4f);
                if(!e.Struck && FishingPrototype.FlatDistance(e.Root.position,player)<1.9f && _c.Shore.ClearSight(e.Root.position+Vector3.up*.5f,player+Vector3.up*.5f))
                {e.Struck=true;Hurt(e.Boss?25:12);}
                if(e.Clock>=.4f){e.Phase=3;e.Clock=0;e.Ring.enabled=false;}
            }
            else if(e.Clock>=1.4f){e.Phase=0;e.Clock=0;}
            if(delta.sqrMagnitude>.02f)e.Root.rotation=Quaternion.Slerp(e.Root.rotation,Quaternion.LookRotation(delta),dt*4);
        }
        private void Move(Enemy e,Vector3 step)
        {
            var target=e.Root.position+step;target.y-=e.FootOffset;
            if(FishingPrototype.FlatDistance(target,Arena)>21 || !_c.Shore.Ground(target,out var floor) || Mathf.Abs(floor.y-(e.Root.position.y-e.FootOffset))>1)return;
            e.Root.position=floor+Vector3.up*e.FootOffset;
        }
        private void Hurt(int damage)
        {
            if(Time.time<_hurtUntil)return;
            _hurtUntil=Time.time+1;Health=Mathf.Max(0,Health-damage);_c.WorldAudio?.Play("AnimalHit",_c.ExplorerPosition,.35f);
            _c.Shore.Character.PlayInteraction(true);
            _c.Notice("受到冲撞 · 生命 "+Health+" / 100；红圈出现后侧移离开落点。",2);
            if(Health>0)return;
            EndChallenge("试炼失利，已安全回港；物品与金币全部保留。");_c.ReturnToHarbor();
        }
        private void EndChallenge(string notice)
        {Challenging=false;Health=100;foreach(var e in _enemies){e.Root.gameObject.SetActive(false);e.Ring.enabled=false;}if(!string.IsNullOrEmpty(notice))_c.Notice(notice,5);}
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
