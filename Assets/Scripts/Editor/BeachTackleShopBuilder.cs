using System;
using System.IO;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using TMPro;

namespace BoatAttack.EditorTools
{
    /// <summary>Produces an editable scene prefab; nothing is regenerated when the game starts.</summary>
    public static class BeachTackleShopBuilder
    {
        public const string Folder = "Assets/Objects/BeachTackleShop";
        public const string PrefabPath = Folder + "/BeachTackleShop.prefab";
        private static Material _wood, _pale, _teal, _roof, _metal, _gold, _glass, _cloth, _red;
        private static Transform _root;

        public static void ResolveFootprint()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use only as an explicit scene-authoring operation.");
            var scene=EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
            var shop=Object.FindObjectOfType<FishingTackleShop>();
            var changed=new HashSet<GameObject>();
            foreach(var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if(renderer.transform.IsChildOf(shop.transform))continue;
                var p=shop.transform.InverseTransformPoint(renderer.bounds.center);
                if(Mathf.Abs(p.x)>6.2f || p.z < -7 || p.z>5.4f || p.y>6 || p.y< -3)continue;
                var lod=renderer.GetComponentInParent<LODGroup>();
                var target=lod!=null ? lod.gameObject : renderer.gameObject;
                var name=target.name;
                if(!(name.StartsWith("Palm",StringComparison.Ordinal) || name.StartsWith("Geo_Props_Chair",StringComparison.Ordinal) || name.StartsWith("Geo_Props_Umbrella",StringComparison.Ordinal)))continue;
                if(!changed.Add(target))continue;
                target.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                Debug.Log("[SHOP-FOOTPRINT] Scene-only inactive override: "+name+" local="+p);
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SHOP-FOOTPRINT] PASS: "+changed.Count+" intersecting decoration instances hidden; original prop assets unchanged.");
            EditorApplication.Exit(0);
        }

        public static void InspectFootprint()
        {
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
            var shop=Object.FindObjectOfType<FishingTackleShop>();
            foreach(var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if(renderer.transform.IsChildOf(shop.transform))continue;
                var p=shop.transform.InverseTransformPoint(renderer.bounds.center);
                if(Mathf.Abs(p.x)>9 || p.z < -10 || p.z>7 || p.y>9 || p.y< -4)continue;
                var path=renderer.name;var parent=renderer.transform.parent;
                for(int i=0;i<4 && parent!=null;i++,parent=parent.parent)path=parent.name+"/"+path;
                Debug.Log("[SHOP-FOOTPRINT] "+path+" local="+p+" size="+renderer.bounds.size);
            }
            EditorApplication.Exit(0);
        }

