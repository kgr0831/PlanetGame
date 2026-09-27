# URP 연출 구성

2026-09-27 사용자의 UI·VFX 강화 요청에 따라 `SampleScene`에 저장했다. 추가 패키지나 수동 설정은 필요하지 않다.

## 편집할 에셋

| 대상 | 파일 | 조절 항목 |
| --- | --- | --- |
| 행성 대기 | `Assets/Shaders/OblationAtmosphere.shadergraph` | Fresnel → 투명도, HDR 색상 × 강도 → 발광. `Atmosphere.mat`의 `_RimPower`, `_Intensity`, `_Opacity` |
| 행성 표면 | `Assets/Shaders/OblationPlanet.shader`, `OblationPlanetStyles.hlsl`, `OblationPlanetSurface.hlsl` | 외계 암석·화산·사막·빙권·가스·충돌 분지·철분·광물 지질, 자연스러운 요철과 제한된 발광 |
| 행성 고리 | `Assets/Shaders/OblationPlanetRing.shader` | EIDOLON·SERAPH의 먼지 띠와 틈, 원거리 무늬 필터링 |
| 구름층 | `Assets/Shaders/OblationClouds.shader`, `PlanetClouds.mat` | 표면 위 별도 구체의 구름·미세 기류·회전, 행성별 시드와 구름량 |
| 우주 배경 | `Assets/Shaders/OblationStarfield.shader` | 여러 크기의 별, 청록·보라 성운, 어두운 먼지 띠 |
| UI | `Assets/Shaders/OblationHologram.shader`, `OblationHologram.cs`, `OblationButtonFeedback.cs` | 모서리·발광 테두리·호버 광택·클릭 파동·패널 등장 |
| 충격 링 | `Assets/Shaders/OblationEnergyPulse.shader`, `OblationEnergyPulse.cs` | 확장 링·잔향·중앙 섬광·방사광, 1.7초 후 제거 |
| 함대·충돌 | `OblationAttackVisual.cs`, `OblationCombatBurst.cs` | 여러 가닥의 함대 잔광, 늘어나는 파편, 순간 점광원 |
| 액션별 연출 | `OblationActionFx.shader`, `OblationActionFx.cs`, `ActionFx.mat` | 강습·역병·궤도·안테나·생산·연구·점령 7개 형태, HDR 발광과 수명 |
| 안테나 구축 | `OblationSignalVisual.cs`, `OblationGame.ActionEffects.cs` | 수직 안테나, 3차원 전파 링, 보급로를 따라 흐르는 신호 묶음 |
| 후처리 | `Assets/Settings/OblationGameplayProfile.asset`, `OblationVisualDirector.cs` | Bloom 1.05 / 임계값 0.9 / 산란 0.76, ACES, HDR 색 보정, Split Toning, 약한 필름 그레인·색수차, 사건별 짧은 강도 변화 |

Shader Graph는 URP Unlit/Transparent/Additive 대상으로 저장했으며, 36개 행성의 `Body/Atmosphere` 렌더러에서 실제 사용한다. 공통 머티리얼과 행성별 `MaterialPropertyBlock`으로 색과 집중도를 적용한다. 표면·배경·UI·액션 효과에는 커스텀 HLSL 셰이더를 사용한다.

블룸은 밝은 픽셀에서 퍼지므로 후처리 강도뿐 아니라 대기·궤적·충돌의 HDR 출력도 함께 조절했다. 기준은 Unity의 [URP Bloom 설명](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing-bloom.html)과 [Shader Graph Fresnel 노드](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Fresnel-Effect-Node.html)를 확인했다. UI는 오버레이 캔버스에서 자체 발광 테두리를 그리므로 글자에 카메라 블룸이 번지지 않는다.

## 접근성과 성능

- `강한 효과 줄이기`: 블룸 0.2, 색수차 0, 대기·UI·입자·충격파 강도 축소. 기본 장면과 사건 연출 모두 적용한다.
- `움직임 줄이기`: UI 클릭 파동·스캔 이동·나타나는 애니메이션을 억제하고 기존 카메라 제한을 유지한다.
- PC는 HDR 64비트 버퍼와 HDR 색 보정을 사용한다. 저장된 청록·보라 보조 점광원 2개와 전투 점광원은 그림자를 생성하지 않는다.
- 충돌 입자는 효과당 최대 128개다. 충격파용 머티리얼은 효과 종료 시, UI 머티리얼은 컴포넌트 종료 시 제거한다. 고정 행성·대기·UI·조명은 씬에 직렬화돼 있다.
- 고해상도·저사양 GPU·모바일의 프레임 시간은 별도 실기 측정이 필요하다. 이번 검증은 현재 PC의 Unity Editor에서 수행했다.

## 씬 재작성과 검증

