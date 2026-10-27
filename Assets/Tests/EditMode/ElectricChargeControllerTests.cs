using NUnit.Framework;
using UnityEngine;
using BatterySimulation.Modules;

namespace BatterySimulation.Tests
{
    public class ElectricChargeControllerTests
    {
        private GameObject _testObj;
        private ElectricChargeController _controller;
        private Renderer _renderer;
        private Material _mat;

        [SetUp]
        public void SetUp()
        {
            _testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _renderer = _testObj.GetComponent<Renderer>();
            Shader shader = Shader.Find("Custom/ElectricChargeFlow");
            _mat = new Material(shader);
            _renderer.sharedMaterial = _mat;
            _controller = _testObj.AddComponent<ElectricChargeController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mat != null) Object.DestroyImmediate(_mat);
            if (_testObj != null) Object.DestroyImmediate(_testObj);
        }

        [Test]
        public void SetChargeProgress_ClampsBetweenZeroAndOne()
        {
            _controller.SetChargeProgress(0.75f);
            Assert.AreEqual(0.75f, _controller.ChargeProgress, 0.0001f);

            _controller.SetChargeProgress(1.5f);
            Assert.AreEqual(1.0f, _controller.ChargeProgress, 0.0001f);

            _controller.SetChargeProgress(-0.5f);
            Assert.AreEqual(0.0f, _controller.ChargeProgress, 0.0001f);
        }

        [Test]
        public void SetFlowDirection_UpdatesDirectionCorrectly()
        {
            Vector3 customDir = new Vector3(1f, 0f, 0f);
            _controller.SetFlowDirection(customDir);
            Assert.AreEqual(customDir, _controller.FlowDirection);

            Vector3 reverseDir = new Vector3(-1f, 0f, 0f);
            _controller.SetFlowDirection(reverseDir);
            Assert.AreEqual(reverseDir, _controller.FlowDirection);
        }

        [Test]
        public void SetFlowColor_UpdatesColor()
        {
            Color neonCyan = new Color(0f, 1f, 1f, 1f);
            _controller.SetFlowColor(neonCyan);
            Assert.AreEqual(neonCyan, _controller.FlowColor);
        }

        [Test]
        public void ApplyProperties_UpdatesMaterialPropertyBlock()
        {
            _controller.SetChargeProgress(0.85f);
            _controller.SetFlowDirection(new Vector3(1f, 0f, 0f));
            _controller.ApplyProperties();

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);

            float appliedProgress = block.GetFloat(Shader.PropertyToID("_ChargeProgress"));
            Vector4 appliedDir = block.GetVector(Shader.PropertyToID("_FlowDirection"));

            Assert.AreEqual(0.85f, appliedProgress, 0.0001f);
            Assert.AreEqual(1f, appliedDir.x, 0.0001f);
            Assert.AreEqual(0f, appliedDir.y, 0.0001f);
            Assert.AreEqual(0f, appliedDir.z, 0.0001f);
        }

        [Test]
        public void SetTransparency_UpdatesAlphaPropertiesCorrectly()
        {
            _controller.SetTransparency(0.25f, 0.05f, 0.5f);
            Assert.AreEqual(0.25f, _controller.BaseAlpha, 0.0001f);
            Assert.AreEqual(0.05f, _controller.UnchargedAlpha, 0.0001f);
            Assert.AreEqual(0.5f, _controller.ChargedAlpha, 0.0001f);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);

            float baseAlpha = block.GetFloat(Shader.PropertyToID("_BaseAlpha"));
            float unchargedAlpha = block.GetFloat(Shader.PropertyToID("_UnchargedAlpha"));
            float chargedAlpha = block.GetFloat(Shader.PropertyToID("_ChargedAlpha"));

            Assert.AreEqual(0.25f, baseAlpha, 0.0001f);
            Assert.AreEqual(0.05f, unchargedAlpha, 0.0001f);
            Assert.AreEqual(0.5f, chargedAlpha, 0.0001f);
        }
    }
}
