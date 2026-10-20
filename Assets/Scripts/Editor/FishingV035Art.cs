using System;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV035Art
    {
        public const string ArtRoot="V035 crafted coastal scenery";
        private const string Folder="Assets/Resources/FishingV035/";
        private static Material _material,_foliage,_water,_stone,_cascade;
        public static void Install()
        {
            try
            {
                if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before installing scenery.");
                AssetDatabase.Refresh();PrepareMaterials();
                var models=Directory.GetFiles(Folder,"*.fbx");
                foreach(string required in new[]{"SanctuaryWind","SanctuaryGrove","SanctuaryDrydock","SanctuaryStars","AncientTree","Windmill","Galleon","ObservatoryExterior","LevelTerrain0","LevelTerrain1","LevelTerrain2","LevelTerrain3"})
                    if(!File.Exists(Folder+required+".fbx"))throw new FileNotFoundException("Missing authored asset "+required);
                foreach(string path in models)
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.materialImportMode=ModelImporterMaterialImportMode.None;
                    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.isReadable=true;importer.SaveAndReimport();
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
                    {
                        if(filter.sharedMesh.uv.Length!=filter.sharedMesh.vertexCount)throw new InvalidDataException("Missing surface UV: "+path);
                        if(filter.sharedMesh.subMeshCount>1)
                        {
                            string collisionPath=Folder+Path.GetFileNameWithoutExtension(path)+"Collision.asset";
                            var collision=AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);bool create=collision==null;
                            if(create)collision=new Mesh{indexFormat=IndexFormat.UInt32};else collision.Clear();
                            collision.vertices=filter.sharedMesh.vertices;collision.triangles=filter.sharedMesh.GetTriangles(0);collision.RecalculateBounds();
                            if(create)AssetDatabase.CreateAsset(collision,collisionPath);else EditorUtility.SetDirty(collision);
                        }
                    }
                }
                var scene=EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                var islands=scene.GetRootGameObjects().Single(x=>x.name==FishingIslandWorld.WorldName).transform;
                for(int i=0;i<4;i++)InstallIsland(islands.GetChild(i),i);
                var realms=scene.GetRootGameObjects().Single(x=>x.name==FishingRealmWorld.RootName).transform;
                for(int i=0;i<4;i++)InstallRealm(realms.Find("Realm "+i),i);
                PlayerSettings.bundleVersion=FishingPrototype.Version;AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save V035 scenery.");
                Debug.Log("[V035-ART] PASS: "+models.Length+" textured Blender assets; distinct island relief and four authored traversal levels installed.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        private static void PrepareMaterials()
        {
            foreach(string name in new[]{"CoastalAlbedo","CoastalNormal","CoastalMetalSmooth"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+name+".png");importer.maxTextureSize=2048;importer.mipmapEnabled=true;
                importer.wrapMode=TextureWrapMode.Clamp;importer.sRGBTexture=name=="CoastalAlbedo";
                if(name=="CoastalNormal")importer.textureType=TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            _material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Coastal.mat");
            if(_material==null){_material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(_material,Folder+"Coastal.mat");}
            _material.enableInstancing=true;_material.SetFloat("_Cull",0);
            _material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"CoastalAlbedo.png"));
            _material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"CoastalNormal.png"));_material.SetFloat("_BumpScale",.5f);_material.EnableKeyword("_NORMALMAP");
            _material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"CoastalMetalSmooth.png"));_material.SetFloat("_Smoothness",1);_material.EnableKeyword("_METALLICSPECGLOSSMAP");EditorUtility.SetDirty(_material);
            _stone=AssetDatabase.LoadAssetAtPath<Material>(Folder+"ApproachStone.mat");
            if(_stone==null){_stone=new Material(_material);AssetDatabase.CreateAsset(_stone,Folder+"ApproachStone.mat");}
            _stone.SetTextureScale("_BaseMap",Vector2.one*.2325f);_stone.SetTextureOffset("_BaseMap",new Vector2(.00875f,.75875f));EditorUtility.SetDirty(_stone);
            _foliage=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Foliage.mat");
            if(_foliage==null){_foliage=new Material(Resources.Load<Shader>("FishingPalette"));AssetDatabase.CreateAsset(_foliage,Folder+"Foliage.mat");}
            _foliage.enableInstancing=true;_foliage.SetTexture("_BaseMap",_material.GetTexture("_BaseMap"));_foliage.SetFloat("_Textured",1);_foliage.SetFloat("_Shadows",1);
            _foliage.SetFloat("_Cull",0);_foliage.SetFloat("_DoubleSidedLighting",1);EditorUtility.SetDirty(_foliage);
            _foliage.SetFloat("_TitleBreeze",.055f);
            _water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"RealmWater.mat");var shader=Shader.Find("BoatAttack/FishingSanctuaryWater");
            if(shader==null)throw new InvalidOperationException("Missing sanctuary water shader.");
            if(_water==null){_water=new Material(shader);AssetDatabase.CreateAsset(_water,Folder+"RealmWater.mat");}else _water.shader=shader;
            EditorUtility.SetDirty(_water);
            _cascade=AssetDatabase.LoadAssetAtPath<Material>(Folder+"RealmCascade.mat");
            if(_cascade==null){_cascade=new Material(_water);AssetDatabase.CreateAsset(_cascade,Folder+"RealmCascade.mat");}
            _cascade.SetFloat("_FlowVertical",1);EditorUtility.SetDirty(_cascade);
        }
        private static Transform Spawn(string name,Transform parent,Vector3 position,float scale=1,float yaw=0,bool solid=false,bool dynamic=false)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".fbx");if(source==null)throw new FileNotFoundException(name);
            var go=new GameObject(name);var t=go.transform;t.SetParent(parent,false);Object.Instantiate(source,t,false);
            t.localPosition=position;t.localRotation=Quaternion.Euler(0,yaw,0);t.localScale=Vector3.one*scale;
            bool leaves=name=="FlowerPatch" || name=="IvyDrape" || name=="MeadowPatch" || name=="DuneGrass" || name=="FernClump";
            FishingV035Materials.Apply(t,leaves);
            if(leaves){var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.012f,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();}
            if(solid)foreach(var filter in go.GetComponentsInChildren<MeshFilter>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=Resources.Load<Mesh>("FishingV035/"+name+"Collision")??filter.sharedMesh;
            foreach(var child in go.GetComponentsInChildren<Transform>())child.gameObject.isStatic=!dynamic;
            return t;
        }
        private static Transform Empty(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        private static void Clear(Transform parent){foreach(var child in parent.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);}
        private static float Ground(Transform island,Vector3 p)
        {
            var collider=island.Find("Walkable beach and interior").GetComponent<MeshCollider>();
            if(!collider.Raycast(new Ray(island.TransformPoint(p+Vector3.up*30),Vector3.down),out var hit,60))throw new InvalidOperationException("Scenery outside terrain: "+p);
            return island.InverseTransformPoint(hit.point).y;
        }
        private static Transform OnGround(string name,Transform root,Vector3 p,float scale=1,float yaw=0,bool solid=false)
        {p.y=Ground(root.parent,p);return Spawn(name,root,p,scale,yaw,solid);}
        private static void ReplaceTerrain(Transform island,int id)
        {
            var temporary=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"LevelTerrain"+id+".fbx"));temporary.transform.position=Vector3.zero;
            var filters=temporary.GetComponentsInChildren<MeshFilter>();var combine=filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray();
            var baked=new Mesh{name="V035 coastline "+id,indexFormat=IndexFormat.UInt32};baked.CombineMeshes(combine,true,true);baked.RecalculateBounds();
            string path=Folder+"Terrain"+id+".asset";var persistent=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(persistent==null){persistent=baked;AssetDatabase.CreateAsset(persistent,path);}
            else {persistent.Clear();persistent.vertices=baked.vertices;persistent.normals=baked.normals;persistent.uv=baked.uv;persistent.triangles=baked.triangles;persistent.RecalculateBounds();Object.DestroyImmediate(baked);EditorUtility.SetDirty(persistent);}
            Object.DestroyImmediate(temporary);var ground=island.Find("Walkable beach and interior");ground.GetComponent<MeshFilter>().sharedMesh=persistent;
            ground.GetComponent<MeshRenderer>().sharedMaterial=_material;var collider=ground.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=persistent;
            Physics.SyncTransforms();
            foreach(string name in new[]{"Voyage letter","Hidden treasure","Compass beacon"}){var t=island.Find(name);var p=t.localPosition;p.y=Ground(island,p);t.localPosition=p;}
        }
        private static void InstallIsland(Transform island,int id)
        {
            ReplaceTerrain(island,id);
            var previous=island.Find(ArtRoot);if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var root=Empty(ArtRoot,island,Vector3.zero);var old=island.Find(FishingV020WorldBuilder.ArtRoot);
            if(old!=null)foreach(var child in old.Cast<Transform>().ToArray())if(child.name!="Planked landing jetty" && !child.name.StartsWith("Weathered shore outcrop"))Object.DestroyImmediate(child.gameObject);
            var character=island.Find("V019 island character");var portal=character.Find("Portal");
            foreach(var child in character.Cast<Transform>().ToArray())if(child!=portal)Object.DestroyImmediate(child.gameObject);
            Clear(portal);
            foreach(var renderer in portal.GetComponents<Renderer>())renderer.enabled=false;
            foreach(var collider in portal.GetComponents<Collider>())Object.DestroyImmediate(collider);
            var p=id==0?new Vector3(11,0,-12):id==1?new Vector3(-9,0,-9):id==2?new Vector3(-8,0,-8):new Vector3(4,0,-10);
            float size=id==1?1.35f:id==2?.7f:id==3?.65f:1;float yaw=id==3?180:0;
            var hero=OnGround(new[]{"Windmill","AncientTree","Galleon","ObservatoryExterior"}[id],root,p,size,yaw,true);
            if(id==2)
            {
                var start=island.InverseTransformPoint(FishingRealmWorld.Marker(hero,"Approach0").position);
                hero.localPosition+=Vector3.up*(Ground(island,start)-start.y);
            }
            portal.SetPositionAndRotation(FishingRealmWorld.Marker(hero,"Portal").position,Quaternion.Euler(0,yaw+180,0));portal.localScale=Vector3.one;
            var approach=Empty("Approach",portal,Vector3.forward*1.8f);
            if(id<2){var stance=island.InverseTransformPoint(approach.position);stance.y=Ground(island,stance);approach.position=island.TransformPoint(stance);}
            if(id==2)approach.position=FishingRealmWorld.Marker(hero,"Approach2").position;
            var route=Empty("Island approach",root,Vector3.zero);int index=0;
            Action<Vector3> point=v=>Empty("Route "+index++,route,v);
            point(new Vector3(0,1.04f,26.4f));point(new Vector3(0,Ground(island,new Vector3(0,0,10)),10));
            if(id==0)
            {
                point(new Vector3(-3,Ground(island,new Vector3(-3,0,-6)),-6));point(new Vector3(4,Ground(island,new Vector3(4,0,-18)),-18));
                point(new Vector3(11,Ground(island,new Vector3(11,0,-18)),-18));
                var rotor=Spawn("WindmillRotor",hero,hero.InverseTransformPoint(FishingRealmWorld.Marker(hero,"Rotor").position),1,0,false,true);rotor.gameObject.AddComponent<GenericRotation>().rotationVector=Vector3.forward*12;
                var climb=Empty("Windmill rope ladder",character,Vector3.zero).gameObject.AddComponent<FishingClimbRoute>();
                climb.bottom=climb.transform.InverseTransformPoint(FishingRealmWorld.Marker(hero,"ClimbBottom").position);
                climb.top=climb.transform.InverseTransformPoint(FishingRealmWorld.Marker(hero,"ClimbTop").position);
                climb.topOutset=climb.transform.InverseTransformVector(FishingRealmWorld.Marker(hero,"ClimbEnd").position-FishingRealmWorld.Marker(hero,"ClimbTop").position);
            }
            if(id==1)
            {
                point(new Vector3(3,Ground(island,new Vector3(3,0,-14)),-14));point(new Vector3(-9,Ground(island,new Vector3(-9,0,-15)),-15));
                var water=GameObject.CreatePrimitive(PrimitiveType.Plane);water.name="Tidal forest inlet";water.transform.SetParent(root,false);water.transform.localPosition=new Vector3(0,.54f,-3);water.transform.localScale=new Vector3(.7f,1,2.4f);water.GetComponent<Renderer>().sharedMaterial=_water;Object.DestroyImmediate(water.GetComponent<Collider>());
                foreach(var q in new[]{new Vector3(-18,0,-4),new Vector3(6,0,-12),new Vector3(-17,0,12),new Vector3(17,0,-6)})OnGround("AncientTree",root,q,.85f,q.x*11);
            }
            if(id==2)
            {
                foreach(var q in new[]{new Vector3(-18,0,10),new Vector3(-18,0,-3.8f)})point(new Vector3(q.x,Ground(island,q),q.z));
                for(int j=0;j<3;j++)point(island.InverseTransformPoint(FishingRealmWorld.Marker(hero,"Approach"+j).position));
            }
            if(id==3)
            {
                var first=hero.TransformPoint(new Vector3(0,0,-13.6f));var basePoint=island.InverseTransformPoint(first)+Vector3.forward*3; basePoint.y=Ground(island,basePoint);
                Steps(root,basePoint,island.InverseTransformPoint(first),2);point(basePoint);point(island.InverseTransformPoint(first));
                for(int j=0;j<17;j++)point(island.InverseTransformPoint(FishingRealmWorld.Marker(hero,"Route"+j).position));
                var telescope=Spawn("TelescopeBase",hero,new Vector3(0,3.6f,10));var mount=Spawn("TelescopeMount",telescope,Vector3.up*2.1f);Spawn("TelescopeTube",mount,Vector3.zero,1,-25);
                for(int j=0;j<20;j++)
                {
                    float a=j*18*Mathf.Deg2Rad;Spawn("FlowerPatch",hero,Native(new Vector3(Mathf.Cos(a)*8.1f,.02f,-2+Mathf.Sin(a)*8.1f)),.9f,j*37);
                    if(j%3==0)Spawn("IvyDrape",hero,Native(new Vector3(Mathf.Cos(a)*4.8f,3.55f,10+Mathf.Sin(a)*4.8f)),1,j*18);
                }
            }
            if(id==0)foreach(float x in new[]{-1.7f,1.7f})Spawn("IvyDrape",hero,Native(new Vector3(x,5.1f,-1.4f)),1.2f);
            point(island.InverseTransformPoint(approach.position));
            // Scattered details stay off the actual approach and resident paths.
            int count=id==3?120:id==2?110:190;
            for(int j=0;j<count;j++)
            {
                float a=j*2.399963f,r=5+Mathf.Sqrt((j+.5f)/count)*21;var q=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                if(route.Cast<Transform>().Any(t=>FishingPrototype.FlatDistance(q,t.localPosition)<2.8f) || (Mathf.Abs(q.x)<3.2f && q.z>0) || (q.z>16 && q.z<27 && q.x>-2 && q.x<11) || new Vector2(q.x-7,q.z-17).magnitude<3.2f || new Vector2(q.x+6,q.z-16).magnitude<3.2f || (new Vector2(q.x-p.x,q.z-p.z).magnitude<(id==2?13:id==3?9:5)))continue;
                if(id==1 && j%12==0)OnGround("MangroveTree",root,q,1.4f+j%3*.18f,j*47);
                else if(id==0 && j%23==0)OnGround("CoastalPine",root,q,1.2f,j*47);
                else if(id==2 && j%30==0)OnGround("DunePalm",root,q,1.3f,j*47);
                else if(j%11==0 || id==3 && j%3==0)OnGround("ShoreRock",root,q,.9f+j%3*.35f,j*47,true);
                else {string asset=id==1 && j%3==0?"FernClump":id==2?"DuneGrass":j%6==0?"FlowerPatch":"MeadowPatch";var plant=OnGround(asset,root,q,1,j*47);if(asset!="FlowerPatch")plant.localScale=Vector3.Scale(plant.localScale,new Vector3(2.5f,1,2.5f));}
            }
            var points=route.Cast<Transform>().ToArray();
            for(int segment=1;segment<points.Length;segment++)
            {
                var a=points[segment-1].localPosition;var b=points[segment].localPosition;var delta=Vector3.ProjectOnPlane(b-a,Vector3.up);
                if(delta.magnitude<2)continue;var side=Vector3.Cross(delta.normalized,Vector3.up);int samples=Mathf.Min(12,Mathf.CeilToInt(delta.magnitude/1.8f));
                for(int sample=1;sample<samples;sample++)foreach(float sign in new[]{-1f,1f})
                {
                    var q=Vector3.Lerp(a,b,sample/(float)samples)+side*(sign*1.8f);float y=Ground(island,q);
                    if(Mathf.Abs(y-Mathf.Lerp(a.y,b.y,sample/(float)samples))>.55f)continue;
                    Spawn(id==1?"FernClump":id==2?"DuneGrass":sample%3==0?"MeadowPatch":"FlowerPatch",root,new Vector3(q.x,y,q.z),id==2?1.7f:1.35f,segment*37+sample*47);
                }
            }
            Physics.SyncTransforms();
        }
        private static void Steps(Transform root,Vector3 a,Vector3 b,float width)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(b.y-a.y)/.15f));var direction=b-a;float length=new Vector2(direction.x,direction.z).magnitude;
            for(int i=0;i<count;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Carved approach step";go.transform.SetParent(root,false);var p=Vector3.Lerp(a,b,(i+.5f)/count);p.y=Mathf.Lerp(a.y,b.y,(i+1f)/count)-.08f;
                go.transform.localPosition=p;go.transform.localRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Vector3.up));go.transform.localScale=new Vector3(width,.16f,length/count+.035f);go.GetComponent<Renderer>().sharedMaterial=_stone;
            }
        }
        private static void Light(Transform root,Vector3 p,Color color,float range,float intensity)
        {var light=Empty("Sanctuary practical light",root,p).gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.range=range;light.intensity=intensity;light.shadows=LightShadows.None;}
        private static void InstallRealm(Transform room,int id)
        {
            Clear(room);var root=Empty(GeometryRootName,room,Vector3.zero);
            var model=Spawn(new[]{"SanctuaryWind","SanctuaryGrove","SanctuaryDrydock","SanctuaryStars"}[id],root,Vector3.zero,1,0,true);
            for(int j=0;j<5;j++)
            {
                var marker=FishingRealmWorld.Marker(model,"Device"+j);var device=Empty("Device "+j,room,room.InverseTransformPoint(marker.position));
                if(j==2 && id==2){device.gameObject.SetActive(false);continue;}
                if(j<3 && id==1)Spawn("PuzzlePlinth1",device,Vector3.zero,1,0,false,true);
                if(j==0 && id==2){Spawn("CargoCrate",device,Vector3.zero,1,0,false,true);var collider=device.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.up*.43f;collider.size=new Vector3(1.3f,.86f,.8f);}
                if(j==1 && id==2)Spawn("PuzzlePlinth2",device,Vector3.zero,1,0,false,true);
                if((j==1 || j==2) && id==3)Spawn("StarChart",device,Vector3.zero,1,0,false,true);
                if(j==3)Spawn(new[]{"GiantBell","LivingFountain","CaptainChart","StarChart"}[id],device,id==0?Vector3.up*2.8f:Vector3.zero,1,0,false,true);
                if(j<3){var signal=Empty("Signal",device,Vector3.up*.08f);Spawn("StarChart",signal,Vector3.zero,.18f,0,false,true);signal.gameObject.SetActive(false);}
                if(j==4)Spawn("StarChart",device,Vector3.zero,.65f,0,false,true);
            }
            if(id==1)for(int j=0;j<4;j++){var marker=FishingRealmWorld.Marker(model,"Ancient"+j);Spawn("AncientTree",root,room.InverseTransformPoint(marker.position),1.2f,j*67,true);}
            if(id==0){Light(root,new Vector3(0,6,-17),new Color(1,.89f,.70f),18,1.2f);Light(root,new Vector3(0,9,23),new Color(1,.74f,.4f),12,1.4f);}
            if(id==1)foreach(var p in new[]{new Vector3(-7,4,-4),new Vector3(-6,4,8),new Vector3(6,4,8),new Vector3(6,4,21)})Light(root,p,new Color(1,.81f,.45f),11,1.25f);
            if(id==2)foreach(var p in new[]{new Vector3(-4,4,-14),new Vector3(4,4,4),new Vector3(-3,1,-3),new Vector3(0,4,17)})Light(root,p,new Color(1,.66f,.33f),12,1.7f);
            if(id==3){Light(root,new Vector3(0,8,9),new Color(.68f,.80f,1),17,2.7f);Light(root,new Vector3(3,6,12),new Color(1,.73f,.37f),9,2);Light(root,new Vector3(-5,3,-4),new Color(1,.76f,.48f),12,1.4f);}
            DressRealm(root,id);
            room.gameObject.SetActive(false);
        }
        private static Vector3 Native(Vector3 p)=>new Vector3(-p.x,p.y,p.z);
        private static void DressRealm(Transform root,int id)
        {
            if(id==0)
            {
                var centres=new[]{new Vector3(0,0,-18),new Vector3(-7,1.5f,-3),new Vector3(6,3.2f,9),new Vector3(0,5,23)};
                for(int n=0;n<centres.Length;n++)for(int j=0;j<12;j++)
                {
                    float a=(j*30+15)*Mathf.Deg2Rad;var c=centres[n];float r=n==0?4.1f:3.5f;
                    Spawn("FlowerPatch",root,Native(c+new Vector3(Mathf.Cos(a)*r,.015f,Mathf.Sin(a)*r)),.85f,j*47);
                    if(j%2==0)Spawn("IvyDrape",root,Native(c+new Vector3(Mathf.Cos(a)*(r+1),-.1f,Mathf.Sin(a)*(r+1))),1,-j*30);
                }
                foreach(float x in new[]{-2f,2f})Spawn("IvyDrape",root,Native(new Vector3(x,8.8f,23.7f)),.75f);
                var clouds=GameObject.CreatePrimitive(PrimitiveType.Plane);clouds.name="Soft cloud sea below wind bridges";clouds.transform.SetParent(root,false);
                clouds.transform.localPosition=new Vector3(0,-5.8f,6);clouds.transform.localScale=new Vector3(24,1,24);Object.DestroyImmediate(clouds.GetComponent<Collider>());
                var renderer=clouds.GetComponent<Renderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Objects/environment/Clouds/CloudLayer.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            if(id==1)
            {
                var centres=new[]{new Vector3(-10.8f,0,1),new Vector3(10.5f,0,-5),new Vector3(-10.8f,0,15),new Vector3(12,0,19),new Vector3(6,0,22)};
                foreach(var c in centres)for(int j=0;j<9;j++)
                {
                    float a=j*40*Mathf.Deg2Rad;Spawn(j%3==0?"FernClump":"FlowerPatch",root,Native(c+new Vector3(Mathf.Cos(a)*2.25f,.02f,Mathf.Sin(a)*2.25f)),.85f,j*37);
                    if(j%3==0)Spawn("IvyDrape",root,Native(c+new Vector3(Mathf.Cos(a)*2,5.8f,Mathf.Sin(a)*2)),1.3f,j*40);
                }
                var waterfall=Spawn("GroveWaterfall",root,Native(new Vector3(11,.33f,22)),1.25f);waterfall.name="Living seed spillway";
                foreach(var renderer in waterfall.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=_cascade;
            }
            if(id==2)
            {
                foreach(float z in new[]{-8f,1f,8f})Spawn("MooringPulley",root,Native(new Vector3(-3.5f,5.2f,z)),.85f);
                foreach(float x in new[]{-3f,3f})Spawn("IvyDrape",root,Native(new Vector3(x,4.3f,-9)),.5f);
            }
            if(id==3)
            {
                for(int j=0;j<24;j++)
                {
                    float a=j*15*Mathf.Deg2Rad;Spawn("FlowerPatch",root,Native(new Vector3(Mathf.Cos(a)*8.2f,.02f,-2+Mathf.Sin(a)*8.2f)),.72f,j*31);
                    if(j%3==0){var p=new Vector3(Mathf.Cos(a)*4.8f,3.55f,10+Mathf.Sin(a)*4.8f);Spawn("IvyDrape",root,Native(p),.85f,j*15);}
                }
            }
        }
        private const string GeometryRootName=FishingRealmWorld.GeometryRootName;
    }
}