이미 저장된 씬은 그대로 실행하면 된다. 에디터에서 재작성할 때만 공식 Unity CLI의 `run_script`로 `OblationAtmosphereGraph.Build`, `OblationVfxBuilder.Build`, `OblationPlanetDetailBuilder.Build`, `OblationPlanetVarietyBuilder.Build`, `OblationSilverUiBuilder.Build`, `OblationUiReadabilityBuilder.Build`, `OblationExpansionBuilder.Build` 순서로 실행한다. 마지막 확장 패스가 36개 행성·69개 보급로·스토리 UI·지도 조작 참조를 저장한다.

- EditMode `OblationSmokeTests`: URP 설정, 셰이더 11개(HLSL 10개·Shader Graph 1개), 36개 행성의 연결도·저장 위치, 8개 표면 계열·6개 고리·구름·연출 참조와 Silver 폰트를 검증한다.
- Play `OblationCinematicReview.Walkthrough`: 실제 생산·공격·점령·안테나·연구를 수행하고 `Temp/vfx_battle.png`, `Temp/vfx_capture.png`에 화면을 저장한다.
- Play `OblationCinematicReview.VerifyEffects`: 원점이 아닌 목표 좌표의 입자 생성, 수명 종료 후 정리, 강한 효과 줄이기의 후처리 값을 검증한다.
- 검증 후 `OblationCinematicReview.RestorePreferences`로 완료 설정을 복원하고 Play 모드를 종료한다.

## 행성 질감 조절

행성별 표면 머티리얼에서 `_Relief`는 요철 강도, `_CloudCoverage`는 구름 발생 임계값, `_CloudOpacity`는 구름 양이다. `OblationPlanetView`가 표면과 구름층에 같은 시드·임계값을 전달한다. 계열별 외형과 조절 항목은 [행성 외형](PLANET_STYLES.md)에 기록했다.

지형은 구면 좌표의 절차적 노이즈로 계산하며, 거리에 따라 화면의 한 픽셀보다 작은 무늬를 감쇠한다. 대기층 반지름은 표면의 1.025배, 구름층은 1.014배다. 새 작전의 외형은 독립 시드로 생성하며 [랜덤 변화 범위](PLANET_STYLES.md)에 기록했다. Play 모드의 `OblationPlanetReview.Before`·`After`는 같은 카메라·행성 각도로 `Temp/planet_before.png`·`planet_after.png`를 저장하고 원래 화면 상태를 복원한다.

## Silver·디지털 UI

- `Assets/Silver.ttf`를 `interfaceFont`에 연결하고 UI 문구 143개에 적용했다. 런타임의 시스템 폰트 덮어쓰기를 제거했다. 한글 글리프를 확인하고 Silver의 줄 높이에 맞춰 자막 영역과 버튼 자동 크기 조절을 적용했다.
- 홀로그램 패널과 글자는 처음부터 표시한다. 검은 시작 페이드·패널 투명도 페이드는 제거하고 테두리 스캔만 약 0.25초 동안 재생한다. 버튼의 클릭 파동은 약 0.28초, 눌림 해제는 0.25초이며 호버 광택과 크기 변화도 부드럽게 따라온다.
- `OblationTerminalText`는 전체 한국어 문자열과 글자 배치를 유지하면서 글자 UV만 외계 기호로 치환했다가 0.6초에 걸쳐 복원한다. 기호 갱신은 초당 18회다. 모든 글자는 처음부터 밝게 보이며 수치만 바뀌면 해독을 재시작하지 않는다. 움직임 줄이기에서는 한국어가 즉시 표시된다.
- `OblationUiAudio`는 타이핑·호버·패널 접속 전자음을 생성한다. 타이핑은 75ms, 호버는 100ms 간격을 두며 기존 효과음 볼륨과 음소거를 따른다. 오디오 파일이나 추가 패키지는 필요하지 않다.
- 자막 제목 54·본문 42, 자원 숫자 최대 50, 주요 버튼 42 기준으로 크기를 키웠다. 글자 영역과 생산 카드 높이도 늘려 자동 축소·잘림을 줄였다. 실제 표시 크기는 UI 배율과 버튼별 가용 폭에 따라 조절된다.
- 새 작전은 이전 완료 기록과 무관하게 튜토리얼을 자동 시작한다. 도입·짧은 장면·마무리는 각각 1.2초로, 해독이 끝난 뒤에도 자막을 볼 시간을 둔다. 화면 상하단 띠는 0.25초에 펼쳐지며 글자와 버튼 입력은 즉시 표시·활성화한다. 상단 `튜토리얼` 또는 H로 다시 열 수 있다.
- `OblationTerminalReview.Verify`로 완료 기록이 있는 상태의 튜토리얼 복구, 첫 프레임의 글자 표시, 0.2초 시점의 해독 진행·0.75초 시점의 완료, 원문·배치 유지, 큰 자막의 영역 적합성, 움직임 줄이기·효과음 음소거를 확인한다.

