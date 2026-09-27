using System;
using UnityEngine;

namespace Resources.DataBase.Review_Data
{
    [Serializable]
    public class ReviewData
    {
        public int index;
        public ReviewType type;
        public string contentUp;
        public string contentDown;
        public string contentLate;
    }

    public enum ReviewType
    {
        None,
        Good,
        Bad,
        Late
    }
}
