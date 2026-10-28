using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Fixed camera-only views of every island, islet, home and harbour for before/after scenery review,
    /// plus an approximate visible triangle / draw-call count per view (frustum-culled renderers, no batching credit).</summary>
    public static class FishingV044SceneCapture
    {
        public static void Run()=>FishingV011Validation.Begin("V044-SCENE");
        public static IEnumerator Checks(FishingPrototype c)
        {
            string folder=System.Environment.GetEnvironmentVariable("V044_SCENE_OUT");
            if(string.IsNullOrEmpty(folder))folder="Logs/V044/scene/before";
            Directory.CreateDirectory(folder);
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);if(c.Paused)c.TogglePause();
            c.ReturnToHarbor();for(int f=0;f<30;f++)yield return null;
            var stats=new System.Collections.Generic.List<string>{"view,triangles,drawcalls"};
            void Shot(string name,Vector3 eye,Vector3 target)
            {
                var cam=c.viewCamera;var t=cam.transform;var p=t.position;var r=t.rotation;
                try
                {
                    t.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
                    FishingV016Validation.Capture(c,folder,name);
                    var planes=GeometryUtility.CalculateFrustumPlanes(cam);long tris=0;int calls=0;
                    foreach(var renderer in Object.FindObjectsOfType<MeshRenderer>())
                    {
                        if(!renderer.enabled || !renderer.gameObject.activeInHierarchy || (cam.cullingMask&(1<<renderer.gameObject.layer))==0 || !GeometryUtility.TestPlanesAABB(planes,renderer.bounds))continue;
                        if(!renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh==null)continue;var mesh=filter.sharedMesh;
                        // a static-batched renderer points at the combined mesh; only its own submesh range is drawn
                        int first=renderer.isPartOfStaticBatch?renderer.subMeshStartIndex:0,count=renderer.isPartOfStaticBatch?renderer.sharedMaterials.Length:mesh.subMeshCount;
                        for(int s=first;s<first+count && s<mesh.subMeshCount;s++){tris+=mesh.GetIndexCount(s)/3;calls++;}
                    }
                    stats.Add(name+","+tris+","+calls);
                }
                finally{t.SetPositionAndRotation(p,r);}
            }
            for(int i=0;i<4;i++)
            {
                var centre=FishingIslandWorld.Center(i);
                Shot("island"+i+"-far",centre+new Vector3(45,24,75),centre+Vector3.up*3);
                var land=FishingIslandWorld.Landing(i);
                Shot("island"+i+"-landing",land+Vector3.up*1.7f,centre+Vector3.up*2);
                Shot("island"+i+"-shore",centre+new Vector3(26,2.2f,14),centre+new Vector3(10,1,2));
                yield return null;
            }
            for(int i=0;i<FishingCoastalPresentation.Islets.Length;i++)
                Shot("islet"+i+"-far",FishingCoastalPresentation.Islets[i]+new Vector3(35,19,38),FishingCoastalPresentation.Islets[i]);
            var landmarks=new[]{"Windmill","AncientTree","Galleon"};
            for(int i=0;i<landmarks.Length;i++)
            {
                var hero=c.Islands.WorldRoot.GetChild(i).Find(FishingV035Art.ArtRoot).Find(landmarks[i]);
                var b=hero.GetComponentsInChildren<MeshRenderer>().First(r=>r.name!="LOD1").bounds;
                Shot("landmark-"+landmarks[i],b.center+new Vector3(b.extents.x*1.8f,b.extents.y*.65f,-b.extents.z*1.8f-8),b.center);
            }
            Shot("landmark-Lighthouse",c.Home.LighthouseLens+new Vector3(13,-4,-15),c.Home.LighthouseLens-Vector3.up*5);
            var home=new Vector3(-180,2.2f,10);
            Shot("home-far",home+new Vector3(-30,14,-40),home);
            Shot("home-near",home+new Vector3(-8,1.8f,-16),home+Vector3.up*1.5f);
            var harbour=c.Player.transform.position;
            Shot("harbour-far",harbour+new Vector3(0,14,-30),harbour+Vector3.forward*30);
            Shot("harbour-near",harbour+new Vector3(4,3,-6),harbour+Vector3.forward*25);
            File.WriteAllLines(Path.Combine(folder,"stats.csv"),stats);
            if(System.Environment.GetEnvironmentVariable("V044_REBAKE_MAP")=="1")BoatAttack.Editor.FishingMapBaker.BakeInitializedScene();
            Debug.Log("[V044-SCENE] CHECK: captured "+(stats.Count-1)+" views → "+Path.GetFullPath(folder));
        }
    }
}
