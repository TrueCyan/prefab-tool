"""Unity MCP Server for Claude Code integration.

이 모듈은 Unity Editor와 Claude Code 사이의 브릿지 역할을 하는 MCP 서버를 제공합니다.
Unity Editor에서 실행 중인 HTTP 서버와 통신하여 다음 기능을 수행합니다:

- AssetDatabase.Refresh() 호출
- 콘솔 로그 조회
- 컴파일 상태 확인
- Play 모드 제어
- 에셋 선택 및 하이라이트
"""

from unityflow.mcp.server import main

__all__ = ["main"]
