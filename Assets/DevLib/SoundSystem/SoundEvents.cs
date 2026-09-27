using DevLib.EventChannelSystem;
using UnityEngine;

namespace DevLib.SoundSystem
{
    public static class SoundEvents
    {
        public static readonly PlaySoundEvent PlaySoundEvent = new PlaySoundEvent();
        public static readonly StopSoundEvent StopSoundEvent = new StopSoundEvent();
    }

    public class PlaySoundEvent : GameEvent
    {
        public Vector3 Position;
        public SoundClipSo ClipData;
        public int ChannelNumber;

        /// <summary>소리가 따라다닐 대상. 달리는 차처럼 움직이는 곳에서 나는 반복 소리에 쓴다. null이면 Position에 머문다.</summary>
        public Transform Follow;

        public PlaySoundEvent Init(Vector3 position, SoundClipSo clipData, int channelNumber = 0, Transform follow = null)
        {
            Position = position;
            ClipData = clipData;
            ChannelNumber = channelNumber;
            Follow = follow;
            return this;
        }
    }

    public class StopSoundEvent : GameEvent
    {
        public int ChannelNumber;

        public StopSoundEvent Init(int channelNumber = 0)
        {
            ChannelNumber = channelNumber;
            return this;
        }
    }

}