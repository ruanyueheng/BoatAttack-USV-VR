using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFarmWorld
    {
        private Mesh _seedPackets;
        private Material _seedPacketMaterial;
        private void BuildSeedPackets()
        {
            var b=new FishingLifeMesh();var paper=new Color(.67f,.44f,.23f);var label=new Color(.94f,.89f,.72f);var green=new Color(.22f,.47f,.26f);
            for(int i=0;i<3;i++)
            {
                float x=9.7f+i*.34f;
                b.Box(new Vector3(x,1.025f,-3.8f),new Vector3(.24f,.32f,.12f),paper);
                b.Box(new Vector3(x,1.18f,-3.8f),new Vector3(.25f,.025f,.13f),label);
                b.Box(new Vector3(x,1.035f,-3.863f),new Vector3(.18f,.21f,.006f),label);
                b.Cylinder(new Vector3(x,.997f,-3.868f),new Vector3(x,.997f,-3.874f),.034f,.034f,Color.white,10);
                b.Box(new Vector3(x,1.05f,-3.875f),new Vector3(.009f,.08f,.004f),green);
                foreach(float side in new[]{-1f,1f})b.Triangle(new Vector3(x,1.065f,-3.878f),new Vector3(x+side*.052f,1.10f,-3.878f),new Vector3(x+side*.043f,1.062f,-3.878f),green);
            }
            _seedPackets=b.Finish("Three folded radish seed packets");
            _seedPacketMaterial=new Material(_c.PaletteShader){name="Garden kraft seed packets"};
            var packets=new GameObject("萝卜种子袋",typeof(MeshFilter),typeof(MeshRenderer));packets.transform.SetParent(Root,false);
            packets.GetComponent<MeshFilter>().sharedMesh=_seedPackets;packets.GetComponent<MeshRenderer>().sharedMaterial=_seedPacketMaterial;
        }
        private void DestroySeedPackets(){if(_seedPackets!=null)Destroy(_seedPackets);if(_seedPacketMaterial!=null)Destroy(_seedPacketMaterial);}
    }
}
