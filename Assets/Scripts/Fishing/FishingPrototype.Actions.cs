using System;
using System.Text;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        private sealed class ActionRequest
        {
            public FishingCareer Profile;
            public FieldItem Tool;
            public int Slot;
            public bool Ashore,Crouched,Fish,Attempted,HadRoot,WaitForStance;
            public Transform ContactRoot;
            public Vector3 ContactLocal,ContactWorld,StartPalm,Lean,StartFish;
            public Quaternion ToolRotation;
            public float ContactFraction;
            public Func<bool> Commit;
        }
        public FishingActionClock Actions { get; } = new FishingActionClock();
        private ActionRequest _action;
        private int _actionContactFrame=-1,_actionConsumedFrame=-1;
        public bool WorldActionBusy=>_action!=null;
        public bool UsesFishingSeat=>Anchored || HasPendingCatch || (_action!=null && _action.Fish);
        public const float FishingSeatYaw=66f;
        public bool WorldActionBlocksGameplay=>WorldActionBusy || _actionConsumedFrame==Time.frameCount;
        public long LastActionId { get; private set; }
        public bool LastActionSucceeded { get; private set; }
        public int ActionCommitAttempts { get; private set; }
        public Vector3 ActionTargetPosition=>_action==null?Vector3.zero:_action.ContactRoot!=null?_action.ContactRoot.TransformPoint(_action.ContactLocal):_action.ContactWorld;
        public Vector3 ActionStartPalm=>_action==null?Vector3.zero:_action.StartPalm;
        public Quaternion ActionToolRotation=>_action==null?Quaternion.identity:_action.ToolRotation;
        public bool ActionUsesHeldTool=>_action!=null && Actions.Kind==FishingActionKind.Harvest && IsPick(_action.Tool);
        public bool ActionSuppressesHeldTool=>_action!=null && !ActionUsesHeldTool;
        public bool ActionMovesFish=>_action!=null && _action.Fish && HasPendingCatch;
        public float ActionContactBlend
        {
            get
            {
                if(_action==null)return 0;
                if(_actionContactFrame==Time.frameCount)return 1;
                float p=Actions.Progress,k=_action.ContactFraction;
                float weight=p<=k?p/Mathf.Max(.001f,k):(1-p)/Mathf.Max(.001f,1-k);
                return Mathf.SmoothStep(0,1,Mathf.Clamp01(weight));
            }
        }
        public Vector3 ActionFishPosition
        {
            get
            {
                if(_action==null)return Vector3.zero;
                var fish=ActiveFish;
                var grip=Quaternion.Inverse(fish.rotation)*fish.TransformVector(CatchRig.FishPalmLocal(false));
                var contactRoot=ActionTargetPosition-(_action.ToolRotation*Quaternion.Euler(0,-90,0))*grip;
                return Vector3.Lerp(_action.StartFish,contactRoot,Mathf.SmoothStep(0,1,Mathf.Clamp01(Actions.Progress/Mathf.Max(.001f,_action.ContactFraction))));
            }
        }
        private static bool IsPick(FieldItem item)=>item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe;

        private enum WorldTargetKind { None,Expedition,Station,Resource,Pickup,Survey }
        private struct WorldTarget
        {
            public WorldTargetKind Kind;public int Id;public Transform Root;
            public FishingWorkshopWorld.ResourceNode Resource;public FishingFieldWorld.Pickup Pickup;
        }
        private WorldTarget ResolveWorldTarget(bool refresh,StringBuilder trace=null)
        {
            WorldTarget best=default;float distance=float.MaxValue;
            if(!Ashore)return best;
            void Offer(WorldTarget value,Vector3 point)
            {
                float d=FlatDistance(ExplorerPosition,point);
                if(trace!=null)trace.Append(" [").Append(value.Kind).Append(':').Append(value.Id).Append(" at=").Append(point.ToString("F4")).Append(" distance=").Append(d.ToString("F4")).Append(']');
                // 同点/重叠标记保留先提出的剧情操作，但较近拾取不会被远处资源抢走。
                if(d<distance-.04f){distance=d;best=value;}
            }
            int expedition=Expedition.Resolve();
            if(expedition>=0)
            {
                var root=Expedition.InteractionRoot(expedition);
                if(root!=null)Offer(new WorldTarget{Kind=WorldTargetKind.Expedition,Id=expedition,Root=root},root.position);
            }
            if(WorkshopWorld.TryInteractionTarget(out var station,out var resource,refresh))
            {
                if(station>=0)
                {
                    var root=WorkshopWorld.StationRoot(station);if(root!=null)Offer(new WorldTarget{Kind=WorldTargetKind.Station,Id=station,Root=root},root.position);
                }
                else if(resource!=null)Offer(new WorldTarget{Kind=WorldTargetKind.Resource,Id=resource.Id,Root=resource.Root,Resource=resource},resource.Feet);
            }
            if(FieldWorld.TryPickupTarget(out var pickup,refresh))Offer(new WorldTarget{Kind=WorldTargetKind.Pickup,Id=pickup.Id,Root=pickup.Root,Pickup=pickup},pickup.Feet);
            if(refresh)Shore.UpdateProximity();int task=Shore.NearbyTask;var marker=Shore.SurveyMarker(task);
            if(marker!=null)Offer(new WorldTarget{Kind=WorldTargetKind.Survey,Id=task,Root=marker},marker.position);
            if(trace!=null)trace.Append(" categories: expedition=").Append(expedition).Append(" station=").Append(station)
                .Append(" resource=").Append(resource==null?-1:resource.Id).Append(" pickup=").Append(pickup==null?-1:pickup.Id).Append(" survey=").Append(task);
            return best;
        }
        public sealed class InteractionPreflight
        {
            public string TargetId="",Reason="",GeometryReason="",Candidates="",ArmBlocker="",ToolBlocker="";
            public Vector3 Actor,RootPosition,Contact,Palm,Shoulder,BaseShoulder,Lean;
            public float ArmReach,Distance,LeanNeed,DistanceAfterLean;
            public bool Grounded,Crouched,ArmSight,ToolSight,AutoCrouch;
            // 几何可达不代表可以起手；动作中检查另一候选时仍保留全部生产状态守卫。
            public bool GeometryReady=>GeometryReason.Length==0;
            public bool CanBegin=>Reason.Length==0;
            public override string ToString()=>"target="+TargetId+" eligible="+CanBegin+" reason="+Reason+" geometryReady="+GeometryReady+" geometryReason="+GeometryReason+" grounded="+Grounded+" crouched="+Crouched
                +" autoCrouch="+AutoCrouch+" actor="+Actor.ToString("F4")+" root="+RootPosition.ToString("F4")+" contact="+Contact.ToString("F4")+" palm="+Palm.ToString("F4")
                +" shoulder="+Shoulder.ToString("F4")+" baseShoulder="+BaseShoulder.ToString("F4")+" armReach="+ArmReach.ToString("F4")
                +" distance="+Distance.ToString("F4")+" leanNeed="+LeanNeed.ToString("F4")+" lean="+Lean.ToString("F4")+" afterLean="+DistanceAfterLean.ToString("F4")
                +" armSight="+ArmSight+" armBlocker="+ArmBlocker+" toolSight="+ToolSight+" toolBlocker="+ToolBlocker+" candidates="+Candidates;
        }
        /// <summary>只读当前感知缓存与几何；不移动角色、不刷新缓存、不启动时钟、保存或发布提示。</summary>
        public InteractionPreflight InspectWorldInteractionStart()
        {
            var trace=new StringBuilder();var target=ResolveWorldTarget(false,trace);
            bool shop=TackleShop!=null && TackleShop.NearCounter;
            trace.Append(" shopNear=").Append(shop).Append(" frame=").Append(Time.frameCount);
            InteractionPreflight check;
            if(shop || target.Kind==WorldTargetKind.None || target.Kind==WorldTargetKind.Station || target.Root==null)
            {
                check=new InteractionPreflight{TargetId=shop?"shop":target.Kind==WorldTargetKind.Station?"station:"+target.Id:"none",
                    Reason=shop || target.Kind==WorldTargetKind.Station?"当前候选是即时 UI，不是接触动作":"没有有效接触候选",
                    Actor=ExplorerPosition,RootPosition=target.Root==null?Vector3.zero:target.Root.position,
                    Shoulder=Shore.Character.ShoulderPosition(false),ArmReach=Shore.Character.ArmReach(false),Grounded=Shore.Grounded,Crouched=Shore.Crouched};
                check.BaseShoulder=check.Shoulder-Shore.Character.AppliedInteractionLean;check.GeometryReason=check.Reason;
            }
            else
            {
                Vector3 point;Quaternion rotation;FishingActionKind kind;string targetId;
                if(target.Kind==WorldTargetKind.Resource)
                {
                    HarvestContact(target.Resource,out point,out rotation);kind=FishingActionKind.Harvest;targetId="resource:"+target.Id;
                }
                else
                {
                    point=FishingContactGeometry.ClosestPoint(target.Root,Shore.Character.ShoulderPosition(false));rotation=Shore.Actor.rotation;
                    kind=target.Kind==WorldTargetKind.Pickup?FishingActionKind.Pickup:target.Kind==WorldTargetKind.Expedition && target.Id==4?FishingActionKind.Repair:FishingActionKind.Inspect;
                    targetId=(target.Kind==WorldTargetKind.Pickup?"pickup:":target.Kind==WorldTargetKind.Expedition?"expedition:":"survey:")+target.Id;
                }
                check=EvaluateActionStart(kind,target.Root,point,rotation,false);check.TargetId=targetId;
                if(target.Kind==WorldTargetKind.Resource)
                {
                    if(!target.Root.gameObject.activeInHierarchy)check.Reason="资源目标已经失效";
                    else if((target.Resource.Item==FieldItem.Coal || target.Resource.Item==FieldItem.IronOre) && !IsPick(Career.Backpack.Equipped))check.Reason="矿石需要镐";
                    else if(!Career.Workshop.ResourceReady(target.Id))check.Reason="资源正在恢复";
                }
            }
            check.Candidates=trace.ToString();return check;
        }
        private InteractionPreflight EvaluateActionStart(FishingActionKind kind,Transform root,Vector3 contact,Quaternion rotation,bool fish)
        {
            var character=Shore.Character;bool pick=kind==FishingActionKind.Harvest && IsPick(Career.Backpack.Equipped);
            var check=new InteractionPreflight{Actor=ExplorerPosition,RootPosition=root==null?contact:root.position,Contact=contact,
                Shoulder=character.ShoulderPosition(false),Grounded=Shore.Grounded,Crouched=Shore.Crouched,ArmReach=character.ArmReach(false)};
            check.BaseShoulder=check.Shoulder-character.AppliedInteractionLean;
            check.Palm=pick?FishingFieldWorld.PickContactPalm(contact,rotation):contact;
            // 使用当前输入最终对应的姿态；起身途中的瞬时可达不能冒充站姿可达。
            if(kind==FishingActionKind.Pickup && Ashore)check.BaseShoulder+=character.AshoreStanceOffset(Shore.Crouched);
            // 先保留当前可达且无遮挡的姿态，不能为台面物品强行蹲到栏杆后面。
            bool currentReachable=Vector3.Distance(check.BaseShoulder,check.Palm)<=check.ArmReach-.015f+.22f
                && ClearActionSight(check.BaseShoulder,check.Palm,root);
            if(kind==FishingActionKind.Pickup && Ashore && check.Grounded
                && !Shore.Crouched && !currentReachable && contact.y<check.BaseShoulder.y-.4f)
            {
                // 三个方向共用剩余蹲姿权重，起身中再次 E 不重复扣除后移量。
                var crouchedShoulder=check.Shoulder-character.AppliedInteractionLean+character.RemainingCrouchOffset;
                if(Vector3.Distance(crouchedShoulder,check.Palm)<=check.ArmReach-.015f+.22f)
                {check.BaseShoulder=crouchedShoulder;check.AutoCrouch=true;}
            }
            var delta=check.Palm-check.BaseShoulder;check.Distance=delta.magnitude;
            // 与原生产守卫保持同一臂长余量和 22cm 俯身上限，不为夹具放宽。
            check.LeanNeed=Mathf.Max(0,check.Distance-(check.ArmReach-.04f));
            check.Lean=delta.sqrMagnitude>.0001f?delta.normalized*Mathf.Min(.22f,check.LeanNeed):Vector3.zero;
            check.DistanceAfterLean=Vector3.Distance(check.BaseShoulder+check.Lean,check.Palm);
            check.ArmSight=ClearActionSight(check.BaseShoulder,check.Palm,root,out check.ArmBlocker);
            check.ToolSight=!pick || ClearActionSight(check.Palm,contact,root,out check.ToolBlocker);
            if(Ashore && !check.Grounded)check.GeometryReason="请落地站稳后再互动，低处物品可以蹲下触及。";
            else if(check.DistanceAfterLean>check.ArmReach-.015f)check.GeometryReason="超过真实可达范围，请靠近或蹲下";
            else if(!check.ArmSight || !check.ToolSight)check.GeometryReason="手臂/工具到目标的视线被阻挡";
            if(Paused || InventoryOpen || JournalOpen)check.Reason="暂停或背包/日志模态";
            else if(ShopBlocksGameplay)check.Reason="商店模态或开关帧";
            else if(WorldActionBlocksGameplay || Actions.Active || Actions.ContactPending)check.Reason="已有动作或动作消费帧";
            else if(_xrActive || Expedition.Aligning)check.Reason="XR 或信标校准占用输入";
            else if((!Ashore && !fish) || (HasPendingCatch && !fish))check.Reason="岸船/待处理鱼状态不允许";
            else if(!Hands.CanPresentTools)check.Reason="当前不能呈现交互手部";
            else check.Reason=check.GeometryReason;
            return check;
        }
        public string WorldInteractionTitle
        {
            get
            {
                if(WorldActionBusy)return "动作进行中 · "+Mathf.RoundToInt(Actions.Progress*100)+"% · Esc 取消";
                if(TackleShop!=null && TackleShop.NearCounter)return "E / Tab 潮汐钓具店 · 柜台购买升级";
                var target=ResolveWorldTarget(false);
                switch(target.Kind)
                {
                    case WorldTargetKind.Expedition:return "E "+Expedition.InteractionLabel(target.Id);
                    case WorldTargetKind.Station:
                        var station=Career.Workshop.Station(target.Id);return station==null?null:"E 打开"+FishingRecipes.StationName(station.kind);
                    case WorldTargetKind.Resource:return "E 采集 "+FishingBackpack.Name(target.Resource.Item);
                    case WorldTargetKind.Pickup:return "E 拾取 "+FishingBackpack.Name(target.Pickup.Item)+" ×"+target.Pickup.Count;
                    case WorldTargetKind.Survey:return "E "+FishingExploration.TaskName(target.Id);
                    default:return null;
                }
            }
        }
        /// <summary>真实输入与 HUD 使用同一候选选择；接触阶段只提交已经锁定的目标。</summary>
        public bool RequestWorldInteraction()
        {
            if(Paused || InventoryOpen || JournalOpen || ShopBlocksGameplay || WorldActionBlocksGameplay)return false;
            if(!Ashore){Interact();return true;}
            if(TackleShop!=null && TackleShop.NearCounter)return ToggleShop();
            var target=ResolveWorldTarget(true);
            if(target.Kind==WorldTargetKind.None){Notice("靠近可互动对象后按 E；低处物品可蹲下触及。",3);return false;}
            if(target.Kind==WorldTargetKind.Station)return WorkshopWorld.OpenStation(target.Id);
            if(target.Kind==WorldTargetKind.Resource)return RequestHarvest(target.Resource);
            var point=FishingContactGeometry.ClosestPoint(target.Root,Shore.Character.ShoulderPosition(false));int id=target.Id;
            if(target.Kind==WorldTargetKind.Expedition)
                return BeginWorldAction(id==4?FishingActionKind.Repair:FishingActionKind.Inspect,"expedition:"+id,target.Root,point,Shore.Actor.rotation,()=>
                {
                    bool had=Career.Expedition.Has(FishingExpedition.ComponentFound);bool ok=Expedition.InteractAt(id);
                    if(ok && !had && Career.Expedition.Has(FishingExpedition.ComponentFound))Hands.PresentPickup();return ok;
                });
            if(target.Kind==WorldTargetKind.Pickup)
            {
                int count=target.Pickup.Count;var item=target.Pickup.Item;
                return BeginWorldAction(FishingActionKind.Pickup,"pickup:"+id,target.Root,point,Shore.Actor.rotation,()=>FieldWorld.PickupById(id,item,count));
            }
            return BeginWorldAction(FishingActionKind.Inspect,"survey:"+id,target.Root,point,Shore.Actor.rotation,()=>Shore.SurveyTask(id));
        }
        public bool RequestHarvest()
        {
            if(!WorkshopWorld.TryHarvestTarget(out var node)){Notice("靠近可采资源后再使用工具。",3);return false;}
            return RequestHarvest(node);
        }
        private bool RequestHarvest(FishingWorkshopWorld.ResourceNode node)
        {
            if(node==null || node.Root==null || !node.Root.gameObject.activeInHierarchy)return false;
            var tool=Career.Backpack.Equipped;
            if((node.Item==FieldItem.Coal || node.Item==FieldItem.IronOre) && !IsPick(tool)){Notice("矿石需要手持石镐或铁镐。",3);return false;}
            if(!Career.Workshop.ResourceReady(node.Id)){Notice("这处资源正在恢复。",3);return false;}
            HarvestContact(node,out var contact,out var rotation);
            int id=node.Id;var item=node.Item;
            return BeginWorldAction(FishingActionKind.Harvest,"resource:"+id,node.Root,contact,rotation,()=>WorkshopWorld.HarvestNode(id,item,tool));
        }
        private void HarvestContact(FishingWorkshopWorld.ResourceNode node,out Vector3 contact,out Quaternion rotation)
        {
            contact=FishingContactGeometry.ClosestPoint(node.Root,Shore.Character.ShoulderPosition(false));
            var heading=Vector3.ProjectOnPlane(contact-Shore.Position,Vector3.up);
            rotation=(heading.sqrMagnitude>.001f?Quaternion.LookRotation(heading):Shore.Actor.rotation)*Quaternion.Euler(0,0,-110);
        }
        public bool RequestPlaceEquipped()
        {
            if(!WorkshopWorld.TryPlacement(1.02f,out var feet,out var yaw))return false;
            var item=Career.Backpack.Equipped;
            var facing=Quaternion.Euler(0,yaw,0);
            float handX=Mathf.Clamp((Quaternion.Inverse(facing)*(Shore.Character.ShoulderPosition(false)-feet)).x,-.5f,.5f);
            var contact=feet+Vector3.up*(item==FieldItem.Furnace?1.07f:.97f)+facing*new Vector3(handX,0,-.47f);
            bool began=BeginWorldAction(FishingActionKind.Place,"place:"+Guid.NewGuid().ToString("N"),null,contact,facing,
                ()=>WorkshopWorld.PlaceAt(feet,yaw,item));
            if(began)WorkshopWorld.SetPlacementPreview(feet,yaw,true);
            return began;
        }
        public bool RequestInspectCatch()
        {
            if(!HasPendingCatch || Paused || WorldActionBlocksGameplay)return false;
            Shore.SyncRider();
            Hands.GetToolFrame(out var origin,out var frame);
            string id=Career.PendingCatch.fish.id;
            bool began=BeginWorldAction(FishingActionKind.Inspect,"fish:"+id,null,origin+frame*new Vector3(.06f,-.16f,.27f),frame,
                ()=>HasPendingCatch && Career.PendingCatch.fish.id==id && MeasureCatch(),true);
            if(began)CatchRig.PrepareHandAction();return began;
        }
        public bool RequestCatchResolution(bool store)
        {
            if(!HasPendingCatch || Paused || WorldActionBlocksGameplay)return false;
            if(store && FishingSpecies.Of(Career.PendingCatch.fish.kind).Legendary)
            {MeasureCatch();Notice("传说鱼请测量后确认放生。",4);return false;}
            CatchRig.ConfigureLayout(false);
            // 布局变更可能触发刚体插值同步，随后再把人体放到同一船体参考系。
            Shore.SyncRider();
            string id=Career.PendingCatch.fish.id;
            // 松手点属于角色可达域，落水终点由独立的无碰撞表现处理，不能要求手臂伸到水面。
            Hands.GetToolFrame(out _,out var frame);
            var shoulder=Shore.Character.ShoulderPosition(false);
            var releasePoint=shoulder+(frame*new Vector3(.08f,-.10f,.3f)).normalized*Mathf.Max(.05f,Shore.Character.ArmReach(false)-.06f);
            var root=store?FishBox:Player.transform;
            var target=store?CatchRig.ReceivingPoint(shoulder):releasePoint;
            bool began=BeginWorldAction(store?FishingActionKind.Store:FishingActionKind.Release,"fish:"+id,root,target,
                store?Quaternion.Euler(0,Player.transform.eulerAngles.y,0):frame,()=>HasPendingCatch && Career.PendingCatch.fish.id==id && ResolveCatch(store),true);
            if(began)CatchRig.PrepareHandAction();return began;
        }
        private bool BeginWorldAction(FishingActionKind kind,string targetId,Transform root,Vector3 contact,Quaternion rotation,Func<bool> commit,bool fish=false)
        {
            var check=EvaluateActionStart(kind,root,contact,rotation,fish);
            if(!check.CanBegin){Notice(check.Reason,4);return false;}
            var shoulder=check.BaseShoulder;var lean=check.Lean;
            const float duration=.7f,contactTime=.36f;
            if(!Actions.TryBegin(kind,targetId,duration,contactTime))return false;
            if(check.AutoCrouch)Shore.SetCrouch(true);
            Hands.GetToolFrame(out _,out var frame);
            _action=new ActionRequest{Profile=Career,Tool=Career.Backpack.Equipped,Slot=Career.Backpack.Selected,Ashore=Ashore,Crouched=Shore.Crouched,Fish=fish,WaitForStance=check.AutoCrouch || (kind==FishingActionKind.Pickup && Ashore && !Shore.Character.IsStanceSettled(true,Shore.Crouched)),
                ContactRoot=root,HadRoot=root!=null,ContactLocal=root==null?Vector3.zero:root.InverseTransformPoint(contact),ContactWorld=contact,ToolRotation=rotation,Commit=commit,
                ContactFraction=contactTime/duration,StartPalm=shoulder+frame*new Vector3(.025f,-.18f,.20f),Lean=lean,StartFish=fish?ActiveFish.position:Vector3.zero};
            _actionContactFrame=-1;_actionConsumedFrame=Time.frameCount;LastActionSucceeded=false;LowerFieldTools();Shore.ResetMoveInput();
            return true;
        }
        private bool ClearActionSight(Vector3 from,Vector3 to,Transform target)=>ClearActionSight(from,to,target,out _);
        private bool ClearActionSight(Vector3 from,Vector3 to,Transform target,out string blocker)
        {
            blocker="";var d=to-from;if(d.sqrMagnitude<.0001f)return true;
            foreach(var hit in Physics.RaycastAll(from,d.normalized,d.magnitude,~(1<<4),QueryTriggerInteraction.Ignore))
                if(hit.collider!=Shore.Body && !hit.transform.IsChildOf(Player.transform) && !FishingShore.NonSolidSurface(hit.collider)
                    && (target==null || !hit.transform.IsChildOf(target)))
                {blocker=hit.collider.name+" at="+hit.point.ToString("F4")+" distance="+hit.distance.ToString("F4");return false;}
            return true;
        }
        public void TickWorldAction(float deltaTime,bool focused,bool xrActive)
        {
            var request=_action;if(request==null)return;
            if(!focused || xrActive || Paused || InventoryOpen || JournalOpen || ShopOpen || Expedition.Aligning || request.Profile!=Career
                || request.Ashore!=Ashore || (Ashore && request.Crouched!=Shore.Crouched) || request.Tool!=Career.Backpack.Equipped || request.Slot!=Career.Backpack.Selected
                || (!request.Attempted && request.HadRoot && (request.ContactRoot==null || !request.ContactRoot.gameObject.activeInHierarchy)))
            {CancelWorldAction();return;}
            // 起手预检可能使用未来蹲姿；先让真实 LateUpdate 骨架落位，再计接触时间。
            // 取消/模态/目标守卫仍在等待前执行，卡顿也不能用上一帧肩位消费唯一许可。
            if(request.WaitForStance)
            {
                if(!Shore.Character.IsStanceSettled(request.Ashore,request.Crouched))return;
                request.WaitForStance=false;
            }
            Actions.Tick(deltaTime);
            if(request.Fish)Shore.SyncRider();
            if(Actions.ContactPending && Actions.TryConsumeContact(Actions.ActionId))
            {
                request.Attempted=true;_actionContactFrame=_actionConsumedFrame=Time.frameCount;
                var character=Shore.Character;var baseShoulder=character.ShoulderPosition(false)-character.AppliedInteractionLean;
                var palm=ActionUsesHeldTool?FishingFieldWorld.PickContactPalm(ActionTargetPosition,request.ToolRotation):ActionTargetPosition;
                bool reachable=Vector3.Distance(baseShoulder+request.Lean,palm)<=character.ArmReach(false)-.005f && ClearActionSight(baseShoulder,palm,request.ContactRoot)
                    && (!ActionUsesHeldTool || ClearActionSight(palm,ActionTargetPosition,request.ContactRoot));
                var contact=ActionTargetPosition;
                if(reachable && request.Fish)CatchRig.ApplyActionContactPose(ActionFishPosition,request.ToolRotation);
                LastActionId=Actions.ActionId;ActionCommitAttempts++;
                LastActionSucceeded=reachable && request.Commit();
                if(LastActionSucceeded)
                {
                    request.ContactWorld=contact;request.ContactRoot=null;request.HadRoot=false;
                    // 放置等成功事务会消费当前物品；收手阶段不能把这次消费误判成用户换装备。
                    request.Tool=Career.Backpack.Equipped;request.Slot=Career.Backpack.Selected;
                }
                if(!LastActionSucceeded){CancelWorldAction();Notice("接触未提交：目标、距离、工具或背包状态已改变，请重新确认后再操作。",4);return;}
                if(_action!=request)return;
            }
            if(!Actions.Active && !Actions.ContactPending && _actionContactFrame!=Time.frameCount)
            {_action=null;WorkshopWorld.SetPlacementPreview(Vector3.zero,0,false);Shore.Character.InteractionLean=Vector3.zero;return;}
            Shore.Character.InteractionLean=request.Lean*ActionContactBlend;
        }
        public void CancelWorldAction()
        {
            if(_action!=null)_actionConsumedFrame=Time.frameCount;
            _action=null;Actions.Cancel();
            if(Shore!=null && Shore.Character!=null)Shore.Character.InteractionLean=Vector3.zero;
            WorkshopWorld?.SetPlacementPreview(Vector3.zero,0,false);
        }
        private void ApplyWorldActionPose()
        {
            if(_action==null || (_action.Fish && !(_actionContactFrame==Time.frameCount && !HasPendingCatch)) || ActionUsesHeldTool || !Hands.CanPresentTools)return;
            var palm=Vector3.Lerp(_action.StartPalm,ActionTargetPosition,ActionContactBlend);
            Hands.Right.SetPose(palm,Quaternion.Euler(0,Shore.Actor.eulerAngles.y,0),Mathf.Lerp(.15f,.7f,ActionContactBlend));
            Hands.RestHand(true);
        }
    }
    public static class FishingContactGeometry
    {
        /// <summary>优先取实际可读网格的最近顶点；只读网格退回其渲染包围盒，仍须经过实际臂长与视线检查。</summary>
        public static Vector3 ClosestPoint(Transform root,Vector3 from)
        {
            Vector3 best=root.position;float distance=float.MaxValue;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();
                if(mesh==null || renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                if(mesh.isReadable)
                {
                    foreach(var local in mesh.vertices)
                    {
                        var p=filter.transform.TransformPoint(local);float d=(p-from).sqrMagnitude;
                        if(d<distance){distance=d;best=p;}
                    }
                }
                else
                {
                    var p=renderer.bounds.ClosestPoint(from);float d=(p-from).sqrMagnitude;
                    if(d<distance){distance=d;best=p;}
                }
            }
            return best;
        }
    }
}
