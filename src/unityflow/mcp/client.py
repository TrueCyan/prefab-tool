"""Unity HTTP 클라이언트.

Unity Editor에서 실행 중인 HTTP 서버와 통신하는 클라이언트입니다.
"""

import json
import urllib.request
import urllib.error
import urllib.parse
from typing import Any, Optional


class UnityClient:
    """Unity MCP HTTP 서버와 통신하는 클라이언트."""

    def __init__(self, host: str = "localhost", port: int = 6850, timeout: float = 5.0):
        """
        Args:
            host: Unity MCP 서버 호스트
            port: Unity MCP 서버 포트
            timeout: 요청 타임아웃 (초)
        """
        self.base_url = f"http://{host}:{port}"
        self.timeout = timeout

    def _request(
        self, path: str, method: str = "GET", params: Optional[dict] = None
    ) -> dict[str, Any]:
        """HTTP 요청을 보내고 JSON 응답을 반환합니다.

        Args:
            path: API 경로 (예: "/refresh")
            method: HTTP 메서드
            params: 쿼리 파라미터 (GET) 또는 바디 (POST)

        Returns:
            JSON 응답 딕셔너리

        Raises:
            ConnectionError: Unity 서버에 연결할 수 없을 때
            RuntimeError: 요청 실패 시
        """
        url = self.base_url + path

        if method == "GET" and params:
            url += "?" + urllib.parse.urlencode(params)

        req = urllib.request.Request(url, method=method)
        req.add_header("Content-Type", "application/json")

        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as response:
                data = response.read().decode("utf-8")
                return json.loads(data)
        except urllib.error.URLError as e:
            if isinstance(e.reason, ConnectionRefusedError):
                raise ConnectionError(
                    f"Unity MCP 서버에 연결할 수 없습니다. "
                    f"Unity Editor에서 서버가 실행 중인지 확인하세요. ({self.base_url})"
                ) from e
            raise RuntimeError(f"요청 실패: {e}") from e
        except json.JSONDecodeError as e:
            raise RuntimeError(f"응답 파싱 실패: {e}") from e

    def get_status(self) -> dict[str, Any]:
        """서버 상태를 조회합니다."""
        return self._request("/status")

    def refresh(self) -> dict[str, Any]:
        """AssetDatabase.Refresh()를 호출합니다."""
        return self._request("/refresh", method="POST")

    def get_logs(
        self, count: int = 100, level: Optional[str] = None
    ) -> dict[str, Any]:
        """콘솔 로그를 조회합니다.

        Args:
            count: 가져올 로그 수
            level: 필터링할 로그 레벨 (error, warning, log)
        """
        params = {"count": count}
        if level:
            params["level"] = level
        return self._request("/logs", params=params)

    def clear_logs(self) -> dict[str, Any]:
        """로그 버퍼를 클리어합니다."""
        return self._request("/logs/clear", method="POST")

    def get_compile_status(self) -> dict[str, Any]:
        """컴파일 상태를 조회합니다."""
        return self._request("/compile/status")

    def play(self) -> dict[str, Any]:
        """Play 모드를 시작합니다."""
        return self._request("/play", method="POST")

    def stop(self) -> dict[str, Any]:
        """Play 모드를 종료합니다."""
        return self._request("/stop", method="POST")

    def pause(self) -> dict[str, Any]:
        """일시정지를 토글합니다."""
        return self._request("/pause", method="POST")

    def ping_asset(self, asset_path: str) -> dict[str, Any]:
        """에셋을 하이라이트합니다.

        Args:
            asset_path: Unity 프로젝트 내 에셋 경로 (예: "Assets/Prefabs/Player.prefab")
        """
        return self._request("/ping", params={"path": asset_path})

    def get_selection(self) -> dict[str, Any]:
        """현재 선택된 오브젝트를 조회합니다."""
        return self._request("/selection")

    def get_project_path(self) -> dict[str, Any]:
        """프로젝트 경로를 조회합니다."""
        return self._request("/project/path")

    def get_current_scene(self) -> dict[str, Any]:
        """현재 씬 정보를 조회합니다."""
        return self._request("/scene/current")

    def is_connected(self) -> bool:
        """Unity 서버에 연결되어 있는지 확인합니다."""
        try:
            status = self.get_status()
            return status.get("success", False)
        except (ConnectionError, RuntimeError):
            return False
