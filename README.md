# Hero - AR Word Chain

スマートフォンのカメラで現実の物体を検出し、検出された英単語を使ってしりとりを遊ぶARゲームです。

Unity + AR Foundationをクライアントに、Python + FastAPI + YOLOv8 + ByteTrackを物体検出サーバに使用しています。

## 主な機能

- ARカメラ映像からのリアルタイム物体検出
- YOLOv8 + ByteTrackによる検出と追跡
- 候補を状態別に色分け（緑: 一致、灰色: 不一致、斜線: 使用済み）
- 物体をタップして英単語を確定
- スコア、ライフ、60秒タイマー、使用履歴
- ダブルタップによるスキップ
- シェイクによるヒント、長押しによる詳細表示
- 成功単語のAR空間への固定
- 視覚・音・振動フィードバック
- Unity Editor用Mockデモ

## システム構成

~~~text
Android端末のARカメラ
        ↓ JPEG / HTTP
PC上のFastAPI検出サーバ
        ↓ YOLOv8 + ByteTrack
物体名・信頼度・bbox・tracking_id
        ↓ JSON
Android端末で候補表示・タップ・しりとり判定
~~~

カメラ画像はLAN内のPCへ送信して推論に使用します。現在のサーバ実装では受信画像をファイルとして保存しません。

## 必要環境

### PC

- Windows 10 / 11
- Unity Hub
- Unity Editor 2022.3.62f1
- Python 3.10 - 3.12推奨
- PCとAndroid端末が同じLANに接続できること

### Unity Hubで追加するモジュール

Unity 2022.3.62f1の「モジュールを加える」から、次をインストールします。

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

### Android端末

- Android 10以上
- ARCore対応端末
- Google Play Services for AR
- USBデバッグを利用できるデータ通信対応USBケーブル

