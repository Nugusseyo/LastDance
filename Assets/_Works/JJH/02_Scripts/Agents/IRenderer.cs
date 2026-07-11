using UnityEngine;

namespace JJH._02_Scripts.Agents
{
    public interface IRenderer
    {
        Animator Animator { get; }

        void PlayClip(int clipHash, float normalizedTime, float crossFadeDuration, int layerIndex = 0);
        void SetBool(int hash, bool value);
        void SetFloat(int hash, float value, float dampTime = 0, float deltaTime = 0);
    }
}