using System;

namespace BoatAttack.Fishing
{
    public enum FishingActionKind
    {
        None, Harvest, Pickup, Place, Repair, Inspect, Cast, Scoop, Store, Release,
        Board, UseTool, Craft
    }

    /// <summary>
    /// 仅由调用方传入的秒数驱动的动作时钟，不持有库存、存档或系统时间。
    /// 接触只授予一次提交许可；调用方须在消费许可后重新验证并提交现有规则，
    /// 交易失败也不能再次消费同一动作的许可。相机切换无需重启动作。
    /// </summary>
    public sealed class FishingActionClock
    {
        private long lastActionId;
        private double elapsed;
        private double duration;
        private double contactTime;
        private bool contactConsumed;

        /// <summary>动作仍在播放；结束后仍可能有待消费的接触。</summary>
        public bool Active { get; private set; }
        public bool ContactPending { get; private set; }
        /// <summary>实例内单调递增的动作编号；初始或取消后为零。</summary>
        public long ActionId { get; private set; }
        public string TargetId { get; private set; } = string.Empty;
        public FishingActionKind Kind { get; private set; }
        /// <summary>归一化进度；完成后保留为一，取消后归零。</summary>
        public float Progress => duration > 0 ? (float)(elapsed / duration) : 0f;

        /// <summary>
        /// 时间单位为秒，目标标识须非空白。拒绝无效输入、重叠动作和未消费接触，
        /// 且不改变现有状态。接触时间为零时，在成功开始的当下即可消费。
        /// </summary>
        public bool TryBegin(FishingActionKind kind, string targetId, float duration, float contactTime)
        {
            if (Active || ContactPending || lastActionId == long.MaxValue ||
                kind == FishingActionKind.None || !Enum.IsDefined(typeof(FishingActionKind), kind) ||
                string.IsNullOrWhiteSpace(targetId) || !Finite(duration) || duration <= 0f ||
                !Finite(contactTime) || contactTime < 0f || contactTime > duration)
                return false;

            ActionId = ++lastActionId;
            TargetId = targetId;
            Kind = kind;
            this.duration = duration;
            this.contactTime = contactTime;
            elapsed = 0;
            contactConsumed = false;
            ContactPending = contactTime == 0f;
            Active = true;
            return true;
        }

        /// <summary>
        /// 暂停、零或无效增量均不推进。使用双精度累计并先记录接触再结束，
        /// 避免大增量溢出或低帧率同时越过两个阈值时漏掉许可。
        /// 完成后保留编号、目标、类型和进度，直到下一次成功开始或显式取消。
        /// </summary>
        public void Tick(float deltaTime, bool paused = false)
        {
            if (!Active || paused || !Finite(deltaTime) || deltaTime <= 0f)
                return;

            elapsed = Math.Min(duration, elapsed + (double)deltaTime);
            if (!contactConsumed && elapsed >= contactTime)
                ContactPending = true;
            if (elapsed >= duration)
                Active = false;
        }

        /// <summary>只消费当前动作已到达的接触，旧编号或重复消费均失败。</summary>
        public bool TryConsumeContact(long actionId)
        {
            if (actionId != ActionId || !ContactPending)
                return false;

            ContactPending = false;
            contactConsumed = true;
            return true;
        }

        /// <summary>作废未消费许可并清空当前状态，但绝不回退编号计数器。</summary>
        public void Cancel()
        {
            Active = false;
            ContactPending = false;
            ActionId = 0;
            TargetId = string.Empty;
            Kind = FishingActionKind.None;
            elapsed = duration = contactTime = 0;
            contactConsumed = false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
