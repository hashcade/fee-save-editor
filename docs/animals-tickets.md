# Animals and Relay Tickets

## Animals

Owned counts are integer game variables in USER: `G_動物_<ANID>`. Missing keys
mean zero. The catalog contains the 24 entries from the pinned
`Xzonn/FireEmblemEngageData` Animal table, revision
`86b8be7b9820e1bb3bce87d2a9a805ead85d92ab`. Names come from the Person text
table in all nine supported languages, pinned by `import_animal_catalog.py`.

In the supplied version 1.3.0 executable, `HubUtil.GetAnimalCaptureNum`
at `0x2a62370` and `SetAnimalCaptureNum` at `0x2a62460` clamp owned counts to
0–99. The setter calls the integer clamp at `0x32dfb20` with bounds 0 and 99.
`IncAnimalCaptureNum` at `0x2a62640` uses this setter. This cap is independent of
the five grazing slots initialized by `HubVariable` at `0x2a74160`.

The editor updates or inserts only the animal count variables. It never changes
HUB grazing selections, region donations, or animal acquisition achievements.
Existing nonnegative over-limit values are preserved on reading/copying, while
requested writes are validated against 99.

## Relay Tickets

Tickets are persisted in the Global RLGL section, separately for each campaign
identifier. They are not convoy items or ordinary USER variables. Native
`RelayUserGlobalData.GetTicketCount` at `0x200da70` clamps the effective count to
0–999; its serializer is at `0x200eec0`. `Ticket.Serialize` at `0x1b0d900` writes
the count and timestamp.

RLGL payload version 1:

| Payload offset | Field |
| --- | --- |
| 0 | UInt32 version, 1 |
| 4 | 28 preserved singleton bytes |
| 32 | UInt32 daily ticket counter |
| 36 | UInt32 campaign count |
| 40 + 20n | UInt64 campaign identifier |
| 48 + 20n | UInt32 ticket count |
| 52 + 20n | Int64 last-update timestamp |

The parser requires an exact payload length, a known version and unique
identifiers. A count edit changes only its four-byte quantity and the enclosing
file CRC32. Daily counters, timestamps, uncommitted game-save state, other
campaigns and all other Global sections remain untouched. Only registered
campaigns are offered; the editor does not enable online access or invent
campaign records. As with other save edits, perform editing with the game closed
so its in-memory state does not replace the edited values.

Focused validation uses synthetic multi-campaign files, the supplied game and
Global saves, boundary/malformed input checks, exact byte comparisons,
serialization/checksum round trips and headless GUI draft/navigation checks.
These checks do not substitute for testing an edited save in-game.
