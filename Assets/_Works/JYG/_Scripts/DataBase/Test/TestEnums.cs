using System;

namespace _Works.JYG._Scripts.DataBase.Test
{
    [Serializable]
    public class TestEnums
    {
        public int test;
        public TestCharacterEnums testEnum;
    }

    public enum TestCharacterEnums
    {
        GoodChar,
        BadChar,
        NormalChar
    }
}