using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingAcceptance
    {
        [Serializable]
        private sealed class CaptureEntry
        {
            public string file, scenario, phase, profile, utc;
            public int width, height;
            public bool phaseFrozen, applicationFocused, fullHudProbeVerified;
        }

        [Serializable]
        private sealed class CaptureManifest
        {
            public string version = FishingPrototype.Version;
            public string session;
            public string source = "ScreenCapture.CaptureScreenshotAsTexture after WaitForEndOfFrame; includes IMGUI";
            public bool physicalInputTested = false;
            public bool visuallyReviewed = false;
            public List<CaptureEntry> captures = new List<CaptureEntry>();
        }

        public bool IsCapturing { get; private set; }
        public string CaptureDirectory { get; private set; }
        private CaptureManifest _manifest;

        private IEnumerator CaptureFrame()
        {
            if (IsCapturing || Failure != null) yield break;
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Fail(new InvalidOperationException("Full-HUD capture requires a visible graphics player, not batchmode/nographics."));
                yield break;
            }
            IsCapturing = true;
            // The HUD draws an opaque probe while this flag is set. A hidden/black backbuffer cannot pass.
            yield return new WaitForEndOfFrame();
            Texture2D image = null;
            try
            {
                if (_manifest == null)
                {
                    string root = Options.OutputDirectory ?? Path.GetDirectoryName(Options.ProfilePath(Application.temporaryCachePath, false));
                    CaptureDirectory = Path.Combine(root, "capture-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(CaptureDirectory);
                    _manifest = new CaptureManifest { session = Options.SessionId };
                }
                image = ScreenCapture.CaptureScreenshotAsTexture();
                if (image == null || image.width != Screen.width || image.height != Screen.height)
                    throw new InvalidOperationException("Screen capture returned the wrong backbuffer dimensions.");
                string name = (_manifest.captures.Count + 1).ToString("D2") + "-" + CurrentCase + "-" + image.width + "x" + image.height + ".png";
                bool fullHud = HasHudProbe(image);
                File.WriteAllBytes(Path.Combine(CaptureDirectory, fullHud ? name : "INVALID-" + name), image.EncodeToPNG());
                if (!fullHud) throw new InvalidOperationException("HUD proof marker is absent. Keep the player visible and unminimized; the invalid image was retained for diagnosis.");
                _manifest.captures.Add(new CaptureEntry
                {
                    file = name, scenario = CurrentCase.ToString(), phase = Controller.Session.Phase.ToString(),
                    profile = Path.GetFileName(Controller.SavePath), utc = DateTime.UtcNow.ToString("o"),
                    width = image.width, height = image.height, phaseFrozen = FreezeScenario,
                    applicationFocused = Application.isFocused, fullHudProbeVerified = true
                });
                File.WriteAllText(Path.Combine(CaptureDirectory, "manifest.json"), JsonUtility.ToJson(_manifest, true));
                Status = "已保存完整 HUD：" + name;
                Debug.Log("[FISHING-ACCEPTANCE] Screenshot: " + Path.Combine(CaptureDirectory, name) + " fullHudProbe=true");
            }
            catch (Exception ex) { Fail(ex); }
            finally
            {
                if (image != null) Destroy(image);
                IsCapturing = false;
            }
        }

        private static bool HasHudProbe(Texture2D image)
        {
            var top = FishingHUD.AcceptanceProbeScreen(image.width, image.height);
            int x = Mathf.RoundToInt(top.x), y = image.height - 1 - Mathf.RoundToInt(top.y);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    var color = image.GetPixel(Mathf.Clamp(x + dx, 0, image.width - 1), Mathf.Clamp(y + dy, 0, image.height - 1));
                    if (color.r > .85f && color.g < .15f && color.b > .85f) return true;
                }
            return false;
        }
    }
}
