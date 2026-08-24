namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States
{
    public abstract class AbstractState
    {
        protected Agent Agent { get; private set; }
        protected AbstractStateMachine StateMachine { get; private set; }
        protected PlayerInputSO Input { get; private set; }

        protected AbstractState(Agent agent, AbstractStateMachine stateMachine, PlayerInputSO input)
        {
            Agent = agent;
            StateMachine = stateMachine;
            Input = input;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }
}