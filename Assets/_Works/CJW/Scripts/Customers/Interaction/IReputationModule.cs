namespace _Works.CJW.Scripts.Customers.Interaction
{
    public interface IReputationModule
    {
        float Reputation { get; }
        float MaxReputation { get; }
        void InitializeReputation();
    }
}