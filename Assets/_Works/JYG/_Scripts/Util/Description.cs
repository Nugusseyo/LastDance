using UnityEngine;

namespace _Works.JYG._Scripts.Util
{
    public abstract class Description : ScriptableObject
    {
        [SerializeField, TextArea] private string description;
    }
}
