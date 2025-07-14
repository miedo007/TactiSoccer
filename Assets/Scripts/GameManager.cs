using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;       // ← for Touchscreen
using TMPro;
using MoreMountains.Feedbacks;
using UnityEngine.SceneManagement;
using CozyFramework;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public GameObject ballPrefab;
    public GameObject aiPrefab;

    [Header("Draft Settings")]
[Tooltip("How many modifiers to offer each turn (2 or 3)")]
[SerializeField, Range(2, 3)] 
private int draftSize = 3;


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
 
 [Tooltip("If true, once a modifier is picked it won't be offered again until the match ends")]
    [SerializeField] private bool uniqueDraft = true;
   
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
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI goalText;

    [Header("Modifier UI")]
    [Tooltip("Separate text field to display modifier alerts")]
    public TextMeshProUGUI modifierText;

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

    [Header("Tackle Push Settings")]
    [Tooltip("How many Unity units the tackled piece is knocked back")]
    public float tacklePushDistance = 2f;

    [Tooltip("Seconds it takes to slide back")]
    public float tacklePushDuration = 0.4f;

    [Header("Movement Settings")]
    [Tooltip("When true, only adjacent+diagonal moves are allowed; when false, all columns are valid.")]
    public bool restrictToAdjacent = true;

    [Header("Feature Toggles")]
    [Tooltip("Turn off to disable all power-up spawning and effects.")]
    public bool enablePowerUps = true;
    [Tooltip("Turn off to disable all match modifiers and their effects.")]
    public bool enableModifiers = true;

    [Header("Result Popup")]
    public GameObject resultPopup;        // assign a simple panel with Text + Continue button
    public TextMeshProUGUI resultText;    // “You Win!” / “You Lose!”
    public Button continueButton;         // “Continue” → back to MainMenu

    [Header("Reward Settings")]
    [Tooltip("How many coins the player paid to enter this match")]
    public int entryFee = 5;

    [Tooltip("Currency ID you use for coins")]
    public string currencyId = "GOLD";

    [Tooltip("Gold awarded when the player wins a match")]
    public int winReward = 15;      // tweak in Inspector or compute at runtime

    [Header("Leaderboard")]
    [Tooltip("Remote‐config ID for your leaderboard")]
    private int _leaderboardScore = 0;
    public string leaderboardID = "highscore";

    [Header("Draft Delay")]
    [Tooltip("How long to wait before showing the modifier draft panel each turn")]
    [SerializeField] private float draftDelay = 1.0f;

    // private state
    private int playerGold;
    private bool _playerWon;
    private GameObject ballInstance;
    private BallController ballCtrl;
    private int ballRow, ballCol;

    private int _pushThroughBlockedCol = -1;
    private int _lockedColThisTurn = -1;

   

    public enum Actor { Player, AI }
    private Actor possession;

    private enum Phase { ChoosingModifiers, PlayerAttack, AwaitingDefense }
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

    // your UI panel that shows 3 modifier buttons/icons
    [SerializeField] private ModifierDraftPanel modifierDraftPanel;

    // the three-option pools and the picks
    private List<MatchModifierDefinition> _playerDraft;
    private List<MatchModifierDefinition> _aiDraft;
    private MatchModifierDefinition _playerPick;
    private MatchModifierDefinition _aiPick;


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

   /// <summary>
    /// Adjusts the running leaderboard score by +10 (win) or –5 (lose), clamps ≥0,
    /// then submits via the CozyLeaderboards API.
    /// </summary>

   
   void Start()
{
    // If you still have the old rulesPanel in your scene, hide it so it never blocks clicks.
    if (rulesPanel != null)
        rulesPanel.SetActive(false);
        rulesButton.gameObject.SetActive(false);
        
        // 2) wire up the toggle button so the player can always open it later
    rulesButton.onClick.RemoveAllListeners();
    rulesButton.onClick.AddListener(() =>
    {

            rulesPanel.SetActive(!rulesPanel.activeSelf);
    });

      // **this is the trick**: always keep it on top
    rulesButton.transform.SetAsLastSibling();

    // Cache penalty visuals
    penaltyBallStartPos   = penaltyBall.anchoredPosition;
    goalkeeperBaseScale   = goalkeeperImage.rectTransform.localScale;

    // Initialize each Cell with its row/col and a reference back to this GM
    for (int r = 0; r < gridManager.rows; r++)
    {
        for (int c = 0; c < gridManager.cols; c++)
        {
            gridManager
                .cells[r, c]
                .GetComponent<Cell>()
                .Initialize(r, c, this);
        }
    }

    // Clear any on-screen text
    messageText.text  = "";
    modifierText.text = "";

    // Kick off the very first match turn
    InitializeMatch();
}



    /// <summary>
    /// Sets up a fresh match without any betting UI.
    /// </summary>
    /// <summary>
