using System;
using UnityEditor;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingHuntingAssets
    {
        public static void Build()
        {
            const string folder="Assets/Resources/FishingHunting/";
            try
            {
                AssetDatabase.Refresh();
                var importer=(ModelImporter)AssetImporter.GetAtPath(folder+"Rifle.fbx");
                importer.importAnimation=false;importer.addCollider=false;importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                var textureImporter=(TextureImporter)AssetImporter.GetAtPath(folder+"Diffuse.png");
                textureImporter.sRGBTexture=true;textureImporter.mipmapEnabled=true;textureImporter.maxTextureSize=2048;textureImporter.SaveAndReimport();
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
                var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Rifle.mat");
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,folder+"Rifle.mat");}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Diffuse.png"));material.SetFloat("_Smoothness",.22f);material.SetFloat("_Metallic",.15f);EditorUtility.SetDirty(material);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Rifle.fbx");
                var root=UnityEngine.Object.Instantiate(model);root.name="Hunting Rifle";
                try
                {
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
                    PrefabUtility.SaveAsPrefabAsset(root,folder+"HuntingRifle.prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                AssetDatabase.SaveAssets();Debug.Log("[HUNTING-ASSETS] PASS: supplied mesh, diffuse texture and URP prefab imported.");
                EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
