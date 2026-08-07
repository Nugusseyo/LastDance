using DevLib.ModuleSystem;
using UnityEngine;

namespace JJH._02_Scripts.Agents
{
    public class AgentRenderer : AbstractModule, IRenderer
    {
        public Animator Animator { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            Animator = GetComponent<Animator>();
        }

        public void SetVisualPos(Vector3 fixedPos)
        {
            gameObject.transform.position = transform.parent.position + fixedPos;
        }

        public void PlayClip(int clipHash, float normalizedTime, float crossFadeDuration, int layerIndex = 0)
        {
            Animator.CrossFadeInFixedTime(clipHash, crossFadeDuration, layerIndex, normalizedTime);
        }

        public void SetFloat(int hash, float value, float dampTime = 0, float deltaTime = 0)
        {
            Animator.SetFloat(hash, value, dampTime, deltaTime);
        }

        public void SetBool(int hash, bool value)
        {
            Animator.SetBool(hash, value);
        }
    }
}