## 마우스 휠 카메라

- 휠 위로 확대, 아래로 축소한다. Input System의 기본 정규화 값(한 칸 약 1)을 사용하며 Windows의 원시 입력 범위를 선택한 경우에만 120으로 나눈다. 기존 입력 시스템도 한 칸 단위로 처리한다.
- 한 칸마다 현재 거리의 약 8%씩 목표 거리를 바꾸고 기존 카메라 보간을 적용한다. 카메라 감도 설정을 따르며 지도 거리 4~210, 행성 집중 거리 3~90 안으로 제한한다. 실제 카메라 오프셋은 거리 값에 `(0, 0.72, -0.72)`를 곱한다.
- 튜토리얼에서는 장면의 기본 카메라 오프셋을 0.55~2.2배로 조절하고, 줌·이동 교육에서는 실제 지도 카메라를 조작한다. UI 위·일시정지·설정·연구·운영·행성 역할 선택 중에는 줌 입력을 받지 않는다.
- `OblationCameraReview.Verify`는 실제 Input System 큐에 휠·포인터 입력을 넣어 세 화면의 확대·축소, 실제 카메라 이동, 거리 제한, UI·메뉴 입력 차단, 움직임 줄이기와 새 작전 초기화를 확인한다.

## 확장 지도·스토리·액션 구분

- `OblationGalaxyLayout`이 36개 행성 이름·위치와 69개 연결을 정의한다. 기존 중심부 간격은 2.5배이며 반경 약 40·64의 성역을 추가했다. 고정 객체는 `OblationExpansionBuilder`가 씬에 저장한다. 지도 카메라 이동 범위는 X/Z ±78, 전체 보기 거리는 170이다.
- 우클릭 드래그는 화면의 두 포인터 위치를 지면 평면으로 투영해 이동한다. 클릭은 해당 행성으로 중심과 거리를 부드럽게 맞춘다. 원거리에는 작은 행성 표식과 최소 18픽셀 선택 판정을 사용한다. M·전체 지도 버튼으로 복귀한다.
- `OblationGame.Story.cs`는 도입·첫 연결·적 함대 조우의 3개 장, 장마다 3개 장면을 관리한다. 각 장면은 6초이며 다음 버튼·Space로 넘기고 Esc로 현재 장만 건너뛴다. 카메라의 전체 조망·접근·궤도 이동, 검은 상하단 띠, Silver 자막·해독 효과와 사건 VFX를 함께 사용한다. 화면 전체를 검게 페이드하지 않는다.
- 스토리 대사는 기존의 VESPER·NEMESIS·신호·정복 소재로 이번 요청에 맞춰 구성한 초안이다. 별도 확정된 시나리오 원문을 그대로 구현했다고 간주하지 않는다. 컷씬 중에는 경제·전투·건설 시간이 멈추며, 종료 후 진행하던 튜토리얼과 조작을 복원한다. 한 작전에서 각 장은 한 번만 재생된다.
- 튜토리얼은 16단계다. 휠 두 칸, 우클릭 드래그(또는 WASD), 자원 수입 버튼을 실제 조작한 뒤 기존 생산·강습·점령·연결·연구로 이어진다. 자원 설명 후 수입 상세는 다시 접는다.

| 행동 | 구분되는 연출 |
| --- | --- |
| 강습 | 화살촉 형태 편대·대형별 잔광·방사형 충돌 파편 |
| 역병 | 저고도 굴곡 경로·나선형 잔광·부드러운 녹색 소용돌이 |
| 궤도 공격 | 목표 위 충전 궤도·수직 낙하 광선·교차 섬광 |
| 안테나 | 건설 중 3차원 전파 링·이동 신호 묶음·연결 완료 전파 |
| 생산 | 위로 올라가는 갈매기형 발광 표시 |
| 연구 | 교차하는 타원 궤도·공전하는 광점 |
| 점령 | 확장하는 관 모양 광륜·기존 구체 충격파 |

`OblationExpansionReview.VerifyNavigation`은 원거리 행성 중심에서 10픽셀 떨어진 지점의 클릭, 실제 카메라 접근, 우클릭 유지·해제, UI에서의 드래그 차단, 전체 보기 복귀를 검증한다. 검증용 가상 마우스와 임시 입력 설정은 `finally`에서 제거·복원한다. `VerifyStory`는 자막 영역, 게임 시간 정지, 다음·건너뛰기·Esc와 중복 큐 방지를 검사한다. `CaptureEffects`는 7개 액션의 화면과 수명 종료 후 정리를 확인한다.
