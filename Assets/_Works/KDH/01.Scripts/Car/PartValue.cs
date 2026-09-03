using UnityEngine;

public class PartValue : MonoBehaviour
{
    [SerializeField] private int sellPrice = 100;

    public int SellPrice => sellPrice;
}