/// Sets up a fresh match without any betting UI.
/// </summary>
private void InitializeMatch()
{
    // hide any leftover UIs
    resultPopup?.SetActive(false);
    penaltyPanel?.SetActive(false);
    penaltyBall?.gameObject.SetActive(false);
    goalkeeperImage?.gameObject.SetActive(false);

    // reset grid & state
    DisableGrid();
    ballRow = gridManager.rows / 2;
    ballCol = gridManager.cols / 2;
    possession = (Random.value < 0.5f) ? Actor.Player : Actor.AI;
    SpawnCharacter();

    // reset used‐set at start of match
    matchModifierManager.ResetUsedModifiers();

    // push your inspector toggle into the manager
    matchModifierManager.UniqueDraft = uniqueDraft;

    // power-ups & modifiers if enabled
    ClearFieldPowerUps();
    if (enableModifiers)
        matchModifierManager.PickRandomModifiers();
    if (enablePowerUps)
        powerUpSpawner.SpawnDrops();

    // start the very first turn
    StartNewTurn();

    // re-enable the grid so cells are clickable immediately
    EnableGrid();
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

    void StartNewTurn()
    {
    _pushThroughBlockedCol = -1;
     _lockedColThisTurn = -1;
    rulesPanel.SetActive(false);
    rulesText.text = "";
    rulesButton.gameObject.SetActive(false);
        _inputLocked = true;
        phase = Phase.ChoosingModifiers;
        StartCoroutine(BeginModifierDraftWithDelay());
    }

    private IEnumerator BeginModifierDraftWithDelay()
    {
        // 1) wait so the last move animation can finish
        yield return new WaitForSeconds(draftDelay);
        // 2) then open the draft
        BeginModifierDraft();
    }

    void BeginModifierDraft()
{
    matchModifierManager.ClearTurnModifiers();
    _playerPick = _aiPick = null;
    modifierDraftPanel.SetToggleChoicesVisible(true);
    rulesButton.gameObject.SetActive(false);

    // attacker always gets Offensive+Tactical, defender Defensive+Tactical
    var offenseCats = new[] {
        MatchModifierDefinition.ModifierCategory.Offensive,
        MatchModifierDefinition.ModifierCategory.Tactical
    };
    var defenseCats = new[] {
        MatchModifierDefinition.ModifierCategory.Defensive,
        MatchModifierDefinition.ModifierCategory.Tactical
    };

    // pick the right category‐sets for player vs AI
    var playerCats = (possession == Actor.Player) ? offenseCats : defenseCats;
    var   aiCats   = (possession == Actor.Player) ? defenseCats : offenseCats;

    // draft exactly `draftSize` for each
    _playerDraft = matchModifierManager.Draft(draftSize, playerCats);
    _aiDraft     = matchModifierManager.Draft(draftSize,   aiCats);

    // show in UI
    modifierDraftPanel.Show(_playerDraft, OnPlayerModifierChosen);
    modifierDraftPanel.transform.SetAsLastSibling();

    // AI picks immediately from its new draft
    _aiPick = _aiDraft[Random.Range(0, _aiDraft.Count)];
}


    private void OnPlayerModifierChosen(MatchModifierDefinition pick)
    {
        _playerPick = pick;
        modifierDraftPanel.LockButtons();
        TryResolveDraft();
    }

    private void TryResolveDraft()
{
    if (_playerPick == null || _aiPick == null) return;

    modifierDraftPanel.Reveal(_playerPick, _aiPick);
    matchModifierManager.ApplyTurnModifiers(_playerPick.type, _aiPick.type);

    // Fill out and show the rules panel
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"<b>You picked:</b> {_playerPick.modifierName}");
    sb.AppendLine(_playerPick.description);
    sb.AppendLine();
    sb.AppendLine($"<b>Opponent picked:</b> {_aiPick.modifierName}");
    sb.AppendLine(_aiPick.description);
    rulesText.text = sb.ToString();
    rulesPanel.SetActive(true);
    rulesButton.gameObject.SetActive(true);

    // Hide the draft-toggle until next turn
    modifierDraftPanel.SetToggleChoicesVisible(false);

    // Auto-hide the rules panel (and clear text) after 3 seconds
    StartCoroutine(HideRulesPanelDelayed(3f));

    // continue the turn
    StartCoroutine(ContinueAfterDraft());
}

