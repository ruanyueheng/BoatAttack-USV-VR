using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV062CreativeValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException("[V062-CREATIVE] "+label);Debug.Log("[V062-CREATIVE] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Creative frontend checks own an isolated career");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);c.XRNotebook.Close();
            if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
            var original=c.Career.Snapshot();bool creative=c.CreativeMode;
            try
            {
                c.SetCreativeMode(false);c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                Check(c.ReturnHome(),"The shared quick-home action reaches supported home ground");yield return null;
                var world=c.HomeEcology;world.ClearHabitatFilter();world.Refresh();
                var ordinary=world.Choices(0);
                Check(ordinary.Count==6 && ordinary.All(choice=>choice.Label.Contains("0/1")),"Normal facility menus describe real missing kits");
                ordinary[0].Run();Check(c.Career.AquariumLevel==0,"The ordinary world action rejects an absent aquarium kit");
                c.SetCreativeMode(true);
                var free=world.Choices(0);
                Check(free.Count==6 && free.All(choice=>choice.Enabled && choice.Label.Contains("免套件")),"All six actual deployment menu entries advertise enabled zero-kit construction");
                free[0].Run();free[1].Run();
                Check(c.Career.AquariumLevel==1 && c.Career.HabitatLevel(LandAnimalKind.Rabbit)==1
                    && c.Career.Backpack.Count(FieldItem.Aquarium)==0 && c.Career.Backpack.Count(FieldItem.RabbitHutch)==0,
                    "The shared aquarium and animal menu callbacks build visible facilities from zero kits");
                Check(world.transform.Find("家园生态展示/家园水族箱")!=null
                    && world.transform.Find("家园生态展示/"+FishingCareer.HabitatName(LandAnimalKind.Rabbit))!=null,"Both zero-kit transactions create their actual world displays");
                c.SetCreativeMode(false);var fox=world.Choices(0)[2];
                Check(fox.Label.Contains("0/1"),"Disabling creative mode immediately restores the real kit requirement text");
                fox.Run();Check(c.Career.HabitatLevel(LandAnimalKind.Fox)==0,"The same facility frontend rejects zero kits after disabling creative mode");

                c.SetCreativeMode(true);c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                var notebook=c.XRNotebook;
                // Exercise the real shared layout/callback without pretending a batch Editor is a native XR session.
                typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(notebook,true);
                var canvas=(Canvas)typeof(FishingXRNotebook).GetField("_canvas",Private).GetValue(notebook);canvas.gameObject.SetActive(true);
                typeof(FishingXRNotebook).GetField("_page",Private).SetValue(notebook,2);
                typeof(FishingXRNotebook).GetField("_craftBasics",Private).SetValue(notebook,true);
                typeof(FishingXRNotebook).GetField("_craftSelection",Private).SetValue(notebook,0);
                typeof(FishingXRNotebook).GetField("_craftListOffset",Private).SetValue(notebook,0);
                var refresh=(Action)Delegate.CreateDelegate(typeof(Action),notebook,typeof(FishingXRNotebook).GetMethod("Refresh",Private));refresh();
                var make=(Image)typeof(FishingXRNotebook).GetField("_craftMake",Private).GetValue(notebook);
                var costs=(Text)typeof(FishingXRNotebook).GetField("_craftCosts",Private).GetValue(notebook);
                var recipe=(CraftRecipe)typeof(FishingXRNotebook).GetField("_selectedRecipe",Private).GetValue(notebook);
                Check(recipe.Id=="planks" && make.color==FishingUITheme.Selected && costs.text.Contains("免材料"),"The actual notebook craft button is available and labelled free with zero driftwood");
                make.GetComponent<FishingNotebookButton>().Command();
                Check(c.Career.Backpack.Count(FieldItem.Plank)==4 && c.Career.Backpack.Count(FieldItem.Driftwood)==0,"The real notebook craft callback produces usable planks from zero material");
                c.SetCreativeMode(false);refresh();
                Check(make.color==FishingUITheme.Disabled && !costs.text.Contains("免材料"),"Disabling mode restores the same notebook button's insufficient-material state");
                make.GetComponent<FishingNotebookButton>().Command();
                Check(c.Career.Backpack.Count(FieldItem.Plank)==4,"The formerly free notebook callback cannot add another output in normal mode");
                notebook.Close();

                Check(c.Shore.Ground(c.Farm.Target(0).position+Vector3.back*1.1f,out var feet)
                    && c.Shore.Relocate(feet,Quaternion.identity),"The first farm plot has a supported working position");
                c.SetCreativeMode(true);
                Check(c.Farm.InteractAt(0) && c.Farm.Hint(0).Contains("创造模式免消耗") && c.Farm.Status.Contains("种子 / 水免耗"),"The actual farm frontend explains free planting after tilling");
                Check(c.Farm.InteractAt(0) && c.Career.Farm.Plot(0).planted && c.Career.Backpack.Count(FieldItem.TurnipSeeds)==0,"The actual farm interaction plants with zero seeds");
                c.SetCreativeMode(false);
                Check(!c.Farm.Status.Contains("免耗") && !c.Farm.Hint(0).Contains("创造模式"),"Turning mode off restores ordinary farm descriptions");
                c.SetCreativeMode(true);c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                Check(c.Shore.Ground(c.CoastWorld.Grill.position+Vector3.back*1.6f,out feet)
                    && c.Shore.Relocate(feet,Quaternion.identity) && c.CoastWorld.NearGrill,"The real grill has a supported zero-fish practice stance");
                typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(notebook,true);canvas.gameObject.SetActive(true);
                typeof(FishingXRNotebook).GetField("_page",Private).SetValue(notebook,17);refresh();
                var actions=(Action[])typeof(FishingXRNotebook).GetField("_actions",Private).GetValue(notebook);
                var labels=(Text[])typeof(FishingXRNotebook).GetField("_labels",Private).GetValue(notebook);
                Check(actions[0]!=null && labels[0].text.Contains("零材料练习"),"The actual VR grill menu exposes a practice button with an empty fishbox");
                actions[0]();
                var raw=(GameObject)typeof(FishingV028CoastWorld).GetField("_raw",Private).GetValue(c.CoastWorld);
                Check(c.Career.PracticeBarbecue && c.Career.Barbecue.fish!=null && c.Career.Count==0 && c.Career.Catches==0,
                    "The actual grill menu callback starts a visible roast without inventing or consuming real catches");
                Check(raw!=null && raw.activeInHierarchy,"The zero-material menu callback creates the actual fish model on the grill");
                var saved=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning.Length==0 && saved.Barbecue.fish==null && saved.Catches==0,"Saving during practice omits its temporary timer and preserves a readable real career");
                notebook.Close();for(int i=0;i<72;i++)c.Career.TickBarbecue(.25f);
                Check(c.CoastWorld.Collect() && c.Career.Backpack.Count(FieldItem.GrilledFish)==1 && c.Career.Catches==0,
                    "The actual world collect action delivers one timed practice meal with no catch-statistic changes");
                yield return null;
            }
            finally
            {
                c.XRNotebook.Close();c.SetCreativeMode(creative);c.RestoreShopTestProfile(original);c.HomeEcology.Refresh();c.Farm.Refresh();
            }
        }
    }
}
