using System;
using System.Collections.Generic;
using UnityEditor;

namespace _Works.CJW.Scripts.Customers.Data.Editor
{
    /// <summary>서로 다른 <see cref="SerializedObject"/> 사이로 값을 옮긴다. <c>SerializedObject.CopyFromSerializedProperty</c>는 <c>[SerializeReference]</c>를 제대로 옮기지 못해 쓰지 않고, managed reference만 따로 다룬다.</summary>
    internal static class SerializedPropertyCopier
    {
        /// <summary>대상의 같은 경로로 값을 통째로 옮긴다. 대상에 없는 경로는 조용히 넘어간다.</summary>
        public static void Copy(SerializedProperty src, SerializedProperty dst)
        {
            if (src == null || dst == null)
            {
                return;
            }

            // 배열은 propertyType이 Generic이라 아래 switch보다 먼저 걸러야 한다.
            if (src.isArray && src.propertyType == SerializedPropertyType.Generic)
            {
                dst.arraySize = src.arraySize;

                for (int i = 0; i < src.arraySize; i++)
                {
                    Copy(src.GetArrayElementAtIndex(i), dst.GetArrayElementAtIndex(i));
                }

                return;
            }

            switch (src.propertyType)
            {
                case SerializedPropertyType.ManagedReference:
                    CopyManagedReference(src, dst);
                    return;

                case SerializedPropertyType.Generic:
                    // PhaseSequence 같은 [Serializable] 클래스. 자식마다 다시 내려간다.
                    CopyChildren(src, dst);
                    return;

                default:
                    // enum·float·ObjectReference·Vector3 등. boxedValue는 enum을 이름 순서가 아닌
                    // 원시 값으로 다루므로 VisitPhase처럼 번호가 비어 있는 enum도 안전하다.
                    dst.boxedValue = src.boxedValue;
                    return;
            }
        }

        /// <summary><c>[SerializeReference]</c> 한 칸. 타입만 먼저 세우고 값은 자식 순회로 채운다.</summary>
        private static void CopyManagedReference(SerializedProperty src, SerializedProperty dst)
        {
            object source = src.managedReferenceValue;

            if (source == null)
            {
                dst.managedReferenceValue = null;
                return;
            }

            // JSON으로 통째 복사하지 않는 이유: EditorJsonUtility는 중첩된 [SerializeReference]를
            // 보존하지 못한다. 행동 안에 든 조건처럼 한 겹 더 들어간 참조가 조용히 null이 된다.
            // 타입만 세워 두고 나머지는 이 함수가 재귀로 채우면 몇 겹이든 따라온다.
            dst.managedReferenceValue = Activator.CreateInstance(source.GetType());

            CopyChildren(src, dst);
        }

        private static void CopyChildren(SerializedProperty src, SerializedProperty dst)
        {
            foreach (SerializedProperty child in Children(src))
            {
                Copy(child, dst.FindPropertyRelative(child.name));
            }
        }

        /// <summary>직속 자식 프로퍼티들. 순회하는 동안 반복자가 움직이므로 복사해서 돌려준다.</summary>
        private static IEnumerable<SerializedProperty> Children(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                yield return iterator.Copy();
            }
        }
    }
}
