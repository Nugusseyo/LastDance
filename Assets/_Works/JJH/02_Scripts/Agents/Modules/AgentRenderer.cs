using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public class AgentRenderer : AbstractModule, IRenderer
    {
        public Animator Animator { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            // 손님처럼 Animator가 visual 자식에 있는 프리팹도 있다. 같은 오브젝트면 이쪽이 먼저 잡힌다.
            Animator = GetComponentInChildren<Animator>(true);
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