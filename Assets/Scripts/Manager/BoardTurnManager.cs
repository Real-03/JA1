using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;

// Tabuleiro "Board 1" (32 casas, 4 lados de 8 casas).
// Em cada lado: canto (1, 9, 17, 25) | propriedade | propriedade | propriedade | casa da sorte (5, 13, 21, 29) | propriedade x3
// Canto e casa da sorte não são compráveis. As restantes são propriedades.
// Casa comprada começa só com HouseBase; depois pode ativar até 2 upgrades.
// Controlo: Espaço = lançar dado, C = posição, V = propriedades, setas = escolher, Enter/Espaço = confirmar, 1-4 = upgrades.
public class BoardTurnManager : MonoBehaviour
{
    [Header("Players")]
    public PlayerController p1;
    public PlayerController p2;

    [Header("Player Colors")]
    public Color p1Color = Color.red;
    public Color p2Color = Color.blue;

    [Header("Posição do peão na casa (fica na borda, nunca por cima da casa)")]
    [Tooltip("Distância ao centro da casa, para o lado de fora do tabuleiro.")]
    public float edgeDistance = 0.35f;
    [Tooltip("Desvio lateral: separa os dois jogadores.")]
    public float lateralOffset = 0.2f;
    [Tooltip("Altura acima do tabuleiro.")]
    public float standHeight = 0.5f;

    [Header("Movimento (salto)")]
    [Tooltip("Tempo que o peão demora a saltar de uma casa para a seguinte.")]
    public float hopDuration = 0.25f;
    [Tooltip("Altura máxima do salto.")]
    public float hopHeight = 0.4f;

    [Header("Cameras")]
    public Camera mainCamera;

    [Header("References")]
    public UIManager ui;
    [Tooltip("Pai das casas 1..32. Se ficar vazio, procura um objeto chamado 'Board 1'.")]
    public Transform boardRoot;
    [Tooltip("Casa de referência (objeto 'House' da cena). Se ficar vazio, procura um objeto chamado 'House'. Fica escondida ao iniciar e serve de modelo para as casas compradas.")]
    public Transform houseTemplate;

    [Header("Game Settings")]
    public int startingMoney = 1500;
    public int lapBonus = 200;
    public int housePrice = 300;
    public int houseRent = 150;
    [Tooltip("Custo de cada upgrade escolhido na compra.")]
    public int upgradePrice = 100;
    public int maxUpgradesPerHouse = 2;

