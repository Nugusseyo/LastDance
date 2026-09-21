using _Works.CJW.Scripts.Customers.Data;using _Works.CJW.Scripts.MapSystems;
using JetBrains.Annotations;
using Resources.DataBase.Human_Data;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>손님 한 명의 상태들이 공유하는 값. 차 단위 값을 담는 <see cref="VisitContext"/>와는 스코프가 다르다. sealed인 이유는 손님 종류별 상속이 상태 쪽에 다운캐스팅을 만들기 때문이다.</summary>
    public sealed class CustomerContext
    {
        /// <summary> 진상 손님인지 평범한 손님인지 체크 </summary>
        [field:SerializeField] public HumanType HumanType { get; private set; }
        /// <summary>이 컨텍스트의 주인.</summary>
        public AbstractCustomer Customer { get; private set; }

        /// <summary>주인의 상태 머신. 상태가 스스로 인터럽트를 걸 때 쓴다.</summary>
        public CustomerStateMachine Machine { get; private set; }

        /// <summary>목적지 조회용. 상태마다 따로 들지 않도록 여기 하나만 둔다.</summary>
        public MapDataSo MapData { get; private set; }

        /// <summary>현재 방문의 차 단위 값. 방문 밖에서는 null이다.</summary>
        public VisitContext Visit { get; private set; }

        /// <summary> 손님이 가는 맵의 위치. Rentable일 경우 Release를 해야한다. </summary>
        [CanBeNull]
        public RentableMapPosition RentedPosition { get; set; }

        /// <summary>전투 대상이나 파손 대상. 인터럽트를 건 쪽이 채워준다.</summary>
        public Transform Target;

        /// <summary>약속으로 맺어진 상대. 짝이 풀리면 null이 되므로, 기다리는 쪽은 이걸 보고 빠져나온다.
        /// 다른 차의 손님일 수 있어 <see cref="Visit"/>의 손님 목록으로는 찾을 수 없다.</summary>
        [CanBeNull]
        public CustomerContext Partner;

        /// <summary>짝과 만나기로 한 지점. <see cref="CustomerRendezvousSO"/>가 둘의 중간으로 정해 양쪽에 같은 값을 넣는다.</summary>
        public Vector3 MeetPoint;
        /// <summary>이 방문에서 배정받은 좌석 번호. 하차 순서와 승차 좌석에 모두 쓰인다.</summary>
        public int SeatIndex { get; private set; }
        public CustomerDataSO Data => Customer != null ? Customer.Data : null;

        public void Bind(AbstractCustomer customer, CustomerStateMachine machine, MapDataSo mapData)
        {
            Customer = customer;
            Machine = machine;
            MapData = mapData;
        }

        public void SetVisit(VisitContext visit, int seatIndex = 0)
        {
            Visit = visit;
            SeatIndex = seatIndex;
        }

        /// <summary>풀 반납 시 호출. 풀링에서는 OnDestroy가 거의 불리지 않으므로 여기가 유일한 정리 지점이다.</summary>
        public void Reset()
        {
            // 상태가 취소로 끊겨 자기 finally를 못 지났을 수 있다. 마지막 안전망으로 여기서 짝을 맞춘다.
            // 빼먹으면 손님이 반납되어도 그 지점이 점유 상태로 남는다.
            if (RentedPosition != null)
            {
                MapData?.Release(RentedPosition);
                RentedPosition = null;
            }

            // 짝도 같은 이유로 끊어 준다. 안 끊으면 상대가 반납된 손님을 계속 기다린다.
            if (Partner != null)
            {
                if (Partner.Partner == this)
                {
                    Partner.Partner = null;
                }

                Partner = null;
            }

            Visit = null;
            Target = null;
            MeetPoint = Vector3.zero;
            SeatIndex = 0;
        }
    }
}
