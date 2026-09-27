using System.Text;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.Data.Review
{
    public class ReviewItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI userName;
        [SerializeField] private TextMeshProUGUI content;
        [SerializeField] private TextMeshProUGUI star;
        
        private StringBuilder sb = new StringBuilder();

        public void SetReview(string userName, string content, int starCount)
        {
            this.userName.text = userName;
            this.content.text = content;
            sb.Clear();
            for (int i = 0; i < starCount; i++)
            {
                sb.Append("★");
            }
            for (int i = 0; i < 5 - starCount; i++)
            {
                sb.Append("☆");
            }

            star.text = sb.ToString();
        }
    }
}
