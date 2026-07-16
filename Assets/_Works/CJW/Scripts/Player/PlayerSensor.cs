using System;
using JJH._02_Scripts.Agents;
using Unity.VisualScripting;
using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public class PlayerSensor : AgentSensor
    {
        [SerializeField] private float detectAngle;
        [SerializeField] private float detectRadius;
        private IFlashLightModule _flashLightModule;

        [ContextMenu("MatchViewAngle")]
        private void MatchViewAngle()
        {
            _debugViewAngle = detectAngle;
            _debugViewRadius = detectRadius;
        }
        
        private void Update()
        {
            if (_flashLightModule.IsActive)
            {
                // if(IsTargetInSight(detectAngle, 10, ))
            }
        }
    }
}