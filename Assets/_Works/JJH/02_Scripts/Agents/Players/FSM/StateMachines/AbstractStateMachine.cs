using _Works.JJH._02_Scripts.Agents.Players.FSM.States;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM
{
    public abstract class AbstractStateMachine
    {
        protected AbstractState CurrentState { get; private set; }

        public void ChangeState(AbstractState nextState)
        {
            if (CurrentState == nextState)
                return;

            CurrentState?.Exit();
            CurrentState = nextState;
            CurrentState.Enter();
        }

        public void Update()
        {
            CurrentState?.Update();
        }
    }
}