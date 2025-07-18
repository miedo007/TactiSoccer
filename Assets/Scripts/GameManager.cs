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

    [Header("Turn Prompt")]
    [Tooltip("Instruction shown at the start of each turn")]
    [SerializeField] private TextMeshProUGUI instructionText;

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
    [Tooltip("Turn off to disable all match modifiers and their effects.")]
    public bool enableModifiers = true;

    [Header("Result Popup")]
    public GameObject resultPopup;        // assign a simple panel with Text + Continue button
    public TextMeshProUGUI resultText;    // “You Win!” / “You Lose!”
    public Image               coinIcon;       // drag in your gold‐coin sprite
    public TextMeshProUGUI     coinText;       // “+15”
    public Image               trophyIcon;     // drag in a trophy/leaderboard sprite
    public TextMeshProUGUI     trophyText;     // “+12”
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
    private HashSet<int> _tintedColumns = new HashSet<int>();

    private GameObject _playerMarkerInstance;
    private GameObject _aiMarkerInstance;
   

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

    private WaitForSeconds _draftDelayWait;
    private WaitForSeconds _revealStaggerWait;
    private readonly List<int> _movementBuffer = new List<int>(16);

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



   /// <summary>
    /// Adjusts the running leaderboard score by +10 (win) or –5 (lose), clamps ≥0,
    /// then submits via the CozyLeaderboards API.
    /// </summary>

   
   private void Start()
{
    // 1) Cap the frame rate for consistent timing & lower CPU load
    Application.targetFrameRate = 60;
    QualitySettings.vSyncCount  = 1;

    // 2) Hide the old rulesPanel (if any) so it never blocks clicks
    if (rulesPanel != null)
    {
        rulesPanel.SetActive(false);
        rulesButton.gameObject.SetActive(false);
    }

    // 3) Hide the “Pick a cell to move to” prompt until later
    if (instructionText != null)
    {
        instructionText.gameObject.SetActive(false);
    }

    // 4) Wire up the toggle button so the player can always open the rules
    rulesButton.onClick.RemoveAllListeners();
    rulesButton.onClick.AddListener(() =>
    {
        rulesPanel.SetActive(!rulesPanel.activeSelf);
    });
    rulesButton.transform.SetAsLastSibling(); // always keep it on top

    // 5) Cache penalty visuals
    penaltyBallStartPos = penaltyBall.anchoredPosition;
    goalkeeperBaseScale = goalkeeperImage.rectTransform.localScale;

    // 6) Initialize each Cell with its row/col and a reference back to this GM
    for (int r = 0; r < gridManager.rows; r++)
    {
        for (int c = 0; c < gridManager.cols; c++)
        {
            gridManager.cells[r, c]
                       .GetComponent<Cell>()
                       .Initialize(r, c, this);
        }
    }

    // 7) Clear any on-screen text
    messageText.text  = "";
    modifierText.text = "";

    // 8) Cache the draft delay so we don’t alloc each turn
    _draftDelayWait = new WaitForSeconds(draftDelay);
    _revealStaggerWait = new WaitForSeconds(revealStaggerDelay);

    // 9) Pre-instantiate one of each reveal marker and disable them
    _playerMarkerInstance = Instantiate(revealMarkerPlayerPrefab);
    _playerMarkerInstance.SetActive(false);
    _aiMarkerInstance     = Instantiate(revealMarkerAIPrefab);
    _aiMarkerInstance.SetActive(false);

   

    // 10) Kick off the very first match turn
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
    // 0) hide the “pick a cell” prompt
    instructionText?.gameObject.SetActive(false);

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

     // modifiers only
    if (enableModifiers)
        matchModifierManager.PickRandomModifiers();

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
        yield return _draftDelayWait;
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


    // 2) Show the correct prompt
    if (instructionText != null)
    {
        if (possession == Actor.Player)
            instructionText.text = "Pick a cell to move to";
        else
            instructionText.text = "Pick a cell to defend";

        instructionText.gameObject.SetActive(true);
    }

    // 2) Recompute the row we’ll be moving into
    int targetRow = (possession == Actor.Player)
        ? ballRow + 1
        : ballRow - 1;

    // 3) Restore your old turn logic:
    if (possession == Actor.Player)
        phase = Phase.PlayerAttack;
    else
        phase = Phase.AwaitingDefense;

    // 4) Highlight the allowed cells (this will also handle the Q-o-D override for PlayerAttack)
    HighlightRow(targetRow, possession);

    // 5) Now let the AI pick from those highlighted columns (if it’s AI’s turn to attack)
    if (possession != Actor.Player)
        attackChoice = AIAttackGuess();

    // 6) Flight Path
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
        && _allowedColumns.Count > 0)
    {
        int fastFromAllowed = _allowedColumns[Random.Range(0, _allowedColumns.Count)];
        matchModifierManager.SetFastLaneColumn(fastFromAllowed);
    }

    // 7) Quit-or-Double: choose the column (no UI here)
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble))
    {
        int qodCol = restrictToAdjacent && _allowedColumns.Count > 0
            ? _allowedColumns[Random.Range(0, _allowedColumns.Count)]
            : Random.Range(0, gridManager.cols);
        matchModifierManager.SetQuitOrDoubleColumn(qodCol);

        // 7a) **Immediate override highlight** so AI’s Q-o-D pick also shows up:
        //     white-highlight all legal cells...
        foreach (int c in _allowedColumns)
        {
            var cellGO = gridManager.cells[targetRow, c];
            cellGO.GetComponent<Cell>().Highlight(true);
            var sr = cellGO.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;
        }
        //     …then magenta-tint exactly the Q-o-D column
        if (_allowedColumns.Contains(qodCol))
        {
            var qodCell = gridManager.cells[targetRow, qodCol].GetComponent<Cell>();
            qodCell.Highlight(true);
            var sr = qodCell.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = new Color(1f, 0f, 1f, 0.5f);
        }
    }

    // 8) Highlight Fast-Lane (cyan)
    int fastCol = matchModifierManager.GetFastLaneColumn();
    if (fastCol >= 0 && targetRow >= 0 && targetRow < gridManager.rows)
    {
        var fastCell = gridManager.cells[targetRow, fastCol].GetComponent<Cell>();
        fastCell.Highlight(true);
        fastCell.GetComponent<SpriteRenderer>().color =
            new Color(0f, 1f, 1f, 0.5f);
    }

    
    
    // and now letting OnCellClicked drive into ResolveTurn() as usual…
}

   public void OnCellClicked(int r, int c)
{
    
    // hide prompt immediately
        if (instructionText != null)
            instructionText.gameObject.SetActive(false);

    // ignore taps if we’re mid‐resolution or showing the bet UI
    if (_inputLocked || penaltyPanel.activeSelf)
        return;

    int tr = possession == Actor.Player ? ballRow + 1 : ballRow - 1;
    if (r != tr || (_allowedColumns.Count > 0 && !_allowedColumns.Contains(c)))
        return;

    // lock out any further clicks until this turn fully resolves
    _inputLocked = true;
// ── NEW: if player is attacking and clicked the PushThrough column, show the message ──
    if (possession == Actor.Player
        && phase == Phase.PlayerAttack
        && enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.PushThrough)
        && c == _pushThroughBlockedCol)
    {
        ShowModifier($"PushThrough", 2f);
    }
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
if (_playerMarkerInstance != null)
{
    // compute the drop start position
    Vector3 cellPos = gridManager.GetCellPosition(targetRow, playerPick);

    // reuse the pooled marker
    var pm = _playerMarkerInstance;
    pm.transform.position = cellPos + Vector3.up * dropHeight;
    pm.transform.rotation = Quaternion.identity;
    pm.SetActive(true);

    // keep track so we can hide it later
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

yield return _revealStaggerWait;

/// AI marker
if (_aiMarkerInstance != null)
{
    // compute the drop start position
    Vector3 cellPos = gridManager.GetCellPosition(targetRow, aiPick);

    // reuse the pooled marker
    var am = _aiMarkerInstance;
    am.transform.position = cellPos + Vector3.up * dropHeight;
    am.transform.rotation = Quaternion.identity;
    am.SetActive(true);

    // keep track so we can hide it later
    _revealMarkers.Add(am);

    // make it invisible at first
    var sr = am.GetComponent<SpriteRenderer>();
    if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);

    // fade in
    if (sr != null)
        sr.DOFade(1f, fadeDuration);

    // drop straight down onto the cell, with a little bounce at the end
    am.transform
      .DOMove(cellPos, dropDuration)
      .SetEase(Ease.OutBounce);
}

