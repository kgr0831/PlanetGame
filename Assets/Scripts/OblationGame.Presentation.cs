using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed partial class OblationGame
{
    [Header("Inspector - readable HUD and onboarding")]
    [SerializeField] Text[] resourceValues;
    [SerializeField] Text[] resourceRates;
    [SerializeField] Image[] resourceCards;
    [SerializeField] Canvas resourceCanvas;
    [SerializeField] Text researchStatus;
    [SerializeField] Image researchProgress;
    [SerializeField] Text productionStatus;
    [SerializeField] Image productionProgress;
    [SerializeField] Image battleProgress;
    [SerializeField] Button quickProduceButton;
    [SerializeField] Button speedButton;
    [SerializeField] Button helpButton;
    [SerializeField] GameObject tutorialRoot;
    [SerializeField] Text tutorialTitle;
    [SerializeField] Text tutorialBody;
    [SerializeField] Button tutorialAction;
    [SerializeField] Button tutorialSkip;
    [SerializeField] Text feedbackText;
    [SerializeField] Button reducedMotionButton;
    [SerializeField] Button reducedEffectsButton;
    [SerializeField] Button contrastButton;
    [SerializeField] Volume gameplayVolume;
    [SerializeField] Material impactMaterial;
    [SerializeField] OblationVisualDirector visualDirector;

    public static bool ReducedMotion { get; private set; }
    bool reducedEffects;
    bool highContrast;
    bool tutorialActive;
    int tutorialStep;
    int tutorialTarget = 2;
    float gameSpeed = 1;
    float feedbackUntil;
    readonly float[] previousResourceValues = new float[5];
    readonly float[] resourceFlash = new float[5];
    GameObject focusedModal;

    void BindPresentation()
    {
        if (resourceValues == null || resourceValues.Length != 5 || resourceRates == null || resourceRates.Length != 5 ||
            tutorialRoot == null || quickProduceButton == null || gameplayVolume == null || impactMaterial == null)
        {
            Debug.LogError("OBLATION: 새 HUD와 튜토리얼의 Inspector 참조를 연결해야 합니다.", this);
            enabled = false;
            return;
        }
        ReducedMotion = PlayerPrefs.GetInt("Oblation.ReducedMotion", 0) == 1;
        reducedEffects = PlayerPrefs.GetInt("Oblation.ReducedEffects", 0) == 1;
        highContrast = PlayerPrefs.GetInt("Oblation.HighContrast", 0) == 1;
        quickProduceButton.onClick.AddListener(QuickProduce);
        speedButton.onClick.AddListener(() => { gameSpeed = gameSpeed >= 3 ? 1 : gameSpeed + 1; PlayClick(); });
        helpButton.onClick.AddListener(() => { if (tutorialActive) FinishTutorial(); else BeginTutorial(); PlayClick(); });
        tutorialSkip.onClick.AddListener(FinishTutorial);
        reducedMotionButton.onClick.AddListener(() => { ReducedMotion = !ReducedMotion; ApplyGraphicsPreferences(); });
        reducedEffectsButton.onClick.AddListener(() => { reducedEffects = !reducedEffects; ApplyGraphicsPreferences(); });
        contrastButton.onClick.AddListener(() => { highContrast = !highContrast; ApplyGraphicsPreferences(); });
        BindFocusedExperience();
        mapOverviewButton.onClick.AddListener(ShowGalaxyOverview);
        BindStory();
        ApplyGraphicsPreferences();
    }

    void ApplyGraphicsPreferences()
    {
        Shader.SetGlobalFloat("_OblationMotion", ReducedMotion ? 0 : 1);
        Shader.SetGlobalFloat("_OblationEffects", reducedEffects ? .3f : 1);
        if(visualDirector!=null)visualDirector.Configure(reducedEffects,highContrast);
        if (gameplayVolume != null && gameplayVolume.profile.TryGet(out Bloom bloom))
            bloom.intensity.Override(reducedEffects ? .2f : 1.05f);
        if (gameplayVolume != null && gameplayVolume.profile.TryGet(out Vignette vignette))
            vignette.intensity.Override(highContrast ? .05f : .16f);
        SetButtonText(reducedMotionButton, "움직임 줄이기  " + (ReducedMotion ? "켜짐" : "꺼짐"));
        SetButtonText(reducedEffectsButton, "강한 효과 줄이기  " + (reducedEffects ? "켜짐" : "꺼짐"));
        SetButtonText(contrastButton, "높은 대비  " + (highContrast ? "켜짐" : "꺼짐"));
        PlayerPrefs.SetInt("Oblation.ReducedMotion", ReducedMotion ? 1 : 0);
        PlayerPrefs.SetInt("Oblation.ReducedEffects", reducedEffects ? 1 : 0);
        PlayerPrefs.SetInt("Oblation.HighContrast", highContrast ? 1 : 0);
    }

    void ResetPresentation(bool begin)
    {
        gameSpeed = 1;
        tutorialActive = begin;
        tutorialStep = 0;
        tutorialTarget = 2;
        focusedModal = null;
        feedbackUntil = 0;
        ResetCinematic();
    }

    void SelectPlanet(int index)
    {
        if (index < 0 || index >= planets.Count) return;
        if (selectedPlanet != index) { detailsExpanded = false; operationTab = 0; }
        selectedPlanet = index;
        FocusSelectedPlanet();
        if (CanScout(index)) planets[index].scouted = true;
        if (planets[index].owner == Allegiance.Player) { sourcePlanet = index; targetPlanet = -1; }
        else if (sourcePlanet >= 0 && planets[sourcePlanet].links.Contains(index)) targetPlanet = index;
    }

    int AvailableFactory()
    {
        for (int i = 0; i < planets.Count; i++)
            if (CanProduceUnit(catalog.units[0], planets[i], i)) return i;
        return -1;
    }

    void QuickProduce()
    {
        int factory = AvailableFactory();
        if (factory < 0) { AddLog("생산 중이거나 생체 물질이 부족합니다. 연결된 유닛 특화 행성을 확인하세요."); return; }
        int previous = selectedPlanet;
        selectedPlanet = factory;
        TryQueueUnit(0);
        selectedPlanet = previous;
        PlayClick();
    }

    void RefreshPresentation()
    {
        resourceCanvas.overrideSorting = true;
        resourceCanvas.sortingOrder = 20;
        bool playing = state == ScreenState.Playing;
        RefreshCinematic();
        float[] amounts = { biomass, minerals, neural, offering, combatUnits };
        OblationCost totalRate = new OblationCost();
        foreach (Planet planet in planets)
        {
            int index = planets.IndexOf(planet);
            if (planet.owner != Allegiance.Player || !IsConnected(index, Allegiance.Player) || IsTargeted(index)) continue;
            OblationCost rate = ProductionPerMinute(planet);
            totalRate.biomass += rate.biomass; totalRate.minerals += rate.minerals; totalRate.neural += rate.neural;
        }
        float[] rates = { totalRate.biomass, totalRate.minerals, totalRate.neural };
        for (int i = 0; i < 5; i++)
        {
            if (Mathf.Abs(amounts[i] - previousResourceValues[i]) >= 1) resourceFlash[i] = .4f;
            previousResourceValues[i] = amounts[i];
            resourceFlash[i] = Mathf.Max(0, resourceFlash[i] - Time.unscaledDeltaTime);
            resourceValues[i].text = amounts[i].ToString("0") + (i == 4 ? "기" : "");
            resourceRates[i].text = i < 3 ? $"+{rates[i]:0}/분" : i == 3 ? "정복 보상" : "강습 4기";
            resourceCards[i].color = Color.Lerp(highContrast ? new Color(.015f,.025f,.045f,1) : new Color(.025f,.05f,.075f,.45f),
                new Color(.09f,.22f,.28f,1), reducedEffects ? 0 : resourceFlash[i]);
        }
        researchStatus.text = activeResearch == null ? "연구 대기  ·  상단 ‘연구’에서 기술 선택" :
            $"연구  {activeResearch.displayName}   {researchRemaining / ResearchSpeed():0}초 남음";
        SetProgress(researchProgress, activeResearch == null ? 0 : 1 - researchRemaining / activeResearch.durationSeconds);
        Attack selectedBattle = attacks.Find(a => a.to == selectedPlanet);
        SetProgress(battleProgress, selectedBattle == null ? 0 : selectedBattle.elapsed / selectedBattle.duration);
        Planet producing = planets.Find(p => p.owner == Allegiance.Player && !string.IsNullOrEmpty(p.queuedUnitId));
        productionStatus.text = producing == null ? "빠른 병력 보충  ·  정찰체 8초" :
            $"{producing.name}  {FindUnit(producing.queuedUnitId).displayName} 생산  {producing.unitRemaining:0}초";
        SetProgress(productionProgress, producing == null ? 0 : 1 - producing.unitRemaining / Mathf.Max(1, producing.unitTotalTime));
        quickProduceButton.interactable = AvailableFactory() >= 0;
        SetButtonText(quickProduceButton, AvailableFactory() >= 0 ? "정찰체 생산 · 생체 40" : producing != null ? "생산 중" : "생체 40 / 생산 행성 필요");
        SetButtonText(speedButton, $"×{gameSpeed:0}");
        speedButton.interactable = playing && !optionsOpen;
        helpButton.interactable = playing && !optionsOpen;
        SetButtonText(helpButton,"튜토리얼");
        doctrineOpenButton.interactable = playing && !optionsOpen && !paused && pendingAssignment < 0;
        pauseOpenButton.interactable = playing && !optionsOpen && pendingAssignment < 0;
        SetButtonText(pauseOpenButton, paused ? "계속" : "메뉴");
        feedbackText.gameObject.SetActive(playing && !tutorialActive && Time.unscaledTime < feedbackUntil && !optionsOpen && !operationsOpen && !factoryOpen);
        ApplyFocusedHud(producing != null);
        FitAndFocusModal();
        UpdateCinematicReticle();
    }

    void FitAndFocusModal()
    {
        GameObject current = null;
        foreach (GameObject modal in new[] { doctrineModal, operationsModal, assignmentModal, pauseModal, endingModal, optionsModal })
        {
            RectTransform card = modal.transform.Find("Card") as RectTransform;
            RectTransform canvas = modal.transform.parent as RectTransform;
            if (card != null && canvas != null)
            {
                card.anchoredPosition = new Vector2(0,-65);
                card.localScale = Vector3.one * Mathf.Min(1f, (canvas.rect.width - 40) / card.sizeDelta.x, (canvas.rect.height - 200) / card.sizeDelta.y);
            }
            if (modal.activeInHierarchy) current = modal;
        }
        if (current != focusedModal && EventSystem.current != null)
        {
            focusedModal = current;
            Selectable first = current != null ? current.GetComponentInChildren<Selectable>() : helpButton;
            if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    void HandleAccessibilityShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null || optionsOpen || pendingAssignment >= 0) return;
        if (Keyboard.current.pKey.wasPressedThisFrame) { operationsOpen = factoryOpen = false; paused = !paused; }
        if (Keyboard.current.hKey.wasPressedThisFrame) { if (tutorialActive) FinishTutorial(); else BeginTutorial(); }
        if (paused || factoryOpen || operationsOpen) return;
        if (tutorialActive)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame && Beat == TutorialBeat.Produce) QuickProduce();
            if (Keyboard.current.digit2Key.wasPressedThisFrame && Beat == TutorialBeat.Attack) BeginPlayerAttack(AttackMode.Assault);
            if (Keyboard.current.tabKey.wasPressedThisFrame && (Beat == TutorialBeat.SelectHome || Beat == TutorialBeat.SelectTarget)) ClickCinematicTarget();
            return;
        }
        if (Keyboard.current.digit1Key.wasPressedThisFrame) QuickProduce();
        if (Keyboard.current.digit2Key.wasPressedThisFrame) BeginPlayerAttack(AttackMode.Assault);
        if (Keyboard.current.tabKey.wasPressedThisFrame) { zoomed = false; SelectPlanet((selectedPlanet + 1) % planets.Count); }
#endif
    }

    static void SetButtonText(Button button, string value)
    {
        if (button == null) return;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null) label.text = value;
    }

    static void SetProgress(Image image, float progress)
    {
        if (image == null) return;
        image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
    }
}
