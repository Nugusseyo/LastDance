using JJH._02_Scripts.Agents.Enemies.BT;

namespace JJH._02_Scripts.Agents.Enemies
{
    public class CryingEngelEnemy : AbstractEnemy, IDetectable
    {
        public void IsDetected(bool isDetected)
        {
            if (isDetected)
                BehaviorAgent.SetVariableValue("State", EnemyState.STUNNED);
            else
                BehaviorAgent.SetVariableValue("State", EnemyState.COMBAT);
        }
    }
}