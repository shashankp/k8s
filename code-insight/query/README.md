# Run
```
sudo apt update && sudo apt install python3-venv
python3 -m venv .venv
source .venv/bin/activate

python -m venv .venv
.\.venv\Scripts\Activate.ps1

pip install -r requirements.txt

uvicorn main:app --reload
```