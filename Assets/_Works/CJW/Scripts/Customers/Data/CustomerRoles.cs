namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>한 차에 둘 이상 태우지 않을 역할. 종류는 달라도 하는 일이 겹치면 같은 역할로 묶는다.</summary>
    public static class CustomerRoles
    {
        /// <summary>이 종류가 차지하는 역할. None(일반 손님)은 역할이 아니라서 그대로 None이다.</summary>
        public static CustomerType RoleOf(CustomerType type)
        {
            return type switch
            {
                // 내려서 주유기로 가든 차 안에서 기다리든 주유를 원하는 건 같다. 한 차에 주유 손님은 하나뿐이다.
                // 이상하게 걷는 손님도 걸음걸이만 다를 뿐 주유하러 온 손님이다.
                CustomerType.StayInCar => CustomerType.Refueling,
                CustomerType.OddWalker => CustomerType.Refueling,
                _ => type,
            };
        }

        /// <summary>이 손님이 주유를 원하는지. 한 차에는 주유 손님이 하나만 탄다.
        /// 역할과 따로 본다 — 네고 손님처럼 자기 역할이 있으면서 베이스 프리팹의 주유 상태를 물려받은 변형도 있어서,
        /// 역할 하나로만 거르면 그런 손님과 주유 손님이 한 차에 함께 탄다.</summary>
        public static bool WantsFuel(CustomerDataSO data)
        {
            return data != null && (RoleOf(data.CustomerType) == CustomerType.Refueling || data.WantsFuel);
        }
    }
}
