#!/usr/bin/env bash
# BlackFlip title / splash screen — displayed at login and container start.

RED='\033[1;31m'
WHT='\033[1;37m'
GRY='\033[0;90m'
RST='\033[0m'

cat <<'SPLASH'

 ██████╗ ██╗      █████╗  ██████╗██╗  ██╗███████╗██╗     ██╗██████╗
 ██╔══██╗██║     ██╔══██╗██╔════╝██║ ██╔╝██╔════╝██║     ██║██╔══██╗
 ██████╔╝██║     ███████║██║     █████╔╝ █████╗  ██║     ██║██████╔╝
 ██╔══██╗██║     ██╔══██║██║     ██╔═██╗ ██╔══╝  ██║     ██║██╔═══╝
 ██████╔╝███████╗██║  ██║╚██████╗██║  ██╗██║     ███████╗██║██║
 ╚═════╝ ╚══════╝╚═╝  ╚═╝ ╚═════╝╚═╝  ╚═╝╚═╝     ╚══════╝╚═╝╚═╝

SPLASH

echo -e "${RED}      ╔══════════════════════════════════════════════════╗${RST}"
echo -e "${RED}      ║${WHT}  B L A C K F L I P   v${BLACKFLIP_VERSION:-1.0.0}                  ${RED}║${RST}"
echo -e "${RED}      ║${GRY}  BlackArch Linux  ×  Flipper Zero  ×  RPi 5       ${RED}║${RST}"
echo -e "${RED}      ╚══════════════════════════════════════════════════╝${RST}"
echo ""
echo -e "${GRY}  ┌─────────────────────────────────────────────────────┐${RST}"
echo -e "${GRY}  │${WHT}   ┌──────┐   Flipper Zero Ready                     ${GRY}│${RST}"
echo -e "${GRY}  │${WHT}   │ ◉  ◉ │   WiFi toolkit loaded                    ${GRY}│${RST}"
echo -e "${GRY}  │${WHT}   │  ──  │   BlackArch full suite                    ${GRY}│${RST}"
echo -e "${GRY}  │${WHT}   └──────┘   Type 'blackflip-help' for commands     ${GRY}│${RST}"
echo -e "${GRY}  └─────────────────────────────────────────────────────┘${RST}"
echo ""
