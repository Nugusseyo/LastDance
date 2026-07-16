using System;
using JJH._02_Scripts.Agents;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.FSM
{
    public class LayeredStateMachine
    {
        private readonly StateMachine[] _layers;

        public LayeredStateMachine(StateMachineSO[] layers, Agent owner)
        {
            int layerCount = 0;
            foreach (var so in layers)
            {
                layerCount = Mathf.Max(layerCount, so.layer + 1);
            }

            _layers = new StateMachine[layerCount];
            foreach (var so in layers)
            {
                Debug.Assert(_layers[so.layer] == null, $"레이어 {so.layer} 가 중복 정의됨");
                Type t = Type.GetType(so.className);
                _layers[so.layer] = (StateMachine)Activator.CreateInstance(t, owner, so.stateList);
            }
        }

        public void ChangeState(int layer, int stateIndex, float transitionDuration = 0.1f)
        {
            Debug.Assert(layer < _layers.Length, $"존재하지 않는 레이어 : {layer}");
            if (layer >= _layers.Length) return;

            _layers[layer]?.ChangeState(stateIndex, transitionDuration);
        }

        public void Update()
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                Debug.Assert(_layers[i] != null, $"{_layers}번째 레이어가 존재하지 않음");
                _layers[i]?.UpdateMachine();
            }
        }
    }
}
