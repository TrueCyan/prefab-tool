#!/usr/bin/env python3
"""Unity MCP Server.

Claude Code와 같은 MCP 클라이언트에서 사용할 수 있는 Unity 통합 서버입니다.
JSON-RPC 2.0 over stdio 프로토콜을 사용합니다.

실행:
    python -m unityflow.mcp
"""

import json
import sys
from typing import Any

from unityflow.mcp.client import UnityClient


# MCP 프로토콜 버전
PROTOCOL_VERSION = "2024-11-05"

# 서버 정보
SERVER_NAME = "unity-mcp"
SERVER_VERSION = "1.0.0"

# Unity 클라이언트
_unity_client: UnityClient | None = None


def get_unity_client() -> UnityClient:
    """Unity 클라이언트 인스턴스를 반환합니다."""
    global _unity_client
    if _unity_client is None:
        _unity_client = UnityClient()
    return _unity_client


# === Tool Definitions ===

TOOLS = [
    {
        "name": "unity_status",
        "description": "Unity Editor 상태를 조회합니다. Unity 버전, 프로젝트 이름, Play 모드 상태, 컴파일 상태 등을 확인할 수 있습니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_refresh",
        "description": "Unity AssetDatabase.Refresh()를 호출합니다. 파일 시스템에서 변경된 에셋을 Unity가 인식하도록 합니다. unityflow로 파일을 수정한 후 이 도구를 호출하세요.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_logs",
        "description": "Unity 콘솔 로그를 조회합니다. 에러, 경고, 일반 로그를 확인할 수 있습니다.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "count": {
                    "type": "integer",
                    "description": "가져올 로그 수 (기본값: 50)",
                    "default": 50,
                },
                "level": {
                    "type": "string",
                    "description": "필터링할 로그 레벨",
                    "enum": ["error", "warning", "log"],
                },
            },
            "required": [],
        },
    },
    {
        "name": "unity_clear_logs",
        "description": "Unity 로그 버퍼를 클리어합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_compile_status",
        "description": "Unity 컴파일 상태를 확인합니다. 컴파일 중인지, 에러가 있는지 확인할 수 있습니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_play",
        "description": "Unity Play 모드를 시작합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_stop",
        "description": "Unity Play 모드를 종료합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_pause",
        "description": "Unity Play 모드 일시정지를 토글합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_ping",
        "description": "Unity에서 특정 에셋을 하이라이트하고 선택합니다. 사용자에게 작업 결과를 보여줄 때 유용합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "에셋 경로 (예: 'Assets/Prefabs/Player.prefab')",
                },
            },
            "required": ["path"],
        },
    },
    {
        "name": "unity_selection",
        "description": "Unity Editor에서 현재 선택된 오브젝트 목록을 조회합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_project_path",
        "description": "Unity 프로젝트 경로를 조회합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
    {
        "name": "unity_current_scene",
        "description": "현재 열려 있는 씬 정보를 조회합니다.",
        "inputSchema": {
            "type": "object",
            "properties": {},
            "required": [],
        },
    },
]


def call_tool(name: str, arguments: dict[str, Any]) -> dict[str, Any]:
    """도구를 실행하고 결과를 반환합니다."""
    client = get_unity_client()

    try:
        if name == "unity_status":
            result = client.get_status()
        elif name == "unity_refresh":
            result = client.refresh()
        elif name == "unity_logs":
            count = arguments.get("count", 50)
            level = arguments.get("level")
            result = client.get_logs(count=count, level=level)
        elif name == "unity_clear_logs":
            result = client.clear_logs()
        elif name == "unity_compile_status":
            result = client.get_compile_status()
        elif name == "unity_play":
            result = client.play()
        elif name == "unity_stop":
            result = client.stop()
        elif name == "unity_pause":
            result = client.pause()
        elif name == "unity_ping":
            path = arguments.get("path", "")
            result = client.ping_asset(path)
        elif name == "unity_selection":
            result = client.get_selection()
        elif name == "unity_project_path":
            result = client.get_project_path()
        elif name == "unity_current_scene":
            result = client.get_current_scene()
        else:
            return {"error": f"Unknown tool: {name}"}

        return {"content": [{"type": "text", "text": json.dumps(result, indent=2, ensure_ascii=False)}]}

    except ConnectionError as e:
        return {
            "content": [
                {
                    "type": "text",
                    "text": f"Unity 연결 실패: {e}\n\n"
                    "Unity Editor에서 Tools > Unity MCP > Start Server를 실행했는지 확인하세요.",
                }
            ],
            "isError": True,
        }
    except Exception as e:
        return {
            "content": [{"type": "text", "text": f"오류 발생: {e}"}],
            "isError": True,
        }


# === JSON-RPC Handler ===


def handle_request(request: dict[str, Any]) -> dict[str, Any]:
    """JSON-RPC 요청을 처리합니다."""
    method = request.get("method", "")
    params = request.get("params", {})
    request_id = request.get("id")

    result: Any = None
    error: dict[str, Any] | None = None

    try:
        if method == "initialize":
            result = {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {
                    "tools": {},
                },
                "serverInfo": {
                    "name": SERVER_NAME,
                    "version": SERVER_VERSION,
                },
            }
        elif method == "notifications/initialized":
            # 알림 - 응답 없음
            return None  # type: ignore
        elif method == "tools/list":
            result = {"tools": TOOLS}
        elif method == "tools/call":
            tool_name = params.get("name", "")
            arguments = params.get("arguments", {})
            result = call_tool(tool_name, arguments)
        elif method == "ping":
            result = {}
        else:
            error = {
                "code": -32601,
                "message": f"Method not found: {method}",
            }
    except Exception as e:
        error = {
            "code": -32603,
            "message": str(e),
        }

    # 응답 생성
    response: dict[str, Any] = {"jsonrpc": "2.0"}

    if request_id is not None:
        response["id"] = request_id

    if error:
        response["error"] = error
    else:
        response["result"] = result

    return response


def read_message() -> dict[str, Any] | None:
    """stdin에서 JSON-RPC 메시지를 읽습니다."""
    try:
        line = sys.stdin.readline()
        if not line:
            return None
        return json.loads(line)
    except json.JSONDecodeError:
        return None


def write_message(message: dict[str, Any]) -> None:
    """stdout으로 JSON-RPC 메시지를 씁니다."""
    sys.stdout.write(json.dumps(message) + "\n")
    sys.stdout.flush()


def main() -> None:
    """MCP 서버 메인 루프."""
    # stderr로 로그 출력 (stdout은 JSON-RPC용)
    sys.stderr.write(f"[{SERVER_NAME}] Starting Unity MCP Server v{SERVER_VERSION}\n")
    sys.stderr.flush()

    while True:
        request = read_message()
        if request is None:
            break

        response = handle_request(request)
        if response is not None:
            write_message(response)


if __name__ == "__main__":
    main()
