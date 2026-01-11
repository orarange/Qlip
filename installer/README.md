# Qlip インストーラ（GUI）

このリポジトリ（`orarange/Qlip`）は **インストーラ用** です。
プログラム本体のソースは別リポジトリ（`orarange/Qlip-src`）に置く想定です。

エンドユーザー向け: `QlipSetup_...exe` は GUI ウィザード形式で、
- 進行状況表示
- 利用規約(EULA)の同意
- 第三者ライセンス通知
を表示してインストールします。

## 使い方（エンドユーザー向け）

### 1) インストール

1. GitHub の Releases から `QlipSetup_...exe` をダウンロードします。
2. ダウンロードしたEXEを実行し、ウィザードに従ってインストールします。
3. インストール後、スタートメニューの **Qlip** を起動します。

※ 現在の配布は「EXE単体で完結する同梱型（オフライン）インストーラ」です。通常、別途 zip をダウンロードする必要はありません。

### 2) 基本操作（Instant Replay）

Qlip は「常に録画し続けて、必要な瞬間だけ“直近N秒”を保存する」タイプのアプリです。

- **開始**: 録画バッファを開始します（ステータスが「録画中」になります）
- **保存**: 録画を止めずに、直近N秒を `mp4` として保存します
- **設定**: 画質・音声・保存先・リプレイ秒数などを変更します

保存に成功すると、ステータスに `保存: Replay_yyyyMMdd_HHmmss.mp4` のように表示されます。

### 3) 保存される場所

- 既定: Windowsの「ビデオ」フォルダ配下
- 保存先(ベース) を `設定` で変更できます
- 実際の保存先は「当日フォルダ」にまとめます
  - 例: `...\ビデオ\yy-MM-dd-replay\Replay_yyyyMMdd_HHmmss.mp4`

### 4) 設定項目

- **画質**: 高/標準/低/カスタム（ビットレートとFPS）
- **リプレイ秒数**: 保存する“直近N秒”（5〜600秒）
- **システム音 / マイク**: 収録する音声の種類
- **出力デバイス / 入力デバイス**: 収録対象のデバイス（(既定) を選べます）

※ 画質を上げるほどPC負荷とディスク使用量が増えます。カクつく場合は「低」やFPS/ビットレートを下げてください。

### 5) 終了・常駐

- ウィンドウ右上の **×** は「終了」ではなく「非表示（トレイへ）」です。
- 完全に終了したい場合は、タスクトレイの Qlip アイコンを右クリックして **終了** を選びます。

### 6) 設定の保存場所（リセットしたいとき）

設定は次のファイルに保存されます。

- `%APPDATA%\InstantReplayApp\settings.yaml`

このファイルを削除すると、設定は初期化されます。

### 7) トラブルシューティング

- **保存ボタンが押せない**: 先に **開始** を押して「録画中」にしてください。
- **音が入らない**: `設定` で「システム音/マイク」が有効か、デバイス選択が適切か確認してください。
- **保存に失敗する / ffmpeg が見つからない**:
  - 通常は同梱の `ffmpeg\ffmpeg.exe` を使います。
  - もし見つからない場合は、環境変数 `QLIP_FFMPEG_PATH` に `ffmpeg.exe` のフルパス（または格納フォルダ）を指定してください。

## 生成方法（PowerShell不要）

このリポジトリにはインストーラ生成用のGUIツール `InstallerBuilder` を同梱します。

- Visual Studio で `InstallerBuilder` を実行 → `Qlip-src` フォルダを指定 → 「ビルド開始」
- 成果物:
  - 配布zip（GitHub Releases にアップロードするアセット）: `installer/dist/Qlip_win-x64.zip`
  - Webインストーラ（GitHubからDLして展開）: `installer/dist/QlipSetup_...exe`（Inno Setup 6 が必要）

## 必要ツール（インストーラ生成時のみ）

- Inno Setup 6（ISCC.exe）
  - https://jrsoftware.org/isinfo.php

※ エンドユーザーは追加ツール不要（セットアップEXEを実行するだけ）

## デプロイ（単一スクリプト）

EULA更新などで Release の成果物を差し替えたい場合、リポジトリ直下の `deploy.ps1` で
「Qlip本体（portable zip）」または「インストーラEXE」のどちらかを選んでデプロイできます。

例: インストーラEXEをビルドして `v1.0.0` にアップロード

- `powershell -ExecutionPolicy Bypass -File .\deploy.ps1 -Target installer -Tag v1.0.0 -Upload`

例: portable版zipをビルドして `v1.0.0` にアップロード

- `powershell -ExecutionPolicy Bypass -File .\deploy.ps1 -Target app -Tag v1.0.0 -Upload`

※ `-Upload` を付けない場合はローカル生成のみ行います。

## 同梱 ffmpeg

インストール先の `ffmpeg\ffmpeg.exe` を Qlip が優先的に利用します。

## Webインストーラの仕組み

Webインストーラ（`installer/Qlip.iss`）は、インストール時に **インストーラ用リポジトリ（orarange/Qlip）** の GitHub Releases から
`Qlip_win-x64.zip` をダウンロードして `{app}` に展開します。

想定URL（latestの例）:

`https://github.com/orarange/Qlip/releases/latest/download/Qlip_win-x64.zip`

※ このzipは Releases のアセット名が一致している必要があります。

## オフラインインストーラ

従来の「全部同梱」のオフラインインストーラ用スクリプトは `installer/QlipOffline.iss` です。

## リリース運用（2リポジトリ）

- `orarange/Qlip-src`: `dotnet publish` して Qlip 本体（+ ffmpeg同梱）を作る
- `orarange/Qlip`: 上記成果物を `Qlip_win-x64.zip` にして Releases に添付し、Webインストーラを配布する

## GitHub Actions（重要）

`orarange/Qlip` の Actions は、別リポジトリ `orarange/Qlip-src` を checkout してビルドします。

- `Qlip-src` が **public** の場合: 追加設定なしで動きます。
- `Qlip-src` が **private** の場合: `orarange/Qlip` 側の Repository secrets に `QLIP_SRC_TOKEN` が必要です。
  - classic PAT なら scopes は `repo`（読み取りに必要）
  - fine-grained PAT なら `orarange/Qlip-src` に対して `Contents: Read` が付くように作成

作成したPATを `Settings > Secrets and variables > Actions > New repository secret` から `QLIP_SRC_TOKEN` として登録してください。

## 注意

- `installer/EULA.txt` はひな形です。配布前に必ず内容を確定してください。
