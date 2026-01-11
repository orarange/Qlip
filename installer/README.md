# Qlip インストーラ（GUI）

このリポジトリ（`orarange/Qlip`）は **インストーラ用** です。
プログラム本体のソースは別リポジトリ（`orarange/Qlip-src`）に置く想定です。

エンドユーザー向け: `QlipSetup_...exe` は GUI ウィザード形式で、
- 進行状況表示
- 利用規約(EULA)の同意
- 第三者ライセンス通知
を表示してインストールします。

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

## 注意

- `installer/EULA.txt` はひな形です。配布前に必ず内容を確定してください。
