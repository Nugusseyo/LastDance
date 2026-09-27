using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Appearance
{
    /// <summary>손님의 겉모습. 스폰될 때마다 캐릭터 후보 중 하나로 옷·머리·얼굴을 갈아입는다.</summary>
    public interface ICustomerAppearance
    {
        /// <summary>지금 입고 있는 캐릭터 프리팹. 아직 갈아입지 않았으면 null이다.</summary>
        GameObject CurrentLook { get; }

        /// <summary>후보 중 하나를 무작위로 골라 갈아입는다.</summary>
        void Randomize();
    }
}