private IEnumerator HideRulesPanelDelayed(float delay)
{
    yield return new WaitForSeconds(delay);
    rulesPanel.SetActive(false);
    
}


    private IEnumerator ContinueAfterDraft()
    {
        // give player a moment to see both picks
        yield return new WaitForSeconds(0.5f);
        modifierDraftPanel.Hide();

    
    // 1) Unlock input
    _inputLocked = false;
    
    // 2) Recompute the row we’ll be moving into
    int targetRow = (possession == Actor.Player)
        ? ballRow + 1
        : ballRow - 1;
    
    // 3) Restore your old turn logic:
    //    a) Set phase &, if AI’s turn, let it pick its attack
    if (possession == Actor.Player)
{
    phase = Phase.PlayerAttack;
}
else
{
    phase = Phase.AwaitingDefense;
}
    
    //    b) Highlight the allowed cells
    HighlightRow(targetRow, possession);

//    c) Now let the AI pick from those highlighted columns
if (possession != Actor.Player)
{
    attackChoice = AIAttackGuess();
}
    
    //    c) Flight Path
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
        && _allowedColumns.Count > 0)
    {
        int fastFromAllowed = _allowedColumns[Random.Range(0, _allowedColumns.Count)];
        matchModifierManager.SetFastLaneColumn(fastFromAllowed);
    }
    
    //    d) Quit‐or‐Double
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble))
    {
        int qodCol = restrictToAdjacent && _allowedColumns.Count > 0
            ? _allowedColumns[Random.Range(0, _allowedColumns.Count)]
            : Random.Range(0, gridManager.cols);
        matchModifierManager.SetQuitOrDoubleColumn(qodCol);
    }
    
    //    e) Highlight Fast‐Lane (cyan)
    int fastCol = matchModifierManager.GetFastLaneColumn();
    if (fastCol >= 0 && targetRow >= 0 && targetRow < gridManager.rows)
    {
        var fastCell = gridManager.cells[targetRow, fastCol].GetComponent<Cell>();
        fastCell.Highlight(true);
        fastCell.GetComponent<SpriteRenderer>().color =
            new Color(0f, 1f, 1f, 0.5f);
    }

    
    //    g) Highlight Quit‐or‐Double (magenta)
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble))
    {
        int qodCol = matchModifierManager.GetQuitOrDoubleColumn();
        if (qodCol >= 0 && targetRow >= 0 && targetRow < gridManager.rows)
        {
            var qodCell = gridManager.cells[targetRow, qodCol].GetComponent<Cell>();
            qodCell.Highlight(true);
            qodCell.GetComponent<SpriteRenderer>().color =
                new Color(1f, 0f, 1f, 0.5f);
        }
    }
    
    // and now letting OnCellClicked drive into ResolveTurn() as usual…
}

   public void OnCellClicked(int r, int c)
{
    // ignore taps if we’re mid‐resolution or showing the bet UI
    if (_inputLocked || penaltyPanel.activeSelf)
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
    bool tackle = (attackChoice == defendChoice);
    int originalRow = ballRow;
    int originalCol = ballCol;
    int dir = (attacker == Actor.Player) ? +1 : -1;

// 0) Check if this is the 4th dribble under MomentumLimit
    bool momentumLimitViolated = enableModifiers
    && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit)
    && !matchModifierManager.CanAdvance();
    // track Grid Mastery for this turn
    bool gridMasteryActive = enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.GridMastery)
        && matchModifierManager.IsGridMasteryReady();

    // track dynamic Corridor for this turn
    bool dynamicCorridorActive = enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.DynamicCorridor)
        && matchModifierManager.IsDynamicCorridorReady(attacker);

    // ── Counter Strike flag ──
    bool counterStrikeActive =
    enableModifiers
    && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.CounterStrike)
    && matchModifierManager.IsCounterStrikeReady(attacker);

    // 1) Reveal picks (stamp-drop animation)
