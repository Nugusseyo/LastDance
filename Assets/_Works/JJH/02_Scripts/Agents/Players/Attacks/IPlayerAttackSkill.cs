namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public interface IPlayerAttackSkill
    {
        void Attack();
        void ChangeCurrentAttack<T>() where T : AbstractPlayerAttack;
    }
}
