using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>Single-renderer glove with fifteen articulated phalanges; no frame-time mesh allocations.</summary>
    public sealed class FishingGlove : MonoBehaviour
    {
        private readonly List<Transform> _joints=new List<Transform>();
        private readonly List<Quaternion> _rest=new List<Quaternion>();
        private Mesh _mesh;
        public SkinnedMeshRenderer Skin { get; private set; }
        public float Curl { get; private set; }
        public void Build(bool left)
        {
            var bones=new List<Transform>{transform};
            var verts=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();
            var indices=new List<int>();var weights=new List<BoneWeight>();
            Add("GlovePalm",Matrix4x4.identity,0,verts,normals,colors,indices,weights);
            for(int digit=0;digit<5;digit++)
            {
                Transform parent=transform;
                for(int joint=0;joint<3;joint++)
                {
                    var bone=new GameObject("Finger "+digit+" joint "+joint).transform;
                    bone.SetParent(parent,false);
                    bone.localPosition=joint==0?(digit==4?new Vector3(-.041f,-.002f,-.013f):new Vector3(-.031f+digit*.0205f,0,.058f)):new Vector3(0,0,.028f);
                    bone.localRotation=joint==0?Quaternion.Euler(0,digit==4?-52f:(digit-1.5f)*4f,0):Quaternion.identity;
                    bones.Add(bone);_joints.Add(bone);_rest.Add(bone.localRotation);
                    var matrix=transform.worldToLocalMatrix*bone.localToWorldMatrix;
                    float scale=digit==3?.84f:digit==4?.93f:1f;
                    Add("GloveFinger",matrix*Matrix4x4.Scale(new Vector3(1,1,scale)),bones.Count-1,verts,normals,colors,indices,weights);
                    parent=bone;
                }
            }
            _mesh=new Mesh{name="Articulated fishing glove"};_mesh.SetVertices(verts);_mesh.SetNormals(normals);_mesh.SetColors(colors);_mesh.SetTriangles(indices,0);_mesh.boneWeights=weights.ToArray();
            var poses=new Matrix4x4[bones.Count];for(int i=0;i<poses.Length;i++)poses[i]=bones[i].worldToLocalMatrix*transform.localToWorldMatrix;
            _mesh.bindposes=poses;_mesh.RecalculateBounds();
            Skin=gameObject.AddComponent<SkinnedMeshRenderer>();Skin.sharedMesh=_mesh;Skin.bones=bones.ToArray();Skin.rootBone=transform;
            Skin.sharedMaterial=Resources.Load<Material>("FishingProps/HarborPalette");Skin.shadowCastingMode=ShadowCastingMode.Off;
            Skin.localBounds=new Bounds(new Vector3(0,0,-.16f),new Vector3(.4f,.4f,.95f));
            // Mirroring the whole rig retains matching bind poses and a single renderer.
            transform.localScale=new Vector3(left?-1:1,1,1);
        }
        private static void Add(string name,Matrix4x4 matrix,int bone,List<Vector3> vertices,List<Vector3> normals,List<Color> colors,List<int> triangles,List<BoneWeight> weights)
        {
            var prefab=Resources.Load<GameObject>("FishingGear/"+name);
            var filter=prefab.GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;
            matrix=matrix*filter.transform.localToWorldMatrix;
            int start=vertices.Count;var v=mesh.vertices;var n=mesh.normals;var c=mesh.colors;
            for(int i=0;i<v.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(v[i]));normals.Add(matrix.MultiplyVector(n[i]).normalized);colors.Add(c.Length==v.Length?c[i]:Color.white);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
            foreach(int index in mesh.triangles)triangles.Add(start+index);
        }
        public void Pose(float curl,bool pointing=false)
        {
            Curl=Mathf.Clamp01(curl);
            for(int i=0;i<_joints.Count;i++)_joints[i].localRotation=_rest[i]*Quaternion.Euler((pointing && i<3?0:Curl)*(i>=12?42:74),0,0);
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);}
    }
}
