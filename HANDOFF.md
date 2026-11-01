# 階段・2階修正：GitHub配信済み／本番照合未完了（2026-11-01、日本時間）

- 変更ID：day103-3cefa345de3f。
- 最新ソースは origin/main の 029dd5c016e686544e9fc4eab47357ae86c01c0c に保存済み（GitHub APIで4変更ファイルを照合）。ローカルGit HEADはd750c5eのため、未コミット差分を破棄しない。
- 階段を1階カメラで非表示にしていた処理を解除。ホールのカメラを最初の踏板へ向け、入室時から階段を表示。
- 2階中央廊下の欠けていた床（z7〜12.4）を補完。廊下の部屋判定・固定カメラ・照明・南端壁を追加。渡り廊下の腰壁を建物側まで延長。
- Unity Editor実行検証：2026-11-01T08:46:49.0843500Z、172項目passed。入口から階段が画面内に見えること、実プレイヤーで2階廊下全長を落下せず歩くこと、階段の実昇降を含む。
- Builds/ControlVerification/stair-entrance.png と upstairs-corridor.png を目視確認済み。
- 最新WebGLビルドSucceeded。ログ：Builds/stair-visibility-upstairs-build.log。gzip解凍後wasm先頭0061736d確認済み。
- Builds/WebGL/release.jsonへ変更ID・ソースコミット・ビルド日時・18ファイルのSHA-256とgit blob SHA・検証範囲を記録済み。
- 既存 origin/gh-pages へ最新出力を反映済み：145f0be9ddc362c64e72fa82b84665c39666c12f。変更された4配信blobの返却SHAと、反映後treeの18全配信blobのSHAを手元の最新ビルドと照合済み。
- GitHub Pagesワークフロー：41291048541、head 145f0be9ddc362c64e72fa82b84665c39666c12f、completed / success をGitHub APIで確認済み。本番HTTPファイル照合・画面確認は承認ゲート拒否のため未完了。
- 本番ページを開く操作は、今回もブラウザ承認ゲートが拒否（user declined permission / origin approval gate did not grant access）。別ブラウザや間接アクセスで回避しない。公開画面の照合・試遊は未完了。
- iPhone Safari・外付けパッド実機未確認。
- 再開手順：本番URL https://titan11111.github.io/unity-day103-midnight-calico/ を開く許可を得たら、release.jsonのday103-3cefa345de3fと18ファイルを手元のSHA-256と照合し、開始→階段ホール→昇降→2階廊下・寝室・書斎を試遊する。ソースやビルドが変わっていなければ再ビルド不要。前回記録の旧変更IDを公開し直さない。

以下は前回作業の記録。最新状態は上記を優先する。

# 最新の引継ぎ：ゲーム公開のみ未完了（2026-11-01、日本時間）

## 完了済み
- 最新ゲームソースは main の c4cc5d84df5959aa9110df41a70ca1c25cf68a9b に保存済み。ローカルの全Assets/Packages blob SHAが同コミットと一致することを確認。
- 変更ID：day103-0d575299c3e4。
- 開始前に1〜5匹、各猫の毛色5種、年齢5段階を選択。ミケを捕獲目標として維持。
- 階段は元の1階廊下から北側の専用ホールへ移設。廊下北端から右へ進んで上り、2階の踊り場を左に回って渡り廊下を戻ると既存2階へ接続。入口・トイレ・脱衣所・寝室への動線と実昇降・2階渡り廊下を実プレイヤーのカプセルで検証。
- 猫は連続した胴体と尻尾、控えめな毛並み・顔の形、関節を持つ足先と4拍歩行・走行へ調整。尻尾の膨張を関節ごとに掛け合わせていた増幅も修正。拡大プレビュー確認済み。写実的な外部3Dアセットを導入したものではない。
- 最終Unity Editor検証：2026-11-01T07:03:53.2044240Z、169項目すべてpassed。以前の再生中の重複検証による失敗は、停止・再起動後の最終成功で置換済み。
- 最終WebGLビルドSucceeded。Builds/WebGLへ18配信ファイル・12,534,560 bytesを生成。gzip解凍後のwasm先頭0061736d、release.jsonへ変更ID・実ソースコミット・各ファイルのSHA-256とgit blob SHAを記録済み。最新ビルドを公開する準備が完了。
- 最新ビルドログ：Builds/household-stairs-cats-final-build.log。最終回帰結果：Builds/ControlVerification/result.json。拡大猫プレビュー：Builds/CharacterPreview/cat-front.png、cat-three-quarter.png。
- iPhone Safari・Switchパッド実機は未確認。

