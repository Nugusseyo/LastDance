using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>맵이 제공하는 지점들의 등록소. 좌표는 저장하지 않고 씬의 MapPosition이 가진 값을 그대로 쓰며, 이 에셋은 누가 있고 누가 비었는지만 안다.</summary>
    [CreateAssetMenu(fileName = "JW/MapData", menuName = "JW/Map Data", order = 0)]
    public class MapDataSo : ScriptableObject
    {
        private readonly Dictionary<MapPointType, List<MapPosition>> _points = new();

        /// <summary>등록 목록이나 점유 상태가 바뀔 때 발생. 에디터 인스펙터가 이걸 보고 다시 그린다.</summary>
        public event Action Changed;

        /// <summary>MapPosition이 켜질 때 스스로 부른다.</summary>
        public void Register(MapPosition point)
        {
            if (point == null || point.Type == MapPointType.None)
            {
                return;
            }

            List<MapPosition> list = GetOrCreate(point.Type);
            if (list.Contains(point))
            {
                return;
            }

            if (point is RentableMapPosition rentable)
            {
                rentable.SetOccupied(false);
            }

            list.Add(point);
            Changed?.Invoke();
        }

        /// <summary>MapPosition이 꺼질 때 스스로 부른다.</summary>
        public void Unregister(MapPosition point)
        {
            if (point == null || !_points.TryGetValue(point.Type, out List<MapPosition> list))
            {
                return;
            }

            if (list.Remove(point))
            {
                Changed?.Invoke();
            }
        }

        public int CountOf(MapPointType type)
            => _points.TryGetValue(type, out List<MapPosition> list) ? list.Count : 0;

        public int AvailableCountOf(MapPointType type)
        {
            if (!_points.TryGetValue(type, out List<MapPosition> list))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                MapPosition point = list[i];
                if (point != null && point.IsAvailable)
                {
                    count++;
                }
            }

            return count;
        }

        public bool HasAvailable(MapPointType type) => AvailableCountOf(type) > 0;

        /// <summary>에디터 인스펙터가 목록을 그릴 때 쓴다. 런타임 코드는 조회 메서드를 쓸 것.</summary>
        public IReadOnlyList<MapPosition> GetAll(MapPointType type)
            => _points.TryGetValue(type, out List<MapPosition> list) ? list : Array.Empty<MapPosition>();

        /// <summary>기준 위치에서 가장 가까운, 지금 쓸 수 있는 지점을 찾는다. 빌리지는 않으므로 입구처럼 여럿이 함께 쓰는 지점에 적합하다.</summary>
        public bool TryGetNearest(MapPointType type, Vector3 from, out MapPosition point)
        {
            point = FindNearestAvailable(type, from);
            return point != null;
        }

        /// <summary>쓸 수 있는 지점 중 하나를 무작위로 고른다. "가는 곳은 랜덤"인 손님이 쓰는 조회다.
        /// 후보가 몇 개 없을 때 치우치지 않도록 가중치 없이 균등하게 뽑는다.</summary>
        public bool TryGetRandom(MapPointType type, out MapPosition point)
        {
            point = null;

            if (!_points.TryGetValue(type, out List<MapPosition> list))
            {
                return false;
            }

            // 뽑은 뒤에 쓸 수 없는 걸 발견하면 다시 뽑아야 한다. 후보를 먼저 세어 한 번에 고른다.
            int available = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].IsAvailable)
                {
                    available++;
                }
            }

            if (available == 0)
            {
                return false;
            }

            int pick = UnityEngine.Random.Range(0, available);

            for (int i = 0; i < list.Count; i++)
            {
                MapPosition candidate = list[i];
                if (candidate == null || !candidate.IsAvailable)
                {
                    continue;
                }

                if (pick == 0)
                {
                    point = candidate;
                    return true;
                }

                pick--;
            }

            return false;
        }

        /// <summary>등록된 순서로 index번째 지점을 돌려준다. 범위를 넘으면 처음으로 돌아가므로 순회에 그대로 쓸 수 있다.
        /// 점유 여부는 보지 않는다 — 경유지처럼 여럿이 함께 지나는 지점을 위한 조회다.</summary>
        public bool TryGetAt(MapPointType type, int index, out MapPosition point)
        {
            point = null;

            if (index < 0 || !_points.TryGetValue(type, out List<MapPosition> list) || list.Count == 0)
            {
                return false;
            }

            // 목록에 파괴된 지점이 섞여 있을 수 있다. 순환시킨 자리부터 살아 있는 첫 지점을 고른다.
            int start = index % list.Count;

            for (int i = 0; i < list.Count; i++)
            {
                MapPosition candidate = list[(start + i) % list.Count];
                if (candidate != null)
                {
                    point = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>가장 가까운 빈 지점을 빌린다. 빌린 쪽이 반드시 <see cref="Release"/>로 짝을 맞춰야 한다.</summary>
        public bool TryRentNearest(MapPointType type, Vector3 from, out RentableMapPosition point)
        {
            point = FindNearestAvailable(type, from) as RentableMapPosition;
            if (point == null)
            {
                return false;
            }

            point.SetOccupied(true);
            Changed?.Invoke();
            return true;
        }

        /// <summary>빌린 지점을 돌려준다. 이미 파괴된 지점을 넘겨도 안전하다.</summary>
        public void Release(RentableMapPosition point)
        {
            if (point == null)
            {
                return;
            }

            point.SetOccupied(false);
            Changed?.Invoke();
        }

        /// <summary>가장 가까운 자리를 빌린다. 다 찼으면 false.</summary>
        public bool TryRentParkingSlot(Vector3 from, out RentableMapPosition slot)
            => TryRentNearest(MapPointType.ParkingSlot, from, out slot);

        /// <summary>빌린 자리를 돌려준다. 빌린 쪽이 반드시 짝을 맞춰 부른다.</summary>
        public void ReleaseParkingSlot(RentableMapPosition slot) => Release(slot);

        /// <summary>빈 자리가 하나라도 있는지. 차를 꺼내기 전에 먼저 확인하는 용도.</summary>
        public bool HasFreeParkingSlot => HasAvailable(MapPointType.ParkingSlot);

        public int ParkingSlotCount => CountOf(MapPointType.ParkingSlot);

        private MapPosition FindNearestAvailable(MapPointType type, Vector3 from)
        {
            if (!_points.TryGetValue(type, out List<MapPosition> list))
            {
                return null;
            }

            MapPosition best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                MapPosition candidate = list[i];
                if (candidate == null || !candidate.IsAvailable)
                {
                    continue;
                }

                // 제곱 거리로 비교한다. 순서만 필요하므로 제곱근을 뽑을 이유가 없다.
                float distance = (candidate.Position - from).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private List<MapPosition> GetOrCreate(MapPointType type)
        {
            if (!_points.TryGetValue(type, out List<MapPosition> list))
            {
                list = new List<MapPosition>();
                _points[type] = list;
            }

            return list;
        }

        // ScriptableObject의 런타임 상태는 에디터에서 플레이를 멈춰도 남는다.
        // 죽은 지점이 목록에 남지 않도록 로드/언로드 시점에 비운다.
        private void OnEnable() => _points.Clear();
        private void OnDisable() => _points.Clear();
    }
}
