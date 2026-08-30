using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Modules
{
    public interface IPlayerCamera
    {
        Transform CameraTrans { get; }

        void SetCameraShake(bool isRunning);
    }
}
