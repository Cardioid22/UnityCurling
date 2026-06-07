using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Curling.Core;

namespace Curling.Match
{
    /// <summary>
    /// 盤面状況を Ollama で実況テキスト化し、VOICEVOX(ずんだもん)で読み上げる。
    /// MatchManager が PerformShot 完了ごとに Comment() を呼ぶ。
    /// 接続失敗・タイムアウト・不正レスポンスはすべてログのみで握りつぶし、ゲームは止めない。
    ///
    /// 遅延対策:
    ///  - 起動時ウォームアップ（モデル常駐 + 話者初期化）でコールドスタートのスパイクを除去。
    ///  - keep_alive でモデルを VRAM に常駐。
    ///  - num_predict と maxChars で出力を短くし、文字数比例の VOICEVOX 合成時間を抑制。
    ///  - VOICEVOX は voicevoxUrl(優先, 例: GPU機) → voicevoxFallbackUrl(例: ローカルCPU) を自動選択。
    ///  - 各段階の所要時間を Console にログ出力（実数を見て次の手を判断するため）。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CommentaryService : MonoBehaviour
    {
        LlmConfig _config;
        AudioSource _audio;
        string _voicevoxUrl;     // ウォームアップで解決した有効な VOICEVOX URL
        Coroutine _runCo;        // 進行中の実況コルーチン（force 中断用）
        bool _busy;
        bool _runtimeEnabled = true;
        public KeyCode toggleKey = KeyCode.V;

        bool Active => _config != null && _config.enabled && _runtimeEnabled;

        void Awake()
        {
            _config = LlmConfig.Load();
            _voicevoxUrl = _config.voicevoxUrl.TrimEnd('/');
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D（実況なので定位させない）
        }

        IEnumerator Start()
        {
            if (_config != null && _config.enabled && _config.warmupOnStart)
                yield return Warmup();
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                _runtimeEnabled = !_runtimeEnabled;
                Debug.Log($"[Commentary] 実況 {(_runtimeEnabled ? "ON" : "OFF")}（{toggleKey} で切替）");
            }
        }

