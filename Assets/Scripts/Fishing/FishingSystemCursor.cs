using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>OS boundary: batch-mode Unity cannot lock a real cursor, so integration tests supply a recorder.</summary>
    public interface IFishingCursor
    {
        CursorLockMode LockState { get; set; }
        bool Visible { get; set; }
    }
    public sealed class FishingSystemCursor : IFishingCursor
    {
        public CursorLockMode LockState { get => Cursor.lockState; set => Cursor.lockState=value; }
        public bool Visible { get => Cursor.visible; set => Cursor.visible=value; }
    }
}
