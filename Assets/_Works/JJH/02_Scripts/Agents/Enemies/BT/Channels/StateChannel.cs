using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace JJH._02_Scripts.Agents.Enemies.BT.Channels
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Behavior/Event Channels/StateChannel")]
#endif
    [Serializable, GeneratePropertyBag]
    [EventChannelDescription(name: "StateChannel", message: "Set CurrentState To [State]", category: "Events", id: "8ffd28ee8528937290732974ee60d946")]
    public sealed partial class StateChannel : EventChannel<EnemyState> { }
}

