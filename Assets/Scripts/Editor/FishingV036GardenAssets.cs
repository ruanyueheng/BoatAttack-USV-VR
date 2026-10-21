using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.EditorTools
{
    public static class FishingV036GardenAssets
    {
        public static void Install()
        {
            foreach(string name in new[]{"HomeLand","GardenFixtures"})
            {
                string path="Assets/Resources/FishingV035/"+name+".fbx";
                var import=(ModelImporter)AssetImporter.GetAtPath(path);import.materialImportMode=ModelImporterMaterialImportMode.None;
                import.importAnimation=false;import.isReadable=true;import.SaveAndReimport();
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    string target="Assets/Resources/FishingV035/"+name+"Collision.asset";
                    var collision=AssetDatabase.LoadAssetAtPath<Mesh>(target);bool create=collision==null;
                    if(create)collision=new Mesh{indexFormat=IndexFormat.UInt32};else collision.Clear();
                    collision.vertices=filter.sharedMesh.vertices;collision.triangles=filter.sharedMesh.GetTriangles(0);collision.RecalculateBounds();
                    if(create)AssetDatabase.CreateAsset(collision,target);else EditorUtility.SetDirty(collision);
                }
            }
            const string folder="Assets/Resources/FishingV036/";
            var importer=(ModelImporter)AssetImporter.GetAtPath(folder+"StarlightWateringCan.fbx");
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation=false;importer.bakeAxisConversion=true;importer.isReadable=true;importer.SaveAndReimport();
            var ivory=Material("CanIvory",new Color(.90f,.86f,.74f),0,.62f);
            var gold=Material("CanGold",new Color(.66f,.48f,.21f),1,.64f);
            var sage=Material("CanSage",new Color(.19f,.37f,.34f),0,.55f);
            var inner=Material("CanInterior",new Color(.34f,.45f,.40f),0,.55f);
            var can=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"StarlightWateringCan.fbx"));
            try
            {
                int triangles=0;
                foreach(var renderer in can.GetComponentsInChildren<Renderer>())
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++){string key=materials[i].name;materials[i]=key.Contains("gold")?gold:key.Contains("sage")?sage:key.Contains("inner")?inner:ivory;}
                    renderer.sharedMaterials=materials;
                }
                foreach(var filter in can.GetComponentsInChildren<MeshFilter>())triangles+=filter.sharedMesh.triangles.Length/3;
                if(triangles<1000 || triangles>20000)throw new InvalidOperationException("Invalid original watering-can export");
                PrefabUtility.SaveAsPrefabAsset(can,folder+"StarlightWateringCanView.prefab");
                Debug.Log("[V036-ART] CHECK: original ivory/gold/sage watering can "+triangles+" triangles; deep-well ground and shaft collision rebuilt");
            }
            finally{UnityEngine.Object.DestroyImmediate(can);}
            AssetDatabase.SaveAssets();
        }
        private static Material Material(string name,Color color,float metallic,float smoothness)
        {
            string path="Assets/Resources/FishingV036/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.enableInstancing=true;mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smoothness);mat.SetFloat("_Cull",0);EditorUtility.SetDirty(mat);return mat;
        }
    }
}
