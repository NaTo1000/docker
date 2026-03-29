# BlackFlip v1.0.0

> **BlackArch Linux × Flipper Zero × Raspberry Pi 5**

BlackFlip is a Docker image that packages the BlackArch penetration-testing
tool suite together with Flipper Zero connectivity utilities, WiFi tooling,
and a full XFCE desktop—all themed in the signature BlackArch dark style.

---

## Quick start

```bash
# CLI mode (no GUI)
docker build -t blackflip ./blackflip
docker run -it --privileged \
    -v /dev/bus/usb:/dev/bus/usb \
    blackflip

# GUI mode via VNC
docker run -it --privileged \
    -v /dev/bus/usb:/dev/bus/usb \
    -e BLACKFLIP_GUI=1 \
    -p 5901:5901 \
    blackflip
```

Connect a VNC client to `localhost:5901` (default password: `blackflip`).

### Raspberry Pi 5

Build natively on the Pi or cross-build with `docker buildx`:

```bash
docker buildx build --platform linux/arm64 -t blackflip ./blackflip
```

---

## What is included

### BlackArch tool groups

| Group                    | Description                          |
|--------------------------|--------------------------------------|
| `blackarch-wireless`     | WiFi auditing & attack tools         |
| `blackarch-bluetooth`    | Bluetooth analysis tools             |
| `blackarch-sniffer`      | Network packet sniffers              |
| `blackarch-exploitation` | Exploitation frameworks              |
| `blackarch-recon`        | Reconnaissance & OSINT tools         |
| `blackarch-scanner`      | Vulnerability scanners               |
| `blackarch-radio`        | SDR / radio-frequency tools          |
| `blackarch-forensic`     | Digital forensics tools              |

### WiFi toolkit

aircrack-ng · wifite · iw · wireless_tools · iwd · hostapd · dnsmasq ·
nmap · wireshark-cli · tcpdump · net-tools · macchanger · hcxtools ·
hcxdumptool

### Flipper Zero integration

| Tool / resource          | Purpose                                             |
|--------------------------|-----------------------------------------------------|
| **ufbt**                 | Micro Flipper Build Tool – build & flash firmware    |
| **flipperzero-firmware** | Official firmware source (cloned to `/opt`)          |
| **qFlipper**             | Desktop companion app source (cloned to `/opt`)      |
| **pyserial**             | Python serial library for Flipper CLI over USB       |
| **screen / minicom / picocom** | Terminal emulators for serial communication    |

The entrypoint automatically detects a connected Flipper Zero on USB and
exports `FLIPPER_SERIAL` for use by scripts and tools.

### Desktop environment

- **XFCE 4** with **Arc-Dark** GTK theme and **Papirus-Dark** icons
- **Hack** monospace font throughout
- Solid dark (#0D0D0D) background
- Accessible over VNC on port `5901`

---

## Environment variables

| Variable                 | Default      | Description                            |
|--------------------------|-------------|----------------------------------------|
| `BLACKFLIP_GUI`          | `0`         | Set to `1` to start XFCE over VNC      |
| `VNC_PASSWORD`           | `blackflip` | VNC session password                    |
| `BLACKFLIP_FLIPPER_AUTO` | `1`         | Auto-detect Flipper Zero on USB         |
| `BLACKFLIP_VERSION`      | `1.0.0`     | Shown in the splash banner              |

---

## Flipper Zero usage

```bash
# Interactive serial console (if Flipper is connected)
picocom -b 230400 "$FLIPPER_SERIAL"

# Build a Flipper app with ufbt
cd /opt/flipperzero-firmware
ufbt create APPID=my_app
ufbt

# Flash firmware
ufbt flash_usb
```

---

## Building from source

```bash
cd blackflip/
docker build -t blackflip .
```

To build specifically for Raspberry Pi 5 (arm64) from a different host:

```bash
docker buildx create --use
docker buildx build --platform linux/arm64 -t blackflip --load .
```

---

## License

BlackFlip image definition is provided under the same license as this
repository.  Individual tools retain their own licenses (BlackArch, Flipper
Zero firmware, etc.).
