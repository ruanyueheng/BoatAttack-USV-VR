namespace BoatAttack
{
    /// <summary>Unit-explicit frame-budget decision, shared with hardware-free tests.</summary>
    public static class DynamicResolutionPolicy
    {
        public static float GetAdjustment(float frameTimeSeconds, int targetFps, float step)
        {
            if (float.IsNaN(frameTimeSeconds) || float.IsInfinity(frameTimeSeconds) || frameTimeSeconds <= 0f
                || targetFps <= 0 || float.IsNaN(step) || float.IsInfinity(step) || step <= 0f)
                return 0f;
            var budgetSeconds = 1f / targetFps;
            if (frameTimeSeconds == budgetSeconds) return 0f;
            return frameTimeSeconds > budgetSeconds ? -step : step;
        }
    }
}
