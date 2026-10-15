using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingEcology
    {
        private sealed class SeaBeat
        {
            public Transform root,prey;
            public Vector3 start,target;
            public float wait,age=-1;
            public bool splashed;
        }
        private readonly List<SeaBeat> _leaps=new List<SeaBeat>(),_dives=new List<SeaBeat>();
        public int FishJumpCount {get;private set;}
        public int GullDiveCount {get;private set;}
        private void InitializeSeaSurprises()
        {
            var performers=new HashSet<FishKind>();
            foreach(var fish in _shoal)
                if((fish.kind==FishKind.Mackerel || fish.kind==FishKind.SeaBream || fish.kind==FishKind.Sardine)
                    && performers.Add(fish.kind))
                {
                    // One performer per species keeps the bay lively without a constant jumping loop.
                    _leaps.Add(new SeaBeat{root=fish.root,wait=12+_leaps.Count*17});
                }
            foreach(var creature in _creatures)if(creature.kind==MarineKind.Gull)
            {
                var fish=FishingArt.Fish(creature.transform,_owned,Controller.PaletteShader,FishKind.Sardine);
                var size=fish.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                fish.name="Seabird's visible catch";fish.transform.localScale=Vector3.one*(.15f/Mathf.Max(size.x,size.y,size.z));
                fish.transform.localPosition=new Vector3(0,0,.425f);fish.transform.localRotation=Quaternion.Euler(0,90,25);fish.SetActive(false);
                _dives.Add(new SeaBeat{root=creature.transform,prey=fish.transform,wait=26+_dives.Count*23});
            }
        }
        private bool QuietSea=>Controller.Weather.Climate.Current==BayWeather.Rain;
        private void TickSeaSurprises(float dt)
        {
            foreach(var beat in _leaps)
            {
                if(!beat.root.gameObject.activeSelf)continue;
                if(beat.age<0)
                {
                    beat.wait-=dt;
                    if(beat.wait>0 || QuietSea)continue;
                    beat.age=0;beat.start=beat.root.position;beat.target=beat.start+beat.root.forward*2.2f;
                    Controller.Effects?.Burst(new Vector3(beat.start.x,.04f,beat.start.z),new Color(.72f,.89f,.94f),true);FishJumpCount++;
                }
                beat.age+=dt;float u=Mathf.Clamp01(beat.age/1.25f);
                var point=Vector3.Lerp(beat.start,beat.target,u);point.y=-.08f+Mathf.Sin(u*Mathf.PI)*1.15f;
                beat.root.SetPositionAndRotation(point,Quaternion.LookRotation(beat.target-beat.start)*Quaternion.Euler(-Mathf.Cos(u*Mathf.PI)*52,0,Mathf.Sin(u*Mathf.PI)*8));
                if(u>=1)
                {
                    Controller.Effects?.Burst(point+Vector3.up*.12f,new Color(.72f,.89f,.94f),true);
                    beat.age=-1;beat.wait=38+FishJumpCount%3*13;
                }
            }
            foreach(var beat in _dives)
            {
                if(Controller.RifleHunting!=null && Controller.RifleHunting.DeadWildlife(beat.root)){beat.prey.gameObject.SetActive(false);continue;}
                if(!beat.root.gameObject.activeSelf)continue;
                if(beat.age<0)
                {
                    beat.wait-=dt;
                    if(beat.wait<60)beat.prey.gameObject.SetActive(false);
                    if(beat.wait>0 || QuietSea || (Controller.ExplorerPosition-beat.root.position).sqrMagnitude>100*100)continue;
                    beat.start=beat.root.position;
                    beat.target=beat.start+Vector3.ProjectOnPlane(beat.root.forward,Vector3.up).normalized*3;
                    beat.target.y=.04f;
                    if(Physics.CheckSphere(beat.target+Vector3.up*.15f,.4f,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore))
                    {beat.wait=12;continue;}
                    beat.age=0;beat.splashed=false;beat.prey.gameObject.SetActive(false);GullDiveCount++;
                }
                Vector3 orbit=beat.root.position;
                beat.age+=dt;float u=Mathf.Clamp01(beat.age/5.2f);
                if(u<.43f)
                {
                    float down=Mathf.SmoothStep(0,1,u/.43f);var p=Vector3.Lerp(beat.start,beat.target,down);
                    beat.root.SetPositionAndRotation(p,Quaternion.LookRotation((beat.target-beat.start).normalized)*Quaternion.Euler(0,0,Mathf.Sin(down*Mathf.PI)*12));
                }
                else
                {
                    if(!beat.splashed)
                    {
                        beat.splashed=true;Controller.Effects?.Burst(beat.target,new Color(.76f,.9f,.96f),true);beat.prey.gameObject.SetActive(true);
                    }
                    float up=Mathf.SmoothStep(0,1,(u-.43f)/.57f);var p=Vector3.Lerp(beat.target,orbit,up);
                    beat.root.SetPositionAndRotation(p,Quaternion.LookRotation(Vector3.Lerp((orbit-beat.target).normalized,beat.root.forward,up)));
                }
                if(u>=1){beat.age=-1;beat.wait=64+GullDiveCount%3*19;}
            }
        }
        public Transform PreviewSeaBeat(bool bird,float progress)
        {
            if(!Controller.Automated)return null;
            var list=bird?_dives:_leaps;if(list.Count==0)return null;var beat=list[0];
            var origin=Controller.fishingSpots[0]+new Vector3(4,0,4);
            beat.root.gameObject.SetActive(true);beat.start=origin+Vector3.up*(bird?6:-.08f);beat.target=origin+Vector3.forward*(bird?3:2.2f);beat.target.y=bird?.04f:-.08f;
            if(bird)beat.root.SetPositionAndRotation(origin+new Vector3(3,6,8),Quaternion.identity);
            beat.age=Mathf.Clamp01(progress)*(bird?5.2f:1.25f);beat.wait=0;beat.splashed=false;
            TickSeaSurprises(0);return beat.root;
        }
    }
}
