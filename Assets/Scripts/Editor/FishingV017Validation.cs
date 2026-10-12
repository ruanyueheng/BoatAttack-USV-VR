using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public sealed class FishingV017Driver : MonoBehaviour
    {
        public Engine Engine;
        public float Throttle, Steering;
        private void FixedUpdate(){Engine.Accelerate(Throttle);Engine.Turn(Steering);}
    }
    public static class FishingV017Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V017");
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException(label);Debug.Log("[V017] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/v017-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(evidence);c.ReturnToHarbor();
            var rb=c.Player.engine.RB;var engine=c.Player.engine;
            for(int i=0;i<120;i++)yield return null;
            Check(Quaternion.Angle(rb.inertiaTensorRotation,Quaternion.identity)<.1f,"Inertia axes align with the upright hull constraints");
            var hull=c.Player.GetComponent<MeshCollider>();
            Check(hull.sharedMaterial.staticFriction==0 && hull.sharedMaterial.dynamicFriction==0,"Solid hull slides against obstacles without sticky friction");
            var mesh=c.Player.boatRenderer.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.triangles.Length/3<6500,"Detailed skiff remains below 6500 triangles");
            var vertices=mesh.vertices;var normals=mesh.normals;int inside=0,outside=0;
            for(int i=0;i<vertices.Length;i++)
                if(Mathf.Abs(vertices[i].x)>.65f && vertices[i].y>.3f && vertices[i].y<.59f && Mathf.Abs(vertices[i].z)<1.4f)
                {if(vertices[i].x*normals[i].x<-.2f)inside++;if(vertices[i].x*normals[i].x>.2f)outside++;}
            Check(inside>8 && outside>8,"Imported hull has visible inward and outward wall faces");
            var driver=c.Player.gameObject.AddComponent<FishingV017Driver>();driver.Engine=engine;c.enabled=false;
            GameObject wall=null;
            try
            {
                // Use the real FixedUpdate engine and wave sampler, without repeated frame-rate-dependent forces.
                var start=rb.position;driver.Throttle=1;float until=Time.time+4;
                while(Time.time<until)yield return null;
                Check(c.Speed>2 && Vector3.ProjectOnPlane(rb.position-start,Vector3.up).magnitude>5,"Boat accelerates away from the actual harbor spawn; speed="+c.Speed);
                var sea=c.fishingSpots[0]+new Vector3(-50,.35f,0);
                for(int attempt=0;attempt<8 && Physics.OverlapBox(sea,new Vector3(20,2,20),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Length>0;attempt++)sea.x-=40;
                Check(Physics.OverlapBox(sea,new Vector3(20,2,20),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Length==0,"Wave-thrust fixture has a clear forty-metre water corridor");
                Place(c,sea);
                float maxTilt=0,maxSpeed=0,maxYaw=0;int wet=0,samples=0;
                foreach(float turn in new[]{1f,-1f})
                {
                    driver.Steering=turn;until=Time.time+15;
                    while(Time.time<until)
                    {
                        maxTilt=Mathf.Max(maxTilt,Vector3.Angle(c.Player.transform.up,Vector3.up));maxSpeed=Mathf.Max(maxSpeed,c.Speed);
                        maxYaw=Mathf.Max(maxYaw,Mathf.Abs(rb.angularVelocity.y));if(engine.InWater)wet++;samples++;
                        yield return null;
                    }
                }
                Check(maxTilt<1,"Thirty seconds of full-throttle turns cannot capsize; maximum tilt="+maxTilt);
                Check(maxSpeed<7.5f && maxYaw<=84f*Mathf.Deg2Rad+.01f,"Open-water speed and turn rate stay below the v20 base-rudder envelope; speed="+maxSpeed+" yaw="+maxYaw);
                Check(wet>(samples*.95f),"Propeller retains power through waves; wet samples="+wet+"/"+samples);
                Place(c,sea);driver.Steering=0;driver.Throttle=-1;until=Time.time+5;
                while(Time.time<until)yield return null;
                Check(Vector3.Dot(rb.velocity,c.Player.transform.forward)<-1 && c.Speed<4.5f,"Reverse departs from rest and respects astern speed limit");
                driver.Throttle=0;until=Time.time+9;while(Time.time<until)yield return null;
                Check(c.Speed<1.5f,"Released throttle coasts to a controlled low speed");
                Place(c,sea);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary V017 collision fixture";
                wall.transform.position=sea+new Vector3(0,1,4);wall.transform.localScale=new Vector3(40,4,.4f);Physics.SyncTransforms();
                var obstacle=wall.GetComponent<Collider>();
                Check(rb.SweepTest(Vector3.forward,out var approach,8) && approach.collider==obstacle,"Actual hull sweep reaches the collision fixture");
                driver.Throttle=1;until=Time.time+4;while(Time.time<until)yield return null;
                Check(hull.bounds.max.z<=obstacle.bounds.min.z+.1f,"Real solid collision stops the complete hull at the obstacle; position="+rb.position+" yaw="+rb.rotation.eulerAngles.y);
                var blocked=rb.position;var astern=-c.Player.transform.forward;driver.Throttle=-1;until=Time.time+4;while(Time.time<until)yield return null;
                var contact=obstacle.ClosestPoint(rb.position);
                Check(Vector3.Dot(rb.position-blocked,astern)>2 && Vector3.Distance(hull.ClosestPoint(contact),contact)>.25f,"Reverse frees the hull after sustained obstacle contact");
                Object.Destroy(wall);wall=null;driver.Throttle=0;Place(c,sea);
                rb.useGravity=false;Place(c,sea+Vector3.up*20);
                until=Time.time+2;while(Time.time<until)yield return null;
                rb.velocity=Vector3.zero;driver.Throttle=1;until=Time.time+1;while(Time.time<until)yield return null;
                Check(!engine.InWater && c.Speed<.1f,"A fully dry boat cannot retain propulsion after the bounded wave grace");
                driver.Throttle=0;rb.useGravity=true;Place(c,sea);
                until=Time.time+2;while(Time.time<until)yield return null;
                var oldSave=JsonUtility.FromJson<CalendarData>("{\"day\":4,\"minute\":480}");
                Check(FishingCalendar.TryRestore(oldSave,out var oldClock) && oldClock.Automatic,"Unity deserialization of an old save retains automatic time");
                c.SetDayNight(false);c.ToggleClock();Check(!c.Career.Calendar.Automatic,"Calendar UI action can hold the selected time");
                double held=c.Career.Calendar.TotalMinutes;until=Time.time+1;while(Time.time<until)yield return null;
                Check(c.Career.Calendar.TotalMinutes==held,"Held time stays fixed through real weather Updates");
                var camera=c.viewCamera;camera.transform.SetPositionAndRotation(sea+new Vector3(-3,3,-4),Quaternion.LookRotation(new Vector3(3,-2,4)));
                c.PresentCharacterFrame();c.Weather.ApplyEnvironment();FishingV016Validation.Capture(c,evidence,"01-day-hull");
                var lamp=c.Weather.DeckLight;Check(lamp!=null && !lamp.enabled && lamp.transform.IsChildOf(c.Player.transform),"Daytime switches off the boat-mounted lamp");
                c.SetDayNight(true);Check(c.Career.Calendar.Night && lamp.enabled && lamp.intensity>2.9f,"Nighttime enables local boat illumination");
                FishingV016Validation.Capture(c,evidence,"02-night-deck");
                lamp.enabled=false;FishingV016Validation.Capture(c,evidence,"03-night-without-lamp");c.Weather.ApplyEnvironment();
                c.enabled=true;c.ToggleClock();c.ReturnToHarbor();c.SetDayNight(false);
                Debug.Log("[V017] EVIDENCE: "+evidence);
            }
            finally{c.enabled=true;Object.Destroy(driver);if(wall!=null)Object.Destroy(wall);}
        }
        private static void Place(FishingPrototype c,Vector3 p)
        {
            var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;
            rb.position=p;rb.rotation=Quaternion.identity;c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
        }
    }
}
