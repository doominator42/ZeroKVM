#!/bin/sh
set -e

# This script downloads the proprietary firmware for the WiFi chip of the Raspberry Pi Zero 2W
# Files are extracted into the current directory, which should be /lib/firmware/brcm on the OpenWRT rootfs

firmware_repo_base="https://raw.githubusercontent.com/RPi-Distro/firmware-nonfree/refs/heads/bookworm/debian/config/brcm80211/brcm"

curl -s \
	-O "$firmware_repo_base/brcmfmac43436-sdio.bin" \
	-O "$firmware_repo_base/brcmfmac43436-sdio.txt" \
	-O "$firmware_repo_base/brcmfmac43436-sdio.clm_blob" \
	-O "$firmware_repo_base/brcmfmac43436s-sdio.bin" \
	-O "$firmware_repo_base/brcmfmac43436s-sdio.txt" \
	-O "$firmware_repo_base/brcmfmac43436s-sdio.nolpo.txt"

ln -f -s ./brcmfmac43436-sdio.bin ./brcmfmac43430b0-sdio.raspberrypi,model-zero-2-w.bin
ln -f -s ./brcmfmac43436-sdio.clm_blob ./brcmfmac43430b0-sdio.raspberrypi,model-zero-2-w.clm_blob
ln -f -s ./brcmfmac43436-sdio.txt ./brcmfmac43430b0-sdio.raspberrypi,model-zero-2-w.txt
ln -f -s ./brcmfmac43436-sdio.bin ./brcmfmac43436-sdio.raspberrypi,model-zero-2-w.bin
ln -f -s ./brcmfmac43436-sdio.clm_blob ./brcmfmac43436-sdio.raspberrypi,model-zero-2-w.clm_blob
ln -f -s ./brcmfmac43436-sdio.txt ./brcmfmac43436-sdio.raspberrypi,model-zero-2-w.txt
ln -f -s ./brcmfmac43436s-sdio.bin ./brcmfmac43436s-sdio.raspberrypi,model-zero-2-w.bin
ln -f -s ./brcmfmac43436s-sdio.txt ./brcmfmac43436s-sdio.raspberrypi,model-zero-2-w.txt
