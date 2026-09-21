using UnityEditor;

namespace _Works.CJW.Scripts.Customers.Visit.Editor
{
    /// <summary>방문 단계 연출에 종류 드롭다운을 붙인다. 실제로 그리는 일은 <see cref="GameEditor.ManagedReferencePickerDrawer"/>가 하므로 여기서는 대상 타입만 지정한다.</summary>
    [CustomPropertyDrawer(typeof(VisitState), true)]
    public sealed class VisitStatePickerDrawer : GameEditor.ManagedReferencePickerDrawer
    {
    }
}
