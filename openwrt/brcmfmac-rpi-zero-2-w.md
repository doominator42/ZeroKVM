OpenWRT does not provide the WiFi firmware for the Raspberry Pi Zero 2W because of license issues. To use WiFi, open the SD card root filesystem from another computer and run the following to download the firmware files.
```shell
# Make sure the current directory is lib/firmware/brcm on the OpenWRT rootfs
cd lib/firmware/brcm
sh ./download-brcmfmac43436.sh
```

The WiFi chip is different depending on the board revision. My RPi Zero 2W v1.19 had a bug causing WiFi authentication to fail on my Ubiquiti AP. I fixed it by adding the following to `/etc/modules.conf`.
```conf
options brcmfmac feature_disable=0x82000
```
