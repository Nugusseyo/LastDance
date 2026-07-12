namespace _Works.CJW.Scripts.Player.FSM
{
    public enum PlayerLayers
    {
        Base = 0,   
        Upper = 1,  
    }

    public enum BaseStates
    {
        Idle = 0,
        Move = 1,
    }

    public enum UpperStates
    {
        Combat = 0,
        Attack = 1,
    }
}
