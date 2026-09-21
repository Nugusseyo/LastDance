using System;
using DevLib.ObjectPool.Runtime;
using UnityEngine.UIElements;

namespace DevLib.ObjectPool.Editor
{
    public class PoolItemView
    {
        private Label _nameLabel;
        private Label _warningLabel;
        private Button _deleteBtn;
        private VisualElement _rootElement;

        public event Action<PoolItemView> OnDeleteEvent;
        public event Action<PoolItemView> OnSelectEvent;

        public string Name
        {
            get => _nameLabel.text;
            set => _nameLabel.text = value;
        }

        public PoolItemSO TargetItem { get; private set; }

        public bool IsActive
        {
            get => _rootElement.ClassListContains("active"); //active 클래스를 가지고 있다면 활성화 된것으로 판단.
            set => _rootElement.EnableInClassList("active", value); //active클래스를 넣거나 빼는것을 value값에 따라 
        }

        public bool IsEmpty
        {
            get => _warningLabel.ClassListContains("on"); //경고 라벨이 켜져있다면 비어있다고 보면된다.
            set => _warningLabel.EnableInClassList("on", value);
        }

        public PoolItemView(VisualElement rootElement, PoolItemSO targetItem)
        {
            TargetItem = targetItem;
            _rootElement = rootElement.Q("PoolItem"); //PoolItem에 클래스를 부여해야하니 그걸 가져온다.
            _nameLabel = _rootElement.Q<Label>("ItemName");
            _deleteBtn = _rootElement.Q<Button>("DeleteBtn");
            _warningLabel = _rootElement.Q<Label>("WarningLabel");
            
            _deleteBtn.RegisterCallback<ClickEvent>(evt =>
            {
                OnDeleteEvent?.Invoke(this);
                evt.StopPropagation(); //이벤트가 부모로 전파되는 것을 막아준다.(설명 필요)
            });
            
            _rootElement.RegisterCallback<ClickEvent>(evt =>
            {
                OnSelectEvent?.Invoke(this);
                evt.StopPropagation();
            });
        }

    }
}