yield return _revealStaggerWait;
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

// 3.5 Stall (defensive): on mismatch, attacker keeps ball and no advance
var defender = (attacker == Actor.Player) ? Actor.AI : Actor.Player;
if (!tackle
    && enableModifiers
    && matchModifierManager.IsStallReady(defender)    // only check the “ready” flag
    && attackChoice != defendChoice)                  // it’s a mismatch
{
    Debug.Log($"[Stall] triggered for defender={defender}, atk={attackChoice}, def={defendChoice}");
    matchModifierManager.ConsumeStall(defender);      // consume so it only fires once
    ShowModifier("Stall!\nNo advance, attacker keeps the ball", 3f);
    yield return new WaitForSeconds(afterAnimDelay);

    ClearHighlights();
    // ballRow/ballCol unchanged, attacker still has possession
    StartNewTurn();
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

// Edge Burst 

int edgeBurstBoost = 0;
if (matchModifierManager.IsEdgeBurstReady())
{
    bool toLeftEdge  = attackChoice == 0;
    bool toRightEdge = attackChoice == gridManager.cols - 1;
    if (toLeftEdge || toRightEdge)
    {
        edgeBurstBoost = 1;
        ShowModifier("Edge Burst!\nExtra row from edge", 3f);
    }
    matchModifierManager.ConsumeEdgeBurst();
}

    // Wait & clear highlights
    yield return new WaitForSeconds(tackleAnimDuration);
    ClearHighlights();

    // 9) Advance with all boosts (including doubleBoost)
    int dynamicBoost = dynamicCorridorActive ? 1 : 0;
    int totalBoost = loyaltyBoost + flightBoost + quitBoost + dynamicBoost + doubleBoost + slipBoost + edgeBurstBoost;
    int newRow = ballRow + dir + (totalBoost * dir);
    ballRow = Mathf.Clamp(newRow, 0, gridManager.rows - 1);
    ballCol = attackChoice;

    yield return ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol));
}

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
        foreach (var m in _revealMarkers)
    m.SetActive(false);
    _revealMarkers.Clear();
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
    // 1) record who won
    _playerWon = (attacker == Actor.Player);

    // 2) clear any lingering turn messages
    if (_clearMsgCoroutine != null) StopCoroutine(_clearMsgCoroutine);
    if (_clearModifierCoroutine != null) StopCoroutine(_clearModifierCoroutine);
    messageText.text  = "";
    modifierText.text = "";

    // 3) show the big GOAAAAAL! banner for 3 seconds
    ShowGoalMessage("GOAAAAAL!", 3f);
    if (_playerWon)
    {
        feedbackGoalForPlayer?.PlayFeedbacks();
        feedbackMatchWin?.PlayFeedbacks();
    }
    else
    {
        feedbackGoalAgainst?.PlayFeedbacks();
        feedbackMatchLose?.PlayFeedbacks();
    }

    // 4) hide the penalty‐shoot UI immediately so the goal banner is unobstructed
    penaltyBall.gameObject.SetActive(false);
    penaltyPanel.SetActive(false);
    goalkeeperImage.gameObject.SetActive(false);

    // 5) wait for your goal‐message duration
    yield return new WaitForSeconds(3f);

    // 6) now unlock input and show the result popup
    _inputLocked = false;
    EndMatch();
}
}


    public void OnPenaltyButton(int idx)
    {
        penaltyChoiceMade = true;
        if (penaltyAttacker == Actor.Player) penaltyAttackChoice = idx;
        else                                  penaltyDefendChoice = idx;
    }
