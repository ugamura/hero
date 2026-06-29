#!/bin/bash
set -e
cd "$(dirname "$0")"
source .venv/bin/activate

pip install -q cryptography

LOCAL_IP=$(python gen_cert.py)

echo ""
echo "======================================================"
echo "  YOLO Live Check — HTTPS"
echo "======================================================"
echo ""
echo "  【初回のみ】iPhone に証明書をインストール"
echo "  1. iPhone Safari で開く:"
echo "     https://$LOCAL_IP:8000/cert"
echo "     → 「プロファイルがダウンロードされました」"
echo "  2. 設定 → 一般 → VPN とデバイス管理 → インストール"
echo "  3. 設定 → 一般 → 情報 → 証明書信頼設定 → 信頼をON"
echo ""
echo "  【毎回】リアルタイム検出ページ:"
echo "     https://$LOCAL_IP:8000/check"
echo ""
echo "======================================================"
echo ""

python -m uvicorn main:app --host 0.0.0.0 --port 8000 \
  --ssl-keyfile key.pem --ssl-certfile cert.pem
