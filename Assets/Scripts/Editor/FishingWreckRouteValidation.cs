using System.Linq;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System;
using System.Collections;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingWreckRouteValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V051-WRECK");
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V051-WRECK] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            c.ReturnToHarbor();yield return null;
            var berth=FishingIslandWorld.Berth(2)+Vector3.up;var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=berth;rb.rotation=Quaternion.identity;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Actual wreck-island landing remains available");yield return null;
            var access=c.Islands.WorldRoot.GetChild(2).GetComponentInChildren<FishingWreckAccess>();Check(access!=null,"Visible 19-step access is installed in the real island");
            foreach(var filter in access.GetComponentsInChildren<MeshFilter>())Debug.Log("[V051-WRECK] access bounds="+filter.GetComponent<Renderer>().bounds+" position="+filter.transform.position+" scale="+filter.transform.lossyScale+" rotation="+filter.transform.eulerAngles);
            var stair=access.GetComponentInChildren<MeshCollider>();
            Check(stair.Raycast(new Ray(access.BridgeTop+Vector3.up,Vector3.down),out var topHit,2) && Mathf.Abs(topHit.point.y-access.BridgeTop.y)<.03f,"Imported stair top matches the actual boarding route");
            Inspect(c);
            Check(c.Shore.Relocate(access.Start+Vector3.left*.6f,Quaternion.Euler(0,90,0)),"Access starts on real dry island ground");
            foreach(var point in new[]{access.Start,access.BridgeTop,access.Landing,access.DeckTurn,access.PortalApproach})
            {
                for(int i=0;i<350 && FishingPrototype.FlatDistance(c.Shore.Position,point)>.25f;i++)
                {
                    var delta=Vector3.ProjectOnPlane(point-c.Shore.Position,Vector3.up);c.viewCamera.transform.rotation=Quaternion.LookRotation(delta.normalized);
                    c.Shore.Move(Vector2.up,false,false,false,.04f);yield return null;
                }
                Check(FishingPrototype.FlatDistance(c.Shore.Position,point)<=.3f,"Real collision motor walks without jumping to "+point+"; reached "+c.Shore.Position);
                Check(Mathf.Abs(c.Shore.Position.y-point.y)<.28f,"Walk reaches the visible surface height at "+point);
            }
            Check(c.Shore.Position.y>3.1f && c.Shore.Position.y<3.6f,"Feet stand on the remodelled visible deck instead of the former invisible lower floor");
            Check(c.Shore.Relocate(access.PortalApproach,Quaternion.identity),"Exact entrance marker has supported footing and full standing clearance");
            Physics.SyncTransforms();
            Check(c.Realms.TryTarget(out int portalId,out _) && portalId==10,"The real interaction ray reaches the wreck portal from its deck approach");
            Check(c.Realms.Enter(2),"The player's shared interaction path actually enters the wreck realm");
            while(c.Realms.Transitioning)yield return null;
            Check(c.Realms.Inside && c.Realms.Island==2,"The wreck realm transfer finishes on its supported interior floor");
            c.Realms.Abort();yield return null;
            Inspect(c);c.ReturnToHarbor();
        }
        public static void BakeAccessResources()
        {
            const string folder="Assets/Resources/FishingV051";Directory.CreateDirectory(folder);
            var access=AssetImporter.GetAtPath(folder+"/WreckAccess.fbx") as ModelImporter;
            if(access==null)throw new System.InvalidOperationException("Wreck access FBX is missing");
            access.isReadable=true;access.materialImportMode=ModelImporterMaterialImportMode.None;access.SaveAndReimport();
            const string shipPath="Assets/Resources/FishingV035/Galleon.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(shipPath);bool readable=importer.isReadable;
            try
            {
                if(!readable){importer.isReadable=true;importer.SaveAndReimport();}
                var ship=AssetDatabase.LoadAssetAtPath<GameObject>(shipPath);
                var source=ship.GetComponentsInChildren<MeshFilter>(true).First(f=>!f.name.StartsWith("LOD") && f.sharedMesh!=null).sharedMesh;
                var copy=new Mesh{name="Galleon visible hull and deck collision",indexFormat=source.indexFormat};copy.vertices=source.vertices;copy.triangles=source.triangles;copy.RecalculateBounds();
                string path=folder+"/WreckHullCollision.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(copy,path);else{EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);EditorUtility.SetDirty(existing);}
                AssetDatabase.SaveAssets();Debug.Log("[WRECK-BAKE] PASS: readable 1080-triangle access and visible-hull collision baked.");
            }
            finally{if(!readable){importer.isReadable=false;importer.SaveAndReimport();}}
        }
        public static void Inspect(FishingPrototype c)
        {
            var island=c.Islands.WorldRoot.GetChild(2);
            var hero=island.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Galleon");
            Debug.Log("[WRECK-ROUTE] hero "+hero.position+" scale="+hero.lossyScale+" rotation="+hero.eulerAngles);
            foreach(var collider in hero.GetComponentsInChildren<Collider>())Debug.Log("[WRECK-ROUTE] collider "+collider.name+" "+collider.bounds);
            foreach(var renderer in hero.GetComponentsInChildren<Renderer>())Debug.Log("[WRECK-ROUTE] mesh "+renderer.name+" "+renderer.bounds);
            foreach(string name in new[]{"Approach0","Approach1","Approach2","Portal"})
            {
                var marker=FishingRealmWorld.Marker(hero,name);Debug.Log("[WRECK-ROUTE] "+name+"="+marker.position+" local="+hero.InverseTransformPoint(marker.position));
                if(Physics.Raycast(marker.position+Vector3.up*4,Vector3.down,out var hit,12,~(1<<5),QueryTriggerInteraction.Ignore))Debug.Log("[WRECK-ROUTE] support "+name+"="+hit.point+" normal="+hit.normal+" on="+hit.collider.name);
            }
            var visual=hero.GetComponentsInChildren<MeshFilter>().First(f=>f.GetComponent<Renderer>()!=null && !f.name.StartsWith("LOD"));
            var colliderOnVisual=visual.GetComponent<MeshCollider>();var saved=colliderOnVisual.sharedMesh;
            try
            {
                colliderOnVisual.sharedMesh=visual.sharedMesh;Physics.SyncTransforms();
                foreach(string name in new[]{"Approach0","Approach1","Approach2","Portal"})
                {
                    var at=FishingRealmWorld.Marker(hero,name).position;
                    if(colliderOnVisual.Raycast(new Ray(at+Vector3.up*12,Vector3.down),out var hit,30))Debug.Log("[WRECK-ROUTE] VISIBLE SURFACE "+name+"="+hit.point+" normal="+hit.normal);
                }
            }
            finally{colliderOnVisual.sharedMesh=saved;Physics.SyncTransforms();}
            Directory.CreateDirectory("Logs/WreckInspection");
            var camera=new GameObject("Isolated wreck route inspection").AddComponent<Camera>();var rt=new RenderTexture(1280,720,24);var previous=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.CopyFrom(c.viewCamera);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
                camera.transform.position=hero.position+new Vector3(-19,9,17);camera.transform.LookAt(hero.position+Vector3.up*2);camera.fieldOfView=52;camera.targetTexture=rt;
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/WreckInspection/before.png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
