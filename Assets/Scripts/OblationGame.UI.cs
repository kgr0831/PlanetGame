using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OblationGame
{
    [Header("Inspector UI - roots")]
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
    [SerializeField] Image fadeOverlay;

    [Header("Inspector UI - HUD text")]
    [SerializeField] Text resourcesText;
    [SerializeField] Text ownershipText;
    [SerializeField] Text eventLogText;
    [SerializeField] Text statusText;
    [SerializeField] Text planetNameText;
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

    [Header("Inspector UI - options")]
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider uiScaleSlider;
    [SerializeField] Slider cameraSlider;

    bool uiBound;

    void BindUiEvents()
    {
        if (gameUiScaler == null || hudUiScaler == null || titleScreen == null || hudScreen == null ||
            fadeOverlay == null || planetLabels == null || planetLabels.Length != planets.Count)
        {
            Debug.LogError("OBLATION: Inspector UI 참조가 누락되었습니다.", this);
            enabled = false;
            return;
        }

        Font koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial" }, 18);
        if (koreanFont != null)
        {
            foreach (Text label in gameUiScaler.GetComponentsInChildren<Text>(true)) label.font = koreanFont;
            foreach (Text label in hudUiScaler.GetComponentsInChildren<Text>(true)) label.font = koreanFont;
        }

        startButton.onClick.AddListener(() => { PlayClick(); ResetCampaign(true); });
        titleOptionsButton.onClick.AddListener(() => { PlayClick(); optionsOpen = true; });
        quitButton.onClick.AddListener(QuitGame);
        doctrineOpenButton.onClick.AddListener(() => { PlayClick(); factoryOpen = true; });
        pauseOpenButton.onClick.AddListener(() => { PlayClick(); paused = true; });
        sourceButton.onClick.AddListener(() => { PlayClick(); sourcePlanet = selectedPlanet; targetPlanet = -1; });
        zoomButton.onClick.AddListener(() => { PlayClick(); zoomed = !zoomed; });
        surpriseButton.onClick.AddListener(() => BeginPlayerAttack(AttackMode.Surprise));
        assaultButton.onClick.AddListener(() => BeginPlayerAttack(AttackMode.Assault));
        orbitalButton.onClick.AddListener(() => BeginPlayerAttack(AttackMode.Orbital));
        defendButton.onClick.AddListener(Defend);
        viralButton.onClick.AddListener(() => { if (techViral || psi < 45) return; psi -= 45; techViral = true; AddLog("역병 성전례 연구 완료."); PlayClick(); });
        foundryButton.onClick.AddListener(() => { if (techFoundry || ore < 55 || industry < 45) return; ore -= 55; industry -= 45; techFoundry = true; AddLog("전쟁 주조소 연구 완료."); PlayClick(); });
        orbitalTechButton.onClick.AddListener(() => { if (techOrbital || psi < 55 || industry < 70) return; psi -= 55; industry -= 70; techOrbital = true; AddLog("궤도 칙령 승인 완료."); PlayClick(); });
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

        musicSlider.SetValueWithoutNotify(musicVolume);
        sfxSlider.SetValueWithoutNotify(sfxVolume);
        uiScaleSlider.SetValueWithoutNotify(uiScaleSetting);
        cameraSlider.SetValueWithoutNotify(cameraSensitivity);
        musicSlider.onValueChanged.AddListener(value => musicVolume = value);
        sfxSlider.onValueChanged.AddListener(value => sfxVolume = value);
        uiScaleSlider.onValueChanged.AddListener(value => { uiScaleSetting = value; ApplyUiScale(); });
        cameraSlider.onValueChanged.AddListener(value => cameraSensitivity = value);
        ApplyUiScale();
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
        RefreshUi();
        RefreshPlanetLabels();
    }

    void RefreshUi()
    {
        bool isTitle = state == ScreenState.Title;
        bool isPlaying = state == ScreenState.Playing;
        titleScreen.SetActive(isTitle);
        hudScreen.SetActive(!isTitle);
        doctrineModal.SetActive(isPlaying && factoryOpen && !optionsOpen);
        assignmentModal.SetActive(isPlaying && pendingAssignment >= 0 && !optionsOpen);
        pauseModal.SetActive(isPlaying && paused && pendingAssignment < 0 && !factoryOpen && !optionsOpen);
        endingModal.SetActive(state == ScreenState.Victory || state == ScreenState.Defeat);
        optionsModal.SetActive(optionsOpen);
        fadeOverlay.gameObject.SetActive(fade > .001f);
        if (fade > .001f) fadeOverlay.color = new Color(.005f, .008f, .018f, fade);
        if (isTitle) return;

        resourcesText.text = $"광물 {ore:0}  산업력 {industry:0}  정신력 {psi:0}  에너지 {fuel:0}  전투 {combatUnits:0}  노동 {laborUnits:0}";
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
            planetNameText.color = OwnerColor(p.owner);
            planetMetaText.text = AllegianceText(p.owner) + "  //  " + WorldTypeText(p.type);
            planetStatsText.text = $"인구  {p.population:0.0} / {p.maxPopulation:0}억\n방어력 {p.defense:0}  |  점유 아군 {p.playerClaim:P0} · 적 {p.enemyClaim:P0}";
            planetTraitText.text = p.trait;
            planetProductionText.text = p.owner == Allegiance.Player ? ProductionText(p) : "생산 정보를 확인할 수 없습니다.";
            bool allied = p.owner == Allegiance.Player;
            bool canAttack = !allied && targetPlanet == selectedPlanet && sourcePlanet >= 0 && planets[sourcePlanet].links.Contains(selectedPlanet);
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
            surpriseButton.interactable = routeFree;
            assaultButton.interactable = routeFree;
            orbitalButton.interactable = routeFree && techOrbital;
            planetHintText.text = allied ? "" : canAttack ? "기습 전투6·산업8·정신18·에너지12 | 강습 전투8·광물10·산업16·에너지12\n궤도 전투4·광물20·산업55·정신35·에너지22" : "지정한 출발지와 직접 연결된 보급로가 없습니다.";
        }

        defensePanel.SetActive(isPlaying && defendPlanet >= 0);
        if (defendPlanet >= 0)
        {
            defenseText.text = "적 공격 접근 // " + planets[defendPlanet].name;
            defendButton.interactable = industry >= 15;
        }
        viralButton.interactable = !techViral && psi >= 45;
        foundryButton.interactable = !techFoundry && ore >= 55 && industry >= 45;
        orbitalTechButton.interactable = !techOrbital && psi >= 55 && industry >= 70;
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
    }

    void RefreshPlanetLabels()
    {
        RectTransform hudRect = hudUiScaler.GetComponent<RectTransform>();
        for (int i = 0; i < planets.Count; i++)
        {
            Text label = planetLabels[i];
            Vector3 screen = gameCamera.WorldToScreenPoint(planets[i].position + Vector3.up * (planets[i].scale + .25f));
            bool visible = state != ScreenState.Title && screen.z > 0 && (!zoomed || i == selectedPlanet);
            label.gameObject.SetActive(visible);
            if (!visible) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, screen, null, out Vector2 local);
            label.rectTransform.anchoredPosition = local;
            label.color = OwnerColor(planets[i].owner);
            label.text = planets[i].name;
        }
    }
}
