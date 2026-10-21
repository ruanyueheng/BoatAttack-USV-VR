using System;
using System.Collections;
using System.Globalization;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingSlotStoreValidation
    {
        public static void Run() => Checks();

        public static IEnumerator Checks(FishingPrototype controller)
        {
            Checks();
            yield break;
        }

        public static void Checks()
        {
            string directory = Path.Combine(Path.GetTempPath(), "FishingSlotStore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string empty = Path.Combine(directory, "empty.json");
                for (int slot = 0; slot < FishingSlotStore.Count; slot++)
                    foreach (FishingSlotKind kind in new[] { FishingSlotKind.Manual, FishingSlotKind.Auto })
                    {
                        var info = FishingSlotStore.Read(empty, slot, kind);
                        Check(!info.Exists && !info.Valid && !info.FromBackup && info.Data == null, "空槽不会创建或恢复进度");
                    }
                Check(!File.Exists(empty), "读取空槽不会写入基础旧档");
                var malformedCareer = new FishingCareer();
                typeof(FishingCareer).GetProperty("Coins").GetSetMethod(true).Invoke(malformedCareer, new object[] { -1 });
                Check(RejectArgument(() => FishingSlotStore.Write(empty, 0, FishingSlotKind.Manual, malformedCareer, Resume(), "invalid-career"))
                    && !FishingSlotStore.Read(empty, 0, FishingSlotKind.Manual).Exists,
                    "无效career以可被运行时捕获的参数异常拒绝且不写磁盘");
                Check(FishingSlotStore.PathFor(empty, 2, FishingSlotKind.Manual) == empty + ".slot-3.manual.json"
                    && FishingSlotStore.PathFor(empty, 0, FishingSlotKind.Auto) == empty + ".slot-1.auto.json", "路径保留基础路径且手动与自动分离");
                Check(RejectArgument(() => FishingSlotStore.PathFor(empty, -1, FishingSlotKind.Manual))
                    && RejectArgument(() => FishingSlotStore.PathFor(empty, 3, FishingSlotKind.Manual))
                    && RejectArgument(() => FishingSlotStore.PathFor(empty, 0, (FishingSlotKind)99)), "槽索引和存档类别严格校验");

                string slots = Path.Combine(directory, "slots.json");
                for (int slot = 0; slot < FishingSlotStore.Count; slot++)
                    foreach (FishingSlotKind kind in new[] { FishingSlotKind.Manual, FishingSlotKind.Auto })
                    {
                        int coins = 100 + slot * 10 + (int)kind;
                        var resume = Resume();
                        var data = FishingSlotStore.Write(slots, slot, kind, Career(coins), resume, "run-" + slot + "-" + kind);
                        Check(DateTime.TryParseExact(data.savedUtc, "O", CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var utc) && utc.Kind == DateTimeKind.Utc, "保存时间为可解析UTC而非Unix时间");
                        resume.boatX = 999;
                        Check(data.resume.boatX == 1.25f && data.career.version == 15, "快照不共享恢复对象且兼容career v15");
                    }
                for (int slot = 0; slot < FishingSlotStore.Count; slot++)
                    foreach (FishingSlotKind kind in new[] { FishingSlotKind.Manual, FishingSlotKind.Auto })
                    {
                        var info = FishingSlotStore.Read(slots, slot, kind);
                        Check(info.Exists && info.Valid && !info.FromBackup && info.Data.career.coins == 100 + slot * 10 + (int)kind,
                            "三个槽的手动和自动档彼此独立：" + slot + "/" + kind);
                        Check(info.Summary.Contains("金币") && info.Summary.Contains("鱼获") && info.Summary.Contains("第 1 天"), "摘要含本地时间、天数、金币和鱼获");
                    }

                string recovery = Path.Combine(directory, "recovery.json");
                string primary = FishingSlotStore.PathFor(recovery, 0, FishingSlotKind.Manual);
                FishingSlotStore.Write(recovery, 0, FishingSlotKind.Manual, Career(10), Resume(), "before");
                FishingSlotStore.Write(recovery, 0, FishingSlotKind.Manual, Career(20), Resume(), "after");
                Check(File.Exists(primary + ".bak") && FishingSlotStore.Read(recovery, 0, FishingSlotKind.Manual).Data.career.coins == 20,
                    "原子覆盖保留上一份有效备份");
                string backupText = File.ReadAllText(primary + ".bak");
                File.WriteAllText(primary, "{broken");
                var backup = FishingSlotStore.Read(recovery, 0, FishingSlotKind.Manual);
                Check(backup.Exists && backup.Valid && backup.FromBackup && backup.Data.career.coins == 10
                    && backup.Message.Length > 0 && backup.Summary.Contains("备份"), "损坏主档从有效备份读取并明确展示");
                FishingSlotStore.Write(recovery, 0, FishingSlotKind.Manual, Career(30), Resume(), "repaired");
                Check(File.ReadAllText(primary + ".bak") == backupText && FishingSlotStore.Read(recovery, 0, FishingSlotKind.Manual).Data.career.coins == 30,
                    "修复损坏主档不会覆盖有效备份");
                File.WriteAllText(primary, "{broken");
                Check(FishingSlotStore.Read(recovery, 0, FishingSlotKind.Manual).Data.career.coins == 10, "修复后再次损坏仍可恢复原备份");
                File.Delete(primary);
                Check(FishingSlotStore.Read(recovery, 0, FishingSlotKind.Manual).FromBackup, "只有备份也被识别为已有可用存档");
                File.WriteAllText(primary + ".bak", "{broken");
                var damaged = FishingSlotStore.Read(recovery, 0, FishingSlotKind.Manual);
                Check(damaged.Exists && !damaged.Valid && damaged.Data == null && damaged.Message.Length > 0,
                    "只剩损坏备份仍是已有无效槽而不是空槽");
                FishingSlotStore.Write(recovery, 0, FishingSlotKind.Manual, Career(40), Resume(), "backup-only-repair");
                Check(FishingSlotStore.Read(recovery, 0, FishingSlotKind.Manual).Valid, "缺失主档可新建而不依赖损坏备份");
                Check(Directory.GetFiles(directory, "*.tmp-*", SearchOption.AllDirectories).Length == 0, "原子写入后没有残留临时档");

                CheckNewRunTransactions(directory);

                string invalid = Path.Combine(directory, "invalid.json");
                string invalidPath = FishingSlotStore.PathFor(invalid, 0, FishingSlotKind.Auto);
                var seed = FishingSlotStore.Write(invalid, 0, FishingSlotKind.Auto, Career(5), Resume(), "validation");
                seed.format = 2;
                Invalid(invalid, invalidPath, seed, "未来封装版本拒绝读取");
                seed.format = 1;
                seed.career.version = new FishingSaveData().version+1;
                Invalid(invalid, invalidPath, seed, "未来career版本拒绝读取");
                seed.career.version = new FishingSaveData().version;
                seed.career.coins = -1;
                Invalid(invalid, invalidPath, seed, "当前career版本也必须通过TryRestore校验");
                seed.career = Career(5).Snapshot();
                seed.savedUtc = "1729161600";
                Invalid(invalid, invalidPath, seed, "Unix字符串不被误读为保存UTC时间");
                seed.savedUtc = "2026-10-17T08:00:00.0000000";
                Invalid(invalid, invalidPath, seed, "无UTC时区的时间拒绝读取");
                seed.savedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                seed.resume = null;
                File.WriteAllText(invalidPath,JsonUtility.ToJson(seed,true).Replace("\"resume\":", "\"missingResume\":"));
                Check(!FishingSlotStore.Read(invalid,0,FishingSlotKind.Auto).Valid,"缺失resume禁止盲目恢复");
                seed.resume = Resume();
                string incomplete=JsonUtility.ToJson(seed,true);
                File.WriteAllText(invalidPath,System.Text.RegularExpressions.Regex.Replace(incomplete,"\"resume\"\\s*:\\s*\\{[^{}]*\\}","\"resume\": {}"));
                Check(!FishingSlotStore.Read(invalid,0,FishingSlotKind.Auto).Valid,"空resume对象即使其他字段有效也不可作为出生点恢复");
                seed.resume.navigation = -1;
                Invalid(invalid, invalidPath, seed, "负导航索引拒绝恢复");
                seed.resume.navigation = 64;
                Invalid(invalid, invalidPath, seed, "越界导航索引拒绝恢复");
                seed.resume.navigation = 63;
                Check(FishingSlotStore.IsValidResume(seed.resume, out _), "磁盘层保留未来航点索引，由运行时再次约束");
                seed.resume = Resume();
                seed.resume.chartRange = 0;
                Invalid(invalid, invalidPath, seed, "有位置档必须保存有效海图范围");
                seed.resume = Resume();
                seed.resume.boatX = 10001;
                Invalid(invalid, invalidPath, seed, "过大世界坐标拒绝恢复");
                seed.resume = Resume();
                seed.resume.actorYaw = 361;
                Invalid(invalid, invalidPath, seed, "过大方向角拒绝恢复");
                seed.resume = Resume();
                seed.resume.actorX = float.NaN;
                Check(!FishingSlotStore.IsValidResume(seed.resume, out _)
                    && RejectArgument(() => FishingSlotStore.Write(invalid, 1, FishingSlotKind.Manual, Career(1), seed.resume, "nan")),
                    "NaN恢复位置在写入边界被拒绝");
                Invalid(invalid, invalidPath, seed, "JSON中的NaN恢复位置拒绝读取");
                seed.resume.actorX = float.PositiveInfinity;
                Invalid(invalid, invalidPath, seed, "JSON中的无限恢复位置拒绝读取");
                seed.resume = Resume();
                string full = JsonUtility.ToJson(seed, true);
                File.WriteAllText(invalidPath, full.Replace("\"actorX\": 2.5,", ""));
                Check(full.Contains("\"actorX\": 2.5,") && !FishingSlotStore.Read(invalid, 0, FishingSlotKind.Auto).Valid,
                    "漏写actorX不能误当零坐标");
                File.WriteAllText(invalidPath, "{}");
                Check(FishingSlotStore.Read(invalid, 0, FishingSlotKind.Auto).Exists
                    && !FishingSlotStore.Read(invalid, 0, FishingSlotKind.Auto).Valid, "空JSON对象不是空槽也不是可加载档");

                string legacyPath = Path.Combine(directory, "legacy.json");
                var legacy = Career(123);
                Check(!FishingSlotStore.ImportLegacy(legacyPath, legacy), "没有真实读取来源不能凭新career迁移");
                File.WriteAllText(legacyPath, "{broken");
                Check(!FishingSlotStore.ImportLegacy(legacyPath, legacy), "损坏旧档不能作为迁移来源");
                File.WriteAllText(legacyPath, JsonUtility.ToJson(legacy.Snapshot(), true));
                string oldText = File.ReadAllText(legacyPath);
                Check(FishingSlotStore.ImportLegacy(legacyPath, legacy), "三个槽全空时可迁移真实v13旧档");
                var imported = FishingSlotStore.Read(legacyPath, 0, FishingSlotKind.Manual);
                Check(imported.Valid && imported.Data.career.coins == 123 && !imported.Data.resume.positioned
                    && File.ReadAllText(legacyPath) == oldText, "迁移保留career、默认出生点并且不改旧档");
                string importedText = File.ReadAllText(FishingSlotStore.PathFor(legacyPath, 0, FishingSlotKind.Manual));
                Check(!FishingSlotStore.ImportLegacy(legacyPath, Career(999))
                    && File.ReadAllText(FishingSlotStore.PathFor(legacyPath, 0, FishingSlotKind.Manual)) == importedText, "重复迁移绝不覆盖已有手动槽");
                string occupied = Path.Combine(directory, "occupied.json");
                File.WriteAllText(occupied, oldText);
                File.WriteAllText(FishingSlotStore.PathFor(occupied, 2, FishingSlotKind.Auto) + ".bak", "{broken");
                Check(!FishingSlotStore.ImportLegacy(occupied, legacy)
                    && !FishingSlotStore.Read(occupied, 0, FishingSlotKind.Manual).Exists, "任一槽的损坏备份也阻止旧档自动迁移");
                string sourceBackup = Path.Combine(directory, "source-backup.json");
                File.WriteAllText(sourceBackup + ".bak", oldText);
                Check(FishingSlotStore.ImportLegacy(sourceBackup, legacy) && !File.Exists(sourceBackup)
                    && File.ReadAllText(sourceBackup + ".bak") == oldText, "旧档只有有效备份也可迁移且不改源档");
                string compatible = Path.Combine(directory, "compatible.json");
                FishingSaveStore.Save(compatible, legacy);
                var originalCareer = FishingSaveStore.Load(compatible, out _);
                Check(FishingSlotStore.ImportLegacy(compatible, originalCareer)
                    && FishingSaveStore.Load(compatible, out _).Coins == 123, "兼容原FishingSaveStore并保留可读取旧进度");
                string earlierSource = Path.Combine(directory, "v12-source.json");
                var earlierData = legacy.Snapshot();
                earlierData.version = 12;
                File.WriteAllText(earlierSource, JsonUtility.ToJson(earlierData, true));
                string earlierText = File.ReadAllText(earlierSource);
                string newerBase = Path.Combine(directory, "v13-target.json");
                Check(FishingSlotStore.ImportLegacy(newerBase, new FishingCareer(), earlierSource), "三参数接口支持真实早期fallback来源");
                var earlierImport = FishingSlotStore.Read(newerBase, 0, FishingSlotKind.Manual);
                Check(earlierImport.Valid && earlierImport.Data.career.coins == 123 && earlierImport.Data.runId == "legacy"
                    && !File.Exists(newerBase) && File.ReadAllText(earlierSource) == earlierText,
                    "迁移重新读取真实来源而不是误用重置入参，源档完全保留");
                Check(!FishingSlotStore.ImportLegacy(newerBase, legacy, earlierSource), "三参数迁移也不可覆盖已有槽");
                string missingTarget = Path.Combine(directory, "missing-target.json");
                Check(!FishingSlotStore.ImportLegacy(missingTarget, legacy, Path.Combine(directory, "missing-source.json"))
                    && !FishingSlotStore.Read(missingTarget, 0, FishingSlotKind.Manual).Exists, "不存在的fallback来源不能创建槽");
                string backupTarget = Path.Combine(directory, "backup-target.json");
                File.WriteAllText(earlierSource, "{broken");
                File.WriteAllText(earlierSource + ".bak", earlierText);
                Check(FishingSlotStore.ImportLegacy(backupTarget, new FishingCareer(), earlierSource)
                    && FishingSlotStore.Read(backupTarget, 0, FishingSlotKind.Manual).Data.career.coins == 123,
                    "三参数来源主档损坏时只迁移有效备份career");
                Debug.Log("[SLOT-STORE] 磁盘槽位、备份校验与旧档迁移检查完成；所有文件均隔离于GUID临时目录。");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void CheckNewRunTransactions(string directory)
        {
            string basePath = Path.Combine(directory, "new-run.json");
            var liveCareer = Career(456);
            var fresh = new FishingCareer();
            string manual = FishingSlotStore.PathFor(basePath, 0, FishingSlotKind.Manual);
            string auto = FishingSlotStore.PathFor(basePath, 0, FishingSlotKind.Auto);
            FishingSlotStore.Write(basePath, 0, FishingSlotKind.Manual, Career(10), Resume(), "old-manual-backup");
            FishingSlotStore.Write(basePath, 0, FishingSlotKind.Manual, Career(20), Resume(), "old-manual");
            FishingSlotStore.Write(basePath, 0, FishingSlotKind.Auto, Career(30), Resume(), "old-auto-backup");
            FishingSlotStore.Write(basePath, 0, FishingSlotKind.Auto, Career(40), Resume(), "old-auto");
            string manualText = File.ReadAllText(manual), autoText = File.ReadAllText(auto);
            string manualBackup = File.ReadAllText(manual + ".bak"), autoBackup = File.ReadAllText(auto + ".bak");
            File.Delete(auto);
            Directory.CreateDirectory(auto);
            try
            {
                Check(RejectIO(() => FishingSlotStore.WriteNewRun(basePath, 0, fresh, Resume(), "blocked-run"))
                    && File.ReadAllText(manual) == manualText && File.ReadAllText(manual + ".bak") == manualBackup
                    && File.ReadAllText(auto + ".bak") == autoBackup && Directory.Exists(auto) && liveCareer.Coins == 456,
                    "auto路径被目录占用时新航程失败且manual、两份bak与liveCareer全不变");
            }
            finally { Directory.Delete(auto); }
            File.WriteAllText(auto, autoText);
            if (Application.platform == RuntimePlatform.WindowsEditor)
                using (new FileStream(auto, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Check(RejectIO(() => FishingSlotStore.WriteNewRun(basePath, 0, fresh, Resume(), "locked-run"))
                        && File.ReadAllText(manual) == manualText && File.ReadAllText(manual + ".bak") == manualBackup
                        && File.ReadAllText(auto) == autoText && File.ReadAllText(auto + ".bak") == autoBackup,
                        "第二主档被锁导致提交失败时已写manual逆序回滚且原四文件完全一致");
                }
            var written = FishingSlotStore.WriteNewRun(basePath, 0, fresh, new FishingResumeData(), "new-run");
            var manualInfo = FishingSlotStore.Read(basePath, 0, FishingSlotKind.Manual);
            var autoInfo = FishingSlotStore.Read(basePath, 0, FishingSlotKind.Auto);
            Check(manualInfo.Valid && autoInfo.Valid && manualInfo.Data.runId == "new-run" && autoInfo.Data.runId == "new-run"
                && manualInfo.Data.career.coins == 0 && autoInfo.Data.career.coins == 0
                && manualInfo.Data.savedUtc == written.savedUtc && autoInfo.Data.savedUtc == written.savedUtc
                && File.ReadAllText(manual) == File.ReadAllText(auto) && liveCareer.Coins == 456,
                "新航程两主档使用同一快照、runId与UTC且未修改旧liveCareer");
            Check(File.ReadAllText(manual + ".bak") == manualText && File.ReadAllText(auto + ".bak") == autoText,
                "新航程事务备份各自有效旧主档");
            File.WriteAllText(manual, "{broken");
            string preservedBackup = File.ReadAllText(manual + ".bak");
            FishingSlotStore.WriteNewRun(basePath, 0, fresh, new FishingResumeData(), "new-run-repair");
            Check(File.ReadAllText(manual + ".bak") == preservedBackup, "新航程事务也不让损坏主档覆盖有效备份");
            string empty = Path.Combine(directory, "empty-new-run.json");
            FishingSlotStore.WriteNewRun(empty, 2, fresh, new FishingResumeData(), "empty-run");
            Check(FishingSlotStore.Read(empty, 2, FishingSlotKind.Manual).Valid && FishingSlotStore.Read(empty, 2, FishingSlotKind.Auto).Valid
                && !File.Exists(FishingSlotStore.PathFor(empty, 2, FishingSlotKind.Manual) + ".bak"),
                "全空槽的新航程创建两主档而不捏造备份");
            Check(Directory.GetDirectories(directory, "*.transaction-*", SearchOption.AllDirectories).Length == 0,
                "成功或正常回滚后事务暂存目录已清理");
        }

        private static bool RejectIO(Action action)
        {
            try { action(); return false; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        private static FishingCareer Career(int coins)
        {
            var data = new FishingCareer().Snapshot();
            data.coins = coins;
            if (!FishingCareer.TryRestore(data, out var career)) throw new InvalidOperationException("隔离测试career构造失败。");
            return career;
        }

        private static FishingResumeData Resume() => new FishingResumeData
        {
            positioned = true, boatX = 1.25f, boatY = 1, boatZ = 3, boatYaw = 180,
            actorX = 2.5f, actorY = 2, actorZ = 4, actorYaw = 90,
            navigation = 16, chartRange = 160, chartExpanded = true
        };

        private static bool RejectArgument(Action action)
        {
            try { action(); return false; }
            catch (ArgumentException) { return true; }
        }

        private static void Invalid(string basePath, string path, FishingSlotData data, string label)
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            var info = FishingSlotStore.Read(basePath, 0, FishingSlotKind.Auto);
            Check(info.Exists && !info.Valid && info.Data == null && !string.IsNullOrEmpty(info.Message), label);
        }

        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("[SLOT-STORE] " + label);
            Debug.Log("[SLOT-STORE] CHECK: " + label);
        }
    }
}
