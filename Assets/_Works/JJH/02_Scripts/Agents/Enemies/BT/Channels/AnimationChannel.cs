using DevLib.AnimatorSystem;
using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/AnimationChannel")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "AnimationChannel", message: "Set Animation to [Clip]", category: "Events", id: "e6a56e20d4259fa9aed1f326c2eda8e4")]
public sealed partial class AnimationChannel : EventChannel<HashDataSO> { }

