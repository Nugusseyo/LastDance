using System.Collections.Generic;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.Customers.Visit;
using Resources.DataBase.Human_Data;
using UnityEngine;

namespace _Works.KDH._01.Scripts.Warehouse
{
    public class BadCustomerCars : MonoBehaviour
    {
        [SerializeField] private VisitDirector visitDirector;

        private readonly Dictionary<GameObject, List<AbstractCustomer>> riders = new Dictionary<GameObject, List<AbstractCustomer>>();
        private readonly HashSet<GameObject> badCars = new HashSet<GameObject>();

        private void OnEnable()
        {
            if (visitDirector != null) visitDirector.VisitStarted += RememberRiders;
        }

        private void OnDisable()
        {
            if (visitDirector != null) visitDirector.VisitStarted -= RememberRiders;
        }

        private void Update()
        {
            foreach (KeyValuePair<GameObject, List<AbstractCustomer>> pair in riders)
            {
                if (badCars.Contains(pair.Key)) continue;
                if (HasBadCustomer(pair.Value)) badCars.Add(pair.Key);
            }
        }

        public bool IsBadCar(GameObject car)
        {
            return car != null && badCars.Contains(car);
        }

        private void RememberRiders(VisitSession session)
        {
            if (session.Car == null) return;

            GameObject car = session.Car.gameObject;
            riders[car] = new List<AbstractCustomer>(session.Customers);
            badCars.Remove(car);
        }

        private bool HasBadCustomer(List<AbstractCustomer> customers)
        {
            foreach (AbstractCustomer customer in customers)
            {
                if (customer != null && FindCustomerType(customer) == HumanType.Bad) return true;
            }

            return false;
        }

        private HumanType FindCustomerType(AbstractCustomer customer)
        {
            int lineIndex = customer.Fsm != null && customer.Fsm.Context != null ? customer.Fsm.Context.LineIndex : 0;
            if (lineIndex > 0) return HumanTypeTable.Of(lineIndex, customer.HumanType);

            if (customer.ReviewIndices != null && customer.ReviewIndices.Length > 0)
            {
                return HumanTypeTable.Of(customer.ReviewIndices[0], customer.HumanType);
            }

            return customer.HumanType;
        }
    }
}
