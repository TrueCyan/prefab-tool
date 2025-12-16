# Unity MCP Server

Claude Code와 Unity Editor를 연결하는 MCP(Model Context Protocol) 서버입니다.

## 개요

이 MCP 서버를 사용하면 Claude Code에서 직접 Unity Editor를 제어할 수 있습니다:

- **AssetDatabase.Refresh()** - 파일 변경 후 Unity가 인식하도록 새로고침
- **콘솔 로그 조회** - 에러, 경고, 일반 로그 확인
- **컴파일 상태 확인** - 컴파일 에러 자동 감지
- **Play 모드 제어** - Play/Stop/Pause
- **에셋 하이라이트** - 작업 결과물을 Unity에서 바로 확인

## 설치

### 1. Unity Editor Script 설치

`unity-mcp/Editor/UnityMCPServer.cs` 파일을 Unity 프로젝트의 `Assets/Editor/` 폴더에 복사합니다.

```bash
cp unity-mcp/Editor/UnityMCPServer.cs /path/to/your/unity/project/Assets/Editor/
```

또는 Unity Package Manager로 설치할 수 있도록 패키지 형태로 추가:

```bash
# Unity 프로젝트의 Packages 폴더에 심볼릭 링크 생성
ln -s /path/to/unityflow/unity-mcp /path/to/your/unity/project/Packages/com.unityflow.mcp
```

### 2. Claude Code에 MCP 서버 등록

```bash
# 로컬 프로젝트에 추가
claude mcp add unity-mcp -s local -- python -m unityflow.mcp

# 또는 전역으로 추가
claude mcp add unity-mcp -s user -- python -m unityflow.mcp
```

설정 확인:
```bash
claude mcp list
```

## 사용법

### Unity에서 서버 시작

1. Unity Editor에서 **Tools > Unity MCP > Start Server** 메뉴 클릭
2. 또는 Settings 창에서 **Auto Start** 활성화 (기본값: 활성화)

서버가 시작되면 콘솔에 다음 메시지가 표시됩니다:
```
[Unity MCP] Server started on http://localhost:6850/
```

### Claude Code에서 사용

Claude Code에서 다음과 같이 Unity를 제어할 수 있습니다:

```
# unityflow로 prefab 수정 후 Unity에 반영
unityflow set Player.prefab --path "/Player/m_Name" --value "NewPlayer"
→ unity_refresh 도구 자동 호출

# 컴파일 에러 확인
"컴파일 에러가 있는지 확인해줘"
→ unity_compile_status 도구 호출

# 콘솔 로그 확인
"최근 에러 로그를 보여줘"
→ unity_logs(level="error") 도구 호출
```

## 사용 가능한 도구

| 도구 | 설명 |
|------|------|
| `unity_status` | Unity Editor 상태 조회 |
| `unity_refresh` | AssetDatabase.Refresh() 호출 |
| `unity_logs` | 콘솔 로그 조회 (count, level 파라미터) |
| `unity_clear_logs` | 로그 버퍼 클리어 |
| `unity_compile_status` | 컴파일 상태 및 에러 확인 |
| `unity_play` | Play 모드 시작 |
| `unity_stop` | Play 모드 종료 |
| `unity_pause` | 일시정지 토글 |
| `unity_ping` | 에셋 하이라이트 및 선택 |
| `unity_selection` | 현재 선택된 오브젝트 조회 |
| `unity_project_path` | 프로젝트 경로 조회 |
| `unity_current_scene` | 현재 씬 정보 조회 |

## 설정

Unity Editor에서 **Tools > Unity MCP > Settings** 메뉴로 설정 창을 열 수 있습니다:

- **Port**: HTTP 서버 포트 (기본값: 6850)
- **Auto Start**: 에디터 시작 시 자동으로 서버 시작

## HTTP API

MCP 서버 없이 직접 HTTP API를 사용할 수도 있습니다:

```bash
# 상태 확인
curl http://localhost:6850/status

# 에셋 새로고침
curl -X POST http://localhost:6850/refresh

# 로그 조회
curl "http://localhost:6850/logs?count=50&level=error"

# 컴파일 상태
curl http://localhost:6850/compile/status

# Play 모드
curl -X POST http://localhost:6850/play
curl -X POST http://localhost:6850/stop

# 에셋 하이라이트
curl "http://localhost:6850/ping?path=Assets/Prefabs/Player.prefab"
```

## 워크플로우 예시

### unityflow + Unity MCP 통합 워크플로우

```
1. Claude: "Player prefab에 새로운 컴포넌트를 추가할게요"

2. Claude: unityflow add-component Player.prefab --target "/Player" \
           --component BoxCollider --set "m_Size: {x: 1, y: 2, z: 1}"

3. Claude: unity_refresh (자동 호출)
   → Unity가 변경사항 인식

4. Claude: unity_compile_status
   → 컴파일 에러 없음 확인

5. Claude: unity_ping(path="Assets/Prefabs/Player.prefab")
   → Unity에서 해당 에셋 하이라이트

6. Claude: "완료되었습니다. Unity에서 Player prefab을 확인해보세요."
```

## 문제 해결

### 서버에 연결할 수 없음

1. Unity Editor가 실행 중인지 확인
2. **Tools > Unity MCP > Start Server**로 서버 시작
3. 방화벽이 localhost:6850을 차단하지 않는지 확인

### 포트 충돌

다른 프로그램이 6850 포트를 사용 중이면 Settings에서 포트 변경:
1. **Tools > Unity MCP > Settings**
2. Port 값 변경 (예: 6851)
3. 서버 재시작

Claude Code MCP 설정도 업데이트해야 합니다.

## 라이선스

MIT License
