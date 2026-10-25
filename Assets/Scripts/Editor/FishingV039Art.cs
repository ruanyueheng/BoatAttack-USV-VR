using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.EditorTools
{
    public static class FishingV039Art
    {
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            string[] names={"HomeLandscaping","SanctuaryWind","SanctuaryGrove","SanctuaryDrydock","SanctuaryStars","WindBridge0","WindBridge1","WindBridge2","RootBridge","Galleon","Windmill","AncientTree","ObservatoryExterior"};
            foreach(string name in names)
            {
                string folder="Assets/Resources/FishingV035/",path=folder+name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.importAnimation=false;importer.SaveAndReimport();
                var filters=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>();
                if(filters.Length!=1)throw new InvalidDataException("Expected one scenery mesh: "+name);
                var source=filters[0].sharedMesh;string collisionPath=folder+name+"Collision.asset";
                var collision=AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);bool create=collision==null;
                if(create)collision=new Mesh{name=name+" solid surfaces"};else collision.Clear();
                collision.indexFormat=IndexFormat.UInt32;collision.vertices=source.vertices;collision.triangles=source.GetTriangles(0);collision.RecalculateBounds();
                if(create)AssetDatabase.CreateAsset(collision,collisionPath);else EditorUtility.SetDirty(collision);
                Debug.Log("[V039-ART] Solid geometry refreshed: "+name+" / "+collision.triangles.Length/3+" triangles");
            }
            AssetDatabase.SaveAssets();
            const string roast="Assets/Resources/FishingV039/";
            var fishImporter=(ModelImporter)AssetImporter.GetAtPath(roast+"GrilledFish.fbx");
            fishImporter.materialImportMode=ModelImporterMaterialImportMode.None;fishImporter.importAnimation=false;fishImporter.isReadable=true;fishImporter.SaveAndReimport();
            var texture=(TextureImporter)AssetImporter.GetAtPath(roast+"GrilledFishAlbedo.png");texture.maxTextureSize=2048;texture.mipmapEnabled=true;texture.SaveAndReimport();
            var material=AssetDatabase.LoadAssetAtPath<Material>(roast+"GrilledFish.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,roast+"GrilledFish.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(roast+"GrilledFishAlbedo.png"));material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.32f);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
        }
    }
}
