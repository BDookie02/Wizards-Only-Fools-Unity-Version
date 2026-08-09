using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofQuestDialogRulesTests
    {
        [Test]
        public void UnstartedDarrelDialogMatchesReactOpening()
        {
            var session = WofQuestDialogRules.CreateInitial(WofDarrelQuestProgress.Unstarted);

            Assert.That(session.NpcId, Is.EqualTo("-64--48"));
            Assert.That(session.TownId, Is.EqualTo("base-village"));
            Assert.That(session.DisplayName, Is.EqualTo("Darrel"));
            Assert.That(session.Line, Is.EqualTo("Darrel: Who are you what do you want!"));
            Assert.That(session.Choices, Has.Length.EqualTo(2));
            Assert.That(session.Choices[0].Id, Is.EqualTo("darrel-jerk"));
            Assert.That(session.Choices[1].Id, Is.EqualTo("darrel-two-spells"));
        }

        [Test]
        public void DarrelJerkChoiceMatchesReactBranch()
        {
            var session = WofQuestDialogRules.CreateInitial(WofDarrelQuestProgress.Unstarted);

            Assert.That(WofQuestDialogRules.TryChoose(session, "darrel-jerk", out var transition), Is.True);
            Assert.That(transition.AcceptedQuest, Is.False);
            Assert.That(transition.Closed, Is.False);
            Assert.That(transition.Session.Line, Is.EqualTo(WofQuestDialogRules.JerkResponseLine));
            Assert.That(transition.Session.Choices[0].Id, Is.EqualTo("darrel-two-spells"));
            Assert.That(transition.Session.Choices[1].Label, Is.EqualTo("Leave"));
        }

        [Test]
        public void DarrelTwoSpellChoiceOffersExactJob()
        {
            var session = WofQuestDialogRules.CreateInitial(WofDarrelQuestProgress.Unstarted);

            Assert.That(WofQuestDialogRules.TryChoose(session, "darrel-two-spells", out var transition), Is.True);
            Assert.That(transition.Session.Line, Is.EqualTo(WofQuestDialogRules.JobOfferLine));
            Assert.That(transition.Session.Choices[0].Id, Is.EqualTo("darrel-accept-job"));
            Assert.That(transition.Session.Choices[1].Label, Is.EqualTo("Not right now"));
        }

        [Test]
        public void DarrelAcceptChoiceMarksAssignmentAndUsesExactBrief()
        {
            var session = WofQuestDialogRules.CreateInitial(WofDarrelQuestProgress.Unstarted);
            WofQuestDialogRules.TryChoose(session, "darrel-two-spells", out var offered);

            Assert.That(WofQuestDialogRules.TryChoose(offered.Session, "darrel-accept-job", out var accepted), Is.True);
            Assert.That(accepted.AcceptedQuest, Is.True);
            Assert.That(accepted.Closed, Is.False);
            Assert.That(accepted.Session.Line, Is.EqualTo(WofQuestDialogRules.AcceptedLine));
            Assert.That(accepted.Session.Choices[0].Label, Is.EqualTo("Head to the fields"));
        }

        [TestCase(null, WofDarrelQuestProgress.Unstarted)]
        [TestCase("unstarted", WofDarrelQuestProgress.Unstarted)]
        [TestCase("assigned", WofDarrelQuestProgress.Accepted)]
        [TestCase("accepted", WofDarrelQuestProgress.Accepted)]
        [TestCase("started", WofDarrelQuestProgress.Accepted)]
        [TestCase("completed", WofDarrelQuestProgress.Completed)]
        public void DarrelProgressSanitizationIsBackwardCompatible(string status, WofDarrelQuestProgress expected)
        {
            Assert.That(WofQuestDialogRules.ResolveProgress(status), Is.EqualTo(expected));
        }

        [Test]
        public void AcceptedAndCompletedInitialDialogsMatchReact()
        {
            var accepted = WofQuestDialogRules.CreateInitial(WofDarrelQuestProgress.Accepted);
            var completed = WofQuestDialogRules.CreateInitial(WofDarrelQuestProgress.Completed);

            Assert.That(accepted.Line, Is.EqualTo(WofQuestDialogRules.AcceptedReminderLine));
            Assert.That(accepted.Choices[0].Label, Is.EqualTo("I will get the ingredients"));
            Assert.That(completed.Line, Is.EqualTo(WofQuestDialogRules.CompletedLine));
            Assert.That(completed.Choices[0].Label, Is.EqualTo("Leave Darrel alone"));
        }

        [Test]
        public void TargetScoreMatchesReactFormula()
        {
            var origin = Vector3.zero;
            var direction = Vector3.forward;
            var target = new Vector3(0.5f, 0f, 5f);

            Assert.That(WofQuestTargetMath.TryScoreTarget(origin, direction, target, out var score), Is.True);
            var distance = target.magnitude;
            var expected = 0.5f * 2.3f + distance * 0.12f;
            Assert.That(score, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void CloseTargetDoesNotRequireAimRadius()
        {
            Assert.That(WofQuestTargetMath.TryScoreTarget(
                Vector3.zero,
                Vector3.forward,
                new Vector3(3f, 0f, 1f),
                out var score), Is.True);
            Assert.That(score, Is.LessThan(float.PositiveInfinity));
        }

        [Test]
        public void TargetBehindPlayerOrOutsideRangeIsRejected()
        {
            Assert.That(WofQuestTargetMath.TryScoreTarget(
                Vector3.zero,
                Vector3.forward,
                new Vector3(0f, 0f, -1f),
                out _), Is.False);
            Assert.That(WofQuestTargetMath.TryScoreTarget(
                Vector3.zero,
                Vector3.forward,
                new Vector3(0f, 0f, 9.5001f),
                out _), Is.False);
        }

        [Test]
        public void FarOffAxisTargetIsRejected()
        {
            Assert.That(WofQuestTargetMath.TryScoreTarget(
                Vector3.zero,
                Vector3.forward,
                new Vector3(3f, 0f, 5f),
                out _), Is.False);
        }
    }
}
