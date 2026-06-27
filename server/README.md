# Hero Detection Server

FastAPI + YOLOv8 + ByteTrackでカメラ画像から物体候補を返します。

## Windowsで起動

1. `setup_server.bat` をダブルクリック（初回のみ）
2. `start_server.bat` をダブルクリック
3. `http://127.0.0.1:8000/healthz` をブラウザで確認

Python 3.10以上を推奨します。初回はYOLOv8nモデルが自動取得されます。

## コマンドで起動

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python -m uvicorn main:app --host 0.0.0.0 --port 8000
```

## API

- `GET /healthz` - 稼働状態
- `POST /detect` - multipartのimage（JPEG）とframe_idを受信

レスポンスには正規化bbox、英語ラベル、confidence、ByteTrackのtracking_idが含まれます。

## テスト

```powershell
python -m unittest discover -s tests
python test_webcam.py
```

`test_webcam.py` はPCカメラと起動済みサーバを使います。Qキーで終了します。
