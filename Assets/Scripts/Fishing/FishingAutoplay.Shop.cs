using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingAutoplay
    {
        /// <summary>PR17 route/purchase checks adapted to v10 coastline, v8 workshop saves and frame-owned input.</summary>
        public static IEnumerator ValidateBeachShop(FishingPrototype c)
        {
            RequireShop(c.Automated && c.HasIsolatedProfile, "isolated automated profile only");
            var shop = c.TackleShop;
            RequireShop(shop != null && shop.Available, "authored shop and all landing references available");
            var original = c.Career.Snapshot();
            var previousKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var previousInputSettings=InputSystem.settings;
            var fixtureInputSettings=UnityEngine.Object.Instantiate(previousInputSettings);
            fixtureInputSettings.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                // 无焦点的编辑器批处理默认写 Editor 输入缓冲；此临时夹具走真实玩家缓冲，结束即还原。
                fixtureInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                fixtureInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                InputSystem.settings=fixtureInputSettings;
                InputSystem.EnableDevice(keyboard);
                RequireShop(c.NavigationCount == 17 && c.ShopNavigationIndex == 9 && c.Shore.Sites.Length == 4, "seventeen waypoints including home, two islets and original four survey sites");
                for (int i = 0; i < c.NavigationCount; i++)
                {
                    c.SelectNavigation(i); var p = c.NavigationPosition;
                    RequireShop(!float.IsNaN(p.x) && !string.IsNullOrEmpty(c.NavigationName), "safe navigation lookup " + i);
                }
                c.SelectNavigation(c.ShopNavigationIndex);
                RequireShop(c.NavigationName == shop.DisplayName && c.NavigationPosition == shop.mooringPoint.position, "boat shop waypoint uses mooring, not survey array");
                RequireShop(shop.clerkSpaces.Length == 2 && shop.customerSpaces.Length == 4, "two clerk anchors and four customer anchors retained");
                foreach (var marker in shop.clerkSpaces.Concat(shop.customerSpaces))
                    RequireShop(c.Shore.Clearance(marker.position), "reserved floor space clear " + marker.name);
                SetShopBoat(c, shop.mooringPoint.position, shop.transform.rotation);
                RequireShop(!shop.Toggle() && !shop.Buy(UpgradeTrack.Line) && !c.BuyUpgrade(UpgradeTrack.Line), "boat cannot shop remotely");
                RequireShop(c.Shore.TryDisembark(), "real shop landing passes support, clearance and transfer checks");
                RequireShop(c.Shore.ShopLanding && !c.Shore.FreeLanding && !c.Shore.HasSurveySite && c.Shore.CurrentSite == -1, "named shop landing has no survey task index");
                RequireShop(c.NavigationPosition == shop.entrance.position, "ashore shop waypoint uses entrance");
                c.viewCamera.transform.rotation = shop.transform.rotation;
                for (int i = 0; i < 500 && Vector3.Distance(c.Shore.Position, shop.purchasePoint.position) > .35f; i++)
                {
                    ShopWalkToward(c, shop.purchasePoint.position);
                    if (i % 4 == 0) yield return null;
                }
                RequireShop(Vector3.Distance(c.Shore.Position, shop.purchasePoint.position) < .5f, "CharacterController walked deck/ramp/door/counter; actual=" + shop.transform.InverseTransformPoint(c.Shore.Position));
                RequireShop(shop.Inside && shop.NearCounter && !c.BuyUpgrade(UpgradeTrack.Line), "counter is recognized but closed shop has no purchase authority");
                var funded = c.Career.Snapshot(); funded.coins = 500; c.RestoreShopTestProfile(funded);
                c.Career.Backpack.Select(2);
                int coins = c.Career.Coins;
                var feet = c.Shore.Position;
                RequireShop(ShopKeys(c, keyboard, Key.E, Key.Digit1, Key.W, Key.F, Key.G) && c.DialogueOpen && c.Residents.ActiveId == 4, "E opens the original cashier dialogue through production input");
                c.Shore.Tick(true, .05f); c.SelectFieldSlot(0); c.UseFieldItem();
                RequireShop(!c.ShopOpen && c.Career.Coins == coins && c.Career.Backpack.Selected == 2 && c.Shore.Position == feet && !c.Shore.TryBoard(), "dialogue opening frame cannot purchase, move, equip, use tools or board");
                c.Residents.Choose(1);
                RequireShop(c.Residents.Speech.Contains("望远镜") && c.Residents.Speech.Contains("金币不足"), "cashier explains actual goods and purchase rules");
                c.Residents.Close(); yield return null;
                RequireShop(ShopKeys(c, keyboard, Key.Tab, Key.Digit1, Key.W, Key.F, Key.G), "opening Tab frame consumed");
                c.Shore.Tick(true, .05f); c.SelectFieldSlot(0); c.UseFieldItem();
                RequireShop(c.ShopOpen && c.Career.Coins == coins && c.Career.Backpack.Selected == 2 && c.Shore.Position == feet && !c.Shore.TryBoard(), "opening frame cannot purchase, move, use tools, change hotbar or board");
                yield return null;
                var empty = c.Career.Snapshot(); empty.coins = 0; c.RestoreShopTestProfile(empty);
                RequireShop(!shop.Buy(UpgradeTrack.Line) && c.Career.Coins == 0, "insufficient funds never charge");
                c.RestoreShopTestProfile(funded); c.Career.Backpack.Select(2);
                c.Career.CompleteShoreTask(0);
                RequireShop(c.Career.Backpack.TryCollect(0, FieldItem.Shell, 3), "pickup save fixture");
                c.Career.Backpack.Observe(LandAnimalKind.Fox);
                var bag = c.Career.Backpack; var workshop = c.Career.Workshop;
                RequireShop(bag.TryAdd(FieldItem.IronOre, 2) && bag.TryAdd(FieldItem.Coal, 1) && bag.TryAdd(FieldItem.Driftwood, 1), "v8 materials fixture");
                RequireShop(FishingRecipes.Craft(bag, "planks", CraftStation.Hand), "v10 hand crafting retained");
                var furnace = workshop.Stations.First(s => s.kind == CraftStation.Furnace);
                RequireShop(workshop.Start(bag, furnace.id, "iron", 1, FieldItem.Coal) && workshop.Harvest(bag, 0, FieldItem.Stone, 1), "v8 smelting and renewable cooldown fixture");
                workshop.Tick(1);
                string workshopBefore = JsonUtility.ToJson(workshop.Snapshot());
                var viewBefore = c.viewCamera.transform.rotation;
                c.View.ApplyShoreLookDelta(new Vector2(100, 100)); c.View.ApplyView(true, false);
                RequireShop(Quaternion.Angle(viewBefore, c.viewCamera.transform.rotation) < .01f && !c.View.IsLooking, "shop blocks look and releases cursor ownership");
                for (int i = 0; i < 3; i++)
                {
                    var track = (UpgradeTrack)i; int before = c.Career.Coins, price = c.Career.NextUpgradeCost(track);
                    RequireShop(ShopKeys(c, keyboard, (Key)((int)Key.Digit1 + i)), "purchase key consumed " + track);
                    RequireShop(c.Career.Level(track) == 1 && c.Career.Coins == before - price && bag.Selected == 2, "exact price/effect without hotbar leak " + track);
                }
                var reloaded = FishingSaveStore.Load(c.SavePath, out var warning);
                RequireShop(warning.Length == 0 && reloaded.Snapshot().version == 12 && reloaded.Coins == c.Career.Coins
                    && reloaded.LineLevel == 1 && reloaded.ReelLevel == 1 && reloaded.HoldLevel == 1, "v9 upgrade save round trip");
                RequireShop(reloaded.ShoreTaskMask == 1 && reloaded.Backpack.Collected(0) && reloaded.Backpack.Seen(LandAnimalKind.Fox)
                    && reloaded.Backpack.Selected == 2 && reloaded.Backpack.Count(FieldItem.Plank) == bag.Count(FieldItem.Plank), "survey, pickup, animal, hotbar and crafted materials survive shopping");
                RequireShop(JsonUtility.ToJson(reloaded.Workshop.Snapshot()) == workshopBefore, "all v8 stations, furnace reservation/progress and 64 resource cooldowns preserved exactly");
                RequireShop(shop.Buy(UpgradeTrack.Line) && c.Career.LineLevel == 2, "second upgrade tier");
                coins = c.Career.Coins; RequireShop(!shop.Buy(UpgradeTrack.Line) && c.Career.Coins == coins, "max level cannot charge twice");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    string directory = "Logs/shop-preview-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(directory);
                    string image = directory + "/shop-open.png"; ScreenCapture.CaptureScreenshot(image);
                    for (int i = 0; i < 12; i++) yield return null;
                    if (File.Exists(image)) Debug.Log("[BEACH-SHOP] GAMEVIEW: " + Path.GetFullPath(image));
                    else Debug.LogWarning("[BEACH-SHOP] Game-view screenshot was not produced; visual acceptance remains pending.");
                }
                RequireShop(ShopKeys(c, keyboard, Key.I) && c.InventoryOpen && c.Paused && !c.ShopOpen && !shop.Toggle(), "I replaces shop with inventory, never overlaps it");
                c.ToggleInventory(); yield return null;
                RequireShop(ShopKeys(c, keyboard, Key.Tab) && c.ShopOpen, "Tab reopens only at counter"); yield return null;
                RequireShop(ShopKeys(c, keyboard, Key.J) && c.JournalOpen && c.Paused && !c.ShopOpen, "J replaces shop with journal");
                c.ToggleJournal(); yield return null;
                RequireShop(ShopKeys(c, keyboard, Key.Tab) && c.ShopOpen, "Tab entry after journal"); yield return null;
                feet = c.Shore.Position;
                RequireShop(ShopKeys(c, keyboard, Key.Escape, Key.Digit2, Key.W, Key.G) && !c.ShopOpen && !c.Paused, "Esc closes without opening pause or purchasing");
                c.Shore.Walk(Vector2.up, false, .05f); c.SelectFieldSlot(0);
                RequireShop(c.Shore.Position == feet && bag.Selected == 2 && !c.Shore.TryBoard() && !c.BuyUpgrade(UpgradeTrack.Reel), "closing frame has no movement, hotbar, boarding or purchase authority");
                yield return null; c.SelectFieldSlot(0); RequireShop(bag.Selected == 0, "hotbar recovers next frame");
                RequireShop(c.ToggleShop(), "reopen before focus loss"); yield return null;
                RequireShop(c.TickShopInput(keyboard, false, false, false) && !c.ShopOpen, "focus loss closes and consumes frame"); yield return null;
                RequireShop(c.ToggleShop(), "reopen before XR boundary"); yield return null;
                RequireShop(c.TickShopInput(keyboard, false, true, true) && !c.ShopOpen, "simulated XR activation closes shop (not a hardware test)"); yield return null;
                RequireShop(c.ToggleShop(), "reopen before pause"); c.TogglePause();
                RequireShop(!c.ShopOpen && !shop.Buy(UpgradeTrack.Reel), "pause clears modal and purchase lock"); c.TogglePause(); yield return null;
                RequireShop(c.ToggleShop(), "reopen before distance invalidation"); yield return null;
                SetShopFeet(c, shop.entrance.position + Vector3.up * .08f);
                RequireShop(!shop.Buy(UpgradeTrack.Reel) && !c.BuyUpgrade(UpgradeTrack.Reel), "leaving counter immediately revokes purchase authority");
                c.TickShopInput(keyboard, false, true, false); RequireShop(!c.ShopOpen, "distance invalidation releases modal"); yield return null;
                for (int i = 0; i < 500 && Vector3.Distance(c.Shore.Position, c.Shore.LandingPoint) > 1.5f; i++)
                {
                    ShopWalkToward(c, c.Shore.LandingPoint);
                    if (i % 4 == 0) yield return null;
                }
                RequireShop(c.Shore.TryBoard() && !shop.NearCounter, "real exit/ramp return uses unchanged boarding policy");
                c.ReturnToHarbor(); yield return null;
                coins = c.Career.Coins; int harborPrice = c.Career.NextUpgradeCost(UpgradeTrack.Reel);
                RequireShop(c.AtHarbor && c.BuyUpgrade(UpgradeTrack.Reel) && c.Career.Coins == coins - harborPrice, "original harbor upgrade channel retained");
                SetShopBoat(c, shop.mooringPoint.position, shop.transform.rotation);
                RequireShop(c.Shore.TryDisembark(), "repeat named landing");
                SetShopFeet(c, c.Shore.Sites[0].Tasks[0] + Vector3.up * .08f);
                RequireShop(c.Expedition.Resolve() == 0, "arriving from a shop or free coast does not disable nearby beacon tasks");
                SetShopFeet(c, shop.purchasePoint.position + Vector3.up * .08f);
                RequireShop(c.ToggleShop(), "rescue modal fixture"); c.ReturnToHarbor();
                RequireShop(c.AtHarbor && !c.ShopOpen && !c.Ashore, "rescue leaves neither shop lock nor stranded actor");
                Debug.Log("[BEACH-SHOP] CHECK: purchases, permissions, modal frames, navigation, real landing/walking/boarding and v8/workshop persistence complete.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings=previousInputSettings;
                UnityEngine.Object.Destroy(fixtureInputSettings);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                shop.Close(); if (c.InventoryOpen) c.ToggleInventory(); if (c.JournalOpen) c.ToggleJournal(); if (c.Paused) c.TogglePause();
                c.ReturnToHarbor(); c.RestoreShopTestProfile(original); c.SelectNavigation(1);
            }
        }
        private static bool ShopKeys(FishingPrototype c, Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            // Input System 1.14 的 ButtonControl 首次读取才启用边沿追踪；先在释放状态订阅，避免快速注入丢首个按下。
            foreach(var key in keys) _ = keyboard[key].wasPressedThisFrame;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
            bool ePressed=keyboard.eKey.wasPressedThisFrame,tabPressed=keyboard.tabKey.wasPressedThisFrame;
            bool consumed=c.TickShopInput(keyboard, true, true, false);
            if(!consumed) Debug.Log("[BEACH-SHOP] INPUT-DIAG: keys="+string.Join(",",keys)+" enabled="+keyboard.enabled
                +" eDown="+keyboard.eKey.isPressed+" eEdge="+ePressed+" tabEdge="+tabPressed+" near="+c.TackleShop.NearCounter
                +" open="+c.ShopOpen+" blocked="+c.ShopBlocksGameplay+" paused="+c.Paused+" inventory="+c.InventoryOpen
                +" journal="+c.JournalOpen+" pending="+c.HasPendingCatch+" aligning="+c.Expedition.Aligning+" message="+c.Message);
            return consumed;
        }
        private static void ShopWalkToward(FishingPrototype c, Vector3 destination)
        {
            var direction = Quaternion.Inverse(Quaternion.Euler(0, c.viewCamera.transform.eulerAngles.y, 0)) * (destination - c.Shore.Position);
            c.Shore.Walk(new Vector2(direction.x, direction.z).normalized, false, .05f);
        }
        private static void SetShopBoat(FishingPrototype c, Vector3 position, Quaternion rotation)
        {
            var rb = c.Player.engine.RB; rb.velocity = rb.angularVelocity = Vector3.zero;
            rb.position = position; rb.rotation = rotation; c.Player.transform.SetPositionAndRotation(position, rotation); Physics.SyncTransforms();
        }
        private static void SetShopFeet(FishingPrototype c, Vector3 position)
        { c.Shore.Body.enabled = false; c.Shore.Actor.position = position; c.Shore.Body.enabled = true; Physics.SyncTransforms(); }
        private static void RequireShop(bool ok, string message)
        { if (!ok) throw new Exception("Shop regression: " + message); Debug.Log("[BEACH-SHOP] CHECK: " + message); }
    }
    public sealed partial class FishingPrototype
    {
        public void RestoreShopTestProfile(FishingSaveData data)
        {
            if (!Automated || !HasIsolatedProfile) throw new InvalidOperationException("Shop fixtures require automation and an isolated profile.");
            if (!FishingCareer.TryRestore(data, out var career)) throw new InvalidOperationException("Invalid shop test profile.");
            CancelWorldAction();
            Career = career; RefreshHold(); WorkshopWorld.RefreshFromCareer(); FieldWorld.RefreshPickups(); Shore.RefreshMarkers(); Farm?.Refresh(); Save();
        }
    }
}
