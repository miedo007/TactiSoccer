using System.Collections;
using System.Collections.Generic;
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

    [Header("Lose Prefabs (drag here)")]
    public GameObject playerLosePrefab;   // Prefab to throw when Player loses
    public GameObject aiLosePrefab;       // Prefab to throw when AI loses

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
    public MMFeedbacks feedbackPlayerAdvance;    // when player advances
    public MMFeedbacks feedbackOpponentAdvance;  // when AI advances
    public MMFeedbacks feedbackTackleWin;        // on successful player tackle
    public MMFeedbacks feedbackTackleLose;       // on failed player tackle
    public MMFeedbacks feedbackPenaltySaved;     // on penalty save
    public MMFeedbacks feedbackPenaltyCounter;   // on penalty counter
    public MMFeedbacks feedbackGoalForPlayer;    // on player goal
    public MMFeedbacks feedbackGoalAgainst;      // on AI goal
    public MMFeedbacks feedbackMatchWin;         // on match win
    public MMFeedbacks feedbackMatchLose;        // on match lose

    [Header("Power-Up (assign in Inspector)")]
    public PowerUpManager powerUpManager;
    public PowerUpSpawner  powerUpSpawner;

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
        if (playerLosePrefab == null || aiLosePrefab == null) Debug.LogError("Lose prefabs not assigned!");
        if (penaltyPanel == null || penaltyButtons == null || penaltyButtons.Length == 0
            || penaltyBall == null || goalkeeperImage == null || goalkeeperIdleAnchor == null)
            Debug.LogError("Penalty UI not assigned!");
        if (goldText == null || potText == null || messageText == null
            || betPanel == null || bet5Button == null || bet10Button == null
            || bet20Button == null || restartButton == null)
            Debug.LogError("UI references not fully assigned!");
        if (powerUpManager == null) Debug.LogError("PowerUpManager not assigned!");
        if (powerUpSpawner  == null) Debug.LogError("PowerUpSpawner not assigned!");

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

    void OnBetSelected(int amount)
    {
        // **RESET FOR A NEW MATCH**
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

        ShowMessage(
            possession == Actor.Player ? "You Kick-Off" : "Opponent Kick-Off",
            1.5f
        );

        betPanel.SetActive(false);
        bet5Button.gameObject.SetActive(false);
        bet10Button.gameObject.SetActive(false);
        bet20Button.gameObject.SetActive(false);

        SpawnCharacter();
        EnableGrid();

        // spawn pickups for this match
        powerUpSpawner.SpawnDrops();

        StartNewTurn();
    }

    void StartNewTurn()
    {
        if (possession == Actor.Player)
        {
            phase = Phase.PlayerAttack;
            HighlightRow(ballRow + 1, Actor.Player);
        }
        else
        {
            phase = Phase.AwaitingDefense;
            attackChoice = AI_Guess();
            HighlightRow(ballRow - 1, Actor.AI);
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
        Actor attacker = possession;
        bool tackle = (attackChoice == defendChoice);

        // Tackle vs. Advance feedback
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

        // *** NEW: manual pickup check ***
        CheckForPickups();

        // Check for goal line
        bool goal = !tackle &&
            ((attacker == Actor.Player && ballRow == gridManager.rows - 1) ||
             (attacker == Actor.AI     && ballRow == 0));
        if (goal)
        {
            StartCoroutine(PenaltySequence(attacker));
            yield break;
        }

        // Tackle resolution: throw loser
        if (tackle)
        {
            Vector3 cellPos = gridManager.GetCellPosition(ballRow, ballCol);
            GameObject loser = Instantiate(
                attacker == Actor.Player ? aiLosePrefab : playerLosePrefab,
                cellPos,
                Quaternion.identity
            );
            StartCoroutine(ThrowOffScreen(loser, attacker));

            possession = (attacker == Actor.Player) ? Actor.AI : Actor.Player;
            SpawnCharacter();
            StartNewTurn();
        }
        else
        {
            StartNewTurn();
        }
    }

    /// <summary>
    /// Manually detect any pickup at ball position and invoke it.
    /// </summary>
    private void CheckForPickups()
    {
        Vector2 pos2d = ballInstance.transform.position;
        float radius = 0.1f;
        var hits = Physics2D.OverlapCircleAll(pos2d, radius);
        foreach (var hit in hits)
        {
            var pu = hit.GetComponent<PowerUpPickup>();
            if (pu != null)
            {
                pu.ManualPickup(ballInstance);
            }
        }
    }

    private IEnumerator ThrowOffScreen(GameObject loser, Actor attacker)
    {
        float elapsed = 0f;
        float duration = tackleAnimDuration;
        Vector3 start = loser.transform.position;
        Vector3 direction = (attacker == Actor.Player) ? Vector3.down : Vector3.up;
        Vector3 end = start + direction * (gridManager.rows + 1);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            loser.transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        Destroy(loser);
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
        goalkeeperImage.sprite = (attacker ==
                                   Actor.Player) ? aiGKIdleSprite : playerGKIdleSprite;
        goalkeeperImage.rectTransform.anchoredPosition =
            goalkeeperIdleAnchor.anchoredPosition;
        goalkeeperImage.rectTransform.localScale = goalkeeperBaseScale;
        goalkeeperImage.gameObject.SetActive(true);

        // 2) preassign other side
        if (attacker == Actor.Player)
            penaltyDefendChoice = Random.Range(0, penaltyButtons.Length);
        else
            penaltyAttackChoice = Random.Range(0, penaltyButtons.Length);

        // 3) wait for click
        while (!penaltyChoiceMade) yield return null;

        // 4) compute shoot/defend targets
        Vector2 shootTarget = penaltyButtons[penaltyAttackChoice]
            .GetComponent<RectTransform>().anchoredPosition;
        Vector2 defendTarget = penaltyButtons[penaltyDefendChoice]
            .GetComponent<RectTransform>().anchoredPosition;

        // 5) GK jump & ball fly
        Vector2 keeperIdlePos = goalkeeperIdleAnchor.anchoredPosition;
        var flipScale = goalkeeperBaseScale;
        flipScale.x = (defendTarget.x < keeperIdlePos.x)
            ? -Mathf.Abs(flipScale.x)
            : Mathf.Abs(flipScale.x);
        goalkeeperImage.rectTransform.localScale = flipScale;
        goalkeeperImage.sprite = (attacker == Actor.Player)
            ? aiGKJumpSprite
            : playerGKJumpSprite;

        float elapsed2 = 0f;
        while (elapsed2 < penaltyAnimDuration)
        {
            elapsed2 += Time.deltaTime;
            float tBall = Mathf.Clamp01(elapsed2 / penaltyAnimDuration);
            float tGK = Mathf.Clamp01(elapsed2 / goalkeeperJumpDuration);

            penaltyBall.anchoredPosition = Vector2.Lerp(
                penaltyBallStartPos, shootTarget, tBall);
            penaltyBall.localScale = Vector3.Lerp(
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
            .GetComponent<Image>().color = Color.red;

        bool saved = (penaltyAttackChoice == penaltyDefendChoice);
        if (saved)
        {
            if (attacker == Actor.Player)
            {
                ShowMessage("Countered", 1f);
                feedbackPenaltyCounter?.PlayFeedbacks();
                possession = Actor.AI;
            }
            else
            {
                ShowMessage("Saved", 1f);
                feedbackPenaltySaved?.PlayFeedbacks();
                possession = Actor.Player;
            }

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

        yield break;
    }

    public void OnPenaltyButton(int idx)
    {
        if (penaltyAttacker == Actor.Player)
            penaltyAttackChoice = idx;
        else
            penaltyDefendChoice = idx;
        penaltyChoiceMade = true;
    }

    // Updated to respect Focus power-up
    private void HighlightRow(int tr, Actor attacker)
    {
        ClearHighlights();
        if (tr < 0 || tr >= gridManager.rows) return;

        List<int> allowed = powerUpManager.GetAllowedColumns(attacker.ToString());
        foreach (int c in allowed)
            gridManager.cells[tr, c].GetComponent<Cell>().Highlight(true);
    }

    // Legacy overload
    private void HighlightRow(int tr)
    {
        HighlightRow(tr, possession);
    }

    private void ClearHighlights()
    {
        foreach (var cellGO in gridManager.AllCells)
            cellGO.GetComponent<Cell>().Highlight(false);
    }

    private int AI_Guess() => Random.Range(0, gridManager.cols);
    private void UpdatePotUI() => potText.text = $"Pot: {pot}g";
    private void UpdateGoldUI() => goldText.text = $"Gold: {playerGold}g";

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
