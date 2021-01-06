# G733_windows_app
Allow to disable lights, change sidetone and check battery without logitech "full feature" drivers which are incompatible with Dolby Atmos

### Some reverse ...

#### lights

Off
11ff 043a01 00 0000000000000000000000000000

Fixed, white (255, 255, 255)
11ff 043a01 01 ffffff 0200000000000000000000

Fixed, black (0, 0, 0)
11ff 043a01 01 000000 0200000000000000000000

Fixed, Red-ish (244, 25, 29)
11ff 043a01 01 e60203 0200000000000000000000

Breathing, white, 5000ms, 100% brightness
11ff 043a01 02 ffffff 13 88 00 64 00000000000000

0x1388 = 0d5000
0x64 = 0d100

Breathing, white, 5000ms, 50% brightness
11ff 043a01 02 ffffff 13 88 00 32 00000000000000

0x1388 = 0d5000
0x32 = 0d050

Breathing, white, 5000ms, 78% brightness
11ff 043a01 02 ffffff 13 88 00 4e 00000000000000

0x1388 = 0d5000
0x4e = 0d78

Breathing, white, 1000ms, 100% brightness
11ff 043a01 02 ffffff 03 e8 00 64 00000000000000

0x03e8 = 0d1000
0x64 = 0d100

Breathing, white, 20000ms, 100% brightness
11ff 043a01 02 ffffff 4e 20 00 64 00000000000000

0x4e20 = 0d20000
0x64 = 0d100

Send Sidetone, 100
11ff 071e 64 000000000000000000000000000000

0x64 = 0d100

Send Sidetone, 68
11ff 071e 44 000000000000000000000000000000

0x44 = 0d68

Battery ??

Rq    .0  -> 11ff 08 0b 0000 00 00000000000000000000000000
Rsp   .3  <- 11ff 08 0b 1045 01 00000000000000000000000000

1st try :
11ff 08 00 105a 03 00000000000000000000000000 Plugged in
11ff 08 00 107e 07 00000000000000000000000000 On charge
11ff 08 00 1046 01 00000000000000000000000000 Unplugged

2nd try :
11ff 08 00 105a 03 00000000000000000000000000 Plugged in
11ff 08 00 107f 07 00000000000000000000000000 On charge
11ff 08 00 1045 01 00000000000000000000000000 Unplugged

0x105a = 0d4186 --> 4086mV
0x107f = 0d4223 --> 4223mV
0x1045 = 0d4165 --> 4165mV

Needs to be converted in %

Battery update :
read req : 11ff 08 0f 00000000000000000000000000000000
read rsp : 11ff 08 0f 1042 01 00000000000000000000000000

01 : Unplugged
03 : Just plugged
07 : Charging

Wireshark filters :
usb.bInterfaceClass == HID &&  (usb.dst == "1.23.0" || usb.src == "1.23.0")
usb.src == "1.23.0" || usb.dst == "1.23.0"  || usb.src == "1.23.1" || usb.dst == "1.23.1"
usb.src == "1.24.0" || usb.dst == "1.24.0"  || usb.src == "1.24.1" || usb.dst == "1.24.1"  || usb.src == "1.24.2" || usb.dst == "1.24.2"  || usb.src == "1.24.3" || usb.dst == "1.24.3"

.0 -> HID commands write
.1 -> Audio Stream ??
.3 -> HID commands read (+ interrupts ?)

Timeout poweroff :

write : 11ff 08 2f 01 000000000000000000000000000000
resp  : OK ? (resp NULL)
read  : 11ff 08 1f 00 000000000000000000000000000000
resp  : 11ff 08 1f 01 000000000000000000000000000000
