using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WOF.Tests
{
    public sealed class WofLaunchFlowTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void InviteCodeSubmitInvokesJoinValidation()
        {
            _root = new GameObject("LaunchFlowTestRoot");
            var flow = _root.AddComponent<WofLaunchFlow>();
            var input = new GameObject("InviteCode", typeof(RectTransform), typeof(InputField))
                .GetComponent<InputField>();
            input.transform.SetParent(_root.transform, false);
            var status = new GameObject("LaunchStatus", typeof(RectTransform), typeof(Text))
                .GetComponent<Text>();
            status.transform.SetParent(_root.transform, false);

            SetField(flow, "inviteCodeInput", input);
            SetField(flow, "launchStatus", status);
            InvokePrivate(flow, "Awake");

            input.text = "   ";
            input.onSubmit.Invoke(input.text);

            Assert.That(status.text, Is.EqualTo(WofPublicSessionRules.JoinCodeRequired));
        }

        [Test]
        public void InviteCodeSubmitDoesNotRunAfterFlowIsDestroyed()
        {
            _root = new GameObject("LaunchFlowTestRoot");
            var flow = _root.AddComponent<WofLaunchFlow>();
            var input = new GameObject("InviteCode", typeof(RectTransform), typeof(InputField))
                .GetComponent<InputField>();
            input.transform.SetParent(_root.transform, false);
            var status = new GameObject("LaunchStatus", typeof(RectTransform), typeof(Text))
                .GetComponent<Text>();
            status.transform.SetParent(_root.transform, false);

            SetField(flow, "inviteCodeInput", input);
            SetField(flow, "launchStatus", status);
            InvokePrivate(flow, "Awake");
            InvokePrivate(flow, "OnDestroy");

            input.text = "   ";
            input.onSubmit.Invoke(input.text);

            Assert.That(status.text, Is.Empty);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, null);
        }
    }
}
