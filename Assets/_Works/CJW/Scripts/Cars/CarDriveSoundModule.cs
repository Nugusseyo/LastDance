using _Works.CJW.Scripts.Sounds;
using DevLib.ModuleSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>차가 달리는 동안 주행 소리를 반복해서 튼다. 누가 차를 움직였는지(방문·도난)와 상관없이
    /// 교통 센서(<see cref="ICarTrafficSensor"/>)가 잰 실제 속도만 보고 켜고 끈다.
    /// 소리는 차를 따라다닌다. 재생은 같은 차의 사운드 모듈(<see cref="ISoundEmitter"/>)이 한다.</summary>
    [DisallowMultipleComponent]
    public class CarDriveSoundModule : AbstractModule
    {
        [Tooltip("달리는 동안 반복할 소리. SoundClipSo의 loop를 켜야 한다.")]
        [SerializeField] private SoundClipSo driveSound;

        [Tooltip("이 속도(m/s)를 넘으면 달리는 것으로 보고 소리를 켠다.")]
        [SerializeField, Min(0f)] private float startSpeed = 0.5f;

        [Tooltip("속도가 이 값(m/s) 아래로 떨어진 채 stopDelay초가 지나면 소리를 끈다. startSpeed보다 작아야 켜졌다 꺼졌다 떨지 않는다.")]
        [SerializeField, Min(0f)] private float stopSpeed = 0.2f;

        [Tooltip("멈춘 뒤 소리를 끄기까지 기다리는 시간(초). 앞차에 막혀 잠깐 서는 동안 소리가 끊기지 않게 한다.")]
        [SerializeField, Min(0f)] private float stopDelay = 1f;

        private ICarTrafficSensor _sensor;
        private ISoundEmitter _sound;
        private float _stoppedFor;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _sensor = owner != null ? owner.GetModule<ICarTrafficSensor>() : null;
            _sound = owner != null ? owner.GetModule<ISoundEmitter>() : null;

            if (_sensor == null || _sound == null)
            {
                Debug.LogWarning($"[{nameof(CarDriveSoundModule)}] {name}에 교통 센서나 사운드 모듈이 없어 주행 소리를 내지 않습니다.", this);
            }

            if (driveSound != null && !driveSound.loop)
            {
                Debug.LogWarning($"[{nameof(CarDriveSoundModule)}] {driveSound.name}의 loop가 꺼져 있어 주행 소리를 내지 않습니다.", this);
            }
        }

        private void OnDisable()
        {
            _stoppedFor = 0f;
        }

        // 센서가 LateUpdate에서 속도를 갱신하므로 같은 시점에 본다.
        private void LateUpdate()
        {
            // loop가 꺼진 클립은 PlayLoop가 거절하므로, 걸러 두지 않으면 매 프레임 다시 틀려고 든다.
            if (_sensor == null || _sound == null || driveSound == null || !driveSound.loop)
            {
                return;
            }

            Vector3 velocity = _sensor.Velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            // 반복 소리는 차마다 하나다. 주유 소리처럼 다른 모듈이 튼 소리는 건드리지 않는다.
            bool playing = _sound.CurrentLoop == driveSound;

            if (!playing)
            {
                if (_sound.IsLooping)
                {
                    return;
                }

                if (speed >= startSpeed)
                {
                    _stoppedFor = 0f;
                    _sound.PlayLoop(driveSound);
                }

                return;
            }

            if (speed > stopSpeed)
            {
                _stoppedFor = 0f;
                return;
            }

            _stoppedFor += Time.deltaTime;
            if (_stoppedFor >= stopDelay)
            {
                _sound.StopLoop();
            }
        }
    }
}
