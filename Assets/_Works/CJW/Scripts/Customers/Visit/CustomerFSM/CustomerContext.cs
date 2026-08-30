using _Works.CJW.Scripts.Customers.Data;using _Works.CJW.Scripts.MapSystems;

using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>
    /// 손님 한 명의 상태들이 공유하는 값. 스코프가 손님이라
    /// 차 단위 값을 담는 <see cref="VisitContext"/>와는 다르다.
    ///
    /// 여기에 넣는 것은 두 가지뿐이다.
    ///  - 여러 상태가 함께 읽는 참조
    ///  - 상태 전이를 넘어 살아남아야 하는 값
    /// 한 상태 안에서만 쓰는 타이머나 커서는 그 상태의 지역변수로 둔다.
    ///
    /// sealed인 이유: 손님 종류별로 상속하면 상태 쪽에 다운캐스팅이 생기고 조합 설계가 무너진다.
    /// 값을 더 붙여야 하면 필드를 추가하고 안 쓰는 손님은 기본값으로 둔다.
    /// </summary>
    public sealed class CustomerContext
    {
        /// <summary>이 컨텍스트의 주인.</summary>
        public AbstractCustomer Customer { get; private set; }

        /// <summary>주인의 상태 머신. 상태가 스스로 인터럽트를 걸 때 쓴다.</summary>
        public CustomerStateMachine Machine { get; private set; }

        /// <summary>목적지 조회용. 상태마다 따로 들지 않도록 여기 하나만 둔다.</summary>
        public MapDataSo MapData { get; private set; }

        /// <summary>현재 방문의 차 단위 값. 방문 밖에서는 null이다.</summary>
        public VisitContext Visit { get; private set; }

        /// <summary>전투 대상이나 파손 대상. 인터럽트를 건 쪽이 채워준다.</summary>
        public Transform Target;
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

        /// <summary>
        /// 풀 반납 시 호출. 풀링에서는 OnDestroy가 거의 불리지 않으므로 여기가 유일한 정리 지점이다.
        /// 손님 수치(Patience 등)는 다음 스폰의 Setup에서 채워지므로 여기서 건드리지 않는다.
        /// </summary>
        public void Reset()
        {
                        Visit = null;
            Target = null;
            SeatIndex = 0;
        }
    }
}
