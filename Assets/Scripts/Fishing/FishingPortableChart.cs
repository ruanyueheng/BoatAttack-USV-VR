using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Desktop paper and palm goals; the modal owns time and the character owns its skin.</summary>
    public sealed class FishingPortableChart : MonoBehaviour
    {
        public const float OpenSeconds=.7f, CloseSeconds=.4f;
        private FishingPrototype _c;
        private Mesh _mesh;
        private Material _material;
        private Quaternion _cameraRotation;
        private bool _restoreCamera;
        private readonly Vector3[] _vertices=new Vector3[6];
        private readonly Vector2[] _uv={new Vector2(0,0),new Vector2(0,1),new Vector2(.5f,0),new Vector2(.5f,1),new Vector2(1,0),new Vector2(1,1)};
        private static readonly Quaternion LeftPalmRotation=Quaternion.LookRotation(new Vector3(.25f,.97f,0),Vector3.back);
        private static readonly Quaternion RightPalmRotation=Quaternion.LookRotation(new Vector3(-.25f,.97f,0),Vector3.back);
        public bool Active { get; private set; }
        public bool Closing { get; private set; }
        public bool Ready=>Active && !Closing && Progress>=1;
        public float Progress { get; private set; }
        public Transform Paper { get; private set; }

        public void Build(FishingPrototype c)
        {
            Cancel();ReleasePaper();_c=c;
        }

        public void Open()
        {
            if(_c==null || XRSettings.isDeviceActive)return;
            if(!Active)
            {
                _restoreCamera=_c.FirstPerson && _c.viewCamera!=null;
                if(_restoreCamera)_cameraRotation=_c.viewCamera.transform.rotation;
            }
            Progress=0;
            EnsurePaper();Active=true;Closing=false;
            Paper.gameObject.SetActive(true);
        }

        public void Close()
        {
            if(Active)Closing=true;
        }

        public void Cancel()
        {
            if(_restoreCamera && _c!=null && _c.viewCamera!=null)_c.viewCamera.transform.rotation=_cameraRotation;
            _restoreCamera=false;
            Active=false;Closing=false;Progress=0;
            if(Paper!=null)Paper.gameObject.SetActive(false);
        }

        public void Tick(float unscaledDelta)
        {
            if(!Active)return;
            if(XRSettings.isDeviceActive){Cancel();return;}
            if(float.IsNaN(unscaledDelta) || float.IsInfinity(unscaledDelta) || unscaledDelta<=0)return;
            Progress=Mathf.MoveTowards(Progress,Closing?0:1,unscaledDelta/(Closing?CloseSeconds:OpenSeconds));
            if(Closing && Progress<=0)Cancel();
        }

        public void Present()
        {
            if(!Active || Paper==null || _c==null || _c.Hands==null || _c.Shore?.Character==null)return;
            if(XRSettings.isDeviceActive){Cancel();return;}
            var character=_c.Shore.Character;
            if(!character.HandRigReady)return;
            // Keep the paper below eye height; its local north remains toward the fingertips.
            float raise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.85f,Progress));
            float unfold=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,Progress));
            if(_restoreCamera && _c.viewCamera!=null)
                _c.viewCamera.transform.rotation=Quaternion.Slerp(_cameraRotation,
                    Quaternion.Euler(22,character.transform.eulerAngles.y,0),raise);
            _c.Hands.GetToolFrame(out var origin,out var frame);
            if(_restoreCamera)frame=Quaternion.Euler(0,character.transform.eulerAngles.y,0);
            var stowed=new Vector3(.19f,-.43f,.12f);
            var reading=new Vector3(0,.015f,.36f);
            Paper.SetPositionAndRotation(origin+frame*Vector3.Lerp(stowed,reading,raise),
                frame*Quaternion.Euler(Mathf.Lerp(76,8,raise),0,Mathf.Lerp(-24,0,raise)));
            float halfWidth=Mathf.Lerp(.025f,.19f,unfold);
            UpdatePaper(halfWidth,unfold);
            // The hand backs face the reader while curled fingers hold the paper's lower edges.
            float gripDepth=Mathf.Lerp(.13f,0,unfold)-.025f;
            var left=new Vector3(-halfWidth+.01f,-.095f,gripDepth);
            var right=new Vector3(halfWidth-.01f,-.095f,gripDepth);
            _c.Hands.FitToolPose(Paper,left,right,LeftPalmRotation,RightPalmRotation,Progress>=.5f,true);
            _c.Hands.Right.SetFromGrip(Paper,right,RightPalmRotation,.38f);
            _c.Hands.Right.SetFingers(new FishingFingerPose(.6f,.66f,.7f,.72f,.55f));
            if(Progress>=.5f)
            {
                _c.Hands.Left.SetFromGrip(Paper,left,LeftPalmRotation,.38f);
                _c.Hands.Left.SetFingers(new FishingFingerPose(.6f,.66f,.7f,.72f,.55f));
            }
            else
            {
                var rest=character.ShoulderPosition(true)+frame*new Vector3(character.HandSide(true)*.04f,-.3f,.08f);
                float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.5f,Progress));
                _c.Hands.Left.SetPose(Vector3.Lerp(rest,Paper.TransformPoint(left),reach),
                    Quaternion.Slerp(frame,Paper.rotation*LeftPalmRotation,reach),Mathf.Lerp(.12f,.38f,reach));
            }
            _c.Hands.Left.SetForearmRoll(.4f);_c.Hands.Right.SetForearmRoll(.4f);
        }

        private void EnsurePaper()
        {
            if(Paper!=null)return;
            var go=new GameObject("Held folded voyage chart",typeof(MeshFilter),typeof(MeshRenderer));
            Paper=go.transform;Paper.SetParent(transform,false);
            _mesh=new Mesh{name="Folded paper chart - 4 triangles"};
            _mesh.MarkDynamic();UpdatePaper(.025f,0);_mesh.uv=_uv;
            _mesh.triangles=new[]{0,1,2,2,1,3,2,3,4,4,3,5};
            _mesh.colors=new[]{Color.white,Color.white,new Color(.92f,.91f,.88f),new Color(.92f,.91f,.88f),Color.white,Color.white};
            _mesh.RecalculateNormals();go.GetComponent<MeshFilter>().sharedMesh=_mesh;
            var map=Resources.Load<FishingMapDefinition>("FishingMap");
            var shader=Resources.Load<Shader>("FishingTerrainMap");
            _material=new Material(shader!=null?shader:Shader.Find("Universal Render Pipeline/Unlit"))
                {name="Held paper chart",hideFlags=HideFlags.HideAndDontSave};
            if(shader!=null)
            {
                _material.SetFloat("_Circular",0);_material.SetFloat("_HasMap",map!=null && map.IsUsable?1:0);
                _material.SetVector("_ViewUV",new Vector4(0,0,1,1));_material.SetFloat("_ZTest",4);
                FishingUITheme.StyleMap(_material);
            }
            else
            {
                _material.SetColor("_BaseColor",new Color(.9f,.85f,.7f));_material.SetFloat("_Cull",0);
            }
            if(map!=null && map.IsUsable)
            {
                _material.mainTexture=map.texture;
                if(_material.HasProperty("_BaseMap"))_material.SetTexture("_BaseMap",map.texture);
            }
            _material.renderQueue=2450;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private void UpdatePaper(float halfWidth,float unfold)
        {
            float edgeDepth=Mathf.Lerp(.13f,0,unfold);
            _vertices[0]=new Vector3(-halfWidth,-.15f,edgeDepth);_vertices[1]=new Vector3(-halfWidth,.15f,edgeDepth);
            _vertices[2]=new Vector3(0,-.15f,-.006f);_vertices[3]=new Vector3(0,.15f,-.006f);
            _vertices[4]=new Vector3(halfWidth,-.15f,edgeDepth);_vertices[5]=new Vector3(halfWidth,.15f,edgeDepth);
            _mesh.vertices=_vertices;_mesh.RecalculateBounds();
        }

        private void ReleasePaper()
        {
            if(Paper!=null){Destroy(Paper.gameObject);Paper=null;}
            if(_mesh!=null){Destroy(_mesh);_mesh=null;}
            if(_material!=null){Destroy(_material);_material=null;}
        }

        private void OnDestroy(){Cancel();ReleasePaper();}
    }
}
