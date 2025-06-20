using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
// 1) import the Feel namespace
using MoreMountains.Feedbacks;

public class GameManager : MonoBehaviour
{
    public enum Actor { Player, AI }
    private enum Phase { PlayerAttack, AwaitingDefense }

    [Header("References (assign in Inspector)")]
    public GridManager    gridManager;
    public PowerUpManager powerUpManager;
    public GameObject     ballPrefab;
    public GameObject     aiPrefab;

    [Header("Lose Prefabs (drag here)")]
    public GameObject playerLosePrefab;
    public GameObject aiLosePrefab;

    [Header("Penalty UI (Canvas)")]
    public GameObject    penaltyPanel;
    public Button[]      penaltyButtons;
    public RectTransform penaltyBall;
    public Image         goalkeeperImage;
    public Sprite        playerGKIdleSprite;
    public Sprite        playerGKJumpSprite;
    public Sprite        aiGKIdleSprite;
    public Sprite        aiGKJumpSprite;

    [Header("Penalty GK Position")]
    public RectTransform goalkeeperIdleAnchor;

    [Header("UI Text")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI potText;
    public TextMeshProUGUI messageText;

    [Header("Bet UI")]
    public GameObject betPanel;
    public Button     bet5Button;
    public Button     bet10Button;
    public Button     bet20Button;
    public Button     restartButton;

    [Header("Bet Settings")]
    public int startingGold    = 20;
    public int bonusPerAdvance = 1;

    [Header("Animation Settings")]
    public float   tackleAnimDuration     = 0.5f;
    public float   penaltyAnimDuration    = 0.5f;
    public float   goalkeeperJumpDuration = 0.5f;
    public float   afterAnimDelay         = 0.5f;
    public Vector3 penaltyBallStartScale  = Vector3.one;
    public Vector3 penaltyBallEndScale    = Vector3.one * 0.5f;

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

    public GameObject CurrentBallInstance => _ballInstance;

    private int        playerGold, currentBet, pot;
    private GameObject _ballInstance;
    private BallController _ballCtrl;
    private int        ballRow, ballCol;

    private Actor possession;
    private Phase phase;

    private int attackChoice, defendChoice;

    // Penalty state
    private Actor   penaltyAttacker;
    private bool    penaltyChoiceMade;
    private int     penaltyAttackChoice, penaltyDefendChoice;
    private Vector2 penaltyBallStartPos;
    private Vector3 goalkeeperBaseScale;

    private Coroutine _clearMsgCoroutine;

    void Start()
    {
        // Sanity checks...
        goalkeeperBaseScale = goalkeeperImage.rectTransform.localScale;

        // Initialize grid cells
        for (int r = 0; r < gridManager.rows; r++)
            for (int c = 0; c < gridManager.cols; c++)
                gridManager.cells[r, c]
                    .GetComponent<Cell>()
                    .Initialize(r, c, this);

        // Randomize possession & center ball
        possession = (Random.value < 0.5f) ? Actor.Player : Actor.AI;
        ballRow     = gridManager.rows / 2;
        ballCol     = gridManager.cols / 2;

        // Penalty UI off
        penaltyBallStartPos = penaltyBall.anchoredPosition;
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        penaltyPanel.SetActive(false);

        for (int i = 0; i < penaltyButtons.Length; i++)
        {
            int idx = i;
            penaltyButtons[i].onClick
                .RemoveAllListeners();
            penaltyButtons[i].onClick
                .AddListener(() => OnPenaltyButton(idx));
        }

        // Initialize gold/UI
        playerGold = startingGold;
        UpdateGoldUI();
        messageText.text = "";

        // Betting UI
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
        if (_ballInstance != null) Destroy(_ballInstance);
        var prefab = (possession == Actor.Player)
            ? ballPrefab
            : aiPrefab;
        _ballInstance = Instantiate(
            prefab,
            gridManager.GetCellPosition(ballRow, ballCol),
            Quaternion.identity
        );
        _ballCtrl = _ballInstance.GetComponent<BallController>();
    }

    void OnBetSelected(int amount)
    {
        possession = (Random.value < 0.5f)
            ? Actor.Player
            : Actor.AI;
        ballRow = gridManager.rows / 2;
        ballCol = gridManager.cols / 2;

        powerUpManager.SetupNewMatch();

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
            possession == Actor.Player
                ? "You Kick-Off"
                : "Opponent Kick-Off",
            1.5f
        );

        betPanel.SetActive(false);
        bet5Button.gameObject.SetActive(false);
        bet10Button.gameObject.SetActive(false);
        bet20Button.gameObject.SetActive(false);

        SpawnCharacter();
        EnableGrid();
        StartNewTurn();
    }

