using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevLib.ModuleSystem
{
    public class ModuleOwner : MonoBehaviour
    {
        protected Dictionary<Type, Module> _moduleDict;

        protected virtual void Awake()
        {
            _moduleDict = GetComponentsInChildren<Module>().ToDictionary(module => module.GetType());
            InitializeModules();
            AfterInitializeModules();
        }
        protected virtual void Start(){}
        
        protected virtual void InitializeModules()
        {
            foreach (Module module in _moduleDict.Values)
            {
                module.Initialize(this);
            }
        }
        
        protected virtual void AfterInitializeModules()
        {
            foreach (IAfterInitModule module in _moduleDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInit();
            }
        }
        
        public T GetModule<T>() 
        {
            if (_moduleDict.TryGetValue(typeof(T), out Module module))
            {
                return (T)(object)module;
            }

            Module findModule = _moduleDict.Values.FirstOrDefault(moduleType => moduleType is T);
            
            if(findModule is T castedModule)
                return castedModule;

            return default;
        }

    }
}