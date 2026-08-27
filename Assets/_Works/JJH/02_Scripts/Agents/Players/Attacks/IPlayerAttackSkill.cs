namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public interface IPlayerAttackSkill
    {
        void Attack<T>() where T : AbstractPlayerAttack;
    }
}
