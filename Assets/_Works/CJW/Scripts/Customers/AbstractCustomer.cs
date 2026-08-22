using _Works.CJW.Scripts.Customers.Data;
using DevLib.ObjectPool.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers
{
    public abstract class AbstractCustomer : ManagingAgent, IPoolable
    {
        [field: SerializeField] public NavMeshAgent Agent { get; private set; }
        [field: SerializeField] public PoolItemSO PoolItem { get; set; }

        public GameObject GameObject => this != null ? gameObject : null;

        /// <summary>이 손님의 수치. 스폰될 때 <see cref="Setup"/>으로 주입된다.</summary>
        public CustomerDataSO Data { get; private set; }

        /// <summary>차에 타고 있는 동안에는 NavMesh 위에 없다.</summary>
        public bool IsBoarded { get; private set; }

        public bool IsArrived =>
            !IsBoarded &&
            Agent.enabled &&
            !Agent.pathPending &&
            Agent.remainingDistance <= Agent.stoppingDistance;

        /// <summary>풀에서 꺼낸 직후 이 손님이 쓸 데이터를 넣어준다.</summary>
        public virtual void Setup(CustomerDataSO data)
        {
            Data = data;
            if (data == null || Agent == null)
            {
                return;
            }

            if (data.MoveSpeed > 0f)
            {
                Agent.speed = data.MoveSpeed;
            }

            if (data.AngularSpeed > 0f)
            {
                Agent.angularSpeed = data.AngularSpeed;
            }

            if (data.StoppingDistance >= 0f)
            {
                Agent.stoppingDistance = data.StoppingDistance;
            }
        }

        public void MoveTo(Vector3 destination)
        {
            if (IsBoarded)
            {
                Debug.LogWarning($"[Customer] {name}이(가) 탑승 중이라 이동할 수 없습니다.");
                return;
            }

            Agent.SetDestination(destination);
        }

        /// <summary>좌석에 붙이고 NavMesh 제어를 끊는다.</summary>
        public void Board(Transform seat)
        {
            if (IsBoarded)
            {
                return;
            }

            IsBoarded = true;
            Agent.enabled = false;

            Transform tr = transform;
            tr.SetParent(seat, false);
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
        }

        /// <summary>좌석에서 떼어내 지정 위치로 걸어가게 한다.</summary>
        public void Unboard(Vector3 landingPosition, Vector3 destination)
        {
            if (!IsBoarded)
            {
                return;
            }

            IsBoarded = false;

            Transform tr = transform;
            tr.SetParent(null, true);
            tr.position = landingPosition;

            Agent.enabled = true;
            // 좌석에 붙어 있는 동안 NavMesh 밖으로 나갔을 수 있으므로 현재 위치로 맞춰준다.
            Agent.Warp(landingPosition);
            Agent.SetDestination(destination);
        }

        public virtual void ResetItem()
        {
            // 다음 스폰에서 Setup이 다시 넣어준다. 남겨두면 이전 방문의 값이 샌다.
            Data = null;
            IsBoarded = false;

            if (Agent != null)
            {
                Agent.enabled = true;
                if (Agent.isOnNavMesh)
                {
                    Agent.ResetPath();
                }
            }
        }
    }
}
