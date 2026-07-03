# Telemetry Expansion Plan (v2)

## Design Decisions

- **No compression libraries** — bandwidth is manageable (~62kbps at 30fps).
- **Type-safe binary** — unmanaged structs in `BeatDash.Data`, `[StructLayout(LayoutKind.Sequential)]`, packed via `MemoryMarshal` on both sides. `BinaryPacket` type ID dispatches to the correct struct.
- **Two channels**:
  - **Text (JSON)**: discrete events (per-note scoring, combo breaks, energy changes, missed notes, cut kinematics). Batched every 2s.
  - **Binary**: continuous motion (saber/head/hand transforms, blade speed). Downsampled to 30fps, batched every 2s.
- **Final flush**: subscribe to level end events in `LiveStatsTracker`, flush partial buffer before disposal.
- **Practice mode**: detect and skip binding trackers entirely.

---

## Data Inventory

### Per-Note Detail (JSON batch, every 2s)

Each entry in a `NoteEvent[]` array:

| Field | Source | Type |
|---|---|---|
| SongTime | `NoteData.startSongTime` | float |
| ColorType | `NoteData.colorType` | byte (0=A/left, 1=B/right, 255=none/bomb) |
| NoteType | `NoteData.noteType` | byte (Note, Bomb, ChainHead, ChainLink) |
| CutDirection | `NoteData.cutDirection` | byte (0-8, Any=9) |
| LineIndex | `NoteData.lineIndex` | sbyte (-2 to 5 for extended lanes) |
| NoteLineLayer | `NoteData.noteLineLayer` | byte (0-2) |
| Result | derived from ScoringElement type | byte (0=good, 1=bad, 2=miss) |
| Score | `ScoringElement.score` or component sum | ushort |
| MaxScore | `ScoringElement.maxScore` | ushort |
| BeforeCutScore | `CutScoreBuffer.beforeCutScore` | byte |
| CenterDistanceScore | `CutScoreBuffer.centerDistanceCutScore` | byte |
| AfterCutScore | `CutScoreBuffer.afterCutScore` | byte |
| BeforeCutSwing | `CutScoreBuffer.beforeCutSwingRating` | float |
| AfterCutSwing | `CutScoreBuffer.afterCutSwingRating` | float |
| SaberSpeed | `NoteCutInfo.saberSpeed` (from `noteWasCutEvent`) | float |
| CutPointDistance | `NoteCutInfo.cutPoint` magnitude from center | float |

Source events:
- `IScoreController.scoringForNoteFinishedEvent` → scoring data
- `BeatmapObjectManager.noteWasCutEvent` → kinematics (NoteCutInfo)
- `BeatmapObjectManager.noteWasMissedEvent` → miss timing

### Combo & Energy Events (JSON batch, every 2s)

**Combo break events** — `ComboController.comboBreakingEventHappenedEvent`:
| Field | Type |
|---|---|
| SongTime | float |
| ComboBefore | int |

**Energy change events** — `IGameEnergyCounter.energyDidChangeEvent`:
| Field | Type |
|---|---|
| SongTime | float |
| DeltaEnergy | float |
| NewEnergy | float |

### Saber & Player Motion (Binary batch, every 2s at 30fps)

Unmanaged struct, type-safe via shared contract:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MotionFrame {
    public readonly float SongTime;
    // Left saber
    public readonly Vector3 LeftSaberPos;
    public readonly Quaternion LeftSaberRot;
    public readonly float LeftSaberSpeed;
    // Right saber
    public readonly Vector3 RightSaberPos;
    public readonly Quaternion RightSaberRot;
    public readonly float RightSaberSpeed;
    // Head
    public readonly Vector3 HeadPos;
    public readonly Quaternion HeadRot;
    // Left hand
    public readonly Vector3 LeftHandPos;
    public readonly Quaternion LeftHandRot;
    // Right hand
    public readonly Vector3 RightHandPos;
    public readonly Quaternion RightHandRot;
}
// Size: 1 + (3+4+1)×2 + (3+4)×3 = 1 + 16 + 21 = 38 floats = 152 bytes
```

Binary packet header:
```
[4B correlationId] [4B startSongTime] [2B frameCount] [2B frameIntervalMs]
[152B × frameCount]
```

Sources:
- `SaberManager.sabers[0/1]` → `.transform.position/rotation`, `.bladeSpeed`
- `PlayerTransforms` → `.headWorldTransform`, `.leftHandWorldTransform`, `.rightHandWorldTransform`
- `PlayerHeadAndObstacleInteraction.headDidEnterObstacleEvent` → wall hit events (JSON batch)

### Score Controller Events (JSON batch, every 2s)

**Score change events** — `IScoreController.scoreDidChangeEvent`:
| Field | Type |
|---|---|
| SongTime | float |
| ScoreDelta | int |

**Max multiplied score** — `IScoreController.maxMultipliedScore`:
Already computed via `ScoreModel.ComputeMaxMultipliedScoreForBeatmap`, but the controller's value is available for verification.

### Map/Beatmap Context (JSON, sent once at map start)

Added to `MapStartMessage` or a new `MapDetailMessage`:

| Field | Source | Type |
|---|---|---|
| NPS Curve | Computed from `TransformedBeatmapData` note list | `float[]` (NPS per second bucket) |
| Wall Timeline | `TransformedBeatmapData.obstaclesCount` + individual obstacle `beatTime`/`duration` | `WallEntry[]` |
| Bomb Positions | Individual bomb `NoteData.startSongTime` + grid position | `BombEntry[]` |
| Notes Per Hand | Count ColorA vs ColorB in transformed data | `(int Left, int Right)` |

### Audio/Time (sent once at map start)

| Field | Source | Type |
|---|---|---|
| SongSpeed | `AudioTimeSyncController.songSpeed` | float |

---

## Implementation Order

1. **Final flush** in `LiveStatsTracker` — subscribe to level end events, flush partial buffer
2. **Map detail data** (NPS curve, walls, bombs, notes per hand, songSpeed) — extend `MapStartMessage`
3. **Per-note events** — new `NoteEventBatchMessage`, subscribe to events in `LiveStatsTracker`
4. **Combo & energy events** — extend batch message
5. **Motion binary packet** — define `MotionFrame` struct in Data, new `BinaryPacketType`, sample in `Tick()`
6. **Score change events** — extend batch message
7. **Practice mode detection** — skip tracker binding in installer
8. **Chain notes** — research scoring model before including

## Chain Notes (Research Needed)

- `NoteData.noteType` has `ChainHead` and `ChainLink` values
- Chain heads likely score like regular notes
- Chain links have simplified scoring — need to decompile `ScoreModel` / `CutScoreBuffer` to determine exact values
- `scoringForNoteFinishedEvent` may or may not fire for chain links — needs verification
