using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.Mod.Network;
using UnityEngine;
using Zenject;

namespace Shiron.BeatDash.Mod.Trackers;

public sealed class LiveStatsTracker(
    GameplaySession session,
    IScoreController scoreController,
    ComboController comboController,
    IGameEnergyCounter energyCounter,
    AudioTimeSyncController atsc,
    BeatmapObjectManager beatmapObjectManager,
    SaberManager saberManager,
    PlayerTransforms playerTransforms,
    PauseController pauseController,
    ILevelEndActions levelEndActions,
    NetworkManager networkManager
) : IInitializable, ITickable, IDisposable {

    private const float SnapshotInterval = 2f;
    private const float MotionSampleInterval = 1f / 30f;

    private readonly HandAccumulator _left = new();
    private readonly HandAccumulator _right = new();

    private readonly List<NoteEventDto> _noteEvents = [];
    private readonly List<ComboBreakDto> _comboBreaks = [];
    private readonly List<EnergyChangeDto> _energyChanges = [];
    private readonly List<MotionFrame> _motionFrames = [];

    private readonly Dictionary<float, (float SaberSpeed, float CutPointDist)> _pendingKinematics = [];

    private float? _lastSnapshotSongTime;
    private float? _lastMotionSongTime;
    private int _currentCombo;
    private bool _flushed;

    public void Initialize() {
        scoreController.scoringForNoteFinishedEvent += OnNoteFinished;
        beatmapObjectManager.noteWasCutEvent += OnNoteCut;
        comboController.comboDidChangeEvent += OnComboChanged;
        energyCounter.gameEnergyDidChangeEvent += OnEnergyChange;

        pauseController.didReturnToMenuEvent += OnLevelEnd;
        levelEndActions.levelFinishedEvent += OnLevelEnd;
        levelEndActions.levelFailedEvent += OnLevelEnd;
    }

    public void Tick() {
        if (!session.IsInitialized) return;

        var songTime = atsc.songTime;

        if (!_lastSnapshotSongTime.HasValue) {
            _lastSnapshotSongTime = songTime;
            _lastMotionSongTime = songTime;
            return;
        }

        if (songTime - _lastMotionSongTime.GetValueOrDefault() >= MotionSampleInterval) {
            _lastMotionSongTime = songTime;
            SampleMotion(songTime);
        }

        if (songTime - _lastSnapshotSongTime.GetValueOrDefault() >= SnapshotInterval) {
            _lastSnapshotSongTime = songTime;
            _ = SendBatchAsync(songTime);
        }
    }

    private void OnNoteCut(NoteController noteController, in NoteCutInfo cutInfo) {
        var nd = noteController.noteData;

        if (nd.gameplayType == NoteData.GameplayType.Bomb) {
            var cp = cutInfo.cutPoint;
            _noteEvents.Add(new NoteEventDto {
                SongTime = nd.time,
                ColorType = (int) nd.colorType,
                NoteType = (int) nd.gameplayType,
                CutDirection = (int) nd.cutDirection,
                LineIndex = nd.lineIndex,
                NoteLineLayer = (int) nd.noteLineLayer,
                Result = 1,
                MaxScore = 0,
                BeforeCutScore = 0,
                CenterDistanceScore = 0,
                AfterCutScore = 0,
                BeforeCutSwing = 0f,
                AfterCutSwing = 0f,
                SaberSpeed = cutInfo.saberSpeed,
                CutPointDistance = Mathf.Sqrt(cp.x * cp.x + cp.y * cp.y + cp.z * cp.z),
            });
            return;
        }

        var cp2 = cutInfo.cutPoint;
        _pendingKinematics[nd.time] = (
            cutInfo.saberSpeed,
            Mathf.Sqrt(cp2.x * cp2.x + cp2.y * cp2.y + cp2.z * cp2.z)
        );
    }

    private void OnNoteFinished(ScoringElement element) {
        var nd = element.noteData;
        if (nd.colorType == ColorType.None && nd.gameplayType != NoteData.GameplayType.Bomb) return;

        var songTime = nd.time;
        var hasKinematics = _pendingKinematics.TryGetValue(songTime, out var kin);
        if (hasKinematics) _pendingKinematics.Remove(songTime);

        int result;
        int maxScore = 0;
        int beforeCut = 0, centerDist = 0, afterCut = 0;
        float beforeSwing = 0f, afterSwing = 0f;

        switch (element) {
            case GoodCutScoringElement good:
                result = 0;
                var buf = good.cutScoreBuffer;
                beforeCut = buf.beforeCutScore;
                centerDist = buf.centerDistanceCutScore;
                afterCut = buf.afterCutScore;
                beforeSwing = buf.beforeCutSwingRating;
                afterSwing = buf.afterCutSwingRating;
                maxScore = good.maxPossibleCutScore;
                break;
            case BadCutScoringElement:
                result = 1;
                break;
            default:
                result = 2;
                break;
        }

        var colorType = nd.colorType;
        if (colorType != ColorType.None) {
            var hand = colorType == ColorType.ColorA ? _left : _right;
            switch (result) {
                case 0:
                    hand.GoodCuts++;
                    hand.TotalBeforeCutScore += beforeCut;
                    hand.TotalCenterDistanceScore += centerDist;
                    hand.TotalAfterCutScore += afterCut;
                    hand.TotalBeforeCutSwing += beforeSwing;
                    hand.TotalAfterCutSwing += afterSwing;
                    break;
                case 1: hand.BadCuts++; break;
                case 2: hand.Misses++; break;
            }
        }

        _noteEvents.Add(new NoteEventDto {
            SongTime = songTime,
            ColorType = (int) colorType,
            NoteType = (int) nd.gameplayType,
            CutDirection = (int) nd.cutDirection,
            LineIndex = nd.lineIndex,
            NoteLineLayer = (int) nd.noteLineLayer,
            Result = result,
            MaxScore = maxScore,
            BeforeCutScore = beforeCut,
            CenterDistanceScore = centerDist,
            AfterCutScore = afterCut,
            BeforeCutSwing = beforeSwing,
            AfterCutSwing = afterSwing,
            SaberSpeed = hasKinematics ? kin.SaberSpeed : 0f,
            CutPointDistance = hasKinematics ? kin.CutPointDist : 0f,
        });

        _ = SendScoreUpdateAsync(atsc.songTime);
    }

    private void OnComboChanged(int combo) {
        if (combo == 0 && _currentCombo > 0) {
            _comboBreaks.Add(new ComboBreakDto {
                SongTime = atsc.songTime,
                ComboBefore = _currentCombo,
            });
        }
        _currentCombo = combo;
    }

    private void OnEnergyChange(float energy) {
        _energyChanges.Add(new EnergyChangeDto {
            SongTime = atsc.songTime,
            Energy = energy,
        });
    }

    private void SampleMotion(float songTime) {
        _motionFrames.Add(new MotionFrame(
            songTime,
            ToTransformData(saberManager.leftSaber.transform),
            ToTransformData(saberManager.rightSaber.transform),
            ToTransformData(playerTransforms.headWorldPos, playerTransforms.headWorldRot)
        ));
    }

    private void OnLevelEnd() {
        if (_flushed) return;
        _flushed = true;
        _ = SendBatchAsync(atsc.songTime);
    }

    private async Task SendScoreUpdateAsync(float songTime) {
        try {
            var modifiedScore = scoreController.modifiedScore;
            var maxModifiedScore = scoreController.immediateMaxPossibleModifiedScore;
            var accuracy = maxModifiedScore > 0 ? modifiedScore / (float) maxModifiedScore : 0f;

            var message = new ScoreUpdateMessage {
                CorrelationId = session.CorrelationId,
                SongTime = songTime,
                Score = modifiedScore,
                MaxScore = maxModifiedScore,
                Accuracy = accuracy,
                Rank = ScoreRank.FromAccuracy(accuracy),
                Energy = energyCounter.energy,
                Combo = _currentCombo,
                Misses = _left.Misses + _right.Misses,
            };

            await networkManager.PostMessageAsync(JsonConvert.SerializeObject(message));
        } catch (Exception e) {
            Plugin.Log.Error($"Failed to send score update: {e.Message}");
        }
    }

    private async Task SendBatchAsync(float songTime) {
        try {
            var noteEvents = _noteEvents.ToArray();
            var comboBreaks = _comboBreaks.ToArray();
            var energyChanges = _energyChanges.ToArray();
            _noteEvents.Clear();
            _comboBreaks.Clear();
            _energyChanges.Clear();

            var motionFrames = _motionFrames.ToArray();
            _motionFrames.Clear();

            var message = new LiveStatsMessage {
                CorrelationId = session.CorrelationId,
                SongTime = songTime,
                Score = scoreController.multipliedScore,
                ModifiedScore = scoreController.modifiedScore,
                MaxPossibleScore = scoreController.immediateMaxPossibleMultipliedScore,
                Energy = energyCounter.energy,
                CurrentCombo = _currentCombo,
                MaxCombo = comboController.maxCombo,
                LeftHand = ToDto(_left),
                RightHand = ToDto(_right),
                NoteEvents = noteEvents,
                ComboBreaks = comboBreaks,
                EnergyChanges = energyChanges,
            };

            await networkManager.PostMessageAsync(JsonConvert.SerializeObject(message));

            if (motionFrames.Length > 0) {
                var binaryPayload = PackMotionFrames(session.CorrelationId, motionFrames);
                var packet = new BinaryPacket(BinaryPacketTypes.MotionFrameBatch, binaryPayload);
                await networkManager.PostMessageAsync(packet);
            }
        } catch (Exception e) {
            Plugin.Log.Error($"Failed to send live stats batch: {e.Message}");
        }
    }

    private static unsafe byte[] PackMotionFrames(int correlationId, MotionFrame[] frames) {
        var buffer = new byte[6 + MotionFrame.Size * frames.Length];
        fixed (byte* pBuf = buffer) {
            *(int*) pBuf = correlationId;
            *(short*) (pBuf + 4) = (short) frames.Length;

            var p = (float*) (pBuf + 6);
            for (int i = 0; i < frames.Length; i++) {
                var f = frames[i];
                *p++ = f.SongTime;
                *p++ = f.LeftSaber.PosX;
                *p++ = f.LeftSaber.PosY;
                *p++ = f.LeftSaber.PosZ;
                *p++ = f.LeftSaber.RotX;
                *p++ = f.LeftSaber.RotY;
                *p++ = f.LeftSaber.RotZ;
                *p++ = f.LeftSaber.RotW;
                *p++ = f.RightSaber.PosX;
                *p++ = f.RightSaber.PosY;
                *p++ = f.RightSaber.PosZ;
                *p++ = f.RightSaber.RotX;
                *p++ = f.RightSaber.RotY;
                *p++ = f.RightSaber.RotZ;
                *p++ = f.RightSaber.RotW;
                *p++ = f.Head.PosX;
                *p++ = f.Head.PosY;
                *p++ = f.Head.PosZ;
                *p++ = f.Head.RotX;
                *p++ = f.Head.RotY;
                *p++ = f.Head.RotZ;
                *p++ = f.Head.RotW;
            }
        }
        return buffer;
    }

    private static TransformData ToTransformData(Transform t) {
        var pos = t.position;
        var rot = t.rotation;
        return new TransformData(pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w);
    }

    private static TransformData ToTransformData(Vector3 pos, Quaternion rot) {
        return new TransformData(pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w);
    }

    private static HandStatsDto ToDto(HandAccumulator h) {
        return new HandStatsDto {
            GoodCuts = h.GoodCuts,
            BadCuts = h.BadCuts,
            Misses = h.Misses,
            TotalBeforeCutScore = h.TotalBeforeCutScore,
            TotalCenterDistanceScore = h.TotalCenterDistanceScore,
            TotalAfterCutScore = h.TotalAfterCutScore,
            AverageBeforeCutSwing = h.GoodCuts > 0 ? h.TotalBeforeCutSwing / h.GoodCuts : 0f,
            AverageAfterCutSwing = h.GoodCuts > 0 ? h.TotalAfterCutSwing / h.GoodCuts : 0f,
        };
    }

    public void Dispose() {
        scoreController.scoringForNoteFinishedEvent -= OnNoteFinished;
        beatmapObjectManager.noteWasCutEvent -= OnNoteCut;
        comboController.comboDidChangeEvent -= OnComboChanged;
        energyCounter.gameEnergyDidChangeEvent -= OnEnergyChange;

        pauseController.didReturnToMenuEvent -= OnLevelEnd;
        levelEndActions.levelFinishedEvent -= OnLevelEnd;
        levelEndActions.levelFailedEvent -= OnLevelEnd;
    }

    private sealed class HandAccumulator {
        public int GoodCuts;
        public int BadCuts;
        public int Misses;
        public int TotalBeforeCutScore;
        public int TotalCenterDistanceScore;
        public int TotalAfterCutScore;
        public float TotalBeforeCutSwing;
        public float TotalAfterCutSwing;
    }
}
