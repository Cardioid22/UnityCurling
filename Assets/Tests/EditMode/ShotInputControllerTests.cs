#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Curling.Input;

namespace Curling.Tests.EditMode
{
    public class ShotInputControllerTests
    {
        GameObject _go;
        ShotInputController _input;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ShotInputTest");
            _input = _go.AddComponent<ShotInputController>();
            _input.applySkillNoise = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Preview_UsesConfiguredSpeed()
        {
            _input.speed = 1.2f;
            float slow = _input.Preview().Speed;

            _input.speed = 3.4f;
            float fast = _input.Preview().Speed;

            Assert.AreEqual(1.2f, slow, 0.0001f);
            Assert.AreEqual(3.4f, fast, 0.0001f);
        }

        [Test]
        public void Preview_UsesAimOffsetFromCenterline()
        {
            _input.speed = 2f;
            _input.aimOffsetDeg = 0f;
            var straight = _input.Preview();

            _input.aimOffsetDeg = 20f;
            var aimed = _input.Preview();

            Assert.AreEqual(0f, straight.velocity.x, 0.0001f);
            Assert.Less(aimed.velocity.x, -0.1f);
            Assert.AreEqual(straight.Speed, aimed.Speed, 0.0001f);
        }

        [Test]
        public void Preview_UsesClockwisePositiveSpinForUnitySheetView()
        {
            _input.ccw = false;
            float clockwise = _input.Preview().angular_velocity;

            _input.ccw = true;
            float counterClockwise = _input.Preview().angular_velocity;

            Assert.Greater(clockwise, 0f);
            Assert.Less(counterClockwise, 0f);
        }
    }
}
#endif
