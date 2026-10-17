using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public enum FishingSlotKind { Manual, Auto }

    [Serializable]
    public sealed class FishingResumeData
    {
        // JsonUtility expands a null inline object into zero fields; an explicit marker rejects that blank payload.
        public string checkpointFormat = "checkpoint-v1";
        public bool positioned, ashore, firstPerson, anchored;
        public float boatX, boatY, boatZ, boatYaw, actorX, actorY, actorZ, actorYaw;
        public int navigation, chartRange;
        public bool chartExpanded;
    }

    [Serializable]
    public sealed class FishingSlotData
    {
        public int format = 1;
        public string savedUtc, runId;
        public FishingSaveData career;
        public FishingResumeData resume;
    }

    public sealed class FishingSlotInfo
    {
        public bool Exists, Valid, FromBackup;
        public string Message = string.Empty;
        public FishingSlotData Data;
        public string Summary = "空存档";
    }

    public static class FishingSlotStore
    {
        public const int Count = 3;

        public static string PathFor(string basePath, int slot, FishingSlotKind kind)
        {
            if (string.IsNullOrWhiteSpace(basePath)) throw new ArgumentException("存档基础路径不能为空。", nameof(basePath));
            if (slot < 0 || slot >= Count) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!Enum.IsDefined(typeof(FishingSlotKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            return basePath + ".slot-" + (slot + 1) + (kind == FishingSlotKind.Manual ? ".manual.json" : ".auto.json");
        }

        public static FishingSlotInfo Read(string basePath, int slot, FishingSlotKind kind)
        {
            string path = PathFor(basePath, slot, kind);
            var result = new FishingSlotInfo { Exists = File.Exists(path) || File.Exists(path + ".bak") };
            if (!result.Exists) return result;
            if (TryRead(path, out var data, out string primaryMessage))
            {
                Fill(result, data);
                return result;
            }
            if (TryRead(path + ".bak", out data, out string backupMessage))
            {
                Fill(result, data);
                result.FromBackup = true;
                result.Message = "主存档缺失或损坏，已读取有效备份。";
                result.Summary += " · 备份";
                return result;
            }
            result.Message = "存档已有但不可读取，不能加载。主档：" + primaryMessage + "；备份：" + backupMessage;
            result.Summary = "已有损坏存档 · 无有效备份";
            return result;
        }

        public static FishingSlotData Write(string basePath, int slot, FishingSlotKind kind,
            FishingCareer career, FishingResumeData resume, string runId)
        {
            string path = PathFor(basePath, slot, kind);
            var data = CreateData(career, resume, runId);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                {
                    // 损坏的主档不可成为新备份；原有有效备份必须保持不变。
                    if (TryRead(path, out _, out _)) File.Replace(temp, path, path + ".bak");
                    else File.Replace(temp, path, null);
                }
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            return data;
        }

        public static FishingSlotData WriteNewRun(string basePath, int slot, FishingCareer fresh,
            FishingResumeData resume, string runId)
        {
            string manual = PathFor(basePath, slot, FishingSlotKind.Manual);
            string auto = PathFor(basePath, slot, FishingSlotKind.Auto);
            var data = CreateData(fresh, resume, runId);
            string[] paths = { manual, auto, manual + ".bak", auto + ".bak" };
            foreach (string path in paths)
                if (Directory.Exists(path)) throw new IOException("存档路径被目录占用，原航程未改变：" + path);
            string directory = Path.GetDirectoryName(manual);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string transaction = manual + ".transaction-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(transaction);
            bool[] existed = new bool[paths.Length];
            bool[] committed = new bool[paths.Length];
            bool keepRecovery = false;
            try
            {
                // 在任何目标写入前完成全部快照和暂存，新航程的两份记录完全一致。
                string json = JsonUtility.ToJson(data, true);
                for (int i = 0; i < paths.Length; i++)
                {
                    existed[i] = File.Exists(paths[i]);
                    if (existed[i]) File.Copy(paths[i], Path.Combine(transaction, "original-" + i));
                }
                File.WriteAllText(Path.Combine(transaction, "next-0"), json);
                File.WriteAllText(Path.Combine(transaction, "next-1"), json);
                for (int i = 0; i < 2; i++)
                    if (existed[i] && TryRead(paths[i], out _, out _))
                        File.Copy(Path.Combine(transaction, "original-" + i), Path.Combine(transaction, "next-" + (i + 2)));
                try
                {
                    for (int i = 0; i < paths.Length; i++)
                    {
                        string next = Path.Combine(transaction, "next-" + i);
                        if (!File.Exists(next)) continue;
                        if (existed[i]) File.Replace(next, paths[i], null);
                        else File.Move(next, paths[i]);
                        committed[i] = true;
                    }
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is ArgumentException)
                {
                    Exception rollbackFailure = null;
                    for (int i = paths.Length - 1; i >= 0; i--)
                    {
                        if (!committed[i]) continue;
                        try
                        {
                            if (existed[i])
                            {
                                string original = Path.Combine(transaction, "original-" + i);
                                string restore = Path.Combine(transaction, "restore-" + i);
                                File.Copy(original, restore);
                                if (File.Exists(paths[i])) File.Replace(restore, paths[i], null);
                                else File.Move(restore, paths[i]);
                            }
                            else File.Delete(paths[i]);
                        }
                        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
                        {
                            rollbackFailure = error;
                        }
                    }
                    if (rollbackFailure != null)
                    {
                        keepRecovery = true;
                        throw new IOException("新航程保存失败且磁盘拒绝回滚；原文件快照保留于：" + transaction, rollbackFailure);
                    }
                    throw;
                }
                return data;
            }
            finally
            {
                // 提交成功后的暂存清理失败不能误报交易失败，或让调用方保留旧内存进度。
                if (!keepRecovery)
                    try { Directory.Delete(transaction, true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
            }
        }

        private static FishingSlotData CreateData(FishingCareer career, FishingResumeData resume, string runId)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (!IsValidResume(resume, out string message)) throw new ArgumentException(message, nameof(resume));
            if (string.IsNullOrWhiteSpace(runId) || runId.Length > 128)
                throw new ArgumentException("存档运行标识不能为空或超过128字符。", nameof(runId));
            var data = new FishingSlotData
            {
                savedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                runId = runId,
                career = career.Snapshot(),
                resume = JsonUtility.FromJson<FishingResumeData>(JsonUtility.ToJson(resume))
            };
            if (!FishingCareer.TryRestore(data.career, out _))
                throw new ArgumentException("当前进度未通过存档校验，未写入磁盘。", nameof(career));
            return data;
        }

        public static bool ImportLegacy(string basePath, FishingCareer legacy) => ImportLegacy(basePath, legacy, basePath);

        public static bool ImportLegacy(string basePath, FishingCareer legacy, string legacySourcePath)
        {
            if (legacy == null || string.IsNullOrWhiteSpace(legacySourcePath)) return false;
            for (int slot = 0; slot < Count; slot++)
                foreach (FishingSlotKind kind in new[] { FishingSlotKind.Manual, FishingSlotKind.Auto })
                {
                    string path = PathFor(basePath, slot, kind);
                    if (File.Exists(path) || File.Exists(path + ".bak")) return false;
                }
            if (!TryReadLegacy(legacySourcePath, out var sourceCareer)
                && !TryReadLegacy(legacySourcePath + ".bak", out sourceCareer)) return false;
            // 迁移重新读取的真实源进度，避免调用方已重置的对象覆盖旧进度。
            Write(basePath, 0, FishingSlotKind.Manual, sourceCareer, new FishingResumeData(), "legacy");
            return true;
        }

        public static bool IsValidResume(FishingResumeData resume, out string message)
        {
            message = "恢复位置资料缺失或不合法，不能加载。";
            if (resume == null || resume.checkpointFormat != "checkpoint-v1") return false;
            if (!Coordinate(resume.boatX) || !Coordinate(resume.boatY) || !Coordinate(resume.boatZ)
                || !Coordinate(resume.actorX) || !Coordinate(resume.actorY) || !Coordinate(resume.actorZ)
                || !Yaw(resume.boatYaw) || !Yaw(resume.actorYaw)) return false;
            if (resume.navigation < 0 || resume.navigation > 63) return false;
            bool chartRangeValid = resume.chartRange == 20 || resume.chartRange == 40 || resume.chartRange == 80
                || resume.chartRange == 160 || resume.chartRange == 320;
            if (!chartRangeValid && (resume.positioned || resume.chartRange != 0)) return false;
            if (!resume.positioned && (resume.ashore || resume.anchored)) return false;
            message = string.Empty;
            return true;
        }

        private static bool Coordinate(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= 10000f;
        private static bool Yaw(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= 360f;

        private static void Fill(FishingSlotInfo result, FishingSlotData data)
        {
            result.Valid = true;
            result.Data = data;
            FishingCareer.TryRestore(data.career, out var career);
            DateTime utc = DateTime.ParseExact(data.savedUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            result.Summary = utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
                + " · 第 " + (career.Calendar.Day + 1) + " 天 · " + career.Coins + " 金币 · 鱼获 " + career.Catches
                + "（鱼箱 " + career.Count + "）";
        }

        private static bool TryRead(string path, out FishingSlotData data, out string message)
        {
            data = null;
            message = "文件不存在";
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                message = "存档结构缺失";
                if (!json.Contains("\"format\"") || !json.Contains("\"savedUtc\"") || !json.Contains("\"runId\"")
                    || !json.Contains("\"career\"") || !json.Contains("\"resume\"") || !HasCareerFields(json)) return false;
                var candidate = JsonUtility.FromJson<FishingSlotData>(json);
                message = "存档封装版本不受支持";
                if (candidate == null || candidate.format != 1) return false;
                message = "保存时间不是有效UTC时间";
                if (!DateTime.TryParseExact(candidate.savedUtc, "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var saved) || saved.Kind != DateTimeKind.Utc) return false;
                message = "存档运行标识无效";
                if (string.IsNullOrWhiteSpace(candidate.runId) || candidate.runId.Length > 128) return false;
                message = "恢复位置包含非有限值，不能加载";
                if (Regex.IsMatch(json, @":\s*[-+]?(?:NaN|Infinity)\s*[,}]", RegexOptions.IgnoreCase)) return false;
                message = "进度资料损坏或来自不受支持的版本";
                if (!FishingCareer.TryRestore(candidate.career, out _)) return false;
                if (!IsValidResume(candidate.resume, out message)) return false;
                // 必须明确保存恢复字段，不能把空对象或漏写的字段当作默认出生点。
                var resumeObject = Regex.Match(json, "\"resume\"\\s*:\\s*\\{(?<fields>[^{}]*)\\}");
                foreach (string key in new[] { "checkpointFormat", "positioned", "ashore", "firstPerson", "anchored", "boatX", "boatY", "boatZ", "boatYaw",
                    "actorX", "actorY", "actorZ", "actorYaw", "navigation", "chartRange", "chartExpanded" })
                    if (!resumeObject.Success || !Regex.IsMatch(resumeObject.Groups["fields"].Value, "\"" + key + "\"\\s*:"))
                    {
                        message = "恢复位置资料缺失：" + key;
                        return false;
                    }
                data = candidate;
                message = string.Empty;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is ArgumentException || error is InvalidOperationException)
            {
                message = "文件或JSON不可读取";
                return false;
            }
        }

        private static bool HasCareerFields(string json) => json.Contains("\"version\"")
            && json.Contains("\"inventory\"") && json.Contains("\"coins\"");

        private static bool TryReadLegacy(string path, out FishingCareer career)
        {
            career = null;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                return HasCareerFields(json) && FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(json), out career);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is ArgumentException || error is InvalidOperationException) { return false; }
        }
    }
}
