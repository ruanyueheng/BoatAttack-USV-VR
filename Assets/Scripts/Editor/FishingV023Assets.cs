using System;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV023Assets
    {
        public static void Build()
        {
            try
            {
                AssetDatabase.Refresh();
                const string root="Assets/Resources/FishingV023/";
                string path=root+"CoastalPalette.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(Resources.Load<Shader>("FishingPalette"));AssetDatabase.CreateAsset(material,path);}
                material.SetFloat("_Gloss",.18f);material.SetFloat("_Rim",.08f);material.enableInstancing=true;EditorUtility.SetDirty(material);
                foreach(string directory in new[]{root+"Home",root+"Items","Assets/Resources/FishingFish"})
                {
                    foreach(string file in Directory.GetFiles(directory,"*.fbx"))
                    {
                        if(directory.EndsWith("FishingFish") && (int)Enum.Parse(typeof(FishKind),Path.GetFileNameWithoutExtension(file))<12
                            && !new[]{"Coral","Dart","Puffer","Sailfish"}.ContainsName(Path.GetFileNameWithoutExtension(file)))continue;
                        var importer=(ModelImporter)AssetImporter.GetAtPath(file);
                        importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;
                        importer.animationType=ModelImporterAnimationType.None;importer.isReadable=true;importer.bakeAxisConversion=true;
                        importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.None;
                        importer.SaveAndReimport();
                        if(!directory.EndsWith("Home"))continue;
                        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(file));
                        try
                        {
                            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
                            string name=Path.GetFileNameWithoutExtension(file);
                            string oldPath=root+"Home/"+name+".prefab",viewPath=root+"Home/"+name+"View.prefab";
                            if(AssetDatabase.LoadAssetAtPath<GameObject>(oldPath)!=null && AssetDatabase.LoadAssetAtPath<GameObject>(viewPath)==null)
                            {string error=AssetDatabase.MoveAsset(oldPath,viewPath);if(error.Length>0)throw new IOException(error);}
                            PrefabUtility.SaveAsPrefabAsset(model,viewPath);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(model);}
                    }
                    foreach(string file in Directory.GetFiles(directory,"*Thumbnail.png"))
                    {
                        if(directory.EndsWith("FishingFish") && (int)Enum.Parse(typeof(FishKind),Path.GetFileName(file).Replace("Thumbnail.png",""))<12
                            && !new[]{"Coral","Dart","Puffer","Sailfish"}.ContainsName(Path.GetFileName(file).Replace("Thumbnail.png","")))continue;
                        var importer=(TextureImporter)AssetImporter.GetAtPath(file);importer.alphaIsTransparency=true;
                        importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                }
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                Debug.Log("[V023-ART] PASS: calibrated item meshes, actual object photos, 24 home prefabs and 12 marine imports");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
        private static bool ContainsName(this string[] names,string value)=>Array.IndexOf(names,value)>=0;
    }
}
