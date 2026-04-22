using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Dice UI")]
    public GameObject dicePanel;
    public TMP_Text diceText;

    [Header("House UI")]
    public GameObject housePanel;
    public TMP_Text houseText;
    public Button buyButton;
    public Button declineButton;

    [Header("Money UI")]
    public TMP_Text moneyTextP1;
    public TMP_Text moneyTextP2;

    public void UpdateMoneyUI(int p1Money, int p2Money)
    {
        moneyTextP1.text = $"P1: {p1Money}€";
        moneyTextP2.text = $"P2: {p2Money}€";
    }

    public void ShowDice(int value)
    {
        dicePanel.SetActive(true);
        diceText.text = $"Dice roll: {value}";
    }
}