import json
import os
import time
import requests

# ─────────────────────────────────────────────────────
# CONFIGURATION
# ─────────────────────────────────────────────────────

# These paths must exactly match what StrategyBridge.cs prints in the Unity Console.
# Step 1: Run Unity in Play mode with StrategyBridge active (trainingMode = false).
# Step 2: Look in the Console for:
#     [StrategyBridge] Game state path:    C:\Users\...
#     [StrategyBridge] Strategy tag path:  C:\Users\...
# Step 3: Copy those exact paths here.

GAME_STATE_PATH   = r"PASTE_GAME_STATE_PATH_HERE"
STRATEGY_TAG_PATH = r"PASTE_STRATEGY_TAG_PATH_HERE"

# Ollama settings — do not change these unless Ollama is on a different port
OLLAMA_URL   = "http://localhost:11434/api/generate"
MODEL_NAME   = "mistral"
TIMEOUT_SECS = 40

# How often to check if game_state.json has been updated
POLL_INTERVAL = 1.0

# ─────────────────────────────────────────────────────
# PROMPT BUILDER
# ─────────────────────────────────────────────────────

def build_prompt(state: dict) -> str:
    bot_hp      = state.get("player_hp", 100)
    avg_npc_hp  = state.get("avg_npc_hp", 60)
    npcs_alive  = state.get("npcs_alive", 5)
    bot_moving  = state.get("bot_moving", True)
    nearest     = state.get("nearest_dist", 10)

    return (
        f"You control a squad of {npcs_alive} NPC soldiers fighting an enemy bot.\n"
        f"Current situation:\n"
        f"  - Enemy bot HP: {bot_hp:.0f}%\n"
        f"  - Average NPC HP: {avg_npc_hp:.0f}%\n"
        f"  - NPCs alive: {npcs_alive}/5\n"
        f"  - Bot is moving: {bot_moving}\n"
        f"  - Nearest NPC to bot: {nearest:.1f} units away\n\n"
        f"Choose the best tactic for your squad RIGHT NOW.\n"
        f"Reply with EXACTLY ONE DIGIT and absolutely nothing else:\n"
        f"0 = Surround (encircle the bot from all angles)\n"
        f"1 = Aggressive (rush the bot and attack)\n"
        f"2 = Flank (approach from the sides)\n"
        f"3 = Retreat (pull back and preserve NPC health)\n\n"
        f"Your answer (one digit only):"
    )

# ─────────────────────────────────────────────────────
# QUERY MISTRAL
# ─────────────────────────────────────────────────────

def query_mistral(prompt: str, previous_tag: int) -> int:
    payload = {
        "model": MODEL_NAME,
        "prompt": prompt,
        "stream": False,
        "options": {
            "num_predict": 1,
            "temperature": 0.1,
            "top_p": 0.9
        }
    }

    try:
        response = requests.post(
            OLLAMA_URL,
            json=payload,
            timeout=TIMEOUT_SECS
        )
        response.raise_for_status()

        data       = response.json()
        raw_output = data.get("response", "").strip()

        print(f"  [Mistral raw output]: '{raw_output}'")

        # Take the first character only
        if len(raw_output) > 0 and raw_output[0] in "0123":
            return int(raw_output[0])
        else:
            print(f"  [Warn] Unexpected output. Keeping previous tag: {previous_tag}")
            return previous_tag

    except requests.exceptions.Timeout:
        print(f"  [Warn] Mistral timed out after {TIMEOUT_SECS}s. "
              f"Keeping previous tag: {previous_tag}")
        return previous_tag

    except Exception as e:
        print(f"  [Error] Ollama request failed: {e}. "
              f"Keeping previous tag: {previous_tag}")
        return previous_tag

# ─────────────────────────────────────────────────────
# FILE HELPERS
# ─────────────────────────────────────────────────────

def read_game_state(path: str) -> dict:
    try:
        with open(path, "r") as f:
            return json.load(f)
    except Exception as e:
        print(f"  [Error] Could not read game_state.json: {e}")
        return {}

def write_strategy_tag(path: str, tag: int):
    try:
        with open(path, "w") as f:
            json.dump({"tag": tag}, f)
    except Exception as e:
        print(f"  [Error] Could not write strategy_tag.json: {e}")

