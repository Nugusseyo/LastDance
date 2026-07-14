using System;
using System.Collections.Generic;
using Agents.FSM;
using JJH._02_Scripts.Agents;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.FSM
{
    public abstract class StateMachine
    {
        public int LayerIndex { get; }
        public AgentState CurrentState { get; private set; }

        private readonly Dictionary<int, AgentState> _states;

        public StateMachine(Agent agent, StateListSO listSO)
        {
            LayerIndex = listSO.layer;
            _states = BuildStates(agent, listSO.states);
        }

        public void ChangeState(int stateIndex, float transitionDuration = 0.1f)
        {
            AgentState next = _states.GetValueOrDefault(stateIndex);
            Debug.Assert(next != null, $"[Layer {LayerIndex}] 존재하지 않는 상태 인덱스 : {stateIndex}");
            if (next == null) return;

            CurrentState?.Exit();
            CurrentState = next;
            CurrentState.Enter(transitionDuration, LayerIndex);
        }

        public virtual void UpdateMachine() => CurrentState?.Update();

        private static Dictionary<int, AgentState> BuildStates(Agent agent, StateSO[] stateList)
        {
            Dictionary<int, AgentState> states = new(stateList.Length);
            foreach (StateSO so in stateList)
            {
                Type type = Type.GetType(so.className); // 클래스 이름으로 타입을 찾음
                Debug.Assert(type != null, $"State class {so.className} not found.");

                int paramHash = so.stateParam == null ? 0 : so.stateParam.HashValue;

                // 런타임에 생성자를 호출해 상태 인스턴스를 만든다.
                AgentState state = (AgentState)Activator.CreateInstance(type, agent, paramHash);

                // 생성되는 enum 값이 곧 assetIndex 이므로 이를 키로 사용
                states.Add(so.assetIndex, state);
            }

            return states;
        }
    }
}
