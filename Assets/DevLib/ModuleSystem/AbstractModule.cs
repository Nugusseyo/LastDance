using UnityEngine;

namespace DevLib.ModuleSystem
{
    public abstract class AbstractModule : MonoBehaviour, IModule
    {
        protected ModuleOwner _owner;
        public virtual void Initialize(ModuleOwner owner)
        {
            _owner = owner;
        }
    }
}