def log_decision(tag: int, state: dict):
    log_path = "strategy_decisions.csv"
    write_header = not os.path.exists(log_path)
    try:
        with open(log_path, "a") as f:
            if write_header:
                f.write("timestamp,tag,bot_hp,avg_npc_hp,"
                        "npcs_alive,bot_moving,nearest_dist\n")
            f.write(
                f"{time.time():.2f},{tag},"
                f"{state.get('player_hp', 0):.1f},"
                f"{state.get('avg_npc_hp', 0):.1f},"
                f"{state.get('npcs_alive', 0)},"
                f"{state.get('bot_moving', False)},"
                f"{state.get('nearest_dist', 0):.2f}\n"
            )
    except Exception as e:
        print(f"  [Error] Could not write strategy_decisions.csv: {e}")

# ─────────────────────────────────────────────────────
# MAIN LOOP
# ─────────────────────────────────────────────────────

def main():
    print("[LLM Sidecar] Starting...")
    print(f"[LLM Sidecar] Game state path:   {os.path.abspath(GAME_STATE_PATH)}")
    print(f"[LLM Sidecar] Strategy tag path: {os.path.abspath(STRATEGY_TAG_PATH)}")

    # Check paths are not the placeholder
    if "PASTE_" in GAME_STATE_PATH or "PASTE_" in STRATEGY_TAG_PATH:
        print()
        print("[ERROR] You have not set the file paths in llm_strategy.py.")
        print("  1. Open Unity, press Play with StrategyBridge trainingMode = false")
        print("  2. Look at the Console for '[StrategyBridge] Game state path:'")
        print("  3. Copy that path into GAME_STATE_PATH in this script")
        print("  4. Do the same for STRATEGY_TAG_PATH")
        print("  Exiting.")
        return

    # Verify Ollama is reachable before starting the loop
    print("[LLM Sidecar] Checking Ollama connection...")
    try:
        test = requests.get("http://localhost:11434", timeout=5)
        print("[LLM Sidecar] Ollama is reachable.")
    except Exception:
        print("[ERROR] Cannot reach Ollama at http://localhost:11434.")
        print("  Make sure Ollama is running. On Windows it may be in the system tray.")
        print("  Try running: ollama serve")
        print("  Exiting.")
        return

    current_tag      = 0
    last_modified    = 0.0
    tag_names        = ["SURROUND", "AGGRESSIVE", "FLANK", "RETREAT"]

    # Write initial tag so Unity always has a file to read
    write_strategy_tag(STRATEGY_TAG_PATH, current_tag)
    print(f"[LLM Sidecar] Initial tag written: {current_tag} ({tag_names[current_tag]})")
    print("[LLM Sidecar] Watching for game_state.json changes...")
    print()

    while True:
        try:
            if os.path.exists(GAME_STATE_PATH):
                modified = os.path.getmtime(GAME_STATE_PATH)

                if modified != last_modified:
                    last_modified = modified

                    state = read_game_state(GAME_STATE_PATH)

                    if state:
                        print(f"[LLM Sidecar] Game state update detected.")
                        print(f"  Bot HP: {state.get('player_hp', '?'):.0f}%  "
                              f"Avg NPC HP: {state.get('avg_npc_hp', '?'):.0f}%  "
                              f"Alive: {state.get('npcs_alive', '?')}/5  "
                              f"Time: {state.get('time_elapsed', '?'):.0f}s")
                        print(f"[LLM Sidecar] Querying Mistral...")

                        new_tag = query_mistral(
                            build_prompt(state), current_tag)

                        if new_tag != current_tag:
                            print(f"  Strategy changed: "
                                  f"{tag_names[current_tag]} → {tag_names[new_tag]}")
                            current_tag = new_tag
                        else:
                            print(f"  Strategy unchanged: {tag_names[current_tag]}")

                        write_strategy_tag(STRATEGY_TAG_PATH, current_tag)
                        log_decision(current_tag, state)
                        print()

            time.sleep(POLL_INTERVAL)

        except KeyboardInterrupt:
            print("\n[LLM Sidecar] Shutting down cleanly.")
            break

if __name__ == "__main__":
    main()