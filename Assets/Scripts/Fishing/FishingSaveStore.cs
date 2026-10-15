using System;
using System.IO;
using UnityEngine;
namespace BoatAttack.Fishing
{
    public static class FishingSaveStore
    {
        public static FishingCareer Load(string path, out string message)
        {
            message = string.Empty;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new FishingCareer();
            if (TryRead(path, out var career)) return career;
            if (TryRead(path + ".bak", out career)) { message = "已从备份恢复进度。"; return career; }
            message = "存档不可读取；本次使用新进度。";
            return new FishingCareer();
        }
        // Read legacy progress only when no new-format primary OR backup exists. Never write to
        // the old path: a v0.5 executable does not know about schema v5 and may otherwise reset it.
        public static FishingCareer LoadWithLegacyFallback(string path, string legacyPath, out string message)
        {
            if (File.Exists(path) || File.Exists(path + ".bak")
                || (!File.Exists(legacyPath) && !File.Exists(legacyPath + ".bak")))
                return Load(path, out message);
            var career = Load(legacyPath, out message);
            if (message.Length == 0) message = "已读取旧版进度；后续保存使用新版独立存档，旧档保留。";
            return career;
        }
        public static void Save(string path, FishingCareer career)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(career.Snapshot(), true));
            if (File.Exists(path))
            {
                // Do not replace a valid backup with an already-corrupt primary file.
                if (TryRead(path, out _)) File.Replace(temp, path, path + ".bak");
                else { File.Copy(temp, path, true); File.Delete(temp); }
            }
            else File.Move(temp, path);
        }
        private static bool TryRead(string path, out FishingCareer career)
        {
            career = null;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                if (!json.Contains("\"version\"") || !json.Contains("\"inventory\"") || !json.Contains("\"coins\"")) return false;
                return FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(json), out career);
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return false; }
        }
    }
}
