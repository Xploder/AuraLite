# ASUS Aura compatibility

AuraLite talks to the motherboard's **internal USB HID lighting controller**. The chipset, PCI device IDs, and the Aura Sync branding alone do not identify this controller. Board revisions and firmware can differ.

## Controller families

| ASUS USB ID | AuraLite support | Evidence |
| --- | --- | --- |
| `0B05:19AF` | Candidate for ARGB control. AuraLite verifies the HID descriptor, firmware reply, and zone configuration before enabling control. Static lighting and hardware color cycle were verified on firmware `AULA3-AR32-0309`. | [frugalRGB driver](https://github.com/emaspa/frugalRGB/blob/main/crates/frugalrgb-core/src/controllers/aura.rs), [Hypercolor capture](https://github.com/hyperb1iss/hypercolor/blob/main/docs/specs/24-asus-aura-protocol-driver.md) |
| `0B05:18F3`, `0B05:1939` | Related controller families. The developer probe can list their HID metadata. Lighting control is disabled. | [liquidctl Aura guide](https://github.com/liquidctl/liquidctl/blob/main/docs/asus-aura-led-guide.md), [frugalRGB device notes](https://github.com/emaspa/frugalRGB#supported-devices) |
| `0B05:1AA6`, `0B05:1BED` | Related IDs registered by OpenRGB. The developer probe can list their HID metadata. Lighting control is disabled. | [OpenRGB detector log](https://gitlab.com/CalcProgrammer1/OpenRGB/-/work_items/5755) |

## Motherboards reported with these controllers

These are documented examples. Verify the hardware ID on your system even if your model is listed.

| ASUS motherboard | Reported USB ID | AuraLite status |
| --- | --- | --- |
| ProArt Z690-Creator WiFi | `0B05:19AF` | Candidate. [liquidctl board report](https://github.com/liquidctl/liquidctl/blob/main/docs/asus-aura-led-guide.md) |
| ROG Strix Z790-A Gaming WiFi II | `0B05:19AF` | Candidate. HID descriptor and protocol captured by [Hypercolor](https://github.com/hyperb1iss/hypercolor/blob/main/docs/specs/24-asus-aura-protocol-driver.md). |
| TUF Gaming B760M-BTF WiFi D4 | `0B05:19AF` | Candidate. [OpenRGB hardware report](https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/4990) |
| TUF Gaming H570-Pro | `0B05:19AF` | Candidate. [OpenRGB hardware report](https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/1390) |
| ROG Maximus Z690 Formula | `0B05:18F3` | Related controller, lighting control disabled. [liquidctl board report](https://github.com/liquidctl/liquidctl/blob/main/docs/asus-aura-led-guide.md) |
| ROG Strix B550-F Gaming (Wi-Fi) II | `0B05:1939` | Related controller, lighting control disabled. [OpenRGB hardware report](https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/4752) |

## Check your board in Windows

1. Open **Device Manager**. Find **AURA LED Controller** or its vendor-defined HID device.
2. In **Properties → Details → Hardware Ids**, check for `VID_0B05&PID_19AF`. The HID collection also reports `HID\VID_0B05&UP:FF72_U:00A1`. A composite controller can show a separate WinUSB entry and a HID entry. AuraLite only uses the HID entry.
3. Run AuraLite to check the firmware and ARGB channels. The status and **Detect controller** button are at the bottom. A matching USB ID is a candidate. Successful detection means the HID descriptor and both protocol queries were accepted. If AuraLite reports that lighting is unavailable, the firmware did not provide an unambiguous ARGB channel layout.

The following built-in PowerShell command lists connected ASUS HID collections with the expected usage. It only reads Windows device metadata. See [Microsoft's PnPUtil examples](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-examples).

```powershell
pnputil /enum-devices /connected /deviceid 'HID\VID_0B05&UP:FF72_U:00A1' /deviceids
```

AuraLite requires report ID `EC` in both directions, 65-byte input and output reports, and a valid firmware and configuration response. The Windows HID descriptor identifies the collection independently of its USB interface number. Multiple matching collections are treated as ambiguous. The developer probe's `list` command enumerates known related HID devices without sending Aura commands.

AuraLite controls ARGB channels exposed by the supported USB controller. Aura-branded GPUs, memory, laptops, standalone controllers, SMBus lighting and fixed RGB zones are outside this driver. Firmware effects may differ even between boards with the same USB ID.