int playerPick = (possession == Actor.Player) ? attackChoice : defendChoice;
int aiPick     = (possession == Actor.Player) ? defendChoice : attackChoice;

// How far above the cell you want it to start (tweak in Inspector if you expose it)
float dropHeight   = 5f;
float dropDuration = 0.3f;
float fadeDuration = 0.2f;

// PLAYER marker
if (revealMarkerPlayerPrefab != null)
{
    Vector3 cellPos = gridManager.GetCellPosition(targetRow, playerPick);
    // start ABOVE the cell
    var pm = Instantiate(
        revealMarkerPlayerPrefab,
        cellPos + Vector3.up * dropHeight,
        Quaternion.identity
    );
    _revealMarkers.Add(pm);

    // make it invisible at first
    var sr = pm.GetComponent<SpriteRenderer>();
    if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);

    // fade in
    if (sr != null)
        sr.DOFade(1f, fadeDuration);

    // drop straight down onto the cell, with a little bounce at the end
    pm.transform
      .DOMove(cellPos, dropDuration)
      .SetEase(Ease.OutBounce);
}

yield return new WaitForSeconds(revealStaggerDelay);

// AI marker
if (revealMarkerAIPrefab != null)
{
    Vector3 cellPos = gridManager.GetCellPosition(targetRow, aiPick);
    var am = Instantiate(
        revealMarkerAIPrefab,
        cellPos + Vector3.up * dropHeight,
        Quaternion.identity
    );
    _revealMarkers.Add(am);

    var sr = am.GetComponent<SpriteRenderer>();
    if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);
    if (sr != null)
        sr.DOFade(1f, fadeDuration);

    am.transform
      .DOMove(cellPos, dropDuration)
      .SetEase(Ease.OutBounce);
}

yield return new WaitForSeconds(revealStaggerDelay);
ClearRevealMarkers();

    // 2) Mirror Clash
    if (!tackle
        && enableModifiers
       && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MirrorClash)
        && matchModifierManager.IsMirrorClash(attackChoice, defendChoice, ballCol))
    {
        matchModifierManager.ApplyMirrorClash(ref ballRow, attacker);
        ShowModifier("Mirror Clash! \nBall moves back!", 3f);
        yield return new WaitForSeconds(afterAnimDelay);
        ClearHighlights();
        yield return ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol));
        StartNewTurn();
        yield break;
    }

    // 3) Momentum Limit
    // ── 3) Handle 4th dribble immediately ──
    if (momentumLimitViolated && !tackle)
    {
        // you advanced on the 4th dribble → straight to penalties
        ShowModifier("Momentum Limit! \nPenalty Shootout!", 3f);
        (attacker == Actor.Player
            ? feedbackPlayerAdvance
            : feedbackOpponentAdvance
        )?.PlayFeedbacks();
        matchModifierManager.OnTackle(attacker);       // reset the streak
        StartCoroutine(PenaltySequence(attacker));
        yield break;
    }



    // 4) Tackle or Dribble