        [MenuItem("BoatAttack/Fishing/Focus beach tackle shop")]
        public static void FocusShop()
        {
            var shop=Object.FindObjectOfType<FishingTackleShop>();
            if(shop==null && !EditorApplication.isPlaying)
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                shop=Object.FindObjectOfType<FishingTackleShop>();
            }
            if(shop==null){Debug.LogWarning("No beach shop in this scene.");return;}
            Selection.activeGameObject=shop.gameObject;
            if(SceneView.lastActiveSceneView!=null)
                SceneView.lastActiveSceneView.LookAt(shop.transform.position+Vector3.up*2,shop.transform.rotation*Quaternion.Euler(25,145,0),22);
        }

        [MenuItem("BoatAttack/Fishing/Create beach tackle shop (only if missing)")]
        public static void Generate()
        {
            try
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                bool replace=Application.isBatchMode && FishingPrototype.HasArgument("-replace-generated-shop");
                var existing=Object.FindObjectOfType<FishingTackleShop>();
                if (existing != null && !replace) throw new InvalidOperationException("Shop already exists. Edit the existing prefab/instance instead of overwriting it.");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && !replace) throw new InvalidOperationException("Shop prefab already exists; reuse it rather than regenerating custom modelling.");
                if(existing!=null)Object.DestroyImmediate(existing.gameObject);
                var c = Object.FindObjectOfType<FishingPrototype>();
                Physics.SyncTransforms();
                FindBeach(c.harbor, out var position, out var rotation, out var pierLength);
                Directory.CreateDirectory(Folder + "/Materials"); AssetDatabase.Refresh();
                _wood = Mat("01 Salt-weathered cedar", new Color(.37f,.23f,.13f));
                _pale = Mat("02 Warm sand painted boards", new Color(.88f,.81f,.63f));
                _teal = Mat("03 Sea green joinery", new Color(.075f,.32f,.30f));
                _roof = Mat("04 Deep teal standing seam", new Color(.045f,.16f,.19f), .35f);
                _metal = Mat("05 Graphite hardware", new Color(.055f,.075f,.085f), .45f);
                _gold = Mat("06 Brass and amber", new Color(.94f,.57f,.16f), .4f);
                _glass = Mat("07 Sea glass", new Color(.35f,.68f,.73f,.16f), .65f);
                _glass.SetFloat("_Surface",1); _glass.SetFloat("_SrcBlend",5); _glass.SetFloat("_DstBlend",10); _glass.SetFloat("_ZWrite",0);
                _glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); _glass.renderQueue=3000;
                _glass.SetOverrideTag("RenderType","Transparent");_glass.SetShaderPassEnabled("ShadowCaster",false);
                _cloth = Mat("08 Warm canvas", new Color(.96f,.90f,.74f));
                _red = Mat("09 Buoy vermilion", new Color(.77f,.22f,.12f));
                _root = new GameObject("Beach Tackle Shop - Tide and Tackle").transform;
                var shop = _root.gameObject.AddComponent<FishingTackleShop>();
                BuildShell(); BuildInterior(shop); BuildPorch(position.y, pierLength, shop);
                shop.entrance = Marker("Entry - 3.2m clear doorway", new Vector3(0,0,-4.7f));
                shop.purchasePoint = Marker("Purchase interaction - customer side", new Vector3(0,0,1.15f));
                var model = PrefabUtility.SaveAsPrefabAsset(_root.gameObject, PrefabPath);
                Object.DestroyImmediate(_root.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                instance.transform.SetPositionAndRotation(position, rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[BEACH-SHOP] PASS: editable shop prefab placed at " + position + " yaw=" + rotation.eulerAngles.y + " pier=" + pierLength + "m. NPC markers only.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
                else Selection.activeGameObject=instance;
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }

        private static void FindBeach(Vector3 harbor, out Vector3 position, out Quaternion rotation, out float pierLength)
        {
            position=default;rotation=Quaternion.identity;pierLength=0;float best=float.MaxValue;
            var terrains=Object.FindObjectsOfType<Terrain>();
            foreach(var terrain in terrains)
            {
                var data=terrain.terrainData;if(data==null)continue;
                for(float x=8;x<data.size.x-8;x+=4)for(float z=8;z<data.size.z-8;z+=4)
                {
                    var center=terrain.transform.position+new Vector3(x,0,z);
                    float h=GroundHeight(center,terrains);
                    if(h<.6f || h>5f || FishingPrototype.FlatDistance(center,harbor)>500)continue;
                    for(int angle=0;angle<360;angle+=30)
                    {
                        var q=Quaternion.Euler(0,angle,0);float min=100,max=-100;
                        for(int a=-1;a<=1;a++)for(int b=-1;b<=1;b++)
                        {float y=GroundHeight(center+q*new Vector3(a*6.5f,0,b*5.7f),terrains);min=Mathf.Min(min,y);max=Mathf.Max(max,y);}
                        if(min<.35f || max-min>1.8f || max>5.5f)continue;
                        float length=0;
                        // A visible, continuous ramp reaches a small landing deck just offshore.
                        for(float d=16;d<=38;d+=2)
                        {
                            var end=center+q*new Vector3(0,0,-d);
                            if(GroundHeight(end,terrains)<-.6f && GroundHeight(end+q*new Vector3(0,0,-4),terrains)<-1f){length=d;break;}
                        }
                        if(length==0)continue;
                        float floor=max+.13f;bool clear=true;
                        for(float d=6;d<=length;d+=1)
                        {
                            float deck=Mathf.Lerp(floor,.75f,(d-6)/(length-6));
                            var step=center+q*new Vector3(0,0,-d);
                            for(int side=-1;side<=1;side++)if(GroundHeight(step+q*Vector3.right*side*1.65f,terrains)>deck-.06f){clear=false;break;}
                            if(!clear)break;
                            var pathHits=Physics.OverlapBox(new Vector3(step.x,deck+1,step.z),new Vector3(1.65f,.92f,.6f),q,~(1<<4),QueryTriggerInteraction.Ignore);
                            foreach(var hit in pathHits)if(!(hit is TerrainCollider) && hit.GetComponentInParent<WaterSystem.Water>()==null){clear=false;break;}
                            if(!clear)break;
                        }
                        if(!clear)continue;
                        var approach=center+q*new Vector3(0,0,-length-6);approach.y=1;
                        foreach(var hit in Physics.OverlapBox(approach,new Vector3(2,1,3),q,~(1<<4),QueryTriggerInteraction.Ignore))
                            if(hit.GetComponentInParent<WaterSystem.Water>()==null){clear=false;break;}
                        if(!clear)continue;
                        var hits=Physics.OverlapBox(new Vector3(center.x,floor+1.5f,center.z),new Vector3(6.6f,1.4f,6),q,~(1<<4),QueryTriggerInteraction.Ignore);
                        foreach(var hit in hits)if(!(hit is TerrainCollider) && hit.GetComponentInParent<WaterSystem.Water>()==null){clear=false;break;}
                        if(!clear)continue;
                        float score=FishingPrototype.FlatDistance(center,harbor)+length*.6f+(max-min)*15f;
                        if(score>=best)continue;
                        best=score;position=new Vector3(center.x,floor,center.z);rotation=q;pierLength=length;
                    }
                }
            }
            if(best==float.MaxValue)throw new Exception("No safe gently sloping beach site found; no terrain was changed.");
        }
        private static float GroundHeight(Vector3 p,Terrain[] terrains)
        {
            float height=-100f;
            foreach(var t in terrains)
            {
                var v=p-t.transform.position;var s=t.terrainData.size;
                if(v.x>=0 && v.z>=0 && v.x<=s.x && v.z<=s.z){height=t.SampleHeight(p)+t.transform.position.y;break;}
            }
            // The island also has large sculpted cliff meshes above its heightmap.
            foreach(var hit in Physics.RaycastAll(new Vector3(p.x,150,p.z),Vector3.down,250,~(1<<4),QueryTriggerInteraction.Ignore))
                if(hit.collider.GetComponentInParent<WaterSystem.Water>()==null)height=Mathf.Max(height,hit.point.y);
            return height;
        }
        private static void BuildShell()
        {
            var shell=Group("01 Architecture - editable shell");
            Box("Continuous walkable floor",shell,new Vector3(0,-.15f,0),new Vector3(12,.3f,10),_wood,true);
            foreach(float x in new[]{-5.65f,0,5.65f})foreach(float z in new[]{-4.6f,0,4.6f})
                Pole("Cedar foundation pile",shell,new Vector3(x,-4,z),new Vector3(x,-.14f,z),.32f,_wood);
            for(int i=0;i<30;i++)Box("Cedar floorboard",shell,new Vector3(-5.8f+i*.4f,.015f,0),new Vector3(.375f,.025f,9.96f),i%4==0?_wood:_pale);
            Box("Rear wall",shell,new Vector3(0,1.8f,5),new Vector3(12.2f,3.6f,.22f),_pale,true);
            foreach(float side in new[]{-1f,1f})
            {
                // Tall side windows are openings in the wall with transparent collision panes.
                Box("Side wainscot",shell,new Vector3(side*6,.55f,0),new Vector3(.22f,1.1f,10),_teal,true);
                Box("Side lintel",shell,new Vector3(side*6,3.25f,0),new Vector3(.22f,.7f,10),_pale,true);
                foreach(float z in new[]{-3.5f,3.5f})Box("Side wall bay",shell,new Vector3(side*6,2,z),new Vector3(.22f,1.8f,3),_pale,true);
                Box("Side window",shell,new Vector3(side*6,2,0),new Vector3(.06f,1.8f,4),_glass,true);
                for(int z=-2;z<=2;z+=2)Box("Side window mullion",shell,new Vector3(side*6,2,z),new Vector3(.26f,1.86f,.08f),_teal);
                Box("Side window sill",shell,new Vector3(side*6,1.13f,0),new Vector3(.45f,.13f,4.3f),_wood);
                Box("Front lower bay",shell,new Vector3(side*3.85f,.55f,-5),new Vector3(4.3f,1.1f,.22f),_teal,true);
                Box("Front upper bay",shell,new Vector3(side*3.85f,3.25f,-5),new Vector3(4.3f,.7f,.22f),_pale,true);
                Box("Front display window",shell,new Vector3(side*3.85f,2,-5),new Vector3(4.3f,1.8f,.06f),_glass,true);
                foreach(float x in new[]{1.65f,3.85f,6f})Box("Front frame",shell,new Vector3(side*x,1.8f,-5.06f),new Vector3(.14f,3.6f,.27f),_wood,true);
                Box("Window ledge",shell,new Vector3(side*3.85f,1.12f,-5.1f),new Vector3(4.35f,.12f,.45f),_wood);
                // Plank cladding creates readable horizontal relief at close range.
                for(int row=0;row<12;row++)Box("Rear painted lap board",shell,new Vector3(side*3, .14f+row*.29f,5.13f),new Vector3(5.94f,.265f,.045f),row<4?_teal:_pale);
            }
            Box("Door header - clear height 2.9m",shell,new Vector3(0,3.25f,-5),new Vector3(3.2f,.7f,.22f),_teal,true);
            Box("Entry threshold",shell,new Vector3(0,.025f,-5),new Vector3(3.15f,.05f,.3f),_wood,true);
            // Individual standing-seam roof strips; open rafter underside is visible indoors.
            float slope=Mathf.Atan2(1.8f,5.7f)*Mathf.Rad2Deg;
            for(int i=0;i<25;i++)foreach(float side in new[]{-1f,1f})
            {
                var roof=Box("Standing seam roof panel",shell,new Vector3(-6.24f+i*.52f,4.5f,side*2.85f),new Vector3(.515f,.16f,5.98f),_roof);
                roof.localRotation=Quaternion.Euler(side*slope,0,0);
                var seam=Box("Raised metal roof seam",shell,new Vector3(-6.47f+i*.52f,4.59f,side*2.85f),new Vector3(.025f,.08f,5.98f),_teal);
                seam.localRotation=roof.localRotation;
            }
            Box("Ridge cap",shell,new Vector3(0,5.48f,0),new Vector3(13.05f,.12f,.22f),_gold);
            for(int x=-5;x<=5;x+=2)
            {
                Pole("Exposed roof truss",shell,new Vector3(x,3.5f,-5),new Vector3(x,3.5f,5),.09f,_wood);
                Pole("Rafter",shell,new Vector3(x,3.55f,-5.5f),new Vector3(x,5.25f,0),.08f,_wood);
                Pole("Rafter",shell,new Vector3(x,5.25f,0),new Vector3(x,3.55f,5.5f),.08f,_wood);
            }
            // End gables are saved mesh assets, so the prefab remains independently reusable.
            var gable=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Gable.asset");bool newGable=gable==null;
            if(newGable)gable=new Mesh {name="Shop gable infill"};
            gable.vertices=new[]{new Vector3(0,3.6f,-5),new Vector3(0,5.25f,0),new Vector3(0,3.6f,5),new Vector3(0,3.6f,-5),new Vector3(0,5.25f,0),new Vector3(0,3.6f,5)};
            gable.triangles=new[]{0,1,2,5,4,3};gable.RecalculateNormals();if(newGable)AssetDatabase.CreateAsset(gable,Folder+"/Gable.asset");else EditorUtility.SetDirty(gable);
            foreach(float side in new[]{-1f,1f})
            {
                var end=new GameObject("Gable end",typeof(MeshFilter),typeof(MeshRenderer));end.transform.SetParent(shell,false);end.transform.localPosition=Vector3.right*side*6;
                end.GetComponent<MeshFilter>().sharedMesh=gable;end.GetComponent<MeshRenderer>().sharedMaterial=_teal;
            }
            Box("Main shop sign backing",shell,new Vector3(0,3.37f,-5.31f),new Vector3(5.9f,.85f,.16f),_wood);
            Box("Main shop sign inset",shell,new Vector3(0,3.37f,-5.405f),new Vector3(5.65f,.64f,.025f),_teal);
            Text("TIDE & TACKLE",shell,new Vector3(0,3.38f,-5.435f),.42f,_cloth.color);
            foreach(float x in new[]{-2.7f,2.7f})foreach(float y in new[]{3.12f,3.64f})Sphere("Brass sign fixing",shell,new Vector3(x,y,-5.44f),Vector3.one*.065f,_gold);
            Text("RODS / REELS / FIELD GOODS",shell,new Vector3(0,2.97f,-5.20f),.095f,_cloth.color);
        }
        private static void BuildInterior(FishingTackleShop shop)
        {
            var goods=Group("02 Interior - fixtures and tackle displays");
            Box("Service counter",goods,new Vector3(0,.54f,2.75f),new Vector3(6,1.08f,1.0f),_teal,true);
            Box("Countertop",goods,new Vector3(0,1.15f,2.72f),new Vector3(6.3f,.14f,1.2f),_wood,true);
            for(int i=0;i<16;i++)Box("Counter fluting",goods,new Vector3(-2.84f+i*.38f,.54f,2.225f),new Vector3(.04f,1.0f,.045f),_gold);
            Box("Counter kick board",goods,new Vector3(0,.1f,2.21f),new Vector3(6,.14f,.07f),_metal);
            Box("Cash register base",goods,new Vector3(1.9f,1.32f,2.78f),new Vector3(.65f,.2f,.48f),_metal);
            var screen=Box("Register screen",goods,new Vector3(1.9f,1.65f,2.87f),new Vector3(.48f,.35f,.07f),_teal);screen.localRotation=Quaternion.Euler(-15,0,0);
            Text("EQUIPMENT / UPGRADES",goods,new Vector3(0,2.6f,4.82f),.18f,_teal.color);
            Text("TIDE & TACKLE",goods,new Vector3(0,3.03f,4.82f),.28f,_wood.color);
            // Six upright rods on a rack, all merchandise silhouettes rather than NPC substitutes.
            Box("Rod rack collision",goods,new Vector3(-4.9f,1.25f,-.7f),new Vector3(1.0f,2.5f,4.0f),_wood,true);
            for(int i=0;i<6;i++)
            {
                float z=-2.3f+i*.61f;
                Pole("Display rod blank",goods,new Vector3(-4.28f,.27f,z),new Vector3(-4.19f,2.85f,z+.1f),.024f,_metal);
                Pole("Cork rod handle",goods,new Vector3(-4.28f,.27f,z),new Vector3(-4.26f,.85f,z+.025f),.06f,_pale);
                Cylinder("Rod reel",goods,new Vector3(-4.13f,.84f,z),new Vector3(.19f,.09f,.19f),_gold,new Vector3(0,0,90));
                for(int ring=0;ring<4;ring++)Sphere("Rod line guide",goods,new Vector3(-4.13f,1.1f+ring*.5f,z+.05f),new Vector3(.055f,.055f,.055f),_gold);
            }
            // Right wall shelving has substantial collision; small stock is decorative.
            Shelf(goods,new Vector3(5.05f,0,-.6f));
            for(int row=0;row<3;row++)for(int i=0;i<5;i++)
            {
                float z=-2.25f+i*.78f,y=.62f+row*.7f;
                if(row==0) {Box("Packaged line",goods,new Vector3(4.66f,y,z),new Vector3(.4f,.4f,.48f),i%2==0?_teal:_red);Cylinder("Line spool",goods,new Vector3(4.43f,y,z),new Vector3(.24f,.03f,.24f),_cloth,new Vector3(0,0,90));}
                else {Cylinder("Reel spool",goods,new Vector3(4.72f,y,z),new Vector3(.35f,.18f,.35f),_gold,new Vector3(0,0,90));Pole("Reel crank",goods,new Vector3(4.48f,y,z),new Vector3(4.42f,y-.16f,z+.1f),.022f,_metal);}
            }
            Box("Fish box display plinth",goods,new Vector3(3.8f,.16f,3.4f),new Vector3(1.35f,.32f,1.6f),_wood,true);
            for(int i=0;i<2;i++)
            {
                Box("Insulated fish box",goods,new Vector3(3.8f,.58f+i*.68f,3.4f),new Vector3(1.2f,.58f,1.4f),_cloth,true);
                Box("Fish box lid",goods,new Vector3(3.8f,.91f+i*.68f,3.4f),new Vector3(1.26f,.1f,1.46f),_teal);
                foreach(float side in new[]{-1f,1f})Box("Crate handle",goods,new Vector3(3.8f+side*.64f,.62f+i*.68f,3.4f),new Vector3(.08f,.12f,.38f),_metal);
            }
            // Front window displays: low plinths keep the middle entrance and customer floor open.
            foreach(float side in new[]{-1f,1f})
            {
                Box("Window display bench",goods,new Vector3(side*3.8f,.39f,-3.85f),new Vector3(2.6f,.78f,.85f),_pale,true);
                for(int i=0;i<3;i++) {Sphere("Painted fishing float",goods,new Vector3(side*3.8f+(i-1)*.48f,1.05f,-3.85f),new Vector3(.28f,.46f,.28f),i%2==0?_red:_teal);Pole("Float stem",goods,new Vector3(side*3.8f+(i-1)*.48f,1.15f,-3.85f),new Vector3(side*3.8f+(i-1)*.48f,1.5f,-3.85f),.018f,_gold);}
            }
            // Labels are inward-facing; text is in English on props, Chinese on the purchase UI.
            Text("RODS",goods,new Vector3(-4.2f,2.6f,-2.82f),.16f,_cloth.color);
            Text("REELS + LINE",goods,new Vector3(4.55f,2.62f,-2.82f),.12f,_teal.color);
            for(int i=-1;i<=1;i++)
            {
                Pole("Pendant cord",goods,new Vector3(i*3.1f,4.5f,.3f),new Vector3(i*3.1f,3.0f,.3f),.015f,_metal);
                Cylinder("Pendant shade",goods,new Vector3(i*3.1f,2.97f,.3f),new Vector3(.7f,.09f,.7f),_teal);
                Sphere("Warm glass diffuser",goods,new Vector3(i*3.1f,2.83f,.3f),new Vector3(.4f,.15f,.4f),_cloth);
                if(i!=0)
                {
                    var lamp=new GameObject("Warm interior lamp",typeof(Light));lamp.transform.SetParent(goods,false);lamp.transform.localPosition=new Vector3(i*3.1f,2.7f,.3f);
                    var light=lamp.GetComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.84f,.61f);light.range=7f;light.intensity=1.4f;light.shadows=LightShadows.None;
                }
            }
            var npc=Group("03 Reserved NPC spaces - empty markers only");
            shop.clerkSpaces=new[]{Marker("Clerk standing area 1 - 1.4m square",new Vector3(-1.4f,0,4),npc),Marker("Clerk standing area 2 - 1.4m square",new Vector3(1.1f,0,4),npc)};
            shop.customerSpaces=new[]{Marker("Customer queue 1 - 1.2m square",new Vector3(0,0,1),npc),Marker("Customer queue 2 - 1.2m square",new Vector3(0,0,-.6f),npc),Marker("Customer browse left - 1.2m square",new Vector3(-2.65f,0,-.9f),npc),Marker("Customer browse right - 1.2m square",new Vector3(2.65f,0,-.9f),npc)};
        }
        private static void BuildPorch(float floorY,float length,FishingTackleShop shop)
        {
            var porch=Group("04 Porch and beach access");
            Box("Front porch floor",porch,new Vector3(0,-.15f,-6),new Vector3(12,.3f,2),_wood,true);
            for(int i=0;i<30;i++)Box("Porch decking",porch,new Vector3(-5.8f+i*.4f,.015f,-6),new Vector3(.375f,.025f,1.95f),_pale);
            foreach(float side in new[]{-1f,1f})
            {
                Pole("Porch post",porch,new Vector3(side*5.6f,-1,-6.65f),new Vector3(side*5.6f,3.1f,-6.65f),.11f,_wood);
                var awning=Box("Canvas awning",porch,new Vector3(side*3.0f,2.92f,-5.95f),new Vector3(6.0f,.07f,2.15f),_cloth);awning.localRotation=Quaternion.Euler(-9,0,0);
                for(int i=0;i<6;i++){var stripe=Box("Awning teal stripe",porch,new Vector3(side*3f+(i-2.5f)*.92f,2.96f,-5.95f),new Vector3(.42f,.016f,2.15f),_teal);stripe.localRotation=awning.localRotation;}
                Box("Porch bench seat",porch,new Vector3(side*4,.44f,-6),new Vector3(2.1f,.12f,.55f),_wood,true);
                foreach(float x in new[]{-.8f,.8f})Box("Bench leg",porch,new Vector3(side*4+x,.22f,-6),new Vector3(.12f,.44f,.45f),_teal,true);
            }
            float low=.75f-floorY;
            var start=new Vector3(0,0,-7);var end=new Vector3(0,low,-length);
            var delta=end-start;float angle=Mathf.Atan2(delta.y,-delta.z)*Mathf.Rad2Deg;
            var ramp=Box("Continuous accessible boardwalk collision",porch,(start+end)*.5f-Vector3.up*.12f,new Vector3(3.2f,.24f,delta.magnitude+.12f),_wood,true);ramp.localRotation=Quaternion.Euler(angle,0,0);
            int boards=Mathf.CeilToInt(delta.magnitude/.4f);
            for(int i=0;i<=boards;i++)
            {
                var p=Vector3.Lerp(start,end,i/(float)boards);
                var plank=Box("Access ramp plank",porch,p+Vector3.up*.015f,new Vector3(3.18f,.025f,.37f),i%4==0?_wood:_pale);plank.localRotation=ramp.localRotation;
            }
            // Low unobtrusive rails leave a clear 2.8 m pedestrian route.
            for(int i=0;i<=4;i++)foreach(float side in new[]{-1f,1f})
            {
                var p=Vector3.Lerp(start,end,i/4f)+Vector3.right*side*1.55f;
                Pole("Boardwalk pile",porch,p-Vector3.up*3,p+Vector3.up*.9f,.08f,_wood);
            }
            foreach(float side in new[]{-1f,1f})Pole("Boardwalk handrail",porch,start+new Vector3(side*1.55f,.9f,0),end+new Vector3(side*1.55f,.9f,0),.035f,_teal);
            Box("Shore landing deck",porch,new Vector3(0,low-.15f,-length-1.5f),new Vector3(5,.3f,3.4f),_wood,true);
            for(int i=0;i<12;i++)Box("Landing deck planks",porch,new Vector3(-2.28f+i*.415f,low+.015f,-length-1.5f),new Vector3(.39f,.025f,3.35f),_pale);
            foreach(float side in new[]{-1f,1f})
            {
                Pole("Mooring bollard",porch,new Vector3(side*2.1f,low-.7f,-length-2.7f),new Vector3(side*2.1f,low+.8f,-length-2.7f),.13f,_teal);
                Cylinder("Mooring brass cap",porch,new Vector3(side*2.1f,low+.85f,-length-2.7f),new Vector3(.33f,.06f,.33f),_gold);
            }
            shop.mooringPoint=Marker("Boat approach - offshore",new Vector3(0,1-floorY,-length-6));
        }
        private static void Shelf(Transform parent,Vector3 p)
        {
            Box("Tackle shelf backing",parent,p+Vector3.up*1.25f,new Vector3(.2f,2.5f,4.3f),_teal,true);
            foreach(float z in new[]{-2.1f,2.1f})Box("Shelf upright",parent,p+new Vector3(-.35f,1.25f,z),new Vector3(.85f,2.5f,.1f),_wood,true);
            for(int i=0;i<4;i++)Box("Shelf board",parent,p+new Vector3(-.3f,.36f+i*.7f,0),new Vector3(.9f,.08f,4.3f),_pale,true);
        }
        private static Material Mat(string name,Color color,float smooth=.15f)
        {
            var path=Folder+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);bool isNew=material==null;
            if(isNew)material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smooth);if(isNew)AssetDatabase.CreateAsset(material,path);else EditorUtility.SetDirty(material);return material;
        }
        private static Transform Group(string name){var t=new GameObject(name).transform;t.SetParent(_root,false);return t;}
        private static Transform Marker(string name,Vector3 p,Transform parent=null){var t=new GameObject(name).transform;t.SetParent(parent==null?_root:parent,false);t.localPosition=p;return t;}
        private static Transform Box(string name,Transform parent,Vector3 p,Vector3 size,Material material,bool solid=false)=>Shape(name,PrimitiveType.Cube,parent,p,size,material,solid);
        private static Transform Sphere(string name,Transform parent,Vector3 p,Vector3 size,Material material)=>Shape(name,PrimitiveType.Sphere,parent,p,size,material,false);
        private static Transform Cylinder(string name,Transform parent,Vector3 p,Vector3 size,Material material,Vector3 rotation=default){var t=Shape(name,PrimitiveType.Cylinder,parent,p,size,material,false);t.localRotation=Quaternion.Euler(rotation);return t;}
        private static Transform Shape(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 size,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;
        }
        private static void Pole(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material mat)
        {var t=Cylinder(name,parent,(a+b)*.5f,new Vector3(radius,Vector3.Distance(a,b)*.5f,radius),mat);t.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);}
        private static void Text(string value,Transform parent,Vector3 p,float size,Color color)
        {
            var go=new GameObject(value,typeof(TextMeshPro));go.transform.SetParent(parent,false);go.transform.localPosition=p;
            var text=go.GetComponent<TextMeshPro>();text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if(text.font==null)throw new Exception("Shop signs require the project's LiberationSans SDF font.");
            text.text=value;text.fontSize=size*10f;text.alignment=TextAlignmentOptions.Center;text.color=color;
            text.rectTransform.sizeDelta=new Vector2(7,.7f);text.enableWordWrapping=false;
        }
    }
}
