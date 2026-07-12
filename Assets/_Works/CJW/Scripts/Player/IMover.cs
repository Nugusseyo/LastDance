using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public interface IMover
    {
        bool IsGround { get; }
        bool CanManualMove { get; set; }
        void SetMovementDirection(Vector2 inputDirection);
        void StopImmediately(bool horizontal, bool vertical);
    }
}