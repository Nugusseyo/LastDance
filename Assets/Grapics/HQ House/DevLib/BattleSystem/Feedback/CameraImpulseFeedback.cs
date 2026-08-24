using Unity.Cinemachine;
using UnityEngine;

namespace DevLib.BattleSystem.Feedback
{
    public class CameraImpulseFeedback : AbstractFeedback
    {
        [SerializeField] private CameraShakeDataSO    _shakeData;
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        public override void PlayFeedback()
        {
            if (_impulseSource == null || _shakeData == null) return;
            _impulseSource.GenerateImpulse(_shakeData.direction.normalized * _shakeData.force);
        }
    }
}
