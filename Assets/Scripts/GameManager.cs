// Assets/Scripts/GameManager.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
// 1) import the Feel namespace
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
    public Vector3 penaltyBallEndScale = Vector3.one * 0.5f;

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

    private Coroutine _clearMsgCoroutine;

    // columns allowed this turn
    private List<int> _allowedColumns = new List<int>();

    // clear pickups before starting a new match
    private void ClearFieldPowerUps()
    {
        foreach (var pu in FindObjectsOfType<PowerUpPickup>())
            Destroy(pu.gameObject);
    }

    void Start()
    {
        // Sanity checks omitted for brevity...

        if (rulesButton != null && rulesPanel != null)
        {
            rulesPanel.SetActive(false);
            rulesButton.onClick.AddListener(() => rulesPanel.SetActive(!rulesPanel.activeSelf));
        }

        goalkeeperBaseScale = goalkeeperImage.rectTransform.localScale;

        // initialize grid
        for (int r = 0; r < gridManager.rows; r++)
            for (int c = 0; c < gridManager.cols; c++)
                gridManager.cells[r, c].GetComponent<Cell>().Initialize(r, c, this);

        playerGold = startingGold;
        UpdateGoldUI();
        messageText.text = string.Empty;

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
        ClearFieldPowerUps();
        matchModifierManager.PickRandomModifiers();
        PopulateModifiersUI();
        rulesPanel?.SetActive(false);

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
            HighlightRow(ballRow - 1, Actor.AI);
            attackChoice = AIAttackGuess();
        }
    }

    public void OnCellClicked(int r, int c)
    {
        if (betPanel.activeSelf) return;
        int targetRow = (possession == Actor.Player) ? ballRow + 1 : ballRow - 1;
        if (r != targetRow) return;
        if (_allowedColumns.Count > 0 && !_allowedColumns.Contains(c)) return;

        if (possession == Actor.Player && phase == Phase.PlayerAttack)
        {
            attackChoice = c;
            defendChoice = AI_DefenseGuess();
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
        // Record last‐used column for both Player and AI
        matchModifierManager.SetLastUsedColumn(attacker == Actor.Player, attackChoice);

        bool tackle = (attackChoice == defendChoice);

        // Momentum Limit enforcement
        if (!tackle
            && matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit)
            && !matchModifierManager.CanAdvance())
        {
            tackle = true;
            ShowMessage("🔄 Momentum Limit! Must be tackled!", 1f);
            (attacker == Actor.Player ? feedbackTackleLose : feedbackTackleWin)?.PlayFeedbacks();
            matchModifierManager.OnTackle();
        }

        if (tackle)
        {
            ShowMessage(attacker == Actor.Player ? "Tackled!" : "Tackle!", 1f);
            (attacker == Actor.Player ? feedbackTackleLose : feedbackTackleWin)?.PlayFeedbacks();
        }
        else
        {
            matchModifierManager.OnAdvance();
            ShowMessage(attacker == Actor.Player ? "Dribble!" : "Dribbled!", 1f);
            (attacker == Actor.Player ? feedbackPlayerAdvance : feedbackOpponentAdvance)?.PlayFeedbacks();
            pot += bonusPerAdvance;
            UpdatePotUI();
        }

        yield return new WaitForSeconds(tackleAnimDuration);
        ClearHighlights();

        ballRow = targetRow;
        ballCol = attackChoice;
        yield return StartCoroutine(
            ballCtrl.MoveToCell(gridManager.GetCellPosition(ballRow, ballCol)));

        CheckForPickups();

        bool goal = !tackle &&
            ((attacker == Actor.Player && ballRow == gridManager.rows - 1) ||
             (attacker == Actor.AI && ballRow == 0));
        if (goal)
        {
            StartCoroutine(PenaltySequence(attacker));
            yield break;
        }

        if (tackle)
        {
            Vector3 cellPos = gridManager.GetCellPosition(ballRow, ballCol);
            var loserPrefab = (attacker == Actor.Player) ? aiLosePrefab : playerLosePrefab;
            GameObject loser = Instantiate(loserPrefab, cellPos, Quaternion.identity);
            StartCoroutine(ThrowOffScreen(loser, attacker));

            possession = attacker == Actor.Player ? Actor.AI : Actor.Player;
            matchModifierManager.OnTackle();
            SpawnCharacter();
            StartNewTurn();
        }
        else
        {
            StartNewTurn();
        }
    }

    private void CheckForPickups()
    {
        Vector2 pos2d = ballInstance.transform.position;
        var hits = Physics2D.OverlapCircleAll(pos2d, 0.1f);
        foreach (var hit in hits)
            if (hit.TryGetComponent<PowerUpPickup>(out var pu))
                pu.ManualPickup(ballInstance);
    }

    private IEnumerator ThrowOffScreen(GameObject loser, Actor attacker)
    {
        float elapsed = 0f, duration = tackleAnimDuration;
        Vector3 start = loser.transform.position;
        Vector3 dir = attacker == Actor.Player ? Vector3.down : Vector3.up;
        Vector3 end = start + dir * (gridManager.rows + 1);
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
        penaltyAttacker = attacker;
        penaltyChoiceMade = false;
        penaltyAttackChoice = penaltyDefendChoice = -1;
        penaltyPanel.SetActive(true);
        DisableGrid();

        penaltyBall.anchoredPosition = penaltyBallStartPos;
        penaltyBall.localScale = penaltyBallStartScale;
        penaltyBall.gameObject.SetActive(true);

        goalkeeperImage.sprite = attacker == Actor.Player ? aiGKIdleSprite : playerGKIdleSprite;
        goalkeeperImage.rectTransform.anchoredPosition = goalkeeperIdleAnchor.anchoredPosition;
        goalkeeperImage.rectTransform.localScale = goalkeeperBaseScale;
        goalkeeperImage.gameObject.SetActive(true);

        if (attacker == Actor.Player)
            penaltyDefendChoice = Random.Range(0, penaltyButtons.Length);
        else
            penaltyAttackChoice = Random.Range(0, penaltyButtons.Length);

        while (!penaltyChoiceMade) yield return null;

        Vector2 shoot = penaltyButtons[penaltyAttackChoice].GetComponent<RectTransform>().anchoredPosition;
        Vector2 def = penaltyButtons[penaltyDefendChoice].GetComponent<RectTransform>().anchoredPosition;
        Vector2 idle = goalkeeperIdleAnchor.anchoredPosition;
        var flip = goalkeeperBaseScale;
        flip.x = def.x < idle.x ? -Mathf.Abs(flip.x) : Mathf.Abs(flip.x);
        goalkeeperImage.rectTransform.localScale = flip;
        goalkeeperImage.sprite = attacker == Actor.Player ? aiGKJumpSprite : playerGKJumpSprite;

        float e2 = 0f;
        while (e2 < penaltyAnimDuration)
        {
            e2 += Time.deltaTime;
            float tB = Mathf.Clamp01(e2 / penaltyAnimDuration);
            float tK = Mathf.Clamp01(e2 / goalkeeperJumpDuration);

            penaltyBall.anchoredPosition = Vector2.Lerp(penaltyBallStartPos, shoot, tB);
            penaltyBall.localScale = Vector3.Lerp(penaltyBallStartScale, penaltyBallEndScale, tB);
            goalkeeperImage.rectTransform.anchoredPosition = Vector2.Lerp(idle, def, tK);
            yield return null;
        }

        yield return new WaitForSeconds(afterAnimDelay);

        penaltyButtons[penaltyAttackChoice].GetComponent<Image>().color = Color.green;
        penaltyButtons[penaltyDefendChoice].GetComponent<Image>().color = Color.red;

        bool saved = penaltyAttackChoice == penaltyDefendChoice;
        if (saved)
        {
            ShowMessage(attacker == Actor.Player ? "Countered" : "Saved", 1f);
            (attacker == Actor.Player ? feedbackPenaltyCounter : feedbackPenaltySaved)?.PlayFeedbacks();
            possession = attacker == Actor.Player ? Actor.AI : Actor.Player;

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
    }

    public void OnPenaltyButton(int idx)
    {
        if (penaltyAttacker == Actor.Player) penaltyAttackChoice = idx;
        else penaltyDefendChoice = idx;
        penaltyChoiceMade = true;
    }

    // Adjacent + Focus + BurnedColumn filtering
    private List<int> GetAdjacentColumns()
    {
        var adj = new List<int>();
        int cols = gridManager.cols;
        adj.Add(ballCol);
        if (ballCol - 1 >= 0) adj.Add(ballCol - 1);
        if (ballCol + 1 < cols) adj.Add(ballCol + 1);
        return adj;
    }

    private void HighlightRow(int tr, Actor attacker)
    {
        ClearHighlights();
        _allowedColumns.Clear();
        if (tr < 0 || tr >= gridManager.rows) return;

        // adjacency
        var adjacency = GetAdjacentColumns();
        // focus power-up
        var powered = powerUpManager.GetAllowedColumns(attacker.ToString());

        foreach (int c in adjacency)
            if (powered.Contains(c))
                _allowedColumns.Add(c);

        // Burned Column modifier
        if (matchModifierManager.HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn))
        {
            int burned = matchModifierManager.GetLastUsedColumn(attacker == Actor.Player);
            if (burned >= 0)
                _allowedColumns.Remove(burned);
        }

        // highlight
        foreach (int c in _allowedColumns)
            gridManager.cells[tr, c].GetComponent<Cell>().Highlight(true);
    }
    private void HighlightRow(int tr) => HighlightRow(tr, possession);

    private int AIAttackGuess()
    {
        if (_allowedColumns.Count > 0)
            return _allowedColumns[Random.Range(0, _allowedColumns.Count)];
        return Random.Range(0, gridManager.cols);
    }
    private int AI_DefenseGuess() => Random.Range(0, gridManager.cols);

    private void ClearHighlights()
    {
        foreach (var cellGO in gridManager.AllCells)
            cellGO.GetComponent<Cell>().Highlight(false);
    }

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

    private void PopulateModifiersUI()
    {
        // clear old icons
        foreach (Transform child in modifierIconsContainer)
            Destroy(child.gameObject);

        // instantiate new icons
        foreach (var mod in matchModifierManager.activeModifiers)
        {
            var go = Instantiate(modifierIconPrefab, modifierIconsContainer);
            var img = go.GetComponent<Image>();
            if (img != null)
                img.sprite = mod.icon;
            // if you have a tooltip script on the prefab, set its text here
        }

        // populate rules panel text
        if (rulesText != null)
        {
            var sb = new StringBuilder();
            foreach (var mod in matchModifierManager.activeModifiers)
                sb.AppendLine($"• <b>{mod.modifierName}</b>: {mod.description}");
            rulesText.text = sb.ToString();
        }
    }
}
