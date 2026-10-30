using System;
using System.IO;
using System.Linq;
using System.Text;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingPicoPresentationValidation
    {
        private const string Folder="Assets/Resources/FishingV044/Scene/";
        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException(message);Debug.Log("[PICO-PRESENTATION] CHECK: "+message);}

        public static void RepairAndValidate()
        {
            try
            {
                Check(Application.isBatchMode,"Explicit batch operation");
                ValidateTurning();
                var scene=EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                int placed=0;
                foreach(var r in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MeshRenderer>(true)).ToArray())
                {
                    if(r.name=="LOD1" || r.transform.Find("LOD1")==null)continue;
                    FishingV035Materials.AddLod(r);placed++;
                }
                int clusters=RebuildClusterLods();
                ValidateClusters();
                foreach(var group in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<LODGroup>(true)))group.RecalculateBounds();
                Check(placed>0 && clusters>0,"Existing scenery and both Full/Half cluster sets repaired: "+placed+" / "+clusters);
                ValidateModels();
                FishingPicoQuality.Ensure();
                var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(FishingPicoQuality.AssetPath);
                Check(FishingMobileQuality.RenderScale==1 && pipeline.renderScale==1 && pipeline.msaaSampleCount==4,"PICO retains full render resolution and 4x MSAA");
                AssetDatabase.SaveAssets();
                Check(EditorSceneManager.SaveScene(scene),"Repaired scene saved");
                var importer=AssetImporter.GetAtPath(Folder+"GrassClusters.asset");importer.userData="PicoLodAxesV047";AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                foreach(string name in new[]{"CoastalPine","Lighthouse","Galleon"})
                {
                    CaptureModel(name,false,false);
                    CaptureModel(name,true,true);
                    CaptureModel(name,true,false);
                }
                Debug.Log("[PICO-PRESENTATION] PASS: repaired source LOD placement, baked clusters, full resolution and persisted smooth turning; renders in Logs/V047.");
                EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
        }

        private static void ValidateTurning()
        {
            string[] keys={FishingXRComfort.TurnPreference,"FishingXR.Turn"};
            int?[] saved=keys.Select(k=>PlayerPrefs.HasKey(k)?(int?)PlayerPrefs.GetInt(k):null).ToArray();
            try
            {
                PlayerPrefs.DeleteKey(FishingXRComfort.TurnPreference);PlayerPrefs.SetInt("FishingXR.Turn",0);
                Check(FishingXRComfort.TurnMode==FishingXRComfort.Turn.Smooth,"Existing 30-degree default upgrades to smooth turning");
                FishingXRComfort.TurnMode=FishingXRComfort.Turn.Snap45;
                Check(FishingXRComfort.TurnMode==FishingXRComfort.Turn.Snap45,"An explicit comfort choice still persists");
                PlayerPrefs.DeleteKey(FishingXRComfort.TurnPreference);
                Check(FishingXRComfort.TurnMode==FishingXRComfort.Turn.Smooth,"Fresh settings default to smooth turning");
                foreach(int fps in new[]{30,72,90,120})
                {
                    float yaw=0;for(int i=0;i<fps;i++)yaw+=FishingXRComfort.SmoothTurnDegrees(1,1f/fps);
                    Check(Mathf.Abs(yaw-75)<.001f,"Continuous turning is frame independent at "+fps+" Hz");
                }
                Check(FishingXRComfort.SmoothTurnDegrees(.2f,.01f)==0 && FishingXRComfort.SmoothTurnDegrees(-1,.01f)<0
                    && FishingXRComfort.SmoothTurnDegrees(1,.01f)>0,"Deadzone and left/right direction are correct");
                Check(FishingXRComfort.SmoothTurnDegrees(float.NaN,.01f)==0 && FishingXRComfort.SmoothTurnDegrees(1,float.PositiveInfinity)==0
                    && FishingXRComfort.SmoothTurnDegrees(1,1)<=3.75f,"Invalid input and long stalls cannot cause large camera jumps");
            }
            finally{for(int i=0;i<keys.Length;i++){if(saved[i].HasValue)PlayerPrefs.SetInt(keys[i],saved[i].Value);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();}
        }

        private static int RebuildClusterLods()
        {
            var meshes=AssetDatabase.LoadAllAssetsAtPath(Folder+"GrassClusters.asset").OfType<Mesh>().ToDictionary(m=>m.name);
            if(AssetImporter.GetAtPath(Folder+"GrassClusters.asset").userData=="PicoLodAxesV047")return meshes.Keys.Count(n=>n.EndsWith(" L1"));
            var prepared=new System.Collections.Generic.List<(Mesh low,CombineInstance[] parts)>();
            foreach(var high in meshes.Values.Where(m=>m.name.EndsWith(" L0")))
            {
                string name=high.name.Split(' ')[1];
                var far=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+"_LOD1.fbx").GetComponentInChildren<MeshFilter>();
                var low=meshes[high.name.Substring(0,high.name.Length-1)+"1"];
                var vertices=far.sharedMesh.vertices;var uv=far.sharedMesh.uv;var combined=low.vertices;var combinedUv=low.uv;
                var lookup=new System.Collections.Generic.Dictionary<Vector2Int,Vector3>();var ambiguous=new System.Collections.Generic.HashSet<Vector2Int>();
                for(int i=0;i<vertices.Length;i++)
                {
                    var key=UvKey(uv[i]);
                    if(lookup.TryGetValue(key,out var other) && Vector3.Distance(other,vertices[i])>.0001f)ambiguous.Add(key);
                    else lookup[key]=vertices[i];
                }
                // Import optimization changed the source vertex indices after these clusters were baked; UV0 is stable.
                int stride=meshes.Values.Where(m=>m.name.EndsWith(" L1") && m.name.Split(' ')[1]==name).Select(m=>m.vertexCount).Aggregate(Gcd);
                if(stride<vertices.Length/2 || combined.Length%stride!=0)throw new InvalidOperationException("Unexpected combined plant stride: "+high.name);
                var parts=new CombineInstance[combined.Length/stride];
                for(int n=0;n<parts.Length;n++)
                {
                    int offset=n*stride;var from=new System.Collections.Generic.List<Vector3>();var to=new System.Collections.Generic.List<Vector3>();
                    for(int i=0;i<stride;i++)
                    {
                        var key=UvKey(combinedUv[offset+i]);
                        if(!ambiguous.Contains(key) && lookup.TryGetValue(key,out var position)){from.Add(position);to.Add(combined[offset+i]);}
                    }
                    if(from.Count<100)throw new InvalidOperationException("Insufficient stable UV correspondences: "+high.name);
                    var basis=Basis(from.ToArray(),out int a,out int b,out int c,out int d).inverse;
                    var fit=Frame(to[a],to[b],to[c],to[d])*basis;
                    int matches=0;for(int v=0;v<from.Count;v++)if(Vector3.Distance(fit.MultiplyPoint3x4(from[v]),to[v])<=.002f)matches++;
                    if(matches<from.Count*.95f)throw new InvalidOperationException("Plant transform correspondence below 95%: "+high.name+" instance "+n+" matches="+matches+"/"+from.Count);
                    parts[n]=new CombineInstance{mesh=far.sharedMesh,transform=fit*Matrix4x4.TRS(far.transform.position,far.transform.rotation,far.transform.lossyScale)};
                }
                prepared.Add((low,parts));
            }
            // Reuse each mesh asset/fileID so the scene, its Full/Half sets and static batching keep their references.
            if(!File.Exists("Logs/V047/GrassClusters-before.asset"))File.Copy(Folder+"GrassClusters.asset","Logs/V047/GrassClusters-before.asset");
            foreach(var entry in prepared)
            {entry.low.Clear();entry.low.indexFormat=IndexFormat.UInt32;entry.low.CombineMeshes(entry.parts,true,true);entry.low.RecalculateBounds();EditorUtility.SetDirty(entry.low);}
            return prepared.Count;
        }

        private static Vector2Int UvKey(Vector2 uv)=>new Vector2Int(Mathf.RoundToInt(uv.x*100000),Mathf.RoundToInt(uv.y*100000));
        private static int Gcd(int a,int b){while(b!=0){int r=a%b;a=b;b=r;}return a;}

        private static void ValidateClusters()
        {
            var meshes=AssetDatabase.LoadAllAssetsAtPath(Folder+"GrassClusters.asset").OfType<Mesh>().ToDictionary(m=>m.name);int count=0;
            foreach(var high in meshes.Values.Where(m=>m.name.EndsWith(" L0")))
            {
                var low=meshes[high.name.Substring(0,high.name.Length-1)+"1"];
                Check(Vector3.Distance(high.bounds.center,low.bounds.center)<.6f && (high.bounds.size-low.bounds.size).magnitude<1
                    && low.triangles.Length<high.triangles.Length*.4f,"Baked near/far plant bounds align and retain reduction: "+high.name+" near="+high.bounds+" far="+low.bounds+" triangles="+high.triangles.Length/3+"/"+low.triangles.Length/3);count++;
            }
            Check(count==314,"All Full/Half ground-cover LOD pairs validated");
        }

        private static Matrix4x4 Frame(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            var m=Matrix4x4.identity;m.SetColumn(0,new Vector4(b.x-a.x,b.y-a.y,b.z-a.z,0));
            m.SetColumn(1,new Vector4(c.x-a.x,c.y-a.y,c.z-a.z,0));m.SetColumn(2,new Vector4(d.x-a.x,d.y-a.y,d.z-a.z,0));
            m.SetColumn(3,new Vector4(a.x,a.y,a.z,1));return m;
        }
        private static Matrix4x4 Basis(Vector3[] v,out int a,out int b,out int c,out int d)
        {
            a=b=c=d=0;float best=0;
            for(int i=1;i<v.Length;i++){float score=(v[i]-v[a]).sqrMagnitude;if(score>best){best=score;b=i;}}
            best=0;for(int i=1;i<v.Length;i++){float score=Vector3.Cross(v[b]-v[a],v[i]-v[a]).sqrMagnitude;if(score>best){best=score;c=i;}}
            var normal=Vector3.Cross(v[b]-v[a],v[c]-v[a]);best=0;
            for(int i=1;i<v.Length;i++){float score=Mathf.Abs(Vector3.Dot(v[i]-v[a],normal));if(score>best){best=score;d=i;}}
            if(best<.000001f)throw new InvalidOperationException("Plant mesh has no usable affine basis");
            return Frame(v[a],v[b],v[c],v[d]);
        }

        private static void ValidateModels()
        {
            int count=0;
            foreach(string path in Directory.GetFiles(Folder,"*_LOD1.fbx"))
            {
                string name=Path.GetFileNameWithoutExtension(path).Replace("_LOD1","");
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/FishingV035/"+name+".fbx");if(source==null)continue;
                var instance=Object.Instantiate(source);
                try
                {
                    FishingV035Materials.Apply(instance.transform);
                    var near=instance.GetComponentInChildren<MeshFilter>();var far=near.transform.Find("LOD1").GetComponent<MeshFilter>();
                    var a=near.GetComponent<Renderer>().bounds;var b=far.GetComponent<Renderer>().bounds;
                    float tolerance=Mathf.Max(.3f,a.size.magnitude*.08f);
                    Check(Vector3.Distance(a.center,b.center)<tolerance && (a.size-b.size).magnitude<tolerance*2,"LOD origin/orientation/extent match for "+name);
                    Check(far.sharedMesh.triangles.Length<near.sharedMesh.triangles.Length*.35f,"Real reduced geometry retained for "+name);
                    var pose=far.transform.localRotation;FishingV035Materials.AddLod(near.GetComponent<MeshRenderer>());
                    Check(Quaternion.Angle(pose,far.transform.localRotation)<.001f && near.GetComponent<LODGroup>().lodCount==2,"Repeated setup preserves two aligned LODs for "+name);
                    count++;
                }
                finally{Object.DestroyImmediate(instance);}
            }
            Check(count==13,"All 13 imported far models validated");
        }

        private static void CaptureModel(string name,bool low,bool legacy)
        {
            var scene=EditorSceneManager.NewPreviewScene();RenderTexture previous=RenderTexture.active;
            var target=new RenderTexture(640,640,24);var photo=new Texture2D(640,640,TextureFormat.RGB24,false);
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/FishingV035/"+name+".fbx");
                var model=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);FishingV035Materials.Apply(model.transform);
                model.transform.position=Vector3.up*10000;
                foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                var renderer=model.GetComponentInChildren<MeshRenderer>();var bounds=renderer.bounds;
                var far=renderer.transform.Find("LOD1");
                if(legacy){far.localPosition=Vector3.zero;far.localRotation=Quaternion.identity;far.localScale=Vector3.one;}
                renderer.GetComponent<LODGroup>().enabled=false;renderer.enabled=!low;far.GetComponent<Renderer>().enabled=low;
                var cameraGo=new GameObject("LOD comparison camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);
                var camera=cameraGo.GetComponent<Camera>();camera.scene=scene;camera.cameraType=CameraType.Preview;camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.1f,.17f,.23f);camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=bounds.size.magnitude*.58f;
                camera.farClipPlane=200;camera.targetTexture=target;camera.stereoTargetEye=StereoTargetEyeMask.None;
                camera.transform.position=bounds.center+new Vector3(.5f,.2f,-1).normalized*bounds.size.magnitude*2;
                camera.transform.LookAt(bounds.center);
                var lightGo=new GameObject("LOD comparison light",typeof(Light));SceneManager.MoveGameObjectToScene(lightGo,scene);
                var light=lightGo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,-35,0);
                camera.Render();camera.Render();RenderTexture.active=target;photo.ReadPixels(new Rect(0,0,640,640),0,0);photo.Apply();
                File.WriteAllBytes("Logs/V047/"+name+(low?legacy?"-old-lod1":"-fixed-lod1":"-lod0")+".png",photo.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(photo);EditorSceneManager.ClosePreviewScene(scene);}
        }

        public static void CaptureGallery()
        {
            try
            {
                Directory.CreateDirectory("Logs/V047");
                foreach(string name in new[]{"CoastalPine","Lighthouse","Galleon"})
                {CaptureModel(name,false,false);CaptureModel(name,true,true);CaptureModel(name,true,false);}
                EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
        }

        public static void Audit()
        {
            try
            {
                var report = new StringBuilder("name,near_mesh_size,near_mesh_center,near_node_rotation,near_node_scale,far_mesh_size,far_mesh_center,far_node_rotation,far_node_scale\n");
                foreach (string path in Directory.GetFiles("Assets/Resources/FishingV044/Scene", "*_LOD1.fbx"))
                {
                    string name = Path.GetFileNameWithoutExtension(path).Replace("_LOD1", "");
                    var near = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/FishingV035/" + name + ".fbx");
                    var far = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                    if (near == null || far == null) continue;
                    var a = near.GetComponentInChildren<MeshFilter>();
                    var b = far.GetComponentInChildren<MeshFilter>();
                    report.AppendLine(name + "," + a.sharedMesh.bounds.size + "," + a.sharedMesh.bounds.center + "," + a.transform.localEulerAngles + "," + a.transform.localScale
                        + "," + b.sharedMesh.bounds.size + "," + b.sharedMesh.bounds.center + "," + b.transform.localEulerAngles + "," + b.transform.localScale);
                }
                Directory.CreateDirectory("Logs/V047");
                File.WriteAllText("Logs/V047/lod-audit.txt", report.ToString());
                Debug.Log("[PICO-PRESENTATION] AUDIT: " + report);
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
    }
}
