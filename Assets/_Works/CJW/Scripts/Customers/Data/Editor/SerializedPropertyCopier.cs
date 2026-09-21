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
                    // 같은 인스턴스를 넘기면 초안과 구운 프리팹이 한 객체를 공유한다.
                    // 창을 닫지 않고 손님을 연달아 만들 때 값이 서로 샌다.
                    dst.managedReferenceValue = DeepCopy(src.managedReferenceValue);
                    return;

                case SerializedPropertyType.Generic:
                    // PhaseSequence 같은 [Serializable] 클래스. 자식마다 다시 내려간다.
                    foreach (SerializedProperty child in Children(src))
                    {
                        Copy(child, dst.FindPropertyRelative(child.name));
                    }

                    return;

                default:
                    // enum·float·ObjectReference·Vector3 등. boxedValue는 enum을 이름 순서가 아닌
                    // 원시 값으로 다루므로 VisitPhase처럼 번호가 비어 있는 enum도 안전하다.
                    dst.boxedValue = src.boxedValue;
                    return;
            }
        }

        /// <summary><c>[Serializable]</c> 평범한 클래스의 복사본. <see cref="EditorJsonUtility"/>를 쓰는 이유는 <c>UnityEngine.Object</c> 참조를 세션 안에서 온전히 보존하기 때문이다.</summary>
        private static object DeepCopy(object source)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            object clone = Activator.CreateInstance(type);

            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), clone);

            return clone;
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
