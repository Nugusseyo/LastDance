using System;
using System.Collections.Generic;

namespace Resources.DataBase.Human_Data
{
    [Serializable]
    public class HumanData
    {
        public HumanType type;      //손님의 타입
        public int index;           //손님 인덱스 (진상 손님 인덱스)
        public string contents1;
        public string contents2;
        public string contents3;
        public string contents4;
        public string contents5;
        public string contents6;
        public string contents7;
        public string contents8;
        public string contents9;
        public string contents10;

        public List<string> GetStrings()    //content들을 모두 리스트화 시킴.
        {
            List<string> contents = new List<string>()
            {
                contents1,  contents2, contents3, contents4, contents5, contents6, contents7, contents8, contents9, contents10
            };
            
            List<string> res = new List<string>();
            
            foreach (string content in contents)
            {
                if (!string.IsNullOrEmpty(content) && content.ToLower() != "null")
                {
                    res.Add(content);
                }
            }
            
            return res;
        }
    }

    public enum HumanType
    {
        None,
        Good,
        Bad
    }
}
