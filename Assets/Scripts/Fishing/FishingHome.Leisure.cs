using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHome
    {
        private Transform _sofa;
        private Material _film;
        private Renderer _screen;
        private Mesh _filmSurface;
        private Vector3 _sofaForward;
        private Quaternion _sofaFacing;
        private float _sitAge,_leaveAge=-1;
        private Vector3 _loungeCamera,_loungeHip;
        private Quaternion _loungeRotation;
        private readonly FishingPressEdge _loungePrimary=new FishingPressEdge(),_loungeSecondary=new FishingPressEdge();
        public bool Lounging {get;private set;}
        public bool TelevisionOn {get;private set;}
        public Renderer TelevisionScreen=>_screen;
        public Vector3 LoungeEyeOffset {get;private set;}
        public Vector3 LoungeStance=>Origin+new Vector3(-1.25f,.2f,-3.05f);

        private void BuildLeisure()
        {
            Furnish("cabinetBedDrawer",new Vector3(-3.2f,.2f,-4.55f),1.55f,0,true,out var televisionStand);
            float standTop=LeisureBounds(televisionStand).max.y-Origin.y;
            var tv=FishingV019Art.Spawn("televisionVintage",House,new Vector3(-3.2f,standTop+.015f,-4.55f),1.18f,0,"FishingV036");
            foreach(var r in tv.GetComponentsInChildren<Renderer>())r.sharedMaterial=Resources.Load<Material>("FishingV023/CoastalPalette");
            var tvBounds=LeisureBounds(tv);
            RegisterInteraction(5,tv,new Vector3(tvBounds.center.x-Origin.x,tvBounds.center.y-Origin.y,tvBounds.max.z-Origin.z+.06f));
            _film=new Material(Resources.Load<Shader>("FishingTideFilm")){name="Tidal picture show"};
            BuildFilmSurface(tv);FitSofaToTelevision();
            var plaque=FishingV019Art.Sign("潮 汐 放 映 机",House,new Vector3(tvBounds.center.x-Origin.x,tvBounds.max.y-Origin.y+.15f,tvBounds.max.z-Origin.z+.025f),.009f,new Color(.88f,.77f,.52f));
            plaque.transform.localRotation=Quaternion.Euler(0,180,0);
            BuildHangingLanterns();
            DecoratePorchFlowers();
            StaticBatchingUtility.Combine(tv.gameObject);
        }
        private void BuildHangingLanterns()
        {
            foreach(var hook in new[]{new Vector3(-1.65f,0,-7.78f),new Vector3(1.65f,0,-7.78f),new Vector3(-5.5f,0,-7.78f),new Vector3(5.5f,0,-7.78f),new Vector3(0,3.21f,1.5f)})
            {
                var p=hook;
                // The porch's curved cedar soffit follows the authored V035 roof profile.
                if(p.y==0){float t=Mathf.Abs(p.x)/6.6f;p.y=2.88f+1.07f*Mathf.Pow(1-t,1.22f)+.10f*Mathf.Sin(t*Mathf.PI*2);}
                var lantern=FishingV019Art.Spawn("HangingLanternView",House,p-Vector3.up*.8f,.8f,0,"FishingV036");
                lantern.name="V036 hanging star-vent lantern";
                var light=new GameObject("Hanging lantern warm pool").AddComponent<Light>();light.transform.SetParent(lantern,false);
                var bounds=LeisureBounds(lantern);light.transform.position=new Vector3(bounds.center.x,bounds.min.y-.08f,bounds.center.z);light.transform.rotation=Quaternion.Euler(90,0,0);
                light.type=LightType.Spot;light.spotAngle=125;light.innerSpotAngle=65;light.range=12;light.renderMode=LightRenderMode.ForcePixel;
                light.color=new Color(1,.75f,.43f);light.shadows=LightShadows.None;_lamps.Add(light);
            }
        }
        private static Bounds LeisureBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;
        }
        private void BuildFilmSurface(Transform television)
        {
            var filter=television.GetComponentInChildren<MeshFilter>();var source=filter.sharedMesh;var vertices=source.vertices;var triangles=source.GetTriangles(0);
            var points=new List<Vector3>();var indices=new List<int>();
            var depths=new float[triangles.Length/3];
            for(int i=0;i<depths.Length;i++)depths[i]=Vector3.Dot(filter.transform.TransformPoint((vertices[triangles[i*3]]+vertices[triangles[i*3+1]]+vertices[triangles[i*3+2]])/3),television.forward);
            System.Array.Sort(depths);float screenDepth=depths[depths.Length/2];
            // shortcut: the broad CRT plane dominates submesh 0; refit this selector when replacing the television.
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=filter.transform.TransformPoint(vertices[triangles[i]]);var b=filter.transform.TransformPoint(vertices[triangles[i+1]]);var c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                if(Mathf.Abs(Vector3.Dot((a+b+c)/3,television.forward)-screenDepth)>.001f)continue;
                foreach(var p in new[]{a,b,c}){indices.Add(points.Count);points.Add(House.InverseTransformPoint(p+television.forward*.004f));}
            }
            if(points.Count<12)throw new System.InvalidOperationException("Television CRT geometry cannot be identified.");
            var screenBounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)screenBounds.Encapsulate(p);
            var uv=new Vector2[points.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(Mathf.InverseLerp(screenBounds.min.x,screenBounds.max.x,points[i].x),Mathf.InverseLerp(screenBounds.min.y,screenBounds.max.y,points[i].y));
            _filmSurface=new Mesh{name="Exact source CRT picture surface"};_filmSurface.SetVertices(points);_filmSurface.SetTriangles(indices,0);_filmSurface.uv=uv;_filmSurface.RecalculateNormals();_filmSurface.RecalculateBounds();
            var screen=new GameObject("Tide film screen",typeof(MeshFilter),typeof(MeshRenderer));screen.transform.SetParent(House,false);screen.GetComponent<MeshFilter>().sharedMesh=_filmSurface;
            _screen=screen.GetComponent<Renderer>();_screen.sharedMaterial=_film;_screen.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;_screen.receiveShadows=false;
        }
        private void FitSofaToTelevision()
        {
            var bounds=LeisureBounds(_sofa);var upper=Vector3.zero;int count=0;
            foreach(var filter in _sofa.GetComponentsInChildren<MeshFilter>())foreach(var vertex in filter.sharedMesh.vertices)
            {var p=filter.transform.TransformPoint(vertex);if(p.y>bounds.min.y+bounds.size.y*.78f){upper+=p;count++;}}
            var back=Vector3.ProjectOnPlane(upper/Mathf.Max(1,count)-bounds.center,Vector3.up);
            _sofaForward=Vector3.ProjectOnPlane(_screen.bounds.center-bounds.center,Vector3.up).normalized;
            if(back.sqrMagnitude>.001f)_sofa.Rotate(Vector3.up,Vector3.SignedAngle(-back,_sofaForward,Vector3.up),Space.World);
            _sofaFacing=Quaternion.LookRotation(_sofaForward);
            bounds=LeisureBounds(_sofa);float cushion=bounds.min.y+bounds.size.y*.52f;
            var side=Vector3.Cross(Vector3.up,_sofaForward);
            foreach(var filter in _sofa.GetComponentsInChildren<MeshFilter>())foreach(var vertex in filter.sharedMesh.vertices)
            {
                var p=filter.transform.TransformPoint(vertex);var delta=p-bounds.center;
                if(Mathf.Abs(Vector3.Dot(delta,side))<bounds.size.x*.15f && Vector3.Dot(delta,_sofaForward)>.03f && p.y<bounds.min.y+bounds.size.y*.72f)cushion=Mathf.Max(cushion,p.y);
            }
            _loungeHip=bounds.center+_sofaForward*.12f;_loungeHip.y=cushion+.06f;
            RegisterInteraction(4,_sofa,House.InverseTransformPoint(_loungeHip+_sofaForward*.25f-side*.85f));
        }
        public bool Sit()
        {
            if(Lounging || Sleeping || !_c.Ashore || _c.Paused || _c.StoryBlocksGameplay || _c.WorldActionBusy || _c.HasPendingCatch || !ReachInteraction(4))return false;
            _loungeCamera=_c.viewCamera.transform.position;_loungeRotation=_c.viewCamera.transform.rotation;
            Lounging=true;_sitAge=0;_leaveAge=-1;_loungePrimary.Reset();_loungeSecondary.Reset();LoungeEyeOffset=Vector3.zero;
            _c.XRNotebook.Close();_c.StoryModalChanged();_c.Notice(XRSettings.isDeviceActive?"沙发休息 · A 起身 · B 开关潮汐放映机":"沙发休息 · E / Esc 起身 · 空格开关潮汐放映机",12);return true;
        }
        public void StandUp(bool immediately=false)
        {
            if(!Lounging)return;
            if(!immediately){if(_leaveAge<0)_leaveAge=0;return;}
            Lounging=false;_leaveAge=-1;LoungeEyeOffset=Vector3.zero;
            if(_c!=null && _c.Shore!=null){_c.Shore.SyncRider();_c.View.ResetLook();_c.StoryModalChanged();}
        }
        public void ToggleTelevision(){TelevisionOn=!TelevisionOn;_film.SetFloat("_Playing",TelevisionOn?1:0);}
        public void TickLeisureInput(Keyboard keyboard,bool focused,bool xr)
        {
            if(!_c.Ashore){StandUp(true);return;}
            if(!focused || _c.Paused){_loungePrimary.Reset();_loungeSecondary.Reset();return;}
            if(xr)
            {
                var hand=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                bool primary=false,secondary=false;
                bool available=!_c.XRSeat.HeadBlocked && _c.Hands.TryControllerPalm(false,out _,out _)
                    && hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton,out primary)
                    && hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton,out secondary);
                if(_loungePrimary.Step(available,primary))StandUp();if(_loungeSecondary.Step(available,secondary))ToggleTelevision();
            }
            else if(!_c.Automated && keyboard!=null)
            {
                if(keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)StandUp();
                if(keyboard.spaceKey.wasPressedThisFrame)ToggleTelevision();
            }
        }
        private void UpdateLeisure()
        {
            if(!Lounging)return;
            if(!_c.Ashore || _c.Home.Sleeping){StandUp(true);return;}
            float dt=_c.Paused?0:Mathf.Min(Time.unscaledDeltaTime,.05f);_sitAge+=dt;
            if(_leaveAge>=0){_leaveAge+=dt;if(_leaveAge>=.85f)StandUp(true);}
        }
        private void PresentLeisurePose()
        {
            float blend=Mathf.SmoothStep(0,1,_sitAge/1.25f)*(1-Mathf.SmoothStep(0,1,_leaveAge<0?0:_leaveAge/.85f));
            _c.Shore.Character.ApplySleepPose(_c.Shore.Position,Quaternion.Slerp(_c.Shore.Actor.rotation,_sofaFacing,blend),blend,_loungeHip,blend);
            var eye=_c.Shore.Character.SleepPoseEye;LoungeEyeOffset=eye-_loungeCamera;
            if(XRSettings.isDeviceActive)return;
            var target=_c.FirstPerson?eye:_loungeHip+_sofaForward*2.15f+Vector3.Cross(Vector3.up,_sofaForward)*-2.1f+Vector3.up*1.45f;
            var rotation=_c.FirstPerson?Quaternion.LookRotation(_screen.bounds.center-eye):Quaternion.LookRotation(_loungeHip+Vector3.up*.35f-target);
            _c.viewCamera.transform.SetPositionAndRotation(Vector3.Lerp(_loungeCamera,target,blend),Quaternion.Slerp(_loungeRotation,rotation,blend));
        }
        private void DisposeLeisure(){if(_film!=null)Destroy(_film);if(_filmSurface!=null)Destroy(_filmSurface);}
    }
}
