using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed class FishingFleetPresentation : MonoBehaviour
    {
        private FishingPrototype _c;
        private Mesh _mesh;
        private Material _material;
        private int _levels = -1;
        public void Build(FishingPrototype c)
        {
            _c = c; _material = new Material(c.PaletteShader) { name = "Fleet brass and upholstery" };
            gameObject.AddComponent<MeshFilter>(); gameObject.AddComponent<MeshRenderer>().sharedMaterial = _material;
            Refresh();
        }
        private void Update()
        {
            if (_c != null && _levels != _c.Career.EngineLevel + _c.Career.RudderLevel * 3 + _c.Career.CabinLevel * 9) Refresh();
        }
        public void Refresh()
        {
            var career = _c.Career; var engine = _c.Player.engine;
            engine.horsePower = FishingFleet.Power(career.EngineLevel);
            engine.forwardSpeedLimit = FishingFleet.SpeedLimit(career.EngineLevel);
            engine.rudderResponse = FishingFleet.Rudder(career.RudderLevel);
            _levels = career.EngineLevel + career.RudderLevel * 3 + career.CabinLevel * 9;
            var b = new FishingMeshBuilder();
            Color brass = new Color(.88f,.65f,.27f), cream = new Color(.92f,.88f,.75f), navy = new Color(.06f,.19f,.27f);
            if (career.EngineLevel > 0)
            {
                b.Box(new Vector3(0,.87f,-1.95f),new Vector3(.48f,.42f,.38f),career.EngineLevel == 2 ? navy : cream);
                for (int i=0;i<3;i++)b.Box(new Vector3(0,.76f+i*.07f,-2.15f),new Vector3(.3f,.025f,.015f),brass);
            }
            if (career.RudderLevel > 0)
            {
                b.Tube(new Vector3(.78f,.61f,.7f),new Vector3(.78f,1.05f,.7f),.045f,.045f,brass);
                b.Arc(new Vector3(.78f,1.13f,.7f),.17f,.017f,0,360,career.RudderLevel==2?brass:navy,16);
            }
            if (career.CabinLevel > 0)
            {
                foreach (float x in new[]{-.86f,.86f})
                {
                    b.Tube(new Vector3(x,.66f,-1.4f),new Vector3(x,.66f,1.35f),.025f,.025f,brass);
                    b.Box(new Vector3(x*.75f,.58f,-1.22f),new Vector3(.42f,.14f,.55f),cream);
                }
                b.Box(new Vector3(0,.56f,1.28f),new Vector3(1.16f,.05f,.44f),new Color(.56f,.32f,.13f));
                for(int i=0;i<5;i++)b.Box(new Vector3((i-2)*.22f,.592f,1.28f),new Vector3(.008f,.006f,.42f),brass);
            }
            if (career.CabinLevel == 2)
            {
                foreach(float x in new[]{-.84f,.84f})
                {
                    b.Tube(new Vector3(x,.64f,-1.42f),new Vector3(x,2.45f,-1.42f),.025f,.025f,brass);
                    b.Tube(new Vector3(x,.64f,.3f),new Vector3(x,2.45f,.3f),.025f,.025f,brass);
                }
                b.Box(new Vector3(0,2.47f,-.55f),new Vector3(1.84f,.08f,1.96f),cream);
                b.Box(new Vector3(0,2.42f,-1.51f),new Vector3(1.86f,.16f,.05f),navy);
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = b.Finish("Paid fleet upgrades"); GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); if (_material != null) Destroy(_material); }
    }
}
