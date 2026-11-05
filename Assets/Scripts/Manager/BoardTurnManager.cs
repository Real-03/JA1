using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;

public class BoardTurnManager : MonoBehaviour
{
    [Header("Players")]
    public PlayerController p1;
    public PlayerController p2;

    [Header("Player Colors")]
    public Color p1Color = Color.red;
    public Color p2Color = Color.blue;

    [Header("Posição do peão na casa")]
    public float edgeFraction = 0.5f;
    public float lateralFraction = 0.3f;

    [Header("Movimento (salto)")]
    public float hopDuration = 0.25f;
    public float hopHeight = 0.4f;

    [Header("Dados")]
    public float diceRestTime = 2f;
    public float diceMaxRollTime = 8f;
    public Transform diceSurface;

    [Header("Cameras")]
    public Camera mainCamera;

    [Header("References")]
    public UIManager ui;
    public Transform boardRoot;
    public Transform houseTemplate;
    public Transform diceTemplate;

    [Header("Game Settings")]
    public int startingMoney = 1500;
    public int lapBonus = 200;
    public int housePrice = 300;
    public int houseRent = 150;
    public int upgradePrice = 100;
    public int maxUpgradesPerHouse = 2;

    private static readonly string[] UpgradeNames = { "HouseAutism", "HouseBlind", "HouseDef", "HouseMobility" };
    private static readonly string[] UpgradeLabels = { "Autism", "Blind", "Def", "Mobility" };
    private static readonly Key[] UpgradeKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
    private static readonly Key[] UpgradeNumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4 };
    private const string BaseName = "HouseBase";
    private const int TilesPerSide = 8;

    private const int DiceCount = 2;
    private const float SpawnHeightInDice = 3f;
    private const float ThrowSpeedInDice = 2f;
    private const float RestLinearInDice = 0.05f;
    private const float RestAngularSpeed = 0.2f;

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

    private Matrix4x4 referenceToHouse;
    private Vector3 houseScale = Vector3.one;
    private Vector3 boardCenter;
    private Vector3 boardMin;
    private Vector3 boardMax;
    private bool templateReady = false;

    private Vector3[] tileCenters;
    private Vector3[] tileExtents;
    private float[] tileTops;
    private float p1FeetOffset;
    private float p2FeetOffset;

    private Rigidbody[] diceBodies;
    private Transform[][] diceFaces;
    private float diceSize = 0f;
    private float surfaceY;

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
        ComputeBoardBounds();
        MeasureFeetOffsets();
        SetupDice();
    }

    void Start()
    {
        if (tiles == null) return;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        p1.money = startingMoney;
        p2.money = startingMoney;
        ui.UpdateMoneyUI(p1.money, p2.money);

        if (ui.dicePanel != null)
            ui.dicePanel.SetActive(false);

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


    static bool IsPropertyIndex(int index)
    {
        int posInSide = index % TilesPerSide;
        return posInSide != 0 && posInSide != 4;
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


    void StartTurn()
    {
        if (diceBodies == null || diceBodies.Length != DiceCount)
        {
            Debug.LogError("BoardTurnManager: os dados não estão prontos, não é possível lançar.");
            return;
        }

        SoundManager.Instance.PlayDiceRoll();
        StartCoroutine(TurnFlow());
    }

    IEnumerator TurnFlow()
    {
        isWaitingInput = true;
        isProcessingTurn = true;

        int[] faces = null;
        yield return StartCoroutine(ThrowDice(result => faces = result));
        SoundManager.Instance.sfxSource.Stop();

        int diceValue = faces[0] + faces[1];

        PlayerController activePlayer = isP1Turn ? p1 : p2;
        int posBefore = activePlayer.currentPosition;
        Debug.Log($"BoardTurnManager: {(isP1Turn ? "P1" : "P2")} tirou {faces[0]} + {faces[1]} = {diceValue}, da casa {posBefore + 1} para a casa {(posBefore + diceValue) % tiles.Length + 1}.");

        foreach (int face in faces)
        {
            SoundManager.Instance.PlayDiceNarrator(face);
            yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);
        }

        SwitchCamera(true);
        yield return StartCoroutine(MovePlayer(activePlayer, diceValue));

        SoundManager.Instance.sfxSource.Stop();

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

    IEnumerator ChooseUpgrades(PlayerController buyer, int tileIdx, GameObject house)
    {
        bool[] chosen = new bool[UpgradeNames.Length];
        int count = 0;

        ui.buyButton.gameObject.SetActive(false);
        ui.declineButton.gameObject.SetActive(false);
        ui.housePanel.SetActive(true);
        ShowUpgradePrompt(chosen, count);

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


    void SetupDice()
    {
        if (diceTemplate == null)
        {
            GameObject go = GameObject.Find("Dice");
            if (go != null) diceTemplate = go.transform;
        }

        if (diceTemplate == null)
        {
            Debug.LogError("BoardTurnManager: modelo do dado 'Dice' não encontrado.");
            return;
        }

        diceBodies = new Rigidbody[DiceCount];
        diceFaces = new Transform[DiceCount][];

        for (int i = 0; i < DiceCount; i++)
        {
            GameObject die = Instantiate(diceTemplate.gameObject);
            die.name = $"Dice_{i + 1}";
            die.SetActive(true);

            diceBodies[i] = PrepareDie(die.transform);
            diceFaces[i] = FindFaces(die.transform);
        }

        diceTemplate.gameObject.SetActive(false);

        surfaceY = FindSurfaceHeight();
        CreateDiceFloor();
    }

    float FindSurfaceHeight()
    {
        if (diceSurface == null)
        {
            diceSurface = boardRoot.Find("Middle");
            if (diceSurface == null) diceSurface = boardRoot.Find("Base");
        }

        if (diceSurface == null)
        {
            Debug.LogWarning("BoardTurnManager: não encontrei 'Middle' nem 'Base' no tabuleiro; os dados caem à altura do topo das casas.");
            return boardMax.y;
        }

        float y = diceSurface.position.y;
        Renderer[] renderers = diceSurface.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            y = float.NegativeInfinity;
            foreach (Renderer r in renderers)
                y = Mathf.Max(y, r.bounds.max.y);
        }

        Debug.Log($"BoardTurnManager: superfície dos dados em '{diceSurface.name}', altura {y}.");
        return y;
    }

    Rigidbody PrepareDie(Transform die)
    {
        LocalBounds(die, out Vector3 min, out Vector3 max);

        BoxCollider box = die.GetComponent<BoxCollider>();
        if (box == null) box = die.gameObject.AddComponent<BoxCollider>();
        box.center = (min + max) * 0.5f;
        box.size = max - min;

        Rigidbody rb = die.GetComponent<Rigidbody>();
        if (rb == null) rb = die.gameObject.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.maxAngularVelocity = 50f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.isKinematic = true;

        Vector3 worldSize = Vector3.Scale(box.size, die.lossyScale);
        diceSize = Mathf.Max(diceSize, Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z)));

        return rb;
    }

    static void LocalBounds(Transform root, out Vector3 min, out Vector3 max)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;

        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;

            Bounds mb = mf.sharedMesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3(
                    (c & 1) != 0 ? mb.max.x : mb.min.x,
                    (c & 2) != 0 ? mb.max.y : mb.min.y,
                    (c & 4) != 0 ? mb.max.z : mb.min.z);

                Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        if (min.x > max.x)
        {
            Debug.LogError($"BoardTurnManager: o dado '{root.name}' não tem malhas.");
            min = max = Vector3.zero;
        }
    }

    static Transform[] FindFaces(Transform die)
    {
        Transform[] faces = new Transform[6];

        foreach (Transform t in die.GetComponentsInChildren<Transform>())
        {
            if (t == die) continue;

            for (int f = 0; f < faces.Length; f++)
            {
                if (faces[f] == null && t.name == (f + 1).ToString())
                    faces[f] = t;
            }
        }

        for (int f = 0; f < faces.Length; f++)
        {
            if (faces[f] == null)
                Debug.LogError($"BoardTurnManager: falta a face {f + 1} no dado '{die.name}'.");
        }

        return faces;
    }

    void ComputeBoardBounds()
    {
        boardMin = Vector3.positiveInfinity;
        boardMax = Vector3.negativeInfinity;

        tileCenters = new Vector3[tiles.Length];
        tileExtents = new Vector3[tiles.Length];
        tileTops = new float[tiles.Length];

        for (int i = 0; i < tiles.Length; i++)
        {
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;

            Renderer[] renderers = tiles[i].GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                min = tiles[i].position;
                max = tiles[i].position;
            }

            foreach (Renderer r in renderers)
            {
                min = Vector3.Min(min, r.bounds.min);
                max = Vector3.Max(max, r.bounds.max);
            }

            tileCenters[i] = (min + max) * 0.5f;
            tileExtents[i] = (max - min) * 0.5f;
            tileTops[i] = max.y;

            boardMin = Vector3.Min(boardMin, min);
            boardMax = Vector3.Max(boardMax, max);
        }
    }

    void MeasureFeetOffsets()
    {
        p1FeetOffset = FeetOffset(p1);
        p2FeetOffset = FeetOffset(p2);
    }

    static float FeetOffset(PlayerController player)
    {
        if (player == null) return 0f;

        Transform t = player.gameObject.transform;
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        float lowest = float.PositiveInfinity;
        foreach (Renderer r in renderers)
            lowest = Mathf.Min(lowest, r.bounds.min.y);

        return t.position.y - lowest;
    }

    void CreateDiceFloor()
    {
        float margin = diceSize * 2f;
        Vector3 center = (boardMin + boardMax) * 0.5f;
        Vector3 size = (boardMax - boardMin) + new Vector3(margin * 2f, 0f, margin * 2f);

        GameObject floor = new GameObject("DiceFloor");
        floor.transform.position = new Vector3(center.x, surfaceY - 0.5f, center.z);

        BoxCollider col = floor.AddComponent<BoxCollider>();
        col.size = new Vector3(size.x, 1f, size.z);
    }

    IEnumerator ThrowDice(System.Action<int[]> onDone)
    {
        for (int i = 0; i < DiceCount; i++)
            LaunchDie(i);

        float elapsed = 0f;
        float restingTime = 0f;
        bool timedOut = false;

        while (true)
        {
            yield return null;
            elapsed += Time.deltaTime;

            bool allResting = true;
            for (int i = 0; i < DiceCount; i++)
            {
                Rigidbody rb = diceBodies[i];

                if (IsOutOfBoard(rb.position))
                {
                    Debug.Log($"BoardTurnManager: {rb.name} saiu do tabuleiro, é lançado de novo.");
                    LaunchDie(i);
                    allResting = false;
                }
                else if (!IsResting(rb))
                {
                    allResting = false;
                }
            }

            restingTime = allResting ? restingTime + Time.deltaTime : 0f;

            if (restingTime >= diceRestTime)
                break;

            if (elapsed >= diceMaxRollTime)
            {
                timedOut = true;
                break;
            }
        }

        if (timedOut)
            Debug.LogWarning("BoardTurnManager: os dados não pararam a tempo, conta as faces que estão para cima.");

        int[] faces = new int[DiceCount];
        for (int i = 0; i < DiceCount; i++)
            faces[i] = ReadTopFace(i);

        onDone(faces);
    }

    void LaunchDie(int i)
    {
        Rigidbody rb = diceBodies[i];

        Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * diceSize;
        Vector3 spawn = new Vector3(boardCenter.x, surfaceY + diceSize * SpawnHeightInDice, boardCenter.z) + offset;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = spawn;
        rb.rotation = Random.rotation;
        rb.transform.SetPositionAndRotation(rb.position, rb.rotation);
        Physics.SyncTransforms();
        rb.WakeUp();

        Vector3 horizontal = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
        rb.linearVelocity = horizontal * (diceSize * ThrowSpeedInDice) + Vector3.up * diceSize;
        rb.angularVelocity = Random.insideUnitSphere * 20f;
    }

    bool IsResting(Rigidbody rb)
    {
        float linearLimit = diceSize * RestLinearInDice;
        return rb.linearVelocity.sqrMagnitude < linearLimit * linearLimit
            && rb.angularVelocity.sqrMagnitude < RestAngularSpeed * RestAngularSpeed;
    }

    bool IsOutOfBoard(Vector3 p)
    {
        float margin = diceSize * 2f;
        return p.x < boardMin.x - margin || p.x > boardMax.x + margin
            || p.z < boardMin.z - margin || p.z > boardMax.z + margin
            || p.y < surfaceY - margin;
    }

    int ReadTopFace(int dieIndex)
    {
        Transform[] faces = diceFaces[dieIndex];

        Vector3[] centers = new Vector3[faces.Length];
        Vector3 average = Vector3.zero;
        for (int f = 0; f < faces.Length; f++)
        {
            centers[f] = faces[f] != null ? FaceCenter(faces[f]) : diceBodies[dieIndex].position;
            average += centers[f];
        }
        average /= faces.Length;

        int top = 1;
        float best = float.NegativeInfinity;
        for (int f = 0; f < faces.Length; f++)
        {
            if (faces[f] == null) continue;

            float dot = Vector3.Dot((centers[f] - average).normalized, Vector3.up);
            if (dot > best)
            {
                best = dot;
                top = f + 1;
            }
        }

        return top;
    }

    static Vector3 FaceCenter(Transform face)
    {
        Renderer[] renderers = face.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return face.position;

        Vector3 sum = Vector3.zero;
        foreach (Renderer r in renderers) sum += r.bounds.center;
        return sum / renderers.Length;
    }


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

        int refIdx = FindNearestTile(houseTemplate.position);
        Debug.Log($"BoardTurnManager: casa de referência está na casa {refIdx + 1}.");

        referenceToHouse = tiles[refIdx].worldToLocalMatrix * houseTemplate.localToWorldMatrix;
        houseScale = houseTemplate.lossyScale;
        templateReady = true;

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

    Vector3 StandingPosition(int tileIdx, PlayerController player)
    {
        Vector3 center = tileCenters[tileIdx];
        Vector3 extents = tileExtents[tileIdx];

        Vector3 outward = new Vector3(center.x - boardCenter.x, 0f, center.z - boardCenter.z);
        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;

        float reachOut = Mathf.Abs(outward.x) * extents.x + Mathf.Abs(outward.z) * extents.z;
        float reachSide = Mathf.Abs(side.x) * extents.x + Mathf.Abs(side.z) * extents.z;
        float lateral = (player == p1 ? -1f : 1f) * lateralFraction * reachSide;

        Vector3 xz = new Vector3(center.x, 0f, center.z) + outward * (reachOut * edgeFraction) + side * lateral;
        float feet = player == p1 ? p1FeetOffset : p2FeetOffset;

        return new Vector3(xz.x, tileTops[tileIdx] + feet, xz.z);
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
