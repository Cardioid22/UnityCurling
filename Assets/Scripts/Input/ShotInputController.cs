using System;
using UnityEngine;
using Curling.Core;
using CCore = Curling.Core.Constants;

namespace Curling.Input
{
    public class ShotInputController : MonoBehaviour
    {
        [Range(0f, 4f)] public float speed = 2.85f;
        [Range(-45f, 45f)] public float aimOffsetDeg = 0f;
        public bool ccw = true;
        public float angularMagnitude = 1.57f;

        public PlayerSkill skill = new PlayerSkill();
        public bool applySkillNoise = true;
        public int seed = 0;
        public Transform previewOrigin;
        public float previewBaseLength = 5f;
        public float previewSpeedLength = 2f;
        public float previewArrowHeadLength = 0.8f;
        public float previewArrowHeadHalfWidth = 0.45f;
        public LineRenderer previewLine;

        System.Random _rng;
        public event Action<ShotInput> OnShotFired;
        bool _ready;
        bool _humanTurn = true;

        void Awake()
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
            EnsurePreviewLine();
            _ready = true;
        }

        public ShotInput Preview()
        {
            float s = Mathf.Clamp(speed, 0f, CCore.MaxSpeed);
            float a = AimAngleRad();
            float w = ccw ? -angularMagnitude : angularMagnitude;
            return new ShotInput(new Vec2(Mathf.Cos(a) * s, Mathf.Sin(a) * s), w);
        }

        public ShotInput FireWithNoise()
        {
            var baseShot = Preview();
            if (!applySkillNoise) return baseShot;
            float noisySpeed = baseShot.Speed * (1f + (float)Gaussian() * skill.stddev_speed);
            float noisyAngle = baseShot.ShotAngle + (float)Gaussian() * skill.stddev_angle;
            var v = new Vec2(Mathf.Cos(noisyAngle) * noisySpeed, Mathf.Sin(noisyAngle) * noisySpeed);
            return new ShotInput(v, baseShot.angular_velocity);
        }

        public void Fire()
        {
            if (!_ready || !_humanTurn) return;
            OnShotFired?.Invoke(FireWithNoise());
        }

        public void SetHumanTurn(bool active)
        {
            _humanTurn = active;
            UpdatePreviewLine();
        }

