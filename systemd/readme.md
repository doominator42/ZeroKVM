# Install on Debian as a SystemD service

- For Raspberry Pi boards, add the following to `/boot/firmware/config.txt` to configure the USB port in device mode
  ```
  # Under the section [all]
  dtoverlay=dwc2,dr_mode=peripheral
  ```
- Copy the built `zerokvm` binary into `/usr/bin/`
- Copy `usb-gadget.conf` into `/etc/modules-load.d/`
- Copy `zerokvm.service` into `/lib/systemd/system/`
- Run the following to enable and start the service:
  ```shell
  systemctl daemon-reload
  systemctl enable zerokvm
  systemctl start zerokvm
  ```