/// <summary>
    /// Highlights  tints one cell, but only if it hasn’t already been tinted this pass.
    /// </summary>
    private void TintCell(int row, int col, Color tint, bool disableCollider = false)
    {
        // if we’ve already colored this column, skip it
        if (!_tintedColumns.Add(col)) return;

        var cellGO = gridManager.cells[row, col];
        cellGO.GetComponent<Cell>().Highlight(true);
        var sr = cellGO.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = tint;
        if (disableCollider)
            cellGO.GetComponent<Collider2D>().enabled = false;
    }
private void HighlightRow(int tr, Actor attacker)
{
    // 1) Clear previous turn’s visuals & state
    ClearHighlights();
    _allowedColumns.Clear();
    _tintedColumns.Clear();
    if (tr < 0 || tr >= gridManager.rows) return;

    // 2) Build base movement list into our reusable buffer
    _movementBuffer.Clear();
    if (restrictToAdjacent)
    {
        _movementBuffer.Add(ballCol);
        if (ballCol - 1 >= 0) _movementBuffer.Add(ballCol - 1);
        if (ballCol + 1 < gridManager.cols) _movementBuffer.Add(ballCol + 1);
    }
    else
    {
        for (int c = 0; c < gridManager.cols; c++)
            _movementBuffer.Add(c);
    }

    // Forced Diagonal: strip out the straight‐ahead column
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.ForcedDiagonal))
    {
        _movementBuffer.RemoveAll(c => c == ballCol);
    }

    // 3) QUIT-OR-DOUBLE override
    bool qodActive = enableModifiers
                     && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble);
    if (qodActive)
    {
        foreach (int c in _movementBuffer)
        {
            _allowedColumns.Add(c);
            var go = gridManager.cells[tr, c];
            go.GetComponent<Cell>().Highlight(true);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;
        }
        int qodCol = matchModifierManager.GetQuitOrDoubleColumn();
        if (_allowedColumns.Contains(qodCol) && _tintedColumns.Add(qodCol))
            TintCell(tr, qodCol, new Color(1f, 0f, 1f, 0.5f));
        return;
    }

    // 4) NORMAL MODIFIER PRUNING

    // Locked Column
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.LockedColumn)
        && _lockedColThisTurn < 0
        && _movementBuffer.Count > 0)
    {
        _lockedColThisTurn = _movementBuffer[Random.Range(0, _movementBuffer.Count)];
        ShowModifier("Cell locked!", 2f);
    }
    if (_lockedColThisTurn >= 0)
        _movementBuffer.Remove(_lockedColThisTurn);

    // Burned Column
   // if (enableModifiers
     //   && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn))
   // {
   //     _movementBuffer.Remove(matchModifierManager.GetLastUsedColumn(mover));
  //  }

    // Blockade (defender-only)
    if (enableModifiers && phase == Phase.AwaitingDefense)
    {
        var defender = (attacker == Actor.Player) ? Actor.AI : Actor.Player;
        if (matchModifierManager.IsBlockadeReady(defender))
        {
            var pruned = _movementBuffer.OrderBy(_ => Random.value).Take(2).ToList();
            _movementBuffer.Clear();
            _movementBuffer.AddRange(pruned);
            matchModifierManager.ConsumeBlockade(defender);
            ShowModifier("Blockade!\nDefender limited to 2 columns", 3f);
        }
    }

    // Sabotage (attacker-only)
    if (enableModifiers && phase == Phase.PlayerAttack
        && matchModifierManager.IsSabotageReady(attacker))
    {
        var pruned = _movementBuffer.OrderBy(_ => Random.value).Take(2).ToList();
        _movementBuffer.Clear();
        _movementBuffer.AddRange(pruned);
        matchModifierManager.ConsumeSabotage(attacker);
        ShowModifier("Sabotage!\nAttacker limited to 2 columns", 3f);
    }

    // PushThrough
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.PushThrough)
        && _pushThroughBlockedCol < 0
        && _movementBuffer.Count > 0)
    {
        _pushThroughBlockedCol = _movementBuffer[Random.Range(0, _movementBuffer.Count)];
    }
    if (phase == Phase.AwaitingDefense && _pushThroughBlockedCol >= 0)
       _movementBuffer.Remove(_pushThroughBlockedCol);

    // Nothing left → auto-resolve
    if (_movementBuffer.Count == 0)
    {
        HandleEmptyMoves(tr, attacker);
        return;
    }

    // 5) Highlight all surviving cells
    foreach (int c in _movementBuffer)
    {
        _allowedColumns.Add(c);
        gridManager.cells[tr, c].GetComponent<Cell>().Highlight(true);
    }

    // 6) Apply tints (Locked, PushThrough, etc.) exactly as before
    if (_lockedColThisTurn >= 0)
        TintCell(tr, _lockedColThisTurn, new Color(1f, 0f, 0f, 0.5f), disableCollider: true);

    if (_pushThroughBlockedCol >= 0)
    {
        var color = (phase == Phase.PlayerAttack)
            ? new Color(1f, 1f, 0f, 0.5f)
            : new Color(1f, 0f, 0f, 0.5f);
        bool disable = (phase == Phase.AwaitingDefense);
        TintCell(tr, _pushThroughBlockedCol, color, disableCollider: disable);
    }

    // — Dynamic Corridor: blue
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.DynamicCorridor)
        && matchModifierManager.IsDynamicCorridorReady(attacker))
    {
        foreach (int c in _allowedColumns)
            TintCell(tr, c, new Color(0f, 0f, 1f, 0.5f));
    }

    // — Counter Strike: purple
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.CounterStrike)
        && matchModifierManager.IsCounterStrikeReady(attacker))
    {
        foreach (int c in _allowedColumns)
            TintCell(tr, c, new Color(0.5f, 0f, 0.5f, 0.5f));
    }

    // — Flight Path: cyan on the fast-lane column
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath))
    {
        int fp = matchModifierManager.GetFastLaneColumn();
        if (_allowedColumns.Contains(fp))
            TintCell(tr, fp, new Color(0f, 1f, 1f, 0.5f));
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

private void HandleEmptyMoves(int targetRow, Actor attacker)
{
    // attacker‐phase but no moves left → defender “wins” the clash
    if (phase == Phase.PlayerAttack)
    {
        attackChoice = ballCol;               // fallback
        defendChoice = AI_DefenseGuess();     // pick a defense so ResolveTurn can run
    }
    else // Phase.AwaitingDefense
    {
        defendChoice = ballCol;               // fallback
        attackChoice = AIAttackGuess();       // pick an attack
    }

    // now jump straight into resolution
    StartCoroutine(ResolveTurn(targetRow));
}

// ───────────────────────────────────────────────────────────────────────
// PURE HELPER: get legal columns for a given row & attacker, with no UI side-effects
// ───────────────────────────────────────────────────────────────────────
private List<int> GetLegalMoves(int tr, Actor mover)
{
    List<int> movement;
    if (enableModifiers && matchModifierManager.IsGridMasteryReady())
        movement = Enumerable.Range(0, gridManager.cols).ToList();
    else if (restrictToAdjacent)
        movement = GetAdjacentColumns();
    else
        movement = Enumerable.Range(0, gridManager.cols).ToList();

    // Locked Column: remove when mover is the defender
    if (enableModifiers
    && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.LockedColumn)
    && _lockedColThisTurn >= 0)
    
{
    _movementBuffer.Remove(_lockedColThisTurn);
}

    // Burned Column
   // if (enableModifiers
   //     && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn))
   // {
   //     _movementBuffer.Remove(matchModifierManager.GetLastUsedColumn(attacker));
   // }

    // Blockade (defender-only)
    if (enableModifiers
        && mover != possession
        && matchModifierManager.IsBlockadeReady(mover))
    {
        movement = movement.OrderBy(_ => Random.value)
                           .Take(2)
                           .ToList();
    }

    // PushThrough (defender-only)
    if (enableModifiers
        && mover != possession
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.PushThrough)
        && _pushThroughBlockedCol >= 0)
    {
       movement.Remove(_pushThroughBlockedCol);
    }

    // ForcedDiagonal (applies to both)
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.ForcedDiagonal))
    {
        movement = movement.Where(col => col != ballCol).ToList();
    }



    return movement;
}




