using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;       // ← for Touchscreen
using TMPro;
using MoreMountains.Feedbacks;

public class GameManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public GameObject ballPrefab;
    public GameObject aiPrefab;

    [Header("Lose Prefabs (drag here)")]
    public GameObject playerLosePrefab;
    public GameObject aiLosePrefab;

    [Header("Reveal Picks UI")]
    [Tooltip("Prefab for showing player's chosen cell prior to resolution.")]
    public GameObject revealMarkerPlayerPrefab;
    [Tooltip("Prefab for showing AI's chosen cell prior to resolution.")]
    public GameObject revealMarkerAIPrefab;
    [Tooltip("Delay between player reveal and AI reveal.")]
    public float revealStaggerDelay = 0.2f;

    [Header("Penalty UI (Canvas)")]
    public GameObject penaltyPanel;
    public Button[] penaltyButtons;
    public RectTransform penaltyBall;
    public Image goalkeeperImage;
    public Sprite playerGKIdleSprite;
    public Sprite playerGKJumpSprite;
    public Sprite aiGKIdleSprite;
    public Sprite aiGKJumpSprite;

    [Header("Penalty GK Position")]
    public RectTransform goalkeeperIdleAnchor;

    [Header("UI Text")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI potText;
    public TextMeshProUGUI messageText;

    [Header("Modifier UI")]
    [Tooltip("Separate text field to display modifier alerts")]
    public TextMeshProUGUI modifierText;

    [Header("Bet UI")]
    public GameObject betPanel;
    public Button bet5Button;
    public Button bet10Button;
    public Button bet20Button;
    public Button restartButton;

    [Header("Bet Settings")]
    public int startingGold = 20;
    public int bonusPerAdvance = 1;

    [Header("Animation Settings")]
    public float tackleAnimDuration = 0.5f;
    public float penaltyAnimDuration = 0.5f;
    public float goalkeeperJumpDuration = 0.5f;
    public float afterAnimDelay = 0.5f;
    public Vector3 penaltyBallStartScale = Vector3.one;
    public Vector3 penaltyBallEndScale   = Vector3.one * 0.5f;

    [Header("Feel Feedbacks")]
    public MMFeedbacks feedbackPlayerAdvance;
    public MMFeedbacks feedbackOpponentAdvance;
    public MMFeedbacks feedbackTackleWin;
    public MMFeedbacks feedbackTackleLose;
    public MMFeedbacks feedbackPenaltySaved;
    public MMFeedbacks feedbackPenaltyCounter;
    public MMFeedbacks feedbackGoalForPlayer;
    public MMFeedbacks feedbackGoalAgainst;
    public MMFeedbacks feedbackMatchWin;
    public MMFeedbacks feedbackMatchLose;

    [Header("Power-Up (assign in Inspector)")]
    public PowerUpManager powerUpManager;
    public PowerUpSpawner powerUpSpawner;

    [Header("Match Modifiers (assign in Inspector)")]
    public MatchModifierManager matchModifierManager;
    public Transform modifierIconsContainer;
    public GameObject modifierIconPrefab;
    public GameObject rulesPanel;
    public TextMeshProUGUI rulesText;
    public Button rulesButton;

    [Header("Movement Settings")]
    [Tooltip("When true, only adjacent+diagonal moves are allowed; when false, all columns are valid.")]
    public bool restrictToAdjacent = true;

    [Header("Feature Toggles")]
    [Tooltip("Turn off to disable all power-up spawning and effects.")]
    public bool enablePowerUps = true;
    [Tooltip("Turn off to disable all match modifiers and their effects.")]
    public bool enableModifiers = true;

    // private state
    private int playerGold, currentBet, pot;
    private GameObject ballInstance;
    private BallController ballCtrl;
    private int ballRow, ballCol;

    public enum Actor { Player, AI }
    private Actor possession;

    private enum Phase { PlayerAttack, AwaitingDefense }
    private Phase phase;

    private int attackChoice, defendChoice;

    private bool _inputLocked = false;

    // Penalty state
    private Actor penaltyAttacker;
    private bool penaltyChoiceMade;
    private int penaltyAttackChoice, penaltyDefendChoice;
    private Vector2 penaltyBallStartPos;
    private Vector3 goalkeeperBaseScale;

    private Coroutine _clearMsgCoroutine;
    private Coroutine _clearModifierCoroutine;
    private List<int> _allowedColumns = new List<int>();
    private List<GameObject> _revealMarkers = new List<GameObject>();

    // ——— NEW: map each grid‐column to whichever buttons you want enabled ———
    [System.Serializable]
    public struct PenaltyColumnMapping
    {
        [Tooltip("Grid column you attacked from (0 = leftmost)")]
        public int columnIndex;
        [Tooltip("Drag in the penalty Buttons you want enabled for that column")]
        public Button[] allowedPenaltyButtons;
    }

    [Header("Penalty → column-to-button mappings")]
    [Tooltip("For each mapping, set columnIndex and drop in the Buttons that should be interactable")]
    public PenaltyColumnMapping[] penaltyColumnMappings;

    // … the rest of your GameManager follows unchanged …


    private void ClearFieldPowerUps()
    {
        var pickups = Object.FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None);
        foreach (var pu in pickups)
            Destroy(pu.gameObject);
    }

    void Start()
    {
        if (rulesButton != null && rulesPanel != null)
        {
            rulesPanel.SetActive(false);
            rulesButton.onClick.AddListener(() => rulesPanel.SetActive(!rulesPanel.activeSelf));
        }

        penaltyBallStartPos = penaltyBall.anchoredPosition;
        goalkeeperBaseScale = goalkeeperImage.rectTransform.localScale;

        // Initialize grid cells
        for (int r = 0; r < gridManager.rows; r++)
            for (int c = 0; c < gridManager.cols; c++)
                gridManager.cells[r, c].GetComponent<Cell>().Initialize(r, c, this);

        playerGold = startingGold;
        UpdateGoldUI();
        messageText.text = "";
        modifierText.text = "";

        bet5Button.onClick.AddListener(() => OnBetSelected(5));
        bet10Button.onClick.AddListener(() => OnBetSelected(10));
        bet20Button.onClick.AddListener(() => OnBetSelected(20));
        restartButton.onClick.AddListener(OnRestart);
        restartButton.gameObject.SetActive(false);

        betPanel.SetActive(true);
        DisableGrid();
    }

    void SpawnCharacter()
    {
        if (ballInstance != null) Destroy(ballInstance);
        var prefab = possession == Actor.Player ? ballPrefab : aiPrefab;
        ballInstance = Instantiate(prefab,
            gridManager.GetCellPosition(ballRow, ballCol),
            Quaternion.identity);
        ballCtrl = ballInstance.GetComponent<BallController>();
    }

    void OnBetSelected(int amount)
    {
        ClearFieldPowerUps();

        if (enableModifiers)
        {
            matchModifierManager.PickRandomModifiers();
            PopulateModifiersUI();
        }
        rulesPanel?.SetActive(false);

        possession = Random.value < 0.5f ? Actor.Player : Actor.AI;
        ballRow = gridManager.rows / 2;
        ballCol = gridManager.cols / 2;

        penaltyPanel.SetActive(false);
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        foreach (var btn in penaltyButtons)
            btn.GetComponent<Image>().color = Color.white;

        ClearHighlights();

        if (amount > playerGold)
        {
            ShowMessage("Not enough gold!", 1f);
            return;
        }

        currentBet = amount;
        playerGold -= amount;
        UpdateGoldUI();

        pot = currentBet * 2;
        UpdatePotUI();

        ShowMessage(possession == Actor.Player ? "You Kick-Off" : "Opponent Kick-Off", 4f);

        betPanel.SetActive(false);
        bet5Button.gameObject.SetActive(false);
        bet10Button.gameObject.SetActive(false);
        bet20Button.gameObject.SetActive(false);

        SpawnCharacter();
        EnableGrid();

        if (enablePowerUps)
            powerUpSpawner.SpawnDrops();

        StartNewTurn();
    }

   void StartNewTurn()
{
    _inputLocked = false;
    int targetRow = possession == Actor.Player ? ballRow + 1 : ballRow - 1;

    // 1) Highlight allowed row cells and populate _allowedColumns
    if (possession == Actor.Player)
    {
        phase = Phase.PlayerAttack;
        HighlightRow(targetRow, Actor.Player);
    }
    else
    {
        phase = Phase.AwaitingDefense;
        HighlightRow(targetRow, Actor.AI);
        attackChoice = AIAttackGuess();
    }

    // 2) If Flight Path is on, choose fast-lane from those same allowed columns
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
        && _allowedColumns.Count > 0)
    {
        int fastFromAllowed = _allowedColumns[Random.Range(0, _allowedColumns.Count)];
        matchModifierManager.SetFastLaneColumn(fastFromAllowed);
    }

    // 3) Now highlight the fast-lane cell (if any)
    int fastCol = matchModifierManager.GetFastLaneColumn();
    if (fastCol >= 0 && targetRow >= 0 && targetRow < gridManager.rows)
    {
        var fastCell = gridManager.cells[targetRow, fastCol].GetComponent<Cell>();
        fastCell.Highlight(true);
        var fastSr = fastCell.GetComponent<SpriteRenderer>();
        fastSr.color = new Color(0f, 1f, 1f, 0.5f);
    }

    // 4) Highlight the locked column in red (if LockedColumn modifier is active)
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.LockedColumn))
    {
        int lockedCol = matchModifierManager.GetLockedColumn();
        if (lockedCol >= 0 && targetRow >= 0 && targetRow < gridManager.rows)
        {
            var lockCell = gridManager.cells[targetRow, lockedCol].GetComponent<Cell>();
            // pulse it too, so it gets the same animation
            lockCell.Highlight(true);
            var lockSr = lockCell.GetComponent<SpriteRenderer>();
            lockSr.color = new Color(1f, 0f, 0f, 0.5f);
        }
    }
}



   public void OnCellClicked(int r, int c)
{
    // ignore taps if we’re mid‐resolution or showing the bet UI
    if (_inputLocked || betPanel.activeSelf) 
        return;

    int tr = possession == Actor.Player ? ballRow + 1 : ballRow - 1;
    if (r != tr || (_allowedColumns.Count > 0 && !_allowedColumns.Contains(c)))
        return;

    // lock out any further clicks until this turn fully resolves
    _inputLocked = true;

    if (possession == Actor.Player && phase == Phase.PlayerAttack)
    {
        attackChoice = c;
        defendChoice = AI_DefenseGuess();
        StartCoroutine(ResolveTurn(tr));
    }
    else if (possession == Actor.AI && phase == Phase.AwaitingDefense)
    {
        defendChoice = c;
        StartCoroutine(ResolveTurn(tr));
    }
}


    private IEnumerator ResolveTurn(int targetRow)
    {
        Actor attacker = possession;
        bool tackle = attackChoice == defendChoice;

        // 1) Reveal picks
        int playerPick = possession == Actor.Player ? attackChoice : defendChoice;
        int aiPick = possession == Actor.Player ? defendChoice : attackChoice;

        if (revealMarkerPlayerPrefab != null)
        {
            var pm = Instantiate(
                revealMarkerPlayerPrefab,
                gridManager.GetCellPosition(targetRow, playerPick),
                Quaternion.identity);
            _revealMarkers.Add(pm);
        }
        yield return new WaitForSeconds(revealStaggerDelay);

        if (revealMarkerAIPrefab != null)
        {
            var am = Instantiate(
                revealMarkerAIPrefab,
                gridManager.GetCellPosition(targetRow, aiPick),
                Quaternion.identity);
            _revealMarkers.Add(am);
        }
        yield return new WaitForSeconds(revealStaggerDelay);
        ClearRevealMarkers();

        // 2) Mirror Clash (modifier)
        if (!tackle
            && enableModifiers
            && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MirrorClash)
            && matchModifierManager.IsMirrorClash(attackChoice, defendChoice))
        {
            matchModifierManager.ApplyMirrorClash(ref ballRow, attacker);
            ShowModifier("Mirror Clash! Ball moves back!", 2f);
            yield return new WaitForSeconds(afterAnimDelay);
            ClearHighlights();
            yield return ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol));
            StartNewTurn();
            yield break;
        }

        // 3) Momentum Limit (modifier)
        if (!tackle
            && enableModifiers
            && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit)
            && !matchModifierManager.CanAdvance())
        {
            tackle = true;
            ShowModifier("Momentum Limit! Must be tackled!", 2f);
            (attacker == Actor.Player ? feedbackTackleLose : feedbackTackleWin)?.PlayFeedbacks();
            matchModifierManager.OnTackle();
        }

        // 4) Tackle or Dribble (gameplay)
        if (tackle)
        {
            ballRow = targetRow;
            ballCol = attackChoice;
            yield return ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol));

            ShowMessage(attacker == Actor.Player ? "Tackled!" : "Tackle!", 2f);
            (attacker == Actor.Player ? feedbackTackleLose : feedbackTackleWin)?.PlayFeedbacks();
            if (enableModifiers) matchModifierManager.OnTackle();

            var loserPrefab = attacker == Actor.Player ? aiLosePrefab : playerLosePrefab;
            var loser = Instantiate(loserPrefab, gridManager.GetCellPosition(ballRow, ballCol), Quaternion.identity);
            StartCoroutine(ThrowOffScreen(loser, attacker));

            possession = attacker == Actor.Player ? Actor.AI : Actor.Player;
            SpawnCharacter();
            StartNewTurn();
            yield break;
        }
        else
        {
            if (enableModifiers) matchModifierManager.OnAdvance();
            ShowMessage(attacker == Actor.Player ? "Dribble!" : "Dribbled!", 2f);
            (attacker == Actor.Player ? feedbackPlayerAdvance : feedbackOpponentAdvance)?.PlayFeedbacks();
            pot += bonusPerAdvance;
            UpdatePotUI();
        }

        // 5) Column Loyalty (modifier)
        int loyaltyBoost = 0;
        if (!tackle
            && enableModifiers
            && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.ColumnLoyalty))
        {
            loyaltyBoost = matchModifierManager.GetLoyaltyBoost(attacker, attackChoice);
            if (loyaltyBoost > 0)
                ShowModifier("Column Loyalty! Extra row!", 2f);
        }

        // 6) Flight Path boost (modifier)
        int flightBoost = 0;
        if (!tackle
            && enableModifiers
            && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
            && attackChoice == matchModifierManager.GetFastLaneColumn())
        {
            flightBoost = 1;
            ShowModifier("Flight Path! Double Advance", 2f);
        }

        // 7) Burned Column update
        if (enableModifiers)
            matchModifierManager.SetLastUsedColumn(attacker, attackChoice);

        yield return new WaitForSeconds(tackleAnimDuration);
        ClearHighlights();

        // 8) Advance with boosts
        {
            int dir = attacker == Actor.Player ? +1 : -1;
            int totalBoost = loyaltyBoost + flightBoost;
            int newRow = ballRow + dir + (totalBoost * dir);
            ballRow = Mathf.Clamp(newRow, 0, gridManager.rows - 1);
            ballCol = attackChoice;
        }

        yield return ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol));

        if (enablePowerUps)
            CheckForPickups();

        // 9) Check goal
        bool goal = !tackle &&
                    ((attacker == Actor.Player && ballRow == gridManager.rows - 1) ||
                     (attacker == Actor.AI && ballRow == 0));
        if (goal)
        {
            StartCoroutine(PenaltySequence(attacker));
            yield break;
        }

        // 10) Next turn
        StartNewTurn();
    }

    private void Update()
    {
        // on touch begin, raycast and forward to OnCellClicked
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(screenPos);
            var hit = Physics2D.Raycast(worldPoint, Vector2.zero);
            if (hit.collider != null)
            {
                var cell = hit.collider.GetComponent<Cell>();
                if (cell != null)
                    OnCellClicked(cell.row, cell.col);
            }
        }
    }

    private void ShowMessage(string msg, float duration)
    {
        if (_clearMsgCoroutine != null) StopCoroutine(_clearMsgCoroutine);
        messageText.text = msg;
        _clearMsgCoroutine = StartCoroutine(ClearAfter(duration));
    }

    private IEnumerator ClearAfter(float t)
    {
        yield return new WaitForSeconds(t);
        messageText.text = "";
        _clearMsgCoroutine = null;
    }

    private void ShowModifier(string msg, float duration)
    {
        if (_clearModifierCoroutine != null) StopCoroutine(_clearModifierCoroutine);
        modifierText.text = msg;
        _clearModifierCoroutine = StartCoroutine(ClearModifierAfter(duration));
    }

    private IEnumerator ClearModifierAfter(float t)
    {
        yield return new WaitForSeconds(t);
        modifierText.text = "";
        _clearModifierCoroutine = null;
    }

    private void ClearRevealMarkers()
    {
        foreach (var m in _revealMarkers) Destroy(m);
        _revealMarkers.Clear();
    }

    private void CheckForPickups()
    {
        var hits = Physics2D.OverlapCircleAll(ballInstance.transform.position, 0.1f);
        foreach (var hit in hits)
            if (hit.TryGetComponent<PowerUpPickup>(out var pu))
                pu.ManualPickup(ballInstance);
    }

    private IEnumerator ThrowOffScreen(GameObject loser, Actor attacker)
    {
        float elapsed = 0f;
        Vector3 start = loser.transform.position;
        Vector3 dir = attacker == Actor.Player ? Vector3.down : Vector3.up;
        Vector3 end = start + dir * (gridManager.rows + 1);

        while (elapsed < tackleAnimDuration)
        {
            elapsed += Time.deltaTime;
            loser.transform.position = Vector3.Lerp(start, end, elapsed / tackleAnimDuration);
            yield return null;
        }

        Destroy(loser);
    }

