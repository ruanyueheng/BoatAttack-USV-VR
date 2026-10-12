using System;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV019WorldBuilder
    {
        private const string Folder = "Assets/Resources/FishingV019";
        private static Material _palette, _stone, _brass, _dark, _mint, _wood, _glow;
        public static void Install()
        {
            try
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before installing v19.");
                ImportArt();
                var scene = EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                var c = Object.FindObjectOfType<FishingPrototype>(); Transform islands = null;
                foreach (var go in scene.GetRootGameObjects()) if (go.name == FishingIslandWorld.WorldName) islands = go.transform;
                if (islands == null || islands.childCount != 4) throw new InvalidOperationException("V18 islands are required.");
                foreach (var go in scene.GetRootGameObjects()) if (go.name == FishingRealmWorld.RootName) Object.DestroyImmediate(go);
                var root = new GameObject(FishingRealmWorld.RootName).transform;
                for (int i = 0; i < 4; i++)
                {
                    var previous = islands.GetChild(i).Find("V019 island character"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
                    CreateIsland(islands.GetChild(i), i);
                    CreateRoom(root, i);
                }
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save v19 fishing scene.");
                MakeThumbnails(); AssetDatabase.Refresh();
                Debug.Log("[V019-WORLD] PASS: CC0 art, four island themes, two climbing routes, four physical sanctuary rooms and thumbnails installed.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Resources.Load<Shader>("FishingPalette")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Unlit", unlit ? 1 : 0); EditorUtility.SetDirty(m); return m;
        }
        private static void ImportArt()
        {
            AssetDatabase.Refresh();
            _palette = Material("MAT-V019-vertex-palette", Color.white);
            _stone = Material("Sanctuary basalt", new Color(.26f,.32f,.38f));
            _brass = Material("Sanctuary brass", new Color(.78f,.56f,.29f));
            _dark = Material("Sanctuary midnight", new Color(.08f,.14f,.19f));
            _mint = Material("Sanctuary foliage", new Color(.18f,.43f,.30f));
            _wood = Material("Sanctuary old timber", new Color(.40f,.27f,.16f));
            _glow = Material("Sanctuary living light", new Color(.38f,.91f,.82f), true);
            foreach (string path in Directory.GetFiles(Folder, "*.fbx"))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                importer.importAnimation = false; importer.isReadable = true; importer.globalScale = 1;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "MAT-V019-vertex-palette"), _palette);
                importer.SaveAndReimport();
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) throw new InvalidOperationException("Import failed " + path);
                foreach (var filter in asset.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh.colors32.Length != filter.sharedMesh.vertexCount) throw new InvalidOperationException("Vertex colours missing " + path);
            }
        }
        private static Transform Group(string name, Transform parent, Vector3 point)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = point; return t; }
        private static Transform Box(string name, Transform parent, Vector3 point, Vector3 size, Material material, bool solid = false)
            => FishingArt.Shape(name, PrimitiveType.Cube, parent, point, size, material, solid).transform;
        private static Transform Cylinder(string name, Transform parent, Vector3 point, Vector3 size, Material material)
            => FishingArt.Shape(name, PrimitiveType.Cylinder, parent, point, size, material).transform;
        private static float GroundY(Transform island, Vector3 p)
        {
            var terrain=island.Find("Walkable beach and interior");if(terrain==null)terrain=island.parent.Find("Walkable beach and interior");
            if (terrain==null || !terrain.GetComponent<MeshCollider>().Raycast(new Ray(island.TransformPoint(p + Vector3.up * 30), Vector3.down), out var hit, 60))
                throw new InvalidOperationException("Island theme missing ground " + island.name);
            return island.InverseTransformPoint(hit.point).y;
        }
        private static void Place(string asset, Transform parent, Vector3 point, float size, float yaw = 0)
        {
            point.y = GroundY(parent, point); var t = FishingV019Art.Spawn(asset, parent, point, size, yaw);
            foreach (var child in t.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = true;
        }
        private static void CreateIsland(Transform island, int id)
        {
            var root = Group("V019 island character", island, Vector3.zero);
            foreach (Transform child in island) if (child.name.StartsWith("Coastal palm")) child.gameObject.SetActive(id == 0 || id == 2);
            if (id == 0)
            {
                Place("tower-watch", root, new Vector3(-15,0,-10), 11, 20);
                Place("stone_tallC", root, new Vector3(17,0,-17), 8, 45);
                Place("stone_tallC", root, new Vector3(11,0,-19), 6, 120);
                for (int j = 0; j < 7; j++) FishingV019Art.Spawn("WindBell", root, new Vector3(-11+j*.43f, 5.7f,-9), .6f);
                Ladder(root, new Vector3(-13,0,5), 4.1f, "风铃瞭望台");
            }
            else if (id == 1)
            {
                for (int j = 0; j < 13; j++)
                {
                    float a = (j * 137.5f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Cos(a) * 20,0,Mathf.Sin(a) * 20);
                    if (p.z > 12 && p.x > -10 && p.x < 12) continue;
                    Place(j % 3 == 0 ? "tree_pineTallA" : "tree_oak", root, p, 7 + j % 4, j * 42);
                    Place("mushroom_redGroup", root, p * .82f, 1.1f, j * 71);
                }
                Place("tent_detailedOpen", root, new Vector3(-16,0,6), 5, 25);
                Place("campfire_logs", root, new Vector3(-12,0,10), 1.3f);
                Place("cliff_blockCave_rock", root, new Vector3(-19,0,-9), 7);
                for (int j = 0; j < 8; j++) Place("flower_purpleA", root, new Vector3(-20+j*1.5f,0,-16), .7f, j*60);
            }
            else if (id == 2)
            {
                Place("ship-wreck", root, new Vector3(-14,0,-12), 19, 38);
                Place("mast-ropes", root, new Vector3(17,0,-16), 9, -18);
                Place("cannon-mobile", root, new Vector3(12,0,-20), 3, 75);
                for (int j = 0; j < 5; j++) Place(j % 2 == 0 ? "barrel" : "crate-bottles", root, new Vector3(-18+j*1.6f,0,13), 1.2f, j*15);
                Ladder(root, new Vector3(-13,0,5), 3.6f, "归潮号打捞台");
            }
            else
            {
                for (int j = 0; j < 7; j++)
                {
                    float a = (j*26 + 15)*Mathf.Deg2Rad; var p = new Vector3(Mathf.Cos(a)*18,0,-Mathf.Sin(a)*18);
                    Place("stone_tallC", root, p, 5 + j % 3, j*26);
                    FishingV019Art.Spawn("StarPrism", root, new Vector3(p.x,6.5f,p.z), .9f, j*26);
                }
                var rings = Group("Armillary of the tidekeepers", root, new Vector3(-15,GroundY(root,new Vector3(-15,0,2))+3,2));
                for (int j = 0; j < 3; j++)
                { Orbit(rings,Vector3.zero,3.1f-j*.3f,Quaternion.Euler(j*48,0,j*40)); }
                FishingV019Art.Spawn("StarPrism", rings, Vector3.down*.6f, 1.8f);
            }
            var portal = Group("Portal", root, new Vector3(-6,GroundY(root,new Vector3(-6,0,16)),16));
            Box("Carved left jamb", portal, new Vector3(-1.25f,1.65f,0), new Vector3(.65f,3.3f,.7f), _stone, true);
            Box("Carved right jamb", portal, new Vector3(1.25f,1.65f,0), new Vector3(.65f,3.3f,.7f), _stone, true);
            Box("Crown lintel", portal, Vector3.up*3.5f, new Vector3(3.25f,.55f,.9f), _brass, true);
            Box("Tide rune", portal, new Vector3(0,3.51f,-.47f), new Vector3(.25f,.25f,.03f), _glow).localRotation = Quaternion.Euler(0,0,45);
            for (int j = 0; j < 5; j++) Box("Inlaid tide wave",portal,new Vector3(-.74f+j*.37f,2.9f,-.35f),new Vector3(.12f,.025f,.025f),_glow);
            FishingV019Art.Sign(FishingLore.ShrineName(id),portal,new Vector3(0,4.1f,-.45f),.06f,new Color(.97f,.85f,.55f));
            Place("flower_purpleA", root, new Vector3(-9,0,17), .55f);
        }
        private static void Ladder(Transform parent, Vector3 bottom, float height, string name)
        {
            bottom.y = GroundY(parent, bottom); var top = bottom + new Vector3(0,height,-6.5f);
            var deck = Box(name, parent, top + new Vector3(0,-.1f,-1), new Vector3(3.6f,.2f,3.8f),_wood,true);
            var route = Group("Climb " + name,parent,Vector3.zero).gameObject.AddComponent<FishingClimbRoute>(); route.bottom=bottom;route.top=top;
            for(int j=0;j<17;j++)
            {
                var p=Vector3.Lerp(bottom,top,j/16f);
                Box("Rope ladder rung",route.transform,p,new Vector3(.85f,.07f,.10f),_wood);
                foreach(float side in new[]{-.46f,.46f})Cylinder("Rope strand",route.transform,p+Vector3.right*side,new Vector3(.045f,.16f,.045f),_brass);
            }
            for(int j=0;j<4;j++)Cylinder("Lookout support",parent,top+new Vector3(j%2==0?-1.5f:1.5f,-height*.5f,j<2?-2.5f:.5f),new Vector3(.18f,height*.5f,.18f),_wood);
            FishingV019Art.Sign(name,parent,top+new Vector3(0,1,-2.6f),.04f,new Color(.95f,.82f,.53f));
        }
        private static void CreateRoom(Transform root, int id)
        {
            var room=Group("Realm "+id,root,new Vector3(500+id*48,96,450));
            Material floor=id==0?_brass:id==1?_mint:id==2?_wood:_stone;
            var foundation=Box("Sanctuary floor",room,new Vector3(0,-.18f,0),new Vector3(20,.36f,30),floor,true);
            foundation.GetComponent<MeshRenderer>().enabled=false;FloorTiles(room,id);
            foreach(float x in new[]{-10f,10f})Box("Collidable side wall",room,new Vector3(x,3,0),new Vector3(.4f,6,30),_dark,true);
            foreach(float z in new[]{-15f,15f})Box("Collidable end wall",room,new Vector3(0,3,z),new Vector3(20,6,.4f),_dark,true);
            Box("Sanctuary ceiling",room,new Vector3(0,6,0),new Vector3(20,.3f,30),_dark,true);
            for(int j=0;j<6;j++)
            {
                foreach(float x in new[]{-8.5f,8.5f})
                {
                    Cylinder("Carved sanctuary column",room,new Vector3(x,2.7f,-12+j*4.6f),new Vector3(.62f,2.7f,.62f),_stone);
                    Box("Luminous wall band",room,new Vector3(x,4.8f,-12+j*4.6f),new Vector3(.68f,.12f,.68f),_glow);
                }
            }
            foreach(float x in new[]{-6.25f,6.25f})Box("Inner sanctuary masonry",room,new Vector3(x,3,6),new Vector3(7.5f,6,.55f),_stone,true);
            foreach(float x in new[]{-2.4f,2.4f})Box("Carved echo arch",room,new Vector3(x,1.9f,6),new Vector3(.5f,3.8f,.8f),_stone,true);
            Box("Echo arch lintel",room,new Vector3(0,3.85f,6),new Vector3(5.4f,.55f,.8f),_stone);
            Box("Luminous echo lintel",room,new Vector3(0,3.66f,5.55f),new Vector3(4.2f,.08f,.04f),_glow);
            var gate=Group("Echo gate",room,new Vector3(0,0,6));var gateCollider=gate.gameObject.AddComponent<BoxCollider>();gateCollider.center=Vector3.up*1.7f;gateCollider.size=new Vector3(4.5f,3.4f,.3f);
            for(int bar=0;bar<9;bar++)Box("Portcullis brass bar",gate,new Vector3((bar-4)*.5f,1.7f,0),new Vector3(.15f,3.4f,.18f),_brass);
            foreach(float y in new[]{.7f,2.7f})Box("Portcullis brace",gate,new Vector3(0,y,0),new Vector3(4.35f,.1f,.18f),_brass);
            for(int j=0;j<10;j++)Box("Inlaid approach",room,new Vector3(0,.012f,-11+j*2.4f),new Vector3(.1f,.02f,1.2f),_glow);
            for(int i=0;i<(id==2?2:3);i++)
            {
                Vector3 p=id==2?new Vector3(i==0?-3:3,0,i==0?-5:-2):new Vector3((i-1)*4,0,-1);
                var device=Group("Device "+i,room,p);
                if(id==2 && i==0)
                {
                    FishingV019Art.Spawn("chest",device,Vector3.zero,1.45f);
                    Box("Cargo body",device,Vector3.up*.42f,new Vector3(1.3f,.84f,.8f),_wood,true);
                    Box("Cargo pressure plate",room,new Vector3(-3,.06f,0),new Vector3(2,.12f,2),_brass);
                    for(int rail=0;rail<2;rail++)Box("Cargo rail",room,new Vector3(-3+(rail==0?-.9f:.9f),.03f,-2.5f),new Vector3(.06f,.06f,6),_dark);
                }
                else
                {
                    Cylinder("Device plinth",device,new Vector3(0,.45f,0),new Vector3(.8f,.45f,.8f),_stone);
                    FishingV019Art.Spawn(id==0?"WindBell":id==1?"Lantern":id==2?"Compass":"StarPrism",device,Vector3.up*.9f,.65f);
                }
                var signal=Box("Signal",device,new Vector3(0,1.65f,0),new Vector3(.15f,.15f,.15f),_glow);signal.localRotation=Quaternion.Euler(0,0,45);signal.gameObject.SetActive(false);
                if(id==3)
                {
                    var pointer=Group("Star pointer",device,Vector3.up*.95f);
                    Box("Needle",pointer,new Vector3(0,0,.35f),new Vector3(.09f,.04f,.8f),_brass);
                    for(int d=0;d<4;d++)FishingV019Art.Sign(FishingAdventure.DirectionName(d),device,new Vector3(Mathf.Sin(d*Mathf.PI*.5f)*.8f,1.02f,Mathf.Cos(d*Mathf.PI*.5f)*.8f),.03f,new Color(.96f,.85f,.64f));
                }
                FishingV019Art.Sign(id==0?(i==0?"风":i==1?"潮":"星"):id==1?(i==0?"晨光":i==1?"林荫":"夕光"):id==2?(i==0?"货箱":"水闸"):(i+1)+" 号星盘",device,new Vector3(0,2.2f,0),.042f,new Color(.96f,.85f,.64f));
            }
            if(id==1)
            {
                Box("Forest water",room,new Vector3(0,.025f,3.8f),new Vector3(19,.03f,3),_dark);
                var barrier=Group("Crossing barrier",room,new Vector3(0,0,3.8f));var barrierCollider=barrier.gameObject.AddComponent<BoxCollider>();barrierCollider.center=Vector3.up*1.3f;barrierCollider.size=new Vector3(19.7f,2.6f,.2f);
                for(int ray=0;ray<19;ray++)Box("Unformed light bridge",barrier,new Vector3(ray-9,1.3f,0),new Vector3(.06f,2.6f,.06f),_glow);
                var bridge=Box("Light bridge",room,new Vector3(0,.12f,3.8f),new Vector3(3,.24f,3.3f),_glow,true);bridge.gameObject.SetActive(false);
                for(int j=0;j<8;j++)FishingV019Art.Spawn(j%2==0?"plant_bush":"mushroom_redGroup",room,new Vector3(j%2==0?-6:6,0,-9+j*2),1.6f);
            }
            if(id==2)
            {
                FishingV019Art.Spawn("barrel",room,new Vector3(-7,0,-8),2);
                FishingV019Art.Spawn("mast-ropes",room,new Vector3(7,0,1),4.8f);
                Box("Drydock shallow water",room,new Vector3(-5,.01f,10),new Vector3(5,.02f,7),_mint);
            }
            if(id==3)
            {
                for(int j=0;j<3;j++)Orbit(room,new Vector3(0,4.3f,9),2.8f+j*.4f,Quaternion.Euler(30+j*26,0,j*42));
            }
            var altar=Group("Device 3",room,new Vector3(0,0,11));
            Cylinder("Memory altar",altar,new Vector3(0,.42f,0),new Vector3(1.3f,.42f,1.3f),_stone);
            FishingV019Art.Spawn("StarPrism",altar,Vector3.up*.84f,.8f);
            FishingV019Art.Sign("潮汐回响",altar,new Vector3(0,2.15f,0),.044f,new Color(.5f,1,.9f));
            var exit=Group("Device 4",room,new Vector3(0,0,-13));
            Box("Return portal jamb",exit,new Vector3(-1.4f,1.6f,0),new Vector3(.4f,3.2f,.5f),_brass);
            Box("Return portal jamb",exit,new Vector3(1.4f,1.6f,0),new Vector3(.4f,3.2f,.5f),_brass);
            Box("Return portal crown",exit,Vector3.up*3.3f,new Vector3(3.2f,.4f,.5f),_glow);
            FishingV019Art.Sign("返回岛屿",exit,new Vector3(0,2.3f,.32f),.048f,new Color(.97f,.89f,.68f));
            FishingV019Art.Sign(FishingLore.ShrineName(id),room,new Vector3(0,4.5f,-14.6f),.072f,new Color(.97f,.89f,.68f));
            var light=Group("Sanctuary practical light",room,new Vector3(0,4.5f,0)).gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.range=24;light.intensity=4;light.color=id==0?new Color(1,.8f,.48f):id==1?new Color(.55f,1,.70f):id==2?new Color(.54f,.80f,1):new Color(.73f,.62f,1);light.shadows=LightShadows.None;
            room.gameObject.SetActive(false);
        }
        private static void FloorTiles(Transform room,int id)
        {
            var b=new FishingMeshBuilder();var color=id==0?new Color(.43f,.36f,.25f):id==1?new Color(.23f,.33f,.25f):id==2?new Color(.35f,.23f,.13f):new Color(.22f,.28f,.36f);
            for(int x=0;x<10;x++)for(int z=0;z<15;z++)
            {float shade=((x+z)%2==0?1:.93f);b.Box(new Vector3((x-4.5f)*2,.01f,(z-7)*2),new Vector3(1.97f,.02f,1.97f),color*shade);}
            var mesh=b.Finish("Carved sanctuary floor "+id);string path=Folder+"/SanctuaryFloor-"+id+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);}
            var tiles=Group("Patterned sanctuary paving",room,Vector3.zero).gameObject;tiles.AddComponent<MeshFilter>().sharedMesh=saved;tiles.AddComponent<MeshRenderer>().sharedMaterial=_palette;
        }
        private static void Orbit(Transform parent,Vector3 center,float radius,Quaternion rotation)
        {
            var root=Group("Armillary orbit",parent,center);root.localRotation=rotation;
            var line=FishingArt.Line("Brass orbital arc",root,_brass,.075f,false);line.positionCount=65;
            for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));}
        }
        public static void CaptureThumbnails(){_palette=Resources.Load<Material>("FishingV019/MAT-V019-vertex-palette");MakeThumbnails();AssetDatabase.Refresh();}
        private static void MakeThumbnails()
        {
            string directory=Folder+"/Thumbnails";Directory.CreateDirectory(directory);
            var camera=new GameObject("V19 temporary icon camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=.74f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.nearClipPlane=.01f;camera.farClipPlane=10;
            var rt=new RenderTexture(192,192,24);camera.targetTexture=rt;
            var icon=new Material(_palette);icon.SetFloat("_Unlit",1);
            try
            {
                foreach(string path in Directory.GetFiles(Folder,"*.fbx"))
                {
                    string name=Path.GetFileNameWithoutExtension(path);
                    var t=FishingV019Art.Spawn(name,null,new Vector3(1800,1800,1800),1);
                    foreach(var child in t.GetComponentsInChildren<Transform>())child.gameObject.layer=31;
                    foreach(var renderer in t.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=icon;
                    var offset=name.StartsWith("Resident")?new Vector3(-1.1f,.95f,1.8f):new Vector3(1.4f,1,-1.8f);
                    camera.transform.SetPositionAndRotation(t.position+offset,Quaternion.LookRotation(Vector3.up*.45f-offset));
                    var texture=new Texture2D(192,192,TextureFormat.RGBA32,false);var prior=RenderTexture.active;
                    try
                    {
                        camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,192,192),0,0);texture.Apply();
                        int visible=0;foreach(var pixel in texture.GetPixels32())if(pixel.a>32 && pixel.r+pixel.g+pixel.b>40)visible++;
                        if(Application.isPlaying && visible<150)throw new InvalidOperationException("Empty icon render "+name);
                        File.WriteAllBytes(directory+"/"+name+".png",texture.EncodeToPNG());
                    }
                    finally{RenderTexture.active=prior;Object.DestroyImmediate(texture);Object.DestroyImmediate(t.gameObject);}
                }
            }
            finally{camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(icon);}
        }
    }
}
