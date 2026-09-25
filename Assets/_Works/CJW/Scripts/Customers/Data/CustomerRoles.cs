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
                CustomerType.StayInCar => CustomerType.Refueling,
                _ => type,
            };
        }
    }
}
