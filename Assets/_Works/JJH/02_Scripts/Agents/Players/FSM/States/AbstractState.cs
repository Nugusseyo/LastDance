namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States
{
    public abstract class AbstractState
    {
        protected Player Player { get; private set; }
        protected AbstractStateMachine StateMachine { get; private set; }

        protected AbstractState(Player player, AbstractStateMachine stateMachine)
        {
            Player = player;
            StateMachine = stateMachine;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }
}