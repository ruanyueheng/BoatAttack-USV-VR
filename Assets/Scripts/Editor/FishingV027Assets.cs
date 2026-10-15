using System;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV027Assets
    {
        public static void Run(){Prepare();FishingV011Validation.Begin("V027");}
        public static void Prepare()
        {
            const string root="Assets/Resources/FishingV027/Home/";
            PlayerSettings.bundleVersion=FishingPrototype.Version;PlayerSettings.Android.bundleVersionCode=28;
            AssetDatabase.Refresh();
            var path=root+"CoastalHomePalette.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Resources.Load<Shader>("FishingPalette"));AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_Gloss",0);material.SetFloat("_Rim",.03f);material.SetFloat("_Unlit",.16f);material.enableInstancing=true;
            foreach(string name in new[]{"IslandGround","IslandDetails","HouseShell"})
            {
                string file=root+name+".fbx";
                var importer=AssetImporter.GetAtPath(file) as ModelImporter;
                if(importer==null)throw new IOException("Missing V027 model: "+file);
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;
                importer.animationType=ModelImporterAnimationType.None;importer.isReadable=true;importer.bakeAxisConversion=true;
                importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.None;importer.SaveAndReimport();
                var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(file));
                try
                {
                    foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
                    if(name!="HouseShell")foreach(var filter in model.GetComponentsInChildren<MeshFilter>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                    PrefabUtility.SaveAsPrefabAsset(model,root+name+"View.prefab");
                }
                finally{Object.DestroyImmediate(model);}
            }
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            Debug.Log("[V027-ART] Imported three metre-scale vertex-colour home models and static collision meshes.");
        }
    }
}
