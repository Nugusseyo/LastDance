using _Works.CJW.Scripts.Sounds;
using _Works.JJH._02_Scripts.Objects;
using DevLib.ModuleSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>플레이어가 주유하는 동안 주유 소리를 반복해서 튼다. 주유 시작·끝은 차에 달린 <see cref="FuelDoor"/>가 알려 준다.
    /// 소리는 차를 따라다닌다. 재생은 같은 차의 사운드 모듈(<see cref="ISoundEmitter"/>)이 한다.</summary>
    [DisallowMultipleComponent]
    public class CarFuelSoundModule : AbstractModule
    {
        [Tooltip("주유하는 동안 반복할 소리. SoundClipSo의 loop를 켜야 한다.")]
        [SerializeField] private SoundClipSo fuelSound;

        private FuelDoor _fuelDoor;
        private ISoundEmitter _sound;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _sound = owner != null ? owner.GetModule<ISoundEmitter>() : null;
            _fuelDoor = owner != null ? owner.GetComponentInChildren<FuelDoor>(true) : null;

            if (_sound == null || _fuelDoor == null)
            {
                Debug.LogWarning($"[{nameof(CarFuelSoundModule)}] {name}에 사운드 모듈이나 {nameof(FuelDoor)}가 없어 주유 소리를 내지 않습니다.", this);
                return;
            }

            // Initialize가 다시 불려도 두 번 듣지 않게 먼저 뗀다.
            _fuelDoor.OnFuelingStarted -= HandleFuelingStarted;
            _fuelDoor.OnFuelingEnded -= HandleFuelingEnded;
            _fuelDoor.OnFuelingStarted += HandleFuelingStarted;
            _fuelDoor.OnFuelingEnded += HandleFuelingEnded;
        }

        private void OnDestroy()
        {
            if (_fuelDoor != null)
            {
                _fuelDoor.OnFuelingStarted -= HandleFuelingStarted;
                _fuelDoor.OnFuelingEnded -= HandleFuelingEnded;
            }
        }

        private void HandleFuelingStarted()
        {
            // 멈춰 선 차라 남아 있는 주행 소리를 바꿔 끼워도 된다.
            _sound.PlayLoop(fuelSound);
        }

        private void HandleFuelingEnded()
        {
            // 그새 다른 반복 소리로 바뀌었으면 그 소리는 끄지 않는다.
            if (fuelSound != null && _sound.CurrentLoop == fuelSound)
            {
                _sound.StopLoop();
            }
        }
    }
}
