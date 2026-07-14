using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public interface IFlashLightModule
    {
        void ActiveFlashLight(bool enable);
        void SetIntensify(float intensify);
        void SetColor(Color color);
        void SetAngle(float? minAngle = null, float? maxAngle = null);
    }
}