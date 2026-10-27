using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.EditorTools
{
    /// <summary>Refresh the six repaired models and their existing solid proxies without saving a scene.</summary>
    public static class FishingP0ArtValidation
    {
        private const string Folder="Assets/Resources/FishingV035/";
        private static readonly string[] HomeModels={"OrchardTree","Turnip0","Turnip1","Turnip2","HomeLandscaping"};
        private static readonly string[] SolidModels={"HomeLandscaping","OrchardTree","Turnip2"};
        [Serializable] private sealed class ModelRecord { public string name,fbxSha256;public int triangles,vertices; }
        [Serializable] private sealed class ArtManifest { public ModelRecord[] models;public string radishSource,radishSourceSha256,akSourceSha256,akFbxSha256;public int akTriangles; }

        private static void Check(bool ok,string text)
        {if(!ok)throw new InvalidOperationException("[P0-ART] "+text);Debug.Log("[P0-ART] CHECK: "+text);}
        private static Mesh Model(string name)=>Resources.Load<GameObject>("FishingV035/"+name).GetComponentInChildren<MeshFilter>(true).sharedMesh;
        private static string Hash(string path)
        {using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}

        public static void Run(){Prepare();FishingV011Validation.Begin("P0-ART");}
        public static void Prepare()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before refreshing P0 art.");
            AssetDatabase.Refresh();
            foreach(string path in HomeModels.Select(n=>Folder+n+".fbx").Concat(new[]{"Assets/Resources/FishingHunting/Rifle.fbx"}))
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)throw new InvalidDataException("Missing P0 model importer: "+path);
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;
                importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
                var filters=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length!=1 || filters[0].sharedMesh.uv.Length!=filters[0].sharedMesh.vertexCount)
                    throw new InvalidDataException("Expected one textured P0 model: "+path);
            }
            foreach(string name in SolidModels)
            {
                var source=Model(name);string path=Folder+name+"Collision.asset";
                var collision=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(collision==null)throw new InvalidDataException("Missing existing collision asset: "+path);
                string guid=AssetDatabase.AssetPathToGUID(path);
                collision.Clear();collision.indexFormat=IndexFormat.UInt32;
                collision.vertices=source.vertices;collision.triangles=source.GetTriangles(0);collision.RecalculateBounds();
                EditorUtility.SetDirty(collision);
                Check(AssetDatabase.AssetPathToGUID(path)==guid,"solid proxy preserves GUID: "+name);
            }
            AssetDatabase.SaveAssets();
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            var record=JsonUtility.FromJson<ArtManifest>(File.ReadAllText("ArtSource/Feedback-20261026/P0Art-manifest.json"));
            Check(record!=null && record.models!=null && record.models.Length==5,"five scoped home model records exist");
            Check(Hash(record.radishSource)==record.radishSourceSha256 && File.Exists("ArtSource/Feedback-20261026/Sources/License.txt")
                && File.Exists("ArtSource/Feedback-20261026/Sources/Textures/colormap.png"),"self-contained CC0 radish source, palette and license are present");
            Check(Hash("ArtSource/HuntingRifle/NewAK.glb")==record.akSourceSha256,"supplied AK source is unchanged");
            Check(Hash("Assets/Resources/FishingHunting/Rifle.fbx")==record.akFbxSha256,"runtime AK matches recorded export");
            foreach(var model in record.models)
            {
                var mesh=Model(model.name);
                Check(Hash(Folder+model.name+".fbx")==model.fbxSha256,"FBX matches recorded art: "+model.name);
                Check(mesh.triangles.Length/3==model.triangles && mesh.uv.Length==mesh.vertexCount,
                    "triangle budget and UVs: "+model.name+" actualTriangles="+mesh.triangles.Length/3+" expectedTriangles="+model.triangles+" vertices="+mesh.vertexCount+" uvs="+mesh.uv.Length);
            }
            var rifle=Resources.Load<GameObject>("FishingHunting/Rifle").GetComponentInChildren<MeshFilter>(true).sharedMesh;
            Check(rifle.triangles.Length/3==record.akTriangles && record.akTriangles==12000 && rifle.vertexCount<20000,"AK runtime mesh uses 12000 triangles");
            Check(FishingV023Art.ItemMesh(FieldItem.HuntingRifle)==rifle,"shooting and inspection share the reduced AK");
            Check(c.RifleHunting.Rifle.GetComponentInChildren<MeshFilter>().sharedMesh==rifle,"equipped rifle resolves the reduced FBX");
            foreach(string name in SolidModels)
            {
                var model=Model(name);var solid=Resources.Load<Mesh>("FishingV035/"+name+"Collision");
                Check(solid!=null && solid.vertexCount==model.vertexCount && solid.triangles.SequenceEqual(model.GetTriangles(0)),"solid proxy uses the updated geometry: "+name);
            }
            var trunk=Model("OrchardTree");
            foreach(var ring in new[]{new Vector2(0,.22f),new Vector2(.7f,.20f),new Vector2(2.4f,.12f),new Vector2(3.3f,.065f)})
            {
                int count=trunk.vertices.Count(p=>Mathf.Abs(p.y-ring.x)<.0015f && Mathf.Abs(new Vector2(p.x,p.z).magnitude-ring.y)<.002f);
                Check(count>=16,"vertical tapered trunk ring at "+ring.x+"m has radius "+ring.y+"m");
            }
            var crops=(Transform[,])typeof(FishingFarmWorld).GetField("_crops",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c.Farm);
            for(int stage=0;stage<3;stage++)
                Check(crops[0,stage]!=null && crops[0,stage].GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh==Model("Turnip"+stage)),"farm binds the replacement radish stage "+stage);
            var landscape=c.Home.transform.Find("V035 Home HomeLandscaping");
            var filter=landscape.GetComponentInChildren<MeshFilter>();
            Check(filter.sharedMesh==Model("HomeLandscaping") && landscape.GetComponentInChildren<MeshCollider>().sharedMesh==Resources.Load<Mesh>("FishingV035/HomeLandscapingCollision"),"home binds the stone-free scenery and collision proxy");
            var oldStone=c.Home.Origin+new Vector3(30.468f,0,-10.7841f);
            Check(!filter.sharedMesh.vertices.Any(v=>{var p=filter.transform.TransformPoint(v);return Mathf.Abs(p.x-oldStone.x)<.13f && Mathf.Abs(p.z-oldStone.z)<.13f;}),"the former garden edging-stone footprint is clear");
            c.ReturnToHarbor();yield return null;
        }
    }
}
