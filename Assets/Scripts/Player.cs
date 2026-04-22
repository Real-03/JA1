using UnityEngine;

[System.Serializable]
public class Player : MonoBehaviour
{
    public string name;
    public Transform transform;
    public Camera playerCamera;
    public int money;
    public int currentPosition = 0;

    public void AddMoney(int amount) => money += amount;
    public void SubtractMoney(int amount) => money -= amount;
}