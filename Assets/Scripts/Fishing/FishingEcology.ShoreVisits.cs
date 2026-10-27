using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingEcology
    {
        private sealed class ShoreVisit
        {
            public Creature creature;
            public Vector3 beach,approach,water;
            public float wait,rest,bottom;
            public int state;
            public MaterialPropertyBlock flying=new MaterialPropertyBlock(),resting=new MaterialPropertyBlock();
        }
        private readonly List<ShoreVisit> _shoreVisits=new List<ShoreVisit>();
        public bool MarineShoreWindow(Transform animal,out Vector3 approach)
        {
            foreach(var visit in _shoreVisits)if(visit.creature.transform==animal)
            {approach=visit.approach;return visit.state==3 && visit.rest>0;}
            approach=default;return false;
        }
        private bool MarineShoreVisiting(Transform animal)
        {foreach(var visit in _shoreVisits)if(visit.creature.transform==animal)return visit.state>0;return false;}
        private void InitializeMarineShoreVisits()
        {
            Physics.SyncTransforms();
            foreach(var creature in _creatures)
            {
                if(creature.kind!=MarineKind.Gull && creature.kind!=MarineKind.Turtle)continue;
                if(!FindMarineBeach(creature.kind,creature.home,creature.transform.position,out var beach,out var approach,out var water))
                {Debug.LogWarning("[MARINE-SHORE] No safe shore visit found for "+creature.kind+" at "+creature.home);continue;}
                var visit=new ShoreVisit{creature=creature,beach=beach,approach=approach,water=water,wait=8+_shoreVisits.Count*5,
                    bottom=creature.transform.GetComponent<MeshFilter>().sharedMesh.bounds.min.y*creature.transform.lossyScale.y};
                var renderer=creature.transform.GetComponent<Renderer>();renderer.GetPropertyBlock(visit.flying);renderer.GetPropertyBlock(visit.resting);
                visit.resting.SetVector("_FinMotion",Vector4.zero);_shoreVisits.Add(visit);
                Debug.Log("[MARINE-SHORE] "+creature.kind+" beach="+beach+" safe approach="+approach+" water="+water);
            }
        }
        private bool Surface(Vector3 point,out RaycastHit surface)
        {
            return Physics.Raycast(new Vector3(point.x,10,point.z),Vector3.down,out surface,30,1<<10,QueryTriggerInteraction.Ignore)
                && surface.normal.y>.8f;
        }
        private bool MarineOpenWater(Vector3 from,Vector3 to)
        {
            var delta=to-from;if(delta.sqrMagnitude<.01f)return true;
            from.y=to.y=Mathf.Max(-.28f,from.y);
            return !Physics.CheckSphere(to,.12f,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore)
                && !Physics.SphereCast(from,.08f,delta.normalized,out _,delta.magnitude,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore);
        }
        private bool FindMarineBeach(MarineKind creatureKind,Vector3 home,Vector3 departure,out Vector3 beach,out Vector3 approach,out Vector3 water)
        {
            beach=approach=water=default;
            var seeds=new List<Vector3>{home};
            if(Controller.CoastPresentation!=null)foreach(var islet in FishingCoastalPresentation.Islets)seeds.Add(islet);
            if(Controller.Shore!=null)foreach(var site in Controller.Shore.Sites)if(site!=null && site.Available)seeds.Add(site.Landing);
            if(Controller.Home!=null)seeds.Add(Controller.Home.Entry);
            for(int seedIndex=0;seedIndex<seeds.Count;seedIndex++)for(int radius=0;radius<=72;radius+=2)for(int angle=0;angle<360;angle+=15)
            {
                var point=seeds[seedIndex]+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;point.y=0;
                if(!Surface(point,out var shore) || shore.point.y<.6f || shore.point.y>1.25f || !Controller.Shore.Clearance(shore.point))continue;
                for(int heading=0;heading<360;heading+=45)
                {
                    var outward=Quaternion.Euler(0,heading,0)*Vector3.forward;
                    var wet=shore.point+outward*4;
                    if(!Surface(wet,out var seabed) || seabed.point.y>-.65f || seabed.point.y< -1.6f)continue;
                    var stand=shore.point-outward*.68f;
                    if(!Controller.Shore.Ground(new Vector3(stand.x,0,stand.z),out stand) || Mathf.Abs(stand.y-shore.point.y)>.5f
                        || !Controller.Shore.Clearance(stand) || !Controller.Shore.ClearSight(stand+Vector3.up*.55f,shore.point+Vector3.up*.35f))continue;
                    bool continuous=true;float previous=seabed.point.y;
                    for(int step=1;step<=20;step++)
                    {
                        var sample=Vector3.Lerp(wet,shore.point,step/20f);
                        if(!Surface(sample,out var hit) || Mathf.Abs(hit.point.y-previous)>.18f
                            || Physics.CheckSphere(hit.point+Vector3.up*.45f,.2f,~((1<<4)|(1<<5)|(1<<10)),QueryTriggerInteraction.Ignore))
                        {continuous=false;break;}
                        previous=hit.point.y;
                    }
                    if(!continuous)continue;
                    wet.y=-.28f;
                    var travelStart=departure+Vector3.up*(creatureKind==MarineKind.Gull?6:0);
                    var travelEnd=creatureKind==MarineKind.Gull?shore.point+Vector3.up*6:wet;
                    if(!MarineOpenWater(travelStart,travelEnd))continue;
                    beach=shore.point;approach=stand;water=wet;return true;
                }
            }
            return false;
        }
        private void ResetMarineShoreVisit(Creature creature)
        {
            foreach(var visit in _shoreVisits)if(visit.creature==creature && visit.state!=0)
            {
                visit.state=0;visit.wait=8;
                creature.motion.position=new Vector3(creature.home.x,creature.kind==MarineKind.Gull?6:-.28f,creature.home.z);
                creature.motion.target=creature.motion.position;
            }
        }
        private bool TickMarineShoreVisit(Creature creature,float seconds)
        {
            ShoreVisit visit=null;foreach(var candidate in _shoreVisits)if(candidate.creature==creature){visit=candidate;break;}
            if(visit==null)return false;
            float dt=Mathf.Min(seconds,.1f);if(dt<=0)return visit.state>0;
            if(visit.state==3 && Vector3.Distance(creature.transform.position,visit.beach-Vector3.up*visit.bottom)>.8f)
            {
                // Respawning restores the original wildlife home; a stale resting phase must not pin it there.
                visit.state=0;visit.wait=8;creature.transform.GetComponent<Renderer>().SetPropertyBlock(visit.flying);
            }
            if(visit.state==0)
            {
                visit.wait-=dt;if(visit.wait>0)return false;
                var destination=creature.kind==MarineKind.Gull?visit.beach+Vector3.up*6:visit.water;
                if(!MarineOpenWater(creature.transform.position,destination)){visit.wait=3;return false;}
                visit.state=1;visit.rest=28;
            }
            bool gull=creature.kind==MarineKind.Gull;
            var goal=visit.state==1?(gull?visit.beach+Vector3.up*6:visit.water):visit.state==2?visit.beach:
                visit.state==4?(gull?visit.beach+Vector3.up*6:visit.water):new Vector3(creature.home.x,gull?6:-.28f,creature.home.z);
            if(visit.state==2 || visit.state==3)goal.y=visit.beach.y-visit.bottom+.025f;
            if(visit.state==3)
            {
                visit.rest-=dt;
                if(visit.rest<=0){visit.state=4;creature.transform.GetComponent<Renderer>().SetPropertyBlock(visit.flying);}
                return true;
            }
            var from=creature.transform.position;var next=Vector3.MoveTowards(from,goal,dt*(gull?3.2f:.65f));
            if(!gull && (visit.state==2 || visit.state==4))
            {
                if(!Surface(next,out var floor))return true;
                next.y=Mathf.Max(-.28f,floor.point.y-visit.bottom+.025f);
            }
            var heading=Vector3.ProjectOnPlane(next-from,Vector3.up);
            creature.transform.position=next;
            if(heading.sqrMagnitude>.0001f)creature.transform.rotation=Quaternion.RotateTowards(creature.transform.rotation,Quaternion.LookRotation(heading),dt*120);
            if((next-goal).sqrMagnitude<.04f)
            {
                if(visit.state==1)visit.state=2;
                else if(visit.state==2){visit.state=3;creature.transform.GetComponent<Renderer>().SetPropertyBlock(visit.resting);}
                else if(visit.state==4)visit.state=5;
                else
                {
                    visit.state=0;visit.wait=40;creature.motion.position=next;creature.motion.target=next;
                    creature.motion.heading=creature.transform.forward;
                }
            }
            return true;
        }
    }
}
