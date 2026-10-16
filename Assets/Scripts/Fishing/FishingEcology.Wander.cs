using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingEcology
    {
        private readonly System.Random _wanderRandom=new System.Random(29017);
        private sealed class Wander {public Vector3 position,target,heading=Vector3.forward;public float wait,probeClock;public bool blocked;}
        public static FishKind[] HabitatSpecies(int zone)
        {
            switch(zone)
            {
                case 0:return new[]{FishKind.Coral,FishKind.Butterfly,FishKind.SeaBream,FishKind.Sardine,FishKind.Seahorse,FishKind.SeaTurtle,FishKind.Crab};
                case 1:return new[]{FishKind.Dart,FishKind.Mackerel,FishKind.Tuna,FishKind.Sardine,FishKind.Stingray,FishKind.Cuttlefish};
                case 2:return new[]{FishKind.Puffer,FishKind.Grouper,FishKind.Lionfish,FishKind.Sardine,FishKind.Octopus,FishKind.Lobster,FishKind.Lanternfish};
                default:return new[]{FishKind.Coral,FishKind.Sardine,FishKind.SeaBream,FishKind.SeaTurtle,FishKind.Jellyfish};
            }
        }
        private Wander NewWander(Vector3 home,float height,int seed)
        {
            float angle=seed*137.5f*Mathf.Deg2Rad;
            var point=home+new Vector3(Mathf.Cos(angle)*6,height,Mathf.Sin(angle)*6);point.y=height;
            return new Wander{position=point,target=point,wait=(seed%5)*.2f};
        }
        private bool OpenWaterSegment(Vector3 from,Vector3 to,bool flying)
        {
            var delta=to-from;from.y=flying?from.y:.45f;to.y=from.y;delta=to-from;
            return !Physics.CheckSphere(to,.35f,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore)
                && (delta.sqrMagnitude<.01f || !Physics.SphereCast(from,.2f,delta.normalized,out _,delta.magnitude,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore));
        }
        private void TickWander(Wander motion,Vector3 home,float radius,float speed,float dt,bool flying)
        {
            dt=Mathf.Min(dt,.1f);if(dt<=0)return;
            motion.probeClock-=dt;
            if(motion.probeClock<=0){motion.probeClock=.25f;motion.blocked=!OpenWaterSegment(motion.position,motion.position+motion.heading*.9f,flying);}
            if(Vector3.Distance(motion.position,motion.target)<.65f || motion.blocked)
            {
                motion.wait-=dt;if(motion.wait>0)return;
                for(int attempt=0;attempt<16;attempt++)
                {
                    float angle=(float)_wanderRandom.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)_wanderRandom.NextDouble())*radius;
                    var target=home+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);target.y=flying?5+(float)_wanderRandom.NextDouble()*3:motion.position.y;
                    if(Vector3.Distance(target,motion.position)<2 || !OpenWaterSegment(motion.position,target,flying))continue;
                    motion.target=target;motion.wait=(float)_wanderRandom.NextDouble()*(flying?.2f:1.1f);break;
                }
            }
            var delta=motion.target-motion.position;if(delta.sqrMagnitude<.05f)return;
            motion.heading=Vector3.RotateTowards(motion.heading,delta.normalized,dt*(flying?1.3f:1.6f),0).normalized;
            var next=motion.position+motion.heading*Mathf.Min(speed*dt,delta.magnitude);
            if(!motion.blocked)motion.position=next;
            if(Vector3.ProjectOnPlane(motion.position-home,Vector3.up).sqrMagnitude>(radius+1)*(radius+1))
                motion.target=new Vector3(home.x,motion.position.y,home.z);
        }
        private bool SeaBeatActive(Transform root)
        {
            foreach(var beat in _leaps)if(beat.root==root && beat.age>=0)return true;
            foreach(var beat in _dives)if(beat.root==root && beat.age>=0)return true;
            return false;
        }
    }
}
