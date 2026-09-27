# PlanetGame

이 저장소는 Unity `6000.3.22f1`의 **Universal Render Pipeline(URP) 3D** 프로젝트다. `Packages/manifest.json`에 URP `17.3.0`이 있고, `ProjectSettings/GraphicsSettings.asset`의 기본 파이프라인은 `Assets/Settings/PC_RPAsset.asset`을 참조한다. Mobile·PC 품질 단계도 URP 에셋을 사용한다.

## 플레이 시작

`Assets/Scenes/SampleScene.unity`를 열고 Play를 실행한다. 새 작전은 병력 6기로 시작한다. 도입 스토리 이후 16단계 튜토리얼에서 휠 줌 → 우클릭 드래그 → 자원 수입 확인 → 행성 선택 → 생산 → 강습 → 점령 → 안테나 → 연구를 직접 수행한다. 안내는 한 번에 한 행동만 보여 주며, 건너뛰기 또는 Esc로 끝내고 상단 `튜토리얼` 또는 H로 다시 열 수 있다.

지도는 36개 행성과 69개 보급로로 확장했다. 기존 12개 중심 행성 주변에 중간·외곽 성역 24개를 배치했으며 전체 가로 폭은 기존의 약 7배다. 새 작전의 도입, 첫 안테나 연결, 적 함대의 첫 공격에 각각 3개 장면으로 된 스토리 컷씬이 재생된다. 컷씬에서는 게임 진행이 잠시 멈추며 Space·다음 버튼으로 넘기거나 Esc로 해당 컷씬만 건너뛸 수 있다.

평소에는 상단에 자원 보유량만 표시한다. `수입`·`기록`으로 추가 정보를 열고, 행성을 선택하면 해당 행성의 행동이 나타난다. `상세`로 설명과 내부 단면을 펼칠 수 있다. 운영 창은 `병력 생산`·`행성 개발`·`강화 · 절멸` 탭으로 나눠 필요한 기능만 보여 준다.

| 조작 | 기능 |
| --- | --- |
| 마우스 클릭 / Tab | 행성 선택·카메라 포커스 |
| 우클릭을 누른 채 드래그 | 지도 이동 |
| M / 전체 지도 버튼 | 은하 전체 보기 |
| 1 / 2 | 빠른 병력 생산 / 선택 목표 강습 |
| F / H / P | 연구 / 도움말 / 일시정지 |
| WASD / Enter | 지도 이동 / 행성 집중 전환 |
| 마우스 휠 위 / 아래 | 확대 / 축소 (지도·행성 집중·튜토리얼) |
| 방향키 / Enter | UI 항목 이동 / 실행 |

상단에서 1·2·3배속을 선택하고 설정에서 UI 크기, 움직임 줄이기, 강한 효과 줄이기, 높은 대비를 조절한다. URP의 HDR·SMAA·Bloom·색 보정은 `Assets/Settings/OblationGameplayProfile.asset`과 씬 카메라에 저장되어 있다.

휠 줌과 지도 이동은 설정의 카메라 감도를 따른다. UI 위에서 시작한 드래그와 메뉴·일시정지 중의 카메라 조작은 차단한다. 멀리 있는 행성에는 작은 선택 표식을 표시한다. 클릭 포커스는 주변 행성과 보급로를 함께 보여 주며 Enter의 행성 집중 화면은 별도로 사용할 수 있다.

행성 대기 Shader Graph, 다중 광원·오로라, 함대 잔광·충격 링, 홀로그램 UI와 사건별 후처리를 적용했다. 에셋 위치와 강도 조절 방법은 [연출 구성](docs/VISUAL_EFFECTS.md)에 정리했다.

행성은 암석·화산·빙권·가스·사막 등 자연스러운 외계 지질의 [8개 외형 계열](docs/PLANET_STYLES.md)로 나뉘며 새 작전마다 지형·색조·구름량이 조금씩 달라진다. UI는 큰 Silver 글자와 즉시 표시되는 패널을 사용한다. 주요 안내는 밝은 외계 기호가 0.6초에 걸쳐 한국어로 해독되는 효과와 전자음으로 표시한다.

## 다른 컴퓨터에서 열기

1. 저장소 루트(`Assets`, `Packages`, `ProjectSettings`가 함께 있는 폴더)를 Unity Hub에 추가한다.
2. Unity `6000.3.22f1`로 열고 패키지 가져오기가 끝날 때까지 기다린다.
3. Editor의 **Project Settings > Graphics**에서 기본 파이프라인이 `PC_RPAsset`인지 확인한다. **Project Settings > Quality**에서는 Mobile·PC 단계에 URP 에셋이 지정돼 있는지 확인한다.
4. Test Runner의 EditMode에서 `OblationSmokeTests.Project_UsesUrpAtEveryQualityLevel`을 실행한다.

Unity Hub의 프로젝트 목록에 Built-In Render Pipeline 경고가 보이더라도, 실제 렌더 파이프라인은 Editor의 Graphics/Quality 설정과 위 테스트로 확인한다. 프로젝트를 새 템플릿으로 다시 만들거나 렌더 파이프라인 에셋을 교체할 필요는 없다.
