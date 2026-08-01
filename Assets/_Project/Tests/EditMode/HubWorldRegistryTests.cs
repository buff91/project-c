using System;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public sealed class HubWorldRegistryTests
    {
        [Test]
        public void RegisterInteraction_StoresIdAndLabelAndReplacesSamePosition()
        {
            var registry = new HubWorldRegistry();
            var position = new GridPos(5, 6, 0);
            var otherPosition = new GridPos(6, 6, 0);

            registry.RegisterInteraction(position, "merchant", "상인");
            registry.RegisterInteraction(otherPosition, "codex", "기록실");
            registry.RegisterInteraction(position, "stash", "창고");

            Assert.IsTrue(registry.TryGetInteraction(
                position,
                out HubInteractionTarget target));
            Assert.AreEqual("stash", target.Id);
            Assert.AreEqual("창고", target.Label);
            Assert.IsTrue(registry.TryGetInteraction(
                otherPosition,
                out HubInteractionTarget otherTarget));
            Assert.AreEqual("codex", otherTarget.Id);
            Assert.Throws<ArgumentException>(() =>
                registry.RegisterInteraction(new GridPos(), "", "시설"));
            Assert.Throws<ArgumentException>(() =>
                registry.RegisterInteraction(new GridPos(), "facility", ""));
        }

        [Test]
        public void Reset_ForgetsRegistrationsWithoutDestroyingVisuals()
        {
            var registry = new HubWorldRegistry();
            var propObject = new GameObject("Hub Prop Test");
            var lightObject = new GameObject("Hub Light Test");

            try
            {
                SpriteRenderer prop = propObject.AddComponent<SpriteRenderer>();
                SpriteRenderer light = lightObject.AddComponent<SpriteRenderer>();
                registry.RegisterInteraction(
                    new GridPos(1, 2, 0),
                    "merchant",
                    "상인");
                registry.RegisterProp(prop, new GridPos(2, 3, 0));
                registry.RegisterLight(light, new GridPos(3, 4, 0));
                prop.transform.position = new Vector3(2f, 3f, 4f);
                light.transform.position = new Vector3(5f, 6f, 7f);

                registry.Reset();
                int projectionCalls = 0;
                registry.ApplyView(
                    new IsoGrid(),
                    _ =>
                    {
                        projectionCalls++;
                        return Vector3.one;
                    });

                Assert.IsFalse(registry.TryGetInteraction(
                    new GridPos(1, 2, 0), out _));
                Assert.AreEqual(0, projectionCalls);
                Assert.AreEqual(new Vector3(2f, 3f, 4f), prop.transform.position);
                Assert.AreEqual(new Vector3(5f, 6f, 7f), light.transform.position);
                Assert.NotNull(prop);
                Assert.NotNull(light);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(propObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void ApplyView_UsesInjectedProjectionAndIsoGridSortingBands()
        {
            var registry = new HubWorldRegistry();
            var propObject = new GameObject("Hub Prop Test");
            var lightObject = new GameObject("Hub Light Test");

            try
            {
                SpriteRenderer prop = propObject.AddComponent<SpriteRenderer>();
                SpriteRenderer light = lightObject.AddComponent<SpriteRenderer>();
                var propPosition = new GridPos(2, 3, 1);
                var lightPosition = new GridPos(4, 1, 0);
                var iso = new IsoGrid(1f, 0.5f, 0.25f);

                registry.RegisterProp(prop, propPosition);
                registry.RegisterLight(light, lightPosition);
                registry.ApplyView(
                    iso,
                    pos => new Vector3(pos.x + 10f, pos.y + 20f, pos.elevation));

                Assert.AreEqual(new Vector3(12f, 23f, 1f), prop.transform.position);
                Assert.AreEqual(new Vector3(14f, 21f, 0f), light.transform.position);
                Assert.AreEqual(iso.SortingOrder(propPosition, 1), prop.sortingOrder);
                Assert.AreEqual(iso.SortingOrder(lightPosition, -1), light.sortingOrder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(propObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void ApplyView_SkipsDestroyedRenderer()
        {
            var registry = new HubWorldRegistry();
            var propObject = new GameObject("Destroyed Hub Prop Test");
            SpriteRenderer prop = propObject.AddComponent<SpriteRenderer>();
            registry.RegisterProp(prop, new GridPos(2, 3, 0));
            UnityEngine.Object.DestroyImmediate(propObject);

            Assert.DoesNotThrow(() =>
                registry.ApplyView(new IsoGrid(), _ => Vector3.zero));
        }
    }
}
