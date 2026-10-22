using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingAudioMixValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string SettingsPrefix = "Fishing.V032.";
        public static void Run() => FishingV011Validation.Begin("AUDIO-MIX");
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            Debug.Log("[AUDIO-MIX] CHECK: " + label);
        }
        private static object Field(object owner, string name)
        {
            var field = owner.GetType().GetField(name, Fields);
            if (field == null) throw new InvalidOperationException("Missing audio-mix inspection field: " + name);
            return field.GetValue(owner);
        }
        private static IDictionary Registry(FishingPrototype c) => (IDictionary)Field(c, "_settingAudio");
        private static object Entry(FishingPrototype c, AudioSource source) => source == null ? null : Registry(c)[source];
        private static float Base(FishingPrototype c, AudioSource source) => (float)Field(Entry(c, source), "BaseVolume");
        private static bool Near(float a, float b) => Math.Abs(a - b) < .00015f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static IEnumerator Checks(FishingPrototype c)
        {
            string temporary = Path.GetFullPath(Application.temporaryCachePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            Check(c != null && c.Automated && c.HasIsolatedProfile
                && Path.GetFullPath(c.SavePath).StartsWith(temporary, StringComparison.OrdinalIgnoreCase),
                "Audio mix checks use only the actual scene and its temporary isolated career");
            var original = c.Career.Snapshot();
            float master = c.MasterVolume, music = c.MusicVolume, ambient = c.AmbientVolume, effects = c.EffectsVolume;
            bool quiet = c.QuietMode, paused = c.Paused;
            var hud = c.GetComponent<FishingHUD>(); int tab = hud.SettingsTab;
            var prefs = CapturePreferences();
            var climate = c.Weather.Climate;
            var climateState = new Dictionary<FieldInfo, object>();
            foreach (var field in typeof(BayClimate).GetFields(Fields)) climateState.Add(field, field.GetValue(climate));
            AudioSource wind = null; float windBase = 0;
            string playerPath = Path.Combine(Application.persistentDataPath, "boatattack-fishing-v13.json");
            byte[] player = Read(playerPath), backup = Read(playerPath + ".bak");
            string evidence = Path.GetFullPath(Path.Combine("Logs", "AudioMix", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(evidence);
            try
            {
                ClosePanels(c); c.ReturnToHarbor();
                var fixture = new FishingCareer().Snapshot(); fixture.calendar.timeFixed = true; fixture.prologueSeen = true;
                c.RestoreShopTestProfile(fixture);
                if (c.QuietMode) c.ToggleQuiet();
                c.SetSetting("Master", .4f); c.SetSetting("Music", .7f); c.SetSetting("Ambient", .45f); c.SetSetting("Effects", .6f);
                for (int frame = 0; frame < 3; frame++) yield return null;
                wind = Field(c.Weather, "_audio") as AudioSource;
                Check(wind != null && Entry(c, wind) != null && c.ActionAudio != null && c.RegionalMusic != null && c.RegionalMusic.Ready,
                    "The real weather, action and regional-music sources are initialized and registered");
                windBase = Base(c, wind);
                ValidateEnvironmentSources(c, wind);
                double deadline = EditorApplication.timeSinceStartup + 16;
                while ((!RegionalAudible(c) || c.RegionalMusic.Transitioning) && EditorApplication.timeSinceStartup < deadline) yield return null;
                Check(RegionalAudible(c), "An actual regional music voice is playing before the channel isolation checks");

                c.ApplyAudioSettings(); AssertRegisteredMix(c);
                Check(Near(AudioListener.volume, .4f), "Master reaches the real listener without replacing the three independent channel values");
                c.SetSetting("Music", 0); c.ApplyAudioSettings();
                Check(CategorySilent(c, FishingAudioCategory.Music) && c.ActionAudio.ItemSource.volume > 0 && wind.volume > 0,
                    "Music zero silences music while real action and environment gains remain audible");
                var effectClip = FishingActionAudio.Clip("FarmPlant1");
                Check(effectClip != null, "An existing imported action clip is available for an isolated mixer playback probe");
                c.ActionAudio.ItemSource.PlayOneShot(effectClip); yield return null;
                Check(c.ActionAudio.ItemSource.isPlaying && c.ActionAudio.ItemSource.volume > 0,
                    "A real imported effect still plays through its existing source while BGM volume is zero");
                c.ActionAudio.ItemSource.Stop();

                c.SetSetting("Music", .7f); c.SetSetting("Effects", 0); c.ApplyAudioSettings();
                Check(CategorySilent(c, FishingAudioCategory.Effects) && RegionalAudible(c) && wind.volume > 0,
                    "Effects zero silences action feedback without muting BGM or environment gain");
                c.SetSetting("Effects", .6f); c.SetSetting("Ambient", 0); c.ApplyAudioSettings();
                Check(CategorySilent(c, FishingAudioCategory.Ambient) && c.ActionAudio.ItemSource.volume > 0 && RegionalAudible(c),
                    "Ambient zero mutes the actual engine, water, coast and wind sources independently of effects and BGM");
                AssertRegisteredMix(c);
                c.SetSetting("Ambient", .45f); c.ToggleQuiet();
                bool allMuted = true;
                foreach (DictionaryEntry pair in Registry(c))
                { var source = pair.Key as AudioSource; if (source != null) allMuted &= source.mute; }
                Check(allMuted && AudioListener.volume == 0, "Quiet mode remains a separate global mute across every registered route");
                c.ToggleQuiet();
                Check(Near(c.MusicVolume, .7f) && Near(c.AmbientVolume, .45f) && Near(c.EffectsVolume, .6f),
                    "Unmuting preserves all three independently selected volume levels");

                foreach (var frame in ValidateWindTransition(c, wind)) yield return frame;
                ValidateNotebookAudio(c);

                c.SetSetting("Master", .47f); c.SetSetting("Music", .37f); c.SetSetting("Ambient", .23f); c.SetSetting("Effects", .61f);
                c.RestoreShopTestProfile(c.Career.Snapshot());
                Check(Levels(c, .47f, .37f, .23f, .61f), "Refreshing the isolated career preserves an existing nondefault audio mix");
                c.RestoreAudioSettings();
                bool authoredRestored = true;
                foreach (DictionaryEntry pair in Registry(c))
                {
                    var source = pair.Key as AudioSource; if (source == null) continue;
                    authoredRestored &= Near(source.volume, (float)Field(pair.Value, "OriginalVolume"))
                        && source.mute == (bool)Field(pair.Value, "OriginalMute")
                        && source.ignoreListenerVolume == (bool)Field(pair.Value, "OriginalIgnoreListener");
                }
                Check(authoredRestored, "Borrowed audio sources restore their original authored volume and flags");
                c.ApplyAudioSettings(); AssertRegisteredMix(c);
                Check(Levels(c, .47f, .37f, .23f, .61f), "Reapplying the quieter scene mix does not reset the player's selected route volumes");

                if (c.DesktopDisplay)
                {
                    c.OpenStartMenu(); c.MenuControls = true; hud.SettingsTab = 2;
                    for (int frame = 0; frame < 4; frame++) yield return null;
                    Check(c.StartMenuOpen && c.MenuControls && c.Paused && hud.SettingsTab == 2,
                        "The real PC menu opens its dedicated BGM/environment/effects sound-mix page");
                    string path = Path.Combine(evidence, "pc-sound-mix.png"); bool requested = true;
                    try { ScreenCapture.CaptureScreenshot(path); }
                    catch (UnityException ex) { requested = false; Debug.Log("[AUDIO-MIX] SCOPE: PC game-view capture unavailable: " + ex.Message); }
                    if (requested)
                    {
                        deadline = EditorApplication.timeSinceStartup + 5;
                        while ((!File.Exists(path) || new FileInfo(path).Length < 128) && EditorApplication.timeSinceStartup < deadline) yield return null;
                    }
                    if (File.Exists(path) && new FileInfo(path).Length >= 128)
                    {
                        var image = new Texture2D(2, 2);
                        try
                        {
                            Check(image.LoadImage(File.ReadAllBytes(path)) && image.width >= 640 && image.height >= 360,
                                "The actual PC game render writes a readable sound-settings screenshot including its HUD");
                            Debug.Log("[AUDIO-MIX] PC SETTINGS EVIDENCE: " + path + " " + image.width + "x" + image.height);
                        }
                        finally { Object.DestroyImmediate(image); }
                    }
                    else
                    {
                        FishingV016Validation.Capture(c, evidence, "pc-menu-scene-only");
                        Debug.Log("[AUDIO-MIX] SCOPE: This batch session did not return a Game View/IMGUI screenshot; the scene-only fallback is not sound-settings UI evidence.");
                    }
                    c.FinishTitleVoyage(); yield return null;
                    Check(Levels(c, .47f, .37f, .23f, .61f), "Closing the PC sound page keeps its existing nondefault mix without a defaults reset");
                }
                else Debug.Log("[AUDIO-MIX] SCOPE: PC screenshot omitted because this run is not a desktop display.");

                Check(PreferencesUnchanged(prefs), "Audio sliders, XR page callbacks and menu reopen leave existing persistent preference keys and values untouched in isolation");
                Debug.Log("[AUDIO-MIX] EVIDENCE: " + evidence);
                Debug.Log("[AUDIO-MIX] SCOPE: Actual source gains and built XR-page delegates were checked; no headset pointer, physical speaker output or subjective headphone balance is claimed. A future normal-launch preference load is not simulated by writing user preferences.");
            }
            finally
            {
                c.ActionAudio?.ItemSource?.Stop(); ClosePanels(c); c.ReturnToHarbor(); c.RestoreShopTestProfile(original);
                foreach (var pair in climateState) pair.Key.SetValue(climate, pair.Value);
                c.Weather.ApplyEnvironment();
                if (wind != null) c.SetAudioBaseVolume(wind, windBase);
                c.SetSetting("Master", master); c.SetSetting("Music", music); c.SetSetting("Ambient", ambient); c.SetSetting("Effects", effects);
                if (c.QuietMode != quiet) c.ToggleQuiet();
                hud.SettingsTab = tab;
                if (paused && !c.Paused) c.TogglePause();
                Check(PreferencesUnchanged(prefs) && Same(player, Read(playerPath)) && Same(backup, Read(playerPath + ".bak")),
                    "The original persistent preferences, player save and backup remain unchanged after mix-test cleanup");
            }
        }

        private static void ValidateEnvironmentSources(FishingPrototype c, AudioSource wind)
        {
            int engines = 0, water = 0, coast = 0;
            var seen = new HashSet<AudioSource>();
            foreach (var engine in Object.FindObjectsOfType<Engine>(true))
            {
                if (engine.gameObject.scene != c.gameObject.scene) continue;
                if (engine.engineSound != null && seen.Add(engine.engineSound))
                { AssertEnvironment(c, engine.engineSound, .20f, .08f); engines++; }
                if (engine.waterSound != null && seen.Add(engine.waterSound))
                { AssertEnvironment(c, engine.waterSound, .18f, .055f); water++; }
            }
            foreach (var source in Object.FindObjectsOfType<AudioSource>(true))
            {
                if (source.gameObject.scene != c.gameObject.scene || !source.loop || source.clip == null) continue;
                string name = source.clip.name;
                if (name != "ShoreWaves" && name != "ShoreRocks" && name != "Jetty" && name != "FlowingWater") continue;
                AssertEnvironment(c, source, .20f, .055f); coast++;
            }
            Check(engines > 0 && water > 0, "Real boat engine and hull-water sources are classified as reduced Ambient loops");
            Check((FishingAudioCategory)Field(Entry(c, wind), "Category") == FishingAudioCategory.Ambient,
                "The real dynamic weather source shares the Ambient route with the engine and coast");
            Debug.Log("[AUDIO-MIX] SOURCE INVENTORY: engine=" + engines + ", water=" + water + ", authored coast loops=" + coast);
            if (coast == 0) Debug.Log("[AUDIO-MIX] SCOPE: No named authored coast loop is instantiated in this scene; engine/water/wind checks still execute.");
        }
        private static void AssertEnvironment(FishingPrototype c, AudioSource source, float scale, float maximum)
        {
            object entry = Entry(c, source);
            Check(entry != null && (FishingAudioCategory)Field(entry, "Category") == FishingAudioCategory.Ambient,
                "Environment source belongs to Ambient rather than the Effects slider: " + source.name + "/" + source.clip?.name);
            float expected = Mathf.Min(maximum, Mathf.Max(0, (float)Field(entry, "OriginalVolume")) * scale);
            Check(Near(Base(c, source), expected) && Base(c, source) <= maximum + .0001f,
                "Environment gain follows its quieter authored-volume scale and cap: " + source.name + " base=" + Base(c, source));
        }
        private static IEnumerable ValidateWindTransition(FishingPrototype c, AudioSource wind)
        {
            var climate = c.Weather.Climate;
            while (climate.Blend < 1) climate.Tick(.25f, false);
            Check(climate.Select(BayWeather.Clear), "Prepare the real weather component's clear-sky mix");
            while (climate.Blend < 1) climate.Tick(.25f, false);
            c.Weather.ApplyEnvironment(); yield return null;
            float baseline = (float)typeof(FishingPrototype).GetField("WindMixBase", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            float rainBoost = (float)typeof(FishingPrototype).GetField("WindRainMixBoost", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(Near(baseline, .025f) && Near(rainBoost, .05f) && Near(Base(c, wind), baseline),
                "Clear weather uses the reduced 0.025 base, with at most 0.05 additional rain gain");
            c.SetSetting("Music", .4f); c.SetSetting("Effects", .65f); c.SetSetting("Ambient", .35f);
            Check(climate.Select(BayWeather.Rain), "Start a real clear-to-rain climate blend for dynamic gain checks");
            float previous = baseline, maximum = baseline;
            for (int sample = 0; sample < 13; sample++)
            {
                for (int tick = 0; tick < 4; tick++) climate.Tick(.25f, false);
                if (sample == 6) c.SetSetting("Ambient", 0);
                if (sample == 9) c.SetSetting("Ambient", .2f);
                c.Weather.ApplyEnvironment(); yield return null;
                float current = Base(c, wind); maximum = Mathf.Max(maximum, current);
                Check(Finite(current) && current >= baseline - .0002f && current <= baseline + rainBoost + .0002f
                    && current >= previous - .0002f && Near(wind.volume, current * c.AmbientVolume)
                    && Near(c.MusicVolume, .4f) && Near(c.EffectsVolume, .65f),
                    "Dynamic rain gain stays bounded and follows only Ambient during its actual component update: " + sample);
                if (sample >= 6 && sample < 9) Check(wind.volume == 0 && CategorySilent(c, FishingAudioCategory.Ambient),
                    "Rain intensity changes cannot bypass the muted environment route: " + sample);
                previous = current;
            }
            Check(maximum > baseline + .04f && maximum <= .0752f, "Fully blended rain reaches its quieter gain cap without overshooting it");
        }
        private static void ValidateNotebookAudio(FishingPrototype c)
        {
            var notebook = c.XRNotebook; var page = notebook.GetType().GetField("_page", Fields);
            var refresh = notebook.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
            int oldPage = (int)page.GetValue(notebook);
            try
            {
                c.SetSetting("Master", .5f); c.SetSetting("Music", .6f); c.SetSetting("Ambient", .4f); c.SetSetting("Effects", .7f);
                page.SetValue(notebook, 18); refresh.Invoke(notebook, null);
                var labels = (Text[])Field(notebook, "_labels"); var actions = (Action[])Field(notebook, "_actions");
                Check(labels[2].text.Contains("BGM") && labels[4].text.Contains("环境") && labels[6].text.Contains("音效"),
                    "The actual built XR audio page exposes separate music, environment and effects controls");
                actions[2](); Check(Levels(c, .5f, .5f, .4f, .7f), "The real XR BGM-decrease callback changes only Music");
                actions[3](); actions[4](); Check(Levels(c, .5f, .6f, .3f, .7f), "The real XR environment-decrease callback changes only Ambient");
                actions[5](); actions[6](); Check(Levels(c, .5f, .6f, .4f, .6f), "The real XR effects-decrease callback changes only Effects");
                actions[7](); actions[0](); Check(Levels(c, .75f, .6f, .4f, .7f), "The XR master control changes the listener route while keeping all three mix levels");
                actions[1](); Check(c.QuietMode, "The XR page's actual mute callback enables the common Quiet setting");
                actions[1](); Check(!c.QuietMode && Levels(c, .75f, .6f, .4f, .7f), "The XR unmute callback preserves all independent volumes");
                AssertRegisteredMix(c);
            }
            finally { page.SetValue(notebook, oldPage); refresh.Invoke(notebook, null); }
        }
        private static void AssertRegisteredMix(FishingPrototype c)
        {
            bool valid = true; int count = 0;
            foreach (DictionaryEntry pair in Registry(c))
            {
                var source = pair.Key as AudioSource; if (source == null) continue;
                var category = (FishingAudioCategory)Field(pair.Value, "Category");
                float route = category == FishingAudioCategory.Music ? c.MusicVolume : category == FishingAudioCategory.Ambient ? c.AmbientVolume : c.EffectsVolume;
                float gain = (float)Field(pair.Value, "BaseVolume");
                valid &= Finite(source.volume) && Near(source.volume, gain * route) && source.volume >= 0 && source.volume <= 1;
                count++;
            }
            Check(valid && count > 4, "All " + count + " registered real sources obey their own category multiplier without cross-route leakage");
        }
        private static bool CategorySilent(FishingPrototype c, FishingAudioCategory category)
        {
            int count = 0;
            foreach (DictionaryEntry pair in Registry(c))
            {
                var source = pair.Key as AudioSource;
                if (source == null || (FishingAudioCategory)Field(pair.Value, "Category") != category) continue;
                count++; if (source.volume != 0) return false;
            }
            return count > 0;
        }
        private static bool RegionalAudible(FishingPrototype c)
        { foreach (var source in c.RegionalMusic.Voices) if (source != null && source.isPlaying && source.volume > .0001f) return true; return false; }
        private static bool Levels(FishingPrototype c, float master, float music, float ambient, float effects)
            => Near(c.MasterVolume, master) && Near(c.MusicVolume, music) && Near(c.AmbientVolume, ambient) && Near(c.EffectsVolume, effects);
        private sealed class Preference { public string Key; public bool Integer, Exists; public float Value; }
        private static Preference[] CapturePreferences()
        {
            var result = new List<Preference>();
            foreach (string key in new[] { "Master", "Music", "Ambient", "Effects" })
                result.Add(new Preference { Key = SettingsPrefix + key, Exists = PlayerPrefs.HasKey(SettingsPrefix + key), Value = PlayerPrefs.GetFloat(SettingsPrefix + key, -913) });
            result.Add(new Preference { Key = "Fishing.V3.Quiet", Integer = true, Exists = PlayerPrefs.HasKey("Fishing.V3.Quiet"), Value = PlayerPrefs.GetInt("Fishing.V3.Quiet", -913) });
            return result.ToArray();
        }
        private static bool PreferencesUnchanged(Preference[] prefs)
        {
            foreach (var pref in prefs)
            {
                float current = pref.Integer ? PlayerPrefs.GetInt(pref.Key, -913) : PlayerPrefs.GetFloat(pref.Key, -913);
                if (PlayerPrefs.HasKey(pref.Key) != pref.Exists || !current.Equals(pref.Value)) return false;
            }
            return true;
        }
        private static void ClosePanels(FishingPrototype c)
        {
            c.Prologue?.Suspend(); c.HandGames?.Cancel(); c.Letter?.Close(); c.Residents?.Close(); c.TackleShop?.Close(); c.XRNotebook?.Close();
            c.Home?.CancelSleep(); c.CloseHomeChart(); c.CloseSaveMenu();
            if (c.StartMenuOpen) c.FinishTitleVoyage();
            if (c.InventoryOpen) c.ToggleInventory();
            if (c.JournalOpen) c.ToggleJournal();
            if (c.Paused) c.TogglePause();
            c.CancelWorldAction();
        }
        private static byte[] Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        private static bool Same(byte[] a, byte[] b)
        { if (a == null || b == null) return a == null && b == null; if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
