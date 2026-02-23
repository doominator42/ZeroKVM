### 901-usb-gadget-support-vendor-descriptors.patch

Note: This patch is not required for ZeroKVM to work, but the USB bandwidth might be limited by the host driver if absent.

This patch changes the FunctionFS gadget to make it accept any USB vendor descriptor and forwards the requests to userland. It is required to define the special vendor descriptor (`bDescriptorType = 0x5f`) used by DisplayLink devices.
