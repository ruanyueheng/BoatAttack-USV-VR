using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.LowLevel;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>
    /// PR20 的对抗性边界回归。只在隔离批处理存档/临时 Play Mode 实例上运行，
    /// 不保存场景，不更改游戏代码。临时 PlayerLoop 回调观察真实 Update/LateUpdate，退出时恢复。
    /// </summary>
    [InitializeOnLoad]
    public static class FishingPlayerBoundaryValidation
    {
        private const string Key = "Fishing.PlayerBoundaryValidation.";
        private const string Marker = "[PLAYER-BOUNDARY]";
        private static IEnumerator _checks;
        private static Action _afterLateUpdate;
        private static PlayerLoopSystem _originalLoop;
        private static bool _installed;
        private static int _lastFrame = -1;
        private struct BoundaryAfterUpdate { }
        private struct BoundaryAfterLateUpdate { }

        static FishingPlayerBoundaryValidation()
        {
            EditorApplication.update += Bootstrap;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Key + "active", false)
                    && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    SessionState.SetString(Key + "error", message + "\n" + stack);
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key + "active", false)) return;
                SessionState.SetBool(Key + "active", false);
                string error = SessionState.GetString(Key + "error", "");
                if (!SessionState.GetBool(Key + "finished", false) && error.Length == 0) error = "套件未完整执行";
                if (error.Length == 0) Debug.Log(Marker + " PASS: 9 个边界案例；不等同于人工画面或头显验收。");
                else Debug.LogError(Marker + " FAIL: " + error);
                EditorApplication.Exit(error.Length == 0 ? 0 : 1);
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("只允许显式 -batchmode 隔离验收。");
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("需要实际人物骨架与呈现；不要使用 -nographics。");
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "finished", false);
            SessionState.SetString(Key + "error", "");
            SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 600);
            try
            {
                FishingScriptBinding.ValidateScene();
                EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex)
            {
                SessionState.SetBool(Key + "active", false);
                Debug.LogError(Marker + " FAIL: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void Bootstrap()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0))
                SessionState.SetString(Key + "error", "边界回归超时");
            if (SessionState.GetString(Key + "error", "").Length != 0)
            {
                if (_installed) Stop();
                else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.Exit(1);
                return;
            }
            if (!EditorApplication.isPlaying || _installed) return;
            var c = Object.FindObjectOfType<FishingPrototype>();
            if (c == null || c.WorkshopWorld == null || c.CatchRig == null) return;
            try
            {
                _originalLoop = PlayerLoop.GetCurrentPlayerLoop();
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                bool update = InsertAfter(ref loop, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate),
                    new PlayerLoopSystem { type = typeof(BoundaryAfterUpdate), updateDelegate = Advance });
                bool late = InsertAfter(ref loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate),
                    new PlayerLoopSystem { type = typeof(BoundaryAfterLateUpdate), updateDelegate = AfterLateUpdate });
                if (!update || !late) throw new InvalidOperationException("找不到生产 Update/LateUpdate PlayerLoop 阶段");
                _checks = Checks(c);
                _lastFrame = -1;
                _installed = true;
                PlayerLoop.SetPlayerLoop(loop);
            }
            catch (Exception ex)
            {
                SessionState.SetString(Key + "error", ex.ToString());
                Stop();
            }
        }

        private static bool InsertAfter(ref PlayerLoopSystem system, Type target, PlayerLoopSystem insertion)
        {
            var children = system.subSystemList;
            if (children == null) return false;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type == target)
                {
                    var list = children.ToList();
                    list.Insert(i + 1, insertion);
                    system.subSystemList = list.ToArray();
                    return true;
                }
                if (InsertAfter(ref children[i], target, insertion)) return true;
            }
            return false;
        }

        private static void Advance()
        {
            if (_checks == null || _lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            try
            {
                if (_checks.MoveNext()) return;
                SessionState.SetBool(Key + "finished", true);
                Stop();
            }
            catch (Exception ex)
            {
                SessionState.SetString(Key + "error", ex.ToString());
                Stop();
            }
        }

        private static void AfterLateUpdate()
        {
            var callback = _afterLateUpdate;
            _afterLateUpdate = null;
            if (callback == null) return;
            try { callback(); }
            catch (Exception ex)
            {
                SessionState.SetString(Key + "error", ex.ToString());
                Stop();
            }
        }

        private static void Stop()
        {
            var checks = _checks;
            _checks = null;
            _afterLateUpdate = null;
            try { (checks as IDisposable)?.Dispose(); }
            finally
            {
                if (_installed) { PlayerLoop.SetPlayerLoop(_originalLoop); _installed = false; }
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            Debug.Log(Marker + " CHECK: " + message);
        }

        private static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),
                "仅使用 temporaryCachePath 下隔离存档");
            Check(!UnityEngine.XR.XRSettings.isDeviceActive, "桌面边界 fixture 不占用活动 XR");
            bool controllerEnabled = c.enabled, fieldEnabled = c.FieldWorld.enabled;
            float capture = Time.captureDeltaTime, maximum = Time.maximumDeltaTime;
            var profile = c.Career.Snapshot();
            try
            {
                Time.captureDeltaTime = 1f / 60;
                c.ReturnToHarbor();
                var rb = c.Player.engine.RB;
                rb.position = c.Shore.Sites[0].Berth + Vector3.up;
                rb.velocity = rb.angularVelocity = Vector3.zero;
                c.Player.transform.position = rb.position;
                Physics.SyncTransforms();
                Check(c.Shore.TryDisembark(), "通过生产 TryDisembark 建立岸上状态");
                yield return null;
                // 移动与取消许可案例显式驱动；三个拾取时序案例只由生产 Update/LateUpdate 推进。
                // 冻结拾取的装饰性上下浮动，但保留原网格、感知、可达性与提交实现。
                c.enabled = false;
                c.FieldWorld.enabled = false;
                var failures = new List<string>();
                var names = new[] { "拒绝自动蹲下与状态重检", "接触许可重入与取消", "低帧率生产帧次序",
                    "沿墙过缝与薄墙拐角", "台阶顶头及蹲行正对照", "远岸与开放水面不能借缝支撑", "狭窄方桩不能登陆", "起身途中重新拾取", "台面物品保留站姿" };
                var cases = new Func<IEnumerator>[] { () => RejectedAutoCrouch(c), () => ContactCancellation(c),
                    () => HitchAcrossContact(c), () => WallAndCorner(c), () => StepHeadroom(c), () => SupportBoundary(c), () => NarrowLanding(c), () => PickupWhileStandingUp(c), () => RaisedPickup(c) };
                for (int i = 0; i < cases.Length; i++)
                {
                    Debug.Log(Marker + " CASE " + (i + 1) + ": " + names[i]);
                    var run = cases[i]();
                    try
                    {
                        // 每例的失败仍清理并继续后续案例；不把生产安全断言改成 bug 的行为。
                        while (true)
                        {
                            bool more;
                            try { more = run.MoveNext(); }
                            catch (Exception ex)
                            {
                                failures.Add(names[i] + ": " + ex.Message);
                                Debug.LogWarning(Marker + " CASE FAIL: " + names[i] + "\n" + ex);
                                break;
                            }
                            if (!more) { Debug.Log(Marker + " CASE PASS: " + names[i]); break; }
                            yield return null;
                        }
                    }
                    finally { (run as IDisposable)?.Dispose(); }
                    c.enabled = false;
                    c.CancelWorldAction();
                    if (c.InventoryOpen) c.ToggleInventory();
                    if (c.Paused) c.TogglePause();
                    c.RestoreShopTestProfile(profile);
                    yield return null; // 动作取消帧/模态开关帧必须真正结束。
                }
                Check(failures.Count == 0, "9 个案例全部通过" + (failures.Count == 0 ? "" : "\n" + string.Join("\n", failures)));
            }
            finally
            {
                _afterLateUpdate = null;
                Time.captureDeltaTime = capture;
                Time.maximumDeltaTime = maximum;
                c.CancelWorldAction();
                if (c.InventoryOpen) c.ToggleInventory();
                if (c.Paused) c.TogglePause();
                c.ReturnToHarbor();
                c.RestoreShopTestProfile(profile);
                c.FieldWorld.enabled = fieldEnabled;
                c.enabled = controllerEnabled;
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly FishingPrototype C;
            public readonly Vector3 Origin = new Vector3(4000, 0, 4000);
            private readonly GameObject _root = new GameObject("PR20 boundary physical fixture");
            private FishingFieldWorld.Pickup _pickup;
            private Vector3 _pickupFeet, _pickupPosition;
            private Quaternion _pickupRotation;
            public Fixture(FishingPrototype c) { C = c; }
            public Vector3 At(float x, float y, float z) => Origin + new Vector3(x, y, z);
            public BoxCollider Box(string name, Vector3 localCenter, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_root.transform, false);
                go.transform.position = Origin + localCenter;
                var box = go.AddComponent<BoxCollider>();
                box.size = size;
                return box;
            }
            public void Pad() => Box("Dry fixture floor", new Vector3(0, 1, 0), new Vector3(8, .4f, 8));
            public FishingFieldWorld.Pickup RelocatePickup()
            {
                _pickup = C.FieldWorld.Pickups.FirstOrDefault(p => p.Root != null && p.Item == FieldItem.Shell
                    && !C.Career.Backpack.Collected(p.Id));
                Check(_pickup != null, "存在未领取的原始贝壳网格/拾取记录");
                _pickupFeet = _pickup.Feet;
                _pickupPosition = _pickup.Root.position;
                _pickupRotation = _pickup.Root.rotation;
                _pickup.Feet = At(0, 1.2f, .55f);
                _pickup.Root.SetPositionAndRotation(_pickup.Feet + Vector3.up * .32f, Quaternion.identity);
                Physics.SyncTransforms();
                return _pickup;
            }
            public void Feet(Vector3 position, bool crouched = false)
            {
                C.Shore.SetCrouch(crouched, true);
                C.Shore.Body.enabled = false;
                C.Shore.Actor.SetPositionAndRotation(position, Quaternion.identity);
                C.Shore.Body.enabled = true;
                Check(C.Shore.Relocate(position,Quaternion.identity), "fixture 在真实支撑上清除上例的游泳/攀登状态");
                C.Shore.SetCrouch(crouched,true);
                C.Shore.ResetMoveInput();
                C.View.ResetLook();
                Physics.SyncTransforms();
                // Grounded 和 _lastSafe 只允许由真实 CharacterController/Move 建立。
                for (int i = 0; i < 24; i++) C.Shore.Move(Vector2.zero, false, false, crouched, 1f / 60);
                Check(C.Shore.Grounded, "fixture 通过真实胶囊落地，feet=" + C.Shore.Position.ToString("F4"));
            }
            public void Dispose()
            {
                C.CancelWorldAction();
                if (_pickup != null && _pickup.Root != null)
                {
                    _pickup.Feet = _pickupFeet;
                    _pickup.Root.SetPositionAndRotation(_pickupPosition, _pickupRotation);
                }
                _root.SetActive(false);
                Object.Destroy(_root);
                Physics.SyncTransforms();
            }
        }

        private static IEnumerator Settle(FishingPrototype c, bool crouched)
        {
            c.Shore.SetCrouch(crouched);
            for (int i = 0; i < 120; i++)
            {
                c.Shore.Move(Vector2.zero, false, false, crouched, 1f / 60);
                c.PresentCharacterFrame();
                if (c.Shore.Grounded && c.Shore.Character.IsStanceSettled(true, crouched)) yield break;
                yield return null;
            }
            throw new InvalidOperationException("真实跨帧落地/骨架未稳定，crouched=" + crouched);
        }

        private static FishingPrototype.InteractionPreflight Inspect(FishingPrototype c, FishingFieldWorld.Pickup pickup)
        {
            c.View.ResetLook();
            c.PresentCharacterFrame();
            var delta = pickup.Root.position - c.Shore.Character.EyePosition;
            float pitch = Mathf.Atan2(-delta.y, Vector3.ProjectOnPlane(delta, Vector3.up).magnitude) * Mathf.Rad2Deg;
            c.View.ApplyShoreLookDelta(new Vector2(0, -Mathf.Clamp(pitch, -65, 65) / .12f));
            c.View.ApplyView();
            Physics.SyncTransforms();
            c.FieldWorld.Sense();
            c.WorkshopWorld.Sense();
            c.Shore.UpdateProximity();
            return c.InspectWorldInteractionStart();
        }

        private static FishingPrototype.InteractionPreflight PreparePickup(Fixture f, FishingFieldWorld.Pickup pickup, bool crouched)
        {
            FishingPrototype.InteractionPreflight check = null;
            // 有限几何搜索，不通过反复请求/取消动作选样本；所有候选使用同一真实物品和地板。
            foreach (float distance in new[] { .55f, .45f, .65f, .35f, .75f })
            {
                f.Feet(pickup.Feet + Vector3.back * distance + Vector3.up * .08f, crouched);
                check = Inspect(f.C, pickup);
                if (check.TargetId == "pickup:" + pickup.Id && check.CanBegin && (crouched || check.AutoCrouch))
                    return check;
            }
            throw new InvalidOperationException("没有找到合法低位拾取几何；最后预检=" + check);
        }

        private static void Unchanged(FishingPrototype c, FishingFieldWorld.Pickup pickup, int before, int attempts, string label)
        {
            Check(c.Career.Backpack.Count(pickup.Item) == before && !c.Career.Backpack.Collected(pickup.Id)
                && c.ActionCommitAttempts == attempts, label + "：无领取位/库存/接触提交副作用");
        }

        private static IEnumerator RejectedAutoCrouch(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                f.Pad();
                var pickup = f.RelocatePickup();
                f.Feet(f.At(0, 1.28f, 0));
                var settle = Settle(c, false); while (settle.MoveNext()) yield return null;
                var initial = PreparePickup(f, pickup, false);
                int count = c.Career.Backpack.Count(pickup.Item), attempts = c.ActionCommitAttempts;
                var actor = c.Shore.Position;
                long id = c.Actions.ActionId;
                var again = c.InspectWorldInteractionStart();
                Check(initial.AutoCrouch && again.CanBegin && !c.Shore.Crouched && c.Shore.Position == actor
                    && c.Actions.ActionId == id && !c.WorldActionBusy, "自动蹲预检只读，不提前缩胶囊或启动时钟");

                // 低檐位于前伸手臂路径，而非把站立角色出生在天花板内。
                // 验证视线/碰撞几何确实成立后，才要求拒绝自动蹲起手。
                var beam = f.Box("Low shelf blocks crouched arm", Vector3.zero, new Vector3(.06f, .04f, .06f));
                FishingPrototype.InteractionPreflight blocked = null;
                bool validBeam = false;
                foreach (float fraction in new[] { .65f, .75f, .85f })
                {
                    beam.transform.position = Vector3.Lerp(initial.BaseShoulder, initial.Palm, fraction);
                    Physics.SyncTransforms();
                    bool overlaps = Physics.ComputePenetration(c.Shore.Body, c.Shore.Actor.position, c.Shore.Actor.rotation,
                        beam, beam.transform.position, beam.transform.rotation, out _, out _);
                    blocked = Inspect(c, pickup);
                    if (!overlaps && blocked.TargetId == initial.TargetId && blocked.AutoCrouch && !blocked.ArmSight
                        && blocked.ArmBlocker.Contains(beam.name)) { validBeam = true; break; }
                }
                Check(validBeam, "低檐阻断预测手臂，但不与站立胶囊重叠且不遮掉感知候选；" + blocked);
                Check(!blocked.CanBegin && !c.RequestWorldInteraction() && !c.Shore.Crouched && !c.WorldActionBusy,
                    "低檐阻挡时不允许用自动蹲绕过手臂视线守卫");
                Unchanged(c, pickup, count, attempts, "低檐拒绝");
                beam.enabled = false;
                Physics.SyncTransforms();
                Check(Inspect(c, pickup).CanBegin, "移去低檐后原几何仍能起手");
                c.ToggleInventory();
                try
                {
                    Check(c.InventoryOpen && !c.RequestWorldInteraction() && !c.Shore.Crouched && !c.WorldActionBusy,
                        "预检通过后打开背包，真实请求重新检查状态，不能先自动蹲下");
                    Unchanged(c, pickup, count, attempts, "模态改变拒绝");
                }
                finally { if (c.InventoryOpen) c.ToggleInventory(); }
            }
        }

        private static IEnumerator ContactCancellation(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                f.Pad();
                var pickup = f.RelocatePickup();
                f.Feet(f.At(0, 1.28f, 0), true);
                var settle = Settle(c, true); while (settle.MoveNext()) yield return null;
                PreparePickup(f, pickup, true);
                int count = c.Career.Backpack.Count(pickup.Item), attempts = c.ActionCommitAttempts;
                Check(c.RequestWorldInteraction(), "接触边界从真实拾取请求起手；" + c.Message);
                long cancelled = c.Actions.ActionId;
                c.TickWorldAction(.1f, true, false);
                Check(!c.RequestWorldInteraction() && c.Actions.ActionId == cancelled, "起手重复 E 不换动作编号");
                // 公开时钟到达许可边界但尚未提交，模拟取消优先于同帧接触消费。
                c.Actions.Tick(1);
                Check(c.Actions.ContactPending, "真实时钟已产生尚未消费的接触许可");
                c.CancelWorldAction();
                c.TickWorldAction(1, true, false);
                Check(!c.Actions.TryConsumeContact(cancelled) && !c.RequestWorldInteraction() && !c.WorldActionBusy,
                    "接触待消费时取消作废旧许可，并封锁取消帧重入");
                Unchanged(c, pickup, count, attempts, "接触前取消");
                yield return null;
                PreparePickup(f, pickup, true);
                Check(c.RequestWorldInteraction() && c.Actions.ActionId > cancelled, "下一帧同一物品以新编号重试");
                long committed = c.Actions.ActionId;
                c.Actions.Tick(1);
                Check(!c.Actions.TryConsumeContact(cancelled) && c.Actions.ContactPending,
                    "旧编号不能盗取消费新动作已到达的许可");
                c.TickWorldAction(0, true, false);
                Check(c.LastActionSucceeded && c.LastActionId == committed
                    && c.Career.Backpack.Count(pickup.Item) == count + pickup.Count
                    && c.Career.Backpack.Collected(pickup.Id) && c.ActionCommitAttempts == attempts + 1,
                    "新动作由生产接触路径恰好提交一次；" + c.Message);
                c.TickWorldAction(5, true, false);
                Check(!c.RequestWorldInteraction() && !c.Actions.TryConsumeContact(committed), "接触帧重复 Tick/E 不获得第二份许可");
                c.CancelWorldAction();
                c.CancelWorldAction();
                yield return null;
                c.TickWorldAction(5, true, false);
                Check(c.Career.Backpack.Count(pickup.Item) == count + pickup.Count && c.Career.Backpack.Collected(pickup.Id)
                    && c.ActionCommitAttempts == attempts + 1, "提交后的重复取消既不回滚收益也不重复提交");
            }
        }

        private static IEnumerator HitchAcrossContact(FishingPrototype c)
        {
            float capture = Time.captureDeltaTime, maximum = Time.maximumDeltaTime;
            using (var f = new Fixture(c))
            {
                try
                {
                    f.Pad();
                    var pickup = f.RelocatePickup();
                    f.Feet(f.At(0, 1.28f, 0));
                    var settle = Settle(c, false); while (settle.MoveNext()) yield return null;
                    var check = PreparePickup(f, pickup, false);
                    Check(check.AutoCrouch && c.Shore.Character.IsStanceSettled(true, false), "从完整站姿发起合法低位拾取");
                    int count = c.Career.Backpack.Count(pickup.Item), attempts = c.ActionCommitAttempts;
                    c.enabled = true;
                    // PlayerLoop 回调位于生产 Update 后；控制器该帧 Update 已结束（或此前禁用），
                    // 此请求对应生产 Update 尾部的 E，而不是把接触 Tick 塞到请求同帧。
                    Check(c.RequestWorldInteraction(), "站立 E 请求被生产起手路径接受；" + check + " message=" + c.Message);
                    long action = c.Actions.ActionId;
                    int requestFrame = Time.frameCount;
                    float remainingAfterLate = -1;
                    _afterLateUpdate = () =>
                    {
                        remainingAfterLate = c.Shore.Character.RemainingCrouchDrop;
                        Time.maximumDeltaTime = Mathf.Max(maximum, .5f);
                        Time.captureDeltaTime = .4f; // 下一帧模拟卡顿，越过 .36s 接触但不人为调用 Tick/Pose。
                    };
                    yield return null;
                    Check(Time.frameCount == requestFrame + 1 && Time.deltaTime >= .39f && remainingAfterLate > .01f,
                        "正常请求帧 LateUpdate 后骨架仍在过渡，下一真实 Update 使用 >=0.39s；remaining="
                        + remainingAfterLate.ToString("F4") + " dt=" + Time.deltaTime.ToString("F4"));
                    // 允许等待真实蹲姿落位，但不能因上一帧骨架而丢弃合法动作。
                    Time.captureDeltaTime = 1f / 60;
                    for (int i = 0; i < 90 && c.WorldActionBusy; i++) yield return null;
                    Check(c.LastActionSucceeded && c.LastActionId == action
                        && c.Career.Backpack.Count(pickup.Item) == count + pickup.Count
                        && c.Career.Backpack.Collected(pickup.Id) && c.ActionCommitAttempts == attempts + 1,
                        "合法自动蹲拾取跨低帧率接触仍成功且恰一次；message=" + c.Message
                        + " remainingBeforeContact=" + remainingAfterLate.ToString("F4"));
                    Time.captureDeltaTime = 1f / 60;
                    for (int i = 0; i < 30; i++) yield return null;
                    Check(c.ActionCommitAttempts == attempts + 1 && c.Career.Backpack.Count(pickup.Item) == count + pickup.Count
                        && !c.Actions.TryConsumeContact(action), "后续真实 Update/LateUpdate 不重放已提交拾取");
                }
                finally
                {
                    _afterLateUpdate = null;
                    c.enabled = false;
                    Time.captureDeltaTime = capture;
                    Time.maximumDeltaTime = maximum;
                }
            }
        }

        private static IEnumerator PickupWhileStandingUp(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                f.Pad(); var pickup = f.RelocatePickup();
                f.Feet(f.At(0, 1.28f, .1f), true);
                var settle = Settle(c, true); while (settle.MoveNext()) yield return null;
                PreparePickup(f, pickup, true);
                c.Shore.SetCrouch(false);
                yield return null;
                c.PresentCharacterFrame();
                float remaining = c.Shore.Character.RemainingCrouchDrop;
                Check(remaining > .01f && remaining < FishingCharacter.CrouchDrop - .01f,
                    "松开 Ctrl 后骨架确实处于起身中途");
                var check = Inspect(c, pickup);
                Check(check.CanBegin && check.AutoCrouch,
                    "起身中 E 按将要稳定的姿态规划低位拾取，而非把瞬时可达误当站姿可达；" + check);
                int count = c.Career.Backpack.Count(pickup.Item), attempts = c.ActionCommitAttempts;
                c.enabled = true;
                Check(c.RequestWorldInteraction(), "起身中重新 E 可以自动俯身起手");
                for (int i = 0; i < 100 && c.WorldActionBusy; i++) yield return null;
                Check(c.LastActionSucceeded && c.Career.Backpack.Count(pickup.Item) == count + pickup.Count
                    && c.ActionCommitAttempts == attempts + 1, "起身中重拾最终只提交一次");
            }
        }

        private static IEnumerator RaisedPickup(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                f.Pad(); var pickup = f.RelocatePickup();
                pickup.Root.position += Vector3.up * .5f;
                f.Feet(pickup.Feet + Vector3.back * .36f + Vector3.up * .08f);
                var settle = Settle(c, false); while (settle.MoveNext()) yield return null;
                var check = Inspect(c, pickup);
                Check(check.CanBegin && !check.AutoCrouch && check.Contact.y < check.Shoulder.y - .4f,
                    "肩下台面物品可达时保留站姿，不仅凭高度强制蹲下；" + check);
                var crouchedShoulder = check.Shoulder + c.Shore.Character.RemainingCrouchOffset;
                var beam = f.Box("Low railing below standing arm", Vector3.zero, new Vector3(.05f, .035f, .05f));
                beam.transform.position = Vector3.Lerp(crouchedShoulder, check.Palm, .68f);
                Physics.SyncTransforms();
                var ray = check.Palm - crouchedShoulder;
                Check(Physics.RaycastAll(crouchedShoulder, ray.normalized, ray.magnitude).Any(h => h.collider == beam),
                    "低栏杆确实挡住蹲姿手臂路径");
                check = Inspect(c, pickup);
                Check(check.CanBegin && !check.AutoCrouch && check.ArmSight,
                    "低栏杆不遮挡站姿时仍允许原有合法拾取");
                int count = c.Career.Backpack.Count(pickup.Item);
                c.enabled = true;
                Check(c.RequestWorldInteraction() && !c.Shore.Crouched, "台面拾取不会先把角色蹲到栏杆后");
                for (int i = 0; i < 100 && c.WorldActionBusy; i++) yield return null;
                Check(c.LastActionSucceeded && c.Career.Backpack.Count(pickup.Item) == count + pickup.Count,
                    "站姿台面拾取接触事务成功；message=" + c.Message + " attempts=" + c.ActionCommitAttempts + " busy=" + c.WorldActionBusy);
            }
        }

        private static void Walk(FishingPrototype c, Vector2 input, bool crouched = false, bool sprint = false)
        {
            c.viewCamera.transform.rotation = Quaternion.identity;
            c.Shore.Move(input, sprint, false, crouched, .05f);
            Physics.SyncTransforms();
        }

        private static void NoPenetration(FishingPrototype c, Collider obstacle, string label)
        {
            bool overlap = Physics.ComputePenetration(c.Shore.Body, c.Shore.Actor.position, c.Shore.Actor.rotation,
                obstacle, obstacle.transform.position, obstacle.transform.rotation, out var direction, out float depth);
            // CharacterController 的 skinWidth 允许轻微接触压入；不能把整个薄墙宽度作为容差。
            float tolerance = c.Shore.Body.skinWidth + .008f;
            if (overlap && depth > tolerance)
                throw new InvalidOperationException(label + "：胶囊穿入 " + obstacle.name + " depth=" + depth.ToString("F4")
                    + " direction=" + direction + " feet=" + c.Shore.Position.ToString("F4"));
        }

        private static void JoinedFloors(Fixture f, float rise = .2f)
        {
            f.Box("Low approach", new Vector3(0, .9f, -2), new Vector3(6, .6f, 4)); // top 1.20, ends z=0
            f.Box("Raised far slab", new Vector3(0, .9f + rise, 2.02f), new Vector3(6, .6f, 3.96f)); // 高台起点 z=.04
        }

        private static IEnumerator WallAndCorner(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                JoinedFloors(f);
                var side = f.Box("Thin side wall", new Vector3(.9f, 2.5f, .5f), new Vector3(.04f, 3, 7));
                var corner = f.Box("Thin perpendicular corner", new Vector3(0, 2.5f, 2.5f), new Vector3(6, 3, .04f));
                f.Feet(f.At(.42f, 1.28f, -1));
                float maxZ = c.Shore.Position.z;
                for (int i = 0; i < 65; i++)
                {
                    Walk(c, new Vector2(.35f, 1), sprint: i > 40);
                    maxZ = Mathf.Max(maxZ, c.Shore.Position.z);
                    NoPenetration(c, side, "沿墙/拐角");
                    NoPenetration(c, corner, "沿墙/拐角");
                    // 逐步检测，防止先穿墙再被最后一帧位置掩盖。
                    CheckBehind(c, side.bounds.min.x, true, "侧墙");
                    CheckBehind(c, corner.bounds.min.z, false, "拐角薄墙");
                    yield return null;
                }
                Check(maxZ > f.Origin.z + 1 && c.Shore.Position.y > 1.32f,
                    "带朝墙分量仍沿墙越过4cm缝/20cm台阶，不把滑墙误当无路可走；feet=" + c.Shore.Position.ToString("F4"));
                Check(c.Shore.Position.z < corner.bounds.min.z, "持续快走进入拐角不能穿过小于单步位移的薄墙");
            }
        }

        private static void CheckBehind(FishingPrototype c, float nearFace, bool xAxis, string label)
        {
            float coordinate = xAxis ? c.Shore.Position.x : c.Shore.Position.z;
            if (coordinate > nearFace - c.Shore.Body.radius + c.Shore.Body.skinWidth + .012f)
                throw new InvalidOperationException(label + "：胶囊中心越过安全侧，feet=" + c.Shore.Position.ToString("F4")
                    + " face=" + nearFace.ToString("F4"));
        }

        private static IEnumerator StepHeadroom(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                JoinedFloors(f, .14f);
                float roofBottom = 1.2f + FishingLocomotion.StandingHeight + .06f;
                var roof = f.Box("Step low ceiling", new Vector3(0, roofBottom + .1f, 1), new Vector3(6, .2f, 4));
                f.Feet(f.At(0, 1.28f, -1.4f));
                Check(c.Shore.Clearance(c.Shore.Position), "低层站姿有真实头部净空，不从重叠状态开始");
                for (int i = 0; i < 45; i++)
                {
                    Walk(c, Vector2.up);
                    NoPenetration(c, roof, "台阶顶头");
                    if (c.Shore.Position.z > f.Origin.z + .08f)
                        throw new InvalidOperationException("站姿在台阶顶净空不足时仍进入高台；feet=" + c.Shore.Position.ToString("F4"));
                    yield return null;
                }
                Check(c.Shore.Position.z < f.Origin.z + .08f, "预抬升不能把站姿挤进低顶高台");
                for (int i = 0; i < 65 && c.Shore.Position.z < f.Origin.z + 1.5f; i++)
                {
                    Walk(c, Vector2.up, true);
                    NoPenetration(c, roof, "同一低顶蹲行正对照");
                    yield return null;
                }
                Check(c.Shore.Crouched && c.Shore.Position.z > f.Origin.z + 1.2f && c.Shore.Position.y > 1.28f,
                    "同一真实低顶允许有足够净空的蹲行，拒绝不是 fixture 把通道完全堵死");
                c.Shore.SetCrouch(false);
                Check(c.Shore.Crouched, "台阶顶低檐下松开蹲键不能强制站起穿顶");
            }
        }

        private static IEnumerator NarrowLanding(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                var post = f.Box("Isolated narrow post", new Vector3(0, 1, 0), new Vector3(.26f, .4f, .26f));
                Physics.SyncTransforms();
                var top = f.At(0, 1.2f, 0);
                Check(c.Shore.Ground(top, out _), "方桩中心存在真实干燥顶面");
                Check(!c.Shore.SafeCoastalFooting(top), "26cm 方桩不能借周边球形容差通过登陆支撑检查");
                post.size = new Vector3(1, .4f, 1);
                Physics.SyncTransforms();
                Check(c.Shore.SafeCoastalFooting(top), "放大为1m实心平台后安全支撑通过");
            }
            yield return null;
        }

        private static IEnumerator SupportBoundary(FishingPrototype c)
        {
            using (var f = new Fixture(c))
            {
                f.Box("Near bank", new Vector3(0, 1, -2), new Vector3(6, .4f, 4));
                var far = f.Box("Far bank", new Vector3(0, 1, 2.02f), new Vector3(6, .4f, 3.96f));
                Physics.SyncTransforms();
                var seam = f.At(0, 1.25f, .02f);
                Check(!Physics.Raycast(seam + Vector3.up, Vector3.down, out _, 1.5f, ~(1 << 4), QueryTriggerInteraction.Ignore),
                    "正对照：中心射线确实漏掉4cm地缝");
                f.Feet(f.At(0, 1.28f, -.8f));
                for (int i = 0; i < 22; i++) { Walk(c, Vector2.up); yield return null; }
                Check(c.Shore.Position.z > f.Origin.z + .8f, "连续步行可以跨4cm地缝");
                // 将同一实体移到 1.2m 水沟对岸，不能继续拿远岸做地缝支撑。
                far.transform.position += Vector3.forward * 1.16f;
                var water = f.Box("Water-layer surface", new Vector3(0, 1.1f, .6f), new Vector3(6, .2f, 1.1f));
                water.gameObject.layer = 4;
                var trigger = f.Box("Non-solid water trigger", new Vector3(0, 1.1f, .6f), new Vector3(6, .2f, 1.1f));
                trigger.isTrigger = true;
                f.Box("NavMesh", new Vector3(0, .95f, .6f), new Vector3(6, .1f, 1.1f));
                f.Box("Submerged bed", new Vector3(0, .1f, 1), new Vector3(6, .4f, 3));
                Physics.SyncTransforms();
                foreach (float z in new[] { .4f, .6f, .8f })
                    Check(!c.Shore.Ground(f.At(0, 0, z), out _), "水沟中心不能借远岸/水层/触发器/导航壳/水下底床支撑，z=" + z);
                f.Feet(f.At(0, 1.28f, -.8f));
                for (int i = 0; i < 70; i++)
                {
                    Walk(c, Vector2.up, sprint: true);
                    if(c.Shore.Swimming)break;
                    Check(c.Shore.Position.z<=f.Origin.z+c.Shore.Body.radius+.05f && c.Shore.Position.y>=.6f,
                        "无支撑时不能继续当作干地步行");
                    yield return null;
                }
                Check(c.Shore.Swimming && c.Shore.Position.z < far.bounds.min.z - .5f, "离岸后进入V026真实游泳，没有借远岸作为地缝支撑");
                far.enabled = false;
                Physics.SyncTransforms();
                Check(!c.Shore.Ground(f.At(0, 0, 1.6f), out _), "移除远岸后开放水面无支撑");
                for (int i = 0; i < 12; i++) { Walk(c, new Vector2(.15f, 1), sprint: true); yield return null; }
                Check(c.Shore.Swimming && !c.Shore.Ground(f.At(0,0,.6f),out _),
                    "开放水面保持真实游泳，不生成干地支撑或隐形水面桥");
            }
        }
    }
}
