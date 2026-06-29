#!/bin/bash
cd "$(dirname "$0")"
if [ ! -f .venv/bin/python ]; then
  echo "Run setup_server.sh first."
  exit 1
fi
source .venv/bin/activate
python -m uvicorn main:app --host 0.0.0.0 --port 8000
