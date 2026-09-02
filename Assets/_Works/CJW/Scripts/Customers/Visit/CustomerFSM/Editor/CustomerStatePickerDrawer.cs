using UnityEditor;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.Editor
{
    /// <summary>
    /// 손님 행동 상태에 종류 드롭다운을 붙인다.
    ///
    /// [SerializeReference] 배열은 요소를 늘려도 null만 들어가서 무엇을 넣을지 고를 수단이 없다.
    /// 그리는 일은 <see cref="GameEditor.ManagedReferencePickerDrawer"/>가 이미 하고 있으므로
    /// 여기서는 대상 타입만 지정한다.
    ///
    /// 새 상태 클래스를 만들 때 이 파일을 손댈 필요는 없다 —
    /// CustomerState를 상속하고 [Serializable]만 붙이면 목록에 나타난다.
    /// </summary>
    [CustomPropertyDrawer(typeof(CustomerState), true)]
    public sealed class CustomerStatePickerDrawer : GameEditor.ManagedReferencePickerDrawer
    {
    }
}
