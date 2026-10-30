using System;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.EditorTools
{
    public static class FishingXRParityValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Field(FishingXRNotebook n,string name)=>typeof(FishingXRNotebook).GetField(name,Private).GetValue(n);
        private static void Call(FishingXRNotebook n,string name,params object[] args)=>typeof(FishingXRNotebook).GetMethod(name,Private).Invoke(n,args);
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[XR-PARITY] CHECK: "+text);}
        public static void Run(FishingPrototype c)
        {
            Check(XRSettings.isDeviceActive && c.HasIsolatedProfile,"Migration checks execute the native XR branch with an isolated save");
            var original=c.Career.Snapshot();var n=c.XRNotebook;
            try
            {
                c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                n.Open(31);var actions=(Action[])Field(n,"_actions");
                Check(actions.All(a=>a!=null),"Journal index exposes catalog/filter, legend, nature, calendar, voyage, world, album and letters");
                n.Open(10);string detail=FishingSpecies.Of(FishKind.Coral).Habitat;
                actions[0]();Call(n,"Refresh");
                Check(((Text)Field(n,"_status")).text.Contains(detail),"A fish selection retains habitat details through the real post-click refresh");
                Call(n,"UpdateHomePointer",7,false);Check(((Text)Field(n,"_specimenHabitat")).gameObject.activeSelf && ((Text)Field(n,"_specimenHabitat")).text.Contains(detail),"Pointing at a footer navigation button does not replace the selected specimen details");
                Check(((string[])Field(n,"_pageContent"))[0].Contains("珍珠变体") && ((string[])Field(n,"_pageContent"))[0].Contains("最大纪录"),"Catalog retains rare-discovery and weight-record information");
                n.Open(31);actions[1]();
                Check(((string[])Field(n,"_pageContent"))[0]=="—" && !((Text)Field(n,"_status")).text.Contains(detail),"An empty discovered-only catalog does not show a stale undiscovered selection");
                n.Open(29);
                for(int i=0;i<LegendRules.ClueCount;i++)Check(((string[])Field(n,"_pageContent"))[i].Contains(LegendRules.ClueHint(i)),"Locked legend clue retains its real unlock hint "+i);
                Check(actions[3]!=null,"Legend page exposes the shared survey-navigation transaction");
                n.Open(33);
                for(int i=0;i<4;i++)for(int t=0;t<2;t++)Check(((string[])Field(n,"_pageContent"))[i].Contains(FishingExploration.TaskName(i*2+t)),"Coastal checklist retains real task "+(i*2+t));
                bool precision=c.Expedition.PrecisionGuide;actions[5]();Check(c.Expedition.PrecisionGuide!=precision,"VR can toggle the shared precision-beacon guide");actions[5]();
                n.Open(32);Check(actions[4]!=null && actions[5]!=null,"World journal exposes navigation and the existing story-reading transaction");
                n.Open(15);actions[0]();Call(n,"Refresh");
                Check(((Text)Field(n,"_status")).text.Contains(BayClimate.MarineHint((MarineKind)0)),"Natural-history selection also survives post-click refresh");
                n.Open(11);
                Check(((Text)Field(n,"_pageSubtitle")).text.Contains("0 / 5"),"Tutorial progress stays in the header while a card is pointed at");
                var icons=(Image[])Field(n,"_homeIcons");
                for(int i=0;i<4;i++)Check(icons[i].gameObject.activeSelf && icons[i].sprite==FishingUITheme.ControllerGlyph(new[]{"A","B","X","Y"}[i]),"Tutorial uses the actual controller glyph "+i);
                var captions=(Text[])Field(n,"_homeCaptions");
                for(int i=0;i<6;i++)Check(-captions[i].rectTransform.anchoredPosition.y>=-icons[i].rectTransform.anchoredPosition.y+icons[i].rectTransform.rect.height
                    && captions[i].preferredHeight<=captions[i].rectTransform.rect.height,"Tutorial preview neither overlaps its icon nor clips its text "+i);
                var feedback=(Canvas)Field(n,"_feedback");
                Check(feedback.GetComponent<Image>().sprite==FishingUITheme.PanelVeilSprite && feedback.GetComponentsInChildren<Collider>(true).Length==0,"Guidance uses Windows ReadingVeil and adds no ray-intercepting colliders");
                Check(feedback.GetComponentsInChildren<Graphic>(true).All(g=>g.material.renderQueue==FishingUITheme.WorldMaterial.renderQueue),"Guidance art and paper share a render queue so the background cannot cover the decorations");
                Check(((Text)Field(n,"_feedbackTitle")).font==FishingUIFont.Font && ((Text)Field(n,"_feedbackText")).font==FishingUIFont.Font,
                    "VR action prompts retain the Windows HUD's body face rather than using a small display heading");
                Check(feedback.GetComponentsInChildren<Shadow>(true).Length==2,"Both guidance text layers retain the Windows dark shadow for legibility");
                Check(GlyphDegrees((Text)Field(n,"_feedbackText"),1.4f)>=1 && GlyphDegrees((Text)Field(n,"_feedbackTitle"),1.4f)>=1.5f,
                    "Actual Chinese glyph ink spans at least 1 degree for guidance and 1.5 degrees for its title at the real parent scale and reading distance");
                ValidateRegionPresentation(c);

                var bag=c.Career.Backpack;Check(bag.TryAdd(FieldItem.Driftwood,9),"Nine wood pieces fit the isolated inventory");
                int source=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==FieldItem.Driftwood);
                Call(n,"SelectInventoryItem",source);
                Check(n.InventoryPage && !c.InventoryOpen,"VR item operations use their own visible notebook boundary");
                actions[3]();Check(c.MoveFieldItem(source,30,true),"The shared inventory command accepts the VR target-selection page");
                Check(bag.At(source).count+bag.At(30).count==9 && Math.Abs(bag.At(source).count-bag.At(30).count)==1,"A half-stack move preserves all nine items and divides them evenly");
                int retained=bag.At(30).count;
                Call(n,"SelectInventoryItem",source);actions[4]();Check(bag.Count(FieldItem.Driftwood)==9,"The first discard press only requests confirmation");
                Check(bag.TryAdd(FieldItem.Driftwood,1),"An intervening pickup changes the selected stack");
                Call(n,"Refresh");actions[4]();Check(bag.Count(FieldItem.Driftwood)==10,"A stale discard quote cannot delete a changed stack");
                actions[4]();Check(bag.Count(FieldItem.Driftwood)==retained,"Fresh confirmed discard removes exactly the selected stack");
                int guide=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==FieldItem.FieldGuide);
                Call(n,"SelectInventoryItem",guide);
                Check(actions[4]==null && !c.DiscardFieldItem(guide,FieldItem.FieldGuide,1),"The natural guide remains protected in both the UI and shared transaction");
                n.Close();
                Check(!c.MoveFieldItem(30,31) && !c.DiscardFieldItem(30,FieldItem.Driftwood,retained),"Closed UI cannot move or discard inventory through the VR command boundary");
                n.Open(1);
                var slots=(Image[])Field(n,"_bagSlots");Check(slots.Length==36 && slots.All(s=>s.gameObject.activeInHierarchy),"Windows backpack layout exposes all 27 storage and nine quick slots at once");
                Call(n,"SelectDesktopBagSlot",30);Call(n,"SelectDesktopBagSlot",31);
                Check(bag.At(30).count==0 && bag.At(31).count==retained,"The visible 36-slot route invokes the real move transaction");
                Call(n,"SelectDesktopBagSlot",31);Call(n,"DiscardDesktopBagSelection");Check(bag.At(31).count==retained,"Visible Windows-style discard still requires confirmation");
                Call(n,"DiscardDesktopBagSelection");Check(bag.At(31).count==0,"Second visible discard press settles only once");
                var directory=System.IO.Path.GetFullPath("Logs/xr-windows-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(directory);
                c.RestoreShopTestProfile(FishingAcceptanceCases.CreateCareer(FishingAcceptanceCase.Journal).Snapshot());
                c.Career.Backpack.TryAdd(FieldItem.Driftwood,9);_ = c.Career.Backpack.TryAdd(FieldItem.Rope,3);
                typeof(FishingXRNotebook).GetField("_knownCatalog",Private).SetValue(n,false);
                var canvas=(Canvas)Field(n,"_canvas");
                n.Open(2);
                var recipes=FishingRecipes.ForStation(c.WorkshopWorld.Context,c.WorkshopWorld.Context==CraftStation.Hand);
                int woodRecipe=Array.FindIndex(recipes,r=>r.Id=="planks");
                if(woodRecipe>=0)
                {
                    typeof(FishingXRNotebook).GetField("_craftSelection",Private).SetValue(n,woodRecipe);Call(n,"Refresh");
                    int wood=c.Career.Backpack.Count(FieldItem.Driftwood),planks=c.Career.Backpack.Count(FieldItem.Plank);
                    Check(c.Career.Backpack.Count(FieldItem.Driftwood)==wood,"Selecting a recipe alone does not spend materials");
                    Call(n,"MakeSelectedRecipe");
                    Check(c.Career.Backpack.Count(FieldItem.Driftwood)==wood-1 && c.Career.Backpack.Count(FieldItem.Plank)==planks+4,"Windows-style explicit craft action uses the shared production recipe and exact material costs");
                }
                foreach(int page in new[]{1,2,10,29,15,33,3,32,11})
                {
                    n.Open(page);Canvas.ForceUpdateCanvases();
                    Check(canvas.GetComponentsInChildren<Text>().Where(t=>t.enabled && !string.IsNullOrWhiteSpace(t.text)).All(t=>t.preferredHeight<=t.rectTransform.rect.height+.05f),"Every visible label fits its real font metrics on Windows-style page "+page);
                    System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,"layout-"+page+".txt"),canvas.GetComponentsInChildren<Text>().Select(t=>t.name+" | "+t.fontSize+" | "+t.rectTransform.rect+" | preferred="+t.preferredWidth+" x "+t.preferredHeight+" | "+t.text.Replace("\n"," / ")));
                    FishingXRPresentationValidation.CaptureCanvas(canvas,System.IO.Path.Combine(directory,"vr-page-"+page+".png"),true);
                }
                Call(n,"ApplyGuidanceText",FishingXRHints.Guidance(c));FishingXRPresentationValidation.CaptureCanvas(feedback,System.IO.Path.Combine(directory,"vr-guidance.png"));
                Debug.Log("[XR-PARITY] WINDOWS COMPARISON: "+directory);
                FishingWreckRouteValidation.Inspect(c);
            }
            finally{n.Close();if(c.InventoryOpen)c.ToggleInventory();c.RestoreShopTestProfile(original);}
        }
        private static void ValidateRegionPresentation(FishingPrototype c)
        {
            var region=c.Regions;var type=typeof(FishingRegionPresentation);
            var shown=type.GetField("_shownAt",Private);var pending=type.GetField("_xrPending",Private);
            var group=(CanvasGroup)type.GetField("_group",Private).GetValue(region);
            float originalShown=(float)shown.GetValue(region),originalAlpha=group.alpha;bool originalPending=(bool)pending.GetValue(region);
            var position=group.transform.position;var rotation=group.transform.rotation;
            string title=region.Title,subtitle=region.Subtitle;int theme=region.Theme;
            try
            {
                region.Show("回潮渔屋","休息整备 · 再赴潮汐",0);
                var present=type.GetMethod("PresentXR",Private);
                shown.SetValue(region,Time.unscaledTime-20);
                present.Invoke(region,new object[]{false,.5f});
                Check((bool)pending.GetValue(region) && group.alpha==0,"A location announced during a film or lost tracking remains pending instead of expiring unseen");
                present.Invoke(region,new object[]{true,0f});
                Check(!(bool)pending.GetValue(region) && region.Age<.01f,"The real region presentation begins its full reading time on first visibility");
                shown.SetValue(region,Time.unscaledTime-3);present.Invoke(region,new object[]{false,.5f});
                Check(Mathf.Abs(region.Age-2.5f)<.01f && group.alpha==0,"Opening a modal pauses an already visible region banner's remaining time");
                shown.SetValue(region,Time.unscaledTime-2);present.Invoke(region,new object[]{true,0f});
                var canvas=group.GetComponent<Canvas>();
                Check(canvas.GetComponentsInChildren<RawImage>().Any(i=>i.texture==FishingUITheme.TitleGlow)
                    && canvas.GetComponentsInChildren<RawImage>().Any(i=>i.texture==FishingUITheme.GiltRule),"The VR location uses the actual Windows glow and gilt-rule artwork");
                Check(canvas.GetComponentsInChildren<Text>().Any(t=>t.font==FishingUITheme.Heading && t.text=="回潮渔屋")
                    && canvas.GetComponentsInChildren<Text>().All(t=>t.preferredHeight<=t.rectTransform.rect.height),"Location name and description use separate matching fonts and fit their measured bounds");
                Check(GlyphDegrees(group.transform.Find("Region calligraphy").GetComponent<Text>(),2.1f)>=1.5f
                    && GlyphDegrees(group.transform.Find("Region description").GetComponent<Text>(),2.1f)>=1,
                    "Region title and subtitle retain readable glyph angles in the actual gameplay transform hierarchy");
                var directory=System.IO.Path.GetFullPath("Logs/xr-region-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(directory);
                FishingXRPresentationValidation.CaptureCanvas(canvas,System.IO.Path.Combine(directory,"vr-region-home.png"),true);
                Check(FishingXRHints.Card(FishingXRHints.Context.Fishing).Contains("按下右摇杆 · 收竿起航"),"The fishing help exposes the actual right-stick exit binding");
            }
            finally
            {
                region.Show(title,subtitle,theme);shown.SetValue(region,originalShown);pending.SetValue(region,originalPending);group.alpha=originalAlpha;
                group.transform.SetPositionAndRotation(position,rotation);
            }
        }
        private static float GlyphDegrees(Text text,float distance)
        {
            text.font.RequestCharactersInTexture("海",text.fontSize,FontStyle.Normal);
            Check(text.font.GetCharacterInfo('海',out var glyph,text.fontSize,FontStyle.Normal),"The production face contains the Chinese readability probe");
            float height=(glyph.maxY-glyph.minY)*text.transform.TransformVector(Vector3.up).magnitude;
            return 2*Mathf.Atan(height/(2*distance))*Mathf.Rad2Deg;
        }
    }
}
