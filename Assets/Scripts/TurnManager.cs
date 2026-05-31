using UnityEngine;
using UnityEngine.InputSystem;
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

    private Transform[] tiles;
    private bool isP1Turn = true;
    private bool isWaitingInput = false;
    private bool isProcessingTurn = false;

    private int[] owners;
    private Dictionary<int, GameObject> spawnedHouses = new Dictionary<int, GameObject>();

    private readonly HashSet<int> whiteTiles = new HashSet<int> { 0, 2, 5, 7, 10, 13, 15, 17, 20, 23, 25, 26, 28, 30, 32, 34, 35, 37 };
    private readonly Dictionary<int, int> secondaryToCanonical = new Dictionary<int, int> { { 4, 3 }, { 9, 8 }, { 12, 11 }, { 19, 18 }, { 22, 21 }, { 39, 38 } };
    private readonly HashSet<int> bigHouses = new HashSet<int> { 3, 8, 11, 18, 21, 38 };

    private readonly Dictionary<int, int> tileIndexToPropertyIndex = new Dictionary<int, int>
    {
        { 1, 0 }, { 3, 1 }, { 6, 2 }, { 8, 3 }, { 9, 4 }, { 11, 5 },
        { 12, 6 }, { 14, 7 }, { 16, 8 }, { 18, 9 }, { 19, 10 }, { 21, 11 },
        { 22, 12 }, { 24, 13 }, { 27, 14 }, { 29, 15 }, { 31, 16 }, { 33, 17 },
        { 36, 18 }, { 38, 19 }, { 39, 20 }
    };

    private bool isAnnouncingPosition = false;
    private bool isAnnouncingProperties = false;

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
        StartCoroutine(AnnounceFirstTurn());
    }

    IEnumerator AnnounceFirstTurn()
    {
        yield return new WaitForSeconds(0.5f);
        SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_P1Turn);
        yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isWaitingInput && !isProcessingTurn)
            StartTurn();

        if (Keyboard.current.cKey.wasPressedThisFrame && !isAnnouncingPosition && !isProcessingTurn)
            StartCoroutine(AnnounceCurrentPosition());

        if (Keyboard.current.vKey.wasPressedThisFrame && !isAnnouncingProperties && !isProcessingTurn)
            StartCoroutine(AnnounceOwnedProperties());
    }

    IEnumerator AnnounceCurrentPosition()
    {
        isAnnouncingPosition = true;
        Player activePlayer = isP1Turn ? p1 : p2;
        int tileIdx = GetCanonicalIndex(activePlayer.currentPosition);

        if (tileIndexToPropertyIndex.TryGetValue(tileIdx, out int propIdx))
        {
            SoundManager.Instance.PlayPropertyNarrator(propIdx);
            yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
        }
        isAnnouncingPosition = false;
    }

    IEnumerator AnnounceOwnedProperties()
    {
        isAnnouncingProperties = true;
        int playerNum = isP1Turn ? 1 : 2;
        List<int> ownedTiles = new List<int>();

        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] == playerNum && tileIndexToPropertyIndex.ContainsKey(i))
                ownedTiles.Add(i);
        }

        foreach (int tileIdx in ownedTiles)
        {
            int propIdx = tileIndexToPropertyIndex[tileIdx];
            SoundManager.Instance.PlayHaveHouseNarrator(propIdx);
            yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
        }
        isAnnouncingProperties = false;
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
        isProcessingTurn = true;

        ui.ShowDice(diceValue);

        yield return new WaitForSeconds(1.5f);
        SoundManager.Instance.sfxSource.Stop();
        SoundManager.Instance.PlayDiceNarrator(diceValue);
        ui.dicePanel.SetActive(false);

        Player activePlayer = isP1Turn ? p1 : p2;
        int posBefore = activePlayer.currentPosition;

        yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

        yield return StartCoroutine(MovePlayer(activePlayer, diceValue));

        SoundManager.Instance.sfxSource.Stop();

        if (activePlayer.currentPosition <= posBefore && diceValue > 0)
        {
            activePlayer.AddMoney(lapBonus);
            ui.UpdateMoneyUI(p1.money, p2.money);
        }

        yield return StartCoroutine(AnnounceCurrentPosition());

        yield return StartCoroutine(CheckTileLogic());

        SwitchCamera(false);
        isP1Turn = !isP1Turn;

        AudioClip turnClip = isP1Turn ? SoundManager.Instance.narr_P1Turn : SoundManager.Instance.narr_P2Turn;
        SoundManager.Instance.PlayNarrator(turnClip);
        yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

        isProcessingTurn = false;
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

            SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_BuyHouse);
            yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

            bool decided = false;
            bool bought = false;

            ui.buyButton.onClick.RemoveAllListeners();
            ui.buyButton.onClick.AddListener(() => { bought = true; decided = true; });
            ui.declineButton.onClick.RemoveAllListeners();
            ui.declineButton.onClick.AddListener(() => decided = true);

            yield return new WaitUntil(() => decided);

            if (bought && currentPlayer.money >= price)
            {
                SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Yes);
                yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
                owners[tileIdx] = isP1Turn ? 1 : 2;
                currentPlayer.SubtractMoney(price);
                SpawnHouseModel(tileIdx);
            }
            else
            {
                SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_No);
                yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
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

        Transform houseSlot = targetTile.Find("House");
        if (houseSlot == null)
        {
            Debug.LogWarning($"Tile {index} has no child named 'House'.");
            return;
        }

        GameObject house = Instantiate(selectedPrefab, houseSlot.position, houseSlot.rotation, houseSlot);

        Color colorToApply = (owner == 1) ? p1Color : p2Color;
        ApplyColorToHouse(house, colorToApply);

        spawnedHouses[index] = house;
    }

    void ApplyColorToHouse(GameObject houseObj, Color color)
    {
        Renderer[] renderers = houseObj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
            r.material.color = color;
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