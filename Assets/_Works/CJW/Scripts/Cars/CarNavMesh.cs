using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>차 전용 NavMesh(에이전트 타입 "Car")에 묻는 조회. NavMesh.SamplePosition·Raycast를 필터 없이 부르면
    /// 기본 타입(사람용 Humanoid) NavMesh를 보므로, 차가 못 가는 인도·주유기 사이까지 갈 수 있다고 답한다.</summary>
    public static class CarNavMesh
    {
        /// <summary>Navigation 창의 Agents 탭에 있는 차 에이전트 이름.</summary>
        public const string AgentTypeName = "Car";

        private static int _agentTypeId;
        private static bool _resolved;

        /// <summary>차 에이전트 타입 ID. 없으면 기본 타입(0)으로 물러난다.</summary>
        public static int AgentTypeId
        {
            get
            {
                if (!_resolved)
                {
                    _agentTypeId = FindAgentTypeId(AgentTypeName);
                    _resolved = true;
                }

                return _agentTypeId;
            }
        }

        /// <summary>도로 밖 바닥(지형)의 NavMesh 구역 번호. 차 NavMesh를 구울 때 지형을 이 구역으로 둔다.</summary>
        public const int OffroadArea = 3;

        public const string OffroadAreaName = "Offroad";

        /// <summary>도로 밖을 달리는 비용 배율. 크게 둬야 차가 풀밭을 가로지르지 않고 도로를 따라가다 목적지 가까이에서만 도로를 벗어난다.</summary>
        public const float OffroadCost = 10f;

        /// <summary>도로의 NavMesh 구역 번호. 도로는 지형 위에 거의 같은 높이로 겹쳐 있어, 구울 때 번호가 큰 구역이 이긴다.
        /// 지형(<see cref="OffroadArea"/>)보다 커야 도로 위가 도로로 남는다.</summary>
        public const int RoadArea = 4;

        public const string RoadAreaName = "Road";

        /// <summary>도로만 다니는 차(지나가는 차)의 구역 마스크.</summary>
        public const int RoadOnlyMask = 1 << RoadArea;

        /// <summary>경로 계산용 필터. 필터의 구역 비용은 프로젝트 설정이 아니라 1로 시작하므로 도로 밖 비용을 직접 넣는다.</summary>
        public static NavMeshQueryFilter Filter
        {
            get
            {
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = AgentTypeId,
                    areaMask = NavMesh.AllAreas,
                };
                filter.SetAreaCost(OffroadArea, OffroadCost);
                return filter;
            }
        }

        public static bool SamplePosition(Vector3 position, out NavMeshHit hit, float maxDistance)
            => NavMesh.SamplePosition(position, out hit, maxDistance, Filter);

        public static bool Raycast(Vector3 from, Vector3 to, out NavMeshHit hit)
            => NavMesh.Raycast(from, to, out hit, Filter);

        public static int FindAgentTypeId(string agentName)
        {
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(id) == agentName)
                {
                    return id;
                }
            }

            return 0;
        }

        // 에이전트 타입을 새로 만든 뒤 도메인 리로드 없이 플레이해도 옛 값을 쓰지 않게 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _resolved = false;

            // 차 에이전트(NavMeshAgent.SetDestination)는 전역 구역 비용을 쓴다. 프로젝트 설정이 바뀌지 않았어도 도로를 따르게 여기서 맞춘다.
            NavMesh.SetAreaCost(OffroadArea, OffroadCost);
        }
    }
}