if (tackle)
{
    // 1) Compute new grid coords (unchanged)…
    if (counterStrikeActive)
        ballRow = Mathf.Clamp(originalRow - 3 * dir, 0, gridManager.rows - 1);
    else if (momentumLimitViolated)
        ballRow = Mathf.Clamp(originalRow - 2 * dir, 0, gridManager.rows - 1);
    else if (enableModifiers
         && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble)
         && attackChoice == matchModifierManager.GetQuitOrDoubleColumn())
        ballRow = Mathf.Clamp(originalRow - 2 * dir, 0, gridManager.rows - 1);
    else
        ballRow = targetRow;
    ballCol = attackChoice;

    // 2) Compute positions
    Vector3 cellPos          = gridManager.GetCellPosition(ballRow, ballCol);
    Vector3 attackerStartPos = gridManager.GetCellPosition(originalRow, originalCol);
    Vector3 defenderStartPos = gridManager.GetCellPosition(originalRow + dir, originalCol);

    // 3) Attacker moves in
    yield return ballCtrl.MoveToCell(cellPos).WaitForCompletion();

    // 4) Feedback
    ShowMessage(attacker == Actor.Player ? "Tackled!" : "Tackle!", 3f);
    (attacker == Actor.Player ? feedbackTackleLose : feedbackTackleWin)?.PlayFeedbacks();

    // 5) Spawn & move in the tackler
    GameObject tacklerGO = Instantiate(
        (attacker == Actor.Player) ? aiPrefab : ballPrefab,
        defenderStartPos,
        Quaternion.identity
    );
    var tacklerCtrl = tacklerGO.GetComponent<BallController>();
    yield return tacklerCtrl.MoveToCell(cellPos).WaitForCompletion();

    // 6) Push the original attacker *farther* and *keep* it there
    Vector3 pushPos = cellPos + Vector3.down * dir * tacklePushDistance;
    Tween pushTween = ballCtrl.transform
        .DOMove(pushPos, tacklePushDuration)
        .SetEase(Ease.OutQuad);
    yield return pushTween.WaitForCompletion();

    // 7) Clean up the tackler pawn
    Destroy(tacklerGO);

    // 8) Modifier bookkeeping
    if (counterStrikeActive)
        matchModifierManager.ConsumeCounterStrike(attacker);
    else if (enableModifiers)
        matchModifierManager.OnCounterTackle(attacker);
    if (enableModifiers)
        matchModifierManager.OnTackle(attacker);

    // 9) Swap possession
    possession = (attacker == Actor.Player) ? Actor.AI : Actor.Player;

    // —— Counter Surge: if the defender had CounterSurge, they now advance +1 row —— 
    if (enableModifiers && matchModifierManager.IsCounterSurgeReady(possession))
    {
        int surgeDir = (possession == Actor.Player) ? +1 : -1;
        ballRow = Mathf.Clamp(ballRow + surgeDir, 0, gridManager.rows - 1);
        matchModifierManager.ConsumeCounterSurge(possession);
        ShowModifier("Counter Surge!\nAdvance an extra row!", 2f);
    }

    // 10) Goal check
    bool atGoalRow = (possession == Actor.Player && ballRow == gridManager.rows - 1)
                  || (possession == Actor.AI     && ballRow == 0);
    if (atGoalRow)
    {
        StartCoroutine(PenaltySequence(possession));
        yield break;
    }

    // 11) Spawn and continue
    SpawnCharacter();
    StartNewTurn();
    yield break;
}

else
{
    // dribble branch remains unchanged
    if (enableModifiers)
    {
        matchModifierManager.OnAdvance();
        matchModifierManager.OnDribble(attackChoice);
        if (attackChoice != originalCol)
            matchModifierManager.OnDiagonalDribble(attacker);
    }

    // — NEW: Double Advance —
    int doubleBoost = 0;
    if (matchModifierManager.IsDoubleAdvanceReady())
    {
        doubleBoost = 1;
        matchModifierManager.ConsumeDoubleAdvance();
        ShowModifier("Double Advance!\nExtra row!", 3f);
    }

    // ── Slipstream: +1 on the very next diagonal dribble
    int slipBoost = 0;
    if (matchModifierManager.IsSlipstreamReady() && attackChoice != originalCol)
    {
    slipBoost = 1;
    matchModifierManager.ConsumeSlipstream();
    ShowModifier("Slipstream!\nDiagonal jump +1 row", 3f);
    }

    if (gridMasteryActive)
    {
        matchModifierManager.ConsumeGridMastery();
        ShowModifier("Glide!\nIgnore adjacency", 3f);
    }

    if (dynamicCorridorActive)
    {
        matchModifierManager.ConsumeDynamicCorridor(attacker);
        ShowModifier("Dynamic Corridor! \nExtra 2 rows advance", 3f);
    }

    ShowMessage(attacker == Actor.Player ? "Dribble!" : "Dribbled!", 3f);
    (attacker == Actor.Player
        ? feedbackPlayerAdvance
        : feedbackOpponentAdvance
    )?.PlayFeedbacks();

    // 5) Column Loyalty
    int loyaltyBoost = 0;
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.ColumnLoyalty))
    {
        loyaltyBoost = matchModifierManager.GetLoyaltyBoost(attacker, attackChoice);
        if (loyaltyBoost > 0)
            ShowModifier("Column Loyalty! \nExtra row!", 3f);
    }

    // 6) Flight Path
    int flightBoost = 0;
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
        && attackChoice == matchModifierManager.GetFastLaneColumn())
    {
        flightBoost = 1;
        ShowModifier("Flight Path! \nDouble Advance", 3f);
    }

    // 7) Quit-or-Double
    int quitBoost = 0;
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble)
        && attackChoice == matchModifierManager.GetQuitOrDoubleColumn())
    {
        quitBoost = 1;
        ShowModifier("Quit or Double! \n2 Extra rows!", 3f);
    }

    // 8) Burned Column update
    if (enableModifiers)
        matchModifierManager.SetLastUsedColumn(attacker, attackChoice);

       

    // Wait & clear highlights
    yield return new WaitForSeconds(tackleAnimDuration);
    ClearHighlights();

    // 9) Advance with all boosts (including doubleBoost)
    int dynamicBoost = dynamicCorridorActive ? 1 : 0;
    int totalBoost = loyaltyBoost + flightBoost + quitBoost + dynamicBoost + doubleBoost + slipBoost;
    int newRow = ballRow + dir + (totalBoost * dir);
    ballRow = Mathf.Clamp(newRow, 0, gridManager.rows - 1);
    ballCol = attackChoice;

    yield return ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol));
}
    if (enablePowerUps)
        CheckForPickups();

    // 10) Check for goal (now triggers even if it was a tackle)
    bool goal = (attacker == Actor.Player && ballRow == gridManager.rows - 1)
             || (attacker == Actor.AI     && ballRow == 0);
    if (goal)
    {
        StartCoroutine(PenaltySequence(attacker));
        yield break;
    }

    // 11) Next turn
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

    private Coroutine _clearGoalCoroutine;
