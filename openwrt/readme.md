# Install on OpenWRT

- OpenWRT 25.12-rc2 or later is required
- For Raspberry Pi boards
  - Add the following to `/boot/config.txt` to configure the USB port in device mode
    ```
    dtoverlay=dwc2,dr_mode=peripheral
    ```
  - See [brcmfmac-rpi-zero-2-w](./brcmfmac-rpi-zero-2-w.md) to fix WiFi
- Install the following packages
  - luci-ssl
  - kmod-usb-dwc2
  - kmod-usb-gadget-fs
  - kmod-usb-gadget-hid
- Copy the built `zerokvm` binary into `/usr/bin/`
- Copy `init.d/zerokvm` into `/etc/init.d/`
- TODO: change LuCI port to 8443
- Run the following to enable and start the service
  ```shell
  chmod 755 /etc/init.d/zerokvm
  service zerokvm enable
  service zerokvm start
  ```
