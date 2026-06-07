using System;
using System.IO;
using UnityEngine;

namespace Curling.Match
{
    /// <summary>
    /// Ollama / VOICEVOX への接続設定。
    /// StreamingAssets/llm_config.json から読み込む（ビルド後も現地で編集可能）。
    /// ファイルが無い / 壊れている場合は、ここで定義した既定値で動作する。
    /// </summary>
    [Serializable]
    public class LlmConfig
    {
        public const string FileName = "llm_config.json";

        // 実況機能全体の ON/OFF。
        public bool enabled = true;

        // Ollama（研究室サーバーへポートフォワードした先）。
        public string ollamaUrl = "http://localhost:11434";
        public string ollamaModel = "gemma4";
        public float temperature = 0.7f;
        // 生成トークン上限。実況は短いので少なめで十分（速度に効く）。
        public int numPredict = 64;
        // モデルをVRAMに常駐させる時間。"30m" や "-1"(無期限)。コールドロード回避。
        public string keepAlive = "30m";

        // VOICEVOX エンジン。speaker=3 はずんだもん（ノーマル）。
        // voicevoxUrl を優先（例: 研究室GPU機）。不通なら voicevoxFallbackUrl（例: ローカルCPU）へ自動切替。
        public string voicevoxUrl = "http://localhost:50021";
        public string voicevoxFallbackUrl = "";
        public int voicevoxSpeaker = 3;

        // 実況本文の目安最大文字数。VOICEVOX合成時間は文字数にほぼ比例するため上限を設ける。
        public int maxChars = 30;

        // 起動時にモデル常駐＋話者初期化のウォームアップを行うか（初回の遅延スパイク除去）。
        public bool warmupOnStart = true;

        // 1リクエストあたりのタイムアウト（秒）。展示でゲームを止めないため短め。
        public float requestTimeoutSec = 15f;

        public static LlmConfig Load()
        {
            var cfg = new LlmConfig();
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, FileName);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    // 既定値インスタンスに上書き → JSON に無いキーは既定値のまま残る。
                    JsonUtility.FromJsonOverwrite(json, cfg);
                    Debug.Log($"[Commentary] 設定読込: {path}");
                }
                else
                {
                    Debug.Log($"[Commentary] {path} が無いため既定設定を使用");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commentary] 設定読込に失敗（既定設定で続行）: {e.Message}");
            }
            return cfg;
        }
    }
}