private void ShowGoalMessage(string msg, float duration)
{
    if (_clearGoalCoroutine != null) StopCoroutine(_clearGoalCoroutine);
    goalText.text = msg;
    _clearGoalCoroutine = StartCoroutine(ClearGoalAfter(duration));
}

private IEnumerator ClearGoalAfter(float t)
{
    yield return new WaitForSeconds(t);
    goalText.text = "";
    _clearGoalCoroutine = null;
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
        ShowMessage(attacker == Actor.Player ? "Countered!" : "Saved!", 2f);
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
        // record result so EndMatch shows correct message
        _playerWon = (attacker == Actor.Player);
         //  — NEW: update & submit leaderboard points —

        if (attacker == Actor.Player)
        {
            ShowGoalMessage("GOAAAAAL!\nYou Win!", 3f);
            feedbackGoalForPlayer?.PlayFeedbacks();
            feedbackMatchWin?.PlayFeedbacks();
        }
        else
        {
            ShowGoalMessage("GOAAAAAL!\nYou Lose!", 3f);
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
    // 1) Clear previous highlights & re-enable all colliders
    ClearHighlights();
    _allowedColumns.Clear();
    if (tr < 0 || tr >= gridManager.rows) return;

    // 2) Build base movement list
    List<int> movement;
    if (enableModifiers && matchModifierManager.IsGridMasteryReady())
    {
        movement = Enumerable.Range(0, gridManager.cols).ToList();
        matchModifierManager.ConsumeGridMastery();
    }
    else if (restrictToAdjacent)
    {
        movement = GetAdjacentColumns();
    }
    else
    {
        movement = Enumerable.Range(0, gridManager.cols).ToList();
    }

    // 3) LOCKED COLUMN: pick one once
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.LockedColumn)
        && _lockedColThisTurn < 0
        && movement.Count > 0)
    {
        _lockedColThisTurn = movement[Random.Range(0, movement.Count)];
        ShowModifier($"Column {_lockedColThisTurn + 1} locked!", 2f);
    }
    //    remove it if we're DEFENDING
    if (phase == Phase.AwaitingDefense && _lockedColThisTurn >= 0)
    {
        movement.Remove(_lockedColThisTurn);
    }

    // 4) BURNED COLUMN
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn))
    {
        int burned = matchModifierManager.GetLastUsedColumn(attacker);
        movement.Remove(burned);
    }

    // 5) BLOCKADE (defender-only)
    if (enableModifiers && phase == Phase.AwaitingDefense)
    {
        var defender = attacker == Actor.Player ? Actor.AI : Actor.Player;
        if (matchModifierManager.IsBlockadeReady(defender))
        {
            movement = movement.OrderBy(_ => Random.value).Take(2).ToList();
            matchModifierManager.ConsumeBlockade(defender);
            ShowModifier("Blockade!\nDefender limited to 2 columns", 3f);
        }
    }

    // 6) SABOTAGE (attacker-only)
    if (enableModifiers && matchModifierManager.IsSabotageReady(attacker))
    {
        movement = movement.OrderBy(_ => Random.value).Take(2).ToList();
        matchModifierManager.ConsumeSabotage(attacker);
        ShowModifier("Sabotage!\nAttacker limited to 2 columns", 3f);
    }

    // 7) PUSH THROUGH: pick one once
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.PushThrough)
        && _pushThroughBlockedCol < 0
        && movement.Count > 0)
    {
        _pushThroughBlockedCol = movement[Random.Range(0, movement.Count)];
        if (phase == Phase.PlayerAttack)
            ShowModifier($"PushThrough → blocking col {_pushThroughBlockedCol + 1}", 2f);
    }
    //    remove it if we're DEFENDING
    if (phase == Phase.AwaitingDefense && _pushThroughBlockedCol >= 0)
    {
        movement.Remove(_pushThroughBlockedCol);
    }

    // — NEW: Forced Diagonal (defender) —
