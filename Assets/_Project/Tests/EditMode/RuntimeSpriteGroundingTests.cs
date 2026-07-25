using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Tests
{
    public class RuntimeSpriteGroundingTests
    {
        // 128-레짐: 기대값은 픽셀 단위라 캔버스 ×2와 함께 전부 ×2 (정규화 피벗은 불변).
        [TestCase("prop-campfire", 12f)]
        [TestCase("prop-explosive-barrel", 10f)]
        [TestCase("prop-portal", 12f)]
        [TestCase("prop-stash", 22f)]
        [TestCase("item-blast-powder", 10f)]
        [TestCase("item-bomb", 8f)]
        [TestCase("item-coin-pouch", 12f)]
        [TestCase("item-frost-bomb", 8f)]
        [TestCase("item-frost-shard", 8f)]
        [TestCase("item-gemstone", 4f)]
        [TestCase("item-herb", 10f)]
        [TestCase("item-oil-flask", 8f)]
        [TestCase("item-potion", 8f)]
        [TestCase("item-recall-scroll", 6f)]
        [TestCase("item-relic", 6f)]
        [TestCase("item-throwing-knife", 4f)]
        public void WorldSprite_PivotMatchesOpaqueGroundContact(string assetName, float expectedPivotY)
        {
            string path = $"Assets/_Project/Art/Runtime/{assetName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            Assert.IsNotNull(sprite, $"Runtime sprite missing: {path}");
            Assert.AreEqual(expectedPivotY, sprite.pivot.y, 0.01f, $"Ground pivot mismatch: {path}");
        }
    }
}
