namespace BoatAttack.Fishing
{
    /// <summary>Do not treat a held mouse button on focus/pause recovery as a new tool action.</summary>
    public sealed class FishingPointerGate
    {
        public bool Ready { get; private set; }
        public void Reset() { Ready = false; }
        public bool Update(bool focused, bool paused, bool leftHeld, bool rightHeld)
        {
            if (!focused || paused) { Reset(); return false; }
            if (!leftHeld && !rightHeld) Ready = true;
            return Ready;
        }
    }
}
