# UnityCurling

DigitalCurling4 レギュレーション準拠（Standard のみ、Mix Doubles 非対応）の Unity スタンドアロン版デジタルカーリング。人間 vs CPU。

研究室 `ylab-curling` (DC3 ベース MCTS) を題材に作成。ただし通信は行わず単体動作する。

## セットアップ手順（初回）

1. **Unity Hub** で `Add Project from disk` → このリポジトリの `UnityCurling` フォルダを選択。
2. Unity バージョンは **Unity 6 LTS (6000.x)** を使用。最初に開く時に Unity が `Library/`, `ProjectSettings/`, `Packages/` を自動生成する。スクリプトは Unity 6 の新 Rigidbody API（`linearVelocity` / `linearDamping` / `angularDamping`）を使用済み。
3. Package Manager で以下を追加（必要な場合）:
   - `com.unity.render-pipelines.universal` (URP) — Unity 6 では `Universal 3D` テンプレートで既定
   - `com.unity.textmeshpro`（Unity 6 では `Unity UI` に統合済みのことあり）
   - `com.unity.test-framework`
   - `com.unity.nuget.newtonsoft-json`
4. **Project Settings → Physics**:
   - `Gravity = (0, 0, -9.81)`（XY 平面を氷面として使用）
   - `Default Solver Iterations = 12`
   - `Default Solver Velocity Iterations = 4`
   - `Bounce Threshold = 0.5`
5. **Edit → Project Settings → Time**:
   - `Fixed Timestep = 0.02`（演出 50Hz。AI 探索用の 1ms ステップは別の純粋 C# `IceSimulator` で行う）
6. メニュー **`Curling → Bootstrap Scene (Step 1 Prototype)`** を実行する。氷面・ハウス・ライン・ストーン・カメラ・ライト・`MatchManager` などがコードから自動生成されるため、シーンファイルを手動で用意する必要はない。その後 **Play** を押せば対戦が始まる（↑↓: 速度 / ←→: 角度 / R: 回転反転 / Space: 投擲）。

## ディレクトリ

```
Assets/
├── Scripts/
│   ├── Core/         POCO データモデル・定数（DC4 互換）
│   ├── Physics/      StoneBody（MonoBehaviour）/ IceSimulator（純粋 C# 2D 円-円シミュ）
│   ├── Rules/        RuleEngine / ScoreCalculator / FgzValidator
│   ├── AI/           IShotDecider / HeuristicAI（Easy/Normal/Hard）
│   ├── Input/        ShotInputController（ショット入力 UI）
│   ├── Match/        MatchManager（試合進行）
│   ├── Serialization/Dc4Json / MatchLogWriter
│   └── UI/           Scoreboard / ThinkingTimer
├── Scenes/           Title / MatchSetup / GameMain / EndOfEnd / Result（要 Unity で作成）
├── Prefabs/          Stone / Sheet / House（要 Unity で作成）
├── PhysicMaterials/  Ice / Stone（要 Unity で作成）
├── Resources/config/ default_match.json（DC4 互換マッチ設定）
└── Tests/EditMode, PlayMode
```

## 実装ロードマップ

| Step | 内容 | 検証 |
|---|---|---|
| 1 | 物理プロトタイプ（シート + 1石 + 摩擦 + カール力） | PlayMode：終点 x が想定範囲 |
| 2 | ショット入力UI + 静止判定 + ターン遷移 | 手動 |
| 3 | RuleEngine（スコア・FGZ・ハンマー） | EditMode 単体テスト |
| 4 | エンド管理（16ショット×Nエンド） | 模擬棋譜再生 |
| 5 | CPU AI（Easy/Normal/Hard） | バッチ自対戦 100戦 |
| 6 | マッチ設定UI + 演出 | エンドツーエンド手動 |
| 7 | 物理 & AI バランス調整 | DC4 fcv1 出力との突合 |

## レギュレーション（DC4 Standard）

- ストーン半径 0.145m / ハウス半径 1.829m / ティーライン Y=38.405m
- シート幅 4.75m / シート長 40.234m
- 1エンド = 16 ショット（8石/チーム）、4 プレイヤー/チーム
- 標準エンド数 10、FGZ 5-rock デフォルト、no_tick_rule オプション
- 思考時間 219s + エクストラエンド 21.9s
- max_speed 4.0 m/s, stddev_speed 0.0076, stddev_angle 0.0018

## 参照

- 研究室の DC3 ベース MCTS 実装（非公開）を題材にしています。
- https://github.com/digitalcurling/DigitalCurling4-Server
- https://github.com/digitalcurling/DigitalCurling4-Client-Cpp
