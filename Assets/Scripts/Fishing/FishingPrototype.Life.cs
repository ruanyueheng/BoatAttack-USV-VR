using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingLifeWorld Life { get; private set; }
        /// <summary>Photos live beside the saves, one folder per run, never inside the save JSON.</summary>
        public string AlbumDirectory => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(SavePath)) ?? Application.persistentDataPath, "FishingAlbum", _runId);
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
