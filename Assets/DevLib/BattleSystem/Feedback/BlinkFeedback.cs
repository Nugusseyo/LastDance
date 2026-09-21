using System.Collections;
using UnityEngine;

namespace DevLib.BattleSystem.Feedback
{
    public class BlinkFeedback : AbstractFeedback
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private float blinkDuration;
        [SerializeField] private float blinkValue;

        private readonly int _blinkShaderParam = Shader.PropertyToID("_BlinkValue");
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            Debug.Assert(targetRenderer != null, $"{gameObject.name} 피드백에 타겟 렌더링이 설정되지 않았습니다.");
            _mpb = new MaterialPropertyBlock();
        }

        public override void PlayFeedback()
        {
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_blinkShaderParam, blinkValue);
            targetRenderer.SetPropertyBlock(_mpb);
            StartCoroutine(SetNormalAfterDelay());
        }

        private IEnumerator SetNormalAfterDelay()
        {
            yield return new WaitForSeconds(blinkDuration);
            StopFeedback();
        }

        public override void StopFeedback()
        {
            StopAllCoroutines();
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_blinkShaderParam, 0f);
            targetRenderer.SetPropertyBlock(_mpb);
        }
    }
}
