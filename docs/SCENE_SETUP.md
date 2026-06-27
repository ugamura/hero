# Unityシーンセットアップ

シーンは `Assets/Editor/HeroProjectBuilder.cs` が初回オープン時に自動生成します。
手作業のGameObject作成やInspector配線は不要です。

生成される主な構成:

```
ARMainScene
├── AR Session
├── XR Origin > Camera Offset > Main Camera
├── AR Managers
├── Game Systems
├── Effects
├── Event System
└── UI Canvas
    ├── Candidate Layer
    └── Safe Area
        ├── Top Bar / Bottom Bar
        ├── Hint / Detail / Game Over
        ├── Server Settings
        └── Tutorial
```

再生成する場合はUnityメニューの `Hero > Rebuild Main Scene` を使います。
モバイル設定だけを再適用する場合は `Hero > Apply Mobile Build Settings` を使います。

AndroidではARCore、iOSではARKitローダーを自動割り当てします。環境によって自動割り当てできない場合のみ、Project Settings > XR Plug-in Managementでチェックを入れてください。