    // Nomes dos filhos do modelo da casa (House.fbx)
    private static readonly string[] UpgradeNames = { "HouseAutism", "HouseBlind", "HouseDef", "HouseMobility" };
    private static readonly string[] UpgradeLabels = { "Autism", "Blind", "Def", "Mobility" };
    private static readonly Key[] UpgradeKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
    private static readonly Key[] UpgradeNumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4 };
    private const string BaseName = "HouseBase";
    private const int TilesPerSide = 8;

    private Transform[] tiles;
    private int[] owners;
    private bool isP1Turn = true;
    private bool isWaitingInput = false;
    private bool isProcessingTurn = false;
    private bool isAnnouncingPosition = false;
    private bool isAnnouncingProperties = false;

    private readonly Dictionary<int, int> propertyIndexByTile = new Dictionary<int, int>();
    private readonly Dictionary<int, GameObject> spawnedHouses = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, bool[]> houseUpgrades = new Dictionary<int, bool[]>();

    // Pose da casa relativa à casa de referência (calculada uma vez, ao iniciar)
    private Matrix4x4 referenceToHouse;
    private Vector3 houseScale = Vector3.one;
    private Vector3 boardCenter;
    private bool templateReady = false;

    void Awake()
    {
        if (boardRoot == null)
        {
            GameObject go = GameObject.Find("Board 1");
            if (go != null) boardRoot = go.transform;
        }

        if (boardRoot == null)
        {
            Debug.LogError("BoardTurnManager: não encontrei o tabuleiro 'Board 1'.");
            enabled = false;
            return;
        }

        tiles = boardRoot.Cast<Transform>()
            .Where(t => int.TryParse(t.name, out _))
            .OrderBy(t => int.Parse(t.name))
            .ToArray();

        owners = new int[tiles.Length];

        Vector3 sum = Vector3.zero;
        foreach (Transform t in tiles) sum += t.position;
        boardCenter = sum / tiles.Length;

        int propertyCount = 0;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (IsPropertyIndex(i))
                propertyIndexByTile[i] = propertyCount++;
        }

        CaptureHouseTemplate();
    }

    void Start()
    {
        if (tiles == null) return;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        p1.money = startingMoney;
        p2.money = startingMoney;
        ui.UpdateMoneyUI(p1.money, p2.money);

        UpdatePlayerVisualPosition();
        SwitchCamera(false);
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
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame && !isWaitingInput && !isProcessingTurn)
            StartTurn();

        if (kb.cKey.wasPressedThisFrame && !isAnnouncingPosition && !isProcessingTurn)
            StartCoroutine(AnnounceCurrentPosition());

        if (kb.vKey.wasPressedThisFrame && !isAnnouncingProperties && !isProcessingTurn)
            StartCoroutine(AnnounceOwnedProperties());
    }

    // ---------- Regras das casas ----------

    static bool IsPropertyIndex(int index)
    {
        int posInSide = index % TilesPerSide;
        return posInSide != 0 && posInSide != 4; // 0 = canto, 4 = casa da sorte
    }

    IEnumerator AnnounceCurrentPosition()
    {
        isAnnouncingPosition = true;
        PlayerController activePlayer = isP1Turn ? p1 : p2;

        if (propertyIndexByTile.TryGetValue(activePlayer.currentPosition, out int propIdx))
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

        List<int> owned = propertyIndexByTile.Keys
            .Where(i => owners[i] == playerNum)
            .OrderBy(i => i)
            .ToList();

        foreach (int tileIdx in owned)
        {
            SoundManager.Instance.PlayHaveHouseNarrator(propertyIndexByTile[tileIdx]);
            yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
        }

        isAnnouncingProperties = false;
    }

    // ---------- Turno ----------

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

        PlayerController activePlayer = isP1Turn ? p1 : p2;
        int posBefore = activePlayer.currentPosition;
        Debug.Log($"BoardTurnManager: {(isP1Turn ? "P1" : "P2")} tirou {diceValue}, da casa {posBefore + 1} para a casa {(posBefore + diceValue) % tiles.Length + 1}.");

        yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

        yield return StartCoroutine(MovePlayer(activePlayer, diceValue));

        SoundManager.Instance.sfxSource.Stop();

        // Ao dar a volta ao tabuleiro a posição desce (passou pela casa 1)
        if (activePlayer.currentPosition < posBefore)
        {
            activePlayer.AddMoney(lapBonus);
            ui.UpdateMoneyUI(p1.money, p2.money);
        }

        yield return StartCoroutine(AnnounceCurrentPosition());

        yield return StartCoroutine(CheckTileLogic());

        ui.UpdateMoneyUI(p1.money, p2.money);

        if (CheckGameOver())
            yield break;

        SwitchCamera(false);
        isP1Turn = !isP1Turn;

        AudioClip turnClip = isP1Turn ? SoundManager.Instance.narr_P1Turn : SoundManager.Instance.narr_P2Turn;
        SoundManager.Instance.PlayNarrator(turnClip);
        yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

        isProcessingTurn = false;
        isWaitingInput = false;
    }

    IEnumerator MovePlayer(PlayerController player, int steps)
    {
        Transform playerT = player.gameObject.transform;

        for (int i = 0; i < steps; i++)
        {
            player.currentPosition = (player.currentPosition + 1) % tiles.Length;

            Vector3 start = playerT.position;
            Vector3 end = StandingPosition(player.currentPosition, player);

            SoundManager.Instance.PlayWalk();

            // Salto: avança em linha reta e ao mesmo tempo sobe e desce num arco
            float elapsed = 0f;
            while (elapsed < hopDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / hopDuration);
                Vector3 pos = Vector3.Lerp(start, end, k);
                pos += Vector3.up * (Mathf.Sin(k * Mathf.PI) * hopHeight);
                playerT.position = pos;
                yield return null;
            }

            playerT.position = end;
        }
    }

    IEnumerator CheckTileLogic()
    {
        PlayerController currentPlayer = isP1Turn ? p1 : p2;
        PlayerController opponent = isP1Turn ? p2 : p1;
        int tileIdx = currentPlayer.currentPosition;

        // Canto e casa da sorte: sem ação por agora
        if (!IsPropertyIndex(tileIdx))
            yield break;

        int myNum = isP1Turn ? 1 : 2;
        int owner = owners[tileIdx];

        if (owner == 0)
            yield return HandleBuy(currentPlayer, tileIdx, myNum);
        else if (owner != myNum)
            yield return HandleRent(currentPlayer, opponent, owner);

        ui.housePanel.SetActive(false);
        ui.UpdateMoneyUI(p1.money, p2.money);
    }

    IEnumerator HandleRent(PlayerController payer, PlayerController receiver, int ownerNum)
    {
        int rent = houseRent;

        ui.houseText.text = $"Owner: Player {ownerNum}\nRent: {rent}€\n\nEnter: pay";
        ui.buyButton.gameObject.SetActive(false);
        ui.declineButton.gameObject.SetActive(false);
        ui.housePanel.SetActive(true);

        yield return new WaitUntil(ConfirmPressed);

        payer.SubtractMoney(rent);
        receiver.AddMoney(rent);
    }

    IEnumerator HandleBuy(PlayerController buyer, int tileIdx, int buyerNum)
    {
        int price = housePrice;
        bool buyFocused = true;

        ui.buyButton.gameObject.SetActive(true);
        ui.declineButton.gameObject.SetActive(true);
        ui.declineButton.GetComponentInChildren<TMP_Text>().text = "Decline";
        ui.housePanel.SetActive(true);
        ShowBuyPrompt(price, buyFocused);

        SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_BuyHouse);
        yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

        bool decided = false;
        bool bought = false;

        ui.buyButton.onClick.RemoveAllListeners();
        ui.buyButton.onClick.AddListener(() => { bought = true; decided = true; });
        ui.declineButton.onClick.RemoveAllListeners();
        ui.declineButton.onClick.AddListener(() => decided = true);

        while (!decided)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
            {
                buyFocused = !buyFocused;
                ShowBuyPrompt(price, buyFocused);
            }

            if (ConfirmPressed())
            {
                bought = buyFocused;
                decided = true;
            }

            yield return null;
        }

        if (bought && buyer.money >= price)
        {
            SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Yes);
            yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

            owners[tileIdx] = buyerNum;
            buyer.SubtractMoney(price);
            ui.UpdateMoneyUI(p1.money, p2.money);

            GameObject house = SpawnHouse(tileIdx);
            yield return ChooseUpgrades(buyer, tileIdx, house);
        }
        else
        {
            SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_No);
            yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
        }
    }

    // Escolha de upgrades (0 a maxUpgradesPerHouse). Ativa/desativa os filhos da casa em tempo real.
    IEnumerator ChooseUpgrades(PlayerController buyer, int tileIdx, GameObject house)
    {
        bool[] chosen = new bool[UpgradeNames.Length];
        int count = 0;

        ui.buyButton.gameObject.SetActive(false);
        ui.declineButton.gameObject.SetActive(false);
        ui.housePanel.SetActive(true);
        ShowUpgradePrompt(chosen, count);

        // Espera um frame para que a tecla que confirmou a compra não conte aqui
        yield return null;

        while (!ConfirmPressed())
        {
            Keyboard kb = Keyboard.current;
            bool changed = false;

            for (int i = 0; kb != null && i < UpgradeNames.Length; i++)
            {
                bool pressed = kb[UpgradeKeys[i]].wasPressedThisFrame || kb[UpgradeNumpadKeys[i]].wasPressedThisFrame;
                if (!pressed) continue;

                if (chosen[i])
                {
                    chosen[i] = false;
                    count--;
                    SetChildActive(house, UpgradeNames[i], false);
                    changed = true;
                }
                else if (count < maxUpgradesPerHouse && buyer.money >= upgradePrice * (count + 1))
                {
                    chosen[i] = true;
                    count++;
                    SetChildActive(house, UpgradeNames[i], true);
                    changed = true;
                }
            }

            if (changed)
                ShowUpgradePrompt(chosen, count);

            yield return null;
        }

        int cost = count * upgradePrice;
        buyer.SubtractMoney(cost);
        houseUpgrades[tileIdx] = chosen;
        ui.UpdateMoneyUI(p1.money, p2.money);
    }

    bool CheckGameOver()
    {
        if (p1.money < 0)
        {
            TriggerGameOver("Player 2");
            return true;
        }
        if (p2.money < 0)
        {
            TriggerGameOver("Player 1");
            return true;
        }
        return false;
    }

    void TriggerGameOver(string winnerName)
    {
        SwitchCamera(false);
        isProcessingTurn = true;
        isWaitingInput = true;
        ui.ShowGameOver(winnerName);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // ---------- Texto da UI ----------

    void ShowBuyPrompt(int price, bool buyFocused)
    {
        string buyLabel = buyFocused ? "> BUY <" : "  Buy  ";
        string declineLabel = buyFocused ? "  Decline  " : "> DECLINE <";
        ui.houseText.text = $"House Available!\nPrice: {price}€\n\n{buyLabel}     {declineLabel}\nLeft/Right: choose   Enter: confirm";
    }

    void ShowUpgradePrompt(bool[] chosen, int count)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"House bought! Upgrades (max {maxUpgradesPerHouse})");
        for (int i = 0; i < UpgradeLabels.Length; i++)
            sb.AppendLine($"[{i + 1}] {(chosen[i] ? "[X]" : "[ ]")} {UpgradeLabels[i]}");
        sb.AppendLine($"Selected: {count}/{maxUpgradesPerHouse}  ({upgradePrice}€ each)");
        sb.Append("1-4: toggle   Enter: confirm");
        ui.houseText.text = sb.ToString();
    }

    static bool ConfirmPressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
    }

    // ---------- Casas no tabuleiro ----------

    // Guarda a pose da casa de referência em relação à casa 27.
    // Cada casa comprada usa a mesma pose relativa à sua própria casa (posição e rotação seguem a casa).
    void CaptureHouseTemplate()
    {
        if (houseTemplate == null)
        {
            GameObject go = GameObject.Find("House");
            if (go != null) houseTemplate = go.transform;
        }

        if (houseTemplate == null || tiles == null || tiles.Length == 0)
        {
            Debug.LogError("BoardTurnManager: casa de referência não encontrada.");
            return;
        }

        // A casa de referência é a casa do tabuleiro mais próxima dela (não depende do número/nome)
        int refIdx = FindNearestTile(houseTemplate.position);
        Debug.Log($"BoardTurnManager: casa de referência está na casa {refIdx + 1}.");

        referenceToHouse = tiles[refIdx].worldToLocalMatrix * houseTemplate.localToWorldMatrix;
        houseScale = houseTemplate.lossyScale;
        templateReady = true;

        // O modelo de referência só serve de guia; as casas aparecem quando são compradas
        houseTemplate.gameObject.SetActive(false);
    }

    int FindNearestTile(Vector3 point)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < tiles.Length; i++)
        {
            float d = (tiles[i].position - point).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    GameObject SpawnHouse(int index)
    {
        if (!templateReady)
        {
            Debug.LogWarning("BoardTurnManager: modelo da casa não está pronto, casa não criada.");
            return null;
        }

        Matrix4x4 pose = tiles[index].localToWorldMatrix * referenceToHouse;
        Vector3 position = new Vector3(pose.m03, pose.m13, pose.m23);
        Vector3 forward = new Vector3(pose.m02, pose.m12, pose.m22).normalized;
        Vector3 up = new Vector3(pose.m01, pose.m11, pose.m21).normalized;

        GameObject house = Instantiate(houseTemplate.gameObject);
        house.name = $"House_{index + 1}";

        Transform t = house.transform;
        t.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
        t.localScale = houseScale;
        house.SetActive(true);

        SetBaseOnly(house);
        ApplyColorToHouse(house, owners[index] == 1 ? p1Color : p2Color);

        spawnedHouses[index] = house;
        return house;
    }

    static void SetBaseOnly(GameObject house)
    {
        SetChildActive(house, BaseName, true);
        foreach (string upgrade in UpgradeNames)
            SetChildActive(house, upgrade, false);
    }

    static void SetChildActive(GameObject house, string childName, bool active)
    {
        if (house == null) return;

        Transform child = house.transform.Find(childName);
        if (child != null)
            child.gameObject.SetActive(active);
        else
            Debug.LogWarning($"BoardTurnManager: a casa '{house.name}' não tem o filho '{childName}'.");
    }

    void ApplyColorToHouse(GameObject houseObj, Color color)
    {
        Renderer[] renderers = houseObj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
            r.material.color = color;
    }

    // ---------- Jogadores e câmaras ----------

    void UpdatePlayerVisualPosition()
    {
        PlaceOnTile(p1);
        PlaceOnTile(p2);
    }

    void PlaceOnTile(PlayerController player)
    {
        if (player == null || tiles == null || tiles.Length == 0) return;

        player.gameObject.transform.position = StandingPosition(player.currentPosition, player);
    }

    // Posição onde o peão fica numa casa: no lado de fora da casa, longe do centro,
    // para nunca ficar por cima da casa (onde pode estar uma casa comprada).
    Vector3 StandingPosition(int tileIdx, PlayerController player)
    {
        Transform tile = tiles[tileIdx];

        Vector3 outward = tile.position - boardCenter;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;

        Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
        float lateral = player == p1 ? -lateralOffset : lateralOffset;

        return tile.position + outward * edgeDistance + side * lateral + Vector3.up * standHeight;
    }

    void SwitchCamera(bool toPlayer)
    {
        if (mainCamera != null)
            mainCamera.gameObject.SetActive(!toPlayer);

        if (p1 != null && p1.playerCamera != null)
            p1.playerCamera.gameObject.SetActive(toPlayer && isP1Turn);

        if (p2 != null && p2.playerCamera != null)
            p2.playerCamera.gameObject.SetActive(toPlayer && !isP1Turn);
    }
}
