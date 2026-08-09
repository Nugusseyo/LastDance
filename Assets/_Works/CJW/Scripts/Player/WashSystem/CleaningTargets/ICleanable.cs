using _Works.CJW.Scripts.Player.Weapons;

namespace _Works.CJW.Scripts.Player.WashSystem.CleaningTargets
{
    public interface ICleanable
    {
        void Clean(ICleaner cleaner);
    }
}