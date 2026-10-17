using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        private Transform _guideCover,_guideLeaf;
        public Transform GuideCover=>_guideCover;
        public static readonly Vector3 GuideHinge=new Vector3(-.215f,.045f,0);
        private Transform GuidePart(string name,Vector3 hinge)
        {
            var source=Resources.Load<GameObject>("FishingV031/"+name);
            if(source==null)throw new System.InvalidOperationException("Missing hinged naturalist guide: "+name);
            var part=MeshObject(name,source.GetComponentInChildren<MeshFilter>().sharedMesh);part.SetParent(_held,false);part.localPosition=hinge;return part;
        }
        private void PresentGuideParts(FieldItem item)
        {
            bool guide=item==FieldItem.FieldGuide;
            if(guide && _guideCover==null)
            {_guideCover=GuidePart("GuideCover",GuideHinge);_guideLeaf=GuidePart("GuideLeaf",new Vector3(-.208f,.036f,0));}
            if(_guideCover==null)return;
            _guideCover.gameObject.SetActive(guide);_guideLeaf.gameObject.SetActive(guide);
            if(!guide)return;
            float amount=_c.GuideOpening?_c.GuideOpenAmount:0;
            _guideCover.localRotation=Quaternion.Euler(0,0,168*amount);
            _guideLeaf.localRotation=Quaternion.Euler(0,0,158*amount-Mathf.Sin(amount*Mathf.PI)*12);
        }
    }
}
