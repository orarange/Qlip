# Qlip Installer

このリポジトリ（orarange/Qlip）はインストーラ配布用です。
プログラム本体のソースは orarange/Qlip-src にあります。

- Webインストーラ: installer/Qlip.iss
- 生成手順: installer/README.md

## GitHub Actions

このリポジトリの Actions は `orarange/Qlip-src` を checkout します。
`Qlip-src` が private の場合は、Secrets に `QLIP_SRC_TOKEN`（PAT）が必要です（詳細: installer/README.md）。
