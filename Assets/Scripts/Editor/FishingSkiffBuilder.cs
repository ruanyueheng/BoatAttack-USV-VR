using System;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using WaterSystem;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingSkiffBuilder
    {
        [MenuItem("BoatAttack/Fishing/Build solo skiff")]
        public static void Build()
        {
            GameObject root=null;
            try
            {
                AssetDatabase.Refresh();
                foreach(var name in new[]{"SoloSkiff","SkiffHull"})
                    AssetDatabase.ImportAsset("Assets/Resources/FishingBoat/"+name+".fbx",ImportAssetOptions.ForceUpdate);
                root=PrefabUtility.LoadPrefabContents("Assets/Objects/boats/renegade/_Renegade.prefab");
                root.name="Solo fishing skiff";
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))renderer.enabled=false;
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                var model=FishingPropsForBoat(root.transform,"SoloSkiff");
                var hull=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/FishingBoat/SkiffHull.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
                if(hull.bounds.size.x<1.8f || hull.bounds.size.z<3.8f)throw new InvalidOperationException("Skiff FBX metre/axis conversion failed: "+hull.bounds);
                var colliderHull=root.AddComponent<MeshCollider>();colliderHull.sharedMesh=hull;colliderHull.convex=true;
                const string contactPath="Assets/Resources/FishingBoat/SkiffContact.physicMaterial";
                var contact=AssetDatabase.LoadAssetAtPath<PhysicMaterial>(contactPath);
                if(contact==null){contact=new PhysicMaterial("Skiff contact");AssetDatabase.CreateAsset(contact,contactPath);}
                contact.dynamicFriction=contact.staticFriction=0;contact.bounciness=0;contact.frictionCombine=PhysicMaterialCombine.Minimum;
                EditorUtility.SetDirty(contact);colliderHull.sharedMaterial=contact;
                var boat=root.GetComponent<Boat>();boat.boatRenderer=model.GetComponentInChildren<MeshRenderer>();
                var rb=root.GetComponent<Rigidbody>();rb.mass=650;rb.drag=.12f;rb.angularDrag=1;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                // shortcut: constrain roll/pitch for the seated view; revisit if wave-driven tilt becomes gameplay.
                rb.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
                var buoyancy=root.GetComponent<BuoyantObject>();buoyancy.centerOfMass=new Vector3(0,-.5f,-.2f);buoyancy.voxelResolution=.22f;
                boat.engine.transform.localPosition=new Vector3(0,.25f,-1.9f);
                boat.engine.enginePosition=new Vector3(0,-1.20f,0);
                boat.engine.stableHull=true;boat.engine.horsePower=FishingFleet.Power(0);boat.engine.forwardSpeedLimit=FishingFleet.SpeedLimit(0);
                var lamp=new GameObject("Skiff deck night light").AddComponent<Light>();lamp.transform.SetParent(root.transform,false);
                lamp.transform.localPosition=new Vector3(-.6f,1.35f,-.85f);
                lamp.type=LightType.Point;lamp.color=new Color(1,.78f,.48f);lamp.range=5;lamp.intensity=3;lamp.shadows=LightShadows.None;lamp.enabled=false;
                // Keep the shared engine and water effects; size their wake origins to this hull.
                foreach(var wake in root.GetComponentsInChildren<BoatFoamGenerator>())
                    wake.transform.localPosition=new Vector3(0,wake.transform.localPosition.y,wake.transform.localPosition.z>0?1.6f:-1.6f);
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/FishingBoat/SoloSkiffBoat.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("[SKIFF] PASS: solo hull, physical collider, recalculated buoyancy and bounded mesh imported.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
            finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
        }
        private static GameObject FishingPropsForBoat(Transform parent,string name)
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/FishingBoat/"+name+".fbx"),parent);
            model.transform.localPosition=Vector3.zero;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=Resources.Load<Material>("FishingProps/HarborPalette");
            return model;
        }
    }
}
