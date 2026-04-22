using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class TurnManager : MonoBehaviour
{
    [Header("Players")]
    public Player p1;
    public Player p2;

    [Header("Player Colors")]
    public Color p1Color = Color.red;
    public Color p2Color = Color.blue;

    [Header("Cameras")]
    public Camera mainCamera;

    [Header("References")]
    public UIManager ui;

    [Header("Game Settings")]
    public int startingMoney = 1500;
    public int lapBonus = 200;
    public int housePrice = 300;
    public int houseRent = 150;
    public int bigHousePrice = 600;
    public int bigHouseRent = 300;

    [Header("Prefabs")]
    public GameObject housePrefab;
    public GameObject bigHousePrefab;
    public float outwardOffset = 1.2f;

    private Transform[] tiles;
    private bool isP1Turn = true;
    private bool isWaitingInput = false;

    // 0 = None, 1 = P1, 2 = P2
    private int[] owners;
    private Dictionary<int, GameObject> spawnedHouses = new Dictionary<int, GameObject>();

    // Board Configuration
    private readonly HashSet<int> whiteTiles = new HashSet<int> { 0, 2, 5, 7, 10, 13, 15, 17, 20, 23, 25, 26, 28, 30, 32, 34, 35, 37 };
    private readonly Dictionary<int, int> secondaryToCanonical = new Dictionary<int, int> { { 4, 3 }, { 9, 8 }, { 12, 11 }, { 19, 18 }, { 22, 21 }, { 39, 38 } };
    private readonly HashSet<int> bigHouses = new HashSet<int> { 3, 8, 11, 18, 21, 38 };

    void Awake()
    {
        tiles = transform.Cast<Transform>()
            .Where(t => int.TryParse(t.name, out _))
            .OrderBy(t => int.Parse(t.name))
            .ToArray();

        owners = new int[tiles.Length];
    }

    void Start()
    {
        p1.money = startingMoney;
        p2.money = startingMoney;
        ui.UpdateMoneyUI(p1.money, p2.money);
        UpdatePlayerVisualPosition();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isWaitingInput)
            StartTurn();
    }

    int GetCanonicalIndex(int index) => secondaryToCanonical.TryGetValue(index, out int main) ? main : index;

    void StartTurn()
    {
        SoundManager.Instance.PlayDiceRoll();
        int diceValue = Random.Range(1, 7);
        SwitchCamera(true);
        StartCoroutine(TurnFlow(diceValue));
    }

    IEnumerator TurnFlow(int diceValue)
    {
        isWaitingInput = true;
        ui.ShowDice(diceValue);
        yield return new WaitForSeconds(1.5f);
        SoundManager.Instance.sfxSource.Stop();
        ui.dicePanel.SetActive(false);

        Player activePlayer = isP1Turn ? p1 : p2;
        int posBefore = activePlayer.currentPosition;

        yield return StartCoroutine(MovePlayer(activePlayer, diceValue));

        SoundManager.Instance.sfxSource.Stop();

        if (activePlayer.currentPosition <= posBefore && diceValue > 0)
        {
            activePlayer.AddMoney(lapBonus);
            ui.UpdateMoneyUI(p1.money, p2.money);
        }

        yield return StartCoroutine(CheckTileLogic());

        SwitchCamera(false);
        isP1Turn = !isP1Turn;
        isWaitingInput = false;
    }

    IEnumerator MovePlayer(Player player, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            player.currentPosition = (player.currentPosition + 1) % tiles.Length;
            if (secondaryToCanonical.ContainsKey(player.currentPosition))
                player.currentPosition = (player.currentPosition + 1) % tiles.Length;

            SoundManager.Instance.PlayWalk();
            UpdatePlayerVisualPosition();
            yield return new WaitForSeconds(0.3f);
        }
    }

    IEnumerator CheckTileLogic()
    {
        Player currentPlayer = isP1Turn ? p1 : p2;
        Player opponent = isP1Turn ? p2 : p1;
        int tileIdx = GetCanonicalIndex(currentPlayer.currentPosition);

        if (whiteTiles.Contains(tileIdx)) yield break;

        bool isBig = bigHouses.Contains(tileIdx);
        int price = isBig ? bigHousePrice : housePrice;
        int rent = isBig ? bigHouseRent : houseRent;
        int owner = owners[tileIdx];

        if (owner != 0 && owner != (isP1Turn ? 1 : 2))
        {
            ui.houseText.text = $"Owner: Player {owner}\nRent: {rent}€";
            ui.buyButton.gameObject.SetActive(false);
            ui.declineButton.GetComponentInChildren<TMP_Text>().text = "Pay";
            ui.housePanel.SetActive(true);

            bool clicked = false;
            ui.declineButton.onClick.RemoveAllListeners();
            ui.declineButton.onClick.AddListener(() => clicked = true);
            yield return new WaitUntil(() => clicked);

            currentPlayer.SubtractMoney(rent);
            opponent.AddMoney(rent);
        }
        else if (owner == 0)
        {
            ui.houseText.text = isBig ? $"Big House Available!\nPrice: {price}€" : $"House Available!\nPrice: {price}€";
            ui.buyButton.gameObject.SetActive(true);
            ui.declineButton.GetComponentInChildren<TMP_Text>().text = "Decline";
            ui.housePanel.SetActive(true);

            bool decided = false;
            bool bought = false;

            ui.buyButton.onClick.RemoveAllListeners();
            ui.buyButton.onClick.AddListener(() => { bought = true; decided = true; });
            ui.declineButton.onClick.RemoveAllListeners();
            ui.declineButton.onClick.AddListener(() => decided = true);

            yield return new WaitUntil(() => decided);

            if (bought && currentPlayer.money >= price)
            {
                owners[tileIdx] = isP1Turn ? 1 : 2;
                currentPlayer.SubtractMoney(price);
                SpawnHouseModel(tileIdx);
            }
        }

        ui.housePanel.SetActive(false);
        ui.UpdateMoneyUI(p1.money, p2.money);
    }

    void SpawnHouseModel(int index)
    {
        int owner = owners[index];
        bool isBig = bigHouses.Contains(index);

        GameObject selectedPrefab = isBig ? bigHousePrefab : housePrefab;
        if (selectedPrefab == null) return;

        Transform targetTile = tiles[index];
        Vector3 spawnPos;

        if (isBig)
        {
            Transform nextTile = tiles[(index + 1) % tiles.Length];
            spawnPos = Vector3.Lerp(targetTile.position, nextTile.position, 0.5f);
        }
        else
        {
            spawnPos = targetTile.position;
        }

        Vector3 boardCenter = new Vector3(transform.position.x, spawnPos.y, transform.position.z);
        Vector3 dirOut = (spawnPos - boardCenter).normalized;
        spawnPos += dirOut * outwardOffset;

        GameObject house = Instantiate(selectedPrefab, spawnPos, Quaternion.identity, targetTile);

        if (isBig)
        {
            int side = index / 10;
            float defaultScale = 0.002f;
            Vector3 newScale = house.transform.localScale;

            if (side == 0 || side == 2) newScale.x = defaultScale;
            else newScale.z = defaultScale;

            house.transform.localScale = newScale;
        }

        Color colorToApply = (owner == 1) ? p1Color : p2Color;
        ApplyColorToHouse(house, colorToApply);

        spawnedHouses[index] = house;
    }

    void ApplyColorToHouse(GameObject houseObj, Color color)
    {
        Renderer[] renderers = houseObj.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            r.material.color = color;
        }
    }

    void UpdatePlayerVisualPosition()
    {
        void PositionPlayer(Player p, string pointName, float rotZ)
        {
            int visIdx = GetCanonicalIndex(p.currentPosition);
            Transform slot = tiles[visIdx].Find(pointName);
            if (slot)
            {
                p.transform.SetParent(slot);
                p.transform.localPosition = Vector3.zero;
                p.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
            }
        }

        PositionPlayer(p1, "Player1", 90);
        PositionPlayer(p2, "Player2", -90);
    }

    void SwitchCamera(bool toPlayer)
    {
        mainCamera.gameObject.SetActive(!toPlayer);
        p1.playerCamera.gameObject.SetActive(toPlayer && isP1Turn);
        p2.playerCamera.gameObject.SetActive(toPlayer && !isP1Turn);
    }
}