using System;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public class PlayerHideModule : MonoBehaviour, IModule
    {
        public bool IsHiding { get; private set; }

        private PlayerController _player;

        public void Initialize(ModuleOwner owner)
        {
            
        }
    }
}