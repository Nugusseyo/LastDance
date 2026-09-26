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

        public static NavMeshQueryFilter Filter => new NavMeshQueryFilter
        {
            agentTypeID = AgentTypeId,
            areaMask = NavMesh.AllAreas,
        };

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
        private static void ResetOnLoad() => _resolved = false;
    }
}
