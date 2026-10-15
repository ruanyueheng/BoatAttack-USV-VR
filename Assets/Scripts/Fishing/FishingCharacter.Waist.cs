using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        private Mesh _waistMesh;
        public MeshRenderer WaistCover { get; private set; }
        public float WaistHalfWidth { get; private set; }=.20f;

        private void BuildWaistCover()
        {
            SkinnedMeshRenderer legs=null;
            foreach(var renderer in _renderers)
                if(renderer.name=="Beach_Legs")legs=renderer as SkinnedMeshRenderer;
            if(legs==null)throw new InvalidOperationException("源人物缺少下肢网格，无法封闭本地腰口");
            var baked=new Mesh();var points=new List<Vector3>();
            try
            {
                legs.BakeMesh(baked);var vertices=baked.vertices;float top=float.MinValue;
                for(int i=0;i<vertices.Length;i++)
                {
                    vertices[i]=transform.InverseTransformPoint(legs.transform.TransformPoint(vertices[i]));
                    top=Mathf.Max(top,vertices[i].y);
                }
                foreach(var p in vertices)if(p.y>=top-.012f)points.Add(p);
            }
            finally{Destroy(baked);}
            points.Sort((a,b)=>a.x==b.x?a.z.CompareTo(b.z):a.x.CompareTo(b.x));
            var unique=new List<Vector3>();
            foreach(var p in points)
                if(unique.Count==0 || new Vector2(p.x-unique[unique.Count-1].x,p.z-unique[unique.Count-1].z).sqrMagnitude>1e-8f)unique.Add(p);
            var hull=new List<Vector3>();
            foreach(var p in unique)
            {
                while(hull.Count>=2 && Turn(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);
                hull.Add(p);
            }
            int upper=hull.Count+1;
            for(int i=unique.Count-2;i>=0;i--)
            {
                while(hull.Count>=upper && Turn(hull[hull.Count-2],hull[hull.Count-1],unique[i])<=0)hull.RemoveAt(hull.Count-1);
                hull.Add(unique[i]);
            }
            if(hull.Count>1)hull.RemoveAt(hull.Count-1);
            if(hull.Count<3)throw new InvalidOperationException("源人物腰口不是有效闭合轮廓");
            foreach(var p in hull)WaistHalfWidth=Mathf.Max(WaistHalfWidth,Mathf.Abs(p.x));
            var center=Vector3.zero;foreach(var p in hull)center+=p;center/=hull.Count;
            var cap=new Vector3[hull.Count+1];var triangles=new int[hull.Count*3];
            cap[0]=ToHip(center+Vector3.up*.002f);
            for(int i=0;i<hull.Count;i++)
            {
                // 微量覆盖裤腰边缘，避免站蹲过渡时从接缝看到网格内部。
                var p=center+(hull[i]-center)*1.025f+Vector3.up*.002f;
                cap[i+1]=ToHip(p);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%hull.Count+1;triangles[i*3+2]=i+1;
            }
            _waistMesh=new Mesh{name="Local waist closure"};_waistMesh.vertices=cap;_waistMesh.triangles=triangles;
            _waistMesh.RecalculateNormals();_waistMesh.RecalculateBounds();
            var go=new GameObject("First-person waist closure",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(_body,false);go.GetComponent<MeshFilter>().sharedMesh=_waistMesh;
            WaistCover=go.GetComponent<MeshRenderer>();WaistCover.sharedMaterial=legs.sharedMaterial;
            WaistCover.shadowCastingMode=ShadowCastingMode.Off;WaistCover.enabled=false;
            Vector3 ToHip(Vector3 p)=>_body.InverseTransformPoint(transform.TransformPoint(p));
        }
        private static float Turn(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
    }
}