if (enableModifiers
    && matchModifierManager.HasModifier(
         MatchModifierDefinition.ModifierType.ForcedDiagonal))
{
    movement = movement.Where(col => col != ballCol).ToList();
}

    // 8) Highlight & cache survivors
    foreach (int c in movement)
    {
        _allowedColumns.Add(c);
        gridManager.cells[tr, c].GetComponent<Cell>().Highlight(true);
    }

    // 9) LOCKED COLUMN: red + disable for DEFENSE
    if (_lockedColThisTurn >= 0)
    {
        var lockedCell = gridManager.cells[tr, _lockedColThisTurn];
        lockedCell.GetComponent<Cell>().Highlight(true);
        var sr = lockedCell.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0f, 0f, 0.5f);
        if (phase == Phase.AwaitingDefense)
            lockedCell.GetComponent<Collider2D>().enabled = false;
    }

    // 10) PUSH THROUGH: yellow for ATTACK, red+disable for DEFENSE
    if (_pushThroughBlockedCol >= 0)
    {
        var pushCell = gridManager.cells[tr, _pushThroughBlockedCol];
        pushCell.GetComponent<Cell>().Highlight(true);
        var sr = pushCell.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = (phase == Phase.PlayerAttack)
                ? new Color(1f, 1f, 0f, 0.5f)  // yellow
                : new Color(1f, 0f, 0f, 0.5f); // red

        // only attacker may still click it
        pushCell.GetComponent<Collider2D>().enabled = (phase == Phase.PlayerAttack);
    }

    // 11) Tactical/offensive tints...
    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble))
    {
        int qo = matchModifierManager.GetQuitOrDoubleColumn();
        if (_allowedColumns.Contains(qo))
            gridManager.cells[tr, qo].GetComponent<SpriteRenderer>()
                       .color = new Color(1f, 0f, 1f, 0.5f);
    }

    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit)
        && !matchModifierManager.CanAdvance())
    {
        foreach (int c in _allowedColumns)
            gridManager.cells[tr, c].GetComponent<SpriteRenderer>()
                       .color = new Color(1f, 0f, 0f, 0.5f);
    }

    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.DynamicCorridor)
        && matchModifierManager.IsDynamicCorridorReady(attacker))
    {
        foreach (int c in _allowedColumns)
            gridManager.cells[tr, c].GetComponent<SpriteRenderer>()
                       .color = new Color(0f, 0f, 1f, 0.5f);
    }

    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.CounterStrike)
        && matchModifierManager.IsCounterStrikeReady(attacker))
    {
        foreach (int c in _allowedColumns)
            gridManager.cells[tr, c].GetComponent<SpriteRenderer>()
                       .color = new Color(0.5f, 0f, 0.5f, 0.5f);
    }

    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath))
    {
        int fp = matchModifierManager.GetFastLaneColumn();
        if (_allowedColumns.Contains(fp))
            gridManager.cells[tr, fp].GetComponent<SpriteRenderer>()
                       .color = new Color(0f, 1f, 1f, 0.5f);
    }
}


    private void ClearHighlights()
{
    foreach (var cellGO in gridManager.AllCells)
    {
        // turn off highlight graphic
        cellGO.GetComponent<Cell>().Highlight(false);

        // reset any sprite tint
        var sr = cellGO.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;

        // re-enable collider so we can selectively turn them off later
        var col2d = cellGO.GetComponent<Collider2D>();
        if (col2d != null) col2d.enabled = true;
    }
}

