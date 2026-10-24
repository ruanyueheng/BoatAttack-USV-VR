using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV038Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V038");
        public static void RunSwimming()=>FishingV011Validation.Begin("V038-SWIMMING");
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[V038] CHECK: "+text);}
        public static IEnumerator Swimming(FishingPrototype c)
        {
            c.ReturnToHarbor();yield return null;
            var berth=FishingIslandWorld.Berth(0)+Vector3.up*.7f;var rb=c.Player.engine.RB;
            rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            try
            {
                Check(c.Shore.TryDisembark(),"Production exterior landing starts the sea-swimming regression");
                var jetty=FishingIslandWorld.Center(0)+new Vector3(0,1.12f,33);
                Check(c.Shore.Relocate(jetty,Quaternion.identity),"The exterior jetty still has clear supported footing");yield return null;
                for(int i=0;i<85;i++){c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.right);c.Shore.Move(Vector2.up,true,false,false,.05f);yield return null;}
                c.PresentCharacterFrame();
                Check(c.Shore.Swimming && c.Shore.Character.EyePosition.y>-.1f && c.Shore.Character.EyePosition.y<.75f,"Walking off the exterior jetty still enters the ocean with the eye near its water level");
                for(int i=0;i<180 && c.Shore.Swimming;i++)
                {
                    c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(jetty-c.Shore.Position,Vector3.up));
                    c.Shore.Move(Vector2.up,true,true,false,.05f);yield return null;
                }
                Check(!c.Shore.Swimming && c.Shore.Ground(c.Shore.Position,out _) && c.Shore.Clearance(c.Shore.Position),"Real swimming and jump input still climb back onto the exterior jetty");
            }
            finally{c.ReturnToHarbor();}
        }
        public static void InspectCan(FishingPrototype c,string phase)
        {
            var can=c.Farm.WateringCan;float closest=float.MaxValue;
            foreach(var filter in can.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.transform==c.Farm.CanWater)continue;
                foreach(var vertex in filter.sharedMesh.vertices)
                    closest=Mathf.Min(closest,Vector3.Distance(can.InverseTransformPoint(filter.transform.TransformPoint(vertex)),FishingFarmWorld.CanHandle));
            }
            Check(closest>.006f && closest<.013f,"The calibrated grip centreline is inside the actual imported ivory handle, distance="+closest+" "+phase);
            InspectGrip(c,"can "+phase);
            if(c.Farm.CanWater.gameObject.activeSelf)InspectLiquid(c);
        }
        public static IEnumerator InspectHold(FishingPrototype c)
        {
            var r=c.Realms;var basin=r.Room.Find("Moving water in cargo hold").GetComponent<FishingWaterVolume>();
            Check(basin!=null && basin.GetComponent<MeshFilter>().sharedMesh.vertexCount>500,"Cargo hold water has a tessellated bounded free surface");
            Check(r.TryWater(r.Room.TransformPoint(new Vector3(2,-1.7f,-5)),out var surface,out var bottom) && surface-bottom>3,"The filled cargo hold has actual navigable water depth");
            Check(!r.TryWater(r.Room.TransformPoint(new Vector3(8,-1,-5)),out _,out _),"Local water does not extend through the hull into empty air");
            foreach(float z in new[]{9.05f,11,12.95f})Check(r.TryWater(r.Room.TransformPoint(new Vector3(.8f,1,z)),out _,out _),"Water extends beneath each floating bridge pontoon: "+z);
            foreach(float x in new[]{-.65f,.65f})
            {
                var walk=FishingV035Levels.Walk(c,r.Room.TransformPoint(new Vector3(x,1.8f,16.4f)));while(walk.MoveNext())yield return walk.Current;
                walk=FishingV035Levels.Walk(c,r.Room.TransformPoint(new Vector3(x,1.8f,7)));while(walk.MoveNext())yield return walk.Current;
            }
            bool immersed=false;
            foreach(var p in new[]{new Vector3(0,-1.8f,-4),new Vector3(-2,-1.8f,-7.5f),new Vector3(2,-1.8f,-7.5f)})
                if(c.Shore.Relocate(r.Room.TransformPoint(p),Quaternion.identity)){immersed=true;break;}
            Check(immersed,"The actual lower hold floor supports the immersion fixture");yield return null;
            for(int i=0;i<45;i++){c.Shore.Move(Vector2.zero,false,false,false,.03f);yield return null;}
            Check(c.Shore.Swimming && c.Shore.Position.y>r.Room.position.y-.8f && c.Shore.Position.y<surface,"Entering deep hold water produces buoyant swimming at the local level");
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/V038","hold-water-immersed");
            var bank=r.Room.TransformPoint(new Vector3(-4.2f,1.8f,-4));
            for(int i=0;i<180 && c.Shore.Swimming;i++)
            {
                c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(bank-c.Shore.Position,Vector3.up));
                c.Shore.Move(Vector2.up,false,true,false,.05f);yield return null;
            }
            Check(!c.Shore.Swimming && c.Shore.Position.y>r.Room.position.y+1.5f,"Real swimming and jump input climb from the water back onto the side deck");
            foreach(var p in new[]{new Vector3(-4.2f,1.8f,-4),new Vector3(-4.2f,1.8f,7),new Vector3(0,1.8f,7)})
            {var walk=FishingV035Levels.Walk(c,r.Room.TransformPoint(p));while(walk.MoveNext())yield return walk.Current;}
            var cross=FishingV035Levels.Walk(c,r.Room.TransformPoint(new Vector3(0,1.8f,16.4f)));while(cross.MoveNext())yield return cross.Current;
            c.PresentCharacterFrame();FishingV016Validation.Capture(c,"Logs/V038","bridge-final-crossing");
        }
        public static void InspectGrip(FishingPrototype c,string phase)
        {
            var hand=c.Hands.Right;var bones=c.Shore.Character.BodySkin.bones;
            foreach(string digit in new[]{"Index","Middle","Ring","Pinky"})
            {
                var root=Array.Find(bones,b=>b.name==digit+"2.R");var middle=Array.Find(bones,b=>b.name==digit+"3.R");var tip=Array.Find(bones,b=>b.name==digit+"4.R");
                float bend=Vector3.Angle(middle.position-root.position,tip.position-middle.position);
                var axis=hand.GripEnd-hand.GripStart;var nearest=hand.GripStart+axis*Mathf.Clamp01(Vector3.Dot(tip.position-hand.GripStart,axis)/axis.sqrMagnitude);
                float distance=Vector3.Distance(tip.position,nearest);
                Debug.Log("[V038] FINGER "+digit+" bend="+bend+" tipDistance="+distance+" phase="+phase);
                Check(bend>35 && distance<hand.GripRadius+.035f,"Distal finger wraps the real shaft: "+digit+" "+phase);
            }
            if(phase.StartsWith("pick",StringComparison.Ordinal))InspectGripSkin(c,phase);
        }
        private static void InspectGripSkin(FishingPrototype c,string phase)
        {
            var hand=c.Hands.Right;var skin=c.Shore.Character.BodySkin;var baked=new Mesh();
            try
            {
                skin.BakeMesh(baked,true);var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                var nearest=new[]{float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue};int inside=0;
                string[] names={"Index","Middle","Ring","Pinky","Thumb"};var axis=hand.GripEnd-hand.GripStart;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];int digit=-1;
                    for(int d=0;d<5;d++)if((w.weight0>.2f && bones[w.boneIndex0].name.StartsWith(names[d],StringComparison.Ordinal) && bones[w.boneIndex0].name.EndsWith(".R",StringComparison.Ordinal))
                        || (w.weight1>.2f && bones[w.boneIndex1].name.StartsWith(names[d],StringComparison.Ordinal) && bones[w.boneIndex1].name.EndsWith(".R",StringComparison.Ordinal))){digit=d;break;}
                    if(digit<0)continue;var p=skin.transform.TransformPoint(vertices[i]);
                    var q=hand.GripStart+axis*Mathf.Clamp01(Vector3.Dot(p-hand.GripStart,axis)/axis.sqrMagnitude);float distance=Vector3.Distance(p,q);
                    nearest[digit]=Mathf.Min(nearest[digit],distance);if(distance<hand.GripRadius-.002f)inside++;
                }
                Debug.Log("[V038] PICK SKIN inside="+inside+" distances="+string.Join(",",nearest));
                Check(inside==0,"Actual source-skinned fingers clear the solid wooden shaft "+phase);
                for(int d=0;d<5;d++)Check(nearest[d]<hand.GripRadius+.014f,"Visible skin of every digit participates in the shaft grasp: "+names[d]+" "+phase);
                var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;float fov=c.viewCamera.fieldOfView;
                try
                {
                    var centre=hand.Palm.position;var offset=hand.Palm.right*.10f-hand.Palm.up*.15f+hand.Palm.forward*.24f;
                    c.viewCamera.fieldOfView=42;
                    camera.SetPositionAndRotation(centre+offset,Quaternion.LookRotation(-offset,Vector3.up));
                    Directory.CreateDirectory("Logs/V038");FishingV016Validation.Capture(c,"Logs/V038","pick-grip-close");
                }
                finally{camera.SetPositionAndRotation(position,rotation);c.viewCamera.fieldOfView=fov;}
            }
            finally{UnityEngine.Object.Destroy(baked);}
        }
        public static void InspectLiquid(FishingPrototype c)
        {
            var water=c.Farm.CanWater;var can=c.Farm.WateringCan;float min=float.MaxValue,max=float.MinValue;bool contained=true,boundary=true;
            float[] ys={-.218f,-.211f,-.186f,-.14f,-.09f,-.047f,-.013f,.008f};
            float[] rs={.066f,.095f,.119f,.139f,.144f,.128f,.101f,.092f};
            var points=water.GetComponent<MeshFilter>().sharedMesh.vertices;
            for(int index=0;index<points.Length;index++)
            {
                var v=points[index];
                var world=water.TransformPoint(v);var p=can.InverseTransformPoint(world);min=Mathf.Min(min,world.y);max=Mathf.Max(max,world.y);
                float innerRadius=0;
                for(int j=1;j<ys.Length;j++)if(p.y>=ys[j-1] && p.y<=ys[j]){innerRadius=Mathf.Lerp(rs[j-1],rs[j],Mathf.InverseLerp(ys[j-1],ys[j],p.y))-.006f;break;}
                float radial=Mathf.Sqrt(p.x*p.x+(p.z+.12f)*(p.z+.12f)/(.88f*.88f));
                contained &= p.y>-.211f && p.y<.008f && radial<=innerRadius+.0001f;
                if(index>0)boundary &= innerRadius-radial<.0003f || p.y<-.2102f || p.y>.0067f;
            }
            Check(contained,"Every liquid vertex remains inside the authored can shell");
            Check(boundary,"Every liquid boundary meets the real inner wall or rim");
            Check(max-min<.00015f,"The contained free surface stays horizontal under gravity through can tilt");
        }
        public static void InspectLiquidOrientations(FishingPrototype c)
        {
            var can=c.Farm.WateringCan;var rotation=can.rotation;var farm=c.Career.Farm;int water=farm.Water;
            var amount=typeof(FishingFarm).GetProperty("Water");
            var update=typeof(FishingFarmWorld).GetMethod("UpdateCanWater",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            try
            {
                foreach(int fill in new[]{1,6,12})foreach(float angle in new[]{0f,25f,50f,85f,135f,180f})
                {
                    amount.SetValue(farm,fill);can.rotation=rotation*Quaternion.AngleAxis(angle,Vector3.forward);update.Invoke(c.Farm,null);InspectLiquid(c);
                    var centre=c.Farm.CanWater.GetComponent<MeshFilter>().sharedMesh.vertices[0];
                    var q=new Vector3(centre.x/.13f,(centre.y+.112f)/.097f,(centre.z+.12f)/.114f);
                    var n=Vector3.Scale(can.InverseTransformDirection(Vector3.up),new Vector3(.13f,.097f,.114f)).normalized;
                    float h=Vector3.Dot(q,n),volume=(2+3*h-h*h*h)*.25f;
                    Check(Mathf.Abs(volume-.94f*fill/FishingFarm.WaterCapacity)<.0001f,"Calibrated water-charge height survives tilt: fill="+fill+" angle="+angle);
                }
            }
            finally{amount.SetValue(farm,water);can.rotation=rotation;update.Invoke(c.Farm,null);}
        }
        public static void Assets()
        {
            var can=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("FishingV036/StarlightWateringCanView"));
            try
            {
                foreach(var f in can.GetComponentsInChildren<MeshFilter>())
                {
                    var bounds=new Bounds();bool first=true;float grip=float.MaxValue,opposite=float.MaxValue;
                    foreach(var v in f.sharedMesh.vertices)
                    {
                        var p=can.transform.InverseTransformPoint(f.transform.TransformPoint(v));
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                        grip=Mathf.Min(grip,Vector3.Distance(p,FishingFarmWorld.CanHandle));
                        opposite=Mathf.Min(opposite,Vector3.Distance(p,new Vector3(-FishingFarmWorld.CanHandle.x,FishingFarmWorld.CanHandle.y,-FishingFarmWorld.CanHandle.z)));
                    }
                    Debug.Log("[V038-ASSETS] can "+f.name+" bounds="+bounds.ToString("F4")+" expectedGripDistance="+grip+" oppositeGripDistance="+opposite);
                }
                foreach(string name in new[]{"SanctuaryDrydock","FloatGangway"})
                    foreach(var f in Resources.Load<GameObject>("FishingV035/"+name).GetComponentsInChildren<MeshFilter>())
                        Debug.Log("[V038-ASSETS] "+name+" mesh="+f.sharedMesh.bounds.ToString("F4")+" rotation="+f.transform.localEulerAngles);
                Debug.Log("[V038-ASSETS] PASS: source coordinates recorded");
                EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogError(ex);EditorApplication.Exit(1);}
            finally{UnityEngine.Object.DestroyImmediate(can);}
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V038");
            var route=FishingV035Levels.Island(c,2,Path.GetFullPath("Logs/V038"));
            try{while(route.MoveNext())yield return route.Current;}finally{(route as IDisposable)?.Dispose();}
            var hands=FishingV037HandsValidation.Checks(c);
            try{while(hands.MoveNext())yield return hands.Current;}finally{(hands as IDisposable)?.Dispose();}
            var pick=FishingPickaxeValidation.Checks(c);
            try{while(pick.MoveNext())yield return pick.Current;}finally{(pick as IDisposable)?.Dispose();}
        }
    }
}
