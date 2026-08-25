using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.VisitActions
{
    public class AbstractVisitAction : MonoBehaviour, IModule
    {
        protected AbstractCustomer Customer;
        
        public void Initialize(ModuleOwner owner)
        {
            Customer = owner as AbstractCustomer;
        }
    }
}