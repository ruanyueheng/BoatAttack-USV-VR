using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingCatchRig
    {
        private Transform _fishGripper;
        private Mesh _fishGripperMesh;
        private Material _fishGripperMaterial;
        private Transform _mouthFish;
        private Vector3 _fishMouthLocal;
        public static readonly Vector3 GripperPalm=new Vector3(0,0,-.025f);
        public static readonly Quaternion GripperPalmRotation=Quaternion.LookRotation(Vector3.right,Vector3.down);
        public static readonly Vector3 GripperJaw=new Vector3(0,-.222f,0);
        public Transform FishGripper=>_fishGripper;
        public Vector3 FishHeadAxis
        {
            get
            {
                var b=_c.ActiveFish.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                return b.size.y>b.size.z?Vector3.up:Vector3.forward;
            }
        }
        public Vector3 FishMouthLocal
        {
            get
            {
                var fish=_c.ActiveFish;if(_mouthFish==fish)return _fishMouthLocal;
                _mouthFish=fish;
                var filter=fish.GetComponentInChildren<MeshFilter>();var b=filter.sharedMesh.bounds;
                // Imported and procedural fish face +Z; upright source meshes use +Y.
                // Project the snout centre onto actual mesh vertices, never onto the catch's trigger sphere.
                var tip=b.center+Vector3.Scale(FishHeadAxis,b.extents);
                // A sailfish's bill tip is not its mouth; use the base of its extended bill.
                if(_c.Career.PendingCatch.fish.kind==FishKind.Sailfish)tip=b.center+Vector3.Scale(FishHeadAxis,b.extents)*.58f;
                return _fishMouthLocal=fish.InverseTransformPoint(FishingContactGeometry.ClosestPoint(fish,filter.transform.TransformPoint(tip)));
            }
        }
        private void BuildFishGripper()
        {
            if(_fishGripper!=null)return;
            var b=new FishingMeshBuilder();
            var rubber=new Color(.055f,.065f,.075f);var steel=new Color(.72f,.77f,.78f);var accent=new Color(.83f,.34f,.08f);
            b.Tube(new Vector3(0,-.055f,0),new Vector3(0,.06f,0),.017f,.017f,rubber,12);
            for(int i=0;i<5;i++)b.Tube(new Vector3(0,-.045f+i*.021f,0),new Vector3(0,-.04f+i*.021f,0),.0185f,.0185f,rubber,12);
            b.Tube(new Vector3(0,.06f,0),new Vector3(0,.07f,0),.018f,.018f,accent,12);
            b.Tube(new Vector3(0,-.055f,0),new Vector3(0,-.17f,0),.009f,.009f,steel,10);
            b.Tube(new Vector3(0,-.06f,0),new Vector3(0,-.078f,0),.014f,.014f,steel,10);
            b.Tube(new Vector3(.014f,-.025f,.006f),new Vector3(.022f,-.065f,.006f),.005f,.005f,accent,8);
            b.Tube(new Vector3(0,-.165f,0),new Vector3(-.027f,-.195f,0),.006f,.005f,steel,8);
            b.Tube(new Vector3(0,-.165f,0),new Vector3(.027f,-.195f,0),.006f,.005f,steel,8);
            // Tube rejects segments shorter than 1 cm; six segments retain these small jaws.
            b.Arc(new Vector3(0,-.195f,0),.027f,.0045f,180,360,steel,6);
            _fishGripperMesh=b.Finish("Fish lip gripper · rubber handle and locking jaws");
            _fishGripperMaterial=new Material(_c.PaletteShader){name="Fish gripper steel and rubber"};
            _fishGripperMaterial.SetFloat("_Gloss",.42f);
            var go=new GameObject("Left hand fish lip gripper",typeof(MeshFilter),typeof(MeshRenderer));
            _fishGripper=go.transform;_fishGripper.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=_fishGripperMesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=_fishGripperMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        private void PresentTrackedFishGripper(Vector3 palm,Quaternion palmFrame)
        {
            BuildFishGripper();_fishGripper.gameObject.SetActive(true);
            var rotation=palmFrame*Quaternion.Inverse(GripperPalmRotation);
            _fishGripper.SetPositionAndRotation(palm-rotation*GripperPalm,rotation);
            _c.Hands.SetToolTarget(true,_fishGripper,GripperPalm,GripperPalmRotation,.9f,fingers:FishingFingerPose.Handle);
            _c.Hands.Left.SetGripSurface(_fishGripper.TransformPoint(new Vector3(0,-.055f,0)),
                _fishGripper.TransformPoint(new Vector3(0,.06f,0)),.0185f);
            // The jaw swivels: the head is above the tail under gravity even when the wrist rolls.
            var forward=Vector3.ProjectOnPlane(_fishGripper.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(_c.Player.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            var heading=Quaternion.LookRotation(forward.normalized,Vector3.up);
            var swing=Quaternion.AngleAxis(Mathf.Sin(_leftLiftTime*7)*4f,heading*Vector3.forward);
            var fishRotation=swing*heading*Quaternion.FromToRotation(FishHeadAxis,Vector3.up);
            var fish=_c.ActiveFish;
            fish.SetPositionAndRotation(_fishGripper.TransformPoint(GripperJaw)-fishRotation*Vector3.Scale(FishMouthLocal,fish.lossyScale),fishRotation);
        }
        private void HideFishGripper(){if(_fishGripper!=null)_fishGripper.gameObject.SetActive(false);}
        private void OnDestroy()
        {
            if(_fishGripperMesh!=null)Destroy(_fishGripperMesh);
            if(_fishGripperMaterial!=null)Destroy(_fishGripperMaterial);
        }
    }
}
