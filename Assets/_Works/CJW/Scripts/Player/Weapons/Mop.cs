using System.Collections;
using DevLib.BattleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public class Mop : AbstractCleaner
    {
        [Tooltip("대걸레의 중앙을 넣어주면 된다. 이 Transform은 충돌 방향을 체크할 때 쓰일 예정")]
        [SerializeField] private Transform center;
        
        [Tooltip("대걸레의 끝을 넣어주면 된다. 이 Transform은 충돌 방향을 체크할 때 쓰일 예정")]
        [SerializeField] private Transform peak;
        
        [Tooltip("대걸레의 충돌을 체크할 대미지 케스터. peak와 같은 오브젝트를 넣어주면 된다.")]
        [SerializeField] private CleanerCaster caster;

        private Vector3 Dir => peak.position - center.position;

        private WaitForSeconds _waitT = new WaitForSeconds(0.2f);
        
        public override void UseCleaner()
        {
            base.UseCleaner();
            StartCoroutine(CleanCoroutine());
        }

        private IEnumerator CleanCoroutine()
        {
            while (IsUsing)
            {
                caster.CastClean(Dir);
                yield return _waitT;
            }
        }
        
    }
}