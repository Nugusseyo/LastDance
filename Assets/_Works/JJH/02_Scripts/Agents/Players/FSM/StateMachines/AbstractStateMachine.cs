using _Works.JJH._02_Scripts.Agents.Players.FSM.States;
using System.Collections.Generic;
using System.Linq;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM
{
    public abstract class AbstractStateMachine
    {
        private List<AbstractState> _states = new();
        private AbstractState _currentState;

        public void SetState(params AbstractState[] states)
        {
            _states.AddRange(states);
        }

        public void ChangeState<T>() where T : AbstractState
        {
            T nextState = GetState<T>();

            if (_currentState == nextState)
                return;

            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter();
        }

        public bool IsState<T>() where T : AbstractState
        {
            return _currentState is T;
        }

        private T GetState<T>() where T : AbstractState
        {
            return _states.OfType<T>().FirstOrDefault();
        }

        public void Update()
        {
            _currentState?.Update();
        }
    }
}