using System;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingAutoplay
    {
        private void ValidateShoreWalking()
        {
            var c = Controller; var walker = c.Shore; var rb = c.Player.engine.RB;
            Vector3 savedPosition = rb.position; Quaternion savedRotation = rb.rotation;
            var origin = new Vector3(5000, 0, 5000);
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Shore regression ground";
            platform.transform.position = origin + Vector3.up * .5f;
            platform.transform.localScale = new Vector3(24, 1, 24);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Shore regression wall";
            wall.transform.position = origin + new Vector3(-8, 2.5f, 0);
            wall.transform.localScale = new Vector3(.3f, 3, 10);
            try
            {
                SetShoreBoatPose(origin + new Vector3(-15, 1, 0), Quaternion.identity);
                RequireShore(walker.TryFindLanding(out _), "Nearby flat shore was not detected.");
                rb.velocity = Vector3.forward * 2;
                RequireShore(!walker.Toggle() && !c.IsAshore, "Moving boat allowed disembarking."); rb.velocity = Vector3.zero;
                c.TogglePause(); RequireShore(!walker.Toggle(), "Paused disembarking was allowed."); c.TogglePause();
                c.ToggleView(); RequireShore(!c.FirstPerson, "Third-person fixture failed.");
                RequireShore(walker.Toggle() && c.IsAshore && c.FirstPerson && rb.isKinematic, "Disembarking did not park the boat and enter first-person.");
                c.View.ApplyView();
                RequireShore(Vector3.Distance(c.viewCamera.transform.position, walker.EyePosition) < .001f, "Camera did not transfer to shore.");
                RequireShore(!c.Cast(), "Shore player can fish from the moored boat.");
                c.LeaveStation(); RequireShore(c.IsAshore && rb.isKinematic, "Q released an unoccupied boat.");
                c.ToggleView(); RequireShore(c.FirstPerson, "Shore view changed to a boat chase camera.");
                c.TogglePause(); var feet = walker.Position; walker.Walk(Vector2.up, .1f);
                RequireShore(walker.Position == feet && !walker.Toggle(), "Pause allowed walking or boarding."); c.TogglePause();
                c.viewCamera.transform.rotation = Quaternion.Euler(0, 90, 0);
                for (int i = 0; i < 35; i++) walker.Walk(Vector2.up, .1f);
                RequireShore(walker.Position.x > feet.x + 1f, "Walking input did not move the shore player.");
                RequireShore(walker.Position.x < origin.x - 8.4f, "Walker passed through a solid wall.");
                RequireShore(!walker.CanBoard && !walker.Toggle(), "Boarding worked far from the landing point.");
                c.viewCamera.transform.rotation = Quaternion.Euler(0, 180, 0);
                for (int i = 0; i < 100; i++) walker.Walk(Vector2.up, .1f);
                RequireShore(walker.Position.z > origin.z - 11.7f && walker.Position.y > .9f, "Walker fell off the shoreline edge.");
                RequireShore(rb.position == origin + new Vector3(-15, 1, 0), "Walking moved the parked boat.");
                walker.Board(); RequireShore(!c.IsAshore && !rb.isKinematic && !c.FirstPerson, "Boarding did not restore the boat and previous view.");
                RequireShore(walker.Toggle() && walker.CanBoard && walker.Toggle(), "Immediate boarding from the landing point failed.");
                RequireShore(walker.Toggle(), "Repeated disembarking failed.");
                c.ReturnToHarbor(); RequireShore(!c.IsAshore && !rb.isKinematic && Vector3.Distance(rb.position, c.spawnPosition) < .01f, "Rescue did not release shore mode.");
                c.ToggleView();

                // The actual procedural market must be reachable too, without rebuilding the scene.
                SetShoreBoatPose(c.harbor + new Vector3(-7, 1, -1.5f), Quaternion.identity);
                RequireShore(walker.Toggle(), "Actual market dock has no safe landing point.");
                RequireShore(c.AtHarbor && c.IsAshore, "Market trade context was lost ashore."); walker.Board();
                SetShoreBoatPose(origin + new Vector3(-15, 1, 0), Quaternion.identity);
                platform.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
                RequireShore(!walker.TryFindLanding(out _), "Trigger-only ground was accepted.");
                platform.GetComponent<Collider>().isTrigger = false;
                platform.transform.position = origin + Vector3.down; Physics.SyncTransforms();
                RequireShore(!walker.TryFindLanding(out _), "Underwater seabed was accepted.");
                platform.SetActive(false); wall.SetActive(false); Physics.SyncTransforms();
                RequireShore(!walker.Toggle() && !rb.isKinematic, "Open water allowed disembarking or froze the boat.");
                Debug.Log("[FISHING-SHORE] PASS: speed/pause gates, real market and shore landing, first-person transfer, walking, wall/edge collisions, mooring, near/far boarding, repeated transitions, rescue, trigger/submerged/open-water rejection.");
            }
            finally
            {
                if (c.Paused) c.TogglePause(); walker.Board();
                if (!c.FirstPerson) c.ToggleView();
                platform.SetActive(false); wall.SetActive(false); Destroy(platform); Destroy(wall);
                SetShoreBoatPose(savedPosition, savedRotation); c.View.ResetLook(); c.View.ApplyView();
            }
        }
        private void SetShoreBoatPose(Vector3 position, Quaternion rotation)
        {
            var rb = Controller.Player.engine.RB;
            rb.velocity = rb.angularVelocity = Vector3.zero; rb.position = position; rb.rotation = rotation;
            Controller.Player.transform.SetPositionAndRotation(position, rotation); Physics.SyncTransforms();
        }
        private static void RequireShore(bool value, string message)
        { if (!value) throw new Exception("Shore regression: " + message); }
    }
}
