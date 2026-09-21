using System.Collections.Generic;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>약속 이름별로 손님 둘을 맺어 주는 등록소. 세션을 가로지르므로 <b>서로 다른 차에서 내린 손님끼리도</b> 짝이 된다 —
    /// <see cref="CustomerContext.Visit"/>의 손님 목록은 같은 차만 보기 때문에 이 역할을 대신할 수 없다.
    /// 빌린 자리와 같은 규율을 따른다: 짝을 지었으면 반드시 <see cref="Leave"/>로 돌려줘야 다음 손님이 짝을 찾는다.</summary>
    [CreateAssetMenu(fileName = "Customer Rendezvous", menuName = "JW/Customers/Customer Rendezvous", order = 2)]
    public class CustomerRendezvousSO : ScriptableObject
    {
        [Tooltip("짝이 모일 지점의 종류. 둘의 중간에서 가장 가까운 지점 하나를 골라 양쪽에 같은 값을 준다. " +
                 "None이면 지점을 찾지 않고 둘의 중간에서 만난다.")]
        [SerializeField] private MapPointType meetAt = MapPointType.FightArea;

        [Tooltip("만날 지점을 NavMesh 위에서 찾을 때 허용할 오차(m). 중간 지점이 길 밖일 때만 쓰인다.")]
        [SerializeField, Min(0f)] private float meetSampleRadius = 3f;

        /// <summary>약속 이름마다 먼저 와서 기다리는 사람 하나.</summary>
        private readonly Dictionary<string, CustomerContext> _waiting = new();

        /// <summary>같은 이름으로 기다리는 짝을 찾는다. 찾으면 양쪽 컨텍스트에 서로를 넣고 true,
        /// 못 찾으면 내가 기다리는 쪽이 되고 false를 돌려준다. 기다리는 쪽은 자기 <see cref="CustomerContext.Partner"/>가
        /// 채워지는지 지켜보면 된다.</summary>
        public bool TryPair(string key, CustomerContext self, out CustomerContext partner)
        {
            partner = null;

            if (self == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (_waiting.TryGetValue(key, out CustomerContext waiting))
            {
                // 내가 이미 기다리는 중이면 나 자신과 짝지을 수는 없다.
                if (ReferenceEquals(waiting, self))
                {
                    return false;
                }

                // 기다리던 쪽이 그 사이 반납됐으면 그 자리를 내가 차지한다.
                if (waiting == null || waiting.Customer == null)
                {
                    _waiting[key] = self;
                    return false;
                }

                _waiting.Remove(key);

                Vector3 meet = ResolveMeetPoint(self, waiting);

                self.Partner = waiting;
                self.MeetPoint = meet;

                waiting.Partner = self;
                waiting.MeetPoint = meet;

                partner = waiting;
                return true;
            }

            _waiting[key] = self;
            return false;
        }

        /// <summary>기다리기를 그만두거나 짝을 놓아준다. 상태의 finally에서 반드시 불러야 한다.</summary>
        public void Leave(string key, CustomerContext self)
        {
            if (self == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(key) &&
                _waiting.TryGetValue(key, out CustomerContext waiting) &&
                ReferenceEquals(waiting, self))
            {
                _waiting.Remove(key);
            }

            // 상대에게도 알린다. 상대는 Partner가 null이 된 걸 보고 빠져나온다.
            CustomerContext partner = self.Partner;
            self.Partner = null;

            if (partner != null && ReferenceEquals(partner.Partner, self))
            {
                partner.Partner = null;
            }
        }

        /// <summary>둘이 모일 자리. 지정한 종류의 지점 중 둘의 중간에서 가장 가까운 하나를 고른다.
        /// 한 번만 계산해 양쪽에 같은 값을 주므로, 각자 "나에게 가까운 곳"을 고르다 서로 다른 데로 가는 일이 없다.</summary>
        private Vector3 ResolveMeetPoint(CustomerContext a, CustomerContext b)
        {
            Vector3 mid = (a.Customer.transform.position + b.Customer.transform.position) * 0.5f;

            if (meetAt != MapPointType.None)
            {
                MapDataSo map = a.MapData != null ? a.MapData : b.MapData;

                if (map != null && map.TryGetNearest(meetAt, mid, out MapPosition point))
                {
                    return point.Position;
                }

                // 싸움터가 없다고 방문을 막지는 않는다. 둘의 중간에서라도 만나게 두고 문제만 남긴다.
                Debug.LogWarning($"[{nameof(CustomerRendezvousSO)}] 맵에 {meetAt} 지점이 없어 둘의 중간에서 만납니다. " +
                                 "씬에 MapPosition을 놓아야 합니다.", this);
            }

            return NavMesh.SamplePosition(mid, out NavMeshHit hit, meetSampleRadius, NavMesh.AllAreas)
                ? hit.position
                : mid;
        }

        // ScriptableObject는 플레이 모드를 넘어 값이 남는다. MapDataSo와 같은 이유로 여기서 비운다.
        private void OnEnable() => _waiting.Clear();
        private void OnDisable() => _waiting.Clear();
    }
}