    void StartNewTurn()
    {
        // clear any previous blockade
        EnableGrid();
        powerUpManager.ClearBlockade();
        ClearHighlights();

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

            // apply blockade if defender had it
            if (powerUpManager.ShouldBlockade(Actor.Player))
            {
                powerUpManager.ApplyBlockade(ballRow - 1, 2);
                ShowMessage("Blockade! 2 columns disabled", 1f);
            }
        }
    }

    public void OnCellClicked(int r, int c)
    {
        if (betPanel.activeSelf) return;
        int targetRow = (possession == Actor.Player)
            ? ballRow + 1
            : ballRow - 1;
        if (r != targetRow) return;

        if (possession == Actor.Player
            && phase == Phase.PlayerAttack)
        {
            attackChoice = c;
            defendChoice = AI_Guess();
            StartCoroutine(ResolveTurn(targetRow));
        }
        else if (possession == Actor.AI
                 && phase == Phase.AwaitingDefense)
        {
            defendChoice = c;
            StartCoroutine(ResolveTurn(targetRow));
        }
    }

    private IEnumerator ResolveTurn(int targetRow)
    {
        Actor attacker = possession;
        Actor defender = (attacker == Actor.Player)
            ? Actor.AI
            : Actor.Player;
        bool tackle = (attackChoice == defendChoice);

        // Shield: next tackle fails
        if (tackle
            && powerUpManager.ConsumeShield(defender))
        {
            tackle = false;
            ShowMessage("Tackle Denied", 1f);
        }

        // Feedback
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
            int extra = powerUpManager.GetAdvanceBonus(attacker);
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
            pot += bonusPerAdvance + extra;
            UpdatePotUI();
        }

        yield return new WaitForSeconds(tackleAnimDuration);

        // Collect power-up
        ballRow = targetRow;
        ballCol = attackChoice;
        var pu = powerUpManager.CollectIfAny(
            ballRow, ballCol, attacker
        );
        if (pu != PowerUpType.None)
            ShowMessage($"Picked up {pu}!", 1f);

        ClearHighlights();

        // Move ball
        yield return StartCoroutine(
            _ballCtrl.MoveToCell(
                gridManager.GetCellPosition(
                    ballRow, ballCol
                )
            )
        );

        // Goal?
        bool goal = !tackle
                 && ((attacker == Actor.Player
                      && ballRow == gridManager.rows - 1)
                     || (attacker == Actor.AI
                         && ballRow == 0));
        if (goal)
        {
            StartCoroutine(PenaltySequence(attacker));
            yield break;
        }

        // Tackle resolution: throw loser
        if (tackle)
        {
            Vector3 cellPos =
                gridManager.GetCellPosition(
                    ballRow, ballCol
                );
            GameObject loser = Instantiate(
                attacker == Actor.Player
                    ? aiLosePrefab
                    : playerLosePrefab,
                cellPos,
                Quaternion.identity
            );
            StartCoroutine(
                ThrowOffScreen(loser, attacker)
            );

            possession = defender;
            SpawnCharacter();
        }

        StartNewTurn();
    }

    private IEnumerator ThrowOffScreen(
        GameObject loser,
        Actor attacker
    )
    {
        float elapsed = 0f;
        float duration = tackleAnimDuration;
        Vector3 start = loser.transform.position;
        Vector3 direction = (attacker == Actor.Player)
            ? Vector3.down
            : Vector3.up;
        Vector3 end =
            start + direction * (gridManager.rows + 1);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            loser.transform.position =
                Vector3.Lerp(
                    start, end, elapsed / duration
                );
            yield return null;
        }

        Destroy(loser);
        yield break;
    }

    private IEnumerator PenaltySequence(Actor attacker)
    {
        penaltyAttacker     = attacker;
        penaltyChoiceMade   = false;
        penaltyAttackChoice = -1;
        penaltyDefendChoice = -1;
        penaltyPanel.SetActive(true);
        DisableGrid();

        penaltyBall.anchoredPosition =
            penaltyBallStartPos;
        penaltyBall.localScale = penaltyBallStartScale;
        penaltyBall.gameObject.SetActive(true);

        goalkeeperImage.sprite = (attacker == Actor.Player)
            ? aiGKIdleSprite
            : playerGKIdleSprite;
        goalkeeperImage.rectTransform
            .anchoredPosition =
            goalkeeperIdleAnchor.anchoredPosition;
        goalkeeperImage.rectTransform.localScale =
            goalkeeperBaseScale;
        goalkeeperImage.gameObject.SetActive(true);

        if (attacker == Actor.Player)
            penaltyDefendChoice = Random.Range(
                0,
                penaltyButtons.Length
            );
        else
            penaltyAttackChoice = Random.Range(
                0,
                penaltyButtons.Length
            );

        while (!penaltyChoiceMade) yield return null;

        Vector2 shootTarget =
            penaltyButtons[penaltyAttackChoice]
                .GetComponent<RectTransform>()
                .anchoredPosition;
        Vector2 defendTarget =
            penaltyButtons[penaltyDefendChoice]
                .GetComponent<RectTransform>()
                .anchoredPosition;

        Vector2 keeperIdlePos =
            goalkeeperIdleAnchor.anchoredPosition;
        var flipScale = goalkeeperBaseScale;
        flipScale.x =
            (defendTarget.x < keeperIdlePos.x)
                ? -Mathf.Abs(flipScale.x)
                : Mathf.Abs(flipScale.x);
        goalkeeperImage.rectTransform.localScale =
            flipScale;
        goalkeeperImage.sprite =
            (attacker == Actor.Player)
                ? aiGKJumpSprite
                : playerGKJumpSprite;

        float elapsed2 = 0f;
        while (elapsed2 < penaltyAnimDuration)
        {
            elapsed2 += Time.deltaTime;
            float tBall =
                Mathf.Clamp01(
                    elapsed2 / penaltyAnimDuration
                );
            float tGK =
                Mathf.Clamp01(
                    elapsed2 / goalkeeperJumpDuration
                );

            penaltyBall.anchoredPosition =
                Vector2.Lerp(
                    penaltyBallStartPos,
                    shootTarget,
                    tBall
                );
            penaltyBall.localScale =
                Vector3.Lerp(
                    penaltyBallStartScale,
                    penaltyBallEndScale,
                    tBall
                );
            goalkeeperImage.rectTransform
                .anchoredPosition =
                Vector2.Lerp(
                    keeperIdlePos,
                    defendTarget,
                    tGK
                );

            yield return null;
        }

        yield return new WaitForSeconds(afterAnimDelay);

        penaltyButtons[penaltyAttackChoice]
            .GetComponent<Image>()
            .color = Color.green;
        penaltyButtons[penaltyDefendChoice]
            .GetComponent<Image>()
            .color = Color.red;

        bool saved =
            (penaltyAttackChoice
             == penaltyDefendChoice);
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
                btn.GetComponent<Image>().color =
                    Color.white;
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
        messageText.text = "";
        possession = (Random.value < 0.5f)
            ? Actor.Player
            : Actor.AI;
        ballRow = gridManager.rows / 2;
        ballCol = gridManager.cols / 2;

        penaltyPanel.SetActive(false);
        penaltyBall.gameObject.SetActive(false);
        goalkeeperImage.gameObject.SetActive(false);
        foreach (var btn in penaltyButtons)
        {
            btn.interactable = true;
            btn.GetComponent<Image>().color =
                Color.white;
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
        _clearMsgCoroutine =
            StartCoroutine(ClearAfter(duration));
    }

    private IEnumerator ClearAfter(float t)
    {
        yield return new WaitForSeconds(t);
        messageText.text = "";
        _clearMsgCoroutine = null;
    }
}
