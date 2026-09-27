using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OblationGame
{
    [Header("Inspector UI - roots")]
    [SerializeField] Font interfaceFont;
    [SerializeField] CanvasScaler gameUiScaler;
    [SerializeField] CanvasScaler hudUiScaler;
    [SerializeField] GameObject titleScreen;
    [SerializeField] GameObject hudScreen;
    [SerializeField] GameObject planetPanel;
    [SerializeField] GameObject defensePanel;
    [SerializeField] GameObject doctrineModal;
    [SerializeField] GameObject assignmentModal;
    [SerializeField] GameObject pauseModal;
    [SerializeField] GameObject endingModal;
    [SerializeField] GameObject optionsModal;
    [SerializeField] GameObject operationsModal;
    [SerializeField] Image fadeOverlay;
    [SerializeField] CanvasGroup focusUi;
    [SerializeField] Text focusHeadingText;
    [SerializeField] Text focusInfoText;

    [Header("Inspector UI - HUD text")]
    [SerializeField] Text resourcesText;
    [SerializeField] Text ownershipText;
    [SerializeField] Text eventLogText;
    [SerializeField] Text statusText;
    [SerializeField] Text planetNameText;
    [SerializeField] RawImage planetInteriorImage;
    [SerializeField] Text planetMetaText;
    [SerializeField] Text planetStatsText;
    [SerializeField] Text planetTraitText;
    [SerializeField] Text planetProductionText;
    [SerializeField] Text planetHintText;
    [SerializeField] Text defenseText;
    [SerializeField] Text assignmentTitleText;
    [SerializeField] Text assignmentDescriptionText;
    [SerializeField] Text endingTitleText;
    [SerializeField] Text endingDescriptionText;
    [SerializeField] Text operationsSummaryText;
    [SerializeField] Text[] planetLabels;

    [Header("Inspector UI - buttons")]
    [SerializeField] Button startButton;
    [SerializeField] Button titleOptionsButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button doctrineOpenButton;
    [SerializeField] Button pauseOpenButton;
    [SerializeField] Button sourceButton;
    [SerializeField] Button zoomButton;
    [SerializeField] Button surpriseButton;
    [SerializeField] Button assaultButton;
    [SerializeField] Button orbitalButton;
    [SerializeField] Button defendButton;
    [SerializeField] Button viralButton;
    [SerializeField] Button foundryButton;
    [SerializeField] Button orbitalTechButton;
    [SerializeField] Button doctrineCloseButton;
    [SerializeField] Button extractorButton;
    [SerializeField] Button forgeButton;
    [SerializeField] Button psionicButton;
    [SerializeField] Button continueButton;
    [SerializeField] Button pauseOptionsButton;
    [SerializeField] Button pauseRestartButton;
    [SerializeField] Button pauseMenuButton;
    [SerializeField] Button endingRestartButton;
    [SerializeField] Button endingMenuButton;
    [SerializeField] Button optionsSaveButton;
    [SerializeField] Button operationsOpenButton;
    [SerializeField] Button operationsCloseButton;
    [SerializeField] Button antennaButton;
    [SerializeField] Button[] traitButtons;
    [SerializeField] Button[] unitButtons;
    [SerializeField] Button[] upgradeButtons;
    [SerializeField] Button[] exterminatusButtons;

    [Header("Inspector UI - options")]
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider uiScaleSlider;
    [SerializeField] Slider cameraSlider;

    bool uiBound;
    float focusUiProgress;
    bool operationsOpen;
    readonly int[] upgradeDisplayIndices = new int[6];

    void BindUiEvents()
    {
        if (gameUiScaler == null || hudUiScaler == null || titleScreen == null || hudScreen == null ||
            fadeOverlay == null || focusUi == null || focusHeadingText == null || focusInfoText == null ||
            operationsModal == null || operationsSummaryText == null || operationsOpenButton == null ||
            planetInteriorImage == null ||
            operationsCloseButton == null || antennaButton == null || traitButtons == null || traitButtons.Length != 3 ||
            unitButtons == null || unitButtons.Length != catalog.units.Length ||
            upgradeButtons == null || upgradeButtons.Length != upgradeDisplayIndices.Length ||
            exterminatusButtons == null || exterminatusButtons.Length != 3 ||
            planetLabels == null || planetLabels.Length != planets.Count || planetMapMarkers == null || planetMapMarkers.Length != planets.Count ||
            storyCanvas == null || storyNextButton == null || storySkipButton == null || mapOverviewButton == null)
        {
            Debug.LogError("OBLATION: Inspector UI 참조가 누락되었습니다.", this);
            enabled = false;
            return;
        }

        Font koreanFont = interfaceFont;
        if (koreanFont != null)
        {
            foreach (Text label in gameUiScaler.GetComponentsInChildren<Text>(true)) label.font = koreanFont;
            foreach (Text label in hudUiScaler.GetComponentsInChildren<Text>(true)) label.font = koreanFont;
        }

        startButton.onClick.AddListener(() => { PlayClick(); ResetCampaign(true); });
        titleOptionsButton.onClick.AddListener(() => { PlayClick(); optionsOpen = true; });
        quitButton.onClick.AddListener(QuitGame);
        doctrineOpenButton.onClick.AddListener(() => { PlayClick(); operationsOpen = false; factoryOpen = !factoryOpen; });
        pauseOpenButton.onClick.AddListener(() => { PlayClick(); operationsOpen = factoryOpen = false; paused = !paused; });
        sourceButton.onClick.AddListener(() => { PlayClick(); sourcePlanet = selectedPlanet; targetPlanet = -1; });
        zoomButton.onClick.AddListener(() => { PlayClick(); zoomed = !zoomed; });
        surpriseButton.onClick.AddListener(() => BeginPlayerAttack(AttackMode.Surprise));
        assaultButton.onClick.AddListener(() => BeginPlayerAttack(AttackMode.Assault));
        orbitalButton.onClick.AddListener(() => BeginPlayerAttack(AttackMode.Orbital));
        defendButton.onClick.AddListener(Defend);
        viralButton.onClick.AddListener(() => TryStartResearch(OblationResearchAxis.Hive));
        foundryButton.onClick.AddListener(() => TryStartResearch(OblationResearchAxis.Industry));
        orbitalTechButton.onClick.AddListener(() => TryStartResearch(OblationResearchAxis.Combat));
        doctrineCloseButton.onClick.AddListener(() => { PlayClick(); factoryOpen = false; });
        extractorButton.onClick.AddListener(() => AssignType(WorldType.Unit));
        forgeButton.onClick.AddListener(() => AssignType(WorldType.Manufacturing));
        psionicButton.onClick.AddListener(() => AssignType(WorldType.Energy));
        continueButton.onClick.AddListener(() => { PlayClick(); paused = false; });
        pauseOptionsButton.onClick.AddListener(() => { PlayClick(); optionsOpen = true; });
        pauseRestartButton.onClick.AddListener(() => { PlayClick(); ResetCampaign(true); });
        pauseMenuButton.onClick.AddListener(() => { PlayClick(); ResetCampaign(false); });
        endingRestartButton.onClick.AddListener(() => { PlayClick(); ResetCampaign(true); });
        endingMenuButton.onClick.AddListener(() => { PlayClick(); ResetCampaign(false); });
        optionsSaveButton.onClick.AddListener(SaveOptions);
        operationsOpenButton.onClick.AddListener(() => { PlayClick(); factoryOpen = false; operationsOpen = true; });
        operationsCloseButton.onClick.AddListener(() => { PlayClick(); operationsOpen = false; });
        antennaButton.onClick.AddListener(TryInstallAntenna);
        for (int i = 0; i < traitButtons.Length; i++)
        {
            int index = i;
            traitButtons[i].onClick.AddListener(() => TryUpgradeTrait((OblationTrait)index));
        }
        for (int i = 0; i < unitButtons.Length; i++)
        {
            int index = i;
            unitButtons[i].onClick.AddListener(() => TryQueueUnit(index));
        }
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            int index = i;
            upgradeButtons[i].onClick.AddListener(() => TryUpgradeUnit(upgradeDisplayIndices[index]));
        }
        for (int i = 0; i < exterminatusButtons.Length; i++)
        {
            int index = i;
            exterminatusButtons[i].onClick.AddListener(() => TryExterminatus((OblationExterminatus)index));
        }

        musicSlider.SetValueWithoutNotify(musicVolume);
        sfxSlider.SetValueWithoutNotify(sfxVolume);
        uiScaleSlider.SetValueWithoutNotify(uiScaleSetting);
        cameraSlider.SetValueWithoutNotify(cameraSensitivity);
        musicSlider.onValueChanged.AddListener(value => musicVolume = value);
        sfxSlider.onValueChanged.AddListener(value => sfxVolume = value);
        uiScaleSlider.onValueChanged.AddListener(value => { uiScaleSetting = value; ApplyUiScale(); });
        cameraSlider.onValueChanged.AddListener(value => cameraSensitivity = value);
        ApplyUiScale();
        BindPresentation();
        uiBound = true;
    }

    void BeginPlayerAttack(AttackMode mode)
    {
        if (sourcePlanet >= 0 && targetPlanet >= 0 && BeginAttack(Allegiance.Player, sourcePlanet, targetPlanet, mode)) PlayClick();
    }

    void SaveOptions()
    {
        PlayerPrefs.SetFloat("Oblation.Music", musicVolume);
        PlayerPrefs.SetFloat("Oblation.Sfx", sfxVolume);
        PlayerPrefs.SetFloat("Oblation.UiScale", uiScaleSetting);
        PlayerPrefs.SetFloat("Oblation.Sensitivity", cameraSensitivity);
        PlayerPrefs.Save();
        optionsOpen = false;
        PlayClick();
    }

    void ApplyUiScale()
    {
        Vector2 reference = new Vector2(1920f / uiScaleSetting, 1080f / uiScaleSetting);
        gameUiScaler.referenceResolution = reference;
        hudUiScaler.referenceResolution = reference;
    }

    void LateUpdate()
    {
        if (!uiBound) return;
        UpdateAllVisuals();
        UpdateActionEffects();
        RefreshUi();
        RefreshPlanetLabels();
        RefreshMapMarkers();
    }

    void RefreshUi()
    {
        bool isTitle = state == ScreenState.Title;
        bool isPlaying = state == ScreenState.Playing;
        titleScreen.SetActive(isTitle);
        hudScreen.SetActive(!isTitle && !storyActive);
        doctrineModal.SetActive(isPlaying && factoryOpen && !optionsOpen);
        operationsModal.SetActive(isPlaying && operationsOpen && !optionsOpen);
        assignmentModal.SetActive(isPlaying && pendingAssignment >= 0 && !optionsOpen && !DeferTutorialAssignment);
        pauseModal.SetActive(isPlaying && paused && pendingAssignment < 0 && !factoryOpen && !operationsOpen && !optionsOpen);
        endingModal.SetActive(state == ScreenState.Victory || state == ScreenState.Defeat);
        optionsModal.SetActive(optionsOpen);
        fadeOverlay.gameObject.SetActive(fade > .001f);
        if (fade > .001f) fadeOverlay.color = new Color(.005f, .008f, .018f, fade);
        float focusTarget = !isTitle && zoomed && selectedPlanet >= 0 ? 1f : 0f;
        focusUiProgress = Mathf.MoveTowards(focusUiProgress, focusTarget, Time.unscaledDeltaTime * 12f);
        focusUi.gameObject.SetActive(focusUiProgress > .001f);
        focusUi.alpha = focusTarget;
        focusUi.transform.localScale = Vector3.one * (ReducedMotion ? 1 : Mathf.Lerp(.96f, 1f, focusUiProgress));
        if (isTitle) { RefreshPresentation(); return; }

        resourcesText.text = $"생체 {biomass:0}  광물 {minerals:0}  신경 {neural:0}  공물 {offering:0}  병력 {combatUnits:0}" +
            (activeResearch != null ? $"  |  연구 {activeResearch.displayName} {researchRemaining:0}초 · 신경 {ResearchCost(activeResearch).neural:0}" : "");
        ownershipText.text = $"지배 {CountOwned(Allegiance.Player)}/{planets.Count}    위협 {aiStrength:0.0}";
        var logBuilder = new StringBuilder();
        for (int i = 0; i < eventLog.Count; i++) logBuilder.Append('›').Append(' ').Append(eventLog[i]).Append('\n');
        eventLogText.text = logBuilder.ToString();
        statusText.text = sourcePlanet >= 0 ? "출발지: " + planets[sourcePlanet].name + (targetPlanet >= 0 ? "  →  목표: " + planets[targetPlanet].name : "  |  연결된 행성을 선택하세요") : "아군 행성 하나를 선택하세요.";

        bool selected = selectedPlanet >= 0;
        planetPanel.SetActive(selected);
        if (selected)
        {
            Planet p = planets[selectedPlanet];
            planetNameText.text = p.name;
            planetInteriorImage.texture = p.definition.interiorPreview;
            planetNameText.color = OwnerColor(p.owner);
            planetMetaText.text = AllegianceText(p.owner) + "  //  " + WorldTypeText(p.type);
            planetStatsText.text = $"인구  {p.population:0.0} / {p.maxPopulation:0}억\n방어력 {p.defense:0}  |  점유 아군 {p.playerClaim:P0} · 적 {p.enemyClaim:P0}";
            string encounter = p.definition.encounter != null && p.definition.encounter.counterTags != null &&
                p.definition.encounter.counterTags.Length > 0 && p.scouted ?
                "  |  대응: " + CounterText(p.definition.encounter.counterTags[0]) : "  |  미정찰";
            planetTraitText.text = p.trait + encounter + (p.owner == Allegiance.Player &&
                p.definition.captureTechnology != null ? "\n점령 기술: " + p.definition.captureTechnology.displayName : "");
            planetProductionText.text = p.owner == Allegiance.Player || p.scouted ? ProductionText(p) : "정찰 후 생산 정보를 확인할 수 있습니다.";
            focusHeadingText.text = "행성 관측  //  " + p.name;
            focusInfoText.text = AllegianceText(p.owner) + "  ·  " + p.trait + "  ·  인구 " + p.population.ToString("0.0") + "억";
            bool allied = p.owner == Allegiance.Player;
            bool canAttack = !allied && !p.destroyed && targetPlanet == selectedPlanet && sourcePlanet >= 0 &&
                planets[sourcePlanet].links.Contains(selectedPlanet) && IsConnected(sourcePlanet, Allegiance.Player);
            sourceButton.gameObject.SetActive(allied);
            zoomButton.gameObject.SetActive(allied);
            surpriseButton.gameObject.SetActive(canAttack);
            assaultButton.gameObject.SetActive(canAttack);
            orbitalButton.gameObject.SetActive(canAttack);
            Text sourceLabel = sourceButton.GetComponentInChildren<Text>();
            if (sourceLabel != null) sourceLabel.text = sourcePlanet == selectedPlanet ? "출발지 지정됨" : "출발지 지정";
            Text zoomLabel = zoomButton.GetComponentInChildren<Text>();
            if (zoomLabel != null) zoomLabel.text = zoomed ? "은하 지도로" : "행성 집중";
            bool routeFree = !IsTargeted(selectedPlanet);
            surpriseButton.interactable = routeFree && techViral && combatUnits >= 3 && CanAfford(new OblationCost(25,0,18));
            assaultButton.interactable = routeFree && combatUnits >= 4 && CanAfford(new OblationCost(30,12,0));
            orbitalButton.interactable = routeFree && techOrbital && combatUnits >= 2 && CanAfford(new OblationCost(0,25,25));
            planetHintText.text = allied ? (p.antennaActive ? "안테나 연결됨" : p.antennaRemaining > 0 ? $"안테나 설치 {p.antennaRemaining:0}초" : "안테나 미설치") :
                p.destroyed ? "파괴된 행성은 정복·개발할 수 없습니다." : canAttack ?
                "역병 병력3·생체25·신경18 | 강습 병력4·생체30·광물12\n궤도 병력2·광물25·신경25" : "연결된 안테나와 보급로가 필요합니다.";
            if (allied && combatUnits < 4) planetHintText.text += "\n행성 운영에서 병력을 생산하십시오.";
            if (activeResearch != null)
                planetHintText.text += $"\n연구 {activeResearch.displayName} {researchRemaining:0}초 · 신경 {ResearchCost(activeResearch).neural:0}";
        }

        defensePanel.SetActive(isPlaying && defendPlanet >= 0);
        if (defendPlanet >= 0)
        {
            defenseText.text = "적 공격 접근 // " + planets[defendPlanet].name;
            defendButton.interactable = minerals >= 15;
        }
        UpdateResearchCard(viralButton, OblationResearchAxis.Hive);
        UpdateResearchCard(foundryButton, OblationResearchAxis.Industry);
        UpdateResearchCard(orbitalTechButton, OblationResearchAxis.Combat);
        UpdateOperationsUi();
        if (pendingAssignment >= 0)
        {
            Planet p = planets[pendingAssignment];
            assignmentTitleText.text = "봉헌할 행성: " + p.name;
            assignmentDescriptionText.text = p.trait + "\n영구 공물 역할을 선택하십시오.";
        }
        if (state == ScreenState.Victory || state == ScreenState.Defeat)
        {
            endingTitleText.text = state == ScreenState.Victory ? "봉헌이 완성되었습니다" : "제단이 무너졌습니다";
            endingDescriptionText.text = state == ScreenState.Victory ? "적이 무릎 꿇었습니다. 모든 궤도가 하나의 이름을 외칩니다." : "핵심 행성을 잃었습니다. 굶주린 신이 등을 돌립니다.";
        }
        RefreshPresentation();
    }

    void UpdateOperationsUi()
    {
        bool selected = selectedPlanet >= 0;
        operationsOpenButton.gameObject.SetActive(selected);
        if (!selected) return;
        Planet p = planets[selectedPlanet];
        operationsSummaryText.text = p.name + "  ·  " + AllegianceText(p.owner) + "  ·  " + WorldTypeText(p.type) +
            $"\n안테나 {(p.antennaActive ? "연결됨" : p.antennaRemaining > 0 ? "설치 중" : "없음")}  ·  자원 초점 {ResourceText(p.definition.focusResource)}";
        antennaButton.interactable = CanInstallAntenna();
        for (int i = 0; i < traitButtons.Length; i++)
        {
            Button button = traitButtons[i];
            button.interactable = p.owner == Allegiance.Player && !p.destroyed && p.traitLevels[i] < 10;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = catalog.traits[i].displayName +
                (i == 0 ? "(" + ResourceText(p.definition.focusResource) + ")" : "") +
                "  " + p.traitLevels[i] + "/10";
        }
        for (int i = 0; i < unitButtons.Length; i++)
        {
            UnitDefinitionSO unit = catalog.units[i];
            Button button = unitButtons[i];
            button.interactable = CanProduceUnit(unit, p, selectedPlanet);
            Text label = button.GetComponentInChildren<Text>();
            string reason = !string.IsNullOrEmpty(unit.requiredResearchId) && !HasResearch(unit.requiredResearchId) ? FindResearch(unit.requiredResearchId)?.displayName + " 필요" :
                unit.largePlanetOnly && !p.definition.largeSpecialPlanet ? "대형 행성 전용" : p.type != WorldType.Unit ? "유닛 특화 행성 필요" :
                !string.IsNullOrEmpty(p.queuedUnitId) ? $"생산 중 {p.unitRemaining:0}초" : !IsConnected(selectedPlanet, Allegiance.Player) ? "안테나 연결 필요" :
                $"생체 {unit.productionCost.biomass:0} · 광물 {unit.productionCost.minerals:0} · {unit.productionSeconds:0}초";
            if (label != null) label.text = unit.displayName + $"  T{unit.tier}\n" + reason;
        }
        int visibleUpgradeCount = 0;
        for (int i = 0; i < catalog.unitUpgrades.Length && visibleUpgradeCount < upgradeButtons.Length; i++)
        {
            UnitUpgradeSO upgrade = catalog.unitUpgrades[i];
            if (upgrade == null || completedUpgrades.Contains(upgrade.id) ||
                !unitInventory.TryGetValue(upgrade.unitId, out int count) || count < 1) continue;
            upgradeDisplayIndices[visibleUpgradeCount++] = i;
        }
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            Button button = upgradeButtons[i];
            bool available = i < visibleUpgradeCount;
            int upgradeIndex = available ? upgradeDisplayIndices[i] : -1;
            upgradeDisplayIndices[i] = upgradeIndex;
            button.interactable = available && CanUpgradeUnit(upgradeIndex);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = available ? catalog.unitUpgrades[upgradeIndex].displayName : "유닛 생산 후 강화";
        }
        for (int i = 0; i < exterminatusButtons.Length; i++)
        {
            Button button = exterminatusButtons[i];
            button.interactable = CanExterminatus((OblationExterminatus)i);
            Text label = button.GetComponentInChildren<Text>();
            ExterminatusDefinitionSO definition = FindExterminatus((OblationExterminatus)i);
            if (label != null && definition != null) label.text = definition.displayName + $"\n공물 {definition.activationCost.offering:0}";
        }
    }

    void UpdateResearchCard(Button button, OblationResearchAxis axis)
    {
        ResearchNodeSO node = NextResearch(axis);
        button.interactable = CanStartResearch(node);
        Transform card = button.transform.parent;
        Text title = card.Find("Title")?.GetComponent<Text>();
        Text description = card.Find("Description")?.GetComponent<Text>();
        Text cost = card.Find("Cost")?.GetComponent<Text>();
        if (title != null) title.text = node != null ? node.displayName : "연구 완료";
        if (description != null)
        {
            string conditions = node == null ? "" : $"행성 {node.requiredPlanets}개" +
                (node.requiredAntennaCount > 0 ? $" · 안테나 {node.requiredAntennaCount}개" : "") +
                (!string.IsNullOrEmpty(node.requiredUnitId) ? " · 유닛 " + FindUnit(node.requiredUnitId)?.displayName : "") +
                (node.prerequisites != null && node.prerequisites.Length > 0 ? " · 선행 " + FindResearch(node.prerequisites[0])?.displayName : "");
            description.text = node != null ? node.description + "\n조건: " + conditions : "이 연구 축의 모든 항목을 완료했습니다.";
        }
        if (cost != null)
        {
            string unlock = node == null ? "" : node.unlockExterminatusIds != null && node.unlockExterminatusIds.Length > 0 ?
                "절멸 해금" : node.unlockUnitIds != null && node.unlockUnitIds.Length > 0 ?
                FindUnit(node.unlockUnitIds[0])?.displayName + " 해금" : "수치 강화";
            cost.text = node != null ?
                $"신경 {ResearchCost(node).neural:0} · {node.durationSeconds:0}초 · {node.tier}티어\n{unlock}  |  대응 {CounterText(node.counterTag)}" : "";
        }
    }

    void RefreshPlanetLabels()
    {
        RectTransform hudRect = hudUiScaler.GetComponent<RectTransform>();
        for (int i = 0; i < planets.Count; i++)
        {
            Text label = planetLabels[i];
            Vector3 screen = gameCamera.WorldToScreenPoint(planets[i].position + Vector3.up * (planets[i].scale + .25f));
            bool relevant = i == selectedPlanet || i == hoveredPlanet || selectedPlanet >= 0 && planets[selectedPlanet].links.Contains(i);
            bool visible = state != ScreenState.Title && !tutorialActive && relevant && screen.z > 0 && (!zoomed || i == selectedPlanet);
            label.gameObject.SetActive(visible);
            if (!visible) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, screen, null, out Vector2 local);
            label.rectTransform.anchoredPosition = local;
            label.color = OwnerColor(planets[i].owner);
            label.text = planets[i].name + " · " + AllegianceText(planets[i].owner);
        }
    }
}