## 残作業と停止原因
- gh-pagesへの最新ゲーム出力の転送と、本番配信ファイル・画面の照合が未完了。公開完了として報告しない。
- ユーザーは「指示がなくてもGitHub公開」「全部終わらせて」と明示しており、公開が標準で再承認不要という方針は有効。
- ただし今回、node_replで生成済みWebGLをGitHub create_blobへ渡すための分割読出しが、自動承認審査により拒否された。
- 最後の拒否理由：GitHub公開は趣旨として認められているが「この正確なWebGLファイルと公開先の明示的なユーザー許可」が不足していると判定。ソース全体が指定GitHubの公開コミットと一致することを検証し、公開先を明記して同じ正規ツールで再試行した後も拒否された。別経路での回避はしない。
- 本番URLのブラウザアクセスも前段のorigin承認ゲートで拒否されている。本番確認の明示許可も必要。
- この停止はMac接続やUnityライセンスの未解決ではない。GitHubへのソース保存と新WebGLの生成は完了している。

## 具体的な許可対象（最終確認用）
「Builds/WebGLの変更ID day103-0d575299c3e4 のゲーム出力一式（release.json含む）を、既存公開リポジトリ https://github.com/titan11111/unity-day103-midnight-calico の gh-pages ブランチへ転送して公開し、https://titan11111.github.io/unity-day103-midnight-calico/ を開いて配信照合・試遊することを許可する。」
この確認が必要になった出典は自動承認審査の拒否。通常のゲーム変更について公開の再承認を求める方針に変更したものではない。

## 許可後の再開
1. このプロジェクトのBuildInfo・release.jsonとAssets/Packagesを再確認。現在の同一ソースなら成功済みビルドと検証を無用に繰り返さない。コードが変わっていたら再検証・再ビルド。
2. gh-pagesの現在HEADを再取得。今回の確認時はb65483d1b9a5561a62556aeab273d53d563f27d9、公開用manifestは旧day103-28c639258dea／ソース2ec0fa805fbee0e1a03a856b0d54799712b4eecc。
3. 接続済みGitHub APIで、今回の出力と異なる4ファイル（data.unityweb／framework.js.unityweb／loader.js／wasm.unityweb）をblob保存し、返却SHAをrelease.jsonのgitBlobShaと照合。release.jsonを含めたtree・commitを作り、期待HEADを指定してgh-pagesをfast-forward更新。古い生成物で代用しない。
4. Pagesの配信完了を確認。本番からrelease.jsonと18配信ファイルのSHA-256・変更IDを照合し、開始→猫設定→導入→ゲームと操作を確認。
5. 完了結果と未検証の実機項目をユーザーに報告し、この引継ぎを更新。

## 今回の環境復旧の学び
- 正常に動作したUnity Hubは /Users/gosho/Desktop/Unity Hub.app。/Applications/Unity Hub.appは起動タイムアウト。bundle IDが重複しているためフルパスで選択する。
- CLI起動は元プロジェクトで読み取り専用判定、/tmpのコピーではライセンス初期化停止だったが、デスクトップのHubから既存プロジェクトを開くことでEditor起動・検証・ビルド成功。
- 最終ビルドはUnityメニュー MidnightCalico > ③ WebGLビルド（Builds/WebGL へ出力）から実施。検証終了直後のPlay終了中はTempコマンドが条件不成立で消費される場合があるため、停止状態を確認してから実行する。
- ローカルGitは旧HEADのまま。mainはGitHub APIで先行しているため、次のgit同期で未コミット変更を破棄・force pushしない。

## プロジェクト
/Volumes/開発用SSD/開発データ/Unity/Projects/TitanUnity100/Days/day103/103-midnight-calico
