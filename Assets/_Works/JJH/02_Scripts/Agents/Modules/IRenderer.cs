using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public interface IRenderer
    {
        Animator Animator { get; }

        void SetVisualPos(Vector3 fixedPos);
        void PlayClip(int clipHash, float normalizedTime, float crossFadeDuration, int layerIndex = 0);
        void SetBool(int hash, bool value);
        void SetFloat(int hash, float value, float dampTime = 0, float deltaTime = 0);
    }
}