        void Update()
        {
            if (_humanTurn)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) Fire();
                if (UnityEngine.Input.GetKey(KeyCode.LeftArrow))  aimOffsetDeg += 18f * Time.deltaTime;
                if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) aimOffsetDeg -= 18f * Time.deltaTime;
                if (UnityEngine.Input.GetKey(KeyCode.UpArrow))    speed = Mathf.Clamp(speed + 0.6f * Time.deltaTime, 0f, CCore.MaxSpeed);
                if (UnityEngine.Input.GetKey(KeyCode.DownArrow))  speed = Mathf.Clamp(speed - 0.6f * Time.deltaTime, 0f, CCore.MaxSpeed);
                if (UnityEngine.Input.GetKeyDown(KeyCode.R))      ccw = !ccw;
                aimOffsetDeg = Mathf.Clamp(aimOffsetDeg, -45f, 45f);
            }

            UpdatePreviewLine();
        }

        void OnGUI()
        {
            if (!_humanTurn) return;

            GUI.color = Color.white;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            var title = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold };

            const float x = 12f;
            const float y = 12f;
            const float w = 456f;
            GUI.Box(new Rect(x, y, w, 182f), string.Empty);
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 24f), "Human turn - tune shot and throw", title);

            GUI.Label(new Rect(x + 12f, y + 40f, 120f, 20f), $"Speed: {speed:F2} m/s", label);
            speed = GUI.HorizontalSlider(new Rect(x + 132f, y + 48f, 188f, 18f), speed, 0.5f, CCore.MaxSpeed);

            GUI.Label(new Rect(x + 12f, y + 72f, 120f, 20f), $"Aim: {aimOffsetDeg:+0.0;-0.0;0.0} deg", label);
            aimOffsetDeg = GUI.HorizontalSlider(new Rect(x + 132f, y + 80f, 188f, 18f), aimOffsetDeg, -45f, 45f);

            GUI.Label(new Rect(x + 12f, y + 104f, 170f, 20f), $"Rotation: {(ccw ? "CCW" : "CW")} (R)", label);
            if (GUI.Button(new Rect(x + 210f, y + 102f, 110f, 24f), "Flip rotation")) ccw = !ccw;

            if (GUI.Button(new Rect(x + 12f, y + 140f, 120f, 28f), "Throw (Space)")) Fire();
            GUI.Label(new Rect(x + 148f, y + 144f, 180f, 20f), "Arrows also adjust shot.", label);
            DrawAimDiagram(new Rect(x + 340f, y + 40f, 100f, 126f), title);
        }

        float AimAngleRad()
        {
            return Mathf.PI * 0.5f + aimOffsetDeg * Mathf.Deg2Rad;
        }

        void EnsurePreviewLine()
        {
            if (previewLine == null) previewLine = GetComponent<LineRenderer>();
            if (previewLine == null) previewLine = gameObject.AddComponent<LineRenderer>();

            previewLine.useWorldSpace = true;
            previewLine.positionCount = 5;
            previewLine.startWidth = 0.09f;
            previewLine.endWidth = 0.06f;
            previewLine.startColor = new Color(0.05f, 0.95f, 1f, 0.95f);
            previewLine.endColor = new Color(0.05f, 0.95f, 1f, 0.8f);
            previewLine.material = MakePreviewMaterial();
        }

        Material MakePreviewMaterial()
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) { name = "ShotPreviewLine" } : null;
        }

        void UpdatePreviewLine()
        {
            if (previewLine == null) return;

            previewLine.enabled = _humanTurn;
            if (!_humanTurn) return;

            var shot = Preview();
            Vector3 start = previewOrigin != null
                ? previewOrigin.position
                : new Vector3(0f, 0f, CCore.HogLineY - 12f);
            start.y = 0.05f;

            Vector3 dir = new Vector3(shot.velocity.x, 0f, shot.velocity.y);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();

            float len = previewBaseLength + shot.Speed * previewSpeedLength;
            Vector3 tip = start + dir * len;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 arrowBase = tip - dir * previewArrowHeadLength;
            previewLine.SetPosition(0, start);
            previewLine.SetPosition(1, tip);
            previewLine.SetPosition(2, arrowBase + side * previewArrowHeadHalfWidth);
            previewLine.SetPosition(3, tip);
            previewLine.SetPosition(4, arrowBase - side * previewArrowHeadHalfWidth);
        }

        void DrawAimDiagram(Rect rect, GUIStyle title)
        {
            GUI.Box(rect, string.Empty);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 20f), "Aim", title);

            Rect lane = new Rect(rect.x + 12f, rect.y + 30f, rect.width - 24f, rect.height - 42f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.15f, 0.2f, 0.25f, 0.65f);
            GUI.DrawTexture(lane, Texture2D.whiteTexture);
            GUI.color = oldColor;

            Vector2 centerTop = new Vector2(lane.center.x, lane.y + 6f);
            Vector2 centerBottom = new Vector2(lane.center.x, lane.yMax - 6f);
            DrawScreenLine(centerBottom, centerTop, 2f, new Color(1f, 1f, 1f, 0.32f));

            var shot = Preview();
            Vector2 screenVelocity = new Vector2(shot.velocity.x, -shot.velocity.y);
            if (screenVelocity.sqrMagnitude < 1e-6f) screenVelocity = Vector2.down;
            screenVelocity.Normalize();

            float magnitude = 24f + 38f * (shot.Speed / CCore.MaxSpeed);
            Vector2 tail = new Vector2(lane.center.x, lane.yMax - 12f);
            Vector2 tip = tail + screenVelocity * magnitude;
            Vector2 arrowBase = tip - screenVelocity * 13f;
            Vector2 side = new Vector2(-screenVelocity.y, screenVelocity.x) * 7f;
            Color arrow = new Color(0.05f, 0.95f, 1f, 1f);
            DrawScreenLine(tail, tip, 4f, arrow);
            DrawScreenLine(tip, arrowBase + side, 4f, arrow);
            DrawScreenLine(tip, arrowBase - side, 4f, arrow);
        }

        void DrawScreenLine(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 1e-6f) return;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        double Gaussian()
        {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
