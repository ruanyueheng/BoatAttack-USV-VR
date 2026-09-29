using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Original low-poly salvage beacon; no downloaded assets, realtime lights or volumetrics.</summary>
    public sealed class FishingBeaconVisual : MonoBehaviour
    {
        private FishingPrototype _controller;
        private Mesh _tower;
        private Material _lit;
        private Renderer _lens;
        private GameObject _coil;
        private Transform _head;
        private bool _wasRepaired;
        private bool _hasState;

        public void Build(FishingPrototype controller, Vector3 feet)
        {
            _controller = controller;
            transform.position = feet;
            Part("Octagonal stone plinth", PrimitiveType.Cylinder, new Vector3(0,.12f,0), new Vector3(1.2f,.12f,1.2f), controller.dark);
            _tower = TaperedTower();
            var tower = new GameObject("Eight-panel weathered tower", typeof(MeshFilter), typeof(MeshRenderer));
            tower.transform.SetParent(transform,false);
            tower.GetComponent<MeshFilter>().sharedMesh = _tower;
            tower.GetComponent<Renderer>().sharedMaterial = controller.cream;
            for(int i=0;i<3;i++)
                Part("Iron reinforcement hoop",PrimitiveType.Cylinder,new Vector3(0,.45f+i*.7f,0),new Vector3(1f-i*.16f,.045f,1f-i*.16f),controller.teal);
            Part("Service hatch",PrimitiveType.Cube,new Vector3(0,.78f,-.39f),new Vector3(.38f,.62f,.08f),controller.dark);
            Part("Brass latch",PrimitiveType.Cube,new Vector3(.12f,.78f,-.45f),new Vector3(.05f,.14f,.04f),controller.orange);
            for(int i=0;i<5;i++)
                Part("Maintenance ladder rung",PrimitiveType.Cube,new Vector3(.43f,.55f+i*.28f,0),new Vector3(.10f,.045f,.35f),controller.dark);
            var head = new GameObject("Rotating lantern housing"); head.transform.SetParent(transform,false);
            head.transform.localPosition=Vector3.up*2.25f; _head=head.transform;
            Part("Lantern gallery",PrimitiveType.Cylinder,new Vector3(0,2.22f,0),new Vector3(1.12f,.08f,1.12f),controller.dark);
            _lit = new Material(controller.PaletteShader) { name="Beacon restored luminous lens" };
            _lit.SetColor("_BaseColor",new Color(.35f,1.6f,1.4f)); _lit.SetFloat("_Unlit",1f);
            _lens=FishingArt.Shape("Prismatic lens",PrimitiveType.Sphere,_head,new Vector3(0,.35f,0),new Vector3(.52f,.63f,.52f),controller.dark).GetComponent<Renderer>();
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f+Mathf.PI*.25f;
                FishingArt.Shape("Lantern cage upright",PrimitiveType.Cylinder,_head,new Vector3(Mathf.Cos(a)*.38f,.35f,Mathf.Sin(a)*.38f),new Vector3(.055f,.35f,.055f),controller.dark);
            }
            FishingArt.Shape("Copper lantern roof",PrimitiveType.Sphere,_head,new Vector3(0,.77f,0),new Vector3(1.1f,.22f,1.1f),controller.orange);
            FishingArt.Shape("Directional sun visor",PrimitiveType.Cube,_head,new Vector3(0,.64f,-.4f),new Vector3(.65f,.12f,.08f),controller.teal);
            // Fresnel-style lens ridges remain open from the shore-facing side.
            for(int i=0;i<3;i++)FishingArt.Shape("Lens brass ridge",PrimitiveType.Cylinder,_head,new Vector3(0,.18f+i*.17f,0),new Vector3(.55f,.014f,.55f),controller.orange);
            FishingArt.Shape("Alignment pointer",PrimitiveType.Cube,_head,new Vector3(0,.77f,.52f),new Vector3(.10f,.06f,.5f),controller.cream);
            Part("Lightning finial",PrimitiveType.Cylinder,new Vector3(0,3.26f,0),new Vector3(.045f,.22f,.045f),controller.dark);
            _coil=Part("Installed replacement coil",PrimitiveType.Cylinder,new Vector3(0,.8f,-.46f),new Vector3(.23f,.2f,.23f),controller.orange);
            Refresh();
        }
        private GameObject Part(string label,PrimitiveType type,Vector3 position,Vector3 scale,Material material) =>
            FishingArt.Shape(label,type,transform,position,scale,material);
        // Separate vertices per face give a deliberate faceted silhouette, rather than a smooth cylinder.
        private static Mesh TaperedTower()
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4, b=(i+1)*Mathf.PI/4;
                int n=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*.5f,.24f,Mathf.Sin(a)*.5f));
                vertices.Add(new Vector3(Mathf.Cos(a)*.28f,2.2f,Mathf.Sin(a)*.28f));
                vertices.Add(new Vector3(Mathf.Cos(b)*.28f,2.2f,Mathf.Sin(b)*.28f));
                vertices.Add(new Vector3(Mathf.Cos(b)*.5f,.24f,Mathf.Sin(b)*.5f));
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            var mesh=new Mesh { name="Original tapered octagonal beacon" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            var colors=new Color[vertices.Count]; for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
            mesh.colors=colors; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private void Update()
        {
            if(_controller==null)return;
            Refresh();
            var scene=_controller.Expedition;
            if(scene!=null && scene.Dial!=null)
                _head.localRotation=Quaternion.Euler(_wasRepaired || scene.Aligning?0:12,_wasRepaired?scene.Dial.Target:scene.Dial.Heading,0);
        }
        private void Refresh()
        {
            bool repaired=_controller.Career.Expedition.Has(FishingExpedition.BeaconRepaired);
            if(_hasState && repaired==_wasRepaired)return;
            _hasState=true; _wasRepaired=repaired;
            _lens.sharedMaterial=repaired?_lit:_controller.dark;
            _coil.SetActive(repaired);
            _head.localRotation=Quaternion.Euler(repaired?0:12,repaired?0:-55,0);
        }
        private void OnDestroy()
        {
            if(_tower!=null)Destroy(_tower);
            if(_lit!=null)Destroy(_lit);
        }
    }
}
