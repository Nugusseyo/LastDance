using System;

namespace _Works.JYG._Scripts.Util
{
    public static class TextConvert
    {
        public static string Get(int value, string unit = "")
        {
            float absValue = Math.Abs(value);

            if (absValue >= 1_000_000_000)  // _은 숫자에 아무런 영향을 미치지 않는다. 가독성용 C#문법
                return $"{(value / 1_000_000_000f):0.##}B{unit}";
            if (absValue >= 1_000_000)
                return $"{(value / 1_000_000f):0.##}M{unit}";
            if (absValue >= 1_000)
                return $"{(value / 1_000f):0.##}K{unit}";

            return $"{value}{unit}";
        }
    }
}