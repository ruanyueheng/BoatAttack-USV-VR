using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    /// <summary>Runs the real scene and catch transaction, with accelerated rule ticks and an isolated save.</summary>
    public static class FishingAnglingValidation
    {
        public static void Run() => FishingV011Validation.Begin("ANGLING");

        private sealed class MeshSample
        {
            public MeshFilter Filter;
            public Mesh Source;
            public Vector3[] Rest, Original;
            public Matrix4x4 ToRod;
        }

        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            Debug.Log("[ANGLING] CHECK: " + label);
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),
                "Only the real scene's temporary isolated career is used");
            var original = c.Career.Snapshot();
            string productionPath = Path.Combine(Application.persistentDataPath, "boatattack-fishing-v13.json");
            byte[] production = ReadIfPresent(productionPath), productionBackup = ReadIfPresent(productionPath + ".bak");
            string output = Path.GetFullPath(Path.Combine("Logs", "Angling", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(output);
            try
            {
                c.ReturnToHarbor();
                c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                c.MoveToSpotForAutomation(0); c.Interact();
                if (!c.FirstPerson) c.ToggleView();
                c.View.ResetLook();
                for (int i = 0; i < 8; i++) { c.PresentCharacterFrame(); yield return null; }
                Check(c.Anchored && !c.Ashore && c.Angling != null && c.Hands.Rod.gameObject.activeInHierarchy,
                    "The actual boat is anchored and the enhanced rod is equipped");
                var rod = c.Hands.Rod;
                var dynamics = rod.GetComponent<FishingRodDynamics>();
                Check(dynamics != null && dynamics.MeshCount > 0 && dynamics.Tip != null,
                    "The imported rod has readable, per-instance flexible meshes");
                CheckRodGeometry(c, dynamics);

                var forward = Vector3.ProjectOnPlane(c.viewCamera.transform.forward, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                Check(c.Cast(.8f) && c.Session.Phase == CatchPhase.Waiting && !c.Hook(),
                    "Production cast starts waiting, and a premature hook is rejected");
                FishingCatchValidation.KeepOrdinaryReceipt(c);
                for (int i = 0; i < 20; i++) c.CastMotion.Tick(.1f);
                Check(!c.CastMotion.Casting, "The float flight finishes before waiting feedback is sampled");
                var waitingPoint = c.Angling.Target(CatchPhase.Waiting);
                c.Angling.Tick(.02f, false, true);
                Check(c.Angling.MarkerVisible && Vector3.Dot(waitingPoint - c.Player.transform.position, forward) > 3,
                    "The real distance marker appears over the water in front of the cast");
                var hint = Array.Find(c.Angling.GetComponentsInChildren<Text>(true), t => t.name == "Fishing hint");
                Check(hint != null && hint.font != null && !hint.raycastTarget
                    && hint.preferredHeight <= hint.rectTransform.rect.height,
                    "The world-space distance hint fits its original font panel without blocking tool input");
                string shortHint=hint.text;
                hint.text="漂亮提竿！\n14.0 m\n鱼向右游 →\n持竿手向左控竿";
                Check(hint.preferredHeight<=hint.rectTransform.rect.height,"The longest Pico strike/direction hint fits without truncation");
                hint.text=shortHint;
                var fishingLine=Array.Find(c.GetComponentsInChildren<LineRenderer>(true),l=>l.name=="Fishing line");
                Check(fishingLine!=null && fishingLine.sharedMaterial.shader.name=="BoatAttack/FishingAnglingLine"
                    && fishingLine.sharedMaterial.shader.isSupported,"Line and fading ripple feedback use the supported vertex-color transparent shader");
                var floatBody = c.GetComponentsInChildren<Transform>(true);
                Check(Array.Exists(floatBody, t => t.name == "Float bright mast")
                    && Array.Exists(floatBody, t => t.name == "Float sight tip"),
                    "The new slender, high-contrast float is instantiated in the real scene");
                c.PresentCharacterFrame();
                FishingV016Validation.Capture(c, output, "01-waiting-float");
                bool peck = false, earlySafe = true;
                for (int i = 0; i < 500 && c.Session.Phase == CatchPhase.Waiting; i++)
                {
                    peck |= c.Session.BitePreview > 0;
                    earlySafe &= !c.Hook();
                    c.Session.Tick(.02f, false);
                }
                Check(peck && earlySafe && c.Session.Phase == CatchPhase.Bite,
                    "Float pecks preview a real bite without granting an early fish");
                var bitePoint = c.Angling.Target(CatchPhase.Bite);
                Check(bitePoint.y < waitingPoint.y - .08f, "The bite visibly sinks the float beneath its waiting height");
                for (int i = 0; i < 15; i++) c.Session.Tick(.02f, false);
                Check(c.Hook() && c.Session.PerfectStrike && c.Session.Claim() == null,
                    "A timely production hook scores a perfect strike but does not settle a catch");
                for (int i = 0; i < 30; i++)
                    c.Session.Tick(.02f, c.Session.Tension < .55f, -Math.Sign(c.Session.FishDirection));
                c.Angling.Target(CatchPhase.Fighting);
                c.Angling.Tick(.02f, true, true);
                c.Angling.PresentRod(.1f); c.PresentCharacterFrame();
                for(int frame=0;frame<5;frame++)yield return null;
                var tipViewport=c.viewCamera.WorldToViewportPoint(dynamics.Tip.position);
                Check(Vector3.Distance(fishingLine.GetPosition(0),dynamics.Tip.position)<.001f,"The rendered line begins at the final deformed rod tip");
                Check(tipViewport.z>0 && tipViewport.x>.05f && tipViewport.x<.95f && tipViewport.y>.05f && tipViewport.y<.98f,
                    "The loaded first-person rod tip stays inside the usable viewport");
                FishingV016Validation.Capture(c, output, "02-loaded-rod");
                for(int i=0;i<60;i++)dynamics.SetLoad(.95f,.6f,true,.02f);
                FishingV016Validation.Capture(c,output,"02b-rod-load-visual-test");
                var audio = (AudioSource)typeof(FishingAnglingPresentation)
                    .GetField("_reelAudio", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c.Angling);
                Check(audio != null && audio.clip != null && audio.loop && !audio.playOnAwake,
                    "The original reel/drag sound is configured as an explicitly controlled loop");
                var xrMode=typeof(FishingPrototype).GetField("_xrActive",BindingFlags.Instance|BindingFlags.NonPublic);
                bool realMode=(bool)xrMode.GetValue(c);float beforeTrackingLoss=c.Angling.VisualTime;
                try
                {
                    // Desktop has no tracked controllers: exercise the production XR validity gate,
                    // not a manufactured controller pose or a claim of physical headset certification.
                    xrMode.SetValue(c,true);
                    typeof(FishingPrototype).GetMethod("TickAnglingPresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);
                    Check(!c.Angling.MarkerVisible && !audio.isPlaying && c.Angling.VisualTime==beforeTrackingLoss,
                        "An XR session without valid tracking cannot retain audio, markers or visual time");
                }
                finally{xrMode.SetValue(c,realMode);}
                c.Angling.Tick(.02f,true,true);
                float visualClock = c.Angling.VisualTime, fightClock = c.Session.FightTime;
                c.TogglePause();
                for (int i = 0; i < 3; i++) yield return null;
                Check(c.Paused && c.Session.FightTime == fightClock && c.Angling.VisualTime == visualClock
                    && !c.Angling.MarkerVisible && !audio.isPlaying,
                    "Pause freezes the real fight, hides the water marker and stops reel audio");
                c.TogglePause(); yield return null;
                c.Angling.Target(CatchPhase.Fighting); c.Angling.Tick(.02f, true, true);
                visualClock = c.Angling.VisualTime; fightClock = c.Session.FightTime;
                c.ToggleInventory();
                for (int i = 0; i < 3; i++) yield return null;
                Check(c.InventoryOpen && c.Paused && c.Session.FightTime == fightClock
                    && c.Angling.VisualTime == visualClock && !c.Angling.MarkerVisible && !audio.isPlaying,
                    "Opening the actual inventory cannot leave a stale fish marker or reel sound running");
                c.ToggleInventory(); yield return null;

                bool left = false, rightSide = false, followsRule = true, countered = false;
                for (int i = 0; i < 6500 && c.Session.Phase == CatchPhase.Fighting; i++)
                {
                    float fishSide = c.Session.FishDirection;
                    var point = c.Angling.Target(CatchPhase.Fighting);
                    float lateral = Vector3.Dot(point - c.Player.transform.position, right);
                    left |= fishSide < -.2f && lateral < -.05f;
                    rightSide |= fishSide > .2f && lateral > .05f;
                    if (Mathf.Abs(fishSide) > .2f) followsRule &= lateral * fishSide > 0;
                    // First observe a complete lateral change, then use the same optional assist as the real input adapters.
                    float side = left && rightSide ? -Math.Sign(fishSide) : 0;
                    // Brief tautening pulses keep a tiny fish alive long enough to observe both directions,
                    // without exceeding the original four-second slack allowance or exhausting its distance.
                    bool reel = left && rightSide ? c.Session.Tension < .45f && c.Session.Beat != FishBeat.Leap
                        : c.Session.FightTime % 2.5f < .25f;
                    c.Session.Tick(.02f, reel, side);
                    countered |= c.Session.ControlQuality > .2f;
                }
                Check(left && rightSide && followsRule && countered,
                    "The actual fish target visibly runs both left and right, and opposing the pull improves control");
                Check(c.Session.Phase == CatchPhase.Landing && c.Session.Stamina == 0,
                    "Following the fight still reaches the original physical-net phase without a second stamina gate");
                // Pure accelerated ticks do not move the scene. Let the controller apply its new phase once.
                yield return null;
                c.Angling.Tick(.02f, false, true); c.PresentCharacterFrame();
                Check(Vector3.Distance(c.ActiveFish.position, c.LandingTarget) < .05f,
                    "Landing returns the fish to the exact existing net target rather than a lateral visual offset");
                FishingV016Validation.Capture(c, output, "03-fish-at-net");
                int hold = c.Career.Count, catches = c.Career.Catches, coins = c.Career.Coins;
                Check(c.Scoop() && !c.HasPendingCatch, "Desktop scoop requests the real net motion, not a reward");
                for (int i = 0; i < 45 && !c.HasPendingCatch; i++) c.CatchRig.StepDesktop(.02f);
                Check(c.HasPendingCatch && c.Career.Count == hold && c.Career.Catches == catches
                    && c.Career.Coins == coins, "A netted fish remains a durable undecided transaction");
                string id = c.Career.PendingCatch.fish.id;
                var loaded = FishingSaveStore.Load(c.SavePath, out string warning);
                Check(warning == "" && loaded.HasPendingCatch && loaded.PendingCatch.fish.id == id
                    && loaded.Snapshot().version == 14, "Pending ownership survives the current v14 JSON save format");
                c.PresentCharacterFrame();
                Check(!c.Cast() && !c.BeginNetCatch(), "A staged fish blocks duplicate casts and net claims");
                Check(c.ResolveCatch(true) && !c.ResolveCatch(true) && !c.HasPendingCatch
                    && c.Career.Count == hold + 1 && c.Career.Catches == catches + 1 && c.Career.Coins == coins,
                    "Explicit box storage commits exactly once without awarding sale coins");
                loaded = FishingSaveStore.Load(c.SavePath, out warning);
                Check(warning == "" && !loaded.HasPendingCatch && loaded.Count == hold + 1
                    && !loaded.Store(loaded.Inventory[loaded.Count - 1]),
                    "Stored receipt survives disk reload and remains deduplicated");
                c.Angling.Tick(0, false, false);
                Check(!c.Angling.MarkerVisible && !audio.isPlaying,
                    "Finishing a catch hides distance feedback and stops the reel loop");
                Debug.Log("[ANGLING] EVIDENCE: " + output + " (scene render and simulated poses, not physical PICO certification)");
            }
            finally
            {
                if (c.InventoryOpen) c.ToggleInventory();
                if (c.Paused) c.TogglePause();
                c.ReturnToHarbor(); c.RestoreShopTestProfile(original);
            }
            Check(SameBytes(production, ReadIfPresent(productionPath))
                && SameBytes(productionBackup, ReadIfPresent(productionPath + ".bak")),
                "The player's original production save and backup remain byte-for-byte unchanged");
        }

        private static void CheckRodGeometry(FishingPrototype c, FishingRodDynamics dynamics)
        {
            var rod = c.Hands.Rod;
            var body = rod.Find("RodBody");
            var sourceBody = Resources.Load<GameObject>("FishingGear/RodBody");
            Check(body != null && sourceBody != null, "The original rod resource remains available beside its runtime instance");
            for (int i = 0; i < 100; i++) dynamics.SetLoad(0, 0, false, .05f);
            var samples = new List<MeshSample>();
            foreach (var filter in body.GetComponentsInChildren<MeshFilter>(true))
            {
                string path = UnityEditor.AnimationUtility.CalculateTransformPath(filter.transform, body);
                var sourceAt = path.Length == 0 ? sourceBody.transform : sourceBody.transform.Find(path);
                Check(sourceAt != null && sourceAt.GetComponent<MeshFilter>() != null,
                    "Every runtime blank mesh maps to its original imported resource: " + path);
                var source = sourceAt.GetComponent<MeshFilter>().sharedMesh;
                if (!source.isReadable) continue;
                samples.Add(new MeshSample { Filter = filter, Source = source, Original = source.vertices,
                    Rest = filter.sharedMesh.vertices, ToRod = rod.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            int clones = 0;
            foreach (var sample in samples) if (sample.Filter.sharedMesh != sample.Source) clones++;
            Check(clones == dynamics.MeshCount && clones > 0, "Flexible blanks use cloned meshes rather than editing imported shared meshes");
            var restTip = rod.InverseTransformPoint(dynamics.Tip.position);
            var crankPosition = c.Hands.Crank.localPosition;
            var crankRotation = c.Hands.Crank.localRotation;
            var crankScale = c.Hands.Crank.localScale;
            var crankMeshes = c.Hands.Crank.GetComponentsInChildren<MeshFilter>();
            var crankVertices = new List<Vector3[]>();
            foreach (var crank in crankMeshes) crankVertices.Add(crank.sharedMesh.vertices);
            for (int i = 0; i < 60; i++) dynamics.SetLoad(.95f, .8f, true, .02f);
            var bentTip = rod.InverseTransformPoint(dynamics.Tip.position);
            Check(dynamics.CurrentBend > .5f && bentTip.y < restTip.y - .3f && bentTip.x > restTip.x + .02f,
                "Line load visibly bends the tip downward with a small lateral pull");
            bool rigid = true, sourceUnchanged = true, moved = false;
            foreach (var sample in samples)
            {
                var vertices = sample.Filter.sharedMesh.vertices;
                sourceUnchanged &= SameVertices(sample.Source.vertices, sample.Original);
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (sample.ToRod.MultiplyPoint3x4(sample.Rest[i]).z <= FishingRodDynamics.FlexibleStart)
                        rigid &= vertices[i] == sample.Rest[i];
                    else moved |= Vector3.Distance(vertices[i], sample.Rest[i]) > .01f;
                }
            }
            Check(rigid && moved && sourceUnchanged, "Blank bends while handle vertices and imported mesh data remain untouched");
            float bend = dynamics.CurrentBend, sideBend = dynamics.CurrentSideBend;
            dynamics.SetLoad(0, -1, false, 0);
            Check(dynamics.CurrentBend == bend && dynamics.CurrentSideBend == sideBend
                && rod.InverseTransformPoint(dynamics.Tip.position) == bentTip,
                "A zero-time step freezes the spring and tip exactly");
            for (int i = 0; i < 100; i++) dynamics.SetLoad(0, 0, false, .05f);
            Check(dynamics.CurrentBend == 0 && dynamics.CurrentSideBend == 0
                && Vector3.Distance(rod.InverseTransformPoint(dynamics.Tip.position), restTip) < .00001f,
                "Removing load returns the same rod to its undeformed rest pose without accumulation");
            bool crankUntouched = c.Hands.Crank.localPosition == crankPosition
                && Quaternion.Angle(c.Hands.Crank.localRotation, crankRotation) < .001f
                && c.Hands.Crank.localScale == crankScale;
            for (int i = 0; i < crankMeshes.Length; i++)
                crankUntouched &= SameVertices(crankMeshes[i].sharedMesh.vertices, crankVertices[i]);
            Check(crankUntouched, "Flexible blank deformation never moves, twists or rewrites the reel/crank");
        }

        private static bool SameVertices(Vector3[] a, Vector3[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static byte[] ReadIfPresent(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
