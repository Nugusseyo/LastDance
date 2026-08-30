using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Works.CJW.Scripts.MapSystems.Editor
{
    /// <summary>
    /// MapDataSo의 인스펙터. 좌표를 여기서 편집하지는 않는다.
    ///
    /// 이 에셋은 좌표를 들고 있지 않고 씬의 MapPosition이 진실이기 때문에,
    /// 인스펙터가 하는 일은 지금 맵에 무엇이 있는지 보여주고 빠뜨린 것을 잡아내는 것이다.
    /// 편집 모드에서는 씬을 훑어 보여주고, 플레이 중에는 실제 등록 상태를 그대로 비춘다.
    /// </summary>
    [CustomEditor(typeof(MapDataSo))]
    public class MapDataInspector : UnityEditor.Editor
    {
        /// <summary>
        /// 틀 자산은 경로를 박아두지 않고 이름으로 찾는다.
        /// 폴더를 옮기는 순간 하드코딩된 경로는 조용히 죽고, 그 사실을 인스펙터를 열어봐야 알게 된다.
        /// </summary>
        private const string AssetName = "MapDataInspector";

        private MapDataSo _mapData;

        private Label _modeLabel;
        private Label _summaryLabel;
        private Label _issueTitle;
        private VisualElement _issueSection;
        private VisualElement _issueList;
        private VisualElement _typeList;
        private EnumField _createTypeField;
        private Toggle _rentableToggle;

        private readonly List<MapPosition> _buffer = new();

        public override VisualElement CreateInspectorGUI()
        {
            _mapData = (MapDataSo)target;

            VisualElement root = new();

            VisualTreeAsset tree = FindAsset<VisualTreeAsset>(".uxml");
            if (tree == null)
            {
                root.Add(new HelpBox($"{AssetName}.uxml을 찾지 못했습니다.", HelpBoxMessageType.Error));
                return root;
            }

            tree.CloneTree(root);

            StyleSheet style = FindAsset<StyleSheet>(".uss");
            if (style != null)
            {
                root.styleSheets.Add(style);
            }

            _modeLabel = root.Q<Label>("mode-label");
            _summaryLabel = root.Q<Label>("summary-label");
            _issueSection = root.Q<VisualElement>("issue-section");
            _issueTitle = root.Q<Label>("issue-title");
            _issueList = root.Q<VisualElement>("issue-list");
            _typeList = root.Q<VisualElement>("type-list");
            _createTypeField = root.Q<EnumField>("create-type-field");
            _rentableToggle = root.Q<Toggle>("rentable-toggle");

            _issueTitle.text = "확인이 필요합니다";

            _createTypeField.label = "새 지점";
            _createTypeField.Init(MapPointType.ParkingSlot);
            _createTypeField.RegisterValueChangedCallback(_ => SyncRentableToggle());

            _rentableToggle.label = "대여 가능";
            _rentableToggle.tooltip = "한 번에 한 명만 쓸 수 있는 지점이면 켠다. 주차 자리는 전용 클래스가 있어 항상 켜진다.";
            SyncRentableToggle();

            Button refresh = root.Q<Button>("refresh-button");
            refresh.text = "새로고침";
            refresh.clicked += Refresh;

            Button selectAll = root.Q<Button>("select-all-button");
            selectAll.text = "씬에서 모두 선택";
            selectAll.clicked += SelectAllInScene;

            Button create = root.Q<Button>("create-button");
            create.text = "씬에 만들기";
            create.clicked += CreatePointInScene;

            // 플레이 중에는 점유 상태가 계속 바뀐다. 이벤트로 못 잡는 변화까지 훑도록 짧게 폴링한다.
            root.schedule.Execute(() =>
            {
                if (Application.isPlaying)
                {
                    Refresh();
                }
            }).Every(500);

            _mapData.Changed += Refresh;
            EditorApplication.hierarchyChanged += Refresh;

            Refresh();

            return root;
        }

        /// <summary>이름과 확장자로 틀 자산을 찾는다. Editor 폴더를 옮겨도 따라온다.</summary>
        private static T FindAsset<T>(string extension) where T : UnityEngine.Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"{AssetName} t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(AssetName + extension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }


        private void OnDisable()
        {
            if (_mapData != null)
            {
                _mapData.Changed -= Refresh;
            }

            EditorApplication.hierarchyChanged -= Refresh;
        }

        private void SyncRentableToggle()
        {
            // 주차 자리는 ParkingSlot이라는 전용 클래스가 대여를 이미 갖고 있다.
            bool isParkingSlot = (MapPointType)_createTypeField.value == MapPointType.ParkingSlot;

            _rentableToggle.SetEnabled(!isParkingSlot);

            if (isParkingSlot)
            {
                _rentableToggle.SetValueWithoutNotify(true);
            }
        }

        private void Refresh()
        {
            if (_typeList == null || _mapData == null)
            {
                return;
            }

            bool playing = Application.isPlaying;

            _modeLabel.text = playing ? "플레이 중" : "편집 모드";
            _typeList.Clear();

            int total = 0;
            int available = 0;

            foreach (MapPointType type in Enum.GetValues(typeof(MapPointType)))
            {
                if (type == MapPointType.None)
                {
                    continue;
                }

                CollectPoints(type, playing);
                if (_buffer.Count == 0)
                {
                    continue;
                }

                int free = 0;
                for (int i = 0; i < _buffer.Count; i++)
                {
                    if (_buffer[i] != null && _buffer[i].IsAvailable)
                    {
                        free++;
                    }
                }

                total += _buffer.Count;
                available += free;

                _typeList.Add(BuildTypeGroup(type, free));
            }

            _summaryLabel.text = total == 0
                ? "등록된 지점이 없습니다"
                : $"지점 {total}개 · 사용 가능 {available}개";

            if (total == 0)
            {
                Label empty = new(playing
                    ? "플레이 중인데 등록된 지점이 없습니다. MapPosition에 이 에셋이 물려 있는지 확인하세요."
                    : "씬에 이 맵을 참조하는 MapPosition이 없습니다.");
                empty.AddToClassList("map-empty");
                _typeList.Add(empty);
            }

            RefreshIssues();
        }

        private VisualElement BuildTypeGroup(MapPointType type, int free)
        {
            Foldout foldout = new()
            {
                text = $"{ToLabel(type)}  ({free}/{_buffer.Count})",
                value = true
            };

            for (int i = 0; i < _buffer.Count; i++)
            {
                MapPosition point = _buffer[i];
                if (point == null)
                {
                    continue;
                }

                foldout.Add(BuildRow(point));
            }

            return foldout;
        }

        private VisualElement BuildRow(MapPosition point)
        {
            VisualElement row = new();
            row.AddToClassList("map-row");

            Label name = new(point.name);
            name.AddToClassList("map-row__name");
            row.Add(name);

            Vector3 position = point.Position;
            Label coordinate = new($"({position.x:0.#}, {position.z:0.#})");
            coordinate.AddToClassList("map-row__pos");
            row.Add(coordinate);

            if (point is RentableMapPosition rentable)
            {
                Label badge = new(rentable.IsOccupied ? "사용 중" : "빈자리");
                badge.AddToClassList("map-badge");
                badge.AddToClassList(rentable.IsOccupied ? "map-badge--busy" : "map-badge--free");
                row.Add(badge);
            }

            // 목록에서 바로 씬 오브젝트로 건너뛸 수 있어야 배치 작업이 편해진다.
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (point == null)
                {
                    return;
                }

                EditorGUIUtility.PingObject(point.gameObject);

                if (evt.clickCount > 1)
                {
                    Selection.activeGameObject = point.gameObject;
                }
            });

            return row;
        }

        private void RefreshIssues()
        {
            _issueList.Clear();

            int count = 0;

            MapPosition[] all = FindObjectsByType<MapPosition>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                MapPosition point = all[i];

                if (point.MapData == null)
                {
                    AddIssue(point, "MapData가 비어 있어 어느 맵에도 등록되지 않습니다.");
                    count++;
                    continue;
                }

                if (point.MapData != _mapData)
                {
                    continue;
                }

                if (point.Type == MapPointType.None)
                {
                    AddIssue(point, "종류가 None이라 등록되지 않습니다.");
                    count++;
                }
            }

            _issueSection.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddIssue(MapPosition point, string message)
        {
            VisualElement row = new();
            row.AddToClassList("map-issue");

            Label label = new($"{point.name} — {message}");
            label.AddToClassList("map-issue__text");
            row.Add(label);

            Button ping = new(() => EditorGUIUtility.PingObject(point.gameObject)) { text = "보기" };
            row.Add(ping);

            _issueList.Add(row);
        }

        private void CollectPoints(MapPointType type, bool playing)
        {
            _buffer.Clear();

            if (playing)
            {
                // 플레이 중에는 레지스트리가 진실이다. 씬을 훑으면 아직 등록 전인 것까지 섞인다.
                IReadOnlyList<MapPosition> registered = _mapData.GetAll(type);
                for (int i = 0; i < registered.Count; i++)
                {
                    _buffer.Add(registered[i]);
                }

                return;
            }

            MapPosition[] all = FindObjectsByType<MapPosition>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                MapPosition point = all[i];
                if (point.MapData == _mapData && point.Type == type)
                {
                    _buffer.Add(point);
                }
            }
        }

        private void SelectAllInScene()
        {
            List<GameObject> targets = new();

            MapPosition[] all = FindObjectsByType<MapPosition>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].MapData == _mapData)
                {
                    targets.Add(all[i].gameObject);
                }
            }

            Selection.objects = targets.ToArray();
        }

        private void CreatePointInScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[MapData] 플레이 중에 만든 지점은 플레이를 멈추면 사라집니다.");
            }

            MapPointType type = (MapPointType)_createTypeField.value;
            if (type == MapPointType.None)
            {
                Debug.LogWarning("[MapData] 종류를 골라야 지점을 만들 수 있습니다.");
                return;
            }

            GameObject go = new($"{type}Point");

            MapPosition point;
            if (type == MapPointType.ParkingSlot)
            {
                // 주차 자리는 종류가 코드로 고정된 전용 클래스가 있다.
                point = go.AddComponent<ParkingSlot>();
            }
            else if (_rentableToggle.value)
            {
                point = go.AddComponent<RentableMapPosition>();
            }
            else
            {
                point = go.AddComponent<MapPosition>();
            }

            SerializedObject so = new(point);
            so.FindProperty("mapData").objectReferenceValue = _mapData;

            SerializedProperty typeProperty = so.FindProperty("type");
            if (typeProperty != null)
            {
                typeProperty.intValue = (int)type;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // 씬 뷰가 보고 있는 곳에 놓아야 만들자마자 찾을 수 있다.
            SceneView view = SceneView.lastActiveSceneView;
            go.transform.position = view != null ? view.pivot : Vector3.zero;

            Undo.RegisterCreatedObjectUndo(go, "Create Map Position");
            Selection.activeGameObject = go;

            if (view != null)
            {
                view.FrameSelected();
            }

            Refresh();
        }

        private static string ToLabel(MapPointType type)
        {
            return type switch
            {
                MapPointType.ParkingSlot => "주차 자리",
                MapPointType.ShopEntrance => "가게 입구",
                MapPointType.Counter => "카운터",
                MapPointType.Table => "테이블",
                MapPointType.Exit => "퇴장 지점",
                _ => type.ToString()
            };
        }
    }
}
