using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Desktop and tracked hunting use muzzle clearance and persistent per-animal receipts.</summary>
    public sealed class FishingRifleHunting : MonoBehaviour
    {
        public const float Range=45f, ShotInterval=.65f;
        public static readonly Vector3 RightGrip=new Vector3(.022f,0,0);
        public static readonly Vector3 LeftGrip=new Vector3(0,-.030f,.095f);
        public static readonly Vector3 MuzzleLocal=new Vector3(0,.069f,.447f);
        private FishingPrototype _c;
        private Transform _rifle;
        private MeshFilter _mesh;
        private Vector3[] _vertices;
        private readonly List<FishingFieldWorld.Animal> _targets=new List<FishingFieldWorld.Animal>();
        private readonly Dictionary<Transform,Vector4> _finMotion=new Dictionary<Transform,Vector4>();
        public IReadOnlyList<FishingFieldWorld.Animal> Targets=>_targets;
        private LineRenderer _trace;
        private Material _traceMaterial;
        private AudioSource _audio;
        private AudioClip _shot,_ownedShotFallback;
        private AudioClip[] _shots;
        private int _lastShot=-1;
        private float _nextShot,_flashUntil;
        private readonly Collider[] _overlaps=new Collider[32];
        private readonly RaycastHit[] _sweepHits=new RaycastHit[64];
        public int ShotsFired { get; private set; }
        public bool Obstructed { get; private set; }
        public string ObstructionReason { get; private set; }="";
        public bool Presented=>_rifle!=null && _rifle.gameObject.activeInHierarchy;
        public Transform Rifle=>_rifle;
        public Vector3 Muzzle=>_rifle.TransformPoint(MuzzleLocal);
        public bool Equipped=>_c!=null && _c.Career.Backpack.Equipped==FieldItem.HuntingRifle;
        private Vector3 Eye=>XRSettings.isDeviceActive?_c.viewCamera.transform.position:_c.Shore.Character.EyePosition;
        private bool Context=>Equipped && !_c.InspectingItem && _c.Ashore && !_c.Paused && !_c.InventoryOpen && !_c.JournalOpen
            && !_c.ShopBlocksGameplay && !_c.StoryBlocksGameplay && !_c.Shore.Swimming && !_c.Shore.Climbing
            && !_c.WorldActionBlocksGameplay && !_c.ActionSuppressesHeldTool && !_c.HasPendingCatch && !_c.Expedition.Aligning
            && !_c.DockConsole.BlocksGameplay && (_c.XRNotebook==null || !_c.XRNotebook.BlocksGameplay) && _c.Hands.CanPresentTools
            && (!XRSettings.isDeviceActive || (!_c.XRSeat.HeadBlocked && Application.isFocused
                && _c.Hands.TryControllerPalm(false,out _,out _)));

        public void Build(FishingPrototype controller)
        {
            _c=controller;
            var prefab=Resources.Load<GameObject>("FishingHunting/HuntingRifle");
            if(prefab!=null){_rifle=Instantiate(prefab,transform).transform;_rifle.localScale=Vector3.one*.82f;_mesh=_rifle.GetComponentInChildren<MeshFilter>();_vertices=_mesh.sharedMesh.vertices;_rifle.gameObject.SetActive(false);}
            foreach(var animal in _c.FieldWorld.Animals)
            {
                Register(animal);
            }
            _audio=gameObject.AddComponent<AudioSource>();_audio.playOnAwake=false;_audio.spatialBlend=0;_audio.volume=.23f;
            _c.RegisterAudioSource(_audio,FishingAudioCategory.Effects,.23f);
            var firstShot=Resources.Load<AudioClip>("FishingAudio/SFX/RifleShot1");
            var secondShot=Resources.Load<AudioClip>("FishingAudio/SFX/RifleShot2");
            if(firstShot!=null || secondShot!=null)
                _shots=firstShot!=null && secondShot!=null?new[]{firstShot,secondShot}:new[]{firstShot!=null?firstShot:secondShot};
            _shot=_shots!=null?_shots[0]:Resources.Load<AudioClip>("FishingAudio/SFX/RifleShot");
            if(_shot==null)
            {
                _ownedShotFallback=AudioClip.Create("Hunting report",7056,1,44100,false);
                var samples=new float[7056];var random=new System.Random(71);
                for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=(float)((random.NextDouble()*2-1)*Math.Exp(-t*45)+.3*Math.Sin(t*1050)*Math.Exp(-t*25));}
                _ownedShotFallback.SetData(samples,0);_shot=_ownedShotFallback;
            }
            if(_shots==null)_shots=new[]{_shot};
            _trace=new GameObject("Hunting shot trace").AddComponent<LineRenderer>();_trace.transform.SetParent(transform,false);
            _traceMaterial=new Material(_c.PaletteShader);_traceMaterial.SetFloat("_Unlit",1);_traceMaterial.SetColor("_BaseColor",new Color(1,.78f,.32f));
            _trace.sharedMaterial=_traceMaterial;_trace.positionCount=2;_trace.startWidth=.012f;_trace.endWidth=.003f;_trace.enabled=false;
            RefreshAnimals();
        }
        public void RefreshAnimals()
        {
            foreach(var animal in _targets)
            {
                var receipt=_c.Career.HuntedAnimal(animal.Id);bool wasDead=animal.Dead;animal.Dead=receipt!=null;
                animal.Hitbox.enabled=!animal.Dead;
                if(receipt!=null)
                {
                    if(receipt.collected){animal.Root.gameObject.SetActive(false);continue;}
                    animal.Feet=new Vector3(receipt.x,receipt.y,receipt.z);animal.Target=animal.Feet;animal.Fleeing=false;
                    animal.Root.SetPositionAndRotation(animal.Feet+Vector3.up*.12f,Quaternion.Euler(0,receipt.yaw,animal.Id>=8?8:85));
                    var renderer=animal.Root.GetComponent<Renderer>();
                    animal.Root.position+=Vector3.up*Mathf.Max(0,animal.Feet.y+.02f-renderer.bounds.min.y);
                    if(!wasDead){var motion=new MaterialPropertyBlock();renderer.GetPropertyBlock(motion);motion.SetVector("_FinMotion",Vector4.zero);renderer.SetPropertyBlock(motion);}
                    animal.Root.gameObject.SetActive(true);
                }
                else if(wasDead)
                {animal.Feet=animal.Home;animal.Target=animal.Home;animal.Root.SetPositionAndRotation(animal.Home,Quaternion.identity);animal.Root.gameObject.SetActive(true);
                    var renderer=animal.Root.GetComponent<Renderer>();var motion=new MaterialPropertyBlock();renderer.GetPropertyBlock(motion);motion.SetVector("_FinMotion",_finMotion[animal.Root]);renderer.SetPropertyBlock(motion);}
            }
        }
        private void Register(FishingFieldWorld.Animal animal)
        {
            var box=animal.Root.gameObject.AddComponent<BoxCollider>();
            var bounds=animal.Root.GetComponent<MeshFilter>().sharedMesh.bounds;
            box.center=bounds.center;box.size=bounds.size;box.isTrigger=true;animal.Hitbox=box;
            _targets.Add(animal);
            var properties=new MaterialPropertyBlock();animal.Root.GetComponent<Renderer>().GetPropertyBlock(properties);_finMotion[animal.Root]=properties.GetVector("_FinMotion");
        }
        private void Update()
        {
            if(_c==null || _c.Paused || (!_c.Automated && !Application.isFocused) || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay
                || (_c.XRNotebook!=null && _c.XRNotebook.BlocksGameplay))return;
            if(XRSettings.isDeviceActive && (!Application.isFocused || _c.XRSeat.HeadBlocked
                || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out _,out _)
                || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out _,out _)))return;
            if(_c.Career.TickHuntedRespawns(Time.deltaTime)){RefreshAnimals();_c.Save();}
        }
        public void RegisterMarine(MarineKind kind,int index,Transform root)
        {
            if(kind!=MarineKind.Gull && kind!=MarineKind.Turtle)return;
            int id=kind==MarineKind.Gull?FishingAnimalLoot.GullStart+index:FishingAnimalLoot.SeaTurtleId;
            Register(new FishingFieldWorld.Animal{Id=id,Kind=kind==MarineKind.Gull?LandAnimalKind.Gull:LandAnimalKind.SeaTurtle,Root=root,Home=root.position,Feet=root.position});
            RefreshAnimals();
        }
        public bool DeadWildlife(Transform root)
        {foreach(var a in _targets)if(a.Root==root)return a.Dead;return false;}
        private Vector3 LandingFor(FishingFieldWorld.Animal animal)
        {
            if(animal.Id<8)return animal.Feet;
            var start=animal.Root.position;
            var groundProbe=new Vector3(start.x,0,start.z);
            if(_c.Shore.Ground(groundProbe,out var ground) && ground.y<=start.y+.6f && _c.Shore.Clearance(ground))return ground;
            // Pelagic targets leave recoverable remains at the nearest safe bank.
            Vector3 best=_c.ExplorerPosition;float distance=float.MaxValue;
            for(int radius=2;radius<=30;radius+=2)for(int angle=0;angle<360;angle+=30)
            {
                var probe=start+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
                probe.y=0;
                if(_c.Shore.Ground(probe,out ground) && _c.Shore.Clearance(ground) && (ground-start).sqrMagnitude<distance)
                {distance=(ground-start).sqrMagnitude;best=ground;}
            }
            return best;
        }
        private bool Ignored(Collider collider)=>collider.transform.IsChildOf(_c.Shore.Actor)
            || collider.transform.IsChildOf(_c.Player.transform) || FishingShore.NonSolidSurface(collider);
        private bool SolidSegment(Vector3 from,Vector3 to,float radius)
        {
            var delta=to-from;
            int count=Physics.OverlapSphereNonAlloc(from,radius,_overlaps,~(1<<4),QueryTriggerInteraction.Ignore);
            if(count==_overlaps.Length)return true;
            for(int i=0;i<count;i++)if(!Ignored(_overlaps[i]))return true;
            if(delta.sqrMagnitude<.000001f)return false;
            count=Physics.SphereCastNonAlloc(from,radius,delta.normalized,_sweepHits,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore);
            if(count==_sweepHits.Length)return true;
            for(int i=0;i<count;i++)if(!Ignored(_sweepHits[i].collider))return true;
            return false;
        }
        private bool ClearModel()
        {
            var eye=Eye;
            if(SolidSegment(eye,Muzzle,.045f)){ObstructionReason="muzzle clearance";return false;}
            var b=_mesh.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var p=_mesh.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(SolidSegment(_rifle.position,p,.012f)){ObstructionReason="model clearance";return false;}
            }
            // Inspect the actual mesh, not empty AABB corners above the stock.
            var meshToCamera=_c.viewCamera.worldToCameraMatrix*_mesh.transform.localToWorldMatrix;
            if(_c.FirstPerson || XRSettings.isDeviceActive)foreach(var vertex in _vertices)
            {
                var p=meshToCamera.MultiplyPoint3x4(vertex);p.z=-p.z;
                if(p.z<_c.viewCamera.nearClipPlane+.008f && Mathf.Abs(p.x)<.06f && Mathf.Abs(p.y)<.06f){ObstructionReason="eye clearance";return false;}
            }
            return true;
        }
        public void Present()
        {
            if(_trace!=null)_trace.enabled=Time.time<_flashUntil && !_c.Paused && Context;
            if(_rifle==null)return;
            bool visible=Context && !_c.Hands.HasGesture && !_c.Hands.PresentingPickup;
            _rifle.gameObject.SetActive(visible);Obstructed=false;ObstructionReason="";if(!visible)return;
            bool tracked=XRSettings.isDeviceActive;
            _c.Hands.GetToolFrame(out _,out var frame);
            var eye=Eye;
            // Keep the rear grip at chest height while aiming. Rotating the entire
            // eye-to-grip offset upward pushed the stock through the face.
            var holdFrame=Quaternion.Euler(0,frame.eulerAngles.y,0);
            _rifle.SetPositionAndRotation(eye+holdFrame*new Vector3(.13f,-.17f,.32f),frame*Quaternion.Euler(0,-14,5));
            var leftRotation=Quaternion.LookRotation(Vector3.right,Vector3.down);
            var rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.right);
            bool leftHeld=true;
            if(tracked)
            {
                if(!_c.Hands.TryControllerPalm(false,out var palm,out var palmFrame)){_rifle.gameObject.SetActive(false);return;}
                var rotation=palmFrame*Quaternion.Inverse(rightRotation);
                _rifle.SetPositionAndRotation(palm-rotation*Vector3.Scale(RightGrip,_rifle.localScale),rotation);
                leftHeld=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).TryGetFeatureValue(CommonUsages.grip,out float grip) && grip>.55f
                    && _c.Hands.TryControllerPalm(true,out var leftPalm,out _)
                    && Vector3.Distance(leftPalm,_rifle.TransformPoint(LeftGrip))<.18f;
            }
            bool fit=tracked || _c.Hands.FitToolPose(_rifle,LeftGrip,RightGrip,leftRotation,rightRotation);
            if(!fit)ObstructionReason="hand reach";
            Obstructed=!fit || !ClearModel();
            if(Obstructed)
            {
                // A tracked weapon cannot be moved away from its physical controller to clear a wall.
                if(tracked){_rifle.gameObject.SetActive(false);return;}
                _rifle.SetPositionAndRotation(eye+frame*new Vector3(.025f,-.24f,.25f),frame*Quaternion.Euler(55,0,0));
                if(!_c.Hands.FitToolPose(_rifle,LeftGrip,RightGrip,leftRotation,rightRotation) || !ClearModel())
                {_rifle.gameObject.SetActive(false);_c.Hands.RestHand(true);_c.Hands.RestHand(false);return;}
            }
            _c.Hands.SetToolTarget(false,_rifle,RightGrip,Quaternion.LookRotation(Vector3.forward,Vector3.right),.72f,fingers:new FishingFingerPose(.25f,.8f,.82f,.85f,.62f));
            if(leftHeld)
            {
                if(tracked)_c.Hands.Left.SetFingers(new FishingFingerPose(.4f,.45f,.5f,.55f,.5f));
                else _c.Hands.SetToolTarget(true,_rifle,LeftGrip,Quaternion.LookRotation(Vector3.right,Vector3.down),.45f,fingers:new FishingFingerPose(.4f,.45f,.5f,.55f,.5f));
            }
        }
        private bool FirstHit(Vector3 start,Vector3 direction,float distance,out RaycastHit closest)
        {
            closest=default;bool found=false;
            foreach(var hit in Physics.RaycastAll(start,direction,distance,~(1<<4),QueryTriggerInteraction.Collide))
            {
                if(Ignored(hit.collider))continue;
                bool animal=false;foreach(var a in _targets)if(!a.Dead && a.Hitbox==hit.collider){animal=true;break;}
                if(hit.collider.isTrigger && !animal)continue;
                if(!found || hit.distance<closest.distance){closest=hit;found=true;}
            }
            return found;
        }
        public bool TryFire()
        {
            if(!Context || Time.time<_nextShot)return false;
            bool tracked=XRSettings.isDeviceActive;
            var device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if(tracked && (!device.TryGetFeatureValue(CommonUsages.triggerButton,out bool held) || !held))return false;
            Present();
            if(!Presented || Obstructed){_c.Notice("枪口前空间不足，请离开障碍物后再射击。",2);return false;}
            var camera=_c.viewCamera.transform;var direction=camera.forward;var aim=Muzzle+_rifle.forward*Range;
            if(!tracked)
            {
                aim=camera.position+direction*Range;
                if(FirstHit(camera.position,direction,Range,out var aimHit))aim=aimHit.point;
            }
            // The player's body/eye must also see the target in third person.
            var eye=Eye;
            if(SolidSegment(eye,Muzzle,.035f))return false;
            _nextShot=Time.time+ShotInterval;ShotsFired++;_flashUntil=Time.time+.06f;
            var delta=aim-Muzzle;var end=Muzzle+delta.normalized*Mathf.Min(Range,delta.magnitude);
            bool killed=false;
            if(FirstHit(Muzzle,delta.normalized,Mathf.Min(Range,delta.magnitude+.03f),out var hit))
            {
                end=hit.point;
                foreach(var animal in _targets)
                    if(!animal.Dead && animal.Hitbox==hit.collider && _c.Shore.ClearSight(eye,hit.point))
                    {
                        var landing=LandingFor(animal);
                        killed=_c.Career.KillLandAnimal(animal.Id,animal.Kind,landing.x,landing.y,landing.z,animal.Root.eulerAngles.y);
                        if(killed){_c.Save();RefreshAnimals();_c.Notice("击倒 "+FishingBackpack.AnimalName(animal.Kind)+" · "+(animal.Id>=8?"附近岸边":"靠近后")+(tracked?" A 收取 ":" E 收取 ")+FishingAnimalLoot.Description(animal.Kind),5);}
                        break;
                    }
            }
            _trace.SetPosition(0,Muzzle);_trace.SetPosition(1,end);_trace.enabled=true;
            _lastShot=(_lastShot+1)%_shots.Length;_shot=_shots[_lastShot];
            if(!_c.QuietMode)_audio.PlayOneShot(_shot);
            if(tracked && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)device.SendHapticImpulse(0,.25f,.06f);
            if(!killed)_c.Notice("未命中动物",1);
            return true;
        }
        public bool TryLootTarget(out FishingFieldWorld.Animal target)
        {
            target=null;if(!_c.Ashore)return false;float nearest=2.2f;
            foreach(var a in _targets)
            {
                if(!a.Dead || !a.Root.gameObject.activeInHierarchy)continue;
                float distance=Vector3.Distance(_c.ExplorerPosition,a.Feet);
                if(distance<nearest && Vector3.Dot(_c.viewCamera.transform.forward,(a.Root.position-_c.viewCamera.transform.position).normalized)>.15f
                    && _c.Shore.ClearSight(Eye,a.Root.position+Vector3.up*.1f))
                {nearest=distance;target=a;}
            }
            return target!=null;
        }
        public bool CollectAnimal(int id)
        {
            if(_c.Paused || !_c.Ashore || _c.ShopBlocksGameplay || _c.StoryBlocksGameplay)return false;
            FishingFieldWorld.Animal animal=null;foreach(var a in _targets)if(a.Id==id){animal=a;break;}
            if(animal==null || !animal.Dead || !animal.Root.gameObject.activeInHierarchy || Vector3.Distance(_c.ExplorerPosition,animal.Feet)>2.2f
                || !_c.Shore.ClearSight(Eye,animal.Root.position+Vector3.up*.1f))return false;
            if(!_c.Career.CollectHuntedAnimal(id)){_c.Notice(XRSettings.isDeviceActive?"背包空间不足，猎物保留在地上；B 打开背包整理后再收取。":"背包空间不足，猎物保留在地上；I 整理背包后再收取。",4);return false;}
            _c.Save();RefreshAnimals();_c.Notice("已收取 "+FishingAnimalLoot.Description(animal.Kind),4);return true;
        }
        private void OnDestroy(){if(_ownedShotFallback!=null)Destroy(_ownedShotFallback);if(_traceMaterial!=null)Destroy(_traceMaterial);}
    }
}
