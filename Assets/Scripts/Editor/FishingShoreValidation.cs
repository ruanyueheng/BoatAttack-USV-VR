using System;
using System.Collections;
using BoatAttack.Fishing;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    [InitializeOnLoad]
    public static class FishingShoreValidation
    {
        private const string Key="Fishing.ShoreSmoke.";
        private static IEnumerator _checks;
        private static float _next;
        static FishingShoreValidation()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"active",false))return;
                SessionState.SetBool(Key+"active",false);
                var error=SessionState.GetString(Key+"error","");
                if(!SessionState.GetBool(Key+"finished",false) && error=="")error="Stopped before completing checks";
                if(error=="")Debug.Log("[SHORE] PASS: four landing routes, embodiment, task rewards, boarding and rescue.");
                else Debug.LogError("[SHORE] FAIL: "+error);
                EditorApplication.Exit(error==""?0:1);
            };
            Application.logMessageReceived+=(message,stack,type)=>
            {
                if(SessionState.GetBool(Key+"active",false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))SessionState.SetString(Key+"error",message);
            };
        }
        public static void Capture()
        {
            SessionState.SetBool(Key+"capture",true);Run();
        }
        private static void CaptureScene(FishingPrototype c,string name)
        {
            if(!SessionState.GetBool(Key+"capture",false))return;
            c.Shore.SyncRider();c.View.ApplyView();
            var camera=c.viewCamera;var before=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
            Texture2D image=null;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                Directory.CreateDirectory("Logs/shore-preview");File.WriteAllBytes("Logs/shore-preview/"+name+".png",image.EncodeToPNG());
            }
            finally{camera.targetTexture=before;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
        }
        public static void Run()
        {
            SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"finished",false);SessionState.SetString(Key+"error","");
            SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+240);
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))SessionState.SetString(Key+"error","Timeout");
            if(SessionState.GetString(Key+"error","")!=""){if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();return;}
            if(!EditorApplication.isPlaying || Time.time<_next)return;
            var c=UnityEngine.Object.FindObjectOfType<FishingPrototype>();if(c==null || c.Shore==null || c.Career==null)return;
            try
            {
                if(_checks==null)_checks=Checks(c);
                _next=Time.time+.02f;
                if(!_checks.MoveNext()){SessionState.SetBool(Key+"finished",true);EditorApplication.ExitPlaymode();}
            }
            catch(Exception ex){SessionState.SetString(Key+"error",ex.ToString());EditorApplication.ExitPlaymode();}
        }
        private static void Check(bool success,string label)
        {
            if(!success)throw new Exception(label);
            Debug.Log("[SHORE] CHECK: "+label);
        }
        private static IEnumerator WalkCorner(FishingPrototype c,Vector3 target)
        {
            int guard=0;
            while(FishingPrototype.FlatDistance(c.Shore.Actor.position,target)>.2f && guard++<400)
            {
                c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(target-c.Shore.Actor.position,Vector3.up));
                c.Shore.Walk(Vector2.up,false,.02f);yield return null;
            }
            Check(guard<400,"walk around old harbor building: "+target+" actual="+c.Shore.Actor.position);
        }
        private static IEnumerator Checks(FishingPrototype c)
        {
            yield return null;
            var shore=c.Shore;
            Check(shore.Body!=null && !shore.Body.enabled && !c.Ashore,"one seated character; boat owns collision while riding");
            for(int i=0;i<shore.Sites.Length;i++)
            {
                var site=shore.Sites[i];Check(site.Available,"landing geometry available "+i);
                c.ReturnToHarbor();
                var rb=c.Player.engine.RB;
                var side=Vector3.ProjectOnPlane(site.Berth-site.Landing,Vector3.up).normalized;
                // 旧港口外侧 5m 的旧夹具位于 Rock_Large_01 内，不能把穿岩传送当成功能。
                var position=site.Berth+side*(i==2?0:5)+Vector3.up;
                c.Player.transform.position=position;rb.position=position;rb.velocity=Vector3.zero;Physics.SyncTransforms();
                rb.velocity=Vector3.right*2;
                Check(!shore.TryDisembark() && !c.Ashore,"reject moving disembark "+i);rb.velocity=Vector3.zero;
                c.TogglePause();Check(!shore.TryDisembark(),"reject paused disembark "+i);c.TogglePause();
                bool landed=shore.TryDisembark();
                if(!landed)
                {
                    bool ground=shore.Ground(site.Landing,out var feet);
                    Debug.Log("[SHORE] LANDING ground="+ground+" feet="+feet+" clear="+shore.Clearance(feet)+" support="+shore.SafeCoastalFooting(feet));
                    var to=feet+Vector3.up*1.1f;var from=new Vector3(position.x,Mathf.Max(position.y+1.4f,to.y),position.z);
                    foreach(var hit in Physics.SphereCastAll(from,.22f,(to-from).normalized,Vector3.Distance(from,to),~(1<<4),QueryTriggerInteraction.Ignore))Debug.Log("[SHORE] TRANSFER HIT "+hit.collider.name+" at "+hit.point);
                }
                Check(landed,"safe disembark "+i+" "+c.Message);
                Check(c.Ashore && shore.Body.enabled && !c.AtHarbor && !c.Cast(),"walking cannot trade or fish "+i);
                CaptureScene(c,"shore-"+i+"-first-person");
                Vector3 moored=rb.position;c.LeaveStation();Check(c.Ashore,"Q cannot start an unmanned boat");
                for(int t=0;t<2;t++)
                {
                    if(i==2)
                    {
                        // The old harbor has a house: follow the real walkway, not a line through its walls.
                        var corner=WalkCorner(c,new Vector3(92.8f,0,-65));while(corner.MoveNext())yield return null;
                    }
                    int guard=0;var target=site.Tasks[t];
                    while(FishingPrototype.FlatDistance(shore.Actor.position,target)>1.8f && guard++<400)
                    {
                        var direction=Vector3.ProjectOnPlane(target-shore.Actor.position,Vector3.up);
                        c.viewCamera.transform.rotation=Quaternion.LookRotation(direction);
                        shore.Walk(Vector2.up,false,.02f);yield return null;
                    }
                    Check(guard<400,"walk to original-terrain task "+(i*2+t)+" pos="+shore.Actor.position+" target="+target);
                    Check(shore.Survey(),"interact at task "+(i*2+t));
                    Check(!c.Career.CompleteShoreTask(i*2+t),"task cannot repeat "+(i*2+t));
                }
                if(Vector3.Distance(shore.Actor.position,site.Landing)>4f)Check(!shore.TryBoard(),"remote boarding rejected "+i);
                if(i==0)
                {
                    var origin=shore.Actor.position;
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Validation obstacle";
                    wall.transform.position=origin+new Vector3(.8f,1.5f,0);wall.transform.localScale=new Vector3(.2f,3,3);Physics.SyncTransforms();
                    shore.Body.Move(Vector3.right*2);
                    Check(shore.Actor.position.x<origin.x+.55f,"physical capsule cannot pass a solid wall");
                    UnityEngine.Object.DestroyImmediate(wall);shore.Body.enabled=false;shore.Actor.position=origin;shore.Body.enabled=true;
                    Check(!shore.Ground(new Vector3(42,0,104),out _),"open sea is not walkable ground");
                }
                Check(FishingPrototype.FlatDistance(rb.position,moored)<.5f,"boat stays moored while exploring "+i);
                c.ToggleView();shore.SyncRider();Check(shore.Actor.GetComponentInChildren<Renderer>().shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On,"third person physical model "+i);
                CaptureScene(c,"shore-"+i+"-third-person");
                c.ToggleView();shore.SyncRider();Check(shore.Actor.GetComponentInChildren<Renderer>().shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly,"first person hides mesh, retains shadow "+i);
                var previous=shore.Actor.position;c.TogglePause();shore.Walk(Vector2.up,true,.05f);Check(shore.Actor.position==previous,"pause prevents walking");c.TogglePause();
                if(i==2){var corner=WalkCorner(c,new Vector3(92.8f,0,-65));while(corner.MoveNext())yield return null;}
                int back=0;
                while(FishingPrototype.FlatDistance(shore.Actor.position,site.Landing)>2f && back++<400)
                {
                    c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(site.Landing-shore.Actor.position,Vector3.up));
                    shore.Walk(Vector2.up,false,.02f);yield return null;
                }
                Check(back<400 && shore.TryBoard(),"return route and boarding "+i);
                Check(!c.Ashore && !shore.Body.enabled,"boarding restores vessel controls "+i);
            }
            c.ReturnToHarbor();Check(c.ClaimShoreRewards()==160 && c.ClaimShoreRewards()==0,"return to harbor pays complete expedition once");
            var loaded=FishingSaveStore.Load(c.SavePath,out var warning);
            Check(warning=="" && loaded.ShoreTaskMask==255 && loaded.ShoreRewardMask==15,"persist full exploration");
            var home=shore.Sites[0];var rbHome=c.Player.engine.RB;
            var safe=home.Berth+new Vector3(0,1,-5);rbHome.position=safe;c.Player.transform.position=safe;rbHome.velocity=Vector3.zero;Physics.SyncTransforms();
            Check(shore.TryDisembark(),"rescue setup");c.ReturnToHarbor();Check(!c.Ashore && c.AtHarbor && c.Career.ShoreTaskMask==255,"ashore rescue restores boat and preserves progress");
        }
        public static void InspectHarbor()
        {
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);Physics.SyncTransforms();
            var text=new StringBuilder("x,z,y,clear\n");
            for(int z=-60;z>=-78;z--)
                for(int x=84;x<=97;x++)
                    if(Physics.Raycast(new Vector3(x,3.5f,z),Vector3.down,out var hit,3.1f,~(1<<4),QueryTriggerInteraction.Ignore))
                    {
                        bool clear=!Physics.CheckCapsule(hit.point+Vector3.up*.4f,hit.point+Vector3.up*1.45f,.28f,~(1<<4),QueryTriggerInteraction.Ignore);
                        text.AppendLine($"{x},{z},{hit.point.y:F2},{clear}");
                    }
            File.WriteAllText("Logs/harbor-walk-grid.csv",text.ToString());EditorApplication.Exit(0);
        }
        public static void Survey()
        {
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
            Physics.SyncTransforms();
            var text = new StringBuilder("x,z,y,normal,collider\n");
            for (int x = -350; x <= 350; x += 4)
                for (int z = -350; z <= 350; z += 4)
                    if (Physics.Raycast(new Vector3(x, 180, z), Vector3.down, out var hit, 180, ~(1 << 4), QueryTriggerInteraction.Ignore))
                        text.AppendLine($"{x},{z},{hit.point.y:F2},{hit.normal.y:F2},{hit.collider.name.Replace(',', '_')}");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/shore-survey.csv", text.ToString());
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>())
                if (c.bounds.size.x > 30 || c.bounds.size.z > 30) Debug.Log($"[SHORE-SURVEY] {c.name} {c.GetType().Name} bounds={c.bounds}");
            Debug.Log("[SHORE-SURVEY] PASS");
            EditorApplication.Exit(0);
        }
    }
}
