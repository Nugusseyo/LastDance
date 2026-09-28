namespace _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks
{
    public interface IPlayerAttackSkill
    {
        /// <summary>지금 고른 공격을 쓸 수 있는지(쿨타임 등). 못 쓰는데 공격 상태로 들어가면 휘두르기만 하고 판정이 없다.</summary>
        bool CanAttack { get; }

        void Attack();
        void ChangeCurrentAttack<T>() where T : AbstractPlayerAttack;
    }
}
