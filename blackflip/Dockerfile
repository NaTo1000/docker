# BlackFlip — BlackArch Linux + Flipper Zero suite for Raspberry Pi 5
#
# A Docker image that bundles BlackArch penetration-testing tools with
# Flipper Zero connectivity utilities, WiFi tooling, and an XFCE desktop
# themed in the BlackArch style.  Designed for arm64 (Raspberry Pi 5) but
# builds on any architecture supported by the base image.

FROM archlinux:base-devel

LABEL maintainer="BlackFlip Contributors"
LABEL description="BlackFlip: BlackArch Linux + Flipper Zero full suite for Raspberry Pi 5"
LABEL version="1.0.0"

# ── Environment ──────────────────────────────────────────────────────────
ENV BLACKFLIP_VERSION=1.0.0
ENV TERM=xterm-256color
ENV LANG=en_US.UTF-8
ENV LC_ALL=en_US.UTF-8

# ── Base system update ───────────────────────────────────────────────────
# NOTE: sudo is required here — the 'blackflip' interactive user needs
# privilege escalation to run security tools that require root.
RUN pacman -Syu --noconfirm && \
    pacman -S --noconfirm --needed \
        git \
        curl \
        wget \
        sudo \
        vim \
        nano \
        htop \
        tmux \
        python \
        python-pip \
        python-setuptools \
        usbutils \
        unzip && \
    pacman -Scc --noconfirm

# ── BlackArch repository ────────────────────────────────────────────────
# Add the official BlackArch repository keyring and mirror list.
RUN curl -sL https://blackarch.org/strap.sh -o /tmp/strap.sh && \
    chmod +x /tmp/strap.sh && \
    /tmp/strap.sh && \
    rm /tmp/strap.sh

# ── BlackArch core tool groups ──────────────────────────────────────────
# Install selected BlackArch tool categories (full suite would be ~70 GB;
# we pick the groups most relevant to Flipper Zero + WiFi work).
RUN pacman -S --noconfirm --needed \
        blackarch-wireless \
        blackarch-bluetooth \
        blackarch-sniffer \
        blackarch-exploitation \
        blackarch-recon \
        blackarch-scanner \
        blackarch-radio \
        blackarch-forensic && \
    pacman -Scc --noconfirm

# ── WiFi / Networking tools ─────────────────────────────────────────────
RUN pacman -S --noconfirm --needed \
        aircrack-ng \
        wifite \
        iw \
        wireless_tools \
        iwd \
        hostapd \
        dnsmasq \
        nmap \
        wireshark-cli \
        tcpdump \
        net-tools \
        macchanger \
        hcxtools \
        hcxdumptool && \
    pacman -Scc --noconfirm

# ── Flipper Zero tools ──────────────────────────────────────────────────
# ufbt  — Micro Flipper Build Tool (firmware development SDK)
# Serial / USB utilities for communicating with the Flipper Zero over USB
RUN pacman -S --noconfirm --needed \
        python-pyserial \
        screen \
        minicom \
        picocom && \
    pip install --break-system-packages ufbt && \
    pacman -Scc --noconfirm

# Clone the official Flipper Zero firmware repository for reference /
# local builds and the Flipper Zero CLI tools.
RUN git clone --depth 1 https://github.com/flipperdevices/flipperzero-firmware.git \
        /opt/flipperzero-firmware

# qFlipper companion app (built from source when the host is arm64)
RUN git clone --depth 1 https://github.com/flipperdevices/qFlipper.git \
        /opt/qFlipper

# ── XFCE desktop + BlackArch theming ────────────────────────────────────
RUN pacman -S --noconfirm --needed \
        xfce4 \
        xfce4-goodies \
        xfce4-terminal \
        xorg-server \
        xorg-xinit \
        xorg-xrandr \
        tigervnc \
        arc-gtk-theme \
        papirus-icon-theme \
        ttf-dejavu \
        ttf-hack \
        dbus && \
    pacman -Scc --noconfirm

# ── BlackArch / BlackFlip theme configuration ───────────────────────────
# Create the default user and configure XFCE with a dark BlackArch look.
RUN useradd -m -G wheel -s /bin/bash blackflip && \
    echo "blackflip:blackflip" | chpasswd && \
    echo "%wheel ALL=(ALL) NOPASSWD: ALL" >> /etc/sudoers

# XFCE settings for dark theme
RUN mkdir -p /home/blackflip/.config/xfce4/xfconf/xfce-perchannel-xml && \
    chown -R blackflip:blackflip /home/blackflip/.config

COPY config/xsettings.xml /home/blackflip/.config/xfce4/xfconf/xfce-perchannel-xml/xsettings.xml
COPY config/xfce4-panel.xml /home/blackflip/.config/xfce4/xfconf/xfce-perchannel-xml/xfce4-panel.xml
COPY config/xfce4-desktop.xml /home/blackflip/.config/xfce4/xfconf/xfce-perchannel-xml/xfce4-desktop.xml

RUN chown -R blackflip:blackflip /home/blackflip/.config

# ── BlackFlip splash / title screen ─────────────────────────────────────
COPY blackflip-splash.sh /usr/local/bin/blackflip-splash
RUN chmod +x /usr/local/bin/blackflip-splash

# Add splash to login profile so every shell session shows the banner
RUN echo '/usr/local/bin/blackflip-splash' >> /home/blackflip/.bashrc

# ── Entrypoint ───────────────────────────────────────────────────────────
COPY blackflip-entrypoint.sh /usr/local/bin/blackflip-entrypoint
RUN chmod +x /usr/local/bin/blackflip-entrypoint

# VNC port
EXPOSE 5901

# USB device access (Flipper Zero)
# Run with: docker run --privileged -v /dev/bus/usb:/dev/bus/usb ...

USER blackflip
WORKDIR /home/blackflip

ENTRYPOINT ["blackflip-entrypoint"]
CMD ["bash"]