対応端末は[ARCore supported devices](https://developers.google.com/ar/devices)で確認できます。

## フォルダ構成

~~~text
Hero/
├── unity/                 Unityプロジェクト
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
├── server/                物体検出サーバ
│   ├── main.py
│   ├── detector.py
│   ├── config.yaml
│   ├── setup_server.bat
│   └── start_server.bat
└── docs/                  設計資料・実装状況
~~~

# 最短で試す: Unity Editorデモ

EditorデモではAndroid端末も検出サーバも不要です。

1. Unity Hubで <code>unity/</code> フォルダを開きます。
2. 初回のパッケージ読み込みとコンパイルが終わるまで待ちます。
3. <code>unity/Assets/Scenes/ARMainScene.unity</code> を開きます。
4. Unity中央上部の再生ボタンを押します。
5. 初回チュートリアルを進めて <code>PLAY</code> を押します。
6. 緑枠をクリックして単語を確定します。

デモでは次の順番を試せます。

~~~text
APPLE → EGGPLANT → TV → VASE
~~~

Editor操作:

- 1回クリック: 単語を確定
- ダブルクリック: スキップ
- 長押し: 候補詳細
- Hキー: シェイク相当のヒント
- RESET: 最初から

ARMainSceneがない場合は、Unityメニューの <code>Hero &gt; Rebuild Main Scene</code> を実行してください。

# Android実機で起動する

## 1. 初回だけ: 検出サーバを準備

<code>server/setup_server.bat</code> をダブルクリックします。

この処理ではPython仮想環境と必要ライブラリを準備します。初回はダウンロードに時間がかかります。

コマンドで準備する場合:

~~~powershell
cd server
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
~~~

## 2. 毎回: 検出サーバを起動

<code>server/start_server.bat</code> をダブルクリックします。開いた黒い画面はゲームを終了するまで閉じないでください。

PCのブラウザで次を開きます。

~~~text
http://127.0.0.1:8000/healthz
~~~

次のように表示されれば正常です。

~~~json
{"status":"ok","model":"yolov8n.pt"}
~~~

## 3. PCのIPv4アドレスを調べる

Windowsのコマンドプロンプトで次を実行します。

~~~text
ipconfig
~~~

使用中のイーサネットまたはWi-Fiアダプターにある「IPv4 アドレス」を確認します。

例:

~~~text
192.168.1.79
~~~

IPアドレスはネットワークによって変わるため、各自の値へ置き換えてください。

## 4. スマートフォンから接続確認

PCとスマートフォンを同じWi-Fi / LANへ接続します。スマートフォンのブラウザで次を開きます。

~~~text
http://<PCのIPv4アドレス>:8000/healthz
~~~

例:

~~~text
http://192.168.1.79:8000/healthz
~~~

statusがokにならない場合は、Windows Defender FirewallでPythonのプライベートネットワーク通信を許可してください。

## 5. Android端末のUSBデバッグを有効化

1. Androidの「設定」→「デバイス情報」を開きます。
2. 「ビルド番号」を7回タップします。
3. 「開発者向けオプション」で「USBデバッグ」をONにします。
4. USBケーブルでPCへ接続します。
5. USBの使用用途は「ファイル転送」を選びます。
6. 「このパソコンからのUSBデバッグを許可しますか？」を許可します。

## 6. UnityのAndroid設定を適用

Unityで次を実行します。

~~~text
Hero > Apply Mobile Build Settings
~~~

このメニューで次が設定されます。

- Android minimum API 29（Android 10）
- ARCoreローダー
- OpenGLES3のみ（Vulkan無効）
- ARM64のみ（ARMv7無効）
- IL2CPP
- LAN内HTTP通信の許可
- カメラ・インターネット関連設定

<code>Edit &gt; Project Settings &gt; XR Plug-in Management</code> のAndroidタブでARCoreが有効なことも確認します。

## 7. AndroidへBuild And Run

1. Unityで <code>File &gt; Build Settings</code> を開きます。
2. PlatformからAndroidを選びます。
3. <code>Switch Platform</code> を押します。
4. <code>Run Device</code> で接続したAndroid端末を選びます。
5. <code>Build And Run</code> を押します。
6. APKの保存先として任意の空フォルダを指定します。
7. ビルドとインストールが完了するまで待ちます。

初回のIL2CPPビルドは時間がかかる場合があります。

## 8. アプリで検出サーバを設定

初回起動時にカメラ権限を許可します。

1. アプリ上部の <code>SERVER</code> を押します。
2. 次の形式で入力します。

~~~text
http://<PCのIPv4アドレス>:8000/detect
~~~

例:

~~~text
http://192.168.1.79:8000/detect
~~~

3. <code>SAVE</code> を押します。
4. 上部ステータスが <code>LIVE xxms</code> になれば接続成功です。

127.0.0.1はスマートフォン自身を指すため、実機では使用できません。

## 9. ゲームを開始

明るい場所でカメラを物体へ向けます。検出しやすい例はchair、bottle、cup、book、laptop、tv、appleです。

最初の必要文字はAです。検出できてもA以外で始まる単語は灰色で表示されます。緑枠をタップすると単語を確定します。

実機操作:

- 緑枠をタップ: 単語を確定
- ダブルタップ: スキップ（ペナルティあり）
- 長押し: 候補の詳細
- スマートフォンをシェイク: ヒント
- RESET: ゲームをリセット

# 2回目以降の起動手順

毎回必要なのは次の5ステップです。

1. PCとスマートフォンを同じLANへ接続
2. <code>server/start_server.bat</code> を起動
3. PCまたはスマートフォンでhealthzを確認
4. Androidアプリを起動
5. <code>LIVE xxms</code> を確認してプレイ

PCのIPv4アドレスが変わった場合は、アプリのSERVER設定を更新してください。

# 接続ステータス

| 表示 | 意味 | 対応 |
|---|---|---|
| LIVE xxms | サーバとの通信成功 | そのままプレイできます |
| OFFLINE | サーバへ接続できない | IP、Wi-Fi、Firewallを確認 |
| DETECTING | 検出リクエスト送信中 | 長時間変わらなければ接続を確認 |
| INVALID RESPONSE | サーバ応答を解析できない | サーバのコンソールを確認 |
| DEMO | Editor用Mock、または実機からまだ未送信 | 実機ではARCoreとカメラ権限を確認 |

# トラブルシューティング

## ARMainSceneがない

コンパイルエラーを解消してから <code>Hero &gt; Rebuild Main Scene</code> を実行します。

## Vulkan graphics API is not supported by ARCore

<code>Hero &gt; Apply Mobile Build Settings</code> を再実行します。

手動ではPlayer SettingsのAndroid / Other SettingsでAuto Graphics APIをOFFにし、Vulkanを削除してOpenGLES3だけを残します。

## ARMv7 APK to ARM64 device

<code>Hero &gt; Apply Mobile Build Settings</code> を再実行します。

手動ではTarget ArchitecturesのARMv7をOFF、ARM64をONにし、Scripting BackendをIL2CPPにします。

## Run Deviceにスマートフォンが表示されない

- USBの用途を「ファイル転送」にする
- USBデバッグをONにする
- 端末に表示されるPCの認証を許可する
- データ通信対応ケーブルを使う
- 必要なら端末メーカーのUSBドライバーを導入する

## スマートフォンからhealthzが開けない

- PCとスマートフォンが同じLANか確認
- start_server.batが起動中か確認
- IPアドレスが変わっていないか確認
- Windows Defender FirewallでPythonを許可
- ゲストWi-Fiや端末間通信を遮断するネットワークを避ける

## OFFLINEと表示される

SERVER設定が次の形式か確認します。

~~~text
http://<PCのIPv4アドレス>:8000/detect
~~~

## LIVEだが物体を検出しない

- 明るい場所で試す
- 物体を画面内へ大きく映す
- chair、bottle、cup、book、laptop、tvなどを試す
- <code>server/config.yaml</code> のconf_thresholdを必要に応じて調整
- 人（person）はゲーム設定で検出対象から除外されています

## 枠は出るが緑色にならない

検出失敗ではありません。現在必要な文字と単語の先頭文字が一致しない候補は灰色になります。

# 検出サーバ設定

<code>server/config.yaml</code> でモデルと閾値を変更できます。

~~~yaml
model:
  weights: "yolov8n.pt"
  conf_threshold: 0.4
  iou_threshold: 0.45
  use_tracker: true
  tracker: "bytetrack.yaml"
~~~

変更後は検出サーバを再起動してください。

## API

### GET /healthz

サーバの稼働状態を返します。

### POST /detect

multipart/form-dataでJPEG画像とframe_idを受信し、検出結果を返します。

~~~json
{
  "frame_id": 123,
  "elapsed_ms": 42,
  "image_size": { "w": 640, "h": 360 },
  "detections": [
    {
      "tracking_id": 7,
      "label": "apple",
      "confidence": 0.92,
      "bbox": { "x": 0.42, "y": 0.31, "w": 0.18, "h": 0.22 }
    }
  ]
}
~~~

# 開発者向け確認

Pythonの軽量テスト:

~~~powershell
cd server
.\.venv\Scripts\Activate.ps1
python -m unittest discover -s tests
~~~

PCカメラを使った検出確認:

~~~powershell
python test_webcam.py
~~~

# ドキュメント

- [実装設計書](docs/DESIGN.md)
- [Unityシーン構成](docs/SCENE_SETUP.md)
- [Editorデモ確認](docs/PROTOTYPE_GUIDE.md)
- [実装状況](docs/IMPL_STATUS.md)

# 注意事項

- 平文HTTPはLAN内の開発用途として使用しています。公開ネットワーク越しで運用しないでください。
- YOLOv8nは軽量モデルのため、環境や対象物によって誤検出・未検出があります。
- YOLO重み、Python仮想環境、Unity Library、APKはリポジトリへ含めません。
- 署名鍵、APIキー、パスワードをコミットしないでください。
