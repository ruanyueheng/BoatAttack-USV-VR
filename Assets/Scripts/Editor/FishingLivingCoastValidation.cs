using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BoatAttack.EditorTools
{
    public static class FishingLivingCoastValidation
    {
        private static string _evidence;
        public static void Run()
        {
            foreach(string guid in UnityEditor.AssetDatabase.FindAssets("t:Model",new[]{"Assets/Resources/FishingFish"}))
                UnityEditor.AssetDatabase.ImportAsset(UnityEditor.AssetDatabase.GUIDToAssetPath(guid),UnityEditor.ImportAssetOptions.ForceUpdate);
            FishingV011Validation.Begin("LIVING-COAST");
        }
        private static void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException(label);
            Debug.Log("[LIVING-COAST] CHECK: "+label);
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            _evidence=Path.GetFullPath("Logs/v015-visual-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_evidence);
            Check(FishingUIFont.Font!=null && FishingUIFont.Font.HasCharacter('潮') && FishingUIFont.Font.HasCharacter('烤'),"Bundled Chinese UI glyphs available");
            var report=new System.Text.StringBuilder("species,triangles,vertices,length_m\n");
            for(int i=0;i<FishingSpecies.Count;i++)
            {
                var kind=(FishKind)i;var owned=new List<Object>();var fish=FishingArt.Fish(c.transform,owned,c.PaletteShader,kind);
                var mesh=fish.GetComponent<MeshFilter>().sharedMesh;
                int triangles=mesh.triangles.Length/3;
                Debug.Log("[LIVING-COAST] Mesh bounds "+kind+" "+mesh.bounds.size.ToString("F5"));
                Check(triangles<=5000 && mesh.subMeshCount==1,"Single-mesh fish budget "+kind+" / "+triangles+" triangles");
                float span=i>=12?Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y,mesh.bounds.size.z):mesh.bounds.size.z;
                Check(Mathf.Abs(span-FishingSpecies.LengthMeters(kind))<.001f,"Metre-scaled marine model "+kind);
                foreach(var v in mesh.vertices)CheckFinite(v);
                if(i>=4)Check((i<12?fish.name.StartsWith("Hunyuan") && fish.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap")!=null:mesh.colors32.Length==mesh.vertexCount)
                    && Resources.Load<Texture2D>("FishingFish/"+kind+"Thumbnail")!=null,"Imported fish, texture and journal preview "+kind);
                report.AppendLine(kind+","+triangles+","+mesh.vertexCount+","+mesh.bounds.size.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Object.Destroy(fish);foreach(var item in owned)Object.Destroy(item);
            }
            File.WriteAllText(Path.Combine(_evidence,"fish-budgets.csv"),report.ToString());
            c.MoveToSpotForAutomation(0);c.Interact();for(int f=0;f<12;f++)yield return null;
            for(int i=0;i<FishingSpecies.Count;i++)
            {
                var kind=(FishKind)i;int grams=FishingSpecies.Grams(kind,1);
                var fish=new CatchRecord{id="v014-visual-"+i,kind=kind,grams=grams,value=FishingSpecies.Value(kind,false,grams)};
                Check(c.Career.BeginPending(fish,0,0,0),"Create isolated pending catch "+kind);
                c.RestorePendingFishVisual();c.CatchRig.PrepareHandAction();c.MeasureCatch();
                for(int f=0;f<3;f++)yield return null;c.PresentCharacterFrame();
                var bounds=c.ActiveFish.GetComponent<Renderer>().bounds;
                Vector2 min=Vector2.one*100,max=Vector2.one*-100;float nearest=100;
                foreach(var vertex in c.ActiveFish.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    var v=c.viewCamera.WorldToViewportPoint(c.ActiveFish.TransformPoint(vertex));
                    min=Vector2.Min(min,v);max=Vector2.Max(max,v);nearest=Mathf.Min(nearest,v.z);
                }
                Debug.Log("[LIVING-COAST] INSPECT "+kind+" viewport="+min+" / "+max+" near="+nearest);
                Debug.Log("[LIVING-COAST] POSE eye="+c.viewCamera.transform.position+" fish="+c.ActiveFish.position+" size="+bounds.size+" scale="+c.ActiveFish.lossyScale+" shoulder="+c.Shore.Character.HandReferencePosition);
                if(i==0)Capture(c.viewCamera,"inspect-debug");
                // Tall fauna need their full silhouette, while the upper quarter stays clear and the near plane remains protected.
                if(!FishingSpecies.Of(kind).Legendary)Check(max.x-min.x<.85f && max.y-min.y<(i>=12?.75f:.55f) && (i<12 || max.y<.75f) && nearest>.15f,"Catch leaves sightline readable "+kind);
                if(i==0 || i==4 || i==11)Capture(c.viewCamera,"held-"+kind);
                Check(c.ResolveCatch(false),"Resolve isolated fish "+kind);
                yield return null;
            }
            c.ReturnToHarbor();var p=c.Shore.Sites[0].Berth+Vector3.up*.6f;var rb=c.Player.engine.RB;
            rb.position=p;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"Enter real coast for light test");for(int i=0;i<12;i++)yield return null;
            Equip(c,FieldItem.Lantern);c.View.ResetLook();c.Weather.Climate.Hold();
            AdvanceTo(c.Career.Calendar,12);c.Weather.ApplyEnvironment();for(int i=0;i<3;i++)yield return null;
            float day=Capture(c.viewCamera,"coast-day");
            AdvanceTo(c.Career.Calendar,23);c.Weather.ApplyEnvironment();for(int i=0;i<3;i++)yield return null;
            float night=Capture(c.viewCamera,"coast-night");
            c.UseFieldItem();for(int i=0;i<3;i++)yield return null;c.PresentCharacterFrame();
            float lamp=Capture(c.viewCamera,"coast-lantern");
            Debug.Log("[LIVING-COAST] LIGHT day="+day+" night="+night+" lamp="+lamp);
            Check(night<day*.8f,"Night image is materially darker than daylight");
            Check(lamp>night+.001f,"Lantern increases visible light on nearby geometry");
            AdvanceTo(c.Career.Calendar,12);c.Weather.ApplyEnvironment();
            Equip(c,FieldItem.StonePickaxe);for(int i=0;i<4;i++)yield return null;c.PresentCharacterFrame();Capture(c.viewCamera,"pick-grip");
            var hunt=Hunt(c);while(hunt.MoveNext())yield return null;
            TrackedHands(c);
            AuditCoast(c);
            Showcase(c);
            Debug.Log("[LIVING-COAST] EVIDENCE: "+_evidence);
        }
        private static IEnumerator Hunt(FishingPrototype c)
        {
            Equip(c,FieldItem.HuntingSpear);c.Shore.SetCrouch(true,true);
            FishingFieldWorld.Animal prey=null;Vector3 foot=Vector3.zero;
            foreach(var animal in c.FieldWorld.Animals)
            {
                if(animal.Kind!=LandAnimalKind.Rabbit || !animal.Root.gameObject.activeSelf)continue;
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI/8;var seed=animal.Feet+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.05f;
                    if(c.Shore.Ground(seed,out foot) && c.Shore.Clearance(foot)){prey=animal;break;}
                }
                if(prey!=null)break;
            }
            Check(prey!=null,"Real hunt fixture has a safe approach");
            c.Shore.Body.enabled=false;c.Shore.Actor.SetPositionAndRotation(foot+Vector3.up*.02f,Quaternion.LookRotation(Vector3.ProjectOnPlane(prey.Feet-foot,Vector3.up)));
            c.Shore.Body.enabled=true;Physics.SyncTransforms();c.Shore.Body.Move(Vector3.down*.1f);
            for(int i=0;i<18;i++){c.Shore.Move(Vector2.zero,false,false,true,.02f);yield return null;}
            c.Shore.Actor.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(prey.Feet-c.Shore.Position,Vector3.up));
            c.View.ResetLook();c.PresentCharacterFrame();
            var direction=prey.Root.position+Vector3.up*.22f-c.viewCamera.transform.position;
            float pitch=-Mathf.Asin(direction.normalized.y)*Mathf.Rad2Deg;c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));c.PresentCharacterFrame();
            int before=c.Career.Hunting.Harvests;
            Check(c.FieldWorld.BeginHunt(),"Live spear windup starts on the sensed rabbit / "+c.Message+" aim="+Vector3.Dot(c.viewCamera.transform.forward,direction.normalized)+" distance="+direction.magnitude);
            for(int i=0;i<90 && c.Career.Hunting.Harvests==before;i++){c.Shore.Move(Vector2.zero,false,false,true,.02f);yield return null;}
            Check(c.Career.Hunting.Harvests==before+1 && c.Career.Backpack.Count(FieldItem.RabbitMeat)>=2,"Spear-tip contact awards meat through the runtime path");
            Check(!prey.Root.gameObject.activeSelf,"Harvested animal leaves scene until recovery");
            int meat=c.Career.Backpack.Count(FieldItem.RabbitMeat);
            Check(FishingCareer.TryRestore(c.Career.Snapshot(),out var reload) && !reload.Hunting.Available(prey.Id,reload.Calendar.TotalMinutes),"Saved cooldown prevents immediate reload farming");
            for(int i=0;i<1199;i++)c.Career.Calendar.Tick(.25f,false);
            yield return null;
            Check(!prey.Root.gameObject.activeSelf,"Animal stays hidden before five active minutes");
            c.Career.Calendar.Tick(.25f,false);c.Career.Calendar.Tick(.25f,false);
            yield return null;yield return null;
            Check(prey.Root.gameObject.activeSelf && Vector3.Distance(prey.Root.position,prey.Home)<.5f
                && c.Career.Backpack.Count(FieldItem.RabbitMeat)==meat,"Live animal returns home without adding meat");
            Capture(c.viewCamera,"hunting-result");c.Shore.SetCrouch(false,true);
        }
        private static void TrackedHands(FishingPrototype c)
        {
            c.PresentCharacterFrame();var body=c.Shore.Character;var original=c.Hands.Right.Palm.position;
            var target=body.ShoulderPosition(false)+c.Shore.Actor.forward*.85f+Vector3.up*.12f;
            c.Hands.Right.SetPose(target,Quaternion.identity,.6f);body.ApplyHandTargets(c.Hands.Left,c.Hands.Right,true);
            Check(Vector3.Distance(body.PalmPosition(false),target)<.002f,"Tracked source hand reaches beyond desktop arm length without moving HMD");
            Check(body.TrackedHandsMesh!=null && body.TrackedHandsMesh.triangles.Length<body.FirstPersonSkin.sharedMesh.triangles.Length,"XR source skin removes forearm stretch triangles");
            c.PresentCharacterFrame();Check(Vector3.Distance(body.PalmPosition(false),target)>.05f,"Desktop pose restores after simulated tracking");
        }
        private static void CheckFinite(Vector3 p)
        {if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))throw new Exception("Nonfinite fish vertex");}
        private static void AdvanceTo(FishingCalendar clock,float hour)
        {double minutes=(hour*60-clock.Minute+1440)%1440;for(int i=0;i<Math.Ceiling(minutes/(.25*FishingCalendar.MinutesPerSecond));i++)clock.Tick(.25f,false);}
        private static void Equip(FishingPrototype c,FieldItem item)
        {
            var bag=c.Career.Backpack;if(bag.Count(item)==0)Check(bag.TryAdd(item,1),"Fixture equipment "+item);
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==item){if(i!=0)bag.Move(i,0);break;}c.SelectFieldSlot(0);
        }
        private static float Capture(Camera camera,string name)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.aspect=1280f/720;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(_evidence,name+".png"),texture.EncodeToPNG());
                var pixels=texture.GetPixels32();double value=0;int count=0;
                for(int y=20;y<400;y+=3)for(int x=200;x<1080;x+=3){var color=pixels[y*1280+x];value+=(color.r*.2126+color.g*.7152+color.b*.0722)/255;count++;}
                return (float)(value/count);
            }
            finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(texture);}
        }
        private static void AuditCoast(FishingPrototype c)
        {
            var report=new System.Text.StringBuilder("area,supported,clear,blocked\n");
            for(int site=0;site<c.Shore.Sites.Length;site++)
            {
                var s=c.Shore.Sites[site];Check(s.Available,"Named landing remains available "+site);
                int supported=0,clear=0;
                for(int x=-12;x<=12;x++)for(int z=-12;z<=12;z++)
                {
                    var seed=s.Landing+new Vector3(x*.5f,0,z*.5f);
                    if(!c.Shore.Ground(seed,out var floor) || Mathf.Abs(floor.y-s.Landing.y)>1)continue;
                    supported++;if(c.Shore.Clearance(floor))clear++;
                }
                Check(clear>35,"Walkable coastal samples "+site+" / "+clear);
                report.AppendLine(site+","+supported+","+clear+","+(supported-clear));
            }
            File.WriteAllText(Path.Combine(_evidence,"coastal-clearance.csv"),report.ToString());
            // Report exact blockers at resource sites rather than deleting intentionally overlapping scenery.
            foreach(var node in c.WorkshopWorld.Nodes)
            {
                bool approach=false;
                for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8;var p=node.Feet+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.85f;
                    if(c.Shore.Ground(p,out var feet) && c.Shore.Clearance(feet)){approach=true;break;}
                }
                Check(approach,"Resource has a standing approach "+node.Id);
            }
        }
        private static void Showcase(FishingPrototype c)
        {
            var root=new GameObject("Temporary fish catalog photograph");var owned=new List<Object>();var center=new Vector3(0,20,0);
            var reflections=new List<UnityEngine.Rendering.Universal.PlanarReflections>();
            foreach(var reflection in Object.FindObjectsOfType<UnityEngine.Rendering.Universal.PlanarReflections>())if(reflection.enabled){reflections.Add(reflection);reflection.enabled=false;}
            var camGo=new GameObject("Catalog camera");var cam=camGo.AddComponent<Camera>();cam.enabled=false;cam.cameraType=CameraType.Preview;cam.cullingMask=1<<29;cam.orthographic=true;cam.orthographicSize=1.9f;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.94f,.95f,.90f);cam.transform.position=center+new Vector3(0,0,-8);cam.transform.rotation=Quaternion.identity;
            try
            {
                for(int i=0;i<FishingSpecies.Count;i++)
                {
                    var kind=(FishKind)i;var go=FishingArt.Fish(root.transform,owned,c.PaletteShader,kind);
                    go.layer=29;go.transform.position=center+new Vector3((i%4-1.5f)*1.45f,(1-i/4)*1.15f+.12f,0);go.transform.rotation=Quaternion.Euler(0,82,0);
                    go.transform.localScale=Vector3.one*(1f/FishingSpecies.LengthMeters(kind));
                    var label=new GameObject("Species caption",typeof(TextMesh));label.layer=29;label.transform.SetParent(root.transform,false);label.transform.position=go.transform.position+Vector3.down*.48f;
                    var text=label.GetComponent<TextMesh>();text.font=FishingUIFont.Font;text.fontSize=48;text.characterSize=.045f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.10f,.25f,.26f);text.text=FishingSpecies.Name(kind);
                    label.GetComponent<MeshRenderer>().sharedMaterial=FishingUIFont.Font.material;
                }
                Capture(cam,"fish-catalog");
            }
            finally{foreach(var reflection in reflections)if(reflection!=null)reflection.enabled=true;Object.Destroy(root);Object.Destroy(camGo);foreach(var item in owned)Object.Destroy(item);}
        }
    }
}
