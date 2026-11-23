# HID-Protokoll und Quellen

| Referenz | Beobachtung | Lizenzhinweis |
|---|---|---|
| [frugalRGB, Aura-Controller](https://github.com/emaspa/frugalRGB/blob/main/crates/frugalrgb-core/src/controllers/aura.rs) | PID 19AF, 65 Byte, EC als Report-ID, Output-Reports. ARGB-Standbild über Direktmodus. Effekt-ID hängt davon ab, ob eine feste Zone gemeldet wird. Separate Speicheroption. | [AGPL-3.0-or-later](https://github.com/emaspa/frugalRGB/blob/main/Cargo.toml) |
| [liquidctl, Aura-Leitfaden](https://github.com/liquidctl/liquidctl/blob/main/docs/asus-aura-led-guide.md) und [Treiber](https://github.com/liquidctl/liquidctl/blob/main/liquidctl/driver/aura_led.py) | PID 19AF auf ProArt Z690. Gleiche Abfragen, permanentes Effektverhalten im dort getesteten Ablauf. Andere Kanalauswahl sowie zusätzliche Farb- und Commit-Reports. | Aura-Treiber GPL-3.0-or-later |
| [HaloDaemon, Aura-Protokoll](https://github.com/TimP4w/HaloDaemon-plugins/blob/main/asus_aura_usb/docs/protocol.md) | Gleiche Abfragen und Direktpakete. Zählt ARGB-Effektkanäle ab 1 und Direktausgänge ab 0. Native ARGB-Effekte über 3B. Der Raw-Transport stellt EC als erstes Byte bereit. | Das Dokument nennt OpenRGB GPL-2.0-or-later als Implementierungsreferenz. |
| [hypercolor, Mitschnitt und Deskriptor](https://github.com/hyperb1iss/hypercolor/blob/main/docs/specs/24-asus-aura-protocol-driver.md) | Meldet für 19AF auf einem Z790-Board Interface 2, Usage Page FF72 und EC als HID-Report-ID. | Protokollangaben als zusätzliche Evidenz. |

AuraLite implementiert dokumentierte Schnittstellen und Paketformate eigenständig mit C# und Windows-HID-APIs. Der Quellbaum enthält keinen übernommenen Implementierungscode der Referenzprojekte. WinUI und die bereitgestellte .NET-Laufzeit stammen von Microsoft. Das App-Paket enthält die von der Laufzeit mitgelieferten Lizenz- und Hinweistexte, soweit diese in den Publish-Ausgaben enthalten sind.

## Freigegebene Paketformen

Alle Frames haben 65 Byte einschließlich der Report-ID. Unbelegte Bytes sind null. Windows bestätigt Report-ID und Länge für Eingabe und Ausgabe vor den Abfragen. Die Anwendungslogik akzeptiert nur genau eine passende HID-Schnittstelle mit den erwarteten VID/PID- und Usage-Werten und prüft die Geräteeigenschaften auf dem exklusiv geöffneten Handle erneut. [Windows HIDP_CAPS](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidpi/ns-hidpi-_hidp_caps)

| Zweck | Ausgabe | Erwartung |
|---|---|---|
| Firmware | `EC 82` | `EC 02`, 16 Byte Firmwarefeld ab Offset 2 |
| Konfiguration | `EC B0` | `EC 30`, 60 Byte ab Offset 4. ARGB-Zahl bei absolutem Offset 6. |
| Statisch, Modus setzen | `EC 35 <Kanal> 00 00 FF` | Kanal zählt ab 1, nur wenn die Konfiguration eine feste Zone meldet und beide primären Kanalmodelle damit übereinstimmen. |
| Statisch, LEDs setzen | `EC 40 <Ausgang+Flag> <Offset> <Anzahl> <RGB …>` | Ausgang zählt ab 0. Bis 20 RGB-Tripel pro Report. Nur der letzte Frame setzt Bit 7. |
| Hardwareeffekt testen | `EC 35 <Kanal> 00 00 <04 oder 05>` | Setzt den in frugalRGB beschriebenen Modus. Sichtprüfung erforderlich. |

Die als RGB-Zahl angezeigte Diagnose folgt dem Feld aus liquidctl. frugalRGB benutzt für feste RGB-Header zusätzlich ein eigenes Feld. AuraLite adressiert nur ARGB-Header. Die LED-Anzahl wird aus Nutzereingaben übernommen, da die Referenzen die Config-Werte dafür unterschiedlich interpretieren.

Das Testprogramm hält die freigegebene Reihenfolge fest und beendet eine Übertragung beim ersten Fehler. Die Entwickler-Probe zeigt zustandsändernde Reports zur Freigabe an. Die WinUI-App überträgt dieselben validierten Paketformen nach Benutzereingaben direkt und bündelt schnelle Änderungen. `EC 52` (Kompatibilitätsumschaltung), `EC 3F 55` (Commit mit möglicher nichtflüchtiger Speicherung), Shutdown-Payloads und Treiberwechsel gehören nicht zum Testablauf. Das Verhalten der Tests ohne diese Befehle ist firmwareabhängig.
