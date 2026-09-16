using System;

namespace _Works.JYG._Scripts.InteractableObj
{
    public interface IInteractableObject
    {
        void HandleInteract();
        void HandleDisconnect();
    }
}