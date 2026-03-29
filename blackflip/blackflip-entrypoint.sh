#!/usr/bin/env bash
# BlackFlip entrypoint — shows the splash screen and starts services.
set -e

# ── Display the BlackFlip title screen ───────────────────────────────────
/usr/local/bin/blackflip-splash

# ── Optional: start a VNC-accessible XFCE session ───────────────────────
if [ "${BLACKFLIP_GUI:-0}" = "1" ]; then
    echo -e "\033[1;31m[BlackFlip]\033[0m Starting XFCE desktop on VNC :1 ..."
    export DISPLAY=:1

    # Ensure the VNC password directory exists
    mkdir -p "$HOME/.vnc"
    # Set VNC password — generate a random one if the user did not provide one
    if [ ! -f "$HOME/.vnc/passwd" ]; then
        if [ -z "${VNC_PASSWORD:-}" ]; then
            VNC_PASSWORD=$(head -c 12 /dev/urandom | base64 | tr -dc 'a-zA-Z0-9' | head -c 12)
            echo -e "\033[1;33m[BlackFlip]\033[0m Generated VNC password: ${VNC_PASSWORD}"
            echo -e "\033[1;33m[BlackFlip]\033[0m Set VNC_PASSWORD env var to use your own."
        fi
        echo "$VNC_PASSWORD" | vncpasswd -f > "$HOME/.vnc/passwd"
        chmod 600 "$HOME/.vnc/passwd"
    fi

    # Write an xstartup that launches XFCE
    cat > "$HOME/.vnc/xstartup" <<'XSTARTUP'
#!/bin/sh
unset SESSION_MANAGER
unset DBUS_SESSION_BUS_ADDRESS
exec startxfce4
XSTARTUP
    chmod +x "$HOME/.vnc/xstartup"

    # Launch the VNC server in the background
    vncserver :1 -geometry 1920x1080 -depth 24 -localhost no
    echo -e "\033[1;31m[BlackFlip]\033[0m VNC desktop running — connect to port 5901"
fi

# ── Optional: auto-detect Flipper Zero on USB ────────────────────────────
if [ "${BLACKFLIP_FLIPPER_AUTO:-1}" = "1" ]; then
    if lsusb 2>/dev/null | grep -qi "0483:5740"; then
        FLIPPER_DEV=$(find /dev -name 'ttyACM*' 2>/dev/null | head -1)
        if [ -n "$FLIPPER_DEV" ]; then
            echo -e "\033[1;31m[BlackFlip]\033[0m Flipper Zero detected on $FLIPPER_DEV"
            export FLIPPER_SERIAL="$FLIPPER_DEV"
        fi
    fi
fi

# ── Hand off to the user command ─────────────────────────────────────────
exec "$@"