private List<int> GetAdjacentColumns()
{
    var adj = new List<int> { ballCol };
    if (ballCol - 1 >= 0)           adj.Add(ballCol - 1);
    if (ballCol + 1 < gridManager.cols) adj.Add(ballCol + 1);
    return adj;
}


    private int AIAttackGuess() =>
        _allowedColumns.Count > 0
            ? _allowedColumns[Random.Range(0, _allowedColumns.Count)]
            : Random.Range(0, gridManager.cols);

    private int AI_DefenseGuess()
{
    var choices = restrictToAdjacent
        ? GetAdjacentColumns()
        : Enumerable.Range(0, gridManager.cols).ToList();

    // LockedColumn
    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.LockedColumn) &&
        _lockedColThisTurn >= 0)
        choices.Remove(_lockedColThisTurn);

    // BurnedColumn
    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn))
    {
        var burned = matchModifierManager.GetLastUsedColumn(
            possession == Actor.Player ? Actor.AI : Actor.Player);
        choices.Remove(burned);
    }

    // Blockade
    if (enableModifiers &&
        matchModifierManager.IsBlockadeReady(
            possession == Actor.Player ? Actor.AI : Actor.Player))
    {
        choices = choices.OrderBy(_ => Random.value).Take(2).ToList();
    }

    // PushThrough
    if (enableModifiers &&
        matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.PushThrough) &&
        _pushThroughBlockedCol >= 0)
        choices.Remove(_pushThroughBlockedCol);

    if (choices.Count == 0)
        return Random.Range(0, gridManager.cols);

    Debug.Log($"[GameManager] AI Defense choices: {string.Join(", ", choices)}");
    return choices[Random.Range(0, choices.Count)];

    // Fallback
    return Random.Range(0, gridManager.cols);
}

        // -------------------------------------------------------
    // UI Helpers
    // -------------------------------------------------------
    private void UpdateGoldUI()
    {
        
        int balance = CozyAPI.Instance.GetCurrencyValue(currencyId);
        goldText.text = $"Gold: {balance}";
    }


  private void EndMatch()
{
    DisableGrid();
    resultText.text = _playerWon ? "You Win!" : "You Lose!";
    resultPopup.SetActive(true);

    // Disable Continue until reward logic finishes
    continueButton.interactable = false;

    if (_playerWon)
        StartCoroutine(AwardGoldCoroutine());
    else
        continueButton.interactable = true;   // loser – no reward to wait for

    int delta = _playerWon ? +10 : -5;
    _ = CozyLeaderboards.Instance.AddScoreToLeaderboard(leaderboardID, delta);

    continueButton.onClick.RemoveAllListeners();
    continueButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
}

// ---------------------------------------------
// GameManager : AwardGoldCoroutine()
// ---------------------------------------------
private IEnumerator AwardGoldCoroutine()
{
    var task = CozyAPI.Instance.GainCurrency(currencyId, winReward);

    while (!task.IsCompleted)         // wait for server reply
        yield return null;

    if (task.IsFaulted)
    {
        Debug.LogException(task.Exception);
        ShowMessage("Reward failed – check connectivity", 2f);
        // Still allow the player to leave
    }

    UpdateGoldUI();                   // bump in-game counter

    // Re-enable Continue now that balance is updated
    continueButton.interactable = true;
}



    public void OnRestart()
    {
        _inputLocked = false;
        messageText.text = "";
        modifierText.text = "";
        possession = (Random.value < 0.5f) ? Actor.Player : Actor.AI;
        ballRow = gridManager.rows / 2;
        ballCol = gridManager.cols / 2;

        penaltyPanel.SetActive(false);
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        foreach (var btn in penaltyButtons)
            btn.GetComponent<Image>().color = Color.white;

        SpawnCharacter();
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


}
 // ← final closing brace for GameManager
