using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevLib.BattleSystem.Feedback
{
    public class FeedbackPlayer : MonoBehaviour
    {
        private List<AbstractFeedback> _feedbacks;

        private void Awake()
        {
            _feedbacks = GetComponents<AbstractFeedback>().ToList();
        }

        public void PlayAllFeedbacks()
        {
            StopAllFeedbacks();
            _feedbacks.ForEach(feedback => feedback.PlayFeedback());
        }

        private void StopAllFeedbacks()
        {
            _feedbacks.ForEach(feedback => feedback.StopFeedback());
        }
    }
}