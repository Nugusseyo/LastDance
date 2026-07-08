using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevLib.ModuleSystem
{
    public class ModuleOwner : MonoBehaviour
    {
        protected Dictionary<Type, IModule> _moduleDict = new Dictionary<Type, IModule>();

        protected virtual void Awake()
        {
            _moduleDict = GetComponentsInChildren<IModule>().ToDictionary(module => module.GetType());
            InitializeComponents();
            AfterInitializeComponents();
        }

        protected virtual void InitializeComponents()
        {
            foreach (IModule module in _moduleDict.Values)
            {
                module.Initialize(this);
            }
        }

        protected virtual void AfterInitializeComponents()
        {
            foreach (IAfterInitModule module in _moduleDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInitialize();
            }
        }

        public T GetModule<T>()
        {
            if (_moduleDict.TryGetValue(typeof(T), out IModule module))
            {
                return (T)module;
            }

            IModule findModule = _moduleDict.Values.FirstOrDefault(moduleType => moduleType is T);

            if (findModule != null && findModule is T castedModule)
                return castedModule;

            return default;
        }
    }
}