// ───────────────────────────────────────────────────────────────────────
// 1) Scoring helpers
// ───────────────────────────────────────────────────────────────────────
private float ScoreAttackColumn(int col)
{
    // 0) Never allow the locked column
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.LockedColumn)
        && col == matchModifierManager.GetLockedColumn())
    {
        return float.NegativeInfinity;
    }

    // 1) Mirror Clash (attacker-only): must go straight if active
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MirrorClash)
        && col != ballCol)
    {
        return float.NegativeInfinity;
    }

    float score = 0f;
    int cols   = gridManager.cols;
    int center = cols / 2;

    // 2) slight center bias
    score += 1f - (Mathf.Abs(col - center) / (float)center);

    if (enableModifiers)
    {
        if (matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.DoubleAdvance))
            score += 2f;

        if (matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.EdgeBurst)
            && (col == 0 || col == cols - 1))
            score += 3f;

        if (matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
            && col == matchModifierManager.GetFastLaneColumn())
            score += 4f;

        if (matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.PushThrough)
            && col == _pushThroughBlockedCol)
            score += 5f;

        if (matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.Slipstream)
            && col != ballCol)
            score += 3f;

        if (matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble)
            && col == matchModifierManager.GetQuitOrDoubleColumn())
            score += 5f;
    }

    // 3) tiny randomness to break ties
    score += Random.Range(-0.5f, 0.5f);
    return score;
}

