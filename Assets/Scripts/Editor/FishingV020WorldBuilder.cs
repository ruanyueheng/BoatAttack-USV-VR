using System;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV020WorldBuilder
    {
        private const string Folder = "Assets/Resources/FishingV020";
        public const string ArtRoot = "V020 coastal art";
        public static void Install()
        {
            try
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before installing v20.");
                AssetDatabase.Refresh();
                var palette = AssetDatabase.LoadAssetAtPath<Material>(Folder+"/CoastalPalette.mat");
                if(palette==null)
                {
                    palette=new Material(Resources.Load<Material>("FishingV019/MAT-V019-vertex-palette")){name="Coastal vertex palette"};
                    AssetDatabase.CreateAsset(palette,Folder+"/CoastalPalette.mat");
                }
                palette.SetFloat("_Shadows",1);EditorUtility.SetDirty(palette);
                if (palette == null || Directory.GetFiles(Folder,"*.fbx").Length != 14) throw new InvalidOperationException("Build the fourteen v20 Blender assets first.");
                foreach (string path in Directory.GetFiles(Folder,"*.fbx"))
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.importAnimation=false; importer.isReadable=true; importer.globalScale=1;
                    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"MAT-V020-island-palette"),palette);
                    importer.SaveAndReimport();
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    foreach(var filter in asset.GetComponentsInChildren<MeshFilter>())
                        if(filter.sharedMesh.colors32.Length!=filter.sharedMesh.vertexCount) throw new InvalidOperationException("Vertex palette lost: "+path);
                }
                var scene=EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                Transform world=null;
                foreach(var go in scene.GetRootGameObjects()) if(go.name==FishingIslandWorld.WorldName) world=go.transform;
                if(world==null || world.childCount!=4) throw new InvalidOperationException("Install the v19 islands first.");
                for(int id=0;id<4;id++) ReplaceIsland(world.GetChild(id),id,palette);
                PlayerSettings.bundleVersion=FishingPrototype.Version;
                AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save v20 coastal scene.");
                Debug.Log("[V020-WORLD] PASS: four sculpted shores, original coastal landmarks, foliage, shipwreck and gates installed.");
                if(Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        private static float Ground(Transform island,Vector3 p)
        {
            var collider=island.Find("Walkable beach and interior").GetComponent<MeshCollider>();
            if(!collider.Raycast(new Ray(island.TransformPoint(p+Vector3.up*30),Vector3.down),out var hit,60))
                throw new InvalidOperationException("Missing island footing: "+island.name+" "+p);
            return island.InverseTransformPoint(hit.point).y;
        }
        private static Transform Place(string asset,Transform root,Vector3 p,float span,float yaw=0,bool solid=false)
        {
            p.y=Ground(root.parent,p);
            var t=FishingV019Art.Spawn(asset,root,p,span,yaw,"FishingV020");
            foreach(var child in t.GetComponentsInChildren<Transform>()) child.gameObject.isStatic=true;
            if(solid) foreach(var filter in t.GetComponentsInChildren<MeshFilter>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            return t;
        }
        private static void ReplaceIsland(Transform island,int id,Material palette)
        {
            island.position=FishingIslandWorld.Center(id);
            var previous=island.Find(ArtRoot);if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var ground=island.Find("Walkable beach and interior");
            var temporary=Object.Instantiate(Resources.Load<GameObject>("FishingV020/IslandTerrain"+id));
            // FBX stores its axis conversion on the root; preserve that rotation when baking collision.
            temporary.transform.position=Vector3.zero;
            var filters=temporary.GetComponentsInChildren<MeshFilter>();var combine=new CombineInstance[filters.Length];
            for(int j=0;j<filters.Length;j++)combine[j]=new CombineInstance{mesh=filters[j].sharedMesh,transform=filters[j].transform.localToWorldMatrix};
            var baked=new Mesh{name="V020 physical coastline "+id};baked.CombineMeshes(combine,true,true);baked.RecalculateBounds();
            string meshPath=Folder+"/Terrain"+id+".asset";
            var persistent=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(persistent==null){persistent=baked;AssetDatabase.CreateAsset(persistent,meshPath);}
            else
            {
                // Mesh setters invalidate PhysX's cached geometry when reinstalling the same asset GUID.
                persistent.Clear();persistent.vertices=baked.vertices;persistent.normals=baked.normals;
                persistent.colors32=baked.colors32;persistent.uv=baked.uv;persistent.triangles=baked.triangles;
                persistent.RecalculateBounds();Object.DestroyImmediate(baked);EditorUtility.SetDirty(persistent);
            }
            persistent.name="V020 physical coastline "+id;EditorUtility.SetDirty(persistent);
            Object.DestroyImmediate(temporary);
            ground.GetComponent<MeshFilter>().sharedMesh=persistent;
            ground.GetComponent<MeshRenderer>().sharedMaterial=palette;
            ground.GetComponent<MeshCollider>().sharedMesh=null;
            ground.GetComponent<MeshCollider>().sharedMesh=persistent;
            ground.gameObject.isStatic=true;
            island.Find("Distinct natural landmark").gameObject.SetActive(false);
            foreach(Transform child in island) if(child.name.StartsWith("Coastal palm"))child.gameObject.SetActive(false);
            Physics.SyncTransforms();
            foreach(string name in new[]{"Voyage letter","Hidden treasure","Compass beacon"})
            {var t=island.Find(name);var p=t.localPosition;p.y=Ground(island,p);t.localPosition=p;}
            var old=island.Find("V019 island character");
            if(old==null)throw new InvalidOperationException("V19 portals and climbing routes required.");
            for(int j=old.childCount-1;j>=0;j--)
            {
                var child=old.GetChild(j);
                if(child.name=="Portal" || child.GetComponent<FishingClimbRoute>()!=null || child.name=="Lookout support"
                    || child.name=="风铃瞭望台" || child.name=="归潮号打捞台" || child.name.StartsWith("Sign "))continue;
                Object.DestroyImmediate(child.gameObject);
            }
            var root=new GameObject(ArtRoot).transform;root.SetParent(island,false);
            var jetty=island.Find("Landing jetty");jetty.GetComponent<MeshRenderer>().enabled=false;
            var dock=Object.Instantiate(Resources.Load<GameObject>("FishingV020/LandingJetty"),root,false);
            dock.name="Planked landing jetty";dock.transform.localPosition=jetty.localPosition;
            var portal=old.Find("Portal");var portalPoint=portal.localPosition;portalPoint.y=Ground(island,portalPoint);portal.localPosition=portalPoint;
            var gate=portal.Find("TideGate");if(gate!=null)Object.DestroyImmediate(gate.gameObject);
            foreach(var renderer in portal.GetComponentsInChildren<MeshRenderer>()) if(renderer.GetComponent<TextMesh>()==null)renderer.enabled=false;
            foreach(var sign in portal.GetComponentsInChildren<TextMesh>())sign.transform.localPosition=new Vector3(0,4.85f,-.45f);
            FishingV019Art.Spawn("TideGate",portal,Vector3.zero,4.6f,0,"FishingV020");
            foreach(var route in old.GetComponentsInChildren<FishingClimbRoute>())
            {
                float delta=Ground(island,route.bottom)-route.Bottom.y+island.position.y;
                route.transform.localPosition+=Vector3.up*delta;
                var deck=old.Find(id==0?"风铃瞭望台":"归潮号打捞台");deck.localPosition+=Vector3.up*delta;
                deck.GetComponent<MeshRenderer>().enabled=false;
                var model=FishingV019Art.Spawn("LookoutDeck",root,old.InverseTransformPoint(route.Top)+Vector3.back,4.6f,0,"FishingV020");
                model.name="Crafted climb lookout";
            }
            if(id==0)
            {
                Place("WindArch",root,new Vector3(8,0,-7),14,0,true);
                Place("CoastalRock",root,new Vector3(18,0,-18),11,35,true);
                Place("CoastalRock",root,new Vector3(-18,0,-12),9,-40,true);
                Place("CoastalPine",root,new Vector3(-20,0,9),8,18);
                Place("CoastalPine",root,new Vector3(16,0,9),7,120);
            }
            else if(id==1)
            {
                for(int j=0;j<13;j++)
                {
                    float a=j*137.5f*Mathf.Deg2Rad;var p=new Vector3(Mathf.Cos(a)*20,0,Mathf.Sin(a)*20);
                    if(p.z>12 && p.x>-10 && p.x<12)continue;
                    Place(j%3==0?"CoastalPine":"Mangrove",root,p,8+j%3,j*42);
                }
                Place("Mangrove",root,new Vector3(10,0,-10),12,-15);
                FishingV019Art.Spawn("tent_detailedOpen",root,new Vector3(-16,Ground(island,new Vector3(-16,0,6)),6),5,25);
                FishingV019Art.Spawn("campfire_logs",root,new Vector3(-12,Ground(island,new Vector3(-12,0,10)),10),1.3f);
            }
            else if(id==2)
            {
                Place("Shipwreck",root,new Vector3(-15,0,-11),21,38,true);
                Place("Shipwreck",root,new Vector3(12,0,-9),8,90,true);
                Place("CoastalRock",root,new Vector3(23,0,-13),6,80,true);
                for(int j=0;j<5;j++)FishingV019Art.Spawn(j%2==0?"barrel":"crate-bottles",root,new Vector3(-18+j*1.6f,Ground(island,new Vector3(-18+j*1.6f,0,13)),13),1.2f,j*15);
            }
            else
            {
                Place("Observatory",root,new Vector3(11,0,-7),13,205,true);
                for(int j=0;j<5;j++)
                {
                    float a=(j*32+10)*Mathf.Deg2Rad;
                    Place("CoastalRock",root,new Vector3(Mathf.Cos(a)*18,0,-Mathf.Sin(a)*18),5+j%3,j*31,true);
                }
            }
            for(int j=0;j<(id==1?8:3);j++)
            {
                float a=(j*137.5f+55)*Mathf.Deg2Rad;
                Place("CoastalFern",root,new Vector3(Mathf.Cos(a)*17,0,Mathf.Sin(a)*17),id==1?1.65f:1.05f,j*43);
            }
            // Reuse the original game's textured shoreline rocks to match the surrounding bay.
            for(int j=0;j<18;j++)
            {
                float a=(j*137.5f+35)*Mathf.Deg2Rad,r=21+j%3;
                var p=new Vector3(Mathf.Cos(a)*r*(id==2?1.25f:1),0,Mathf.Sin(a)*r*(id==2?.88f:1));
                if(p.z>12 && Mathf.Abs(p.x)<13)continue;
                var stone=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/environment/Cliffs/Prefabs/Rock_Small_0"+(j%4+1)+".prefab");
                var detail=(GameObject)PrefabUtility.InstantiatePrefab(stone,root);
                detail.name="Weathered shore outcrop "+j;var t=detail.transform;t.localPosition=p;t.localRotation=Quaternion.Euler(0,j*73,0);
                var renderers=detail.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                t.localScale*= (id==0 && p.z<0?4.2f:1.2f+j%3*.7f)/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                t.position+=Vector3.up*(island.position.y+Ground(island,p)-bounds.min.y-.12f);
                foreach(var child in detail.GetComponentsInChildren<Transform>())child.gameObject.isStatic=true;
                if(id==1)FishingV019Art.Spawn("plant_bush",root,new Vector3(p.x*.8f,Ground(island,p*.8f),p.z*.8f),1.4f+j%3*.4f,j*53);
            }
            Physics.SyncTransforms();
            var npcRay=new Ray(island.position+new Vector3(7,1.95f,17),Vector3.down);
            if(!ground.GetComponent<MeshCollider>().Raycast(npcRay,out var footing,1.2f) || footing.normal.y<.69f)
            {
                bool found=ground.GetComponent<MeshCollider>().Raycast(new Ray(island.position+new Vector3(7,30,17),Vector3.down),out var fullHit,60);
                throw new InvalidOperationException("Resident footing or imported winding invalid: "+id+" found="+found+" y="+fullHit.point.y+" up="+fullHit.normal.y+" bounds="+ground.GetComponent<MeshCollider>().bounds);
            }
            Debug.Log("[V020-WORLD] FOOTING "+id+" y="+footing.point.y+" normal="+footing.normal.y);
        }
    }
}
