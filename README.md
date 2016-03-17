## Introduction

A command line utility to control [Adafruit Bluefruit LE Friend](https://www.adafruit.com/product/2267) (USB BLE beacon)

## Getting Started

Get device info
```
bluefruit --info
```

Start advertising as **iBeacon**
```
bluefruit --uuid f7826da64fa24e988024bc5b71e08901 --major 11061 --minor 425
```

Start advertising as **Eddystone URL** beacon
```
bluefruit --eddystone-url http://adafruit.com
```

## Command line arguments

```
Bluefruit v0.7.5920

Usage:
  bluefruit [options]

Options:
  --help                   This message
  --verbose                Verbose mode
  --port [COMx]            COM port name or 'auto'
  --at [string]            AT command string
  --uuid [uuid]            iBeacon UUID
  --major [value]          iBeacon major
  --minor [value]          iBeacon minor
  --name [string]          Friendly device name
  --eddystone-url [url]    Set Eddystone URL advertising
  --device-address         Get device BLE address
  --peer-address           Get connected peer BLE address
  --rssi                   Get RSSI in dBm
  --power [value]          Set power level in dBm or 'min' or 'max'
  --power                  Get power level
  --echo [on|off]          Set echo on or off
  --echo                   Get echo flag
  --test                   Test if device ready
  --info                   Read device info
  --reset                  Reset the device
  --factory-reset          Reset to factory defaults

Examples:
  bluefruit --port COM7 --verbose --at AT+HELP
  bluefruit --uuid f7826da64fa24e988024bc5b71e08901 --major 11061 --minor 425
  bluefruit --port auto --power max --name "Bluefruit 360"
  bluefruit --verbose --info
```

## Bluefruit References

* [Bluefruit LE Friend - Bluetooth Low Energy (BLE 4.0) - nRF51822 - v2.0](https://www.adafruit.com/products/2267) on adafruit.com
* [AT commands and settings](https://learn.adafruit.com/introducing-adafruit-ble-bluetooth-low-energy-friend?view=all)