private float ScoreDefenseColumn(int col)
{
    float score = 0f;
    int cols   = gridManager.cols;
    int center = cols / 2;

    // ── NEW: defend against a possible EdgeBurst ──
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.EdgeBurst))
    {
        // figure out attacker’s next‐row legal moves
        int dir     = (possession == Actor.Player) ? +1 : -1;
        int nextRow = ballRow + dir;
        var atkLegal = GetLegalMoves(nextRow, possession);

        // if attacker *could* edge‐burst into 0 or cols-1, heavily defend that edge
        if (atkLegal.Contains(0) && col == 0)
            score += 6f;
        if (atkLegal.Contains(cols - 1) && col == cols - 1)
            score += 6f;
    }
    // Base: slight center bias
    score += 1f - (Mathf.Abs(col - center) / (float)center);

    if (enableModifiers)
    {
        // 1) Block Flight Path: highest priority
        if (matchModifierManager.HasModifier(
                MatchModifierDefinition.ModifierType.FlightPath))
        {
            int fpCol = matchModifierManager.GetFastLaneColumn();
            if (col == fpCol)
            {
                // big bonus to defend exactly the Flight Path column
                score += 6f;
            }
            else
            {
                // slight penalty elsewhere
                score -= 1f;
            }
        }

        // 2) Stop Slipstream if active
        if (matchModifierManager.HasModifier(
                MatchModifierDefinition.ModifierType.Slipstream)
            && col != ballCol)
        {
            score += 4f;
        }

        // 3) Counter Surge
        if (matchModifierManager.HasModifier(
                MatchModifierDefinition.ModifierType.CounterSurge))
        {
            score += 3f;
        }

        // 4) Forced Diagonal
        if (matchModifierManager.HasModifier(
                MatchModifierDefinition.ModifierType.ForcedDiagonal)
            && col != ballCol)
        {
            score += 2f;
        }

        // 5) Mirror Clash preference
        if (matchModifierManager.HasModifier(
                MatchModifierDefinition.ModifierType.MirrorClash))
        {
            int mirror = 2 * center - attackChoice;
            if (col == mirror)
            {
                score += 3f;
            }
        }

        // 6) Stall
        if (enableModifiers
    && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.Stall))
{
    // defender wants to pick the PushThrough column to guarantee a stall
    if (col == _pushThroughBlockedCol)
        score += 5f;   // large bonus for guaranteed Stall
    else if (col != ballCol)
        score += 2f;   // existing stall logic for any mismatch
}

        // 7) Never pick Locked Column
        if (matchModifierManager.HasModifier(
                MatchModifierDefinition.ModifierType.LockedColumn)
            && col == matchModifierManager.GetLockedColumn())
        {
            score -= 100f;
        }
    }

    // Tiebreaker randomness
    score += Random.Range(-0.5f, 0.5f);
    return score;
}


