using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DTech.Pulse.Tests
{
    [TestFixture]
    internal sealed class InitializationGraphRecordingTests
    {
        private const double MillisecondsTolerance = 0.001d;

        private readonly List<InitializationGraphSnapshot> _snapshots = new();
        private bool _wasRecordingEnabled;

        [SetUp]
        public void Setup()
        {
            _wasRecordingEnabled = InitializationGraphRecording.IsEnabled;
            _snapshots.Clear();
            InitializationGraphRecording.OnSnapshotRecorded += SnapshotRecordedHandler;
        }

        [TearDown]
        public void TearDown()
        {
            InitializationGraphRecording.OnSnapshotRecorded -= SnapshotRecordedHandler;
            InitializationGraphRecording.IsEnabled = _wasRecordingEnabled;
        }

        [Test]
        public async Task Recording_ShouldPublishSnapshot_WithOrderDependenciesAndTimings()
        {
            InitializationGraphRecording.IsEnabled = true;
            var builder = new InitializationContextBuilder();
            builder.AddSystem(new CriticalSystem()).SetAsCritical();
            builder.AddSystem(new CriticalSystemB()).SetAsCritical();
            builder.AddSystem(new SimpleSystem(null)).AddDependencies(typeof(CriticalSystem), typeof(CriticalSystemB));
            builder.AddSystem(new FieldDependentSystem(null));

            await builder.Build().InitializationAsync(CancellationToken.None);

            Assert.AreEqual(1, _snapshots.Count, "Exactly one snapshot must be published.");
            InitializationGraphSnapshot snapshot = _snapshots[0];
            Assert.AreEqual(InitializationGraphStatus.Completed, snapshot.Status);
            Assert.AreEqual(4, snapshot.Systems.Count);

            int criticalIndex = IndexOf(snapshot, typeof(CriticalSystem));
            int criticalBIndex = IndexOf(snapshot, typeof(CriticalSystemB));
            int simpleIndex = IndexOf(snapshot, typeof(SimpleSystem));
            int dependentIndex = IndexOf(snapshot, typeof(FieldDependentSystem));
            InitializationSystemRecord critical = snapshot.Systems[criticalIndex];
            InitializationSystemRecord criticalB = snapshot.Systems[criticalBIndex];
            InitializationSystemRecord simple = snapshot.Systems[simpleIndex];
            InitializationSystemRecord dependent = snapshot.Systems[dependentIndex];

            Assert.AreEqual(0, critical.StartOrder);
            Assert.AreEqual(1, criticalB.StartOrder);
            Assert.AreEqual(2, simple.StartOrder);
            Assert.AreEqual(3, dependent.StartOrder);
            Assert.IsTrue(critical.IsCritical);
            Assert.IsTrue(criticalB.IsCritical);
            Assert.IsFalse(simple.IsCritical);
            Assert.IsFalse(dependent.IsCritical);
            CollectionAssert.AreEqual(new[] { criticalIndex, criticalBIndex }, simple.DependencyIndices);
            CollectionAssert.AreEqual(new[] { simpleIndex }, dependent.DependencyIndices);
            Assert.AreEqual(InitializationSystemStatus.Completed, simple.Status);
            Assert.AreEqual(InitializationSystemStatus.Completed, dependent.Status);
            Assert.That(simple.DurationMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(dependent.DurationMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(dependent.StartMilliseconds, Is.GreaterThanOrEqualTo(simple.StartMilliseconds));
            Assert.That(snapshot.TotalMilliseconds, Is.GreaterThanOrEqualTo(dependent.StartMilliseconds));
        }

        [Test]
        public async Task Recording_ShouldNotPublishSnapshot_WhenDisabled()
        {
            InitializationGraphRecording.IsEnabled = false;
            var builder = new InitializationContextBuilder();
            builder.AddSystem(new SimpleSystem(null));

            await builder.Build().InitializationAsync(CancellationToken.None);

            CollectionAssert.IsEmpty(_snapshots);
        }

        [Test]
        public void Recording_ShouldPublishFailedSnapshot_AndRethrow_WhenSystemThrows()
        {
            InitializationGraphRecording.IsEnabled = true;
            var builder = new InitializationContextBuilder();
            builder.AddSystem(new FailingSystem());
            builder.AddSystem(new FailingSystemDependent());

            InitializationContext context = builder.Build();

            var exception = Assert.Throws<InvalidOperationException>(
                () => context.InitializationAsync(CancellationToken.None).GetAwaiter().GetResult());
            StringAssert.Contains(FailingSystem.ErrorMessage, exception!.Message);

            Assert.AreEqual(1, _snapshots.Count, "Snapshot must be published even if initialization fails.");
            InitializationGraphSnapshot snapshot = _snapshots[0];
            Assert.AreEqual(InitializationGraphStatus.Failed, snapshot.Status);

            InitializationSystemRecord failing = snapshot.Systems[IndexOf(snapshot, typeof(FailingSystem))];
            Assert.AreEqual(InitializationSystemStatus.Failed, failing.Status);
            StringAssert.Contains(FailingSystem.ErrorMessage, failing.Error);

            InitializationSystemRecord dependent = snapshot.Systems[IndexOf(snapshot, typeof(FailingSystemDependent))];
            Assert.AreEqual(InitializationSystemStatus.NotStarted, dependent.Status);
            Assert.AreEqual(-1, dependent.StartOrder);
        }

        [Test]
        public async Task Recording_ShouldPublishCancelledSnapshot_WhenTokenCancelledDuringInitialization()
        {
            InitializationGraphRecording.IsEnabled = true;
            using var cancellationTokenSource = new CancellationTokenSource();
            var builder = new InitializationContextBuilder();
            builder.AddSystem(new CancellingSystem(cancellationTokenSource));
            builder.AddSystem(new CancellingSystemDependent());

            await builder.Build().InitializationAsync(cancellationTokenSource.Token);

            Assert.AreEqual(1, _snapshots.Count);
            InitializationGraphSnapshot snapshot = _snapshots[0];
            Assert.AreEqual(InitializationGraphStatus.Cancelled, snapshot.Status);

            InitializationSystemRecord cancelling = snapshot.Systems[IndexOf(snapshot, typeof(CancellingSystem))];
            Assert.AreEqual(InitializationSystemStatus.Completed, cancelling.Status);

            InitializationSystemRecord dependent = snapshot.Systems[IndexOf(snapshot, typeof(CancellingSystemDependent))];
            Assert.AreEqual(InitializationSystemStatus.NotStarted, dependent.Status);
            Assert.AreEqual(-1, dependent.StartOrder);
        }

        [Test]
        public async Task Snapshot_ShouldSurviveXmlRoundTrip([Values(false, true)] bool prettyPrint)
        {
            InitializationGraphRecording.IsEnabled = true;
            var builder = new InitializationContextBuilder();
            builder.AddSystem(new SimpleSystem(null)).SetAsCritical();
            builder.AddSystem(new FieldDependentSystem(null));

            await builder.Build().InitializationAsync(CancellationToken.None);

            InitializationGraphSnapshot original = _snapshots.Single();
            InitializationGraphSnapshot restored = InitializationGraphSnapshot.FromXml(original.ToXml(prettyPrint));

            Assert.AreEqual(original.RecordedAtUtc, restored.RecordedAtUtc);
            Assert.AreEqual(original.Status, restored.Status);
            Assert.AreEqual(original.TotalMilliseconds, restored.TotalMilliseconds, MillisecondsTolerance);
            Assert.AreEqual(original.Systems.Count, restored.Systems.Count);

            for (int i = 0; i < original.Systems.Count; i++)
            {
                InitializationSystemRecord expected = original.Systems[i];
                InitializationSystemRecord actual = restored.Systems[i];
                Assert.AreEqual(expected.TypeName, actual.TypeName);
                Assert.AreEqual(expected.FullTypeName, actual.FullTypeName);
                Assert.AreEqual(expected.StartOrder, actual.StartOrder);
                Assert.AreEqual(expected.IsCritical, actual.IsCritical);
                Assert.AreEqual(expected.Status, actual.Status);
                Assert.AreEqual(expected.StartMilliseconds, actual.StartMilliseconds, MillisecondsTolerance);
                Assert.AreEqual(expected.DurationMilliseconds, actual.DurationMilliseconds, MillisecondsTolerance);
                CollectionAssert.AreEqual(expected.DependencyIndices, actual.DependencyIndices);
                Assert.AreEqual(expected.Error, actual.Error);
            }
        }

        [Test]
        public void Snapshot_ShouldSurviveXmlRoundTrip_WhenSystemFailed()
        {
            InitializationGraphRecording.IsEnabled = true;
            var builder = new InitializationContextBuilder();
            builder.AddSystem(new FailingSystem());
            builder.AddSystem(new FailingSystemDependent());

            InitializationContext context = builder.Build();
            Assert.Throws<InvalidOperationException>(
                () => context.InitializationAsync(CancellationToken.None).GetAwaiter().GetResult());

            InitializationGraphSnapshot original = _snapshots.Single();
            InitializationGraphSnapshot restored = InitializationGraphSnapshot.FromXml(original.ToXml(true));

            Assert.AreEqual(InitializationGraphStatus.Failed, restored.Status);

            InitializationSystemRecord failing = restored.Systems[IndexOf(restored, typeof(FailingSystem))];
            Assert.AreEqual(InitializationSystemStatus.Failed, failing.Status);
            StringAssert.Contains(FailingSystem.ErrorMessage, failing.Error);

            int dependentIndex = IndexOf(restored, typeof(FailingSystemDependent));
            InitializationSystemRecord dependent = restored.Systems[dependentIndex];
            Assert.AreEqual(InitializationSystemStatus.NotStarted, dependent.Status);
            Assert.AreEqual(-1, dependent.StartOrder);
            Assert.AreEqual(string.Empty, dependent.Error);
            CollectionAssert.AreEqual(
                original.Systems[dependentIndex].DependencyIndices, dependent.DependencyIndices);
        }

        [Test]
        public void FromXml_ShouldThrow_WhenXmlIsEmpty()
        {
            Assert.Throws<ArgumentException>(() => InitializationGraphSnapshot.FromXml(string.Empty));
        }

        [Test]
        public void FromXml_ShouldThrow_WhenXmlIsMalformed()
        {
            Assert.Throws<ArgumentException>(() => InitializationGraphSnapshot.FromXml("<InitializationGraph>"));
        }

        [Test]
        public void FromXml_ShouldThrow_WhenRootIsUnexpected()
        {
            Assert.Throws<ArgumentException>(() => InitializationGraphSnapshot.FromXml("<Other />"));
        }

        private void SnapshotRecordedHandler(InitializationGraphSnapshot snapshot)
        {
            _snapshots.Add(snapshot);
        }

        private static int IndexOf(InitializationGraphSnapshot snapshot, Type systemType)
        {
            for (int i = 0; i < snapshot.Systems.Count; i++)
            {
                if (snapshot.Systems[i].FullTypeName == systemType.FullName)
                {
                    return i;
                }
            }

            Assert.Fail($"System '{systemType.FullName}' is missing in the snapshot.");
            return -1;
        }

        private sealed class FailingSystem : IInitializable
        {
            public const string ErrorMessage = "Failing system error.";

            public Task InitializeAsync(CancellationToken token) =>
                Task.FromException(new InvalidOperationException(ErrorMessage));
        }

        private sealed class FailingSystemDependent : IInitializable
        {
            [InitDependency]
            private FailingSystem _dependency;

            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private sealed class CancellingSystem : IInitializable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;

            public CancellingSystem(CancellationTokenSource cancellationTokenSource)
            {
                _cancellationTokenSource = cancellationTokenSource;
            }

            public Task InitializeAsync(CancellationToken token)
            {
                _cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            }
        }

        private sealed class CancellingSystemDependent : IInitializable
        {
            [InitDependency]
            private CancellingSystem _dependency;

            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }
    }
}
