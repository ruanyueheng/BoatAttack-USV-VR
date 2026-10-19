using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV034Art
    {
        private const string Root="Assets/Resources/FishingV034/";
        public static void Prepare()
        {
            try
            {
                foreach(string name in new[]{"HandcraftAlbedo","HandcraftNormal","HandcraftMetalSmooth"})
                {
                    string path=Root+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=1024;importer.mipmapEnabled=true;
                    importer.wrapMode=TextureWrapMode.Clamp;importer.sRGBTexture=name=="HandcraftAlbedo";
                    if(name=="HandcraftNormal")importer.textureType=TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                foreach(string name in new[]{"CraftTable","LinenMat","SieveFrame","SieveSand","SieveShell","WinderBody","WinderCrank","WinderCoil","StarCharm"})
                {
                    string path=Root+name+".fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.materialImportMode=ModelImporterMaterialImportMode.None;
                    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.isReadable=false;importer.SaveAndReimport();
                    if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)throw new InvalidDataException("Missing handcraft model "+name);
                }
                var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Handcraft.mat");
                if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Root+"Handcraft.mat");}
                material.name="Harbour handcraft · cedar, petrol paint, brass and flax";material.enableInstancing=true;
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"HandcraftAlbedo.png"));
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"HandcraftNormal.png"));material.SetFloat("_BumpScale",.25f);material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"HandcraftMetalSmooth.png"));material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();Debug.Log("[V034-ART] PASS: nine original UV-mapped models and packed PBR material imported.");EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogError("[V034-ART] FAIL: "+ex);EditorApplication.Exit(1);}
        }
    }
}
