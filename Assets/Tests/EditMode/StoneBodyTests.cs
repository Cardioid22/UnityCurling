#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Curling.Core;
using Curling.Physics;

namespace Curling.Tests.EditMode
{
    public class StoneBodyTests
    {
        GameObject _go;
        StoneBody _stone;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StoneBodyTest");
            _stone = _go.AddComponent<StoneBody>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Launch_SetsRigidbodyVelocityFromShot()
        {
            var shot = new ShotInput(new Vec2(1.25f, 3.5f), -1.57f);

            _stone.Launch(shot, new Vector3(0f, 0f, 16f));

            var body = _go.GetComponent<Rigidbody>();
            Assert.AreEqual(1.25f, body.linearVelocity.x, 0.0001f);
            Assert.AreEqual(0f, body.linearVelocity.y, 0.0001f);
            Assert.AreEqual(3.5f, body.linearVelocity.z, 0.0001f);
            Assert.AreEqual(-1.57f, body.angularVelocity.y, 0.0001f);
        }

        [Test]
        public void Stage_ShowsStoneAtSpawnWithoutPuttingItInPlay()
        {
            _go.SetActive(false);

            _stone.Stage(new Vector3(0.5f, 0f, 16f));

            var body = _go.GetComponent<Rigidbody>();
            Assert.IsTrue(_go.activeSelf);
            Assert.IsFalse(_stone.isInPlay);
            Assert.AreEqual(0.5f, body.position.x, 0.0001f);
            Assert.AreEqual(16f, body.position.z, 0.0001f);
            Assert.AreEqual(Vector3.zero, body.linearVelocity);
            Assert.AreEqual(Vector3.zero, body.angularVelocity);
        }

        [Test]
        public void ApplySimulatedState_RotatesVisibleStoneFromAngularVelocity()
        {
            var stone = new StoneState
            {
                position = new Vec2(0f, 16f),
                angular_velocity = 1.57f,
                in_play = true
            };

            _stone.ApplySimulatedState(stone, 0.02f);

            Assert.Greater(Quaternion.Angle(Quaternion.identity, _go.transform.rotation), 0.1f);
        }
    }
}
#endif
