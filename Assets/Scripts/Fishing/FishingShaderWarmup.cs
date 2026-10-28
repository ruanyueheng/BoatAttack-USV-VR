using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// Draws every material in the built world once into a tiny off-screen target during start-up, so the driver
    /// compiles those shader variants behind the load instead of stalling the first visit to an island/realm/event.
    /// </summary>
    public static class FishingShaderWarmup
    {
        public static int Run(UnityEngine.SceneManagement.Scene scene)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var materials=new HashSet<Material>();
            foreach(var root in scene.GetRootGameObjects())
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)if(material!=null)materials.Add(material);
            var go=new GameObject("Shader warm-up camera"){hideFlags=HideFlags.HideAndDontSave};
            var target=RenderTexture.GetTemporary(64,64,24);
            try
            {
                var camera=go.AddComponent<Camera>();camera.enabled=false;camera.targetTexture=target;camera.cullingMask=1<<31;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.1f;camera.farClipPlane=10;
                go.transform.position=new Vector3(0,-5000,0);
                var quad=Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var matrix=Matrix4x4.TRS(go.transform.position+Vector3.forward*2,Quaternion.identity,Vector3.one);
                foreach(var material in materials)
                    Graphics.DrawMesh(quad,matrix,material,31,camera,0,null,ShadowCastingMode.Off,false);
                camera.Render();
            }
            finally{RenderTexture.ReleaseTemporary(target);Object.Destroy(go);}
            Debug.Log("[SHADER-WARMUP] "+materials.Count+" materials in "+watch.ElapsedMilliseconds+" ms");
            return materials.Count;
        }
    }
}