// ───────────────────────────────────────────────────────────────────────
// 2) AIAttackGuess with two-ply minimax
// ───────────────────────────────────────────────────────────────────────
private int AIAttackGuess()
{
    // 1) Prep
    int dir     = (possession == Actor.Player) ? +1 : -1;
    int nextRow = ballRow + dir;
    var attacker    = possession;
    var defender    = (possession == Actor.Player) ? Actor.AI : Actor.Player;
    int   cols      = gridManager.cols;
    int   center    = cols / 2;

    // 2) Base legal moves for attacker
    var moves = GetLegalMoves(nextRow, attacker);

    // 3) If Mirror Clash is active, scrub out any column 'c' for which
    //    defender could pick mirror = 2*center - c
    if (enableModifiers
        && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MirrorClash))
    {
        var defMoves = GetLegalMoves(nextRow, defender);
        moves.RemoveAll(c => defMoves.Contains(2 * center - c));
    }

    // 4) Fallback if we’ve stripped everything
    if (moves.Count == 0)
        moves = GetLegalMoves(nextRow, attacker);

    // 5) Two-ply minimax
    int   bestCol = moves[0];
    float bestNet = float.NegativeInfinity;

    foreach (int col in moves)
    {
        // a) immediate attack score
        float atkScore = ScoreAttackColumn(col);

        // b) simulate attacker move
        int oldRow = ballRow, oldCol = ballCol;
        ballRow = nextRow;
        ballCol = col;

        // c) defender’s reply
        var defChoices = GetLegalMoves(ballRow, defender);
        float worstDef = float.PositiveInfinity;
        foreach (int d in defChoices)
            worstDef = Mathf.Min(worstDef, ScoreDefenseColumn(d));

        // d) restore state
        ballRow = oldRow;
        ballCol = oldCol;

        // e) net value
        float net = atkScore - worstDef;
        if (net > bestNet)
        {
            bestNet = net;
            bestCol = col;
        }
    }

    return bestCol;
}


