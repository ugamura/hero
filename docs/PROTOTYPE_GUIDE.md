# プロトタイプ確認ガイド

## Editorデモ

1. Unity Hubで `unity/` を開く。
2. パッケージ読み込み後、`Assets/Scenes/ARMainScene.unity` を開く。
3. Playを押す。
4. 緑枠のAPPLEをクリックする。
5. EGGPLANT、TV、VASEの順でクリックする。
6. ダブルクリックでスキップする。
7. Hキーでシェイク相当のヒントを出す。
8. 候補を長押しして信頼度パネルを表示する。
9. RESETで状態が初期化されることを確認する。

EditorではMockDetectionFeederが自動で有効になり、ARカメラと通信は停止します。

## 実機モード

実機ではMockが自動で無効になり、ARカメラのフレームが検出サーバへ送られます。
アプリ上部のSERVERからPCのLANアドレスを設定してください。

## トラブルシュート

- シーンがない: Unityメニュー `Hero > Rebuild Main Scene`
- 枠が出ない: EditorではGameビューをPortrait比率にし、Playを再実行
- 実機でOFFLINE: PCと端末のWi-Fi、IP、ポート8000、Firewallを確認
- カメラが出ない: XR Plug-in ManagementでAndroidはARCore、iOSはARKitが有効か確認
- ビルド不可: Unity Hubで対象プラットフォームのBuild Supportを追加
