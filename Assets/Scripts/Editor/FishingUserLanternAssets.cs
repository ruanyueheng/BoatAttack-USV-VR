using System;
using UnityEditor;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingUserLanternAssets
    {
        [MenuItem("BoatAttack/Fishing/Import Supplied Lantern")]
        public static void Build()
        {
            try
            {
                const string folder="Assets/Resources/FishingUserTools/";AssetDatabase.Refresh();
                var model=(ModelImporter)AssetImporter.GetAtPath(folder+"Lantern.fbx");
                model.materialImportMode=ModelImporterMaterialImportMode.None;model.importAnimation=false;model.animationType=ModelImporterAnimationType.None;
                model.bakeAxisConversion=true;model.isReadable=true;model.importNormals=ModelImporterNormals.Import;model.importTangents=ModelImporterTangents.CalculateMikk;model.addCollider=false;model.SaveAndReimport();
                foreach(string name in new[]{"LanternAlbedo","LanternMetallicSmoothness","LanternNormal","LanternThumbnail"})
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(folder+name+".png");
                    importer.textureType=name=="LanternNormal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                    importer.sRGBTexture=name=="LanternAlbedo" || name=="LanternThumbnail";importer.isReadable=name=="LanternAlbedo";
                    importer.alphaIsTransparency=name=="LanternThumbnail";importer.mipmapEnabled=name!="LanternThumbnail";
                    importer.maxTextureSize=name=="LanternThumbnail"?256:name=="LanternAlbedo"?2048:1024;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                }
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
                var materials=new Material[2];
                for(int i=0;i<2;i++)
                {
                    string path=folder+(i==0?"Lantern":"LanternGlow")+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"LanternAlbedo.png"));material.SetColor("_BaseColor",Color.white);
                    material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"LanternMetallicSmoothness.png"));material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"LanternNormal.png"));material.SetFloat("_BumpScale",.55f);material.EnableKeyword("_NORMALMAP");material.SetFloat("_Smoothness",.55f);
                    material.SetColor("_EmissionColor",Color.black);
                    material.SetFloat("_Cull",0);material.doubleSidedGI=true;
                    if(i==1)
                    {
                        material.SetTexture("_BaseMap",Texture2D.whiteTexture);material.SetColor("_BaseColor",new Color(.91f,.51f,.16f));
                        material.SetTexture("_MetallicGlossMap",null);material.DisableKeyword("_METALLICSPECGLOSSMAP");
                        material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");material.SetFloat("_Smoothness",.18f);
                        material.SetTexture("_EmissionMap",Texture2D.whiteTexture);material.EnableKeyword("_EMISSION");
                        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    else {material.DisableKeyword("_EMISSION");material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");material.SetFloat("_EnvironmentReflections",0);}
                    EditorUtility.SetDirty(material);materials[i]=material;
                }
                var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Lantern.fbx"));
                try
                {
                    source.name="Supplied portable lantern";
                    foreach(var renderer in source.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=materials;
                    PrefabUtility.SaveAsPrefabAsset(source,folder+"UserLantern.prefab");
                    var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
                    for(int i=0;i<mesh.subMeshCount;i++)Debug.Log("[USER-LANTERN-ASSETS] SUBMESH "+i+" tris="+mesh.GetSubMesh(i).indexCount/3+" bounds="+mesh.GetSubMesh(i).bounds);
                    var tex=(Texture2D)materials[0].GetTexture("_BaseMap");int brown=0;
                    for(int i=0;i<mesh.vertexCount;i++)if(mesh.vertices[i].y>.3f){var uv=mesh.uv[i];var colour=tex.GetPixelBilinear(uv.x,uv.y);if(colour.r>colour.b*1.4f && colour.r>.2f)brown++;}
                    Debug.Log("[USER-LANTERN-ASSETS] HANDLE WOOD UV samples="+brown+" first="+mesh.uv[0]);
                    int outward=0,inward=0;var triangles=mesh.GetTriangles(1);var vertices=mesh.vertices;
                    for(int i=0;i<triangles.Length;i+=3)
                    {var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var d=vertices[triangles[i+2]];var p=(a+b+d)/3;var radial=new Vector3(p.x,0,p.z);if(Vector3.Dot(Vector3.Cross(b-a,d-a),radial)>0)outward++;else inward++;}
                    Debug.Log("[USER-LANTERN-ASSETS] PAPER WINDING outward="+outward+" inward="+inward);
                    if(Mathf.Abs(mesh.bounds.size.y-.4f)>.01f || mesh.subMeshCount!=2 || mesh.triangles.Length/3>13000)throw new InvalidOperationException("Lantern source axes/submesh budget mismatch: "+mesh.bounds);
                    Debug.Log("[USER-LANTERN-ASSETS] BOUNDS: "+mesh.bounds+" triangles="+mesh.triangles.Length/3);
                }
                finally{UnityEngine.Object.DestroyImmediate(source);}
                AssetDatabase.SaveAssets();Debug.Log("[USER-LANTERN-ASSETS] PASS: supplied lamp, source PBR, switchable paper emission.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
