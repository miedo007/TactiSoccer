using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
// 1) import the Feel namespace
using MoreMountains.Feedbacks;

public class GameManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public GridManager gridManager;
    public GameObject ballPrefab;         // Player character prefab
    public GameObject aiPrefab;           // AI character prefab
    public GameObject playerTacklePrefab; // Player tackle visual prefab
    public GameObject aiTacklePrefab;     // AI tackle visual prefab

    [Header("Penalty UI (Canvas)")]
    public GameObject penaltyPanel;       // Panel containing penalty UI
    public Button[] penaltyButtons;       // Buttons for penalty zones
    public RectTransform penaltyBall;     // UI Image for penalty ball
    public Image goalkeeperImage;         // UI Image component for goalkeeper
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
    public float tackleAnimDuration     = 0.5f;
    public float penaltyAnimDuration    = 0.5f;
    public float goalkeeperJumpDuration = 0.5f;
    public float afterAnimDelay         = 0.5f;
    public Vector3 penaltyBallStartScale = Vector3.one;
    public Vector3 penaltyBallEndScale   = Vector3.one * 0.5f;

    [Header("Feel Feedbacks")]
    public MMFeedbacks feedbackPlayerAdvance;    // Feel asset when player advances
    public MMFeedbacks feedbackOpponentAdvance;  // Feel asset when opponent advances
    public MMFeedbacks feedbackTackleWin;        // Feel asset when player succeeds a tackle
    public MMFeedbacks feedbackTackleLose;       // Feel asset when player is tackled
    public MMFeedbacks feedbackPenaltySaved;     // Feel asset when player blocks a penalty
    public MMFeedbacks feedbackPenaltyCounter;   // Feel asset when opponent blocks a penalty
    public MMFeedbacks feedbackGoalForPlayer;    // Feel asset when player scores
    public MMFeedbacks feedbackGoalAgainst;      // Feel asset when opponent scores
    public MMFeedbacks feedbackMatchWin;         // Feel asset on match win
    public MMFeedbacks feedbackMatchLose;        // Feel asset on match lose

    // private state
    private int playerGold, currentBet, pot;
    private GameObject ballInstance;
    private BallController ballCtrl;
    private int ballRow, ballCol;

    private enum Actor { Player, AI }
    private Actor possession;

    private enum Phase { PlayerAttack, AwaitingDefense }
    private Phase phase;

    private int attackChoice, defendChoice;

    // Penalty state
    private Actor penaltyAttacker;
    private bool penaltyChoiceMade;
    private int penaltyAttackChoice, penaltyDefendChoice;
    private Vector2 penaltyBallStartPos;
    private Vector3 goalkeeperBaseScale;

    // Message clear coroutine handle
    private Coroutine _clearMsgCoroutine;

    void Start()
    {
        // Sanity checks
        if (gridManager == null) Debug.LogError("GridManager not assigned!");
        if (ballPrefab == null || aiPrefab == null) Debug.LogError("Character prefabs not assigned!");
        if (playerTacklePrefab == null || aiTacklePrefab == null) Debug.LogError("Tackle prefabs not assigned!");
        if (penaltyPanel == null || penaltyButtons == null || penaltyButtons.Length == 0
            || penaltyBall == null || goalkeeperImage == null || goalkeeperIdleAnchor == null)
            Debug.LogError("Penalty UI not assigned!");
        if (goldText == null || potText == null || messageText == null
            || betPanel == null || bet5Button == null || bet10Button == null
            || bet20Button == null || restartButton == null)
            Debug.LogError("UI references not fully assigned!");

        // Store keeper base scale
        goalkeeperBaseScale = goalkeeperImage.rectTransform.localScale;

        // Initialize grid cells
        for (int r = 0; r < gridManager.rows; r++)
            for (int c = 0; c < gridManager.cols; c++)
                gridManager.cells[r, c].GetComponent<Cell>().Initialize(r, c, this);

        // Randomize possession and center ball
        possession = (Random.value < 0.5f) ? Actor.Player : Actor.AI;
        ballRow     = gridManager.rows / 2;
        ballCol     = gridManager.cols / 2;

        // Setup penalty UI
        penaltyBallStartPos    = penaltyBall.anchoredPosition;
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        penaltyPanel.SetActive(false);

        for (int i = 0; i < penaltyButtons.Length; i++)
        {
            int idx = i;
            penaltyButtons[i].onClick.RemoveAllListeners();
            penaltyButtons[i].onClick.AddListener(() => OnPenaltyButton(idx));
        }

        // Initialize gold/UI
        playerGold = startingGold;
        UpdateGoldUI();
        messageText.text = string.Empty;

        // Hook up betting UI
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
        var prefab = (possession == Actor.Player) ? ballPrefab : aiPrefab;
        ballInstance = Instantiate(prefab,
            gridManager.GetCellPosition(ballRow, ballCol),
            Quaternion.identity);
        ballCtrl = ballInstance.GetComponent<BallController>();
    }

    /// <summary>
    /// Called when the player clicks any bet button.
    /// Resets match state if coming from a finished match.
    /// </summary>
    void OnBetSelected(int amount)
    {
        // **RESET FOR A NEW MATCH**
        // randomize new possession
        possession = (Random.value < 0.5f) ? Actor.Player : Actor.AI;
        // recenter the ball
        ballRow = gridManager.rows / 2;
        ballCol = gridManager.cols / 2;
        // hide any lingering penalty UI
        penaltyPanel.SetActive(false);
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        // reset penalty buttons
        foreach (var btn in penaltyButtons)
        {
            btn.interactable = true;
            btn.GetComponent<Image>().color = Color.white;
        }
        ClearHighlights();

        // now process the ante as usual
        if (amount > playerGold)
        {
            ShowMessage("Not enough gold!", 1f);
            return;
        }
        currentBet = amount;
        playerGold -= amount;
        UpdateGoldUI();

        // Both ante
        pot = currentBet * 2;
        UpdatePotUI();

        // Announce possession
        ShowMessage(
            possession == Actor.Player ? "You Kick-Off" : "Opponent Kick-Off",
            1.5f
        );

        // Hide bet UI
        betPanel.SetActive(false);
        bet5Button.gameObject.SetActive(false);
        bet10Button.gameObject.SetActive(false);
        bet20Button.gameObject.SetActive(false);

        // Spawn and begin
        SpawnCharacter();
        EnableGrid();
        StartNewTurn();
    }

    void StartNewTurn()
    {
        if (possession == Actor.Player)
        {
            phase = Phase.PlayerAttack;
            HighlightRow(ballRow + 1);
        }
        else
        {
            phase = Phase.AwaitingDefense;
            attackChoice = AI_Guess();
            HighlightRow(ballRow - 1);
        }
    }

    public void OnCellClicked(int r, int c)
    {
        if (betPanel.activeSelf) return;
        int targetRow = (possession == Actor.Player) ? ballRow + 1 : ballRow - 1;
        if (r != targetRow) return;

        if (possession == Actor.Player && phase == Phase.PlayerAttack)
        {
            attackChoice = c;
            defendChoice = AI_Guess();
            StartCoroutine(ResolveTurn(targetRow));
        }
        else if (possession == Actor.AI && phase == Phase.AwaitingDefense)
        {
            defendChoice = c;
            StartCoroutine(ResolveTurn(targetRow));
        }
    }

    private IEnumerator ResolveTurn(int targetRow)
    {
        var atkCell = gridManager.cells[targetRow, attackChoice].GetComponent<Cell>();
        var defCell = gridManager.cells[targetRow, defendChoice].GetComponent<Cell>();
        Actor attacker = possession;
        bool tackle = (attackChoice == defendChoice);

        // Tackle case
        if (tackle)
        {
            if (attacker == Actor.Player)
            {
                ShowMessage("Tackled!", 1f);
                feedbackTackleLose?.PlayFeedbacks();
            }
            else
            {
                ShowMessage("Tackle!", 1f);
                feedbackTackleWin?.PlayFeedbacks();
            }
        }
        // Advance case
        else
        {
            if (attacker == Actor.Player)
            {
                ShowMessage("Dribble!", 1f);
                feedbackPlayerAdvance?.PlayFeedbacks();
            }
            else
            {
                ShowMessage("Dribbled!", 1f);
                feedbackOpponentAdvance?.PlayFeedbacks();
            }
            pot += bonusPerAdvance;
            UpdatePotUI();
        }

        yield return new WaitForSeconds(tackleAnimDuration);
        ClearHighlights();

        // Move the ball
        ballRow = targetRow;
        ballCol = attackChoice;
        yield return StartCoroutine(
            ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol))
        );

        // Check for goal line
        bool goal = !tackle &&
            ((attacker == Actor.Player && ballRow == gridManager.rows - 1) ||
             (attacker == Actor.AI     && ballRow == 0));
        if (goal)
        {
            StartCoroutine(PenaltySequence(attacker));
            yield break;
        }

        // On actual tackle flip possession
        if (tackle)
        {
            var tacklePrefab = (attacker == Actor.Player)
                ? aiTacklePrefab
                : playerTacklePrefab;
            var tackleGO = Instantiate(
                tacklePrefab,
                gridManager.GetCellPosition(ballRow, ballCol),
                Quaternion.identity
            );
            yield return new WaitForSeconds(tackleAnimDuration);
            Destroy(tackleGO);

            possession = (attacker == Actor.Player) ? Actor.AI : Actor.Player;
            SpawnCharacter();
            StartNewTurn();
        }
        else
        {
            StartNewTurn();
        }
    }

    private IEnumerator PenaltySequence(Actor attacker)
    {
        // Show penalty UI & disable grid
        penaltyAttacker     = attacker;
        penaltyChoiceMade   = false;
        penaltyAttackChoice = -1;
        penaltyDefendChoice = -1;
        penaltyPanel.SetActive(true);
        DisableGrid();

        // Show ball immediately
        penaltyBall.anchoredPosition = penaltyBallStartPos;
        penaltyBall.localScale       = penaltyBallStartScale;
        penaltyBall.gameObject.SetActive(true);

        // 1) idle GK
        goalkeeperImage.sprite = (attacker == Actor.Player)
            ? aiGKIdleSprite
            : playerGKIdleSprite;
        goalkeeperImage.rectTransform.anchoredPosition = goalkeeperIdleAnchor.anchoredPosition;
        goalkeeperImage.rectTransform.localScale       = goalkeeperBaseScale;
        goalkeeperImage.gameObject.SetActive(true);

        // 2) preassign other side
        if (attacker == Actor.Player)
            penaltyDefendChoice = Random.Range(0, penaltyButtons.Length);
        else
            penaltyAttackChoice = Random.Range(0, penaltyButtons.Length);

        // 3) wait for click
        while (!penaltyChoiceMade) yield return null;

        // 4) compute shoot/defend targets
        Vector2 shootTarget  = penaltyButtons[penaltyAttackChoice]
            .GetComponent<RectTransform>().anchoredPosition;
        Vector2 defendTarget = penaltyButtons[penaltyDefendChoice]
            .GetComponent<RectTransform>().anchoredPosition;

        // 5) GK jump & ball fly
        Vector2 keeperIdlePos = goalkeeperIdleAnchor.anchoredPosition;
        var flipScale = goalkeeperBaseScale;
        flipScale.x = (defendTarget.x < keeperIdlePos.x)
            ? -Mathf.Abs(flipScale.x)
            :  Mathf.Abs(flipScale.x);
        goalkeeperImage.rectTransform.localScale = flipScale;
        goalkeeperImage.sprite = (attacker == Actor.Player)
            ? aiGKJumpSprite
            : playerGKJumpSprite;

        float elapsed = 0f;
        while (elapsed < penaltyAnimDuration)
        {
            elapsed += Time.deltaTime;
            float tBall = Mathf.Clamp01(elapsed / penaltyAnimDuration);
            float tGK   = Mathf.Clamp01(elapsed / goalkeeperJumpDuration);

            penaltyBall.anchoredPosition = Vector2.Lerp(
                penaltyBallStartPos, shootTarget, tBall);
            penaltyBall.localScale       = Vector3.Lerp(
                penaltyBallStartScale, penaltyBallEndScale, tBall);
            goalkeeperImage.rectTransform.anchoredPosition =
                Vector2.Lerp(keeperIdlePos, defendTarget, tGK);

            yield return null;
        }

        yield return new WaitForSeconds(afterAnimDelay);

        // highlight choices
        penaltyButtons[penaltyAttackChoice]
            .GetComponent<Image>().color = Color.green;
        penaltyButtons[penaltyDefendChoice]
            .GetComponent<Image>().color   = Color.red;

        bool saved = (penaltyAttackChoice == penaltyDefendChoice);
        if (saved)
        {
            // penalty blocked
            if (attacker == Actor.Player)
            {
                // opponent blocked
                ShowMessage("Countered", 1f);
                feedbackPenaltyCounter?.PlayFeedbacks();
                possession = Actor.AI;
            }
            else
            {
                // player blocked
                ShowMessage("Saved", 1f);
                feedbackPenaltySaved?.PlayFeedbacks();
                possession = Actor.Player;
            }

            // reset UI
            foreach (var btn in penaltyButtons)
            {
                btn.interactable = true;
                btn.GetComponent<Image>().color = Color.white;
            }

            penaltyBall.gameObject.SetActive(false);
            penaltyPanel.SetActive(false);
            goalkeeperImage.gameObject.SetActive(false);

            EnableGrid();
            SpawnCharacter();
            StartNewTurn();
        }
        else
        {
            // goal scored
            if (attacker == Actor.Player)
            {
                playerGold += pot;
                UpdateGoldUI();
                ShowMessage("GOAAAAAL! You Win!", 2f);
                feedbackGoalForPlayer?.PlayFeedbacks();
                feedbackMatchWin?.PlayFeedbacks();
            }
            else
            {
                ShowMessage("GOAAAAAL! You Lose!", 2f);
                feedbackGoalAgainst?.PlayFeedbacks();
                feedbackMatchLose?.PlayFeedbacks();
            }

            penaltyBall.gameObject.SetActive(false);
            penaltyPanel.SetActive(false);
            goalkeeperImage.gameObject.SetActive(false);
            EndMatch();
        }
    }

    public void OnPenaltyButton(int idx)
    {
        if (penaltyAttacker == Actor.Player)
            penaltyAttackChoice = idx;
        else
            penaltyDefendChoice = idx;
        penaltyChoiceMade = true;
    }

    private void HighlightRow(int tr)
    {
        ClearHighlights();
        if (tr < 0 || tr >= gridManager.rows) return;
        foreach (var cellGO in gridManager.Row(tr))
            cellGO.GetComponent<Cell>().Highlight(true);
    }

    private void ClearHighlights()
    {
        foreach (var cellGO in gridManager.AllCells)
            cellGO.GetComponent<Cell>().Highlight(false);
    }

    private int AI_Guess() => Random.Range(0, gridManager.cols);
    private void UpdatePotUI() => potText.text = $"Pot: {pot}g";
    private void UpdateGoldUI() => goldText.text = $"Gold: {playerGold}g";

    // **MODIFIED**: EndMatch now returns straight to the bet screen
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
        restartButton.gameObject.SetActive(false);
        messageText.text = string.Empty;
        possession = (Random.value < 0.5f) ? Actor.Player : Actor.AI;
        ballRow = gridManager.rows / 2;
        ballCol = gridManager.cols / 2;

        penaltyPanel.SetActive(false);
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        foreach (var btn in penaltyButtons)
        {
            btn.interactable = true;
            btn.GetComponent<Image>().color = Color.white;
        }

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

    // Helper to show a message for a limited duration
    private void ShowMessage(string msg, float duration)
    {
        if (_clearMsgCoroutine != null)
            StopCoroutine(_clearMsgCoroutine);
        messageText.text = msg;
        _clearMsgCoroutine = StartCoroutine(ClearAfter(duration));
    }

    private IEnumerator ClearAfter(float t)
    {
        yield return new WaitForSeconds(t);
        messageText.text = "";
        _clearMsgCoroutine = null;
    }
}
