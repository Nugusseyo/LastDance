namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class AttackSkill : AbstractPlayerAttack
    {
        public override void Attack()
        {
            if (player.Grab == null || player.Grab.CurrentWeapon == null)
                return;


        }
    }
}