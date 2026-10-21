using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>Three batched surfaces: carved stone, a mist veil and quiet contour stardust.</summary>
    public sealed class FishingPortalVisual : MonoBehaviour
    {
        public const string RootName="V036 star-tide portal";
        private Material _frameMaterial,_curtainMaterial,_moteMaterial;
        private Mesh _frameMesh,_curtainMesh,_moteMesh;
        private Renderer _curtain;
        private MaterialPropertyBlock _pulse;
        private FishingPrototype _controller;
        private bool _reduced;
        public float Height { get; private set; }
        public void Build(FishingPrototype c,int island,bool compact)
        {
            if(island<0 || island>3)throw new ArgumentOutOfRangeException(nameof(island));
            _controller=c;
            float radius=compact?.78f:.96f,shoulder=compact?1.55f:2.12f;Height=shoulder+radius;
            var bronze=new Color(.56f,.43f,.24f);var gilt=new Color(.83f,.72f,.45f);
            var stone=new Color(.72f,.73f,.64f);var leaf=new Color(.39f,.50f,.39f);
            var tint=new[]{new Color(.38f,.69f,.77f),new Color(.48f,.72f,.58f),new Color(.45f,.64f,.80f),new Color(.65f,.57f,.80f)}[island];
            _frameMaterial=new Material(c.PaletteShader){name="V037 weathered stone and bronze inlay"};_frameMaterial.SetFloat("_Unlit",.28f);_frameMaterial.SetFloat("_Gloss",.16f);_frameMaterial.SetFloat("_Rim",.18f);
            var b=new FishingMeshBuilder();
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*(radius+.12f);
                for(int course=0;course<5;course++)
                {
                    float y0=course*shoulder/5+.012f,y1=(course+1)*shoulder/5-.012f;
                    Block(b,new Vector3(x,(y0+y1)*.5f,0),new Vector3(.25f,y1-y0,.34f),Color.Lerp(stone,new Color(.85f,.81f,.66f),(course%3)*.17f));
                }
                Block(b,new Vector3(x,.09f,0),new Vector3(.39f,.18f,.46f),stone*.92f);
                Block(b,new Vector3(x,shoulder-.04f,0),new Vector3(.34f,.14f,.42f),stone);
                foreach(float face in new[]{-1f,1f})
                {
                    float z=face*.185f;
                    b.Tube(new Vector3(x,.22f,z),new Vector3(x,shoulder-.17f,z),.012f,.012f,bronze,6);
                    for(int j=0;j<5;j++)
                    {
                        float y=.35f+j*(shoulder-.68f)/5;
                        var p=new Vector3(x,y,z+face*.007f);var tip=p+new Vector3(side*(.065f+(j%2)*.025f),.13f,0);
                        b.Tube(p,tip,.006f,.002f,gilt,5);
                        Leaf(b,p+new Vector3(side*.018f,.045f,face*.003f),new Vector3(side*.095f,.073f,0),island==1?leaf:Color.Lerp(leaf,stone,.45f));
                    }
                }
            }
            const int stones=13;
            for(int j=0;j<stones;j++)
            {
                float a=(j*180f/stones+.8f)*Mathf.Deg2Rad,z=((j+1)*180f/stones-.8f)*Mathf.Deg2Rad;
                Wedge(b,Vector3.up*shoulder,radius+.018f,radius+.27f,a,z,.18f,Color.Lerp(stone,new Color(.89f,.83f,.66f),(j%3)*.12f));
            }
            foreach(float face in new[]{-1f,1f})
            {
                b.Arc(new Vector3(0,shoulder,face*.195f),radius+.115f,.013f,0,180,bronze,28);
                b.Arc(new Vector3(0,shoulder,face*.20f),radius+.05f,.007f,0,180,gilt,28);
                for(int j=1;j<7;j++)
                {
                    float a=j*Mathf.PI/7;
                    Star(b,new Vector3(Mathf.Cos(a)*(radius+.145f),shoulder+Mathf.Sin(a)*(radius+.145f),face*.212f),j%2==0?.047f:.029f,gilt);
                }
                // An open crescent and suspended star read as a small storybook talisman.
                var crown=new Vector3(0,Height+.32f,face*.02f);
                b.Arc(crown,.115f,.017f,55,305,bronze,15);
                Star(b,crown+new Vector3(.015f,0,face*.025f),.063f,gilt);
                b.Tube(crown+Vector3.down*.12f,crown+Vector3.down*.23f,.005f,.005f,bronze,5);
                Star(b,crown+Vector3.down*.25f,.035f,tint);
            }
            for(int j=0;j<3;j++)Star(b,new Vector3(0,.025f,.45f+j*.44f),.075f,stone,true);
            _frameMesh=b.Finish("V037 carved limestone arch with bronze botanical inlay");Surface("Carved brass arch",_frameMesh,_frameMaterial);
            var shader=Resources.Load<Shader>("FishingPortal");if(shader==null)throw new InvalidOperationException("Missing portal mist shader");
            _curtainMaterial=new Material(shader){name="V037 watercolor nebula veil"};_curtainMaterial.SetColor("_Tint",tint);_curtainMaterial.SetFloat("_Shoulder",shoulder/Height);_curtainMaterial.SetFloat("_Height",Height);_curtainMaterial.SetFloat("_PulseTime",-10);
            _curtainMesh=new Mesh{name="V037 arched mist veil"};
            _curtainMesh.vertices=new[]{new Vector3(-radius,0,.015f),new Vector3(radius,0,.015f),new Vector3(-radius,Height,.015f),new Vector3(radius,Height,.015f)};
            _curtainMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};_curtainMesh.triangles=new[]{0,2,1,1,2,3};_curtainMesh.RecalculateBounds();
            _curtain=Surface("Visible flowing light curtain",_curtainMesh,_curtainMaterial);_pulse=new MaterialPropertyBlock();
            _moteMaterial=new Material(_curtainMaterial){name="V037 contour fireflies and tiny stars"};_moteMaterial.SetFloat("_Mode",1);
            const int count=32;var vertices=new Vector3[count*4];var uv=new Vector2[count*4];var seeds=new Vector2[count*4];var triangles=new int[count*6];
            for(int j=0;j<count;j++)
            {
                float r=radius+.04f,length=shoulder*2+Mathf.PI*r,d=(j+.5f)*length/count;
                float angle=Mathf.Clamp((d-shoulder)/r,0,Mathf.PI);
                float x=d<shoulder?r:d>shoulder+Mathf.PI*r?-r:Mathf.Cos(angle)*r;
                float y=d<shoulder?d:d>shoulder+Mathf.PI*r?length-d:shoulder+Mathf.Sin(angle)*r;
                float size=j%5==0?.025f:.010f+(j%3)*.003f;int v=j*4,t=j*6;
                for(int corner=0;corner<4;corner++)
                {
                    var q=new Vector2(corner%2,corner/2);vertices[v+corner]=new Vector3(x+(q.x-.5f)*size*2,y+(q.y-.5f)*size*2,.23f+(j%3)*.035f);uv[v+corner]=q;seeds[v+corner]=new Vector2(j*.618034f,j*.37f);
                }
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            _moteMesh=new Mesh{name="V037 32 contour stars"};_moteMesh.vertices=vertices;_moteMesh.uv=uv;_moteMesh.uv2=seeds;_moteMesh.triangles=triangles;_moteMesh.bounds=new Bounds(Vector3.up*(Height*.5f),new Vector3(radius*2+1,Height+1,1));
            Surface("Drifting gold stardust",_moteMesh,_moteMaterial);
            SetReduced(c.Weather!=null && c.Weather.ReducedEffects);
        }
        private static void Block(FishingMeshBuilder b,Vector3 p,Vector3 size,Color color)
        {
            Vector3 h=size*.5f;float bevel=Mathf.Min(.035f,Mathf.Min(h.x,h.y)*.3f);
            var ring=new[]{new Vector2(-h.x+bevel,-h.y),new Vector2(h.x-bevel,-h.y),new Vector2(h.x,-h.y+bevel),new Vector2(h.x,h.y-bevel),new Vector2(h.x-bevel,h.y),new Vector2(-h.x+bevel,h.y),new Vector2(-h.x,h.y-bevel),new Vector2(-h.x,-h.y+bevel)};
            for(int j=0;j<8;j++)
            {
                var a=p+new Vector3(ring[j].x,ring[j].y,-h.z);var d=p+new Vector3(ring[(j+1)%8].x,ring[(j+1)%8].y,-h.z);
                var z=Vector3.forward*size.z;
                b.Fin(p-Vector3.forward*h.z,d,a,color);b.Fin(p+Vector3.forward*h.z,a+z,d+z,color);
                b.Fin(a,d,a+z,color*.91f);b.Fin(d,d+z,a+z,color*.91f);
            }
        }
        private static void Wedge(FishingMeshBuilder b,Vector3 p,float inner,float outer,float from,float to,float depth,Color color)
        {
            var v=new Vector3[8];
            for(int face=0;face<2;face++)for(int corner=0;corner<4;corner++)
            {
                float a=corner%2==0?from:to,r=corner<2?inner:outer;
                v[face*4+corner]=p+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,face==0?-depth:depth);
            }
            int[] faces={0,1,2,1,3,2,4,6,5,5,6,7,0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7};
            for(int j=0;j<faces.Length;j+=3)b.Fin(v[faces[j]],v[faces[j+1]],v[faces[j+2]],color);
        }
        private static void Leaf(FishingMeshBuilder b,Vector3 p,Vector3 direction,Color color)
        {
            Vector3 cross=new Vector3(-direction.y,direction.x,0)*.24f,mid=p+direction*.5f;
            b.Fin(p,mid+cross,p+direction,color);b.Fin(p,p+direction,mid-cross,color);
        }
        private static void Star(FishingMeshBuilder b,Vector3 p,float radius,Color color,bool ground=false)
        {
            for(int j=0;j<8;j++)
            {
                float a=j*Mathf.PI/4,angle=(j+1)*Mathf.PI/4;
                var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*(j%2==0?1:.25f);
                var v=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius*(j%2==0?.25f:1);
                b.Fin(p,p+(ground?new Vector3(u.x,0,u.y):new Vector3(u.x,u.y,0)),p+(ground?new Vector3(v.x,0,v.y):new Vector3(v.x,v.y,0)),color);
            }
        }
        private Renderer Surface(string label,Mesh mesh,Material material)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
        private void SetReduced(bool reduced)
        {
            _reduced=reduced;_curtainMaterial.SetFloat("_Motion",reduced?0:1);_moteMaterial.SetFloat("_Motion",reduced?0:1);
        }
        private void LateUpdate()
        {
            if(_controller!=null && _controller.Weather!=null && _reduced!=_controller.Weather.ReducedEffects)SetReduced(_controller.Weather.ReducedEffects);
        }
        public void Pulse(){if(_reduced)return;_pulse.SetFloat("_PulseTime",Time.time);_curtain.SetPropertyBlock(_pulse);}
        private void OnDestroy()
        {
            if(_frameMesh!=null)Destroy(_frameMesh);if(_curtainMesh!=null)Destroy(_curtainMesh);if(_moteMesh!=null)Destroy(_moteMesh);
            if(_frameMaterial!=null)Destroy(_frameMaterial);if(_curtainMaterial!=null)Destroy(_curtainMaterial);if(_moteMaterial!=null)Destroy(_moteMaterial);
        }
    }
}
