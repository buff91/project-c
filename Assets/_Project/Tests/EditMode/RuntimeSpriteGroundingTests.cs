using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Tests
{
    public class RuntimeSpriteGroundingTests
    {
        [TestCase("prop-campfire", 6f)]
        [TestCase("prop-explosive-barrel", 5f)]
        [TestCase("prop-portal", 6f)]
        [TestCase("prop-stash", 11f)]
        [TestCase("item-blast-powder", 5f)]
        [TestCase("item-bomb", 4f)]
        [TestCase("item-coin-pouch", 6f)]
        [TestCase("item-frost-bomb", 4f)]
        [TestCase("item-frost-shard", 4f)]
        [TestCase("item-gemstone", 2f)]
        [TestCase("item-herb", 5f)]
        [TestCase("item-oil-flask", 4f)]
        [TestCase("item-potion", 4f)]
        [TestCase("item-recall-scroll", 3f)]
        [TestCase("item-relic", 3f)]
        [TestCase("item-throwing-knife", 2f)]
        public void WorldSprite_PivotMatchesOpaqueGroundContact(string assetName, float expectedPivotY)
        {
            string path = $"Assets/_Project/Art/Runtime/{assetName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            Assert.IsNotNull(sprite, $"Runtime sprite missing: {path}");
            Assert.AreEqual(expectedPivotY, sprite.pivot.y, 0.01f, $"Ground pivot mismatch: {path}");
        }
    }
}
