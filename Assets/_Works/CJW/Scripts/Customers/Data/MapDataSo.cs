using System.Collections.Generic;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>
    /// 맵이 제공하는 자리들의 만남 지점. 좌표를 저장하지는 않는다.
    /// 실제 위치는 씬의 ParkingSlot이 갖고 있고, 이 에셋은 "누가 있고 누가 비었는지"만 안다.
    /// 쓰는 쪽은 씬에 무엇이 있는지 몰라도 이 에셋 하나만 참조하면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "JW/MapData", menuName = "JW/Map Data", order = 0)]
    public class MapDataSo : ScriptableObject
    {
        private readonly List<ParkingSlot> _parkingSlots = new();

        public int ParkingSlotCount => _parkingSlots.Count;

        /// <summary>비어 있는 자리가 하나라도 있는지. 차를 꺼내기 전에 먼저 확인하는 용도.</summary>
        public bool HasFreeParkingSlot
        {
            get
            {
                for (int i = 0; i < _parkingSlots.Count; i++)
                {
                    if (!_parkingSlots[i].IsOccupied)
                        return true;
                }

                return false;
            }
        }

        /// <summary>ParkingSlot이 켜질 때 스스로 부른다.</summary>
        public void AddParkingSlot(ParkingSlot slot)
        {
            if (slot == null || _parkingSlots.Contains(slot))
                return;

            slot.SetOccupied(false);
            _parkingSlots.Add(slot);
        }

        /// <summary>ParkingSlot이 꺼질 때 스스로 부른다.</summary>
        public void RemoveParkingSlot(ParkingSlot slot)
        {
            if (slot == null)
                return;

            slot.SetOccupied(false);
            _parkingSlots.Remove(slot);
        }

        /// <summary>빈 자리를 하나 빌린다. 다 찼으면 false.</summary>
        public bool TryRentParkingSlot(out ParkingSlot slot)
        {
            for (int i = 0; i < _parkingSlots.Count; i++)
            {
                ParkingSlot candidate = _parkingSlots[i];
                if (candidate == null || candidate.IsOccupied)
                    continue;

                candidate.SetOccupied(true);
                slot = candidate;
                return true;
            }

            slot = null;
            return false;
        }

        /// <summary>빌린 자리를 돌려준다. 빌린 쪽이 반드시 짝을 맞춰 부른다.</summary>
        public void ReleaseParkingSlot(ParkingSlot slot)
        {
            if (slot == null)
                return;

            slot.SetOccupied(false);
        }

        // ScriptableObject의 런타임 상태는 에디터에서 플레이를 멈춰도 남는다.
        // 죽은 자리가 목록에 남지 않도록 로드/언로드 시점에 비운다.
        private void OnEnable() => _parkingSlots.Clear();
        private void OnDisable() => _parkingSlots.Clear();
    }
}
