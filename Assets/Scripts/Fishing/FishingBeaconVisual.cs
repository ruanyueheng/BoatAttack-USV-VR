using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Original low-poly salvage beacon; no downloaded assets, realtime lights or volumetrics.</summary>
    public sealed class FishingBeaconVisual : MonoBehaviour
    {
        private FishingPrototype _controller;
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
            FishingProps.Spawn("BeaconBody", transform, Vector3.zero);
            var head = new GameObject("Rotating lantern housing");
            head.transform.SetParent(transform, false);
            head.transform.localPosition = Vector3.up * 2.25f;
            _head = head.transform;
            FishingProps.Spawn("BeaconHead", _head, Vector3.zero);
            _lens = FishingProps.Spawn("BeaconLens", _head, Vector3.zero).GetComponentInChildren<Renderer>();
            _lit = new Material(controller.PaletteShader) { name="Beacon restored luminous lens" };
            _lit.SetColor("_BaseColor", new Color(.35f,1.6f,1.4f));
            _lit.SetFloat("_Unlit", 1f);
            _coil = FishingProps.Spawn("RepairCoil", transform, new Vector3(0,.61f,-.57f));
            Refresh();
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
            if(_lit!=null)Destroy(_lit);
        }
    }
}
