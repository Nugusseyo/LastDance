using UnityEngine;

namespace DevLib.BattleSystem.Feedback
{
    public abstract class AbstractFeedback : MonoBehaviour
    {
        public abstract void PlayFeedback();
        public virtual void StopFeedback() { }
    }
}