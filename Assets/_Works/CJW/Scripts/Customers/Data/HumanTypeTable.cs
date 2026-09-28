using System.Collections.Generic;
using Resources.DataBase.Human_Data;

namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>HumanDB의 대사 index → 손님 타입(Good/Bad). 기획서 표의 INDEX마다 정상/진상이 HumanDB에 적혀 있으니,
    /// 평가(진상인지)와 말풍선은 프리팹의 HumanType이 아니라 이 표를 따른다. 표에 없는 index면 <c>false</c>.</summary>
    public static class HumanTypeTable
    {
        private const string Path = "DataBase/Human Data/HumanDB";

        private static Dictionary<int, HumanType> _types;

        public static bool TryGet(int index, out HumanType type)
        {
            _types ??= Load();
            return _types.TryGetValue(index, out type);
        }

        /// <summary>index가 표에 있으면 그 타입, 없으면 <paramref name="fallback"/>.</summary>
        public static HumanType Of(int index, HumanType fallback)
        {
            return TryGet(index, out HumanType type) ? type : fallback;
        }

        private static Dictionary<int, HumanType> Load()
        {
            var types = new Dictionary<int, HumanType>();
            HumanDB db = UnityEngine.Resources.Load<HumanDB>(Path);
            if (db == null || db.Sheet1 == null)
            {
                UnityEngine.Debug.LogError($"[{nameof(HumanTypeTable)}] Resources/{Path}를 찾지 못해 프리팹의 HumanType으로 평가합니다.");
                return types;
            }

            foreach (HumanData row in db.Sheet1)
            {
                // 한 index에 행이 여럿이면 첫 행을 쓴다(말풍선도 같은 index의 첫 행을 찾는다).
                if (row != null && row.type != HumanType.None)
                {
                    types.TryAdd(row.index, row.type);
                }
            }

            return types;
        }
    }
}
