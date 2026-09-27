using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public interface IMover
    {
        float MoveSpeed { get; set; }
        float RunSpeed { get; set; }

        void Move(Vector3 direction);
        void Run(Vector3 direction);
        void Stop();
    }
}