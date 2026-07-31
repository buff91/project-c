using System.Reflection;
using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine.UIElements;

namespace ProjectC.Tests
{
    public class HubUiBindingRegistryTests
    {
        [Test]
        public void ClearThenRebind_OneClickInvokesHandlerOnce()
        {
            var bindings = new HubUiBindingRegistry();
            var button = new Button();
            int callCount = 0;
            System.Action callback = () => callCount++;

            bindings.Bind(button, callback);
            bindings.Clear();
            bindings.Bind(button, callback);

            InvokeClick(button);

            Assert.AreEqual(1, callCount);
            bindings.Clear();
            InvokeClick(button);
            Assert.AreEqual(1, callCount);
        }

        private static void InvokeClick(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(invoke, "Unity UI Toolkit Clickable.Invoke contract changed.");
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
