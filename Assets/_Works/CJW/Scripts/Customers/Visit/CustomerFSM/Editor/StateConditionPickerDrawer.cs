using UnityEditor;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.Editor
{
    /// <summary>행동 발동 조건에 종류 드롭다운을 붙인다. 실제로 그리는 일은 <see cref="GameEditor.ManagedReferencePickerDrawer"/>가 하므로 여기서는 대상 타입만 지정한다.</summary>
    [CustomPropertyDrawer(typeof(StateCondition), true)]
    public sealed class StateConditionPickerDrawer : GameEditor.ManagedReferencePickerDrawer
    {
    }
}
