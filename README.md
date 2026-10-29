# CPU 格闘ゲーム（開発版）

Windows 向けの小規模な 2D CPU 対戦ゲームです。C# / [.NET 10 LTS](https://dotnet.microsoft.com/en-us/platform/support/policy) と [MonoGame DesktopGL 3.8.6](https://www.nuget.org/packages/MonoGame.Framework.DesktopGL/3.8.6) を使用します。MonoGame の公式 NuGet パッケージは .NET 8 用アセンブリを提供し、Windows 10 (22H2 以降) を含むデスクトップ環境を対象に SDL2/OpenGL の描画・入力を提供します。.NET 8 は2026年11月にサポート終了予定のため、2028年までサポートされる .NET 10 をアプリの対象にしています。ライセンスと配布条件は [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt) と各公式パッケージ情報を参照してください。

## 実行・確認

開発環境に .NET SDK 10.0.401 (または同じ feature band の最新パッチ) を用意し、リポジトリのルートで実行します。

```powershell
dotnet restore
dotnet run -- --self-test
dotnet run
```

`--self-test` はコマンド入力、左右反転した攻撃判定、ゲージ・硬直条件、一振り一回のヒット、同時攻撃、画面端、着地、押し合い、同時 KO、再戦初期化を検査します。

## 操作

- `Enter`: タイトルから開始 / 結果画面から再戦
- `A` / `D`: 左右移動。相手と反対向きの入力で地上ガード
- `W`: ジャンプ、`S`: しゃがみ
- `J`: 弱攻撃、`K`: 強攻撃
- `下 → 斜め前下 → 前 + J`: 必殺技（ゲージ消費）
- `F2`: 必殺技を直接発動するデバッグ入力、`F3`: 判定・状態表示
- `Escape`: 終了

向きは入力履歴を記録した時点で前後方向に変換します。対戦中に向きが変わっても、記録済みの履歴は読み替えません。ウィンドウが非アクティブになると更新を止め、復帰時は操作キーがいったん離されるまで入力を無視します。

## 素材・差し替え

この clone には `3312.png` ～ `3317.png` が存在しないため、現状は色付きの図形キャラクターで対戦できます。画像を `Assets/` に追加しても、各画像の内容とコマ境界・基準点を確認するまでは表示素材として使いません。`GameData.cs` の `SpriteAnimations` に明示的な切り出し矩形、基準点、表示フレーム数を記入すると、起動時に一度だけロードして該当アクションへ割り当てられます。現状の攻撃・被弾・ガード演出も仮表示です。素材の権利・生成素材の利用条件・第三者表示は販売前に確認してください。

技のダメージ、発生・持続・硬直、ゲージ、移動速度、ジャンプ物理、入力受付時間は `GameData.cs` にまとめています。対戦モデル、CPU、履歴、衝突、簡易セルフテストは `Battle.cs` にあります。

## Windows 配布用ビルド

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish/win-x64
Compress-Archive -Path publish/win-x64/* -DestinationPath Fighter-win-x64.zip
```

`publish/win-x64` の全ファイルと `Assets/` を ZIP に含めて配布してください。単一 exe は有効化していません。MonoGame DesktopGL は SDL2/OpenGL とネイティブ実行時ファイルを使用するため、単一ファイル化や別 PC での動作は確認済みとは扱いません。この framework-dependent 配布には [.NET 10 x64 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (SDK 不要) が必要です。配布前に開発環境のない Windows 10 22H2 以降の PC に .NET 10 Runtime を用意し、ZIP を展開して起動、CPU 対戦、KO、再戦を確認してください。必要な場合は GPU ドライバーも更新してください。

## 販売・ライセンス上の未解決事項

- このリポジトリには、今回の変更以前から CC0 1.0 の `LICENSE` が存在します。今回それを変更していません。その適用範囲と、新しい自作コードを非公開・独自条件で扱う意図との整合を権利者が確認するまで、ソースのライセンス状態を確定したり販売可能と判断したりしないでください。
- MonoGame とその依存ライブラリの条件は第三者表示に記載しています。販売ビルドに含まれる実際の依存ファイルを確定した後、各公式ライセンスと必要な表示を再確認してください。
- 現在の画像は提供されていません。仮の図形表示も含め、完成素材と権利確認を販売前に行ってください。

## 手動確認

この実行環境では Windows GUI を操作できません。Windows PC でタイトルから開始し、移動・ジャンプ・しゃがみ・ガード・各攻撃、CPU の攻撃とガード、KO 後の結果表示、Enter による再戦を確認してください。F3 の判定表示と F2 の直接必殺技もデバッグに利用できます。