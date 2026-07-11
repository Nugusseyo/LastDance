using Unity.Behavior;

namespace JJH._02_Scripts.Agents.Enemies.BT
{
    [BlackboardEnum]
    public enum EnemyState
    {
        IDLE = 0,
        MOVE = 1,
        COMBAT = 2,
        STUNNED = 3,
        DEAD = 4
    }
}