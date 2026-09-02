using _Works.JYG._Scripts.Util;

namespace _Works.JYG._Scripts.SaveSystem
{
    public interface ISavableData : ISerializableInterface //GameManager에서 싹 긁어와서, Initialize또는 Save 하는 것.
    {
        void InitializeData(string key);
        void SaveData(string key);
    }
}