using System.Collections.Generic;
using UnityEngine;
using CCore = Curling.Core.Constants;

namespace Curling.Match
{
    public class CameraFollow : MonoBehaviour
    {
        public enum FollowMode
        {
            Offset,
            OverheadCenterline,
            ObliqueStone,
            StoneView,
            HumanAim
        }

        static readonly List<CameraFollow> Instances = new List<CameraFollow>();

        public static void SetTarget(Transform t)
        {
            SetTarget(t, false, false);
        }

        public static void SetTarget(Transform t, bool humanAimSetup)
        {
            SetTarget(t, humanAimSetup, false);
        }

        public static void SetShotTarget(Transform t)
        {
            SetTarget(t, false, true);
        }

        static void SetTarget(Transform t, bool humanAimSetup, bool shotInProgress)
        {
            foreach (var instance in Instances) instance.AssignTarget(t, humanAimSetup, shotInProgress);
        }

        public static void ClearTarget()
        {
            foreach (var instance in Instances) instance.AssignTarget(null, false, false);
        }

        public FollowMode mode = FollowMode.Offset;
        public Vector3 followOffset = new Vector3(0f, 5f, -6f);
        public float smooth = 4f;
        public bool autoFollowEnabled = true;

        [Header("Overhead")]
        public float centerlineX = 0f;
        public float overheadHeight = 10f;
        public float sheetMinZ = 0f;
        public float sheetMaxZ = CCore.SheetLength;

        [Header("Oblique")]
        public Vector3 obliqueOffset = new Vector3(1.7f, 1.25f, -2.1f);
        public float stoneLookHeight = 0.12f;

        [Header("Stone View")]
        public float stoneViewHeight = 0.34f;
        public float stoneViewForwardOffset = 0.08f;
        public float stoneViewLookDistance = 6f;

        [Header("Shot Transition")]
        public bool trackShotBeforeOverhead = true;
        public float overheadSwitchZ = CCore.HogLineY;
        public Vector3 shotTrackOffset = new Vector3(0f, 1.35f, -2.6f);
        public float shotTrackLookAhead = 2.6f;
        public float shotTrackLookHeight = 0.18f;
        public float shotTrackFieldOfView = 60f;
        public float shotTrackNearClip = 0.03f;

        [Header("Human Aim")]
        public bool useHumanAimDuringSetup = false;
        public float humanAimEyeHeight = 1.22f;
        public float humanAimBehindStone = 2.05f;
        public float humanAimFocusDistance = 3.5f;
        public float humanAimLookHeight = 0.18f;
        public float humanAimFieldOfView = 58f;
        public float humanAimNearClip = 0.03f;

        Transform _target;
        Rigidbody _targetBody;
        Camera _camera;
        Vector3 _initialPos;
        Quaternion _initialRot;
        Vector3 _lastTravelDirection = Vector3.forward;
        bool _humanAimSetup;
        bool _shotInProgress;
        bool _defaultOrthographic;
        float _defaultOrthographicSize;
        float _defaultFieldOfView;
        float _defaultNearClip;

        void Awake()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
            _camera = GetComponent<Camera>();
            CacheCameraDefaults();
            _initialPos = transform.position;
            _initialRot = transform.rotation;
        }

