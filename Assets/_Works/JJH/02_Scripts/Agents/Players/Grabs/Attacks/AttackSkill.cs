namespace _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks
{
    public class AttackSkill : AbstractPlayerAttack
    {
        public override void Attack()
        {
            if (player.Grab == null || player.Grab.CurrentItem == null)
                return;


        }
    }
}