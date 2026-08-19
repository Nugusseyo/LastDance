using System;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public interface ICleaner
    {
        event Action OnCleanerUsed;
        event Action OnCleanerEnd;
        
        CleanerDataSO CleanerData { get; }
        float NormalizedCooldown { get; }
        public bool IsUsing { get; }
        void InitializeCleaner(ICleanerModule cleanerModule);
        bool CanUseCleaner();
        void UseCleaner();
        void EndCleaner();
    }
}