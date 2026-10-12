using System;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Scoped import policy; does not alter existing boats, fish or environment assets.</summary>
    public sealed class FishingPropImporter : AssetPostprocessor
    {
        public override uint GetVersion()=>1;
        private void OnPreprocessModel()
        {
            bool fish=assetPath.StartsWith("Assets/Resources/FishingFish/",StringComparison.Ordinal);
            bool boat=assetPath.StartsWith("Assets/Resources/FishingBoat/",StringComparison.Ordinal);
            if (!fish && !boat && !assetPath.StartsWith("Assets/Resources/FishingProps/", StringComparison.Ordinal) && !assetPath.StartsWith("Assets/Resources/FishingGear/", StringComparison.Ordinal)) return;
            bool gear=assetPath.StartsWith("Assets/Resources/FishingGear/", StringComparison.Ordinal);
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            if(gear || fish || boat) importer.isReadable=true;
            if(fish || boat)importer.bakeAxisConversion=true;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
        }
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/FishingFish/",StringComparison.Ordinal))return;
            var importer=(TextureImporter)assetImporter;
            bool thumbnail=assetPath.EndsWith("Thumbnail.png",StringComparison.Ordinal);
            importer.maxTextureSize=thumbnail?256:1024;importer.mipmapEnabled=!thumbnail;
            importer.alphaIsTransparency=thumbnail;importer.wrapMode=TextureWrapMode.Clamp;
            importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.Compressed;
        }
        private void OnPostprocessModel(GameObject root)
        {
            if(assetPath=="Assets/Resources/FishingGear/ReelCrank.fbx")
            {
                // The original FBX export mirrors X; restore the left-side crank to its authored grip coordinates.
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
                    for(int i=0;i<vertices.Length;i++)vertices[i].x=-vertices[i].x;
                    for(int i=0;i<normals.Length;i++)normals[i].x=-normals[i].x;
                    for(int i=0;i<indices.Length;i+=3){int index=indices[i];indices[i]=indices[i+2];indices[i+2]=index;}
                    mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateBounds();
                }
                return;
            }
            if(!assetPath.StartsWith("Assets/Resources/FishingFish/",StringComparison.Ordinal) && !assetPath.StartsWith("Assets/Resources/FishingBoat/",StringComparison.Ordinal))return;
            // Fish() consumes the shared mesh, so FBX unit/axis transforms must live in its vertices.
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var matrix=filter.transform.localToWorldMatrix;
                var vertices=mesh.vertices;var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
                mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();
                Debug.Log("[HUNYUAN] Imported "+assetPath+" bounds="+mesh.bounds.size.ToString("F5")+" transform="+matrix);
            }
            foreach(var node in root.GetComponentsInChildren<Transform>())
            {node.localPosition=Vector3.zero;node.localRotation=Quaternion.identity;node.localScale=Vector3.one;}
        }
    }
}