// ───────────────────────────────────────────────────────────────────────
// 3) AI_DefenseGuess with two-ply minimax
// ───────────────────────────────────────────────────────────────────────
private int AI_DefenseGuess()
{
    int dir     = (possession == Actor.Player) ? -1 : +1;
    int nextRow = ballRow + dir;

    // figure out who’s defending
    Actor defender = (possession == Actor.Player) ? Actor.AI : Actor.Player;

    // get their legal moves
    var choices = GetLegalMoves(nextRow, defender);
    if (choices.Count == 0)
        return Random.Range(0, gridManager.cols);

    int bestDef   = choices[0];
    float bestNet = float.PositiveInfinity;

    foreach (int col in choices)
    {
        float defScore = ScoreDefenseColumn(col);

        // simulate placing the ball there
        int oldRow = ballRow, oldCol = ballCol;
        ballRow = nextRow;
        ballCol = col;

        // attacker’s reply
        var atkChoices = GetLegalMoves(ballRow, possession);
        float bestAtk = float.NegativeInfinity;
        foreach (int a in atkChoices)
            bestAtk = Mathf.Max(bestAtk, ScoreAttackColumn(a));

        // restore
        ballRow = oldRow;
        ballCol = oldCol;

        float net = bestAtk - defScore;
        if (net < bestNet)
        {
            bestNet = net;
            bestDef = col;
        }
    }

    Debug.Log($"[GameManager] AI Defense picks: {bestDef} (net={bestNet})");
    return bestDef;
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

    // compute deltas
    int scoreDelta = _playerWon ? +10 : -5;
    int goldDelta  = _playerWon ? winReward : -entryFee;

    // Populate UI
    coinText.text   = (goldDelta >= 0 ? "+" : "") + goldDelta;
    trophyText.text = (scoreDelta >= 0 ? "+" : "") + scoreDelta;
    
    // make sure the icons are visible (or you could hide them on 0)
    coinIcon.enabled   = true;
    trophyIcon.enabled = true;

    resultPopup.SetActive(true);
    continueButton.interactable = false;

    // on win, wait for the server call; on loss just unlock immediately
    if (_playerWon)
        StartCoroutine(AwardGoldCoroutine());
    else
        continueButton.interactable = true;

    // always submit leaderboard delta
    _ = CozyLeaderboards
            .Instance
            .AddScoreToLeaderboard(leaderboardID, scoreDelta);

    continueButton.onClick.RemoveAllListeners();
    continueButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
}

// ---------------------------------------------
// GameManager : AwardGoldCoroutine()
// ---------------------------------------------
private IEnumerator AwardGoldCoroutine()
{
    // 1) Fire off the GainCurrency call
    var task = CozyAPI.Instance.GainCurrency(currencyId, winReward);

    // 2) Wait for the server round-trip
    while (!task.IsCompleted)
        yield return null;

    // 3) Error handling
    if (task.IsFaulted)
    {
        Debug.LogException(task.Exception);
        ShowMessage("Reward failed – check connectivity", 2f);
    }
    else
    {
        // 4) Update your in-game gold display (if you have one)
        UpdateGoldUI();
    }

    // 5) Now that gold is settled, let the player continue
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