        void OnEnable()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
        }

        void OnDestroy()
        {
            Instances.Remove(this);
        }

        void LateUpdate()
        {
            if (!autoFollowEnabled) return;
            if (_target == null)
            {
                ReturnToInitialPose();
                return;
            }

            if (ShouldUseHumanAim())
            {
                FollowHumanAim();
                return;
            }

            if (ShouldTrackShotBeforeOverhead())
            {
                FollowShotOrOverhead();
                return;
            }

            RestoreCameraDefaults();
            switch (mode)
            {
                case FollowMode.OverheadCenterline:
                    FollowOverhead();
                    break;
                case FollowMode.ObliqueStone:
                    FollowOblique();
                    break;
                case FollowMode.StoneView:
                    FollowStoneView();
                    break;
                case FollowMode.HumanAim:
                    FollowHumanAim();
                    break;
                default:
                    FollowOffset();
                    break;
            }
        }

        void AssignTarget(Transform target, bool humanAimSetup, bool shotInProgress)
        {
            _target = target;
            _humanAimSetup = humanAimSetup;
            _shotInProgress = shotInProgress;
            _targetBody = target != null ? target.GetComponent<Rigidbody>() : null;
            if (target != null)
            {
                Vector3 travel = ReadTravelDirection();
                if (travel.sqrMagnitude > 1e-6f) _lastTravelDirection = travel;
                SnapToTarget();
            }
        }

        void ReturnToInitialPose()
        {
            RestoreCameraDefaults();
            MoveTo(_initialPos);
            RotateTo(_initialRot);
        }

        void FollowOffset()
        {
            Vector3 desired = _target.position + followOffset;
            MoveTo(desired);
            LookAt(_target.position + new Vector3(0f, 0.3f, 0f));
        }

        void FollowOverhead()
        {
            SetOverheadPose();
        }

        void FollowOblique()
        {
            MoveTo(_target.position + obliqueOffset);
            LookAt(_target.position + Vector3.up * stoneLookHeight);
        }

        void FollowStoneView()
        {
            Vector3 travel = ReadTravelDirection();
            if (travel.sqrMagnitude > 1e-6f) _lastTravelDirection = travel;
            travel = _lastTravelDirection;

            Vector3 desired = _target.position + Vector3.up * stoneViewHeight + travel * stoneViewForwardOffset;
            MoveTo(desired);
            LookAt(transform.position + travel * stoneViewLookDistance + Vector3.up * 0.01f);
        }

        void FollowHumanAim()
        {
            ApplyHumanAimCameraSettings();

            Vector3 forward = HumanAimForward();
            Vector3 desired = _target.position - forward * humanAimBehindStone + Vector3.up * humanAimEyeHeight;
            MoveTo(desired);
            LookAt(HumanAimLookPoint(forward));
        }

        void FollowShotOrOverhead()
        {
            if (_target.position.z >= overheadSwitchZ)
            {
                RestoreCameraDefaults();
                FollowOverhead();
                return;
            }

            ApplyShotTrackCameraSettings();

            Vector3 travel = ShotTravelDirection();
            Vector3 desired = _target.position
                            - travel * Mathf.Abs(shotTrackOffset.z)
                            + Vector3.right * shotTrackOffset.x
                            + Vector3.up * shotTrackOffset.y;
            MoveTo(desired);
            LookAt(_target.position + travel * shotTrackLookAhead + Vector3.up * shotTrackLookHeight);
        }

        Vector3 ReadTravelDirection()
        {
            if (_targetBody == null) return _lastTravelDirection;

            Vector3 v = _targetBody.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude < 0.0025f) return _lastTravelDirection;
            return v.normalized;
        }

        void MoveTo(Vector3 desired)
        {
            transform.position = Vector3.Lerp(transform.position, desired, FollowT());
        }

        void RotateTo(Quaternion desired)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, FollowT());
        }

        void LookAt(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            if (direction.sqrMagnitude < 1e-6f) return;
            RotateTo(Quaternion.LookRotation(direction, Vector3.up));
        }

        float FollowT()
        {
            return Mathf.Clamp01(Time.deltaTime * smooth);
        }

        void SnapToTarget()
        {
            if (ShouldUseHumanAim())
            {
                SnapHumanAim();
                return;
            }

            if (ShouldTrackShotBeforeOverhead())
            {
                SnapShotOrOverhead();
                return;
            }

            RestoreCameraDefaults();
            switch (mode)
            {
                case FollowMode.OverheadCenterline:
                    SetOverheadPose();
                    break;
                case FollowMode.ObliqueStone:
                    transform.position = _target.position + obliqueOffset;
                    LookAtNow(_target.position + Vector3.up * stoneLookHeight);
                    break;
                case FollowMode.StoneView:
                    SnapStoneView();
                    break;
                case FollowMode.HumanAim:
                    SnapHumanAim();
                    break;
                default:
                    transform.position = _target.position + followOffset;
                    LookAtNow(_target.position + new Vector3(0f, 0.3f, 0f));
                    break;
            }
        }

        void SetOverheadPose()
        {
            float z = Mathf.Clamp(_target.position.z, sheetMinZ, sheetMaxZ);
            transform.position = new Vector3(centerlineX, overheadHeight, z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void SnapStoneView()
        {
            Vector3 travel = ReadTravelDirection();
            if (travel.sqrMagnitude > 1e-6f) _lastTravelDirection = travel;
            travel = _lastTravelDirection;

            transform.position = _target.position + Vector3.up * stoneViewHeight + travel * stoneViewForwardOffset;
            LookAtNow(transform.position + travel * stoneViewLookDistance + Vector3.up * 0.01f);
        }

        void SnapHumanAim()
        {
            ApplyHumanAimCameraSettings();

            Vector3 forward = HumanAimForward();
            transform.position = _target.position - forward * humanAimBehindStone + Vector3.up * humanAimEyeHeight;
            LookAtNow(HumanAimLookPoint(forward));
        }

        void SnapShotOrOverhead()
        {
            if (_target.position.z >= overheadSwitchZ)
            {
                RestoreCameraDefaults();
                SetOverheadPose();
                return;
            }

            ApplyShotTrackCameraSettings();

            Vector3 travel = ShotTravelDirection();
            transform.position = _target.position
                               - travel * Mathf.Abs(shotTrackOffset.z)
                               + Vector3.right * shotTrackOffset.x
                               + Vector3.up * shotTrackOffset.y;
            LookAtNow(_target.position + travel * shotTrackLookAhead + Vector3.up * shotTrackLookHeight);
        }

        bool ShouldUseHumanAim()
        {
            return _humanAimSetup && useHumanAimDuringSetup;
        }

        bool ShouldTrackShotBeforeOverhead()
        {
            return _shotInProgress && trackShotBeforeOverhead && mode == FollowMode.OverheadCenterline;
        }

        Vector3 ShotTravelDirection()
        {
            Vector3 travel = ReadTravelDirection();
            if (travel.sqrMagnitude > 1e-6f) return travel;
            return HumanAimForward();
        }

        Vector3 HumanAimForward()
        {
            Vector3 from = _target != null ? _target.position : transform.position;
            Vector3 to = new Vector3(CCore.HouseCenterX, from.y, CCore.HouseCenterY);
            Vector3 forward = to - from;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        Vector3 HumanAimLookPoint(Vector3 forward)
        {
            float distanceToHouse = Mathf.Max(0.01f, Vector3.Distance(
                new Vector3(_target.position.x, 0f, _target.position.z),
                new Vector3(CCore.HouseCenterX, 0f, CCore.HouseCenterY)));
            float focusDistance = Mathf.Clamp(humanAimFocusDistance, 1f, distanceToHouse);
            return _target.position + forward * focusDistance + Vector3.up * humanAimLookHeight;
        }

        void CacheCameraDefaults()
        {
            if (_camera == null) return;

            _defaultOrthographic = _camera.orthographic;
            _defaultOrthographicSize = _camera.orthographicSize;
            _defaultFieldOfView = _camera.fieldOfView;
            _defaultNearClip = _camera.nearClipPlane;
        }

        void ApplyHumanAimCameraSettings()
        {
            if (_camera == null) return;

            _camera.orthographic = false;
            _camera.fieldOfView = humanAimFieldOfView;
            _camera.nearClipPlane = humanAimNearClip;
        }

        void ApplyShotTrackCameraSettings()
        {
            if (_camera == null) return;

            _camera.orthographic = false;
            _camera.fieldOfView = shotTrackFieldOfView;
            _camera.nearClipPlane = shotTrackNearClip;
        }

        void RestoreCameraDefaults()
        {
            if (_camera == null) return;

            _camera.orthographic = _defaultOrthographic;
            _camera.orthographicSize = _defaultOrthographicSize;
            _camera.fieldOfView = _defaultFieldOfView;
            _camera.nearClipPlane = _defaultNearClip;
        }

        void LookAtNow(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            if (direction.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
