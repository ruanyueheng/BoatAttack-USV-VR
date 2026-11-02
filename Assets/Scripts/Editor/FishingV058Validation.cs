using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV058Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V058");
        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException(message);Debug.Log("[V058] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V058");
            foreach(var run in new[]{FishingFeedbackToolsValidation.TrackedHandChecks(c),
                FishingV044LifeValidation.BinocularChecks(c,c.Life,"Logs/V058"),FishingHomeEcologyValidation.QuickPurchaseAndDeploymentChecks(c),
                FishingV036OrchardValidation.Checks(c),CashierChecks(c)})
            {
                try{while(run.MoveNext())yield return run.Current;}
                finally{(run as IDisposable)?.Dispose();}
            }
        }
        private static IEnumerator CashierChecks(FishingPrototype c)
        {
            c.ReturnToHarbor();yield return null;
            var shop=c.TackleShop;var rb=c.Player.engine.RB;
            rb.position=shop.mooringPoint.position;rb.rotation=shop.transform.rotation;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark() && c.Shore.Relocate(shop.purchasePoint.position,shop.transform.rotation),"Reach the real cashier counter");
            yield return null;yield return null;
            var cashier=shop.Cashier;var root=cashier.transform;var feet=root.position;var facing=root.rotation;
            var skins=cashier.GetComponent<LODGroup>().GetLODs().Select(l=>l.renderers[0] as SkinnedMeshRenderer).ToArray();
            Check(cashier.Animated && skins.Length==2 && skins.All(s=>s!=null && s.bones.Length==7 && s.sharedMesh!=null && s.enabled),"Both original cashier LODs have articulated GPU gesture skins");
            var baked=new Mesh();var before=new Mesh();
            try
            {
                skins[0].BakeMesh(before);var head=skins[0].bones[2];var first=head.localRotation;
                float until=Time.time+1;while(Time.time<until)yield return null;
                skins[0].BakeMesh(baked);
                var vertices=baked.vertices;var original=before.vertices;
                Check(Quaternion.Angle(first,head.localRotation)>.1f && vertices.Where((p,i)=>Vector3.Distance(p,original[i])>.001f).Any(),"Cashier gestures actually deform the visible original mesh");
                Check(root.position==feet && root.rotation==facing,"Gestures preserve planted feet and the original customer-facing anchor");
                Check(Enumerable.Range(0,vertices.Length).Where(i=>original[i].y<.08f).All(i=>Vector3.Distance(vertices[i],original[i])<.0001f),"Feet remain planted during upper-body gestures");
                Check(c.Residents.Talk(4),"Existing cashier conversation opens");
                yield return null;yield return null;
                until=Time.time+2.5f;while(Time.time<until)yield return null;
                Check(cashier.Gesture=="交谈" || cashier.Gesture=="倾听","Cashier reacts to the real conversation state");
                var cam=c.viewCamera.transform;
                cam.SetPositionAndRotation(shop.transform.TransformPoint(new Vector3(-1.1f,1.60f,1.2f)),Quaternion.LookRotation(cashier.FacePosition-shop.transform.TransformPoint(new Vector3(-1.1f,1.60f,1.2f))));
                FishingCashierValidation.ValidateInScene(c,false);FishingV016Validation.Capture(c,"Logs/V058","cashier-conversation");
                c.Residents.Close();
            }
            finally{UnityEngine.Object.Destroy(baked);UnityEngine.Object.Destroy(before);}
        }
    }
}
