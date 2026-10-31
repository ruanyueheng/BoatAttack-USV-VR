using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private int _pageItemCount=-1;
        private bool _notebookHeld,_pageGripReady,_swipeReady,_swiping,_swipeCaptured;
        private Vector3 _holdOffset,_swipeStart;
        private Quaternion _holdRotation;
        private float _swipeStarted;

        private void ResetPageGestures(){_notebookHeld=_pageGripReady=_swipeReady=_swiping=_swipeCaptured=false;}

        private bool TickPageGestures(bool leftGrip,bool rightGrip,Pose left,Pose right,float now)
        {
            var rect=(RectTransform)_canvas.transform;
            if(!leftGrip){_pageGripReady=true;_notebookHeld=false;}
            if(leftGrip && _pageGripReady && !_notebookHeld)
            {
                _pageGripReady=false;var point=rect.InverseTransformPoint(left.position);
                if(Mathf.Abs(point.x-rect.rect.xMin)<100 && point.y>=rect.rect.yMin && point.y<=rect.rect.yMax && Mathf.Abs(point.z)<100)
                {
                    _notebookHeld=true;_following=false;
                    _holdOffset=Quaternion.Inverse(left.rotation)*(rect.position-left.position);
                    _holdRotation=Quaternion.Inverse(left.rotation)*rect.rotation;
                    FishingXRInput.Pulse(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand),.18f,.04f);
                }
            }
            if(_notebookHeld)rect.SetPositionAndRotation(left.position+left.rotation*_holdOffset,left.rotation*_holdRotation);
            if(!rightGrip){_swipeReady=true;_swiping=_swipeCaptured=false;return false;}
            var local=rect.InverseTransformPoint(right.position);
            if(_swipeReady)
            {
                _swipeReady=false;
                if(_page!=1 && _page!=2 && _pageItemCount>=0 && rect.rect.Contains(local) && Mathf.Abs(local.z)<100)
                {_swiping=_swipeCaptured=true;_swipeStart=local;_swipeStarted=now;}
            }
            if(!_swiping)return _swipeCaptured;
            if(now-_swipeStarted>.8f || Mathf.Abs(local.z)>150 || Mathf.Abs(local.y-_swipeStart.y)>120){_swiping=false;return true;}
            float sweep=local.x-_swipeStart.x;if(Mathf.Abs(sweep)<180)return true;
            _swiping=false;
            int next=Mathf.Clamp(_offset+(sweep<0?6:-6),0,Mathf.Max(0,(_pageItemCount-1)/6)*6);
            if(next!=_offset)
            {
                _offset=next;Refresh();_pageOpenedAt=now;
                _c.WorldAudio?.Play("UIPage",_c.ExplorerPosition,.3f);
                FishingXRInput.Pulse(InputDevices.GetDeviceAtXRNode(XRNode.RightHand),.18f,.04f);
            }
            return true;
        }
    }
}
