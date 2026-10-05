using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BoatAttack.EditorTools
{
    /// <summary>Unsaved studio scene for inspecting the actual prefab exterior/interior.</summary>
    [InitializeOnLoad]
    public static class BeachShopPreview
    {
        private const string SceneKey="BeachShopScenePreview.";
        static BeachShopPreview(){EditorApplication.update+=SceneTick;}
        public static void RenderInScene()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Batch-only preview entry.");
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
            SessionState.SetBool(SceneKey+"active",true);
            SessionState.SetFloat(SceneKey+"deadline",(float)EditorApplication.timeSinceStartup+120);
            SessionState.SetFloat(SceneKey+"ready",0);
            EditorApplication.EnterPlaymode();
        }
        private static void SceneTick()
        {
            if(!SessionState.GetBool(SceneKey+"active",false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(SceneKey+"deadline",0))throw new Exception("Shop scene preview timed out.");
                if(!EditorApplication.isPlaying)return;
                var c=UnityEngine.Object.FindObjectOfType<BoatAttack.Fishing.FishingPrototype>();
                if(c==null || c.TackleShop==null || c.Player==null)return;
                float ready=SessionState.GetFloat(SceneKey+"ready",0);
                if(ready==0){SessionState.SetFloat(SceneKey+"ready",(float)EditorApplication.timeSinceStartup+2);return;}
                if(EditorApplication.timeSinceStartup<ready)return;
                ShaderUtil.allowAsyncCompilation=false;Directory.CreateDirectory("Logs/BeachShop");
                var shop=c.TackleShop.transform;
                Shot(c.viewCamera,shop.TransformPoint(new Vector3(18,10,-23)),shop.position+Vector3.up*1.5f,52,"in-scene.png");
                if(c.TackleShop.Cashier!=null)
                {
                    var face=c.TackleShop.Cashier.transform.position+Vector3.up*1.48f;
                    Shot(c.viewCamera,shop.TransformPoint(new Vector3(0,1.68f,1.15f)),face,48,"cashier-in-scene.png");
                    Shot(c.viewCamera,shop.TransformPoint(new Vector3(-1.4f,1.68f,1.5f)),face,32,"cashier-closeup.png");
                }
                SessionState.SetBool(SceneKey+"active",false);
                Debug.Log("[BEACH-SHOP-PREVIEW] PASS: actual island Play Mode shop render written to Logs/BeachShop/in-scene.png.");
                EditorApplication.Exit(0);
            }
            catch(Exception ex){SessionState.SetBool(SceneKey+"active",false);Debug.LogException(ex);EditorApplication.Exit(1);}
        }
        public static void Render()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(BeachTackleShopBuilder.PrefabPath);
                if(prefab==null)throw new Exception("Shop prefab missing.");
                PrefabUtility.InstantiatePrefab(prefab);
                RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.49f,.52f);
                var sun=new GameObject("Preview sunlight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.0f;sun.color=new Color(1,.94f,.85f);sun.transform.rotation=Quaternion.Euler(45,-35,0);sun.shadows=LightShadows.Soft;
                var fill=new GameObject("Preview sky fill",typeof(Light)).GetComponent<Light>();fill.type=LightType.Directional;fill.intensity=.35f;fill.color=new Color(.66f,.82f,1f);fill.transform.rotation=Quaternion.Euler(30,145,0);
                var ground=new GameObject("Preview sloping sand",typeof(MeshFilter),typeof(MeshRenderer));
                var sand=new Mesh();sand.vertices=new[]{new Vector3(-40,-.3f,40),new Vector3(40,-.3f,40),new Vector3(-40,-.3f,-6),new Vector3(40,-.3f,-6),new Vector3(-40,-3.2f,-20),new Vector3(40,-3.2f,-20),new Vector3(-40,-3.2f,-50),new Vector3(40,-3.2f,-50)};
                sand.triangles=new[]{0,1,2,1,3,2,2,3,4,3,5,4,4,5,6,5,7,6};sand.RecalculateNormals();ground.GetComponent<MeshFilter>().sharedMesh=sand;
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.67f,.58f,.41f));ground.GetComponent<Renderer>().sharedMaterial=mat;
                var camera=new GameObject("Preview camera",typeof(Camera)).GetComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=150;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.25f,.30f);camera.allowHDR=false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                ShaderUtil.allowAsyncCompilation=false;
                Directory.CreateDirectory("Logs/BeachShop");
                Shot(camera,new Vector3(17,10,-22),new Vector3(0,1.5f,-1),48,"exterior.png");
                Shot(camera,new Vector3(0,1.68f,-4.55f),new Vector3(0,1.5f,2.7f),78,"interior.png");
                Shot(camera,new Vector3(-2,1.6f,.5f),new Vector3(0,1.5f,3),72,"counter.png");
                Debug.Log("[BEACH-SHOP-PREVIEW] PASS: three actual prefab renders written to Logs/BeachShop. Preview scene not saved.");
                EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
        }
        private static void Shot(Camera camera,Vector3 eye,Vector3 target,float fov,string name)
        {
            camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=fov;
            var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Logs/BeachShop/"+name,image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
