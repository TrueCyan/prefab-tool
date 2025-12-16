using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityMCP
{
    /// <summary>
    /// Unity MCP Server - HTTP 서버를 통해 외부에서 Unity Editor를 제어할 수 있게 합니다.
    /// Claude Code와 같은 AI 도구에서 Unity와 상호작용할 수 있도록 합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class UnityMCPServer
    {
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static bool _isRunning;
        private static readonly object _lock = new object();

        private const int DEFAULT_PORT = 6850;
        private const string PREF_KEY_PORT = "UnityMCP_Port";
        private const string PREF_KEY_ENABLED = "UnityMCP_Enabled";

        // 로그 버퍼 (최근 로그 저장)
        private static readonly List<LogEntry> _logBuffer = new List<LogEntry>();
        private const int MAX_LOG_BUFFER_SIZE = 1000;

        // 컴파일 상태
        private static bool _isCompiling;
        private static List<string> _compileErrors = new List<string>();

        static UnityMCPServer()
        {
            // 에디터 시작 시 자동 시작 (설정에 따라)
            if (EditorPrefs.GetBool(PREF_KEY_ENABLED, true))
            {
                EditorApplication.delayCall += () => StartServer();
            }

            // 로그 콜백 등록
            Application.logMessageReceived += OnLogMessageReceived;

            // 컴파일 콜백 등록
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

            // 에디터 종료 시 정리
            EditorApplication.quitting += StopServer;
            AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        }

        public static int Port => EditorPrefs.GetInt(PREF_KEY_PORT, DEFAULT_PORT);
        public static bool IsRunning => _isRunning;

        [MenuItem("Tools/Unity MCP/Start Server")]
        public static void StartServer()
        {
            if (_isRunning)
            {
                Debug.Log("[Unity MCP] Server is already running");
                return;
            }

            try
            {
                int port = Port;
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                _isRunning = true;

                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "UnityMCPServer"
                };
                _listenerThread.Start();

                Debug.Log($"[Unity MCP] Server started on http://localhost:{port}/");
                EditorPrefs.SetBool(PREF_KEY_ENABLED, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Unity MCP] Failed to start server: {ex.Message}");
                _isRunning = false;
            }
        }

        [MenuItem("Tools/Unity MCP/Stop Server")]
        public static void StopServer()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            try
            {
                _listener?.Stop();
                _listener?.Close();
                _listenerThread?.Join(1000);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Unity MCP] Error stopping server: {ex.Message}");
            }

            Debug.Log("[Unity MCP] Server stopped");
        }

        private static void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // 서버 중지 시 발생
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        Debug.LogError($"[Unity MCP] Listener error: {ex.Message}");
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string responseString = "";
            int statusCode = 200;

            try
            {
                string path = request.Url.AbsolutePath.ToLower();
                string method = request.HttpMethod;

                // CORS 헤더 추가
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (method == "OPTIONS")
                {
                    statusCode = 204;
                }
                else
                {
                    switch (path)
                    {
                        case "/":
                        case "/status":
                            responseString = HandleStatus();
                            break;

                        case "/refresh":
                            responseString = HandleRefresh();
                            break;

                        case "/logs":
                            responseString = HandleGetLogs(request);
                            break;

                        case "/logs/clear":
                            responseString = HandleClearLogs();
                            break;

                        case "/compile/status":
                            responseString = HandleCompileStatus();
                            break;

                        case "/play":
                            responseString = HandlePlay();
                            break;

                        case "/stop":
                            responseString = HandleStop();
                            break;

                        case "/pause":
                            responseString = HandlePause();
                            break;

                        case "/ping":
                            responseString = HandlePing(request);
                            break;

                        case "/selection":
                            responseString = HandleGetSelection();
                            break;

                        case "/project/path":
                            responseString = HandleGetProjectPath();
                            break;

                        case "/scene/current":
                            responseString = HandleGetCurrentScene();
                            break;

                        default:
                            statusCode = 404;
                            responseString = JsonResponse(false, $"Unknown endpoint: {path}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                statusCode = 500;
                responseString = JsonResponse(false, $"Error: {ex.Message}");
            }

            // 응답 전송
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        #region Request Handlers

        private static string HandleStatus()
        {
            var status = new Dictionary<string, object>
            {
                ["success"] = true,
                ["server"] = "Unity MCP Server",
                ["version"] = "1.0.0",
                ["unityVersion"] = Application.unityVersion,
                ["projectName"] = Application.productName,
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPaused"] = EditorApplication.isPaused,
                ["isCompiling"] = _isCompiling || EditorApplication.isCompiling
            };
            return JsonSerialize(status);
        }

        private static string HandleRefresh()
        {
            // 메인 스레드에서 실행해야 함
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
            };
            return JsonResponse(true, "AssetDatabase.Refresh() queued");
        }

        private static string HandleGetLogs(HttpListenerRequest request)
        {
            int count = 100;
            string levelFilter = null;

            // 쿼리 파라미터 파싱
            var query = request.QueryString;
            if (query["count"] != null)
                int.TryParse(query["count"], out count);
            if (query["level"] != null)
                levelFilter = query["level"];

            List<object> logs;
            lock (_logBuffer)
            {
                var filtered = _logBuffer.AsReadOnly();
                if (!string.IsNullOrEmpty(levelFilter))
                {
                    var levelEnum = ParseLogType(levelFilter);
                    filtered = new List<LogEntry>(_logBuffer.FindAll(l => l.Type == levelEnum)).AsReadOnly();
                }

                int startIndex = Math.Max(0, filtered.Count - count);
                logs = new List<object>();
                for (int i = startIndex; i < filtered.Count; i++)
                {
                    logs.Add(new Dictionary<string, object>
                    {
                        ["message"] = filtered[i].Message,
                        ["stackTrace"] = filtered[i].StackTrace,
                        ["type"] = filtered[i].Type.ToString(),
                        ["timestamp"] = filtered[i].Timestamp.ToString("o")
                    });
                }
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                ["success"] = true,
                ["count"] = logs.Count,
                ["logs"] = logs
            });
        }

        private static string HandleClearLogs()
        {
            lock (_logBuffer)
            {
                _logBuffer.Clear();
            }
            return JsonResponse(true, "Logs cleared");
        }

        private static string HandleCompileStatus()
        {
            var status = new Dictionary<string, object>
            {
                ["success"] = true,
                ["isCompiling"] = _isCompiling || EditorApplication.isCompiling,
                ["hasErrors"] = _compileErrors.Count > 0,
                ["errors"] = _compileErrors
            };
            return JsonSerialize(status);
        }

        private static string HandlePlay()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlaying)
                    EditorApplication.isPlaying = true;
            };
            return JsonResponse(true, "Play mode requested");
        }

        private static string HandleStop()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
            };
            return JsonResponse(true, "Stop requested");
        }

        private static string HandlePause()
        {
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPaused = !EditorApplication.isPaused;
            };
            return JsonResponse(true, $"Pause toggled (now: {!EditorApplication.isPaused})");
        }

        private static string HandlePing(HttpListenerRequest request)
        {
            string assetPath = request.QueryString["path"];
            if (string.IsNullOrEmpty(assetPath))
            {
                return JsonResponse(false, "Missing 'path' parameter");
            }

            EditorApplication.delayCall += () =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            };
            return JsonResponse(true, $"Pinged asset: {assetPath}");
        }

        private static string HandleGetSelection()
        {
            var selection = new List<string>();
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                    selection.Add(path);
                else if (obj is GameObject go)
                    selection.Add(GetGameObjectPath(go));
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                ["success"] = true,
                ["count"] = selection.Count,
                ["selection"] = selection
            });
        }

        private static string HandleGetProjectPath()
        {
            return JsonSerialize(new Dictionary<string, object>
            {
                ["success"] = true,
                ["projectPath"] = Path.GetDirectoryName(Application.dataPath),
                ["assetsPath"] = Application.dataPath
            });
        }

        private static string HandleGetCurrentScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return JsonSerialize(new Dictionary<string, object>
            {
                ["success"] = true,
                ["name"] = scene.name,
                ["path"] = scene.path,
                ["isDirty"] = scene.isDirty,
                ["isLoaded"] = scene.isLoaded
            });
        }

        #endregion

        #region Event Handlers

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            lock (_logBuffer)
            {
                _logBuffer.Add(new LogEntry
                {
                    Message = message,
                    StackTrace = stackTrace,
                    Type = type,
                    Timestamp = DateTime.Now
                });

                // 버퍼 크기 제한
                while (_logBuffer.Count > MAX_LOG_BUFFER_SIZE)
                {
                    _logBuffer.RemoveAt(0);
                }
            }
        }

        private static void OnCompilationStarted(object obj)
        {
            _isCompiling = true;
            _compileErrors.Clear();
        }

        private static void OnCompilationFinished(object obj)
        {
            _isCompiling = false;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var msg in messages)
            {
                if (msg.type == CompilerMessageType.Error)
                {
                    _compileErrors.Add($"{msg.file}({msg.line},{msg.column}): {msg.message}");
                }
            }
        }

        #endregion

        #region Utilities

        private static string JsonResponse(bool success, string message)
        {
            return JsonSerialize(new Dictionary<string, object>
            {
                ["success"] = success,
                ["message"] = message
            });
        }

        private static string JsonSerialize(Dictionary<string, object> data)
        {
            // 간단한 JSON 직렬화 (Unity의 JsonUtility는 Dictionary를 지원하지 않음)
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kvp in data)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"\"{kvp.Key}\":");
                sb.Append(SerializeValue(kvp.Value));
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string SerializeValue(object value)
        {
            if (value == null) return "null";
            if (value is bool b) return b ? "true" : "false";
            if (value is int || value is long || value is float || value is double)
                return value.ToString();
            if (value is string s)
                return $"\"{EscapeJson(s)}\"";
            if (value is List<string> list)
            {
                var items = new List<string>();
                foreach (var item in list)
                    items.Add($"\"{EscapeJson(item)}\"");
                return $"[{string.Join(",", items)}]";
            }
            if (value is List<object> objList)
            {
                var items = new List<string>();
                foreach (var item in objList)
                {
                    if (item is Dictionary<string, object> dict)
                        items.Add(JsonSerialize(dict));
                    else
                        items.Add(SerializeValue(item));
                }
                return $"[{string.Join(",", items)}]";
            }
            return $"\"{EscapeJson(value.ToString())}\"";
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static LogType ParseLogType(string level)
        {
            switch (level.ToLower())
            {
                case "error": return LogType.Error;
                case "warning": return LogType.Warning;
                case "log": return LogType.Log;
                case "exception": return LogType.Exception;
                case "assert": return LogType.Assert;
                default: return LogType.Log;
            }
        }

        #endregion

        private class LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public DateTime Timestamp;
        }
    }

    /// <summary>
    /// Unity MCP 서버 설정 윈도우
    /// </summary>
    public class UnityMCPSettingsWindow : EditorWindow
    {
        private int _port;
        private bool _autoStart;

        [MenuItem("Tools/Unity MCP/Settings")]
        public static void ShowWindow()
        {
            GetWindow<UnityMCPSettingsWindow>("Unity MCP Settings");
        }

        private void OnEnable()
        {
            _port = EditorPrefs.GetInt("UnityMCP_Port", 6850);
            _autoStart = EditorPrefs.GetBool("UnityMCP_Enabled", true);
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity MCP Server Settings", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            // 상태 표시
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Status:");
            GUILayout.Label(UnityMCPServer.IsRunning ? "Running" : "Stopped",
                UnityMCPServer.IsRunning ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 포트 설정
            int newPort = EditorGUILayout.IntField("Port", _port);
            if (newPort != _port)
            {
                _port = newPort;
                EditorPrefs.SetInt("UnityMCP_Port", _port);
            }

            // 자동 시작 설정
            bool newAutoStart = EditorGUILayout.Toggle("Auto Start", _autoStart);
            if (newAutoStart != _autoStart)
            {
                _autoStart = newAutoStart;
                EditorPrefs.SetBool("UnityMCP_Enabled", _autoStart);
            }

            EditorGUILayout.Space();

            // 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(UnityMCPServer.IsRunning ? "Stop Server" : "Start Server"))
            {
                if (UnityMCPServer.IsRunning)
                    UnityMCPServer.StopServer();
                else
                    UnityMCPServer.StartServer();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // API 엔드포인트 도움말
            EditorGUILayout.LabelField("API Endpoints", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "GET  /status         - 서버 상태\n" +
                "POST /refresh        - AssetDatabase.Refresh()\n" +
                "GET  /logs           - 콘솔 로그 조회 (?count=N&level=error|warning|log)\n" +
                "POST /logs/clear     - 로그 버퍼 클리어\n" +
                "GET  /compile/status - 컴파일 상태 및 에러\n" +
                "POST /play           - Play 모드 시작\n" +
                "POST /stop           - Play 모드 종료\n" +
                "POST /pause          - 일시정지 토글\n" +
                "GET  /ping?path=...  - 에셋 하이라이트\n" +
                "GET  /selection      - 현재 선택된 오브젝트\n" +
                "GET  /project/path   - 프로젝트 경로\n" +
                "GET  /scene/current  - 현재 씬 정보",
                MessageType.Info);
        }
    }
}
