using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    [InitializeOnLoad]
    public static class FishingExpeditionValidation
    {
        private const string Key="Fishing.ExpeditionValidation.";
        private static IEnumerator _checks;
        private static float _next;
        static FishingExpeditionValidation()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"active",false))return;
                SessionState.SetBool(Key+"active",false);
                string error=SessionState.GetString(Key+"error","");
                if(!SessionState.GetBool(Key+"finished",false) && error=="")error="Stopped before completing checks";
                if(error=="")Debug.Log("[EXPEDITION] PASS: isolated scene interactions, walking, dial, short north sailing approach, catch evidence and JSON migration/recovery; not full-route, physical keyboard or visual acceptance.");
                else Debug.LogError("[EXPEDITION] FAIL: "+error);
                EditorApplication.Exit(error==""?0:1);
            };
            Application.logMessageReceived+=(message,stack,type)=>
            {
                if(SessionState.GetBool(Key+"active",false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))SessionState.SetString(Key+"error",message);
            };
        }
        public static void Capture()
        {
            SessionState.SetBool(Key+"capture",true);
            SessionState.SetString(Key+"images",Path.GetFullPath(Path.Combine("Logs","expedition-preview-"+Guid.NewGuid().ToString("N"))));
            Run();
        }
        private static void Snapshot(FishingPrototype c,string name,Vector3 target,Vector3 offset,bool keepView=false)
        {
            if(!SessionState.GetBool(Key+"capture",false))return;
            var camera=c.viewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var oldTarget=camera.targetTexture;var active=RenderTexture.active;float fov=camera.fieldOfView;
            var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);Texture2D image=null;
            try
            {
                if(!keepView){camera.fieldOfView=46;camera.transform.SetPositionAndRotation(target+offset,Quaternion.LookRotation(-offset));}
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                string directory=SessionState.GetString(Key+"images","");Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());Debug.Log("[EXPEDITION] IMAGE: "+path);
            }
            finally
            {
                camera.targetTexture=oldTarget;camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);
                RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);
            }
        }
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run through the batch validation script to guarantee an isolated profile.");
            SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"finished",false);SessionState.SetString(Key+"error","");
            SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+360);
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))SessionState.SetString(Key+"error","Timeout");
            if(SessionState.GetString(Key+"error","")!="")
            {
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                else {Debug.LogError("[EXPEDITION] FAIL: "+SessionState.GetString(Key+"error",""));EditorApplication.Exit(1);}
                return;
            }
            if(!EditorApplication.isPlaying || Time.time<_next)return;
            var c=UnityEngine.Object.FindObjectOfType<FishingPrototype>();
            if(c==null || c.Expedition==null || c.Career==null)return;
            try
            {
                if(_checks==null)_checks=Checks(c);
                _next=Time.time+.02f;
                if(!_checks.MoveNext()){SessionState.SetBool(Key+"finished",true);EditorApplication.ExitPlaymode();}
            }
            catch(Exception ex){SessionState.SetString(Key+"error",ex.ToString());EditorApplication.ExitPlaymode();}
        }
        private static void Check(bool ok,string label)
        {
            if(!ok)throw new Exception(label);
            Debug.Log("[EXPEDITION] CHECK: "+label);
        }
        private static void SaveCheck(FishingPrototype c,string label)
        {
            var restored=FishingSaveStore.Load(c.SavePath,out string warning);
            Check(warning=="" && restored.Expedition.Facts==c.Career.Expedition.Facts && restored.Count==c.Career.Count && restored.Coins==c.Career.Coins,"JSON stage "+label);
        }
        private static void Land(FishingPrototype c,int site)
        {
            c.ReturnToHarbor();var shore=c.Shore;var location=shore.Sites[site];Check(location.Available,"site available "+site);
            var side=Vector3.ProjectOnPlane(location.Berth-location.Landing,Vector3.up).normalized;
            var p=location.Berth+side*5+Vector3.up;var rb=c.Player.engine.RB;
            c.Player.transform.position=p;rb.position=p;rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();
            Check(shore.TryDisembark(),"disembark "+site+" "+c.Message);
        }
        private static IEnumerator Walk(FishingPrototype c,Vector3 point,float stopDistance=1.4f)
        {
            int guard=0;
            while(FishingPrototype.FlatDistance(c.Shore.Position,point)>stopDistance && guard++<600)
            {
                c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(point-c.Shore.Position,Vector3.up));
                c.Shore.Walk(Vector2.up,false,.02f);yield return null;
            }
            Check(guard<600,"walk to "+point+" actual="+c.Shore.Position);
        }
        private static void Catch(FishingPrototype c,bool fillBeforeLanding=false)
        {
            Check(c.Cast(),"real cast at ground "+c.Spot);
            var s=c.Session;int guard=0;
            while(s.Phase==CatchPhase.Waiting && guard++<1000)s.Tick(.02f,false);
            Check(c.Hook(),"real hook");guard=0;
            while(s.Phase==CatchPhase.Fighting && guard++<6000)s.Tick(.02f,s.Tension<.6f);
            while(s.Phase==CatchPhase.Landing && !s.CanLand && guard++<6500)s.Tick(.02f,false);
            if(fillBeforeLanding)
            {
                while(c.Career.Count<c.Career.Capacity)
                    Check(c.Career.Store(new CatchRecord { id=Guid.NewGuid().ToString("N"),kind=FishKind.Coral,grams=1000,value=FishingSpecies.Value(FishKind.Coral,false,1000) }),"fill hold after cast");
            }
            c.View.ApplyView();c.Hands.Apply();Snapshot(c,"07-net-grip",Vector3.zero,Vector3.zero,true);
            Check(c.Scoop(),"real scoop/storage path");
        }
        private static void MigrationChecks()
        {
            string directory=Path.Combine(Application.temporaryCachePath,"expedition-migration-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string legacy=Path.Combine(directory,"boatattack-fishing-v1.json"), current=Path.Combine(directory,"boatattack-fishing-v5.json");
            var data=new FishingCareer().Snapshot();data.version=4;data.coins=40;
            string original=JsonUtility.ToJson(data,true);File.WriteAllText(legacy,original);
            var migrated=FishingSaveStore.LoadWithLegacyFallback(current,legacy,out string warning);
            Check(warning!="" && migrated.Coins==40 && migrated.Expedition.Facts==0,"legacy profile read without mutating old file");
            migrated.Expedition.Accept();FishingSaveStore.Save(current,migrated);
            Check(File.ReadAllText(legacy)==original,"schema v5 save leaves v4 profile byte-identical");
            var reloaded=FishingSaveStore.LoadWithLegacyFallback(current,legacy,out warning);
            Check(warning=="" && reloaded.Expedition.Has(FishingExpedition.Accepted),"v5 profile takes priority over legacy");
            FishingSaveStore.Save(current,reloaded);File.WriteAllText(current,"corrupt");
            reloaded=FishingSaveStore.LoadWithLegacyFallback(current,legacy,out warning);
            Check(warning!="" && reloaded.Expedition.Has(FishingExpedition.Accepted),"v5 backup takes priority over legacy");
            File.WriteAllText(current+".bak","corrupt");
            reloaded=FishingSaveStore.LoadWithLegacyFallback(current,legacy,out warning);
            Check(warning!="" && reloaded.Coins==0 && reloaded.Expedition.Facts==0,"unreadable v5 does not silently roll back to legacy");
        }
        private static void NorthCorridorCheck(FishingPrototype c)
        {
            // Collision-clear samples, not a claim of a full harbor-to-island sailing playthrough.
            var site=c.Shore.Sites[1];
            var start=site.Berth+Vector3.ProjectOnPlane(site.Berth-site.Landing,Vector3.up).normalized*5;
            var end=c.fishingSpots[FishingGrounds.NorthIsland];
            for(int i=0;i<=18;i++)
            {
                var point=Vector3.Lerp(start,end,i/18f);point.y=.6f;
                foreach(var hit in Physics.OverlapBox(point,new Vector3(1.2f,.7f,1.5f),Quaternion.identity,~(1<<4),QueryTriggerInteraction.Ignore))
                {
                    if(hit.transform.IsChildOf(c.Player.transform) || hit==c.Shore.Body
                        || hit.GetComponentInParent<WaterSystem.Water>()!=null || hit.name=="NavMesh")continue;
                    Check(false,"north corridor blocked by "+hit.name+" at "+point);
                }
            }
            Check(true,"sampled north offshore berth-to-ground corridor is collision-clear");
        }
        private static IEnumerator SailNorth(FishingPrototype c)
        {
            c.ReturnToHarbor();
            var site=c.Shore.Sites[1];var rb=c.Player.engine.RB;
            var start=site.Berth+Vector3.ProjectOnPlane(site.Berth-site.Landing,Vector3.up).normalized*5+Vector3.up;
            var end=c.fishingSpots[FishingGrounds.NorthIsland];
            var facing=Quaternion.LookRotation(Vector3.ProjectOnPlane(end-start,Vector3.up));
            c.Player.transform.SetPositionAndRotation(start,facing);rb.position=start;rb.rotation=facing;
            rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();
            // Only initial placement is synthetic. Traverse the new short corridor using the
            // real engine, buoyancy and colliders, rather than teleporting to the fishing ground.
            int guard=0;
            while(FishingPrototype.FlatDistance(rb.position,end)>2f && guard++<1200)
            {
                c.Player.engine.Accelerate(.25f);yield return null;
            }
            Check(guard<1200,"real engine sails north offshore berth-to-ground; position="+rb.position);
            rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
            c.Interact();Check(c.Anchored && c.Spot==FishingGrounds.NorthIsland,"anchor after real north approach");
        }
        private static void PropChecks(FishingPrototype c)
        {
            string[] names={"CrateBody","CargoSignService","CargoSignFish","CargoSignCloth","RepairCoil","BeaconBody","BeaconHead","BeaconLens","MaintenanceRecord","ChannelMarker"};
            var palette=Resources.Load<Material>("FishingProps/HarborPalette");
            Check(palette!=null && palette.shader.isSupported,"authored prop palette supported");
            foreach(var name in names)
            {
                var prefab=Resources.Load<GameObject>("FishingProps/"+name);
                Check(prefab!=null,"authored prop imported: "+name);
                var filters=prefab.GetComponentsInChildren<MeshFilter>();
                Check(filters.Length==1 && prefab.GetComponentsInChildren<Renderer>().Length==1,"one mesh/renderer: "+name);
                var mesh=filters[0].sharedMesh;
                Check(mesh!=null && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color),"vertex colours imported: "+name);
                Check(mesh.subMeshCount==1 && mesh.GetIndexCount(0)/3<14000,"triangle/submesh budget: "+name);
                var size=Vector3.Scale(mesh.bounds.size,filters[0].transform.lossyScale);
                float extent=Mathf.Max(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
                Check(extent>.1f && extent<5f,"metre-scale FBX: "+name);
                Check(prefab.GetComponentsInChildren<Collider>().Length==0,"no prop gameplay colliders: "+name);
            }
            var visual=c.GetComponentInChildren<FishingBeaconVisual>();
            Check(visual!=null,"authored beacon exists before repair");
            Check(visual.transform.Find("RepairCoil")!=null && !visual.transform.Find("RepairCoil").gameObject.activeSelf,"installed coil hidden before repair");
        }
        private static IEnumerator Checks(FishingPrototype c)
        {
            yield return null;
            Check(c.HasIsolatedProfile && c.Automated && c.SavePath.StartsWith(Application.temporaryCachePath),"isolated test profile only");
            Check(c.Career.Expedition.Facts==0,"fresh quest facts");
            MigrationChecks();NorthCorridorCheck(c);
            PropChecks(c);
            Check(c.Hands!=null && c.Hands.Left.Skin.bones.Length==16 && c.Hands.Right.Skin.bones.Length==16,"two articulated gloves with fifteen finger joints each");
            Check(c.Hands.Left.Skin.sharedMesh.vertexCount<12000,"glove vertex budget");
            Check(c.fishingSpots.Length==4 && c.NavigationCount==9,"four grounds and four shore destinations");
            c.MoveToSpotForAutomation(3);c.Interact();Check(!c.Anchored,"north ground locked before repair");
            c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();
            c.View.ApplyView();c.Hands.Apply();
            Check(c.Hands.Left.gameObject.activeSelf && c.Hands.Right.gameObject.activeSelf,"first person shows both hands");
            Snapshot(c,"04-first-person-rod",Vector3.zero,Vector3.zero,true);
            c.Hands.Left.Pose(0);var baked=new Mesh();c.Hands.Left.Skin.BakeMesh(baked);var open=baked.vertices;
            c.Hands.Left.Pose(1);c.Hands.Left.Skin.BakeMesh(baked);var closed=baked.vertices;int changed=0;for(int i=0;i<open.Length;i++)if((open[i]-closed[i]).sqrMagnitude>.000001f)changed++;
            Check(changed>100,"finger pose deforms actual skinned vertices");UnityEngine.Object.Destroy(baked);c.Hands.Apply();
            c.ToggleView();c.Hands.Apply();Check(!c.Hands.Left.gameObject.activeSelf && !c.Hands.Right.gameObject.activeSelf,"third person hides first-person hands");c.ToggleView();
            Catch(c);
            Check(c.Career.Expedition.Facts==0,"original ground catch cannot advance quest");
            c.ReturnToHarbor();c.SellAll();
            // Discover both clues before accepting, including wrong-box feedback.
            Land(c,3);
            FishingLookRegression.CheckLanding(c);
            c.View.ApplyView();var facing=c.viewCamera.transform.rotation;var feet=c.Shore.Position;
            c.View.ApplyShoreLookDelta(new Vector2(500,-160));c.View.ApplyView();
            Check(Quaternion.Angle(facing,c.viewCamera.transform.rotation)>50 && c.Shore.Position==feet,"shore look turns camera without movement or right mouse button");
            var pausedFacing=c.viewCamera.transform.rotation;c.TogglePause();c.View.ApplyShoreLookDelta(new Vector2(200,200));c.View.ApplyView();
            Check(Quaternion.Angle(pausedFacing,c.viewCamera.transform.rotation)<.01f,"pause prevents shore look");c.TogglePause();
            c.View.ApplyShoreLookDelta(new Vector2(float.NaN,0));c.View.ApplyView();Check(!float.IsNaN(c.viewCamera.transform.rotation.x),"invalid look delta rejected");
            c.Hands.Apply();Snapshot(c,"05-shore-hands",Vector3.zero,Vector3.zero,true);c.View.ResetLook();
            Check(c.Expedition.Resolve()==1,"south landing resolves fish box");
            int before=c.Career.ShoreTaskMask;c.Interact();Check(c.Career.Expedition.Facts==0 && c.Career.ShoreTaskMask==before,"one E only inspects wrong box");
            var walk=Walk(c,c.Shore.Sites[3].Tasks[1]);while(walk.MoveNext())yield return null;
            Check(c.Expedition.Resolve()==3,"stripe box reachable");c.Interact();Check(c.Career.Expedition.Facts==0,"second decoy cannot grant component");
            walk=Walk(c,c.Shore.Sites[3].Tasks[0]);while(walk.MoveNext())yield return null;
            Check(c.Expedition.Resolve()==2 && c.Expedition.ActionTitle.Contains("双环"),"correct box prompt and resolver agree");
            Snapshot(c,"01-component-crate",c.Shore.Sites[3].Tasks[0]+new Vector3(.65f,.45f,0),new Vector3(2,1.4f,-2.7f));
            before=c.Career.ShoreTaskMask;c.Interact();Check(c.Career.Expedition.Facts==4 && c.Career.ShoreTaskMask==before,"component before acceptance; no double survey");SaveCheck(c,"component");
            c.Hands.Apply();Snapshot(c,"06-held-repair-coil",Vector3.zero,Vector3.zero,true);
            c.Interact();Check(c.Career.ShoreTaskMask!=before,"next E can still perform original survey");
            Land(c,0);walk=Walk(c,c.Shore.Sites[0].Tasks[0]);while(walk.MoveNext())yield return null;
            Check(c.Expedition.Resolve()==0,"record reachable");before=c.Career.ShoreTaskMask;c.Interact();
            Check(c.Career.Expedition.Facts==6 && c.Career.ShoreTaskMask==before,"record before acceptance; no double survey");SaveCheck(c,"record");
            c.ReturnToHarbor();c.Expedition.PortAction();Check(c.Career.Expedition.Facts==7,"accept recognizes previous discoveries");SaveCheck(c,"accepted");
            Land(c,2);walk=Walk(c,new Vector3(92.8f,0,-65),.2f);while(walk.MoveNext())yield return null;
            walk=Walk(c,c.Expedition.BeaconPosition);while(walk.MoveNext())yield return null;
            Check(c.Expedition.Resolve()==4 && c.Expedition.ActionTitle.Contains("校准"),"beacon reachable and prompt agrees");
            Snapshot(c,"02-beacon-broken",c.Expedition.BeaconPosition+Vector3.up*1.5f,new Vector3(-5,2.2f,-5));
            c.Interact();Check(c.Expedition.Aligning,"E starts alignment");var position=c.Shore.Position;
            c.Shore.Walk(Vector2.up,true,.1f);Check(Vector3.Distance(position,c.Shore.Position)<.001f,"dial blocks walking");
            c.Interact();Check(c.Career.Expedition.Facts==7 && c.Expedition.Aligning,"wrong confirmation preserves component");
            c.Expedition.Cancel();c.Shore.Walk(Vector2.up,false,.02f);Check(!c.Expedition.Aligning && Vector3.Distance(position,c.Shore.Position)>.01f,"cancel restores walking");
            c.Interact();Check(c.Expedition.Aligning,"retry after cancel");c.TogglePause();Check(!c.Expedition.Aligning,"pause releases dial");c.TogglePause();
            c.Interact();c.SendMessage("OnApplicationFocus",false);Check(!c.Expedition.Aligning,"focus loss releases dial");
            c.Interact();c.ToggleJournal();Check(!c.Expedition.Aligning && c.JournalOpen,"journal releases dial");c.ToggleJournal();
            c.Interact();c.ReturnToHarbor();Check(!c.Expedition.Aligning && c.AtHarbor && c.Career.Expedition.Facts==7,"rescue releases dial and preserves component");
            Land(c,2);walk=Walk(c,new Vector3(92.8f,0,-65),.2f);while(walk.MoveNext())yield return null;
            walk=Walk(c,c.Expedition.BeaconPosition);while(walk.MoveNext())yield return null;
            c.Interact();c.Expedition.Dial.Rotate(c.Expedition.Dial.Error);before=c.Career.ShoreTaskMask;c.Interact();
            Check(c.Career.Expedition.Facts==15 && !c.Expedition.Aligning && c.Career.ShoreTaskMask==before,"aligned explicit confirm repairs only once");SaveCheck(c,"repaired");
            yield return null;
            var visual=c.GetComponentInChildren<FishingBeaconVisual>();Check(visual!=null,"beacon visual exists");
            bool lit=false;foreach(var renderer in visual.GetComponentsInChildren<Renderer>())if(renderer.GetComponent<MeshFilter>()?.sharedMesh?.name=="BeaconLens")lit=renderer.sharedMaterial.name=="Beacon restored luminous lens";
            Check(lit,"repair changes lens material");
            Check(visual.transform.Find("RepairCoil").gameObject.activeSelf,"repair reveals installed coil");
            var head=visual.transform.Find("Rotating lantern housing");
            Check(Mathf.Abs(Mathf.DeltaAngle(head.localEulerAngles.y,c.Expedition.Dial.Target))<.1f,"authored head follows target bearing");
            Snapshot(c,"03-beacon-restored",c.Expedition.BeaconPosition+Vector3.up*1.5f,new Vector3(-5,2.2f,-5));
            c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();Catch(c);Check(c.Career.Expedition.Facts==15,"wrong ground still rejected after repair");
            c.ReturnToHarbor();c.SellAll();var sailing=SailNorth(c);while(sailing.MoveNext())yield return null;
            Check(c.Anchored && c.Spot==3,"north ground unlocked");
            Catch(c,true);Check(c.Career.Expedition.Facts==15 && c.Career.Count==c.Career.Capacity,"full hold at scoop cannot record evidence");
            Check(!c.Cast(),"full hold rejects another cast");c.ReturnToHarbor();c.SellAll();
            c.MoveToSpotForAutomation(3);c.Interact();
            Catch(c);Check(c.Career.Expedition.Facts==31 && c.Career.Count==1,"north newly stored coral records evidence");SaveCheck(c,"caught");
            Check(!c.MessageIsCatch && c.VisibleMessage.Contains("捕获记录"),"quest success notice is not suppressed by catch card");
            c.ReturnToHarbor();c.SellAll();Check(c.Career.Count==0 && c.Career.Expedition.Facts==31,"sale keeps evidence");
            before=c.Career.Coins;c.Interact();Check(c.Career.Expedition.Facts==31,"harbor E never submits story");
            c.Expedition.PortAction();Check(c.Career.Expedition.Facts==63 && c.Career.Coins==before,"independent submission consumes no fish or coins");SaveCheck(c,"delivered");
            c.Expedition.PortAction();Check(c.Career.Coins==before && c.Career.Expedition.Facts==63,"repeat submission has no reward");
            // Corrupt only a unique test copy, not the active test profile or a player's file.
            string backup=Path.Combine(Application.temporaryCachePath,"expedition-backup-"+Guid.NewGuid().ToString("N")+".json");
            FishingSaveStore.Save(backup,c.Career);FishingSaveStore.Save(backup,c.Career);File.WriteAllText(backup,"invalid-json");
            var recovered=FishingSaveStore.Load(backup,out string warning);
            Check(warning!="" && recovered.Expedition.Facts==63 && !recovered.Expedition.Deliver(),"JSON backup recovers completed facts without resubmission");
        }
    }
}
