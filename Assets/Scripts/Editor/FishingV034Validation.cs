using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV034Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V034");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V034] CHECK: "+label);}
        private static void Capture(FishingPrototype c,string name)
        {
            Directory.CreateDirectory("Logs/V034");
            var target=RenderTexture.GetTemporary(1280,720,24);var old=c.viewCamera.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{c.viewCamera.targetTexture=target;c.viewCamera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/V034/"+name+".png",image.EncodeToPNG());}
            finally{c.viewCamera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(image);}
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            c.OpenStartMenu();yield return null;
            var career=c.Career;var position=c.Player.engine.RB.position;var minute=career.Calendar.Minute;
            Check(c.Paused && Time.timeScale==0,"Menu freezes real gameplay");
            c.TitleVoyage.Present(.1f);var cloud=c.TitleVoyage.Clouds.position;var gull=c.TitleVoyage.Gull.position;
            Capture(c,"title-0");c.TitleVoyage.Present(2.1f);Capture(c,"title-2");
            for(int i=0;i<24;i++){c.TitleVoyage.Present(i/12f);Capture(c,"title-motion-"+i.ToString("D2"));}
            c.TitleVoyage.Present(2.1f);
            Check(Vector3.Distance(cloud,c.TitleVoyage.Clouds.position)>.5f && Vector3.Distance(gull,c.TitleVoyage.Gull.position)>2,"Clouds drift and gulls fly while menu is paused");
            Check(Shader.GetGlobalFloat("_FishingVisualTimeOffset")>2,"Water and wing shaders receive a live menu clock");
            Check((c.Player.engine.RB.position-position).sqrMagnitude<.001f && career.Calendar.Minute==minute,"Menu visuals cannot advance the real voyage or calendar");
            Check(c.TitleVoyage.Clouds.GetComponentsInChildren<Collider>(true).Length==0,"Environmental menu actors are visual only");
            bool foliage=false;var properties=new MaterialPropertyBlock();
            foreach(var renderer in c.TitleVoyage.GetComponentsInChildren<MeshRenderer>())
            {renderer.GetPropertyBlock(properties);if(properties.GetFloat("_TitleBreeze")>0)foliage=true;}
            Check(foliage,"Home foliage has wind while trunks and real colliders stay fixed");
            c.ContinueFromMenu();Check(c.StartMenuOpen && c.TitleVoyage.Sailing && c.Paused,"Continue enters departure animation before gameplay resumes");
            var start=c.TitleVoyage.Boat.position;int frames=0;
            while(c.StartMenuOpen && frames++<1000)yield return null;
            Check(!c.StartMenuOpen && !c.Paused && ReferenceEquals(career,c.Career),"Departure finishes automatically into the same career");
            Check(Vector3.Distance(start,c.TitleVoyage.Boat.position)>20 && Shader.GetGlobalFloat("_FishingVisualTimeOffset")==0,"Departure sails away and releases its shader clock");
            Check(FishingPrototype.FlatDistance(position,c.Player.engine.RB.position)<1,"Continuing does not teleport the playable vessel");
            var berth=c.Home.Berth+Vector3.up;var rb=c.Player.engine.RB;rb.position=berth;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=berth;Physics.SyncTransforms();
            for(int i=0;i<12;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Home remains accessible beside the handcraft area");
            c.Career.Backpack.TryAdd(FieldItem.Sand,8);c.Career.Backpack.TryAdd(FieldItem.Fiber,8);
            for(int game=0;game<2;game++)
            {
                var station=c.HandGames.Station(game);var seed=c.HandGames.Approach(game);
                Check(c.Shore.Relocate(seed,Quaternion.LookRotation(-station.forward)),"Handcraft station has reachable ground "+game);
                c.View.ResetLook();
                yield return null;c.PresentCharacterFrame();
                var cam=c.viewCamera.transform;cam.rotation=Quaternion.LookRotation(station.TransformPoint(new Vector3(0,1.1f,.15f))-cam.position);
                Check(c.HandGames.TryTarget(out int selected,out _) && selected==game,"Real world interaction selects the intended handcraft station "+game);
                Check(c.HandGames.Begin(game) && c.StoryBlocksGameplay,"Handcraft enters the shared input boundary "+game);
                float settled=Time.unscaledTime+.35f;while(Time.unscaledTime<settled)yield return null;
                c.PresentCharacterFrame();
                Check(c.Shore.Character.FirstPersonSkin.sharedMesh==c.Shore.Character.TabletopArmsMesh,"Tabletop view keeps both source forearms and hands");
                Check(c.Shore.Character.FirstPersonSkin.enabled && !c.Shore.Character.FirstPersonSkin.forceRenderingOff,"Mouse-operated tabletop view shows the working arms");
                var toolView=c.viewCamera.WorldToViewportPoint(c.HandGames.HandlePoint);
                Check(toolView.z>.1f && toolView.x>.05f && toolView.x<.95f && toolView.y>.05f && toolView.y<.85f,"Interactive sieve or crank fits within the usable view "+toolView);
                var extent=new Rect(toolView.x,toolView.y,0,0);
                foreach(var renderer in c.HandGames.ActiveTool.GetComponentsInChildren<Renderer>())
                {
                    if(!renderer.gameObject.activeInHierarchy)continue;var b=renderer.bounds;
                    for(int k=0;k<8;k++)
                    {
                        var p=c.viewCamera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                        Check(p.z>.25f,"Tabletop assets keep clear of the eyes "+game+" "+renderer.name+" "+p);
                        extent=Rect.MinMaxRect(Mathf.Min(extent.xMin,p.x),Mathf.Min(extent.yMin,p.y),Mathf.Max(extent.xMax,p.x),Mathf.Max(extent.yMax,p.y));
                    }
                }
                // The V035 side crank has a larger projected bounding box, while its open wheel stays readable.
                Check(extent.xMin>.02f && extent.xMax<.98f && extent.yMin>.02f && extent.yMax<.98f && extent.width*extent.height<.35f,"Complete instrument fits in the central working view "+extent);
                Capture(c,"handcraft-"+game);
                int sand=c.Career.Backpack.Count(FieldItem.Sand),fiber=c.Career.Backpack.Count(FieldItem.Fiber);
                Check(!c.RequestWorldInteraction(),"Active handcraft consumes other interactions");
                c.HandGames.Cancel();Check(!c.HandGames.Active && c.Career.Backpack.Count(FieldItem.Sand)==sand && c.Career.Backpack.Count(FieldItem.Fiber)==fiber,"Cancelling handcraft does not spend materials");
                yield return null;
                Check(c.Shore.Character.FirstPersonSkin.sharedMesh!=c.Shore.Character.TrackedHandsMesh,"Leaving handcraft restores the normal desktop body view");
                Check(c.HandGames.Begin(game),"Handcraft can be reopened before focus-loss check");
                typeof(FishingPrototype).GetMethod("OnApplicationFocus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(c,new object[]{false});
                Check(!c.HandGames.Active && c.Career.Backpack.Count(FieldItem.Sand)==sand && c.Career.Backpack.Count(FieldItem.Fiber)==fiber,"Application focus loss cancels handcraft without spending materials");
                yield return null;
                Check(c.HandGames.Begin(game),"Another round starts after cancelling");
                var task=c.HandGames.Task;
                if(game==0)for(int i=0;i<20;i++)for(int j=0;j<12;j++)task.Sample(i%2==0?-.1f:.1f,0,.02f);
                else for(int i=0;i<250;i++){float a=i*Mathf.PI/36;task.Sample(Mathf.Cos(a)*.1f,Mathf.Sin(a)*.1f,.02f);}
                Check(task.Complete,"Observed gesture sequence reaches completion");
                var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
                int reward=c.Career.Backpack.Count(game==0?FieldItem.Shell:FieldItem.Rope);
                try
                {
                    c.HandGames.Tick(null,mouse,true,false);
                    UnityEngine.InputSystem.LowLevel.InputState.Change(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{buttons=1});
                    c.HandGames.Tick(null,mouse,true,false);
                }
                finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);c.HandGames.Cancel();}
                Check(task.Claimed && c.Career.Backpack.Count(game==0?FieldItem.Shell:FieldItem.Rope)==reward+1,"Runtime completion saves one real reward and exits the input boundary");
                yield return null;
            }
            Check(c.Career.Backpack.TryAdd(FieldItem.StonePickaxe,1),"Pickaxe available for framing check");
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==FieldItem.StonePickaxe){if(i!=0)c.Career.Backpack.Move(i,0);break;}
            c.SelectFieldSlot(0);
            if(!c.FirstPerson)c.ToggleView();
            c.View.ResetLook();
            for(int i=0;i<10;i++)yield return null;
            c.PresentCharacterFrame();var tool=c.FieldWorld.HeldTool;
            Check(tool!=null && tool.gameObject.activeSelf,"Lowered pickaxe is still presented");
            var tip=c.viewCamera.WorldToViewportPoint(tool.TransformPoint(FishingFieldWorld.PickTip));
            Debug.Log("[V034] Pick tip viewport "+tip+" camera="+c.viewCamera.transform.eulerAngles);Capture(c,"pickaxe-lowered");
            Check(tip.z>0 && tip.y>.06f && tip.y<.95f,"Pick head fits within the normal first-person view");
            var closest=FishingContactGeometry.ClosestPoint(tool,c.viewCamera.transform.position);
            Check(FishingMapMath.Finite(closest.x) && FishingMapMath.Finite(closest.y),"Contact geometry remains valid after allocation optimization");
            Benchmark(tool,c.viewCamera.transform.position);
        }
        private static Vector3 PreviousClosestPoint(Transform root,Vector3 from)
        {
            var best=root.position;float distance=float.MaxValue;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();
                if(mesh==null || renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                if(mesh.isReadable)foreach(var vertex in mesh.vertices)
                {var p=filter.transform.TransformPoint(vertex);float d=(p-from).sqrMagnitude;if(d<distance){distance=d;best=p;}}
                else {var p=renderer.bounds.ClosestPoint(from);float d=(p-from).sqrMagnitude;if(d<distance){distance=d;best=p;}}
            }
            return best;
        }
        private static void Benchmark(Transform root,Vector3 from)
        {
            var previous=PreviousClosestPoint(root,from);var current=FishingContactGeometry.ClosestPoint(root,from);
            Check(Vector3.Distance(previous,current)<.0001f,"Optimized contact point matches the previous geometry to 0.1mm");
            const int iterations=40;var watch=new System.Diagnostics.Stopwatch();
            long before=GC.GetAllocatedBytesForCurrentThread();watch.Start();
            for(int i=0;i<iterations;i++)PreviousClosestPoint(root,from);
            watch.Stop();double previousMs=watch.Elapsed.TotalMilliseconds;long previousBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            watch.Reset();before=GC.GetAllocatedBytesForCurrentThread();watch.Start();
            for(int i=0;i<iterations;i++)FishingContactGeometry.ClosestPoint(root,from);
            watch.Stop();long currentBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            string report="40 pickaxe contact queries; previous="+previousMs.ToString("F2")+"ms; current="+watch.Elapsed.TotalMilliseconds.ToString("F2")+"ms. Managed allocation counter="+(previousBytes==0 && currentBytes==0?"unavailable in this Unity Mono runtime":previousBytes+" -> "+currentBytes+" bytes");
            File.WriteAllText("Logs/V034/contact-benchmark.txt",report);Debug.Log("[V034] "+report);
        }
    }
}
