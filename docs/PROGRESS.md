# PlanetGame 진행 기록

## 현재 상태 (2026-09-27, Asia/Seoul)

| 작업 | 상태 | 확인 결과 |
| --- | --- | --- |
| Codex 프로젝트 컨텍스트 500k | 완료 | `.codex/config.toml` 설정 및 Codex 설정 로드 확인 |
| 공식 Unity CLI 설치 | 완료 | `unity --version` → `1.0.0-beta.11` |
| Unity Pipeline 패키지 | 완료 | `com.unity.pipeline` `0.8.0-exp.1`; CLI에서 에디터 서버 연결 확인 |
| Figma MCP 기준 수립 | 완료 | 페이지 `0:1`, 152개 노드; `FIGMA_SYNC.md` 기록 |
| 프로젝트 작업 지침 | 완료 | 루트 `AGENTS.md` 작성 |
| 사용량 50% 조건/일일 예약 | 완료 | 현재 주간 소진율 0%; 서울 시간 매일 09:00, 사전 점검 통과 시 실행 |
| Inspector 기반 씬 전환 | 완료 | `SampleScene`의 단일 `Root` 아래 `GameUI`, `HudUI`, `Util`; 12개 행성·21개 항로·uGUI 직렬화 |
| 기본 검증 | 완료 | 플레이 화면 제목/HUD 확인, 콘솔 오류 0, EditMode 스모크 테스트 2개 통과 |
| Figma 기존 기획 1차 반영 | 완료 | 시작 집중 화면, 인구·회복·점유, 3종 개발 방향과 생산 효과 |

## 작업 로그

### 2026-09-27

- 공식 Unity CLI 설치. 기존 Unity Editor `6000.3.22f1`과 프로젝트 인식 확인.
- `com.unity.pipeline`을 프로젝트에 설치하고 Unity CLI의 에디터 서버 연결을 확인.
- Figma MCP에서 기존 링크의 기획 페이지와 참고 이미지를 확인하고 동기화 기준을 작성.
- `tools/Check-CodexUsage.ps1`에서 활성 Codex 계정 사용률을 확인하며, 50% 미만일 때만 통과하도록 구성. 실제 현재 응답 0%, 종료 코드 0 확인.
- Orca 예약 작업 `f8d63397-2462-4921-ad22-1206592b93cd` 생성 및 활성화. `precheck`가 50% 이상/정보 없음이면 에이전트를 시작하지 않는다. 기존 프로젝트 폴더에서 매일 09:00(Asia/Seoul) 실행.
- Unity CLI `get_scene_hierarchy`로 `SampleScene`의 현재 루트가 Main Camera, Directional Light, Global Volume 세 개뿐임을 확인.
- `Assets/Scripts/OblationGame.cs`의 런타임 은하·화면 생성 및 `OnGUI`를 제거하고, `SampleScene`에 고정 객체를 배치. `tools/OblationSceneBuilder.cs`는 에디터에서 1회 씬을 구성하는 스크립트로 `Assets` 밖에 보관한다.
- `Root/GameUI`, `Root/HudUI`, `Root/Util` 계층과 Inspector 참조를 저장. 전투 빔·충격파만 일시적으로 생성한다.
- 플레이 모드에서 제목 화면과 시작 후 HUD를 프레임 캡처로 확인. Unity CLI의 기본 GameView 캡처는 화면 오버레이를 제외하므로 실제 UI 검증에는 `ScreenCapture`를 사용했다.
- `OblationSmokeTests` 2개 통과(은하 연결성, 저장된 씬 계층/참조). `recompile_status` 컴파일 실패 없음, 플레이 후 콘솔 오류·경고 0건.
- Unity MCP(`127.0.0.1:8080/mcp`)는 현재 꺼져 있어 CLI로 작업했다. Figma MCP는 기준 페이지 조회에 사용했다.
- Figma 재조회 결과 기준 메타데이터 해시가 동일해 새 변경은 없었다. 명시적 사용자 요청에 따라 기존 미반영 노드 `5:19`, `5:21`, `5:33`, `32:13`, `34:9`, `34:27`, `34:41`, `34:49`, `34:57`을 게임 구조에 맞게 반영했다.
- 시작 시 행성 하나만 표시하고 지도 전환을 제공. 60~120억 인구, 인구 0 후 약 10분 회복, 두 진영 점유 외곽선과 점유율에 따른 소유권을 적용했다. 시작 행성을 유닛 생산형으로 바꾸고 전투·노동 유닛, 제조·에너지 생산을 연결했다.
- Unity CLI 플레이 검증: 시작 직후 활성 행성 1개(`Unit`, 최대 120억), 1초 회복 0.2억, 지도 전환 후 활성 행성 12개. 컴파일 성공, EditMode 2/2, 콘솔 오류·경고 0건.

## 매일 점검 기록

| 날짜 (Asia/Seoul) | Codex 소진율 | Figma 변경 | Unity/코드 반영 | 검증 |
| --- | --- | --- | --- | --- |
| 2026-09-27 | 주간 0%, 세션 값 없음 | 최초 기준과 동일; 기존 미반영 기획 1차 적용 | Unity CLI/Pipeline 설치, 씬 Inspector 전환, 인구·점유·개발 방향 | CLI 연결, EditMode 2/2, 플레이 검증·콘솔 오류 0 |
