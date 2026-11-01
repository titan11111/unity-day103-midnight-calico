# 公開未完了の引継ぎ（2026-11-01、日本時間）

## 最新のユーザー指示
「何度もいうように指示がなくてもGitHub公開して欲しい」
ゲーム変更はmain保存・WebGLビルド・gh-pages公開・本番照合までを既定とし、再指示を待たない。

## 未公開の変更
開始前の「うちの猫たち」画面。匹数1〜5匹、各猫の毛色（三毛・茶トラ・黒・白・灰白）、年齢（生後3か月・1歳・3歳・8歳・15歳）を選択。年齢による体格、追加同居猫の高齢時速度調整。1匹目のミケを捕獲目標として維持。

Mac側プロジェクト：/Volumes/開発用SSD/開発データ/Unity/Projects/TitanUnity100/Days/day103/103-midnight-calico
変更対象：Scripts/Core/GameManager.cs、Scripts/UI/UIManager.cs、Scripts/Cat/CatAI.cs、Scripts/Cat/CatModel.cs、Editor/ControlVerification.cs、生成BuildInfo.cs、README.md（Assets/MidnightCalico配下）。既存未コミット変更を破棄しない。

## 実施済みの証拠と限界
- Mac上のUnity Editorで2026-11-01T00:50:49.1075830Z、155項目passedを確認。匹数1〜5の実生成数、捕獲目標維持、毛色・年齢適用と既存操作を含む。毛色検査は形状保持の確認であり、全色の見た目の目視確認ではない。
- この検証時の生成IDはday103-c70c261d29d1。その後Editorのsetup-previewコマンドとREADMEを追加したため、最終IDはMacで再確認する。
- 最後にTemp/controls-command.txtへplayを書き込んだ後、Mac接続が途絶えた。現在Play状態・コンパイル状態・その後のビルド実行は確認できない。
- 現環境はLinuxの空の作業領域で、Macの/VolumesもUnityプロジェクトも存在しない。最新ソースと生成物を読み出せないため、最新版のGitHub保存・WebGLビルド・ゲーム公開は未完了。
- GitHub APIは利用可能。今回再確認したmainのGameManager.csにはCatSetupがない。gh-pages/release.jsonはday103-28c639258dea、sourceCommit=2ec0fa805fbee0e1a03a856b0d54799712b4eecc。子猫追加版であり、猫設定機能の公開を意味しない。
- 本番URLのブラウザ確認は前段でorigin承認拒否となっており、回避経路でのアクセスを行わない。iPhone Safari・Switch実機も未確認。

## 再開手順
1. Mac上の既存プロジェクトへ接続し、Play状態を確認して停止する。未コミットソースを保持し、最新IDとコンパイルを確認。
2. 155項目の回帰検証を必要に応じ再実行。開始→猫設定→匹数・毛色・年齢→導入→ゲームと、タッチ・パッドの画面/操作を確認。
3. Unity 6000.3.26f1でMidnightCalico.EditorTools.MidnightCalicoSetup.BuildWebGLを実行。今回のBuildResult.Succeededを確認。古いビルドを代用しない。
4. 今回の必要ソースをmainへ保存。リモートはローカル旧HEADより先行しているため、fetchして差分を照合し、force pushや他の変更の上書きをしない。通常git通信のDNSエラー時は接続済みGitHub APIを使用できる。
5. 最新Builds/WebGLのrelease.jsonへ実ソースコミット・最終変更ID・生成日時・ファイルSHA-256・実施済み検証を記録し、gh-pagesへ公開。
6. 許可された本番アクセスでrelease.json・全配信ファイル・ゲーム内IDを照合。開始と猫設定の反映を確認して初めて公開完了と報告する。

公開先：https://titan11111.github.io/unity-day103-midnight-calico/
