using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV030Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V030");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V030] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var snapshot=c.Career.Snapshot();string output=Path.GetFullPath("Docs/Images/V030");Directory.CreateDirectory(output);
            try
            {
                FishingCharacterValidation.CheckSource(c);
                var source=Resources.Load<GameObject>(FishingCharacter.Resource);
                Check(FishingCharacter.Resource.EndsWith("9SCharacter") && source.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(s=>s.sharedMesh.triangles.Length/3)==17895,"Actual player uses the adapted 17895-triangle 9S avatar with calibrated five-finger gloves");
                Check(c.Shore.Character.BodySkin.sharedMaterials.All(m=>m.GetTexture("_BaseMap")!=null),"Supplied clothing textures survive Unity import");
                foreach(var item in new[]{FieldItem.OldBoot,FieldItem.RustyCan,FieldItem.SeaTreasure})
                    Check(FishingCoastRules.FindQuip(item).All(ch=>FishingUIFont.Font.HasCharacter(ch)),"Humorous catch text has no missing glyphs: "+item);
                c.ReturnToHarbor();c.Letter.ShowIsland(0);c.Letter.Tick(.35f,false,false);
                Check(c.Letter.Open && c.Letter.Envelope.Find("Coastal postage and cancellation").GetComponent<Image>().sprite!=null,"Envelope carries original coastal postage and a cancellation mark");
                FishingV016Validation.Capture(c,output,"envelope");
                c.Letter.Reveal();yield return null;
                Check(c.Letter.Ready && c.Letter.Paper.GetComponent<Image>().sprite.texture.name=="KraftPaper","Extracted letter uses fibre-textured kraft paper");
                foreach(var label in c.Letter.Paper.GetComponentsInChildren<Text>())
                {
                    Check(label.font!=null && label.preferredHeight<=label.rectTransform.rect.height+2,"Letter text fits without truncation: "+label.name);
                    Check(label.text.Where(ch=>!char.IsWhiteSpace(ch)).All(ch=>label.font.HasCharacter(ch)),"Letter typeface contains every displayed character: "+label.name);
                }
                FishingV016Validation.Capture(c,output,"letter");c.Letter.Close();
                FishingV028CoastValidation.SeaFindChecks(c,item=>
                {
                    Check(c.LastCatch.Find==item && c.LastCatch.Fish==null && Time.unscaledTime-c.LastCatch.ShownAt<1,"Committed sea find updates the shared catch card: "+item);
                    Check(c.MessageIsCatch && c.VisibleMessage.Contains(FishingCoastRules.FindQuip(item)),"Each sea find receives its own humorous feedback: "+item);
                    Check(FishingV023Art.Icon(item)!=null || FishingItemIcons.Get(item)!=null,"Catch card has an actual object icon: "+item);
                });
                c.ReturnToHarbor();var p=c.Shore.Sites[0].Berth+Vector3.up;
                var rb=c.Player.engine.RB;rb.position=p;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();yield return null;yield return null;
                Check(c.Shore.TryDisembark(),"New character reaches the actual shoreline");
                var bag=c.Career.Backpack;Check(bag.TryAdd(FieldItem.Lantern,1),"Isolated profile owns a lantern");
                int slot=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==FieldItem.Lantern);
                if(slot>=9){Check(bag.Move(slot,1),"Move lamp to hotbar");slot=1;}
                c.SelectFieldSlot(slot);c.UseFieldItem();c.SetDayNight(true);
                for(int i=0;i<6;i++){c.PresentCharacterFrame();yield return null;}
                var holster=c.FieldWorld.HolsteredLantern;var character=c.Shore.Character;
                Check(holster!=null && holster.gameObject.activeInHierarchy && !c.FieldWorld.LanternHeld,"Selected lantern remains on the belt with hands free");
                Check(Vector3.Dot(holster.up,Vector3.up)>.999f,"Lamp remains upright under gravity");
                float clearance=holster.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v=>Vector3.Dot(holster.TransformPoint(v)-character.HipPosition,c.Shore.Actor.right));
                Check(clearance>character.WaistHalfWidth+.025f,"Lantern stays outside the actual clothing silhouette");
                var mount=c.FieldWorld.transform.Find("Belt hook and short leather lantern strap");
                Check(mount!=null && Vector3.Distance(mount.TransformPoint(new Vector3(.075f,-.073f,0)),holster.TransformPoint(FishingFieldWorld.LanternHangPoint))<.002f,"Short strap physically connects the belt hook to the upper handle");
                Check(mount.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v=>Vector3.Dot(mount.TransformPoint(v)-character.BeltPosition,c.Shore.Actor.right))<=character.WaistHalfWidth,"Mount reaches the actual belt surface without a floating gap");
                Check(mount.GetComponent<MeshFilter>().sharedMesh.colors.Any(color=>color.r>.3f && color.g>.2f),"Metal hook geometry survives the mesh builder's minimum segment length");
                var lanternLight=c.FieldWorld.transform.Find("Field lantern light").GetComponent<Light>();
                Check(c.LanternOn && lanternLight.enabled && lanternLight.type==LightType.Point && lanternLight.color.r>lanternLight.color.b,"Visible waist lamp emits real warm amber light");
                c.ToggleView();c.PresentCharacterFrame();
                var target=character.BeltPosition+Vector3.up*.25f;var pos=target+c.Shore.Actor.right*2.2f+c.Shore.Actor.forward*1.6f+Vector3.up*.3f;
                c.viewCamera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(target-pos));FishingV016Validation.Capture(c,output,"character-waist-lantern");
                c.SetDayNight(false);c.PresentCharacterFrame();c.viewCamera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(target-pos));
                FishingV016Validation.Capture(c,output,"character-daylight");
                c.ToggleView();c.UseFieldItem();c.PresentCharacterFrame();
                Check(!c.LanternOn && !c.FieldWorld.transform.Find("Field lantern light").GetComponent<Light>().enabled
                    && !c.FieldWorld.transform.Find("Lantern soft ground spill").GetComponent<Light>().enabled,"F extinguishes the waist light");
            }
            finally{c.Letter.Close();c.EndItemInspection();c.ReturnToHarbor();c.RestoreShopTestProfile(snapshot);}
        }
    }
}
