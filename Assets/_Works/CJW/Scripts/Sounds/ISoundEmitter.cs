using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Sounds
{
    /// <summary>손님·차가 소리를 내는 창구. 부르는 쪽은 "이 소리를 내"까지만 알고, 어느 채널로 어떻게 틀지는 구현이 정한다.
    /// 클립이 비어 있으면 조용히 넘어간다 — 소리를 아직 채우지 않은 프리팹이 에러 없이 돌아가게 한다.</summary>
    public interface ISoundEmitter
    {
        /// <summary>반복 소리를 틀고 있는지.</summary>
        bool IsLooping { get; }

        /// <summary>지금 틀고 있는 반복 소리. 없으면 null. 반복 소리는 주인마다 하나라, 끄기 전에 내가 튼 것인지 확인할 때 쓴다.</summary>
        SoundClipSo CurrentLoop { get; }

        /// <summary>주인 위치에서 한 번 튼다.</summary>
        void Play(SoundClipSo clip);

        /// <summary><paramref name="position"/>에서 한 번 튼다. 차 문이나 주먹이 닿은 자리처럼 몸 중심이 아닌 곳에서 나는 소리에 쓴다.</summary>
        void Play(SoundClipSo clip, Vector3 position);

        /// <summary>주인을 따라다니는 반복 소리를 튼다. 주인마다 하나만 틀 수 있고, 새로 틀면 앞의 것은 끊긴다.
        /// 클립의 loop가 켜져 있어야 한다. <see cref="StopLoop"/>로 끈다.</summary>
        void PlayLoop(SoundClipSo clip);

        /// <summary>틀어 둔 반복 소리를 끈다. 틀고 있지 않으면 아무것도 하지 않는다.</summary>
        void StopLoop();
    }
}