private IEnumerator PenaltySequence(Actor attacker)
{
    penaltyAttacker = attacker;
    penaltyChoiceMade = false;
    penaltyAttackChoice = penaltyDefendChoice = -1;

    // bring up the panel & disable normal grid input
    penaltyPanel.SetActive(true);
    DisableGrid();

    // reset ball & keeper visuals
    penaltyBall.anchoredPosition = penaltyBallStartPos;
    penaltyBall.localScale       = penaltyBallStartScale;
    penaltyBall.gameObject.SetActive(true);
    goalkeeperImage.sprite       = (attacker == Actor.Player) ? aiGKIdleSprite : playerGKIdleSprite;
    goalkeeperImage.rectTransform.anchoredPosition = goalkeeperIdleAnchor.anchoredPosition;
    goalkeeperImage.rectTransform.localScale       = goalkeeperBaseScale;
    goalkeeperImage.gameObject.SetActive(true);

    // — 1) find the mapping for the column we attacked from —
    var mapping = penaltyColumnMappings
        .FirstOrDefault(m => m.columnIndex == ballCol);

    // build a list of button-indices we want to enable
    var allowedIndices = new List<int>();
    if (mapping.allowedPenaltyButtons != null && mapping.allowedPenaltyButtons.Length > 0)
    {
        foreach (var btn in mapping.allowedPenaltyButtons)
        {
            int idx = System.Array.IndexOf(penaltyButtons, btn);
            if (idx >= 0) allowedIndices.Add(idx);
        }
    }
    // fallback to “all” if none configured
    if (allowedIndices.Count == 0)
        allowedIndices = Enumerable.Range(0, penaltyButtons.Length).ToList();

    // — 2) enable/disable & wire up each button —
    for (int i = 0; i < penaltyButtons.Length; i++)
    {
        var btn = penaltyButtons[i];
        var img = btn.GetComponent<Image>();

        if (allowedIndices.Contains(i))
        {
            btn.interactable = true;
            img.color       = Color.white;
        }
        else
        {
            btn.interactable = false;
            img.color        = new Color(1f, 1f, 1f, 0.3f);
        }

        btn.onClick.RemoveAllListeners();
        int idx = i;
        btn.onClick.AddListener(() => OnPenaltyButton(idx));
    }

    // — 3) AI picks from those same indices —
    if (attacker == Actor.Player)
        penaltyDefendChoice = allowedIndices[Random.Range(0, allowedIndices.Count)];
    else
        penaltyAttackChoice = allowedIndices[Random.Range(0, allowedIndices.Count)];

    // wait for player/AI choice…
    while (!penaltyChoiceMade)
        yield return null;

    // …then the rest of your animation code unchanged…
    var shootPos = penaltyButtons[penaltyAttackChoice].GetComponent<RectTransform>().anchoredPosition;
    var defPos   = penaltyButtons[penaltyDefendChoice].GetComponent<RectTransform>().anchoredPosition;
    var idlePos  = goalkeeperIdleAnchor.anchoredPosition;

    // flip keeper
    var flipScale = goalkeeperBaseScale;
    flipScale.x = defPos.x < idlePos.x
        ? -Mathf.Abs(flipScale.x)
        : Mathf.Abs(flipScale.x);
    goalkeeperImage.rectTransform.localScale = flipScale;
    goalkeeperImage.sprite = (attacker == Actor.Player) ? aiGKJumpSprite : playerGKJumpSprite;

    float t = 0f;
    while (t < penaltyAnimDuration)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / penaltyAnimDuration);
        penaltyBall.anchoredPosition = Vector2.Lerp(penaltyBallStartPos, shootPos, p);
        penaltyBall.localScale       = Vector3.Lerp(penaltyBallStartScale, penaltyBallEndScale, p);
        float k = Mathf.Clamp01(t / goalkeeperJumpDuration);
        goalkeeperImage.rectTransform.anchoredPosition = Vector2.Lerp(idlePos, defPos, k);
        yield return null;
    }

    yield return new WaitForSeconds(afterAnimDelay);

    // color results
    penaltyButtons[penaltyAttackChoice].GetComponent<Image>().color = Color.green;
    penaltyButtons[penaltyDefendChoice].GetComponent<Image>().color   = Color.red;

    bool saved = (penaltyAttackChoice == penaltyDefendChoice);
    if (saved)
    {
        ShowModifier(attacker == Actor.Player ? "Countered" : "Saved", 2f);
        (attacker == Actor.Player ? feedbackPenaltyCounter : feedbackPenaltySaved)?.PlayFeedbacks();
        possession = (attacker == Actor.Player) ? Actor.AI : Actor.Player;

        _inputLocked = false;  // unlock

        penaltyBall.gameObject.SetActive(false);
        penaltyPanel.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);

        EnableGrid();
        SpawnCharacter();
        StartNewTurn();
    }
    else
    {
        if (attacker == Actor.Player)
        {
            playerGold += pot;
            UpdateGoldUI();
            ShowMessage("GOAAAAAL! You Win!", 3f);
            feedbackGoalForPlayer?.PlayFeedbacks();
            feedbackMatchWin?.PlayFeedbacks();
        }
        else
        {
            ShowMessage("GOAAAAAL! You Lose!", 3f);
            feedbackGoalAgainst?.PlayFeedbacks();
            feedbackMatchLose?.PlayFeedbacks();
        }

        penaltyBall.gameObject.SetActive(false);
        penaltyPanel.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);

        _inputLocked = false;  // unlock
        EndMatch();
    }
}



    public void OnPenaltyButton(int idx)
    {
        penaltyChoiceMade = true;
        if (penaltyAttacker == Actor.Player) penaltyAttackChoice = idx;
        else                                  penaltyDefendChoice = idx;
    }

    private void HighlightRow(int tr, Actor attacker)
    {
        ClearHighlights();
        _allowedColumns.Clear();
        if (tr < 0 || tr >= gridManager.rows) return;

        var baseCols = restrictToAdjacent
            ? GetAdjacentColumns()
            : Enumerable.Range(0, gridManager.cols).ToList();

        if (enablePowerUps)
            baseCols = baseCols.FindAll(c => powerUpManager.GetAllowedColumns(attacker.ToString()).Contains(c));

        if (enableModifiers && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn))
            baseCols.Remove(matchModifierManager.GetLastUsedColumn(attacker));

        if (enableModifiers && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.LockedColumn))
            baseCols.Remove(matchModifierManager.GetLockedColumn());

        foreach (int c in baseCols)
        {
            _allowedColumns.Add(c);
            gridManager.cells[tr, c].GetComponent<Cell>().Highlight(true);
        }
    }

    private void ClearHighlights()
    {
        foreach (var cellGO in gridManager.AllCells)
            cellGO.GetComponent<Cell>().Highlight(false);
    }

    private List<int> GetAdjacentColumns()
    {
        var adj = new List<int> { ballCol };
        if (ballCol - 1 >= 0) adj.Add(ballCol - 1);
        if (ballCol + 1 < gridManager.cols) adj.Add(ballCol + 1);
        return adj;
    }

    private int AIAttackGuess() =>
        _allowedColumns.Count > 0
            ? _allowedColumns[Random.Range(0, _allowedColumns.Count)]
            : Random.Range(0, gridManager.cols);

    private int AI_DefenseGuess()
{
    // if we’re restricting to adjacent (or any other modifier-filtered)
    // and we actually have a non-empty allowed list, pick from it:
    if (restrictToAdjacent && _allowedColumns.Count > 0)
        return _allowedColumns[Random.Range(0, _allowedColumns.Count)];
    // otherwise fall back to any column
    return Random.Range(0, gridManager.cols);
}

    private void UpdatePotUI() => potText.text = $"Pot: {pot}";
    private void UpdateGoldUI() => goldText.text = $"Gold: {playerGold}";

    private void EndMatch()
    {
        DisableGrid();
        restartButton.gameObject.SetActive(false);
        betPanel.SetActive(true);
        bet5Button.gameObject.SetActive(true);
        bet10Button.gameObject.SetActive(true);
        bet20Button.gameObject.SetActive(true);
    }

    public void OnRestart()
    {
        _inputLocked = false;
        restartButton.gameObject.SetActive(false);
        messageText.text = "";
        modifierText.text = "";
        possession = Random.value < 0.5f ? Actor.Player : Actor.AI;
        ballRow = gridManager.rows / 2;
        ballCol = gridManager.cols / 2;

        penaltyPanel.SetActive(false);
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        foreach (var btn in penaltyButtons)
            btn.GetComponent<Image>().color = Color.white;

        SpawnCharacter();
        betPanel.SetActive(true);
        bet5Button.gameObject.SetActive(true);
        bet10Button.gameObject.SetActive(true);
        bet20Button.gameObject.SetActive(true);
    }

    private void DisableGrid()
    {
        foreach (var cellGO in gridManager.AllCells)
            cellGO.GetComponent<Collider2D>().enabled = false;
    }

    private void EnableGrid()
    {
        foreach (var cellGO in gridManager.AllCells)
            cellGO.GetComponent<Collider2D>().enabled = true;
    }

    private void PopulateModifiersUI()
    {
        foreach (Transform child in modifierIconsContainer)
            Destroy(child.gameObject);

        foreach (var mod in matchModifierManager.activeModifiers)
        {
            var go = Instantiate(modifierIconPrefab, modifierIconsContainer);
            var img = go.GetComponent<Image>();
            if (img != null) img.sprite = mod.icon;
        }

        if (rulesText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var mod in matchModifierManager.activeModifiers)
                sb.AppendLine($"<b>{mod.modifierName}</b>: {mod.description}");
            rulesText.text = sb.ToString();
        }
    }
}
