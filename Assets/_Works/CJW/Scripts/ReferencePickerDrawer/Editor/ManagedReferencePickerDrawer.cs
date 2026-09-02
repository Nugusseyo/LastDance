using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameEditor
{
    /// <summary>
    /// <c>[SerializeReference]</c> 필드에 <b>종류를 고르는 드롭다운</b>을 붙인다.
    ///
    /// 에디터 버전과 인스펙터 구현에 따라 내장 타입 피커가 나타나지 않아, <c>+</c>를 눌러도
    /// 리스트에 null(<c>rid: -2</c>)만 들어가는 경우가 있다. 조각을 쌓아서 카드와 적을 만드는
    /// 이 프로젝트에서는 그 드롭다운이 곧 작업 도구이므로, 내장 UI에 기대지 않고 직접 그린다.
    ///
    /// 모양은 <c>Editor/UI/ManagedReferencePicker.uxml</c>과 <c>.uss</c>가 갖는다. IMGUI처럼
    /// 좌표를 계산해 그리지 않으므로 <b>줄 높이를 직접 재는 코드가 없다</b> — 조각마다 칸 수가 다르고
    /// 접힘 상태까지 얽히는 자리라, 높이 계산은 원래 어긋나기 가장 쉬운 부분이다.
    ///
    /// 고를 수 있는 후보는 필드에 선언된 타입에서 파생된 것 중 <see cref="SerializableAttribute"/>가
    /// 붙은 구체 클래스뿐이다. 선언 타입은 <c>managedReferenceFieldTypename</c>이 알려 주므로,
    /// 새 조각을 만들어도 이 드로어는 손대지 않는다.
    ///
    /// 인터페이스로 선언된 필드에는 붙일 수 없다 — <see cref="CustomPropertyDrawer"/>가 클래스만
    /// 대상으로 받기 때문이다. 조각의 부모를 추상 클래스로 두는 또 하나의 이유다.
    /// </summary>
    public abstract class ManagedReferencePickerDrawer : PropertyDrawer
    {
        private const string TemplateName = "ManagedReferencePicker";
        private const string EmptyButtonClass = "mref__type-button--empty";

        /// <summary>틀은 모든 조각이 함께 쓰므로 한 번만 찾는다.</summary>
        private static VisualTreeAsset _template;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualTreeAsset template = LoadTemplate();
            if (template == null)
            {
                // 틀을 못 찾아도 인스펙터가 빈 줄만 남기지 않도록 기본 그리기로 물러선다.
                return new PropertyField(property);
            }

            VisualElement root = template.CloneTree();

            var foldout = root.Q<Foldout>("foldout");
            var label = root.Q<Label>("label");
            var button = root.Q<Button>("type-button");
            var content = root.Q<VisualElement>("content");

            foldout.text = property.displayName;
            label.text = property.displayName;

            foldout.RegisterValueChangedCallback(evt =>
            {
                // 조각 안에 또 다른 접힘이 있을 수 있으므로 내 토글이 보낸 것만 받는다.
                if (evt.target != foldout) return;

                property.isExpanded = evt.newValue;
                ApplyExpansion();
            });

            button.clicked += () => ShowTypeMenu(property, button);

            Rebuild();

            // 종류가 바뀌면 열려야 할 수치 칸도 통째로 달라진다.
            root.TrackPropertyValue(property, _ => Rebuild());

            return root;

            void Rebuild()
            {
                bool hasValue = HasValue(property);

                button.text = CurrentTypeName(property) ?? "종류 선택…";
                button.EnableInClassList(EmptyButtonClass, !hasValue); // property에 값이 없으면 버튼을 비활성화 스타일로 전환

                // 비어 있으면 접을 것이 없다. 화살표 대신 라벨만 남긴다.
                foldout.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.display = hasValue ? DisplayStyle.None : DisplayStyle.Flex;

                foldout.SetValueWithoutNotify(property.isExpanded);

                content.Clear();

                if (hasValue)
                {
                    foreach (SerializedProperty child in Children(property))
                    {
                        var field = new PropertyField(child);
                        field.BindProperty(child);
                        content.Add(field);
                    }
                }

                ApplyExpansion();
            }

            void ApplyExpansion()
            {
                bool visible = HasValue(property) && property.isExpanded;
                content.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static bool HasValue(SerializedProperty property) =>
            !string.IsNullOrEmpty(property.managedReferenceFullTypename);

        /// <summary>지금 담겨 있는 구체 타입의 이름. 비어 있으면 null이다.</summary>
        private static string CurrentTypeName(SerializedProperty property)
        {
            string full = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full)) return null;

            //가장 마지막에 있는 . 위치
            int split = full.LastIndexOf('.');
            //split이 없는 값이 아니면 ' . ' 이후의 문자를 shortName에 저장
            string shortName = split >= 0 ? full[(split + 1)..] : full;
            
            //유니티 전용 함수. 단어 사이에 공백을 넣어줌 DamageEffect -> Damage Effect
            return ObjectNames.NicifyVariableName(shortName);
        }

        private static void ShowTypeMenu(SerializedProperty property, VisualElement anchor)
        {
            // 클래스 타입 가져오기
            Type baseType = ResolveFieldType(property);
            if (baseType == null) return;

            // 메뉴 항목은 나중에 실행되므로 SerializedProperty를 그대로 붙잡지 않는다.
            // 그 사이에 인스펙터가 다시 그려지면 잡아 둔 참조가 무효가 된다.
            SerializedObject owner = property.serializedObject;
            string path = property.propertyPath;
            string current = property.managedReferenceFullTypename ?? string.Empty;

            var menu = new GenericDropdownMenu();
            menu.AddItem("None", string.IsNullOrEmpty(current), () => Assign(owner, path, null));
            menu.AddSeparator(string.Empty);

            foreach (Type type in GetSelectableTypes(baseType))
            {
                Type captured = type;
                bool selected = current.EndsWith(type.Name, StringComparison.Ordinal);

                menu.AddItem(ObjectNames.NicifyVariableName(type.Name), selected,
                             () => Assign(owner, path, captured));
            }

            // 버튼 바로 아래에 펼친다. 이름 있는 인자를 쓰지 않는 것은 에디터 버전에 따라
            // 세 번째 매개변수 이름이 다르기 때문이다.
            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Auto);
        }

        private static void Assign(SerializedObject owner, string path, Type type)
        {
            if (owner == null || owner.targetObject == null) return;

            owner.Update();

            SerializedProperty property = owner.FindProperty(path);
            if (property == null) return;

            property.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;

            // 새로 만든 조각의 수치 칸이 바로 보이도록 펼쳐 둔다.
            property.isExpanded = type != null;

            owner.ApplyModifiedProperties();
        }

        /// <summary>
        /// 필드에 선언된 타입. 리스트라면 요소 타입이 들어오므로 목록과 단일 필드를 구분할 필요가 없다.
        /// 형식은 "어셈블리 이름 + 공백 + 전체 이름"이다.
        /// </summary>
        private static Type ResolveFieldType(SerializedProperty property)
        {
            string declared = property.managedReferenceFieldTypename;
            // managedReferenceFieldTypename: 타겟 타입의 어셈블리 이름과 전체 이름을 합쳐서 반환.
            // 예: "Assembly-CSharp MyNamespace.MyClass"
            if (string.IsNullOrEmpty(declared)) return null;

            int space = declared.IndexOf(' ');
            if (space < 0) return null;

            string assembly = declared[..space]; // 공백 기준 앞의 문자열은 어셈블리
            string fullName = declared[(space + 1)..]; // 공백 한 칸 뒤는 클래스 이름

            return Type.GetType($"{fullName}, {assembly}"); 
            // 전체 이름, 어셈블리를 하면 어셈블리로 가서 찾아줌
        }

        /// <summary>
        /// 고를 수 있는 조각들. 추상 클래스와 <see cref="SerializableAttribute"/>가 없는 타입은
        /// 담을 수 없으므로 뺀다.
        /// </summary>
        private static List<Type> GetSelectableTypes(Type baseType)
        {
            var result = new List<Type>();
            
            // AppDomain.CurrentDomain.GetAssemblies를 사용하지 않는 이유:
            // TypeCache가 유니티 프로젝트 도메인을 로드할 때 미리 인덱싱해놔서 훨씬 빠름
            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericType) continue; // 추상클래스가 아니고 제네릭이 아니여야함
                if (!Attribute.IsDefined(type, typeof(SerializableAttribute))) continue; // SerializableAttribute라는 어트리뷰트를 가지고 있어야함
                
                result.Add(type);
            }

            //알파벳순
            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        /// <summary>담긴 조각의 직속 자식 프로퍼티들. 순회하는 동안 반복자가 움직이므로 복사해서 돌려준다.</summary>
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

        /// <summary>
        /// 틀 자산을 찾는다. 경로를 박아 두지 않고 이름으로 찾으므로 Editor 폴더를 옮겨도 따라온다.
        /// </summary>
        private static VisualTreeAsset LoadTemplate()
        {
            if (_template != null) return _template;

            foreach (string guid in AssetDatabase.FindAssets($"{TemplateName} t:VisualTreeAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith($"{TemplateName}.uxml", StringComparison.OrdinalIgnoreCase)) continue;

                _template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (_template != null) return _template;
            }

            Debug.LogError($"{TemplateName}.uxml을 찾지 못해 기본 인스펙터로 그립니다.");
            return null;
        }
    }
}
