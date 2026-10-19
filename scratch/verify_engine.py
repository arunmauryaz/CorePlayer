import os
import subprocess
import time
from PIL import ImageGrab

def main():
    exe_path = r"d:\CORE PLAYER\src\CorePlayer\bin\Debug\net8.0\CorePlayer.exe"
    target_screenshot = r"C:\Users\golde\.gemini\antigravity\brain\eb4a57e0-ff95-4222-9c63-37b9f9db21d0\preview.png"
    backup_screenshot = r"d:\CORE PLAYER\preview_ui.png"

    print(f"Launching CorePlayer: {exe_path}")
    proc = subprocess.Popen([exe_path])

    try:
        # Give UI and libmpv-2.dll time to initialize and render
        time.sleep(4)

        # Check if process is still running
        if proc.poll() is not None:
            raise RuntimeError(f"CorePlayer exited prematurely with return code {proc.returncode}")

        print(f"CorePlayer process running (PID: {proc.pid})")

        # Verify loaded modules using PowerShell Get-Process
        ps_cmd = f"Get-Process -Id {proc.pid} -Module | Select-Object -ExpandProperty ModuleName"
        res = subprocess.run(["powershell", "-NoProfile", "-Command", ps_cmd], capture_output=True, text=True)
        modules = res.stdout.splitlines()

        mpv_loaded = any("mpv" in m.lower() for m in modules)
        print(f"Loaded modules checked. Found libmpv/mpv: {mpv_loaded}")
        for m in modules:
            if "mpv" in m.lower():
                print(f"  -> Found module: {m.strip()}")

        # Capture screenshot
        img = ImageGrab.grab()
        os.makedirs(os.path.dirname(target_screenshot), exist_ok=True)
        img.save(target_screenshot)
        img.save(backup_screenshot)
        print(f"Screenshot successfully saved to: {target_screenshot}")
        print(f"Backup screenshot saved to: {backup_screenshot}")

    finally:
        print("Terminating CorePlayer process...")
        proc.terminate()
        try:
            proc.wait(timeout=3)
        except subprocess.TimeoutExpired:
            proc.kill()
        print("Process terminated cleanly.")

if __name__ == "__main__":
    main()
