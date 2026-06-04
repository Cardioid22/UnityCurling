using UnityEngine;
using Curling.Core;
using CCore = Curling.Core.Constants;

namespace Curling.Physics
{
    [RequireComponent(typeof(Rigidbody))]
    public class StoneBody : MonoBehaviour
    {
        public Team team;
        public int stoneIndex;
        public bool isInPlay;

        public float mu = CCore.IceFrictionCoefficient;
        public float kCurl = CCore.CurlCoefficient;
        public float curlExponent = 1f;
        public float kOmega = CCore.AngularDecel;

        Rigidbody rb;
        int stillFrames;
        bool launchPending;
        Vector3 pendingLaunchPosition;
        Vector3 pendingLaunchVelocity;
        float pendingLaunchAngularVelocity;

        Rigidbody Rb
        {
            get
            {
                if (rb == null)
                {
                    rb = GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.useGravity = false;
                        rb.mass = 19.96f;
                        rb.constraints = RigidbodyConstraints.FreezePositionY
                                       | RigidbodyConstraints.FreezeRotationX
                                       | RigidbodyConstraints.FreezeRotationZ;
                        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rb.linearDamping = 0f;
                        rb.angularDamping = 0f;
                    }
                }
                return rb;
            }
        }

        void Awake()
        {
            _ = Rb;
        }

        public static Vector3 WorldPos(Vec2 p) => new Vector3(p.x, 0f, p.y);
        public static Vector3 WorldVel(Vec2 v) => new Vector3(v.x, 0f, v.y);
        public static Vec2 PlanePos(Vector3 w) => new Vec2(w.x, w.z);
        public static Vec2 PlaneVel(Vector3 w) => new Vec2(w.x, w.z);

        public void Launch(ShotInput shot, Vector3 startWorldPos)
        {
            transform.position = startWorldPos;
            var body = Rb;
            if (body == null) return;

            pendingLaunchPosition = startWorldPos;
            pendingLaunchVelocity = WorldVel(shot.velocity);
            pendingLaunchAngularVelocity = shot.angular_velocity;
            launchPending = true;

            body.position = startWorldPos;
            body.WakeUp();
            body.linearVelocity = pendingLaunchVelocity;
            body.angularVelocity = new Vector3(0f, pendingLaunchAngularVelocity, 0f);
            isInPlay = true;
            stillFrames = 0;

            var trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }

        public void Stage(Vector3 startWorldPos)
        {
            gameObject.SetActive(true);
            transform.position = startWorldPos;
            launchPending = false;
            var body = Rb;
            if (body != null)
            {
                body.position = startWorldPos;
            }

            isInPlay = false;
            stillFrames = 0;
            Stop();

            var trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }
        }

        public void Stop()
        {
            launchPending = false;
            var body = Rb;
            if (body == null) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        public void Deactivate()
        {
            isInPlay = false;
            Stop();
            var trail = GetComponent<TrailRenderer>();
            if (trail != null) trail.emitting = false;
            gameObject.SetActive(false);
        }

        public StoneState ToStoneState()
        {
            var body = Rb;
            if (body == null)
            {
                return new StoneState
                {
                    position = PlanePos(transform.position),
                    linear_velocity = Vec2.Zero,
                    angular_velocity = 0f,
                    in_play = isInPlay,
                    team = team,
                    stone_index = stoneIndex
                };
            }
            return new StoneState
            {
                position = PlanePos(transform.position),
                linear_velocity = PlaneVel(body.linearVelocity),
                angular_velocity = body.angularVelocity.y,
                in_play = isInPlay,
                team = team,
                stone_index = stoneIndex
            };
        }

        public void ApplyState(StoneState s)
        {
            launchPending = false;
            team = s.team;
            stoneIndex = s.stone_index;
            isInPlay = s.in_play;
            gameObject.SetActive(s.in_play);
            var body = Rb;
            if (s.in_play && body != null)
            {
                Vector3 worldPosition = WorldPos(s.position);
                transform.position = worldPosition;
                body.position = worldPosition;
                body.linearVelocity = WorldVel(s.linear_velocity);
                body.angularVelocity = new Vector3(0f, s.angular_velocity, 0f);
            }
            else
            {
                Stop();
            }

            var trail = GetComponent<TrailRenderer>();
            if (trail != null) trail.emitting = s.in_play && !s.IsStill;
        }

        public void ApplySimulatedState(StoneState s, float stepSeconds)
        {
            ApplyState(s);
            if (!s.in_play || stepSeconds <= 0f || Mathf.Abs(s.angular_velocity) < CCore.StopAngularEps) return;

            transform.Rotate(Vector3.up, s.angular_velocity * Mathf.Rad2Deg * stepSeconds, Space.World);
        }

        void FixedUpdate()
        {
            if (!isInPlay) return;
            var body = Rb;
            if (body == null) return;

            if (launchPending)
            {
                ApplyPendingLaunch(body);
                return;
            }

            Vec2 v2 = PlaneVel(body.linearVelocity);
            float speed = v2.Magnitude;
            float w = body.angularVelocity.y;

            if (speed > CCore.StopLinearEps)
            {
                Vec2 fr = CurlForce.FrictionAccel(v2, mu, CCore.Gravity);
                Vec2 fc = CurlForce.Compute(v2, w, kCurl, curlExponent);
                Vector3 accel = new Vector3(fr.x + fc.x, 0f, fr.y + fc.y);
                body.AddForce(accel, ForceMode.Acceleration);
            }
            else
            {
                body.linearVelocity = Vector3.zero;
            }

            if (Mathf.Abs(w) > CCore.StopAngularEps)
            {
                float dw = CurlForce.AngularDecel(w, kOmega) * Time.fixedDeltaTime;
                body.angularVelocity = new Vector3(0f, w + dw, 0f);
            }
            else
            {
                body.angularVelocity = Vector3.zero;
            }

            float halfW = CCore.SheetHalfWidth + CCore.StoneRadius;
            float backZ = CCore.BackLineY + CCore.StoneRadius;
            if (Mathf.Abs(body.position.x) > halfW || body.position.z > backZ)
            {
                Deactivate();
            }
        }

        public bool IsStillThisFrame()
        {
            if (launchPending) return false;
            var body = Rb;
            if (body == null) return true;
            return body.linearVelocity.magnitude < CCore.StopLinearEps
                && Mathf.Abs(body.angularVelocity.y) < CCore.StopAngularEps;
        }

        public string MotionDebugState()
        {
            var body = Rb;
            if (body == null)
            {
                return $"transform={transform.position} rb=missing active={gameObject.activeInHierarchy} inPlay={isInPlay}";
            }

            return $"transform={transform.position} rbPos={body.position} rbVel={body.linearVelocity} rbAng={body.angularVelocity} launchPending={launchPending} sleeping={body.IsSleeping()} kinematic={body.isKinematic} active={gameObject.activeInHierarchy} inPlay={isInPlay}";
        }

        public int IncrementStillCounter()
        {
            if (IsStillThisFrame()) stillFrames++;
            else stillFrames = 0;
            return stillFrames;
        }

        void ApplyPendingLaunch(Rigidbody body)
        {
            transform.position = pendingLaunchPosition;
            body.position = pendingLaunchPosition;
            body.WakeUp();
            body.linearVelocity = pendingLaunchVelocity;
            body.angularVelocity = new Vector3(0f, pendingLaunchAngularVelocity, 0f);
            launchPending = false;
        }
    }
}
