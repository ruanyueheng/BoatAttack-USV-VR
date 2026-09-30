using System;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BoatAttack.EditorTools
{
    /// <summary>Input System events -> production view driver -> cursor and camera, not angle-helper-only tests.</summary>
    public static class FishingLookRegression
    {
        private sealed class RecordingCursor : IFishingCursor
        {
            private CursorLockMode _lock;
            public int LockRequests;
            public CursorLockMode LockState { get => _lock; set { _lock=value; LockRequests++; } }
            public bool Visible { get; set; } = true;
        }
        private static void Check(bool condition,string label)
        {
            if(!condition)throw new Exception("[LOOK] "+label);
            Debug.Log("[LOOK] CHECK: "+label);
        }
        private static void Step(FishingPrototype c,Mouse mouse,Keyboard keyboard,Vector2 delta,
            bool focused=true,bool automated=false,params Key[] keys)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { delta=delta });
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            InputSystem.Update();
            c.View.ApplyView(focused,automated);
        }
        private static bool Same(Quaternion a,Quaternion b)=>Quaternion.Angle(a,b)<.01f;
        private static void Reacquire(FishingPrototype c,Mouse m,Keyboard k,string reason)
        {
            var before=c.viewCamera.transform.rotation;
            Step(c,m,k,new Vector2(1200,600));
            Check(!c.View.IsLooking && Same(before,c.viewCamera.transform.rotation),reason+": first reacquired delta discarded");
            Step(c,m,k,new Vector2(120,0));
            Check(c.View.IsLooking && Quaternion.Angle(before,c.viewCamera.transform.rotation)>5,reason+": following motion resumes");
        }
        public static void CheckLanding(FishingPrototype c)
        {
            var oldMouse=Mouse.current;var oldKeyboard=Keyboard.current;
            var oldPointer=c.View.Pointer;var pointer=new RecordingCursor();
            c.View.Pointer=pointer;
            var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                Check(c.HasIsolatedProfile && c.Automated && c.Ashore,"isolated real landing fixture");
                Check(c.ChartExpanded && !c.ShowDetails && !c.JournalOpen,"landing automatically shows the navigation chart");
                c.View.ResetInput();c.View.ResetLook();
                Step(c,mouse,keyboard,Vector2.zero);
                var before=c.viewCamera.transform.rotation;var feet=c.Shore.Position;
                Step(c,mouse,keyboard,new Vector2(150,-60));
                Check(mouse.delta.ReadValue().x>100 && !mouse.rightButton.isPressed,"Input System delivered mouse motion with no right button");
                Check(c.View.IsLooking && Quaternion.Angle(before,c.viewCamera.transform.rotation)>10 && feet==c.Shore.Position,
                    "expanded landing chart must not block the production mouse-to-camera path; looking="+c.View.IsLooking+" cursor="+pointer.LockState+" visible="+pointer.Visible+" rotation="+Quaternion.Angle(before,c.viewCamera.transform.rotation)+" delta="+mouse.delta.ReadValue()+" chart="+c.ChartExpanded+" paused="+c.Paused+" journal="+c.JournalOpen+" details="+c.ShowDetails);
                Check(pointer.LockState==CursorLockMode.Locked && !pointer.Visible,"shore captures and hides pointer");
                before=c.viewCamera.transform.rotation;
                int lockRequests=pointer.LockRequests;
                Step(c,mouse,keyboard,Vector2.zero);Check(Same(before,c.viewCamera.transform.rotation),"zero motion cannot drift");
                Check(pointer.LockRequests==lockRequests,"stable capture never re-locks the cursor each frame");
                pointer.LockState=CursorLockMode.None;
                Reacquire(c,mouse,keyboard,"OS cursor lock lost");
                c.ToggleChart();Step(c,mouse,keyboard,new Vector2(100,0));
                Check(c.View.IsLooking && !Same(before,c.viewCamera.transform.rotation),"collapsed chart still allows look");
                c.ToggleChart();before=c.viewCamera.transform.rotation;Step(c,mouse,keyboard,new Vector2(100,0));
                Check(c.View.IsLooking && !Same(before,c.viewCamera.transform.rotation),"reopening chart cannot freeze look");
                foreach(var key in new[]{Key.LeftAlt,Key.RightAlt})
                {
                    before=c.viewCamera.transform.rotation;
                    Step(c,mouse,keyboard,new Vector2(500,100),true,false,key);
                    Check(!c.View.IsLooking && Same(before,c.viewCamera.transform.rotation) && pointer.LockState==CursorLockMode.None && pointer.Visible,key+" releases pointer without camera rotation");
                    Reacquire(c,mouse,keyboard,key.ToString());
                }
                before=c.viewCamera.transform.rotation;Step(c,mouse,keyboard,new Vector2(500,100),false);
                Check(!c.View.IsLooking && Same(before,c.viewCamera.transform.rotation) && pointer.LockState==CursorLockMode.None,"focus loss releases pointer and ignores motion");
                Reacquire(c,mouse,keyboard,"focus recovery");
                c.TogglePause();before=c.viewCamera.transform.rotation;Step(c,mouse,keyboard,new Vector2(500,100));
                Check(!c.View.IsLooking && Same(before,c.viewCamera.transform.rotation) && pointer.Visible,"pause blocks input and releases pointer");
                c.TogglePause();Reacquire(c,mouse,keyboard,"pause recovery");
                c.ToggleDetails();before=c.viewCamera.transform.rotation;Step(c,mouse,keyboard,new Vector2(500,100));
                Check(!c.View.IsLooking && Same(before,c.viewCamera.transform.rotation) && pointer.Visible,"detail panel retains UI pointer");
                c.ToggleDetails();Reacquire(c,mouse,keyboard,"details closed");
                c.ToggleJournal();before=c.viewCamera.transform.rotation;Step(c,mouse,keyboard,new Vector2(500,100));
                Check(!c.View.IsLooking && Same(before,c.viewCamera.transform.rotation) && pointer.Visible,"journal blocks look");
                c.ToggleJournal();Reacquire(c,mouse,keyboard,"journal closed");
                before=c.viewCamera.transform.rotation;Step(c,mouse,keyboard,new Vector2(500,100),true,true);
                Check(!c.View.IsLooking && Same(before,c.viewCamera.transform.rotation) && pointer.Visible,"automated mode never captures a user's cursor");
                Reacquire(c,mouse,keyboard,"manual mode restored");
                c.View.ResetLook();Step(c,mouse,keyboard,new Vector2(3000,3000));
                Check(Mathf.Abs(Mathf.DeltaAngle(c.viewCamera.transform.eulerAngles.x,-65))<.1f,"upward pitch clamps to 65 degrees");
                Step(c,mouse,keyboard,new Vector2(0,-6000));
                Check(Mathf.Abs(Mathf.DeltaAngle(c.viewCamera.transform.eulerAngles.x,65))<.1f,"downward pitch clamps to 65 degrees");
                c.ToggleView();Step(c,mouse,keyboard,Vector2.zero);before=c.viewCamera.transform.rotation;
                Step(c,mouse,keyboard,new Vector2(120,0));
                Check(c.View.IsLooking && !Same(before,c.viewCamera.transform.rotation) && c.Shore.Position==feet,"third-person orbit consumes the same mouse events");
                c.ToggleView();Step(c,mouse,keyboard,Vector2.zero);
                Check(c.Shore.TryBoard(),"can board after mouse-look interaction");
                Step(c,mouse,keyboard,Vector2.zero);
                Check(!c.View.IsLooking && pointer.LockState==CursorLockMode.None && pointer.Visible,"boarding releases shore pointer; boat still needs right button");
                Check(c.Shore.TryDisembark(),"can disembark a second time");
                Step(c,mouse,keyboard,Vector2.zero);before=c.viewCamera.transform.rotation;
                Step(c,mouse,keyboard,new Vector2(120,0));
                Check(c.ChartExpanded && c.View.IsLooking && !Same(before,c.viewCamera.transform.rotation),"repeat landing also works with expanded chart");
                Debug.Log("[LOOK] PASS: production mouse/keyboard path, cursor lifecycle and repeat landing. Application focus and OS cursor response are simulated; not physical input certification.");
            }
            finally
            {
                c.View.ResetInput();c.View.ResetLook();
                InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
                if(oldMouse!=null && oldMouse.added)oldMouse.MakeCurrent();
                if(oldKeyboard!=null && oldKeyboard.added)oldKeyboard.MakeCurrent();
                c.View.Pointer=oldPointer;
            }
        }
    }
}
