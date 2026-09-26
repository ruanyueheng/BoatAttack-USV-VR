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
