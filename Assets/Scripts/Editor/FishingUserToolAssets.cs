using System;
using UnityEditor;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingUserToolAssets
    {
        [MenuItem("BoatAttack/Fishing/Import Supplied Pickaxe")]
        public static void Build()
        {
            try
            {
                const string folder="Assets/Resources/FishingUserTools/";AssetDatabase.Refresh();
                var model=(ModelImporter)AssetImporter.GetAtPath(folder+"Pickaxe.fbx");
                model.materialImportMode=ModelImporterMaterialImportMode.None;model.importAnimation=false;model.animationType=ModelImporterAnimationType.None;
                model.bakeAxisConversion=true;model.isReadable=true;model.importNormals=ModelImporterNormals.Import;model.importTangents=ModelImporterTangents.CalculateMikk;model.addCollider=false;model.SaveAndReimport();
                foreach(string name in new[]{"PickaxeAlbedo","PickaxeMetallicSmoothness","PickaxeNormal","PickaxeThumbnail"})
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(folder+name+".png");
                    importer.textureType=name=="PickaxeNormal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                    importer.sRGBTexture=name=="PickaxeAlbedo" || name=="PickaxeThumbnail";
                    importer.isReadable=name=="PickaxeAlbedo";
                    importer.alphaIsTransparency=name=="PickaxeThumbnail";importer.mipmapEnabled=name!="PickaxeThumbnail";
                    importer.maxTextureSize=name=="PickaxeThumbnail"?256:1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                }
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
                var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Pickaxe.mat");
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,folder+"Pickaxe.mat");}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"PickaxeAlbedo.png"));material.SetColor("_BaseColor",Color.white);
                material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"PickaxeMetallicSmoothness.png"));material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"PickaxeNormal.png"));material.SetFloat("_BumpScale",.55f);material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_Smoothness",.6f);EditorUtility.SetDirty(material);
                var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Pickaxe.fbx"));
                try
                {
                    source.name="Supplied hand pickaxe";
                    foreach(var renderer in source.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
                    PrefabUtility.SaveAsPrefabAsset(source,folder+"UserPickaxe.prefab");
                    var bounds=source.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                    if(Mathf.Abs(bounds.size.y-.58f)>.01f || bounds.size.z>.07f)throw new InvalidOperationException("Supplied pickaxe metre axes differ from recorded source export: "+bounds);
                    Debug.Log("[USER-TOOLS] BOUNDS: "+bounds);
                }
                finally{UnityEngine.Object.DestroyImmediate(source);}
                AssetDatabase.SaveAssets();Debug.Log("[USER-TOOLS] PASS: supplied pick head/wood handle, atlas maps and calibrated prefab.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
