using UnityEngine;
using UnityEngine.InputSystem;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingCatchRig CatchRig { get; private set; }
        public FishingDockConsole DockConsole { get; private set; }
        public FishingXRSeat XRSeat { get; private set; }
        public FishingXRComfort XRComfort { get; private set; }
        public bool HasPendingCatch=>Career!=null && Career.HasPendingCatch;
        public Transform ActiveFish=>_fish.transform;
        public Transform LandingNet=>_net;
        public Transform FishBox=>_box.transform;
        public Vector3 BoatPoint(Vector3 point)=>Player.transform.position+Quaternion.Euler(0,Player.transform.eulerAngles.y,0)*point;
        public Vector3 LandingTarget=>UnityEngine.XR.XRSettings.isDeviceActive && viewCamera!=null?HandLandingPoint(viewCamera.transform):LandingTargetForMode(false);
        /// <summary>In VR the landed fish hangs on the line half a metre in front of the face, just below eye level, to be taken by hand.</summary>
        public static Vector3 HandLandingPoint(Transform head)=>head.position+Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized*.5f-Vector3.up*.15f;
        public Vector3 LandingTargetForMode(bool xr)=>BoatPoint(new Vector3(xr?-1.15f:1.5f,.93f,.55f));
        public Vector3 NetTray=>BoatPoint(new Vector3(.55f,.99f,-.28f));
        internal Vector3 StowedNetPosition=>new Vector3(-.8f,.70f,-.55f);
        internal Quaternion StowedNetRotation=>Quaternion.Euler(0,Mathf.Lerp(85f,0f,_netDeployment),-Mathf.Sin(_netMotion*Mathf.PI)*35f);
        public bool BeginNetCatch()=>BeginLandedCatch(false);
        public bool BeginHandCatch()=>BeginLandedCatch(true);
        private bool BeginLandedCatch(bool leftHand)
        {
            if(Paused || !Anchored || HasPendingCatch || !Session.LandFromNet())return false;
            var fish=Session.Claim();var p=Player.engine.RB.position;
            if(fish!=null)
            {
                var find=FishingCoastRules.SeaFind(fish,Career.Discovered(fish.kind,fish.rare));
                if(find!=FieldItem.Empty && Career.CollectSeaFind(fish,find))
                {
                    _caughtAt=-100;_fish.SetActive(false);CoastWorld.ShowSeaFind(find);CatchRig.Recover();Save();
                    LastCatch=new CatchCard{Find=find,ShownAt=Time.unscaledTime};
                    _feedback.PlayOneShot(find==FieldItem.SeaTreasure?_legendTone:_successTone);
                    Effects?.Burst(LandingTarget,find==FieldItem.SeaTreasure?new Color(1,.79f,.37f):new Color(.46f,.82f,.80f));
                    Notice(FishingCoastRules.FindQuip(find)+" 已放入背包。",7,true);
                    return true;
                }
            }
            if(fish==null || !Career.BeginPending(fish,_castGround,p.x,p.z))
            {Debug.LogError("[CATCH] Failed to stage a new unique fish");return false;}
            Save();
            bool special=FishingSpecies.Of(fish.kind).Legendary || fish.rare || !Career.Discovered(fish.kind,fish.rare) || fish.grams>Career.BestGrams(fish.kind);
            LastCatch=new CatchCard{Fish=fish.Copy(),ShownAt=Time.unscaledTime,Discovery=!Career.Discovered(fish.kind,fish.rare),PersonalBest=Career.BestGrams(fish.kind)>0 && fish.grams>Career.BestGrams(fish.kind)};
            _feedback.PlayOneShot(FishingSpecies.Of(fish.kind).Legendary?_legendTone:_successTone);
            Effects?.Splash(_fish.transform.position,1.5f);Effects?.Burst(_fish.transform.position,new Color(1,.79f,.37f));
            WorldAudio?.Play("FishSplash",_fish.transform.position,.5f);
            if(special)WorldAudio?.Play("RareShimmer",_fish.transform.position,.6f);
            if(leftHand)
            {
                CatchRig.StartLeftInspection(_fish.transform.position);
                Notice("左手检视活鱼。E 放入抄网，之后可入箱或放生。",7);return true;
            }
            Notice(special?"特别收获！可以拿起观察测量；普通鱼也可直接倾网入箱。":"鱼已入网：可直接倾网入箱，或拿起观察。",7);return true;
        }
        public bool ResolveCatch(bool store)
        {
            if(Paused || !HasPendingCatch)return false;
            if(store && CatchRig.LeftHandInspecting){Notice("先按 E 将手里的活鱼放进抄网，再入鱼箱。",4);return false;}
            var p=Career.PendingCatch;bool legend=FishingSpecies.Of(p.fish.kind).Legendary;
            if(legend && store){MeasureCatch();Notice("传说鱼已测量。请确认放生领取纪念，不作为普通鱼售卖。",6);return false;}
            if(legend && !p.measured){Notice("先拿起传说鱼测量，再确认放生。",5);return false;}
            bool hadEvidence=Career.Expedition.Has(FishingExpedition.TargetCaught);
            int surveyBefore=FishingSurvey.Stage(Career);
            bool discovery=!Career.Discovered(p.fish.kind,p.fish.rare);int best=Career.BestGrams(p.fish.kind);
            if(!Career.ResolvePending(store,out var record,out int bounty))
            {Notice("鱼箱已满。鱼仍安全保留，可整理鱼箱或选择放生。",5);return false;}
            _caughtAt=store && !legend?Time.time:-100;_catchStart=_fish.transform.position;
            if(store && !legend)WorldAudio?.NotifyFishStored();
            if(!store)StartCoroutine(ReleaseFishVisual(record,_catchStart,_fish.transform.rotation,_fish.transform.localScale));
            LastCatch=new CatchCard{Fish=record.Copy(),ShownAt=Time.unscaledTime,Discovery=discovery,PersonalBest=best>0 && record.grams>best,Bounty=bounty,Released=!store};
            if(legend && _trophy==null)BuildTrophy();
            Effects?.Burst(_catchStart,new Color(.46f,.82f,.80f));RefreshHold();Save();_feedback.PlayOneShot(legend?_legendTone:_successTone);
            if(FishingSurvey.Stage(Career)>surveyBefore)Notice(FishingSurvey.Stage(Career)==1?"浅湾调查完成！北岛渔场已开放。J 查看下一组水道调查。":"北岛渔场与传说坐标已解锁！夕照、飞梭水道、珍珠饵、强化鱼线：条件齐备必遇金帆旗鱼。",10,false);
            else if(!hadEvidence && Career.Expedition.Has(FishingExpedition.TargetCaught))Notice("北岛捕获记录已收录！鱼货与主线凭证独立，返港可提交捕获记录。",9,false);
            else Notice(FishingSpecies.Name(record.kind)+" "+FishingText.Weight(record.grams)+(legend?" 已测量放生，赏金 +"+bounty:store?" 已放入鱼箱。":" 已放生；保留图鉴与体型记录，不获得售鱼金币。"),7,true);
            CatchRig.Recover();return true;
        }
        private System.Collections.IEnumerator ReleaseFishVisual(CatchRecord record,Vector3 start,Quaternion rotation,Vector3 scale)
        {
            // 只表现已成功提交的放生；没有 Rigidbody、库存操作或二次结算回调。
            var visual=MakeFish(transform,record.kind,record.rare);visual.name="Released fish visual";
            visual.transform.localScale=scale;
            var destination=UnityEngine.XR.XRSettings.isDeviceActive?start:BoatPoint(new Vector3(2f,0,.4f));
            destination.y=.04f;
            try
            {
                float elapsed=0;
                while(elapsed<.7f)
                {
                    if(!Paused)elapsed+=Time.deltaTime;
                    float u=Mathf.Clamp01(elapsed/.7f);
                    visual.transform.SetPositionAndRotation(Vector3.Lerp(start,destination,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.15f),rotation);
                    yield return null;
                }
                WorldAudio?.Play("FishSplash",destination,.55f);
            }
            finally{if(visual!=null)Destroy(visual);}
        }
        public bool MeasureCatch()
        {
            if(Paused || !HasPendingCatch)return false;
            if(Career.MeasurePending()){Save();Notice("测量："+FishingSpecies.Name(Career.PendingCatch.fish.kind)+" · "+FishingText.Weight(Career.PendingCatch.fish.grams),4);}
            return true;
        }
        public void MarkFishingOrder(){if(!Paused && AtHarbor && Career.MarkOrder()){Save();Notice("已标记当前捕鱼委托；按需求收集鱼货，回港交付。",5);}}
        public void RestorePendingFishVisual()
        {
            var pending=Career.PendingCatch;if(pending==null)return;
            _fish.SetActive(false);Destroy(_fish);_fish=MakeFish(transform,pending.fish.kind,pending.fish.rare);
            _fish.transform.localScale=Vector3.one*FishingSpecies.SizeScale(pending.fish.kind,pending.fish.grams);
            var body=_fish.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var collider=_fish.AddComponent<SphereCollider>();collider.radius=.18f;collider.isTrigger=true;
        }
        private void ReadPendingDesktop(Keyboard keyboard)
        {
            if(keyboard==null || Paused || _xrActive)return;
            if(keyboard.rKey.wasPressedThisFrame){ReturnToHarbor();return;}
            if(keyboard.eKey.wasPressedThisFrame)RequestInspectCatch();
            else if(keyboard.fKey.wasPressedThisFrame)RequestCatchResolution(true);
            else if(keyboard.xKey.wasPressedThisFrame)CatchRig.RequestRelease(true);
            else if(keyboard.cKey.wasPressedThisFrame)ToggleView();
            else if(keyboard.tabKey.wasPressedThisFrame)ToggleHarborDesk();
        }
    }
}
