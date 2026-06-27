# 実装状況

## 完了

- [x] Unity 2022.3 LTSプロジェクト設定とARパッケージ
- [x] 初回オープン時のARMainScene自動生成
- [x] AR Camera CPU imageのJPEG送信
- [x] FastAPI + YOLOv8 + ByteTrack検出サーバ
- [x] tracking_idによる候補追跡とEMA平滑化
- [x] Match / Unmatch / Used / LowConfの色分け、既出斜線、Match脈動
- [x] 面積最小・同面積時confidence優先のヒットテスト
- [x] シングルタップ確定、ダブルタップスキップ、長押し詳細、シェイクヒント
- [x] スコア、ライフ、制限時間、必要文字、順序付き使用履歴
- [x] Good / Bad / AlreadyUsedの視覚・自動生成音・Haptic
- [x] Good単語のAR Anchor固定、平面なし時フォールバック、最大個数制限
- [x] 初回チュートリアル、ゲームオーバー、リセット
- [x] アプリ内サーバURL設定と接続状態表示
- [x] Editor Mockデモ / 実機AR自動切替
- [x] Android 10+ / iOS 15+向け設定

## 実機確認時のチェック

- [ ] ARCore対応AndroidまたはARKit対応iPhoneでカメラ映像が表示される
- [ ] PCと端末間で8000番ポートへ接続できる
- [ ] 実環境で検出レイテンシ200ms目標を計測する
- [ ] 教室・発表場所の照明で検出精度とシェイク閾値を調整する

実機・ネットワーク固有の4項目は、対象端末と当日のネットワークで最終確認してください。
