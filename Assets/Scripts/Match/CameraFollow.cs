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
            StoneView
        }

        static readonly List<CameraFollow> Instances = new List<CameraFollow>();

        public static void SetTarget(Transform t)
        {
            foreach (var instance in Instances) instance.AssignTarget(t);
        }

        public static void ClearTarget()
        {
            foreach (var instance in Instances) instance.AssignTarget(null);
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

        Transform _target;
        Rigidbody _targetBody;
        Vector3 _initialPos;
        Quaternion _initialRot;
        Vector3 _lastTravelDirection = Vector3.forward;

        void Awake()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
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
                default:
                    FollowOffset();
                    break;
            }
        }

        void AssignTarget(Transform target)
        {
            _target = target;
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

        void LookAtNow(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            if (direction.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
