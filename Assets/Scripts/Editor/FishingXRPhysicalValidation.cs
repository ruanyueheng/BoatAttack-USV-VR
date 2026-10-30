using System;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingXRPhysicalValidation
    {
        private static void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); Debug.Log("[XR-PHYSICAL] CHECK: " + text); }
        public static void Run(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile, "Physical gesture checks use an isolated profile");
            c.ReturnToHarbor();
            typeof(FishingPrototype).GetMethod("PrepareAcceptanceHome", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { FishingAcceptanceCase.HomeInterior });
            try
            {
                Check(c.Shore.Relocate(c.Home.Origin + new Vector3(3.1f, .2f, -1.35f), Quaternion.Euler(0, 90, 0)), "Physical lid test stands beside the storage chest");
                var home = c.Home; var handle = home.StorageLidHandle; var rest = home.StorageLid.localRotation;
                home.StepStorageLidGrip(true, true, false, handle);
                home.StepStorageLidGrip(true, true, true, handle + Vector3.right * .8f);
                home.StepStorageLidGrip(true, true, true, handle + Vector3.up * .15f);
                Check(Quaternion.Angle(rest, home.StorageLid.localRotation) < .01f, "A hand must close on the actual lid edge");
                home.StepStorageLidGrip(true, true, false, handle);
                home.StepStorageLidGrip(true, true, true, handle);
                home.StepStorageLidGrip(true, true, true, handle + Vector3.up * .14f);
                Check(Quaternion.Angle(rest, home.StorageLid.localRotation) > 20 && !c.InventoryOpen, "A partial upward pull moves the real cover before opening storage");
                var partial = home.StorageLid.localRotation;
                home.StepStorageLidGrip(true, false, true, handle);
                home.StepStorageLidGrip(true, true, true, handle + Vector3.up * .3f);
                Check(Quaternion.Angle(partial, home.StorageLid.localRotation) < .01f && !c.InventoryOpen, "Tracking recovery while held cannot reopen or fling the lid");
                var edge = home.StorageLidHandle;
                home.StepStorageLidGrip(false, true, false, edge);
                home.StepStorageLidGrip(false, true, true, edge);
                home.StepStorageLidGrip(false, true, true, edge + Vector3.up * .2f);
                Check(c.InventoryOpen, "Either hand can lift the cover into the existing storage panel");
                c.ToggleInventory();
                var route = c.Keeper.Ladder;
                Check(c.Shore.Relocate(route.Bottom, Quaternion.identity), "Physical ladder test starts on supported ground");
                Vector3 rung = route.RungBottom + Vector3.up * 1.2f;
                var visible = route.transform.parent.Find("Iron outer ladder").GetComponent<MeshFilter>();
                bool leftRung = false, rightRung = false;
                foreach(var vertex in visible.sharedMesh.vertices)
                {
                    var at = visible.transform.TransformPoint(vertex);
                    leftRung |= Vector3.Distance(at, rung - route.transform.right * .26f) < .025f;
                    rightRung |= Vector3.Distance(at, rung + route.transform.right * .26f) < .025f;
                }
                Check(leftRung && rightRung && Mathf.Abs(FishingPrototype.FlatDistance(route.Bottom, route.RungBottom) - .45f) < .001f,
                    "Grip axis intersects the visible rung mesh while the body path retains 45 cm rail clearance");
                var emptyAir = route.Bottom + Vector3.up * 1.2f;
                c.Shore.StepClimbHand(true, true, false, emptyAir);
                c.Shore.StepClimbHand(true, true, true, emptyAir);
                Check(!c.Shore.Climbing, "Gripping the empty body path cannot substitute for touching a visible rung");
                c.Shore.StepClimbHand(true, true, false, rung);
                c.Shore.StepClimbHand(true, true, true, rung);
                Check(c.Shore.Climbing, "A released then closed hand on a rung starts climbing");
                c.Shore.Move(Vector2.zero, false, false, false, .05f);
                rung = route.RungBottom + Vector3.up * (c.Shore.Position.y - route.Bottom.y + 1.2f);
                c.Shore.StepClimbHand(true, true, false, rung);
                c.Shore.StepClimbHand(true, true, true, rung);
                float start = c.Shore.Position.y;
                c.Shore.StepClimbHand(true, true, true, rung - Vector3.up * .04f);
                c.Shore.Move(Vector2.zero, false, false, false, .05f);
                Check(c.Shore.Position.y > start + .02f && c.Shore.Position.y < start + .1f, "Pulling one gripped hand down lifts the collision body within the speed cap");
                rung = route.RungBottom + Vector3.up * (c.Shore.Position.y - route.Bottom.y + 1.2f);
                c.Shore.StepClimbHand(true, true, false, rung);
                c.Shore.StepClimbHand(false, true, false, rung);
                c.Shore.StepClimbHand(false, true, true, rung);
                start = c.Shore.Position.y;
                c.Shore.StepClimbHand(false, true, true, rung - Vector3.up * .04f);
                c.Shore.Move(Vector2.zero, false, false, false, .05f);
                Check(c.Shore.Position.y > start + .02f, "The other hand can take the next rung and continue the climb");
                c.Shore.StepClimbHand(false, false, true, rung);
                c.Shore.StepClimbHand(false, true, true, rung - Vector3.up * .08f);
                start = c.Shore.Position.y;
                c.Shore.Move(Vector2.zero, false, false, false, .05f);
                Check(Mathf.Abs(c.Shore.Position.y - start) < .002f, "Lost tracking and held recovery produce no ladder motion");
                rung = route.RungBottom + Vector3.up * (c.Shore.Position.y - route.Bottom.y + 1.2f);
                c.Shore.StepClimbHand(false, true, false, rung);
                c.Shore.StepClimbHand(false, true, true, rung);
                c.TogglePause(); c.Shore.Tick(false, .05f); c.TogglePause();
                c.Shore.StepClimbHand(false, true, true, rung - Vector3.up * .05f);
                c.Shore.Move(Vector2.zero, false, false, false, .05f);
                Check(Mathf.Abs(c.Shore.Position.y - start) < .002f, "Pause also requires releasing before a new physical grab");
                c.Shore.Move(Vector2.up, false, false, false, .05f);
                Check(c.Shore.Position.y > start + .02f, "Stick/keyboard climbing remains available after releasing the hand latch");
            }
            finally
            {
                if (c.InventoryOpen) c.ToggleInventory();
                if (c.Paused) c.TogglePause();
                c.ReturnToHarbor();
            }
        }
    }
}
