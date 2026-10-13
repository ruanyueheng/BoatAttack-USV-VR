using System;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Explicit, reproducible art import. Does not save or rebuild the fishing scene.</summary>
    public static class FishingV022Assets
    {
        public const string Folder = "Assets/Resources/FishingV022/Residents/";
        public static void Build()
        {
            try
            {
                foreach(int id in new[]{0,1,2,3,5}) Resident(id);
                ExportFishMeshes(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                Debug.Log("[V022-ART] PASS: textured resident LODs and provisional fish meshes exported for Blender portraits");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
        private static void Resident(int id)
        {
            string name="Resident"+id;
            foreach(string suffix in new[]{"Albedo","Normal"})
            {
                string path=Folder+name+suffix+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer==null)throw new FileNotFoundException(path);
                importer.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=suffix!="Normal";importer.maxTextureSize=2048;importer.mipmapEnabled=true;
                importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+name+".mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+name+"Albedo.png"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+name+"Normal.png"));
            material.SetFloat("_BumpScale",.35f);material.SetFloat("_Smoothness",.18f);material.SetFloat("_Metallic",0);
            material.SetFloat("_Cull",(float)CullMode.Off);material.EnableKeyword("_NORMALMAP");material.enableInstancing=true;EditorUtility.SetDirty(material);
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject(name,typeof(LODGroup));SceneManager.MoveGameObjectToScene(root,preview);var lods=new LOD[2];
                for(int i=0;i<2;i++)
                {
                    string path=Folder+name+"_LOD"+i+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                    importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;
                    importer.animationType=ModelImporterAnimationType.None;importer.isReadable=false;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
                    importer.SaveAndReimport();
                    var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform,false);
                    var renderers=model.GetComponentsInChildren<Renderer>();
                    foreach(var renderer in renderers){renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
                    lods[i]=new LOD(i==0?.18f:.009f,renderers);
                }
                var group=root.GetComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root,Folder+name+".prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        [Serializable] private sealed class FishSnapshot
        {
            public string name;
            public Vector3[] vertices;
            public Color[] colors;
            public int[] triangles;
        }
        [Serializable] private sealed class FishSnapshots { public FishSnapshot[] fish; }
        private static void ExportFishMeshes()
        {
            var data=new FishSnapshots {fish=new FishSnapshot[4]};
            for(int i=0;i<4;i++)
            {
                var kind=(FishKind)i;var mesh=FishingMeshBuilder.Fish(kind,false);
                try {data.fish[i]=new FishSnapshot {name=kind.ToString(),vertices=mesh.vertices,colors=mesh.colors,triangles=mesh.triangles};}
                finally {Object.DestroyImmediate(mesh);}
            }
            Directory.CreateDirectory("ArtSource/V022");
            File.WriteAllText("ArtSource/V022/procedural-fish-snapshots.json",JsonUtility.ToJson(data,true));
        }
    }
}
