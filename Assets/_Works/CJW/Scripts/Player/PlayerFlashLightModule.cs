using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public class PlayerFlashLightModule : MonoBehaviour, IModule, IFlashLightModule
    {
        [SerializeField] private Light flashLight;
        private ModuleOwner _owner;

        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
        }

        public void ActiveFlashLight(bool enable)
        {
            Debug.Assert(flashLight == null, "flashLight == null");
            flashLight.enabled = enable;
        }

        public void SetIntensify(float intensify)
            => flashLight.intensity = intensify;

        public void SetColor(Color color)
            => flashLight.color = color;

        public void SetAngle(float? minAngle = null, float? maxAngle = null)
        {
            if(minAngle.HasValue)
                flashLight.innerSpotAngle = minAngle.Value;
            if(maxAngle.HasValue)
                flashLight.spotAngle = maxAngle.Value;
        }
    }
}