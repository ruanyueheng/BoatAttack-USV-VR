using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BoatAttack.Fishing
{
    /// <summary>v0.44 realm layer: stage checkpoints, wind/tide hazards, echo shells and VR grab-twist cranks.</summary>
    public sealed partial class FishingRealmWorld
    {
        public const float GustPeriod=9,GustWarn=5.4f,GustStart=7;
        /// <summary>Gusts, the tide-stalled pump and echo shells; off by default in automation so legacy walk tests stay deterministic.</summary>
        public bool HazardsEnabled { get; set; }
        private Vector3 _safeFeet;
        private float _gustClock;
        private bool _gustWarned,_leakWarned,_wasFlooding;
        private readonly Transform[] _bannerCloth=new Transform[3],_cranks=new Transform[6];
        private readonly Vector3[] _gustAxis=new Vector3[3];
        private readonly Transform[][] _shells=new Transform[4][];
        private Transform _gaugeFloat;
        private Vector3 _gaugeHome;
        private Material _storybook;
        private int _twistId=-1;
        private float _twistAngle;
        private Quaternion _twistLast;
        /// <summary>Warning or gust phase while hazards are live; automation crouches through it like a careful player.</summary>
        public bool GustImminent=>HazardsEnabled && Inside && Island==0 && _gustClock%GustPeriod>=GustWarn;
        public bool GustActive=>Inside && Island==0 && _gustClock%GustPeriod>=GustStart;
        public Transform Shell(int island,int shell)=>_shells[island]?[shell];

        private Transform Storybook(string name,Transform parent,Vector3 local)
        {
            var source=Resources.Load<GameObject>("FishingV044/"+name);if(source==null)throw new InvalidOperationException("Missing V044 realm model "+name);
            if(_storybook==null)_storybook=Resources.Load<Material>("FishingV044/RealmStorybook");
            var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=local;Instantiate(source,root,false);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=_storybook;
            return root;
        }
        private static Transform Child(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        private void BuildV044(int island,Transform room)
        {
            HazardsEnabled=!_c.Automated;
            if(island==0)for(int i=0;i<3;i++)
            {
                _bannerCloth[i]=Child(Storybook("GustBanner",room.Find("Device "+i),Vector3.right*1.1f),"Cloth");
                // Gusts blow across each bridge's narrow axis.
                var bridge=_windBridges[i];float best=0;_gustAxis[i]=room.right;
                foreach(var f in bridge.GetComponentsInChildren<MeshFilter>(true))
                {
                    var ext=f.transform.TransformVector(f.sharedMesh.bounds.extents);ext=new Vector3(Mathf.Abs(ext.x),0,Mathf.Abs(ext.z));
                    if(ext.x*ext.z>best){best=ext.x*ext.z;_gustAxis[i]=ext.x<ext.z?Vector3.right:Vector3.forward;}
                }
            }
            if(island==1)
            {
                for(int i=0;i<3;i++)_cranks[i]=Storybook("GrabCrank",room.Find("Device "+i),new Vector3(0,.8f,-.8f));
                _cranks[5]=Storybook("GrabCrank",_shutterControl,Vector3.up*1.6f);
            }
            if(island==2)
            {
                var gauge=Storybook("TideGauge",room.Find("Device 1"),Vector3.right*1.1f);_gaugeFloat=Child(gauge,"Float");_gaugeHome=_gaugeFloat.localPosition;
            }
        }
        private readonly bool[] _settled=new bool[4];
        /// <summary>Slides a V044 prop sideways until it no longer intersects authored collision (rails, walls, pedestals).</summary>
        private void Settle(Transform prop)
        {
            var renderers=prop.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            var home=prop.position;
            foreach(float distance in new[]{0,.15f,.3f,.45f,.6f})
                foreach(var direction in new[]{prop.right,-prop.right,prop.forward,-prop.forward})
                {
                    prop.position=home+direction*distance;var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                    // Lift the probe 6 cm so the floor it stands on does not count as an overlap.
                    bool clear=!Physics.OverlapBox(b.center+Vector3.up*.06f,Vector3.Max(b.extents-Vector3.one*.03f,Vector3.one*.01f),Quaternion.identity,~(1<<4),QueryTriggerInteraction.Ignore)
                        .Any(x=>!x.transform.IsChildOf(prop) && x!=_c.Shore.Body);
                    if(clear)return;
                    if(distance==0)break;
                }
            prop.position=home;Debug.LogWarning("V044 prop could not be cleared of overlap: "+prop.name+" in "+Room.name);
        }
        private void EnterV044()
        {
            _safeFeet=_entries[Island].position;_gustClock=0;_gustWarned=_leakWarned=_wasFlooding=false;_twistId=-1;
            if(Island==3)PlaceConstellations(Trial);
            if(!_settled[Island]){_settled[Island]=true;foreach(var prop in Room.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="GustBanner" || t.name=="TideGauge" || t.name=="GrabCrank").ToArray())Settle(prop);}
            if(_shells[Island]==null)_shells[Island]=PlaceShells(Room);
            for(int i=0;i<3;i++)_shells[Island][i].gameObject.SetActive(!_c.Career.Lore.ShellFound(Island,i));
        }
        /// <summary>Three shells a step off the authored route, at its quarter, half and three-quarter markers.</summary>
        private Transform[] PlaceShells(Transform room)
        {
            var route=room.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("V35_Route",StringComparison.Ordinal))
                .Select(t=>(t,n:int.TryParse(t.name.Substring(9).Split('.')[0],out int k)?k:-1)).Where(x=>x.n>=0).OrderBy(x=>x.n).Select(x=>x.t).ToArray();
            var result=new Transform[3];
            for(int i=0;i<3;i++)
            {
                int target=route.Length>0?Mathf.Min(route.Length-1,route.Length*(i+1)/4):0;var spot=route.Length>0?route[target].position:_entries[Island].position;bool found=false;
                // Walk outward from the target marker; accept flat, unobstructed ground one step off the path.
                for(int step=0;step<route.Length*2 && !found;step++)
                {
                    int k=target+((step&1)==0?step/2:-(step+1)/2);if(k<0 || k>=route.Length)continue;var anchor=route[k].position;
                    foreach(var side in new[]{room.right,-room.right,room.forward,-room.forward})
                        if(Physics.Raycast(anchor+side*1.3f+Vector3.up*1.5f,Vector3.down,out var hit,3,~(1<<4),QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y-anchor.y)<.35f && hit.normal.y>.95f
                            && !Physics.CheckBox(hit.point+Vector3.up*.42f,new Vector3(.56f,.36f,.56f),Quaternion.identity,~(1<<4),QueryTriggerInteraction.Ignore)
                            && !Physics.CheckCapsule(hit.point+Vector3.up*.45f,hit.point+Vector3.up*1.6f,.32f,~(1<<4),QueryTriggerInteraction.Ignore))
                        {spot=hit.point;found=true;break;}
                }
                result[i]=Storybook("EchoShell",room,room.InverseTransformPoint(spot));result[i].name="Echo shell "+i;result[i].localScale=Vector3.one*1.5f;
            }
            return result;
        }
        private bool Braced=>_c.Shore.Crouched || (_c.DesktopDisplay?Keyboard.current!=null && Keyboard.current.eKey.isPressed:HeldAction());
        public int WindBridgeUnderfoot()
        {
            if(!Physics.Raycast(_c.Shore.Position+Vector3.up*.3f,Vector3.down,out var hit,1.2f,~(1<<4),QueryTriggerInteraction.Ignore))return -1;
            for(int i=0;i<3;i++)if(hit.transform.IsChildOf(_windBridges[i]))return i;
            return -1;
        }
        private void TickV044()
        {
            float dt=Time.deltaTime;var lore=_c.Career.Lore;
            // Each solved stage is engraved immediately; leaving or quitting resumes from it.
            if(!lore.Cleared(Island) && lore.SaveRealmStage(Island,Trial.Checkpoint)){_c.Save();_c.WorldAudio?.PlayRealm(Island,"Solve",_c.ExplorerPosition+Vector3.up,.5f);}
            if(Island==0)
            {
                _gustClock+=dt;float phase=_gustClock%GustPeriod;bool warn=phase>=GustWarn && phase<GustStart,gust=phase>=GustStart;
                for(int i=0;i<3;i++)_bannerCloth[i].localRotation=Quaternion.Euler(0,Mathf.Sin(Time.time*(gust?9:warn?6:1.5f))*(gust?50:warn?28:8),0);
                int bridge=HazardsEnabled?WindBridgeUnderfoot():-1;
                if(warn && bridge>=0 && !_gustWarned){_gustWarned=true;_c.Regions.Chime(0);_c.Notice("旗帜猎猎 · 阵风将至：蹲下或按住 E / 握键稳住，或先退回石台",3);}
                if(!warn && !gust)_gustWarned=false;
                if(gust && bridge>=0 && !Braced)
                {
                    if(FishingXRComfort.AllowsGustPush)_c.Shore.Body.Move(_gustAxis[bridge]*(((int)(_gustClock/GustPeriod))%2==0?1:-1)*FishingXRComfort.GustSpeed(1.8f)*dt);
                    // VR comfort: no sliding; an unbraced gust blinks you back to the last pad instead, keeping the risk.
                    else ComfortGustReturn();
                }
            }
            if(Island==2)
            {
                Trial.LeakLimit=HazardsEnabled?(_c.Career.Calendar.RisingTide?40:70):float.PositiveInfinity;
                if(!_leakWarned && !Trial.Flooding && Trial.WaterLevel<=.15f && Trial.CargoProgress<1 && Trial.LeakLimit-Trial.DrySeconds<10)
                {_leakWarned=true;_c.Notice("潮位计的浮标在发抖 · 排水泵快撑不住了",4);}
                if(Trial.Flooding && !_wasFlooding){_pushing=false;_c.WorldAudio?.Play("MechanismClick",_devices[1].position,.5f);_c.Notice("潮水压垮了排水泵，海水回灌。货箱已退回止滑楔，过半的进度还在；重新排水再推。",7);}
                if(!Trial.Flooding && Trial.DrySeconds==0)_leakWarned=false;
                _wasFlooding=Trial.Flooding;
                _gaugeFloat.localPosition=_gaugeHome+Vector3.up*2*Trial.WaterLevel;
            }
            if(Island==3)Trial.FocusSeconds=_c.Career.Calendar.Night?1:1.4f;
            if(Island==1)for(int i=0;i<3;i++)_cranks[i].localRotation=Quaternion.Euler(0,0,-90*Trial.State(i)-(_twistId==i?_twistAngle:0));
            if(Island==1 && _cranks[5].gameObject.activeInHierarchy)_cranks[5].localRotation=Quaternion.Euler(0,0,Trial.ShutterOpen?-90:0);
            TickShells();TickTwist();
        }
        /// <summary>VR comfort branch of an unbraced gust: ink cover, then return to the last pad. Public so tests can force it.</summary>
        public bool ComfortGustReturn()
        {
            if(!Inside || Transitioning || FishingInkTransition.Playing)return false;
            var island=Island;
            FishingInkTransition.Play(()=>
            {
                if(Island!=island || !_c.Shore.Relocate(_safeFeet,Quaternion.identity))return;
                _c.Shore.ResetMoveInput();_c.Notice("阵风把你吹回了石台 · 下次旗帜狂舞时蹲下或按住握键",4);
            },.35f,.45f);
            return true;
        }
        private void TickShells()
        {
            var shells=_shells[Island];bool lantern=_c.FieldWorld!=null && _c.FieldWorld.LanternHeld;
            for(int i=0;i<3;i++)
            {
                var shell=shells[i];if(_c.Career.Lore.ShellFound(Island,i))continue;
                // The forest's third shell hides in shade and only shows in lantern light.
                bool visible=HazardsEnabled && (Island!=1 || i!=2 || lantern);shell.gameObject.SetActive(visible);if(!visible)continue;
                shell.GetChild(0).localRotation=Quaternion.Euler(0,Time.time*40,0);
                if(FishingPrototype.FlatDistance(shell.position,_c.Shore.Position)>.9f || Mathf.Abs(shell.position.y-_c.Shore.Position.y)>1.2f)continue;
                int pay=_c.Career.CollectRealmShell(Island,i);if(pay<0)continue;
                shell.gameObject.SetActive(false);_c.WorldAudio?.PlayRealm(Island,"Step",shell.position,.5f);_c.Save();
                _c.Notice("回响贝 "+_c.Career.Lore.ShellCount(Island)+" / 3 · "+FishingLore.ShellLine(Island,i)+(pay>0?" · 本境回响集齐 +30 金币":""),8);
            }
        }
        /// <summary>VR: grip a crank and roll the wrist a quarter turn to step a mirror, valve or shutter.</summary>
        private void TickTwist()
        {
            if(_c.DesktopDisplay || _c.Hands==null || !_c.Hands.TryControllerPalm(false,out var palm,out var frame)){_twistId=-1;return;}
            var d=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);d.TryGetFeatureValue(CommonUsages.grip,out float grip);
            if(grip<.6f){_twistId=-1;_twistAngle=0;return;}
            if(Island==2 && !_pushing && Trial.WaterLevel<=.15f && Trial.CargoProgress<1 && Vector3.Distance(palm,_devices[0].position+Vector3.up*.6f)<.9f){Operate(0);return;}
            if(_twistId<0)
            {
                float best=.55f;
                foreach(int id in Island==1?new[]{0,1,2,5}:Island==2?new[]{1}:Array.Empty<int>())
                {
                    var t=_devices[id];if(t==null || !t.gameObject.activeInHierarchy)continue;
                    float distance=Vector3.Distance(palm,Island==1?_cranks[id].position:t.position+Vector3.up*1.1f);if(distance<best){best=distance;_twistId=id;}
                }
                _twistLast=frame;_twistAngle=0;return;
            }
            (Quaternion.Inverse(_twistLast)*frame).ToAngleAxis(out float angle,out var axis);_twistLast=frame;
            if(angle>180)angle-=360;_twistAngle+=angle*Vector3.Dot(axis,Vector3.forward);
            if(Mathf.Abs(_twistAngle)>=80){_twistAngle=0;Operate(_twistId);d.SendHapticImpulse(0,.4f,.08f);}
        }
    }
}
