using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>使用原美女店员模型，交谈由居民系统处理，交易由原商店处理。</summary>
    public sealed class FishingShopCashier : MonoBehaviour
    {
        private static readonly string[] Activities={"整理柜台","舒展手臂","环顾店铺","放松等候"};
        public const string ResourcePath = "FishingCashier/FantasyGirl/Cashier";
        public const float AdultHeight = 1.98f;
        public Vector3 FacePosition => transform.TransformPoint(new Vector3(0, 1.80f, .07f));
        private FishingPrototype _controller;
        private Transform[] _joints;
        private Mesh[] _animatedMeshes;
        private float _clock,_greetingAt=-20,_nextGreeting;
        private bool _near;
        private int _activity;
        private float _activityUntil,_activityBlend;
        public bool Animated => _joints!=null;
        public string Gesture {get;private set;}="待机";

        public void Bind(FishingPrototype controller)
        {
            _controller=controller;
            if(_joints!=null)return;
            // The supplied model is a static posed mesh. Add a small GPU skin to both LODs;
            // rotating an empty Animator or the collision root would leave the character rigid.
            var pivots=new[]{Vector3.zero,new Vector3(0,1.05f,0),new Vector3(0,1.64f,.02f),
                new Vector3(-.20f,1.50f,.04f),new Vector3(.20f,1.50f,.04f),
                new Vector3(-.30f,1.31f,.10f),new Vector3(.30f,1.31f,.10f)};
            _joints=new Transform[pivots.Length];
            for(int i=0;i<_joints.Length;i++)
            {
                var joint=new GameObject(new[]{"Planted feet","Breathing torso","Head and gaze","Left conversational arm","Right conversational arm","Left forearm","Right forearm"}[i]).transform;
                joint.SetParent(transform,false);joint.localPosition=pivots[i];_joints[i]=joint;
            }
            _joints[2].SetParent(_joints[1],true);
            for(int i=3;i<=4;i++){_joints[i].SetParent(_joints[1],true);_joints[i+2].SetParent(_joints[i],true);}
            var group=GetComponent<LODGroup>();var lods=group.GetLODs();_animatedMeshes=new Mesh[lods.Length];
            for(int lod=0;lod<lods.Length;lod++)
            {
                var original=lods[lod].renderers[0];var filter=original.GetComponent<MeshFilter>();
                var mesh=Instantiate(filter.sharedMesh);mesh.name=filter.sharedMesh.name+" cashier gesture skin";
                var vertices=mesh.vertices;var weights=new BoneWeight[vertices.Length];var bind=new Matrix4x4[_joints.Length];
                for(int i=0;i<bind.Length;i++)bind[i]=_joints[i].worldToLocalMatrix*original.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=transform.InverseTransformPoint(original.transform.TransformPoint(vertices[i]));
                    float head=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.59f,1.73f,p.y));
                    float torso=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.92f,1.30f,p.y))*(1-head);
                    float arm=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.13f,.24f,Mathf.Abs(p.x)))
                        *Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.12f,1.23f,p.y))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.45f,1.58f,p.y)));
                    arm*=1-head;torso*=1-arm;
                    float forearm=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.46f,1.25f,p.y));int side=p.x<0?3:4;
                    weights[i]=arm>0?GestureWeights(Mathf.Max(0,1-torso-arm),torso,arm*(1-forearm),arm*forearm,side+2,side)
                        :GestureWeights(Mathf.Max(0,1-head-torso),torso,head,0,side);
                }
                mesh.boneWeights=weights;mesh.bindposes=bind;mesh.UploadMeshData(true);_animatedMeshes[lod]=mesh;
                var visual=new GameObject("Animated cashier LOD "+lod);visual.transform.SetParent(original.transform,false);
                var skin=visual.AddComponent<SkinnedMeshRenderer>();skin.bones=_joints;skin.rootBone=_joints[0];skin.quality=SkinQuality.Bone4;
                skin.sharedMesh=mesh;skin.sharedMaterials=original.sharedMaterials;
                skin.shadowCastingMode=original.shadowCastingMode;skin.receiveShadows=original.receiveShadows;
                var bounds=filter.sharedMesh.bounds;bounds.Expand(.55f);skin.localBounds=bounds;
                original.enabled=false;lods[lod].renderers=new Renderer[]{skin};
            }
            group.SetLODs(lods);group.RecalculateBounds();
        }

        private static void OrderWeight(ref float a,ref int ai,ref float b,ref int bi)
        {if(a>=b)return;float value=a;a=b;b=value;int index=ai;ai=bi;bi=index;}
        private static BoneWeight GestureWeights(float feet,float torso,float head,float arm,int side,int body=2)
        {
            // Unity requires descending weights, including when baking a four-weight skin on
            // a platform whose global quality setting uses only the first two influences.
            int a=0,b=1,c=body,d=side;
            OrderWeight(ref feet,ref a,ref torso,ref b);OrderWeight(ref head,ref c,ref arm,ref d);
            OrderWeight(ref feet,ref a,ref head,ref c);OrderWeight(ref torso,ref b,ref arm,ref d);
            OrderWeight(ref torso,ref b,ref head,ref c);
            float sum=feet+torso+head+arm;
            return new BoneWeight{boneIndex0=a,weight0=feet/sum,boneIndex1=b,weight1=torso/sum,
                boneIndex2=c,weight2=head/sum,boneIndex3=d,weight3=arm/sum};
        }

        private void LateUpdate()
        {
            if(_controller==null || _joints==null || _controller.Paused)return;
            _clock+=Mathf.Min(Time.deltaTime,.05f);
            bool nearby=_controller.Ashore && _controller.TackleShop.Inside;
            if(nearby && !_near && _clock>=_nextGreeting){_greetingAt=_clock;_nextGreeting=_clock+12;}
            _near=nearby;
            bool talking=_controller.Residents!=null && _controller.Residents.Open && _controller.Residents.ActiveId==4;
            bool speaking=talking && _controller.Residents.Typing;
            float greeting=Mathf.Clamp01(1-(_clock-_greetingAt)/2.4f);
            // Like residents' work/fidget loops, short activities cross-fade and yield to conversation.
            if(!talking && greeting<=0 && _clock>=_activityUntil)
            {_activity=(_activity+1)%4;_activityUntil=_clock+Random.Range(4f,7f);_activityBlend=0;}
            _activityBlend=Mathf.MoveTowards(_activityBlend,1,Time.deltaTime*2);
            Gesture=greeting>0?"招手欢迎":speaking?"交谈":talking?"倾听":Activities[_activity];
            float gaze=0;
            if(nearby)
            {
                var to=transform.InverseTransformDirection(_controller.viewCamera.transform.position-FacePosition);
                gaze=Mathf.Clamp(Mathf.Atan2(to.x,to.z)*Mathf.Rad2Deg,-24,24);
            }
            else gaze=Mathf.Sin(_clock*.25f)*8;
            float nod=greeting*Mathf.Sin((_clock-_greetingAt)*5)*7+(speaking?Mathf.Sin(_clock*5)*2.7f:talking?3:0);
            float idle=!talking && greeting<=0?_activityBlend:0;
            float work=idle*(_activity==0?1:0),stretch=idle*(_activity==1?1:0);
            float wave=greeting*Mathf.Sin(Mathf.Clamp01((_clock-_greetingAt)/2.4f)*Mathf.PI);
            _joints[1].localRotation=Quaternion.Euler(Mathf.Sin(_clock*1.6f)*.7f,Mathf.Sin(_clock*.6f)*1.3f,Mathf.Sin(_clock*.8f)*.7f);
            _joints[1].localScale=new Vector3(1,1+Mathf.Sin(_clock*1.6f)*.0025f,1);
            _joints[2].localRotation=Quaternion.Slerp(_joints[2].localRotation,Quaternion.Euler(nod,gaze,Mathf.Sin(_clock*.7f)*1.3f),1-Mathf.Exp(-Time.deltaTime*5));
            // shortcut: The remeshed hands meet waist ornaments; keep gestures close until a segmented hand rig is supplied.
            var left=Quaternion.Euler(-work*(8+Mathf.Sin(_clock*2.2f)*4)-stretch*5,stretch*8,-stretch*10);
            var right=Quaternion.Euler(-wave*16-work*8-stretch*5,0,wave*10+stretch*10);
            float blend=1-Mathf.Exp(-Time.deltaTime*5);
            _joints[3].localRotation=Quaternion.Slerp(_joints[3].localRotation,left,blend);
            _joints[4].localRotation=Quaternion.Slerp(_joints[4].localRotation,right,blend);
            _joints[5].localRotation=Quaternion.Slerp(_joints[5].localRotation,Quaternion.Euler(speaking?-8+Mathf.Sin(_clock*3.4f)*5:work*Mathf.Sin(_clock*2.2f)*5,0,stretch*12+work*6+(speaking?8:0)),blend);
            _joints[6].localRotation=Quaternion.Slerp(_joints[6].localRotation,Quaternion.Euler(-wave*12+(speaking?-8+Mathf.Sin(_clock*3.4f+1)*6:0),wave*Mathf.Sin(_clock*7)*10,-wave*(22+Mathf.Sin(_clock*7)*6)-stretch*12-work*6-(speaking?10:0)),blend);
        }

        private void OnDestroy()
        {
            if(_animatedMeshes!=null)foreach(var mesh in _animatedMeshes)if(mesh!=null)Destroy(mesh);
        }

        public static FishingShopCashier Create(FishingTackleShop shop)
        {
            if (shop == null) return null;
            // 同时检查预先放置及未激活的子物体，避免重载或重复 Bind
            // 在同一预留站位生成第二名店员。
            var existing = shop.GetComponentInChildren<FishingShopCashier>(true);
            if (existing != null) return existing;
            if (shop.clerkSpaces == null || shop.clerkSpaces.Length == 0 || shop.clerkSpaces[0] == null)
            {
                Debug.LogError("[CASHIER] The shop has no first clerk anchor.", shop);
                return null;
            }
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null || prefab.GetComponent<FishingShopCashier>() == null)
            {
                Debug.LogError("[CASHIER] FantasyGirl cashier prefab missing or invalid. Restore FishingCashier/FantasyGirl and validate before building the Player.", shop);
                return null;
            }
            var instance = Instantiate(prefab, shop.clerkSpaces[0], false);
            instance.name = "Cashier - FantasyGirl";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0, 180, 0);
            instance.transform.localScale = Vector3.one;
            return instance.GetComponent<FishingShopCashier>();
        }
    }
}
