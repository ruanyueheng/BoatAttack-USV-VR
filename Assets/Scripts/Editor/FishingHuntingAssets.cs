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
                ImportTexture(folder+"Diffuse.png",false,true);
                ImportTexture(folder+"RifleNormal.png",true,false);
                ImportTexture(folder+"RifleMetallicSmoothness.png",false,false);
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
                var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Rifle.mat");
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,folder+"Rifle.mat");}
                material.shader=shader;
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Diffuse.png"));
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"RifleNormal.png"));
                material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"RifleMetallicSmoothness.png"));
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",1);material.SetFloat("_Metallic",1);
                material.SetFloat("_BumpScale",1);material.SetFloat("_SmoothnessTextureChannel",0);
                material.SetFloat("_Cull",0);material.doubleSidedGI=true;
                material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");EditorUtility.SetDirty(material);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Rifle.fbx");
                var root=UnityEngine.Object.Instantiate(model);root.name="Hunting Rifle";
                try
                {
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
                    PrefabUtility.SaveAsPrefabAsset(root,folder+"HuntingRifle.prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                AssetDatabase.SaveAssets();Debug.Log("[HUNTING-ASSETS] PASS: new supplied AK mesh, three PBR textures and URP prefab imported.");
                EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        private static void ImportTexture(string path,bool normal,bool color)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Missing rifle texture: "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=color;importer.mipmapEnabled=true;importer.maxTextureSize=2048;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }
    }
}
