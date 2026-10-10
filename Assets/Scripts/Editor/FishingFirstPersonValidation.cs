using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.EditorTools
{
    /// <summary>第一人称握持、颈部裁剪和光学视野的真实骨架回归。</summary>
    public static class FishingFirstPersonValidation
    {
        private static string _evidence;
        public static void Run()=>FishingV011Validation.Begin("FIRST-PERSON");
        private static void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException("第一人称："+label);
            Debug.Log("[FIRST-PERSON] CHECK: "+label);
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            _evidence=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/first-person-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_evidence);
            c.MoveToSpotForAutomation(0);c.Interact();
            for(int i=0;i<12;i++)yield return null;
            c.PresentCharacterFrame();Describe(c,"rod");Capture(c,"01-rod");
            CheckLocalMesh(c.Shore.Character);
            c.ReturnToHarbor();
            var p=c.Shore.Sites[0].Berth+Vector3.up*.6f;
            var rb=c.Player.engine.RB;rb.position=p;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"从真实登陆点上岸");
            for(int i=0;i<12;i++)yield return null;
            int shot=2;
            foreach(var item in new[]{FieldItem.StonePickaxe,FieldItem.IronPickaxe,FieldItem.Binoculars,FieldItem.FieldGuide,FieldItem.Lantern})
            {
                Equip(c,item);c.View.ResetLook();
                for(int i=0;i<3;i++)yield return null;
                c.PresentCharacterFrame();Describe(c,item.ToString());Capture(c,(shot++).ToString("D2")+"-"+item);
                Check(c.FieldWorld.PresentedItem==item && c.FieldWorld.HeldToolRenderer.enabled,"平视持物可见 "+item);
                CheckPalm(c,false,item.ToString());
                if(item==FieldItem.Binoculars || item==FieldItem.FieldGuide)CheckPalm(c,true,item.ToString());
                CheckGrasp(c,item);
                foreach(float pitch in new[]{-50f,45f,65f})
                {
                    c.View.ResetLook();c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));
                    for(int frame=0;frame<3;frame++)yield return null;
                    c.PresentCharacterFrame();
                    float palmError=Vector3.Distance(c.Shore.Character.PalmPosition(false),c.Hands.Right.Palm.position);
                    Check(palmError<.035f,"俯仰不拉断握持 "+item+" / "+pitch+" error="+palmError);
                }
                if(item==FieldItem.StonePickaxe)Capture(c,"08-look-down-standing");
                c.View.ResetLook();
                if(item!=FieldItem.Binoculars)continue;
                c.UseFieldItem();
                for(int i=0;i<100 && c.View.BinocularBlend<.999f;i++)yield return null;
                c.PresentCharacterFrame();
                Check(c.BinocularsRaised && c.View.OpticalView && Mathf.Abs(c.viewCamera.fieldOfView-FishingViewRig.BinocularFieldOfView)<.01f,"完成举镜后进入放大光学视野");
                Check(!c.FieldWorld.HeldToolRenderer.enabled && c.Shore.Character.FirstPersonSkin.forceRenderingOff,"光学观察不让手和镜筒挡住画面");
                Check(c.Hands.Left.Visible && c.Hands.Right.Visible,"隐藏的是本地渲染，不删除真实握点或骨架");
                Capture(c,"09-optical-world");
                c.ToggleView();c.PresentCharacterFrame();
                Check(c.BinocularsRaised && c.FieldWorld.HeldToolRenderer.enabled && c.Shore.Character.BodySkin.shadowCastingMode==ShadowCastingMode.On,"第三人称恢复完整角色与举镜实物");
                Check(Mathf.Abs(c.viewCamera.fieldOfView-58)<.01f,"第三人称不套用光学缩放");
                Capture(c,"10-binocular-third-person");
                c.ToggleView();c.UseFieldItem();
                for(int i=0;i<100 && c.View.BinocularBlend>0;i++)yield return null;
                c.PresentCharacterFrame();
                Check(c.View.BinocularBlend==0 && !c.View.OpticalView && c.FieldWorld.HeldToolRenderer.enabled && !c.Shore.Character.FirstPersonSkin.forceRenderingOff,"放下望远镜恢复本地手物渲染和普通 FOV");
            }
            Equip(c,FieldItem.StonePickaxe);c.Shore.SetCrouch(true);
            for(int i=0;i<20;i++)yield return null;
            c.View.ResetLook();c.View.ApplyShoreLookDelta(new Vector2(0,-65/.12f));c.PresentCharacterFrame();
            Capture(c,"11-look-down-crouching");CheckLocalMesh(c.Shore.Character);
            c.Shore.SetCrouch(false);c.View.ResetLook();
            for(int i=0;i<20;i++)yield return null;
            foreach(float aspect in new[]{16f/9,16f/10,21f/9,5f/4})
            {
                float old=c.viewCamera.aspect;
                try{c.viewCamera.aspect=aspect;c.PresentCharacterFrame();CheckPalm(c,false,"画幅 "+aspect);}
                finally{c.viewCamera.aspect=old;}
            }
            Equip(c,FieldItem.Binoculars);c.UseFieldItem();
            for(int i=0;i<100 && c.View.BinocularBlend<.999f;i++)yield return null;
            c.TogglePause();c.PresentCharacterFrame();
            Check(!c.BinocularsRaised && c.View.BinocularBlend==0 && !c.View.OpticalView
                && Mathf.Abs(c.viewCamera.fieldOfView-FishingViewRig.DesktopFieldOfView)<.01f,"暂停取消光学状态，不遗留缩放或隐藏的手");
            c.TogglePause();c.PresentCharacterFrame();
            c.Shore.BoardImmediately();
            var shop=c.TackleShop;
            rb.position=shop.mooringPoint.position;rb.rotation=shop.transform.rotation;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"从实际商店登陆点上岸");
            c.Shore.Body.enabled=false;
            c.Shore.Actor.SetPositionAndRotation(shop.purchasePoint.position+Vector3.up*.05f,shop.transform.rotation);
            c.Shore.Body.enabled=true;Physics.SyncTransforms();c.Shore.Body.Move(Vector3.down*.1f);
            c.View.ResetLook();
            for(int i=0;i<12;i++)yield return null;
            c.PresentCharacterFrame();FishingCashierValidation.ValidateInScene(c);Capture(c,"12-shop-attendant");
            Debug.Log("[FIRST-PERSON] EVIDENCE: "+_evidence+"（相机几何图，不替代完整 HUD Player 验收）");
        }
        private static void Equip(FishingPrototype c,FieldItem item)
        {
            var bag=c.Career.Backpack;
            Check(bag.Count(item)>0 || bag.TryAdd(item,1),"准备装备 "+item);
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==item){if(i!=0)bag.Move(i,0);break;}
            c.SelectFieldSlot(0);
        }
        private static void CheckPalm(FishingPrototype c,bool left,string label)
        {
            var character=c.Shore.Character;var goal=left?c.Hands.Left:c.Hands.Right;
            var point=c.viewCamera.WorldToViewportPoint(character.PalmPosition(left));
            Check(goal.Visible && Vector3.Distance(character.PalmPosition(left),goal.Palm.position)<.025f,label+" 实际掌心到达独立握点");
            Check(point.z>.18f && point.x>.04f && point.x<.96f && point.y>.025f && point.y<.56f,label+" 平视掌心位于下方可见区 "+point.ToString("F3"));
        }
        private static void CheckLocalMesh(FishingCharacter character)
        {
            var source=character.BodySkin.sharedMesh;var local=character.FirstPersonSkin.sharedMesh;
            Check(source!=local && local.vertexCount==source.vertexCount && character.HiddenNeckTriangles>0,"本地调整头颈/近眼躯干的绘制，不缩骨、不换手部顶点");
            Check(character.BodySkin.shadowCastingMode==ShadowCastingMode.ShadowsOnly && character.FirstPersonSkin.enabled
                && character.FirstPersonSkin.shadowCastingMode==ShadowCastingMode.Off,"完整身体保留阴影，本地身体不重复投影");
            Check(character.WaistCover!=null && character.WaistCover.enabled && character.WaistCover.shadowCastingMode==ShadowCastingMode.Off,
                "本地裤腰封口存在且不重复投影");
            var visible=new HashSet<int>(local.triangles);var weights=source.boneWeights;var bones=character.BodySkin.bones;
            int handVertices=0,missingHands=0,visibleNeck=0;
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];
                float neck=Contribution(w,bones,n=>n=="Neck" || n=="Head");
                if(neck>.1f && visible.Contains(i))visibleNeck++;
                if(Contribution(w,bones,IsHand)>.5f){handVertices++;if(!visible.Contains(i))missingHands++;}
            }
            Check(visibleNeck==0,"本地索引不包含颈部/头部顶点");
            Check(handVertices>100 && missingHands==0,"真实手部几何完整保留："+handVertices+" 个顶点");
        }
        private static bool IsHand(string name)=>name.StartsWith("Wrist") || name.StartsWith("Thumb") || name.StartsWith("Index") || name.StartsWith("Middle") || name.StartsWith("Ring") || name.StartsWith("Pinky");
        private static float Contribution(BoneWeight w,Transform[] bones,Func<string,bool> matches)
        {
            return One(w.boneIndex0,w.weight0)+One(w.boneIndex1,w.weight1)+One(w.boneIndex2,w.weight2)+One(w.boneIndex3,w.weight3);
            float One(int index,float weight)=>index<bones.Length && bones[index]!=null && matches(bones[index].name)?weight:0;
        }
        private static void CheckGrasp(FishingPrototype c,FieldItem item)
        {
            bool binocular=item==FieldItem.Binoculars,pick=item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe;
            if(!binocular && !pick)return;
            var body=c.Shore.Character.BodySkin;var baked=new Mesh();
            try
            {
                body.BakeMesh(baked);var vertices=baked.vertices;var weights=body.sharedMesh.boneWeights;var bones=body.bones;
                int hands=0,inside=0,lens=0;float scale=binocular?FishingFieldWorld.BinocularScale:FishingFieldWorld.PickScale;
                for(int i=0;i<vertices.Length;i++)
                {
                    if(Contribution(weights[i],bones,n=>IsHand(n) && (binocular || n.EndsWith(".R")))<.5f)continue;
                    hands++;var p=c.FieldWorld.HeldTool.InverseTransformPoint(body.transform.TransformPoint(vertices[i]));
                    if(binocular)
                    {
                        foreach(float x in new[]{-.15f,.15f})
                        {
                            float radius=Mathf.Lerp(.1f,.14f,Mathf.InverseLerp(-.15f,.22f,p.z));
                            float distance=new Vector2(p.x-x,p.y).magnitude;
                            if(p.z>-.15f && p.z<.25f && distance<radius-.008f/scale)inside++;
                            if(p.z>.20f && distance<.15f)lens++;
                        }
                    }
                    else
                    {
                        float radius=Mathf.Lerp(.025f,.038f,Mathf.InverseLerp(-.26f,.33f,p.y));
                        if(p.y>-.26f && p.y<.33f && new Vector2(p.x,p.z).magnitude<radius-.008f/scale)inside++;
                    }
                }
                Debug.Log("[FIRST-PERSON] GRASP: "+item+" handVertices="+hands+" deepInside="+inside+" frontLens="+lens);
                Check(lens==0,item+" 手部不进入前物镜光路");
                Check(inside<=hands*.01f,item+" 深入物体 8mm 以上的手部顶点不超过 1%");
            }
            finally{UnityEngine.Object.Destroy(baked);}
        }
        private static void Describe(FishingPrototype c,string label)
        {
            var ch=c.Shore.Character;var camera=c.viewCamera;
            Debug.Log("[FIRST-PERSON] METRIC: "+label+" eye="+ch.transform.InverseTransformPoint(ch.EyePosition).ToString("F3")
                +" reach="+ch.ArmReach(true).ToString("F3")+" / "+ch.ArmReach(false).ToString("F3")
                +" palms="+camera.transform.InverseTransformPoint(ch.PalmPosition(true)).ToString("F3")+" / "+camera.transform.InverseTransformPoint(ch.PalmPosition(false)).ToString("F3")
                +" viewport="+camera.WorldToViewportPoint(ch.PalmPosition(true)).ToString("F3")+" / "+camera.WorldToViewportPoint(ch.PalmPosition(false)).ToString("F3"));
        }
        private static void Capture(FishingPrototype c,string label)
        {
            var camera=c.viewCamera;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;float aspect=camera.aspect;
            var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.aspect=1280f/720;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(_evidence,label+".png"),image.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;camera.aspect=aspect;RenderTexture.active=oldActive;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);}
        }
    }
}
