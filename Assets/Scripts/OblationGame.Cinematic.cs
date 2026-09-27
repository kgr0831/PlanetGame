using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed partial class OblationGame
{
    enum TutorialBeat { Intro, SelectHome, Produce, Launch, SelectTarget, Attack, Battle, Captured, Specialize, Network, NetworkBuild, Research, Complete, CameraZoom, CameraPan, Resources }
    TutorialBeat Beat => (TutorialBeat)tutorialStep;
    [Header("Inspector - focused interface and cinematic guide")]
    [SerializeField] Button resourceDetailsButton;
    [SerializeField] Button historyButton;
    [SerializeField] Button planetDetailsButton;
    [SerializeField] Button[] operationTabs;
    [SerializeField] Text[] operationHeadings;
    [SerializeField] RectTransform[] researchChoiceCards;
    [SerializeField] Canvas cinematicCanvas;
    [SerializeField] CanvasGroup cinematicGroup;
    [SerializeField] CanvasGroup cinematicCaption;
    [SerializeField] RectTransform cinematicTopBar;
    [SerializeField] RectTransform cinematicBottomBar;
    [SerializeField] RectTransform cinematicReticle;
    [SerializeField] CanvasGroup reticleGroup;
    [SerializeField] RectTransform cinematicScanLine;
    [SerializeField] Button cinematicTargetButton;
    [SerializeField] Text cinematicTargetLabel;
    [SerializeField] Text cinematicChapter;
    [SerializeField] Image cinematicProgress;
    bool resourceDetailsOpen;
    bool historyOpen;
    bool detailsExpanded;
    int operationTab;
    int hoveredPlanet = -1;
    int previousBeat = -1;
    float beatTime;
    float cinematicBlend;
    float cinematicZoom = 1;
    Vector3 cinematicLook;
    float resourceLessonReadTime;
    Button lastCinematicControl;

    bool CinematicSuspended => storyActive || optionsOpen || paused && pendingAssignment < 0 || state != ScreenState.Playing;
    bool DeferTutorialAssignment => tutorialActive && (Beat == TutorialBeat.Battle || Beat == TutorialBeat.Captured);
    bool TutorialHoldsSimulation => tutorialActive &&
        (NavigationLesson || Beat == TutorialBeat.Resources || Beat == TutorialBeat.Intro || Beat == TutorialBeat.SelectHome || Beat == TutorialBeat.SelectTarget ||
        Beat == TutorialBeat.Produce && AvailableFactory() >= 0 ||
        Beat == TutorialBeat.Attack && combatUnits >= 4 && CanAfford(new OblationCost(30,12,0)) ||
        Beat == TutorialBeat.Network && CanAfford(catalog.antenna.installationCost) ||
        Beat == TutorialBeat.Research && CanAfford(new OblationCost(0,0,60)));

    void BindFocusedExperience()
    {
        resourceDetailsButton.onClick.AddListener(() => { resourceDetailsOpen = !resourceDetailsOpen; PlayClick(); });
        historyButton.onClick.AddListener(() => { historyOpen = !historyOpen; PlayClick(); });
        planetDetailsButton.onClick.AddListener(() => { detailsExpanded = !detailsExpanded; PlayClick(); });
        cinematicTargetButton.onClick.AddListener(ClickCinematicTarget);
        for (int i = 0; i < operationTabs.Length; i++)
        {
            int tab = i;
            operationTabs[i].onClick.AddListener(() => { operationTab = tab; PlayClick(); });
        }
    }

    void ResetCinematic()
    {
        resourceDetailsOpen = historyOpen = detailsExpanded = false;
        operationTab = 0; hoveredPlanet = -1; previousBeat = -1; beatTime = 0; cinematicBlend = 0; cinematicZoom = 1; tutorialZoomTravel = tutorialPanTravel = 0;
        lastCinematicControl = null;
    }

    void BeginTutorial()
    {
        if (state != ScreenState.Playing) return;
        ResetCinematic(); tutorialActive = true;
        paused = factoryOpen = operationsOpen = false; gameSpeed = 1; zoomed = false;
        tutorialTarget = 2;
        for (int i = 1; i < planets.Count; i++)
            if (planets[i].owner == Allegiance.Player && !planets[i].destroyed) { tutorialTarget = i; break; }
        EnterBeat(CountOwned(Allegiance.Player) > 1 ?
            planets[tutorialTarget].antennaActive ? TutorialBeat.Research : TutorialBeat.Network : TutorialBeat.Intro);
    }

    void FinishTutorial()
    {
        tutorialActive = false;
        feedbackUntil = 0;
        cameraFocus = Vector3.zero; cameraFocusVelocity = Vector3.zero; zoomed = false; mapDistance = OblationGalaxyLayout.StartingDistance;
        if (pendingAssignment < 0) selectedPlanet = -1;
        PlayerPrefs.SetInt("Oblation.CinematicTutorialComplete", 1);
        PlayerPrefs.Save();
        ApplyGraphicsPreferences();
        lastCinematicControl = null;
    }

    void EnterBeat(TutorialBeat next)
    {
        tutorialStep = (int)next; previousBeat = tutorialStep; beatTime = 0; lastCinematicControl = null; cinematicZoom = 1;
        if (next == TutorialBeat.Intro) { selectedPlanet = -1; sourcePlanet = 0; zoomed = false; }
        if (next == TutorialBeat.CameraZoom) { mapDistance = OblationGalaxyLayout.StartingDistance; cameraFocus = Vector3.zero; tutorialZoomTravel = 0; }
        if (next == TutorialBeat.CameraPan) tutorialPanTravel = 0;
        if (next == TutorialBeat.Resources) { resourceDetailsOpen = false; resourceLessonReadTime = 0; }
        if (next == TutorialBeat.SelectHome) resourceDetailsOpen = false;
        if (next == TutorialBeat.Produce) SelectPlanet(0);
        if (next == TutorialBeat.Launch)
            OblationCombatBurst.Spawn(effectsRoot, planets[0].position, PlayerColor, impactMaterial, reducedEffects ? 6 : 22, reducedEffects);
        if (next == TutorialBeat.SelectTarget) { sourcePlanet = 0; targetPlanet = -1; }
        if (next == TutorialBeat.Captured) { factoryOpen = operationsOpen = false; }
        if (next == TutorialBeat.Network) SelectPlanet(tutorialTarget);
        if (next == TutorialBeat.Complete) { factoryOpen = operationsOpen = false; selectedPlanet = -1; }
        if (sfxSource != null && clickClip != null && next != TutorialBeat.Battle) sfxSource.PlayOneShot(clickClip, sfxVolume * .4f);
    }

    void ClickCinematicTarget()
    {
        if (!tutorialActive || CinematicSuspended) return;
        if (Beat == TutorialBeat.SelectHome) SelectPlanet(0);
        else if (Beat == TutorialBeat.SelectTarget) SelectPlanet(tutorialTarget);
        else if (Beat == TutorialBeat.Network) { SelectPlanet(tutorialTarget); TryInstallAntenna(); }
        PlayClick();
    }

    bool TutorialAllowsWorldTarget(int index) => !tutorialActive ||
        Beat == TutorialBeat.SelectHome && index == 0 || Beat == TutorialBeat.SelectTarget && index == tutorialTarget;

    void RefreshCinematic()
    {
        bool visible = tutorialActive && !CinematicSuspended;
        cinematicCanvas.overrideSorting = true; cinematicCanvas.sortingOrder = 30;
        cinematicBlend = Mathf.MoveTowards(cinematicBlend, visible ? 1 : 0, ReducedMotion ? 1 : Time.unscaledDeltaTime * 4);
        cinematicCanvas.gameObject.SetActive(visible);
        cinematicGroup.alpha = visible ? 1 : 0;
        cinematicTopBar.sizeDelta = new Vector2(0, 58 * cinematicBlend);
        cinematicBottomBar.sizeDelta = new Vector2(0, 186 * cinematicBlend);
        if (!visible) { cinematicReticle.gameObject.SetActive(false); return; }
        if (previousBeat != tutorialStep) EnterBeat(Beat);
        beatTime += Time.unscaledDeltaTime;
        if (Beat == TutorialBeat.Resources && resourceDetailsOpen) resourceLessonReadTime += Time.unscaledDeltaTime;
        float shortBeat = ReducedMotion ? .2f : 1.2f;
        Attack attack = attacks.Find(a => a.attacker == Allegiance.Player);
        if (Beat == TutorialBeat.Intro && beatTime > (ReducedMotion ? .2f : 1.2f)) EnterBeat(TutorialBeat.CameraZoom);
        else if (Beat == TutorialBeat.CameraZoom && tutorialZoomTravel >= 2) EnterBeat(TutorialBeat.CameraPan);
        else if (Beat == TutorialBeat.CameraPan && tutorialPanTravel >= 3) EnterBeat(TutorialBeat.Resources);
        else if (Beat == TutorialBeat.Resources && resourceLessonReadTime > 3) EnterBeat(TutorialBeat.SelectHome);
        else if (Beat == TutorialBeat.SelectHome && selectedPlanet == 0) EnterBeat(TutorialBeat.Produce);
        else if (Beat == TutorialBeat.Produce && (!string.IsNullOrEmpty(planets[0].queuedUnitId) || unitInventory.TryGetValue("scout",out int scouts) && scouts > 6)) EnterBeat(TutorialBeat.Launch);
        else if (Beat == TutorialBeat.Launch && beatTime > shortBeat) EnterBeat(TutorialBeat.SelectTarget);
        else if (Beat == TutorialBeat.SelectTarget && targetPlanet == tutorialTarget) EnterBeat(TutorialBeat.Attack);
        else if ((Beat == TutorialBeat.Attack || Beat == TutorialBeat.Produce) && attack != null) { tutorialTarget = attack.to; EnterBeat(TutorialBeat.Battle); }
        else if (Beat == TutorialBeat.Battle && planets[tutorialTarget].owner == Allegiance.Player) EnterBeat(TutorialBeat.Captured);
        else if (Beat == TutorialBeat.Battle && attack == null && beatTime > .3f) EnterBeat(TutorialBeat.Attack);
        else if (Beat == TutorialBeat.Captured && beatTime > shortBeat) EnterBeat(TutorialBeat.Specialize);
        else if (Beat == TutorialBeat.Specialize && pendingAssignment < 0) EnterBeat(TutorialBeat.Network);
        else if (Beat == TutorialBeat.Network && (planets[tutorialTarget].antennaRemaining > 0 || planets[tutorialTarget].antennaActive)) EnterBeat(TutorialBeat.NetworkBuild);
        else if (Beat == TutorialBeat.NetworkBuild && planets[tutorialTarget].antennaActive) EnterBeat(TutorialBeat.Research);
        else if (Beat == TutorialBeat.Research && (activeResearch != null || HasResearch("combat1"))) EnterBeat(TutorialBeat.Complete);
        else if (Beat == TutorialBeat.Complete && beatTime > (ReducedMotion ? .3f : 1.2f)) { FinishTutorial(); return; }
        string[] titles = { "첫 번째 신호", "당신의 행성", "함대에 생명을", "부화 신호 수신", "다음 궤도로", "첫 번째 접촉", "방어선 돌파", "지배권 확보", "새로운 거점", "은하를 잇는 신호", "신호 확장 중", "새로운 가능성", "이제 당신의 은하입니다" };
        string[] cues = { "VESPER에 접근 중", "빛나는 행성을 눌러 접속하세요", "생산 버튼을 누르세요 · 생체 40", "함대가 준비되는 동안 다음 목표를 찾습니다", "빛나는 이웃 행성을 선택하세요", "강습을 시작하세요 · 병력 4", "함대가 도착하고 있습니다", planets[tutorialTarget].name + " 점령 완료", "이 행성의 역할을 선택하세요", "행성 위 표식을 눌러 안테나를 설치하세요 · 광물 80", $"연결까지 {planets[tutorialTarget].antennaRemaining:0}초", factoryOpen ? "침식 독성을 연구하세요" : "연구를 열어 새 기술을 확인하세요", "정복하고, 연결하고, 성장하세요" };
        if (Beat == TutorialBeat.Attack && combatUnits < 4) cues[tutorialStep] = "병력을 보충한 뒤 다시 강습하세요";
        if (NavigationLesson || Beat == TutorialBeat.Resources)
        {
            tutorialTitle.text = Beat == TutorialBeat.CameraZoom ? "카메라로 은하를 탐색하세요" : Beat == TutorialBeat.CameraPan ? "지도를 직접 움직여 보세요" : "함대를 움직이는 자원";
            tutorialBody.text = Beat == TutorialBeat.CameraZoom ? "휠을 위나 아래로 두 칸 돌려 확대·축소하세요" : Beat == TutorialBeat.CameraPan ? "우클릭을 누른 채 드래그하세요 · WASD도 가능" : resourceDetailsOpen ? "생체는 생산 · 광물은 연결 · 신경 에너지는 연구" : "상단 ‘수입’을 눌러 자원별 생산량을 확인하세요";
            cinematicChapter.text = "첫 접속 · 탐색과 자원";
            cinematicCaption.alpha = 1;
            SetProgress(cinematicProgress,Beat == TutorialBeat.CameraZoom ? .08f : Beat == TutorialBeat.CameraPan ? .14f : .2f);
            return;
        }
        string title = titles[tutorialStep];
        tutorialTitle.text = title;
        tutorialBody.text = cues[tutorialStep];
        cinematicChapter.text = Beat == TutorialBeat.Intro ? "OBLATION  /  첫 접촉" : "첫 접속  ·  " + Mathf.Min(6, 1 + tutorialStep / 2) + " / 6";
        cinematicCaption.alpha = 1;
        (tutorialRoot.transform as RectTransform).anchoredPosition = new Vector2(0,18);
        SetProgress(cinematicProgress, Beat == TutorialBeat.Intro ? .03f : (tutorialStep + 4) / 16f);
        if (gameplayVolume.profile.TryGet(out Vignette vignette)) vignette.intensity.Override(highContrast ? .08f : .25f);
    }

    bool UpdateCinematicCamera(float dt)
    {
        if (!tutorialActive || CinematicSuspended || NavigationLesson || Beat == TutorialBeat.Resources) return false;
        Vector3 home = planets[0].position, target = planets[tutorialTarget].position;
        Vector3 look = home, offset = new Vector3(2.5f,4,-5.2f);
        float fov = 40;
        if (Beat == TutorialBeat.Intro)
        {
            float t = ReducedMotion ? 1 : Mathf.SmoothStep(0,1,Mathf.Clamp01(beatTime / 1.1f));
            offset = Vector3.Lerp(new Vector3(7,11,-15),new Vector3(2.5f,4,-5.2f),t);
        }
        else if (Beat == TutorialBeat.SelectTarget || Beat == TutorialBeat.Attack || Beat == TutorialBeat.NetworkBuild)
        { look = (home + target) * .5f; offset = new Vector3(1,12,-17); fov = 44; }
        else if (Beat == TutorialBeat.Battle)
        {
            Attack attack = attacks.Find(a => a.attacker == Allegiance.Player && a.to == tutorialTarget);
            float t = attack == null ? 1 : Mathf.Clamp01(attack.elapsed / attack.duration);
            look = Vector3.Lerp(home,target,ReducedMotion ? .5f : Mathf.SmoothStep(0,1,t));
            offset = ReducedMotion ? new Vector3(1,8,-10) : new Vector3(3.5f,5,-6.5f);
        }
        else if (Beat >= TutorialBeat.Captured && Beat < TutorialBeat.Complete)
        { look = target; offset = new Vector3(-2.5f,3.8f,-5.2f); }
        else if (Beat == TutorialBeat.Complete)
        { look = Vector3.zero; offset = new Vector3(0,18,-18); fov = 50; }
        if (!ReducedMotion && (Beat == TutorialBeat.Captured || Beat == TutorialBeat.Network))
            offset = Quaternion.Euler(0,Mathf.Sin(beatTime * .25f) * 5,0) * offset;
        offset *= cinematicZoom;
        float blend = ReducedMotion ? 1 : 1 - Mathf.Exp(-dt * 6f);
        cinematicLook = Vector3.Lerp(cinematicLook,look,blend);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position,look + offset,blend);
        gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation,Quaternion.LookRotation(cinematicLook - gameCamera.transform.position),blend);
        gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView,fov,blend);
        cameraLookFocus = cinematicLook;
        return true;
    }

    Button CinematicControl()
    {
        if (!tutorialActive || CinematicSuspended) return null;
        if (Beat == TutorialBeat.Resources) return resourceDetailsButton;
        if (Beat == TutorialBeat.SelectHome || Beat == TutorialBeat.SelectTarget || Beat == TutorialBeat.Network) return cinematicTargetButton;
        if (Beat == TutorialBeat.Produce || Beat == TutorialBeat.Attack && combatUnits < 4) return quickProduceButton;
        if (Beat == TutorialBeat.Attack) return assaultButton;
        if (Beat == TutorialBeat.Research) return factoryOpen ? orbitalTechButton : doctrineOpenButton;
        return null;
    }

    void UpdateCinematicReticle()
    {
        Button control = CinematicControl();
        bool world = control == cinematicTargetButton;
        bool show = control != null && (world || control.gameObject.activeInHierarchy);
        cinematicReticle.gameObject.SetActive(show);
        if (!show) { lastCinematicControl = null; return; }
        RectTransform canvas = cinematicCanvas.transform as RectTransform;
        Vector2 center, size;
        if (world)
        {
            Planet planet = planets[Beat == TutorialBeat.SelectHome ? 0 : tutorialTarget];
            Vector3 screen = gameCamera.WorldToScreenPoint(planet.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,screen,null,out center);
            Vector3 edge = gameCamera.WorldToScreenPoint(planet.position + gameCamera.transform.right * planet.scale);
            float diameter = Mathf.Clamp(Vector3.Distance(screen,edge) * canvas.rect.width / Screen.width * 2.7f,110,280);
            size = Vector2.one * diameter;
            cinematicTargetLabel.text = Beat == TutorialBeat.Network ? "안테나 설치" : "행성 선택";
            cinematicTargetButton.interactable = Beat != TutorialBeat.Network || CanInstallAntenna();
        }
        else
        {
            Vector3[] corners = new Vector3[4]; (control.transform as RectTransform).GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,corners[0],null,out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,corners[2],null,out Vector2 max);
            center = (min + max) * .5f; size = max - min + Vector2.one * 18;
            cinematicTargetLabel.text = "";
        }
        cinematicTargetButton.GetComponent<Image>().raycastTarget = world;
        cinematicTargetButton.interactable = world && (Beat != TutorialBeat.Network || CanInstallAntenna());
        cinematicReticle.anchoredPosition = center;
        cinematicReticle.sizeDelta = size * (ReducedMotion ? 1 : 1 + .018f * Mathf.Sin(beatTime * 3));
        reticleGroup.alpha = ReducedMotion ? 1 : .8f + .2f * Mathf.Sin(beatTime * 2);
        cinematicScanLine.gameObject.SetActive(world && !ReducedMotion);
        cinematicScanLine.anchoredPosition = new Vector2(0,Mathf.Sin(beatTime * 1.4f) * size.y * .4f);
        if (control != lastCinematicControl && EventSystem.current != null)
        { EventSystem.current.SetSelectedGameObject(control.gameObject); lastCinematicControl = control; }
    }

    void ApplyFocusedHud(bool producing)
    {
        bool cine = tutorialActive && !CinematicSuspended;
        bool modal = operationsOpen || factoryOpen || optionsOpen || paused || pendingAssignment >= 0;
        mapOverviewButton.gameObject.SetActive(!cine && !storyActive);
        PlaceUi(mapOverviewButton.transform,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(135,resourceDetailsOpen?-170:-120),new Vector2(230,52));
        navigationHint.gameObject.SetActive(!cine && !storyActive && !modal);
        tutorialAction.gameObject.SetActive(false);
        tutorialRoot.SetActive(cine && Beat != TutorialBeat.Specialize);
        bool resourceLesson = cine && Beat == TutorialBeat.Resources;
        foreach (Image card in resourceCards) card.gameObject.SetActive(!cine || resourceLesson);
        foreach (Text rate in resourceRates) rate.gameObject.SetActive((!cine || resourceLesson) && resourceDetailsOpen);
        (resourceCanvas.transform as RectTransform).sizeDelta = new Vector2(0,resourceDetailsOpen && (!cine || resourceLesson) ? 134 : 90);
        resourceCanvas.GetComponent<Image>().color = cine ? Color.clear : new Color(.015f,.035f,.055f,.97f);
        resourceDetailsButton.gameObject.SetActive(!cine || resourceLesson); historyButton.gameObject.SetActive(!cine);
        helpButton.gameObject.SetActive(!cine); speedButton.gameObject.SetActive(!cine); pauseOpenButton.gameObject.SetActive(!cine);
        doctrineOpenButton.gameObject.SetActive(!cine || Beat == TutorialBeat.Research);
        PlaceUi(doctrineOpenButton.transform,cine ? new Vector2(.5f,1) : Vector2.one,cine ? new Vector2(.5f,1) : Vector2.one,
            cine ? new Vector2(0,-94) : new Vector2(-372,-18),cine ? new Vector2(240,60) : new Vector2(110,54));
        SetButtonText(doctrineOpenButton,cine ? "연구 열기" : "연구");
        SetButtonText(resourceDetailsButton,resourceDetailsOpen ? "수입 닫기" : "수입");
        SetButtonText(historyButton,historyOpen ? "기록 닫기" : "기록");
        eventLogText.transform.parent.gameObject.SetActive(!cine && !modal && historyOpen);
        ownershipText.gameObject.SetActive(!cine && !modal && historyOpen);
        statusText.transform.parent.gameObject.SetActive(false);
        focusUi.gameObject.SetActive(false);
        researchStatus.gameObject.SetActive(!cine && !modal && activeResearch != null);
        researchProgress.transform.parent.gameObject.SetActive(!cine && !modal && activeResearch != null);
        bool own = selectedPlanet >= 0 && planets[selectedPlanet].owner == Allegiance.Player;
        bool productionCue = cine && (Beat == TutorialBeat.Produce || Beat == TutorialBeat.Attack && combatUnits < 4);
        quickProduceButton.transform.parent.gameObject.SetActive(productionCue || !cine && !modal && (producing || own));
        PlaceUi(quickProduceButton.transform.parent,productionCue ? new Vector2(.5f,0) : Vector2.zero,
            productionCue ? new Vector2(.5f,0) : Vector2.zero,productionCue ? new Vector2(0,210) : new Vector2(22,24),new Vector2(460,124));
        bool attackCue = cine && Beat == TutorialBeat.Attack && combatUnits >= 4;
        planetPanel.SetActive(attackCue || !cine && !modal && selectedPlanet >= 0);
        bool expanded = detailsExpanded && !cine;
        PlaceUi(planetPanel.transform,attackCue ? new Vector2(.5f,0) : new Vector2(1,0),
            attackCue ? new Vector2(.5f,0) : new Vector2(1,0),attackCue ? new Vector2(0,210) : new Vector2(-22,24),new Vector2(420,attackCue ? 144 : expanded ? 620 : 236));
        planetPanel.transform.localScale = Vector3.one * Mathf.Min(1,((RectTransform)hudScreen.transform).rect.height / (expanded ? 790 : 400));
        planetDetailsButton.gameObject.SetActive(!cine);
        SetButtonText(planetDetailsButton,expanded ? "접기" : "상세");
        planetMetaText.gameObject.SetActive(!attackCue); planetStatsText.gameObject.SetActive(!attackCue);
        planetTraitText.gameObject.SetActive(expanded); planetProductionText.gameObject.SetActive(expanded); planetHintText.gameObject.SetActive(expanded);
        planetInteriorImage.transform.parent.gameObject.SetActive(expanded);
        sourceButton.gameObject.SetActive(false);
        if (selectedPlanet >= 0)
        {
            Planet p = planets[selectedPlanet];
            if (!expanded) planetStatsText.text = $"인구 {p.population:0}억  ·  방어 {p.defense:0}";
            PlaceUi(planetStatsText.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(20,expanded ? -138 : -94),new Vector2(380,expanded ? 64 : 32));
            bool hostile = !own && !p.destroyed;
            assaultButton.gameObject.SetActive(attackCue || hostile);
            surpriseButton.gameObject.SetActive(expanded && hostile && techViral);
            orbitalButton.gameObject.SetActive(expanded && hostile && techOrbital);
            zoomButton.gameObject.SetActive(!cine && own);
            operationsOpenButton.gameObject.SetActive(!cine && own);
            float y = attackCue ? -68 : expanded ? -552 : -158;
            PlaceUi(assaultButton.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(expanded ? 150 : 20,y),new Vector2(expanded ? 120 : 380,50));
            assaultButton.interactable &= targetPlanet == selectedPlanet && sourcePlanet >= 0 && planets[sourcePlanet].links.Contains(selectedPlanet);
            SetButtonText(assaultButton,assaultButton.interactable ? "강습 · 병력 4" : IsTargeted(selectedPlanet) ? "전투 진행 중" : combatUnits < 4 ? "병력 4기 필요" : "보급로 / 자원 필요");
            PlaceUi(operationsOpenButton.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(20,y),new Vector2(244,50));
            PlaceUi(zoomButton.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(274,y),new Vector2(126,50));
        }
        feedbackText.gameObject.SetActive(feedbackText.gameObject.activeSelf && !cine && !modal);
        for (int i = 0; i < unitButtons.Length; i++) unitButtons[i].gameObject.SetActive(operationTab == 0);
        antennaButton.gameObject.SetActive(operationTab == 1);
        foreach (Button button in traitButtons) button.gameObject.SetActive(operationTab == 1);
        foreach (Button button in upgradeButtons) button.gameObject.SetActive(operationTab == 2);
        foreach (Button button in exterminatusButtons) button.gameObject.SetActive(operationTab == 2);
        operationHeadings[0].gameObject.SetActive(operationTab == 0);
        operationHeadings[1].gameObject.SetActive(operationTab == 2);
        operationHeadings[2].gameObject.SetActive(operationTab == 2);
        for (int i = 0; i < operationTabs.Length; i++) operationTabs[i].interactable = operationTab != i;
        bool firstResearch = cine && Beat == TutorialBeat.Research;
        RectTransform researchCard = doctrineModal.transform.Find("Card") as RectTransform;
        researchCard.sizeDelta = firstResearch ? new Vector2(460,610) : new Vector2(1120,650);
        researchCard.Find("Heading").GetComponent<Text>().text = firstResearch ? "첫 전투 연구" : "연구";
        researchCard.Find("Description").gameObject.SetActive(!firstResearch);
        for (int i=0;i<researchChoiceCards.Length;i++)
        {
            researchChoiceCards[i].gameObject.SetActive(!firstResearch || i==2);
            researchChoiceCards[i].anchoredPosition = firstResearch ? new Vector2(64,-118) : new Vector2(30+i*364,-150);
        }
        PlaceUi(doctrineCloseButton.transform,new Vector2(0,1),new Vector2(0,1),firstResearch ? new Vector2(70,-536) : new Vector2(400,-574),new Vector2(320,50));
    }

    static void PlaceUi(Transform target,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {
        var rect=(RectTransform)target;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;
    }
}