        void OnGUI()
        {
            string state = !(_config != null && _config.enabled)
                ? "<color=#888888>無効(設定)</color>"
                : _runtimeEnabled ? "<color=#7fff7f>ON</color>" : "<color=#ff7f7f>OFF</color>";
            var style = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft, richText = true };
            GUI.Box(new Rect(12, 12, 220, 24), $"実況(ずんだもん): {state}  [{toggleKey}]", style);
        }

        /// <summary>
        /// 呼び出し時点の状態を即座に文字列化して実況を開始する。
        /// force=true のときは進行中の実況を中断して差し替える（試合終了の勝者コメント等に使用）。
        /// </summary>
        public void Comment(MatchState state, ShotInfo shot, bool force = false)
        {
            if (!Active) return;
            if (state == null || state.current_end == null) return;
            if (_busy && !force) return;
            if (_busy && force) { if (_runCo != null) StopCoroutine(_runCo); _busy = false; }

            // 状態は呼び出し時点で確定文字列化する（コルーチン側で後から読むと盤面が変わって競合するため）。
            string description = CurlingDescriber.Describe(state, shot);
            _busy = true;
            _runCo = StartCoroutine(Run(description));
        }

        IEnumerator Run(string description)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            var sw = new System.Diagnostics.Stopwatch();
            try
            {
                string text = null;
                sw.Restart();
                yield return RequestCommentary(description, t => text = t);
                long tLlm = sw.ElapsedMilliseconds;
                if (string.IsNullOrEmpty(text)) yield break;

                if (text.Length > _config.maxChars) text = text.Substring(0, _config.maxChars);
                Debug.Log($"[Commentary] ずんだもん: {text}");

                byte[] wav = null;
                sw.Restart();
                yield return Synthesize(text, w => wav = w);
                long tTts = sw.ElapsedMilliseconds;
                if (wav == null || wav.Length == 0) yield break;

                AudioClip clip = null;
                sw.Restart();
                yield return DecodeWav(wav, c => clip = c);
                long tDecode = sw.ElapsedMilliseconds;
                if (clip == null) yield break;

                _audio.Stop();
                _audio.clip = clip;
                _audio.Play();

                Debug.Log($"[Commentary] 所要: LLM={tLlm}ms  TTS={tTts}ms  decode={tDecode}ms  合計={swTotal.ElapsedMilliseconds}ms");
            }
            finally
            {
                _busy = false;
            }
        }

        // ---- ウォームアップ（起動時に1回） --------------------------------------

        IEnumerator Warmup()
        {
            Debug.Log("[Commentary] ウォームアップ開始…");
            yield return ResolveAndInitVoicevox();
            yield return WarmOllama();
            Debug.Log("[Commentary] ウォームアップ完了");
        }

        IEnumerator ResolveAndInitVoicevox()
        {
            string[] candidates = string.IsNullOrEmpty(_config.voicevoxFallbackUrl)
                ? new[] { _config.voicevoxUrl }
                : new[] { _config.voicevoxUrl, _config.voicevoxFallbackUrl };

            foreach (var raw in candidates)
            {
                string url = raw.TrimEnd('/');
                bool ok = false;
                yield return ProbeGet(url + "/version", r => ok = r);
                if (ok)
                {
                    _voicevoxUrl = url;
                    Debug.Log($"[Commentary] VOICEVOX 使用: {_voicevoxUrl}");
                    yield return InitSpeaker(_voicevoxUrl, _config.voicevoxSpeaker);
                    yield break;
                }
                Debug.LogWarning($"[Commentary] VOICEVOX 不通: {url}");
            }
            // どれも不通でも既定を保持（実際の合成時にまた試みる）。
            _voicevoxUrl = _config.voicevoxUrl.TrimEnd('/');
        }

        IEnumerator ProbeGet(string url, Action<bool> onResult)
        {
            using (var www = UnityWebRequest.Get(url))
            {
                www.timeout = 5;
                yield return www.SendWebRequest();
                onResult(www.result == UnityWebRequest.Result.Success);
            }
        }

        IEnumerator InitSpeaker(string baseUrl, int speaker)
        {
            using (var www = new UnityWebRequest($"{baseUrl}/initialize_speaker?speaker={speaker}", "POST"))
            {
                www.downloadHandler = new DownloadHandlerBuffer();
                www.timeout = 60; // 話者初期化は重い場合がある
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                    Debug.Log("[Commentary] VOICEVOX 話者初期化 完了");
                else
                    Debug.LogWarning($"[Commentary] VOICEVOX 話者初期化 失敗: {www.error}");
            }
        }

        IEnumerator WarmOllama()
        {
            string url = _config.ollamaUrl.TrimEnd('/') + "/api/chat";
            var req = new OllamaChatRequest
            {
                model = _config.ollamaModel,
                stream = false,
                format = "json",
                keep_alive = _config.keepAlive,
                options = new OllamaOptions { temperature = 0f, num_predict = 1 },
                messages = new[] { new OllamaReqMessage { role = "user", content = "ok" } },
            };
            string body = JsonUtility.ToJson(req);
            using (var www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 120; // 初回モデルロードは時間がかかる
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                    Debug.Log("[Commentary] Ollama モデル常駐 完了");
                else
                    Debug.LogWarning($"[Commentary] Ollama ウォームアップ失敗: {www.error}");
            }
        }

        // ---- 1) Ollama /api/chat で実況テキストを生成 -------------------------------

        IEnumerator RequestCommentary(string description, Action<string> onResult)
        {
            string url = _config.ollamaUrl.TrimEnd('/') + "/api/chat";
            string system =
                "あなたは「カーリング」の試合を盛り上げる実況・解説者「ずんだもん」です。\n" +
                "一人称は「ぼく」、語尾は「〜なのだ」「〜のだ」。明るく短く、1文で実況します。\n" +
                "直前の一投と現在の盤面の見どころ（ナンバーワン、ガード、テイクアウト、得点）を端的に語ること。\n" +
                "勝敗が決まった瞬間は勝者を讃えること。\n" +
                $"出力は厳密に {{ \"text\": \"実況本文（全角{_config.maxChars}文字以内）\" }} の JSON のみ。前置き・他キー・改行は禁止。";

            var req = new OllamaChatRequest
            {
                model = _config.ollamaModel,
                stream = false,
                format = "json",
                keep_alive = _config.keepAlive,
                options = new OllamaOptions { temperature = _config.temperature, num_predict = _config.numPredict },
                messages = new[]
                {
                    new OllamaReqMessage { role = "system", content = system },
                    new OllamaReqMessage { role = "user", content = "現在のゲーム状況:\n" + description },
                },
            };
            string body = JsonUtility.ToJson(req);

            using (var www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = Mathf.Max(1, Mathf.CeilToInt(_config.requestTimeoutSec));

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Commentary] Ollama 接続失敗 ({url}): {www.error}");
                    yield break;
                }
                onResult(ParseCommentary(www.downloadHandler.text));
            }
        }

        static string ParseCommentary(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody)) return null;
            try
            {
                var res = JsonUtility.FromJson<OllamaChatResponse>(responseBody);
                string content = res != null && res.message != null ? res.message.content : null;
                if (string.IsNullOrEmpty(content)) return null;

                // format:"json" 指定なので content は {"text":"..."} の文字列のはず。
                try
                {
                    var parsed = JsonUtility.FromJson<CommentaryText>(content);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.text)) return parsed.text.Trim();
                }
                catch { /* JSON で包まれていない場合は下のフォールバックへ */ }

                return content.Trim();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commentary] Ollama 応答の解析に失敗: {e.Message}");
                return null;
            }
        }

        // ---- 2) VOICEVOX audio_query → synthesis で WAV を取得 ----------------------

        IEnumerator Synthesize(string text, Action<byte[]> onResult)
        {
            string baseUrl = string.IsNullOrEmpty(_voicevoxUrl) ? _config.voicevoxUrl.TrimEnd('/') : _voicevoxUrl;
            int speaker = _config.voicevoxSpeaker;
            int timeout = Mathf.Max(1, Mathf.CeilToInt(_config.requestTimeoutSec));

            // 2-1) audio_query（POST・クエリパラメータ・ボディ無し）
            string queryUrl = $"{baseUrl}/audio_query?text={UnityWebRequest.EscapeURL(text)}&speaker={speaker}";
            string queryJson = null;
            using (var q = new UnityWebRequest(queryUrl, "POST"))
            {
                q.downloadHandler = new DownloadHandlerBuffer();
                q.timeout = timeout;
                yield return q.SendWebRequest();
                if (q.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Commentary] VOICEVOX audio_query 失敗 ({baseUrl}): {q.error}");
                    yield break;
                }
                queryJson = q.downloadHandler.text;
            }
            if (string.IsNullOrEmpty(queryJson)) yield break;

            // 2-2) synthesis（POST・ボディは audio_query の結果 JSON・WAV を返す）
            string synthUrl = $"{baseUrl}/synthesis?speaker={speaker}";
            using (var s = new UnityWebRequest(synthUrl, "POST"))
            {
                s.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(queryJson));
                s.downloadHandler = new DownloadHandlerBuffer();
                s.SetRequestHeader("Content-Type", "application/json");
                s.SetRequestHeader("Accept", "audio/wav");
                s.timeout = timeout;
                yield return s.SendWebRequest();
                if (s.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Commentary] VOICEVOX synthesis 失敗: {s.error}");
                    yield break;
                }
                onResult(s.downloadHandler.data);
            }
        }

        // ---- 3) WAV bytes → AudioClip --------------------------------------------

        IEnumerator DecodeWav(byte[] wav, Action<AudioClip> onResult)
        {
            // 一時ファイルへ書き出し、Unity の WAV デコーダで AudioClip 化（手書きパーサより堅牢）。
            string path = Path.Combine(Application.temporaryCachePath, "zundamon_commentary.wav");
            string uri;
            try
            {
                File.WriteAllBytes(path, wav);
                uri = new Uri(path).AbsoluteUri; // file:///C:/... 形式
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commentary] WAV 一時保存に失敗: {e.Message}");
                yield break;
            }

            using (var www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
            {
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Commentary] WAV デコード失敗: {www.error}");
                    yield break;
                }
                onResult(DownloadHandlerAudioClip.GetContent(www));
            }
        }

        // ---- JSON DTO（JsonUtility 用） ------------------------------------------

        [Serializable] class OllamaChatRequest
        {
            public string model;
            public OllamaReqMessage[] messages;
            public bool stream;
            public string format;
            public string keep_alive;
            public OllamaOptions options;
        }

        [Serializable] class OllamaReqMessage { public string role; public string content; }
        [Serializable] class OllamaOptions { public float temperature; public int num_predict; }
        [Serializable] class OllamaChatResponse { public OllamaMessage message; }
        [Serializable] class OllamaMessage { public string content; }
        [Serializable] class CommentaryText { public string text; }
    }
}
