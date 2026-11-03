using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingLifeWorld Life { get; private set; }
        public string LegacyAlbumDirectory => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(SavePath)) ?? Application.persistentDataPath, "FishingAlbum", _runId);
        /// <summary>Android photos use app-owned internal storage; isolated profiles stay beside their isolated saves.</summary>
        public string AlbumDirectory
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if(!HasIsolatedProfile)
                {
                    if(_internalPhotoRoot==null)
                    {
                        using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                        using(var files=activity.Call<AndroidJavaObject>("getFilesDir"))
                            _internalPhotoRoot=files.Call<string>("getAbsolutePath");
                    }
                    return Path.Combine(_internalPhotoRoot,"FishingAlbum",_runId);
                }
#endif
                return LegacyAlbumDirectory;
            }
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private string _internalPhotoRoot;
#endif
        public Transform PendingFishTransform => HasPendingCatch && _fish != null ? _fish.transform : null;
        private void BuildLife()
        {
            if (Life != null) return;
            Life = gameObject.AddComponent<FishingLifeWorld>();
            Life.Build(this);
            Residents.Plaza = Life.Plaza; // npc 节日傍晚聚到广场
        }
    }
}
