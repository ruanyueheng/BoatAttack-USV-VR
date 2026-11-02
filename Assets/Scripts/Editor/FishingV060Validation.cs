using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV060Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V060");
        private const string Evidence="Logs/V060";
        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException(message);Debug.Log("[V060] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory(Evidence);
            Check(c.HasIsolatedProfile && c.Automated,"Validation owns an isolated profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.Paused)c.TogglePause();
            foreach(var run in new[]{FishingV058Validation.Checks(c),HomeChecks(c),HeldChecks(c),CashierChecks(c),WellChecks(c)})
            {try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}}
        }
        public static void RunWell()=>FishingV011Validation.Begin("V060-WELL");
        public static IEnumerator WellChecks(FishingPrototype c)
        {
            Directory.CreateDirectory(Evidence+"/Well");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.Paused)c.TogglePause();
            var probe=c.gameObject.AddComponent<FishingFeedbackContactProbe>();int bin=-1,cycle=0;var rows=new List<string>();
            var mesh=new Mesh();
            probe.Sample=()=>
            {
                if(!c.FirstPerson || !c.Farm.Busy || !c.Farm.ActionHint.StartsWith("摇井绳"))return;
                int current=Mathf.FloorToInt(c.Farm.WorkProgress*24);
                if(current==bin)return;if(current<bin)cycle++;bin=current;
                c.Shore.Character.FirstPersonSkin.BakeMesh(mesh);
                int near=0;
                var vertices=mesh.vertices;
                foreach(int index in c.Shore.Character.FirstPersonSkin.sharedMesh.triangles.Distinct())
                {
                    var p=c.viewCamera.WorldToViewportPoint(c.Shore.Character.FirstPersonSkin.transform.TransformPoint(vertices[index]));
                    if(p.z>0 && p.z<.10f && p.x>0 && p.x<1 && p.y>0 && p.y<1)near++;
                }
                rows.Add(cycle+","+c.Farm.WorkProgress.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+near);
                FishingV016Validation.Capture(c,Evidence+"/Well","cycle-"+cycle+"-"+current.ToString("D2"));
            };
            var run=FishingV036FarmValidation.Checks(c);
            bool thirdPerson=false,firstPersonAgain=false,wasDrawing=false,cancelRestored=false,completeRestored=false;
            try
            {
                while(run.MoveNext())
                {
                    bool drawing=c.Farm.DrawingWell;
                    if(drawing && !thirdPerson && c.Farm.WorkProgress>.30f)
                    {
                        c.ToggleView();c.PresentCharacterFrame();var character=c.Shore.Character;
                        Check(!c.FirstPerson && !character.FirstPersonSkin.enabled
                            && character.HeadSkin.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On
                            && character.BodySkin.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On,
                            "Switching to third person while drawing the well restores the visible full body and head in the same frame");
                        FishingV016Validation.Capture(c,Evidence+"/Well","thirdperson-full-body");thirdPerson=true;
                    }
                    else if(drawing && thirdPerson && !firstPersonAgain && c.Farm.WorkProgress>.40f)
                    {
                        c.ToggleView();c.PresentCharacterFrame();var character=c.Shore.Character;
                        Check(c.FirstPerson && character.FirstPersonSkin.enabled && character.FirstPersonSkin.sharedMesh==character.TabletopArmsMesh,
                            "Returning to first person during well drawing restores the near-eye tabletop mask in the same frame");
                        firstPersonAgain=true;
                    }
                    if(wasDrawing && !drawing)
                    {
                        c.PresentCharacterFrame();var character=c.Shore.Character;
                        Check(!character.FirstPersonSkin.enabled || character.FirstPersonSkin.sharedMesh!=character.TabletopArmsMesh,
                            "Finishing or cancelling well drawing releases the tabletop mask");
                        if(c.Career.Farm.Water==FishingFarm.WaterCapacity)completeRestored=true;else cancelRestored=true;
                    }
                    wasDrawing=drawing;yield return run.Current;
                }
            }
            finally
            {
                probe.Sample=null;UnityEngine.Object.Destroy(probe);UnityEngine.Object.Destroy(mesh);(run as IDisposable)?.Dispose();
                File.WriteAllLines(Evidence+"/well-near-eye.csv",new[]{"cycle,progress,visibleBodyVerticesWithin10cm"}.Concat(rows));
            }
            Check(rows.Count>=30,"The full well sequence was sampled before, during and after raising and pouring");
            Check(rows.All(row=>row.EndsWith(",0")),"Visible body triangles stay outside the central near-eye region throughout well drawing");
            Check(thirdPerson && firstPersonAgain && cancelRestored && completeRestored,"Well drawing covers both view switches and mask restoration after cancellation and completion");
        }
        private static IEnumerator HomeChecks(FishingPrototype c)
        {
            c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"The home island has a real supported landing");yield return null;
            var profile=new FishingCareer();profile.Backpack.TryAdd(FieldItem.Aquarium,1);Check(profile.BuildAquarium(),"Build aquarium fixture");
            var fish=new CatchRecord{id="v060-aquarium",kind=FishKind.Coral,grams=800,value=FishingSpecies.Value(FishKind.Coral,false,800)};
            Check(profile.Store(fish) && profile.DisplayFish(fish.id),"A saved fish is displayed before relocating the aquarium");
            c.RestoreShopTestProfile(profile.Snapshot());
            c.HomeEcology.Refresh();yield return null;Physics.SyncTransforms();
            var point=c.HomeEcology.AquariumPosition;
            Check(point.x-c.Home.Origin.x>7 && Mathf.Abs(point.z-c.Home.Origin.z)<5,"Aquarium is outside the house on the east island plot");
            var land=c.Home.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();
            foreach(var offset in new[]{new Vector3(-1,0,-.5f),new Vector3(1,0,-.5f),new Vector3(-1,0,.5f),new Vector3(1,0,.5f)})
                Check(land.Raycast(new Ray(point+offset+Vector3.up*20,Vector3.down),out var hit,40) && Mathf.Abs(hit.point.y-point.y)<.03f,"Tank footprint rests on the authored island "+offset);
            Check(c.Shore.Ground(point+Vector3.back*1.65f,out var approach) && c.Shore.Relocate(approach,Quaternion.identity),"The outdoor tank has a walkable viewing approach");
            c.View.ResetLook();yield return null;FishingV036FarmValidation.Aim(c,point+Vector3.up*.9f);
            Check(!c.Home.Inside && c.HomeEcology.NearbyHint!=null && c.Career.AquariumCount==1,"Existing displayed fish and outdoor interaction survive relocation");
            FishingV016Validation.Capture(c,Evidence,"aquarium-outdoors");
            var saved=profile.Snapshot();saved.version=20;saved.homeStorage=new FishingBackpack(false).Snapshot();
            for(int i=0;i<36;i++)saved.homeStorage[i]=new FieldStack{item=FieldItem.Stone,count=64};
            var json=JsonUtility.ToJson(saved);var old=JsonUtility.FromJson<FishingSaveData>(json);
            Check(FishingCareer.TryRestore(old,out var expanded) && expanded.HomeStorage.Capacity==144 && expanded.HomeStorage.Count(FieldItem.Stone)==2304,"Full legacy JSON chest expands to 144 slots with every original item");
            Check(expanded.HomeStorage.TryAdd(FieldItem.Shell,108*64) && !expanded.HomeStorage.TryAdd(FieldItem.Shell,1),"The additional 108 slots fill and stop at their real capacity");
            string path=Path.Combine(Application.temporaryCachePath,"v060-storage-"+Guid.NewGuid().ToString("N")+".json");
            FishingSaveStore.Save(path,expanded);var reloaded=FishingSaveStore.Load(path,out var warning);
            Check(warning=="" && reloaded.HomeStorage.At(143).count==64 && reloaded.HomeStorage.TransferTo(reloaded.Backpack,143),"Real JSON reload and withdrawal from slot 144 conserve the saved stack");
            c.RestoreShopTestProfile(expanded.Snapshot());
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(3.4f,.2f,-2.8f),Quaternion.identity),"Reach the original storage chest");yield return null;
            Check(c.Home.OpenStorage(),"The real chest opens its storage panel");
            var hud=c.GetComponent<FishingHUD>();typeof(FishingHUD).GetField("_storagePage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(hud,3);
            for(int i=0;i<5;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Evidence+"/storage-page-four.png"));for(int i=0;i<5;i++)yield return null;c.ToggleInventory();
        }
        private static IEnumerator HeldChecks(FishingPrototype c)
        {
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(-1,.2f,-6.4f),Quaternion.identity),"Tool audit uses a clear porch stance");
            c.View.ResetLook();if(!c.FirstPerson)c.ToggleView();yield return null;
            var seed=new FishingCareer().Snapshot();var rows=new List<string>();
            foreach(FieldItem item in Enum.GetValues(typeof(FieldItem)))
            {
                if(item==FieldItem.Empty || item==FieldItem.HuntingRifle)continue;
                c.RestoreShopTestProfile(seed);
                var bag=c.Career.Backpack;
                if(item==FieldItem.FieldGuide)bag.Select(0);
                else{Check(bag.TryAdd(item,1),"Equip "+item);bag.Select(1);}
                c.SelectFieldSlot(bag.Selected);
                for(int i=0;i<10;i++)yield return null;c.PresentCharacterFrame();
                bool net=item==FieldItem.CaptureNet;
                Check(net?!c.FieldWorld.HeldTool.gameObject.activeSelf:c.FieldWorld.PresentedItem==item,"Exactly one visible held representation: "+item);
                float error=Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position);
                Check(c.Hands.Right.Visible && error<.09f,"The visible right palm reaches its actual item grip: "+item+" error="+error.ToString("F4"));
                rows.Add(item+","+error.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
                FishingV016Validation.Capture(c,Evidence,"held-"+item);
            }
            File.WriteAllLines(Path.Combine(Evidence,"held-audit.csv"),new[]{"item,palmErrorMetres"}.Concat(rows));
        }
        private static IEnumerator CashierChecks(FishingPrototype c)
        {
            c.ReturnToHarbor();yield return null;var shop=c.TackleShop;var rb=c.Player.engine.RB;
            rb.position=shop.mooringPoint.position;rb.rotation=shop.transform.rotation;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark() && c.Shore.Relocate(shop.purchasePoint.position,shop.transform.rotation),"Reach cashier activity audit");yield return null;
            c.Residents.Close();var cashier=shop.Cashier;var at=cashier.transform.position;
            var skin=(SkinnedMeshRenderer)cashier.GetComponent<LODGroup>().GetLODs()[0].renderers[0];
            var gestures=new HashSet<string>();var elbow=skin.bones[6];var start=elbow.localRotation;float maxAngle=0;
            var rest=skin.transform.parent.GetComponent<MeshFilter>().sharedMesh.vertices;
            var forearms=new List<int>();var feet=new List<int>();var hands=new List<int>();
            for(int i=0;i<rest.Length;i++)
            {
                var p=cashier.transform.InverseTransformPoint(skin.transform.TransformPoint(rest[i]));
                if(p.y<.12f)feet.Add(i);
                if(p.y>1.18f && p.y<1.43f && Mathf.Abs(p.x)>.13f && Mathf.Abs(p.x)<.33f && p.z>-.02f)forearms.Add(i);
                if(p.y>1.18f && p.y<1.34f && Mathf.Abs(p.x)<.12f && p.z>.18f)hands.Add(i);
            }
            Check(forearms.Count>20 && feet.Count>20 && hands.Count>20,"Cashier deformation checks select real source forearm, hand and planted-foot vertices");
            var baked=new Mesh();float maxForearm=0,maxMeanForearm=0,maxFoot=0,maxHandHeight=0;
            var metrics=new List<string>{"time,gesture,forearmMaxMetres,forearmMeanMetres,footMaxMetres,handTopMetres"};
            void SampleSkin()
            {
                skin.BakeMesh(baked);var vertices=baked.vertices;float peak=0,sum=0,foot=0,top=0;
                Check(vertices.Length==rest.Length && baked.bounds.size.x<1.35f && baked.bounds.size.y<2.2f && baked.bounds.size.z<.95f,"Cashier skin retains bounded proportions throughout the gesture");
                foreach(int i in forearms){float d=Vector3.Distance(rest[i],vertices[i]);peak=Mathf.Max(peak,d);sum+=d;}
                foreach(int i in feet)foot=Mathf.Max(foot,Vector3.Distance(rest[i],vertices[i]));
                foreach(int i in hands)top=Mathf.Max(top,cashier.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i])).y);
                Check(!float.IsNaN(peak+sum+foot+top) && !float.IsInfinity(peak+sum+foot+top) && peak<.60f,"Cashier baked forearms remain finite and do not spike away from the body");
                maxForearm=Mathf.Max(maxForearm,peak);maxMeanForearm=Mathf.Max(maxMeanForearm,sum/forearms.Count);maxFoot=Mathf.Max(maxFoot,foot);maxHandHeight=Mathf.Max(maxHandHeight,top);
                metrics.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:F3},{1},{2:F5},{3:F5},{4:F5},{5:F5}",Time.time,cashier.Gesture,peak,sum/forearms.Count,foot,top));
            }
            void Capture(string name)
            {
                var eye=cashier.transform.TransformPoint(new Vector3(.45f,1.65f,2.15f));
                c.viewCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(cashier.FacePosition-Vector3.up*.3f-eye));
                FishingV016Validation.Capture(c,Evidence,name);
            }
            float until=Time.time+35,captureAt=Time.time+.8f,nextSample=0;string pending=cashier.Gesture;
            try
            {
                while(Time.time<until)
                {
                    maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(start,elbow.localRotation));
                    if(cashier.Gesture!=pending){pending=cashier.Gesture;captureAt=Time.time+.8f;}
                    if(Time.time>=captureAt && gestures.Add(pending))Capture("cashier-"+gestures.Count);
                    if(Time.time>=nextSample){SampleSkin();nextSample=Time.time+.15f;}
                    yield return null;
                }
                Check(c.Residents.Talk(4),"The real cashier conversation takes over from idle activities");
                yield return new WaitForSeconds(.8f);SampleSkin();Capture("cashier-conversation");c.Residents.Close();
            }
            finally
            {
                UnityEngine.Object.Destroy(baked);c.Residents.Close();
                File.WriteAllLines(Path.Combine(Evidence,"cashier-skin-motion.csv"),metrics);
            }
            Check(gestures.Count>=4 && maxAngle>8,"Cashier cycles at least four distinct activities with independently articulated forearms");
            Check(maxForearm>.10f && maxMeanForearm>.025f && maxHandHeight>1.38f,"Actual skinned forearms move visibly and the hands rise above the counter: peak="+maxForearm+", mean="+maxMeanForearm+", handTop="+maxHandHeight);
            Check(maxFoot<.002f,"Cashier gestures keep the actual skinned feet planted: "+maxFoot);
            Check(Vector3.Distance(at,cashier.transform.position)<.0001f,"Cashier activities preserve the planted shop anchor");
            File.WriteAllLines(Path.Combine(Evidence,"cashier-activities.txt"),gestures);
        }
    }
}
