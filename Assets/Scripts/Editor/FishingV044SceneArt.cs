using System;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Installs the V044 AI-remodelled scenery: one textured URP Lit material per remodelled V035 model
    /// (Resources/FishingV044/Scene/&lt;Name&gt;.mat, picked up by FishingV035Materials at runtime) and re-points the
    /// already-placed scene instances at it. Geometry is swapped in place by Tools/Art/build_v044_scene.py.</summary>
    public static class FishingV044SceneArt
    {
        public const string Folder="Assets/Resources/FishingV044/Scene/";
        public static readonly string[] Models={"ShoreRock","CoastalPine","DunePalm","MangroveTree","MeadowPatch","DuneGrass","FlowerPatch","FernClump","TideWreck","MarketShip","LandingJetty","AncientTree","Windmill","WindmillRotor","Galleon","Lighthouse"};
        public static void Install()
        {
            try
            {
                if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before installing scenery.");
                AssetDatabase.Refresh();
                foreach(string name in Models)
                {
                    string png=Folder+(name=="WindmillRotor"?"Windmill":name)+"Albedo.png";
                    var importer=(TextureImporter)AssetImporter.GetAtPath(png)??throw new FileNotFoundException(png);
                    importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.sRGBTexture=true;
                    importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_6x6});
                    importer.SaveAndReimport();
                    // Plants keep the shared FishingPalette foliage shader (vertex-alpha wind); rocks use plain URP Lit.
                    bool plant=new[]{"CoastalPine","DunePalm","MangroveTree","MeadowPatch","DuneGrass","FlowerPatch","FernClump","AncientTree"}.Contains(name);var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
                    if(material==null){material=plant?new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/FishingV035/Foliage.mat")):new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+name+".mat");}
                    material.enableInstancing=true;material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(png));
                    if(plant)material.SetFloat("_Textured",1);else{material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.12f);material.SetFloat("_Metallic",0);}
                    EditorUtility.SetDirty(material);
                    // TideWreck is a new lighthouse-tide prop (Resources/FishingV044/TideWreck.fbx); the rest replace V035 models in place.
                    var fbx=(ModelImporter)AssetImporter.GetAtPath(name=="TideWreck"?"Assets/Resources/FishingV044/TideWreck.fbx":name=="MarketShip"?"Assets/Resources/FishingV044/Sea/MarketShip.fbx":name=="LandingJetty"?"Assets/Resources/FishingV020/LandingJetty.fbx":"Assets/Resources/FishingV035/"+name+".fbx");
                    fbx.materialImportMode=ModelImporterMaterialImportMode.None;fbx.isReadable=true;fbx.SaveAndReimport();
                    if(name=="AncientTree")TrunkCollision(name);
                }
                ClearOldLighthouseCollision();
                ClearWreckCabins();
                foreach(string path in Directory.GetFiles(Folder,"*_LOD1.fbx"))
                {var lod=(ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));lod.materialImportMode=ModelImporterMaterialImportMode.None;lod.importAnimation=false;lod.isReadable=true;lod.SaveAndReimport();}
                AssetDatabase.SaveAssets();
                var scene=EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);int changed=0,lods=0;
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true).ToArray())
                    {
                        if(!renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh==null)continue;
                        if(renderer.name=="LOD1")continue;
                        // V020 meshes keep their legacy names (scene fileIDs); match those by source asset instead.
                        string source=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(filter.sharedMesh));
                        var own=FishingV035Materials.Override(filter.sharedMesh) ?? (source=="LandingJetty"?AssetDatabase.LoadAssetAtPath<Material>(Folder+source+".mat"):null);
                        if(own!=null && renderer.sharedMaterial!=own){renderer.sharedMaterials=Enumerable.Repeat(own,filter.sharedMesh.subMeshCount).ToArray();changed++;}
                        if(renderer.transform.Find("LOD1")==null){FishingV035Materials.AddLod(renderer);if(renderer.transform.Find("LOD1")!=null)lods++;}
                    }
                int outcrops=ReplaceOutcrops(scene);int fitted=ClearRoutes(scene);int cleared=ClearPlants(scene);int clusters=BakeClusters(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save V044 scenery.");
                Debug.Log("[V044-SCENE-ART] PASS: "+Models.Length+" remodelled models, "+changed+" placed renderers re-pointed, "+lods+" LOD1 added, "+outcrops+" bleached Rock_Small outcrops replaced by ShoreRock, "+fitted+" shrunk off walking routes, "+cleared+" ground-cover clumps shrunk/removed where they pierced fences or props, "+clusters+" grass clusters baked.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        /// <summary>Replacement outcrops are rounder than the cliff shards they replace; any whose footprint reaches within
        /// 1.2 m of an authored "Island approach" route segment is shrunk in place until the corridor is clear (V019/V044-WALK).</summary>
        private static int ClearRoutes(UnityEngine.SceneManagement.Scene scene)
        {
            int count=0;
            foreach(var route in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="Island approach").ToArray())
            {
                var island=route.parent.parent;var points=route.Cast<Transform>().OrderBy(t=>int.Parse(t.name.Substring(6))).Select(t=>t.position).ToArray();
                foreach(var rock in island.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Weathered shore outcrop")).ToArray())
                {
                    var collider=rock.GetComponentInChildren<MeshCollider>();if(collider==null)continue;
                    var b=collider.bounds;float radius=Mathf.Max(b.extents.x,b.extents.z),gap=float.MaxValue;
                    for(int i=1;i<points.Length;i++)gap=Mathf.Min(gap,FlatSegmentDistance(b.center,points[i-1],points[i]));
                    foreach(var mark in island.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Portal" || t.name=="Approach" || t.name.StartsWith("Resident") || t.name.Contains("jetty")))
                        gap=Mathf.Min(gap,Vector2.Distance(new Vector2(b.center.x,b.center.z),new Vector2(mark.position.x,mark.position.z)));
                    // V035Levels walks dock→resident→portal on paths near (not on) the approach points, so keep 2.5 m clear and drop offenders.
                    if(gap-radius>=2.5f)continue;
                    Object.DestroyImmediate(rock.gameObject);count++;
                }
            }
            return count;
        }
        private static readonly string[] Plants={"MeadowPatch","DuneGrass","FlowerPatch","FernClump"};
        /// <summary>No-interpenetration rule: the new clumps are fuller than the old blade cards, so any whose (80%) bounds
        /// meet a non-plant, non-ground renderer — fence rails, jetty, props, rocks — shrinks once to 65%, else is removed.</summary>
        private static int ClearPlants(UnityEngine.SceneManagement.Scene scene)
        {
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>()).ToArray();
            // Trees are skipped too: their canopy AABB would claim every clump in its shade.
            string[] soft={"CoastalPine","DunePalm","MangroveTree","AncientTree","OrchardTree","IvyDrape"};
            bool PlantPart(Transform t){for(;t!=null;t=t.parent)if(Plants.Contains(t.name) || soft.Contains(t.name))return true;return false;}
            var solids=all.Where(r=>r.name!="LOD1" && !PlantPart(r.transform) && !r.name.StartsWith("Walkable")).ToArray();
            solids=solids.Where(r=>!r.name.StartsWith("Walkable") && !r.transform.name.Contains("water",StringComparison.OrdinalIgnoreCase) && !r.transform.name.Contains("inlet",StringComparison.OrdinalIgnoreCase)).ToArray();
            int count=0;
            foreach(var plant in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>Plants.Contains(t.name)).ToArray())
            {
                bool Hits()
                {
                    var own=plant.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name!="LOD1").ToArray();if(own.Length==0)return false;
                    var b=own[0].bounds;foreach(var r in own)b.Encapsulate(r.bounds);b.extents*=.8f;
                    return solids.Any(r=>r!=null && r.enabled && r.bounds.Intersects(b) && r.bounds.size.magnitude<40);
                }
                if(!Hits())continue;
                plant.localScale*=.65f;
                if(Hits())Object.DestroyImmediate(plant.gameObject);
                count++;
            }
            return count;
        }
        /// <summary>A closed trunk/root volume replaces disconnected AI branch faces that could cage a walking capsule.</summary>
        private static void TrunkCollision(string name)
        {
            var collision=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/FishingV035/"+name+"Collision.asset");
            const int sides=16;var vertices=new Vector3[sides*3+2];var triangles=new System.Collections.Generic.List<int>();
            var heights=new[]{-.2f,.7f,2.8f};var radii=new[]{1.75f,1.1f,.7f};
            for(int ring=0;ring<3;ring++)for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;vertices[ring*sides+i]=new Vector3(Mathf.Cos(angle)*radii[ring],heights[ring],Mathf.Sin(angle)*radii[ring]);
                if(ring<2){int a=ring*sides+i,b=ring*sides+(i+1)%sides;triangles.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
            }
            vertices[sides*3]=Vector3.up*heights[0];vertices[sides*3+1]=Vector3.up*heights[2];
            for(int i=0;i<sides;i++){int next=(i+1)%sides;triangles.AddRange(new[]{sides*3,i,next,sides*3+1,sides*2+next,sides*2+i});}
            collision.Clear();collision.vertices=vertices;collision.triangles=triangles.ToArray();collision.RecalculateBounds();EditorUtility.SetDirty(collision);
        }
        private static void ClearWreckCabins()
        {
            var collision=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/FishingV035/GalleonCollision.asset");
            var vertices=collision.vertices;var source=collision.triangles;var kept=new System.Collections.Generic.List<int>();
            for(int i=0;i<source.Length;i+=3)
            {
                var a=vertices[source[i]];var b=vertices[source[i+1]];var c=vertices[source[i+2]];var normal=Vector3.Cross(b-a,c-a);float top=Mathf.Max(a.y,Mathf.Max(b.y,c.y));
                if(top<.5f || top<=2.2f && normal.y>normal.magnitude*.25f)kept.AddRange(new[]{source[i],source[i+1],source[i+2]});
            }
            if(kept.Count==0)throw new InvalidOperationException("Wreck must retain its authored deck and boarding ramp.");
            collision.triangles=kept.ToArray();collision.RecalculateBounds();EditorUtility.SetDirty(collision);
        }
        private static void ClearOldLighthouseCollision()
        {
            var collision=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/FishingV035/HomeLandscapingCollision.asset");
            var vertices=collision.vertices;var source=collision.triangles;var kept=new System.Collections.Generic.List<int>();
            for(int i=0;i<source.Length;i+=3)
            {
                var center=(vertices[source[i]]+vertices[source[i+1]]+vertices[source[i+2]])/3;
                if(Vector2.Distance(new Vector2(center.x,center.z),new Vector2(-19,12))>=3.1f)kept.AddRange(new[]{source[i],source[i+1],source[i+2]});
            }
            collision.triangles=kept.ToArray();collision.RecalculateBounds();EditorUtility.SetDirty(collision);
        }
        public const string ClusterRoot="V044 grass clusters";
        /// <summary>PICO draw calls: ground-cover clumps are merged per 8×8 m cell and per material into one mesh (plus a merged
        /// LOD1) under "V044 grass clusters/Full" — and a second set from every other clump under ".../Half" (inactive) for
        /// perf's PICO half-density switch. The individual clumps are removed. Meshes live in GrassClusters.asset.</summary>
        private static int BakeClusters(UnityEngine.SceneManagement.Scene scene)
        {
            const float cell=8;string path=Folder+"GrassClusters.asset";
            // Only the always-on outdoor islands; realm rooms toggle their own roots and must keep their clumps.
            var plants=scene.GetRootGameObjects().Where(r=>r.name==FishingIslandWorld.WorldName).SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>Plants.Contains(t.name) && t.GetComponentInChildren<MeshFilter>()!=null).ToArray();
            if(plants.Length==0)return 0;  // already baked on a previous install; keep those clusters
            var store=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(store!=null){foreach(var old in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))Object.DestroyImmediate(old,true);}
            else{store=new Mesh{name="GrassClusters index"};AssetDatabase.CreateAsset(store,path);}
            foreach(var old in scene.GetRootGameObjects().Where(r=>r.name==ClusterRoot).ToArray())Object.DestroyImmediate(old);
            var root=new GameObject(ClusterRoot).transform;int count=0;
            foreach(var (set,half) in new[]{("Full",false),("Half",true)})
            {
                var group=new GameObject(set).transform;group.SetParent(root,false);
                var chosen=plants.Where((t,i)=>!half || i%2==0);
                foreach(var bucket in chosen.GroupBy(t=>(Mathf.FloorToInt(t.position.x/cell),Mathf.FloorToInt(t.position.z/cell),t.GetComponentInChildren<MeshRenderer>().sharedMaterial)))
                {
                    var origin=new Vector3((bucket.Key.Item1+.5f)*cell,bucket.Min(t=>t.position.y),(bucket.Key.Item2+.5f)*cell);
                    var go=new GameObject(bucket.Key.Item3.name+" "+bucket.Key.Item1+","+bucket.Key.Item2);go.transform.SetParent(group,false);go.transform.position=origin;
                    var lods=new LOD[2];
                    for(int level=0;level<2;level++)
                    {
                        var parts=bucket.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>()).Where(f=>(f.name=="LOD1")==(level==1)).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=go.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
                        if(parts.Length==0){lods[level]=new LOD(level==0?.12f:.02f,new Renderer[0]);continue;}
                        var mesh=new Mesh{name=set+" "+go.name+" L"+level,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(parts,true,true);mesh.RecalculateBounds();
                        AssetDatabase.AddObjectToAsset(mesh,path);
                        var part=level==0?go:new GameObject("LOD1");if(level==1)part.transform.SetParent(go.transform,false);
                        part.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=bucket.Key.Item3;
                        renderer.shadowCastingMode=level==0?UnityEngine.Rendering.ShadowCastingMode.On:UnityEngine.Rendering.ShadowCastingMode.Off;
                        lods[level]=new LOD(level==0?.12f:.02f,new Renderer[]{renderer});
                    }
                    var lodGroup=go.AddComponent<LODGroup>();lodGroup.SetLODs(lods);lodGroup.RecalculateBounds();
                    foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;count++;
                }
                if(half)group.gameObject.SetActive(false);
            }
            foreach(var t in plants)Object.DestroyImmediate(t.gameObject);
            AssetDatabase.SaveAssets();return count;
        }
        private static float FlatSegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {
            var p2=new Vector2(p.x,p.z);var a2=new Vector2(a.x,a.z);var ab=new Vector2(b.x,b.z)-a2;
            float t=ab.sqrMagnitude<1e-6f?0:Mathf.Clamp01(Vector2.Dot(p2-a2,ab)/ab.sqrMagnitude);return Vector2.Distance(p2,a2+ab*t);
        }
        /// <summary>The V020 islands reuse BoatAttack's cliff Rock_Small prefabs, which read as bleached white at island scale.
        /// Each is swapped for the mossy ShoreRock with the same footprint, ground contact and yaw, keeping its name.</summary>
        private static int ReplaceOutcrops(UnityEngine.SceneManagement.Scene scene)
        {
            var rock=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/FishingV035/ShoreRock.fbx");int count=0;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var outcrop in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Weathered shore outcrop") && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).ToArray())
                {
                    var renderers=outcrop.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                    var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    var go=new GameObject(outcrop.name);var t=go.transform;t.SetParent(outcrop.parent,false);
                    var view=(GameObject)PrefabUtility.InstantiatePrefab(rock,t);
                    var size=view.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size;
                    t.rotation=Quaternion.Euler(0,outcrop.eulerAngles.y,0);t.localScale=Vector3.one*(Mathf.Max(bounds.size.x,bounds.size.z)/Mathf.Max(size.x,size.z));
                    t.position=new Vector3(bounds.center.x,bounds.min.y+.22f*t.lossyScale.y,bounds.center.z);
                    FishingV035Materials.Apply(t);
                    foreach(var filter in go.GetComponentsInChildren<MeshFilter>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                    foreach(var child in go.GetComponentsInChildren<Transform>())child.gameObject.isStatic=true;
                    Object.DestroyImmediate(outcrop.gameObject);count++;
                }
            return count;
        }
    }
}
