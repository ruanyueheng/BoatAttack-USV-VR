using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.EditorTools
{
    public static class FishingV036LanternAssets
    {
        private const string Folder="Assets/Resources/FishingV036/";
        public static void Install()
        {
            string modelPath=Folder+"HangingLantern.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            if(importer==null)throw new InvalidOperationException("Missing downloaded hanging lantern export");
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;importer.isReadable=true;importer.bakeAxisConversion=true;importer.SaveAndReimport();
            foreach(string name in new[]{"LanternAlbedo","LanternMetallicSmoothness","LanternNormal","LanternEmission"})
            {
                var texture=(TextureImporter)AssetImporter.GetAtPath(Folder+"LanternTextures/"+name+".png");
                texture.maxTextureSize=1024;texture.mipmapEnabled=true;texture.sRGBTexture=name=="LanternAlbedo" || name=="LanternEmission";
                texture.alphaSource=TextureImporterAlphaSource.FromInput;
                if(name=="LanternNormal")texture.textureType=TextureImporterType.NormalMap;
                texture.SaveAndReimport();
            }
            var brass=Material("LanternBrass",false,false);var glass=Material("LanternGlass",true,false);var flame=Material("LanternFlame",true,true);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);var root=UnityEngine.Object.Instantiate(source);
            try
            {
                int triangles=0;foreach(var filter in root.GetComponentsInChildren<MeshFilter>())triangles+=filter.sharedMesh.triangles.Length/3;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++){string name=materials[i].name;materials[i]=name.Contains("flame")?flame:name.Contains("glass")?glass:brass;}
                    renderer.sharedMaterials=materials;
                }
                if(triangles<10000 || triangles>17000)throw new InvalidOperationException("Hanging lantern export triangle count differs from source");
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"HangingLanternView.prefab");
                Debug.Log("[V036-ART] CHECK: distinct CC0 chain-hung brass/star-vent lantern, "+triangles+" triangles, three shared materials, 1K textures");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();
        }
        private static Material Material(string name,bool transparent,bool fire)
        {
            string path=Folder+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.enableInstancing=true;material.SetColor("_BaseColor",fire?Color.white:transparent?new Color(1,.90f,.69f,.68f):new Color(1,.94f,.83f));
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("FishingV036/LanternTextures/LanternAlbedo"));
            material.SetTexture("_MetallicGlossMap",Resources.Load<Texture2D>("FishingV036/LanternTextures/LanternMetallicSmoothness"));
            material.SetTexture("_BumpMap",Resources.Load<Texture2D>("FishingV036/LanternTextures/LanternNormal"));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");material.EnableKeyword("_NORMALMAP");material.SetFloat("_Smoothness",.65f);
            material.SetFloat("_Cull",0);material.SetFloat("_Surface",transparent?1:0);material.SetFloat("_ZWrite",transparent?0:1);
            material.SetFloat("_SrcBlend",(float)(transparent?BlendMode.SrcAlpha:BlendMode.One));material.SetFloat("_DstBlend",(float)(transparent?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
            material.SetFloat("_AlphaClip",transparent?0:1);material.SetFloat("_Cutoff",.5f);material.SetFloat("_Blend",0);
            if(transparent){material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHATEST_ON");}
            else{material.EnableKeyword("_ALPHATEST_ON");material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");}
            material.renderQueue=transparent?3000:2450;
            if(fire){material.SetTexture("_EmissionMap",Resources.Load<Texture2D>("FishingV036/LanternTextures/LanternEmission"));material.SetColor("_EmissionColor",new Color(5,2.3f,.55f));material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;material.EnableKeyword("_EMISSION");}
            else if(transparent){material.SetTexture("_EmissionMap",Texture2D.whiteTexture);material.SetColor("_EmissionColor",new Color(.55f,.27f,.07f));material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;material.EnableKeyword("_EMISSION");}
            EditorUtility.SetDirty(material);return material;
        }
    }
}
