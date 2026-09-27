namespace _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks
{
    public interface IPlayerAttackSkill
    {
        void Attack();
        void ChangeCurrentAttack<T>() where T : AbstractPlayerAttack;
    }
}
