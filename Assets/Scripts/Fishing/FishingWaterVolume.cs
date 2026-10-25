using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>A bounded basin with a tessellated free surface and an actual depth for immersion.</summary>
    public sealed class FishingWaterVolume : MonoBehaviour
    {
        private Mesh _mesh;
        private Material _material;
        private float _nextRipple;
        private Vector2 _size;
        private float _bottom,_surface;
        public float SurfaceHeight=>transform.parent.TransformPoint(new Vector3(0,_surface,0)).y;
        public float BottomHeight=>transform.parent.TransformPoint(new Vector3(0,_bottom,0)).y;
        public void Build(Vector2 size,float bottom,Material material)
        {
            _size=size;_bottom=bottom;gameObject.layer=4;
            int nx=Mathf.CeilToInt(size.x*2),nz=Mathf.CeilToInt(size.y*2),count=(nx+1)*(nz+1);
            var vertices=new Vector3[count+8];var triangles=new int[nx*nz*6+24];var colors=new Color[vertices.Length];int index=0;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int i=z*(nx+1)+x;vertices[i]=new Vector3((x/(float)nx-.5f)*size.x,1,(z/(float)nz-.5f)*size.y);colors[i]=Color.white;
                if(z==nz || x==nx)continue;int a=i,b=i+1,c=i+nx+1,d=c+1;
                triangles[index++]=a;triangles[index++]=c;triangles[index++]=b;triangles[index++]=b;triangles[index++]=c;triangles[index++]=d;
            }
            for(int i=0;i<4;i++)
            {
                var p=new Vector3(i==0 || i==3?-size.x*.5f:size.x*.5f,0,i<2?-size.y*.5f:size.y*.5f);
                vertices[count+i*2]=p;vertices[count+i*2+1]=p+Vector3.up;
                colors[count+i*2]=Color.black;colors[count+i*2+1]=Color.white;
                int a=count+i*2,b=count+(i+1)%4*2;
                triangles[index++]=a;triangles[index++]=a+1;triangles[index++]=b;
                triangles[index++]=b;triangles[index++]=a+1;triangles[index++]=b+1;
            }
            _mesh=new Mesh{name="V038 bounded water basin"};_mesh.vertices=vertices;_mesh.colors=colors;_mesh.triangles=triangles;_mesh.RecalculateNormals();_mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _material=new Material(material){name="V038 basin water"};_material.SetFloat("_UseVertexColor",1);
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            SetLevel(bottom+.02f);
        }
        public void SetLevel(float height)
        {
            _surface=height;var p=transform.localPosition;p.y=_bottom;transform.localPosition=p;
            transform.localScale=new Vector3(1,Mathf.Max(.02f,height-_bottom),1);
        }
        public bool Contains(Vector3 position)
        {
            var p=transform.InverseTransformPoint(position);
            return Mathf.Abs(p.x)<_size.x*.5f && Mathf.Abs(p.z)<_size.y*.5f && p.y>=0 && p.y<=1;
        }
        public void Disturb(Vector3 position)
        {
            if(Time.time<_nextRipple)return;_nextRipple=Time.time+.3f;
            _material.SetVector("_Ripple",new Vector4(position.x,position.z,Time.time,1));
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
