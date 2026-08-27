namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class AttackSkill : AbstractPlayerAttack
    {
        public override void Attack()
        {
            if (_weapon == null || _weapon.CurrentWeaponObject == null ||
                _weapon.CurrentWeaponData == null)
                return;


        }
    }
}