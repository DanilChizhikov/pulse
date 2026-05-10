using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DTech.Pulse.Tests
{
    [TestFixture]
    internal sealed class InitializationContextBuilderTests
    {
        private Type _nodeType;
        private ConstructorInfo _nodeCtor;
        private MethodInfo _addDependenciesMethod;
        private MethodInfo _removeDependenciesMethod;
        private MethodInfo _getDependenciesMethod;

        [SetUp]
        public void Setup()
        {
            var assembly = typeof(InitializationContextBuilder).Assembly;
            _nodeType = assembly.GetType("DTech.Pulse.InitializationNode", throwOnError: true);
            _nodeCtor = _nodeType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(IInitializable) },
                modifiers: null);

            _addDependenciesMethod = _nodeType.GetMethod("AddDependencies",
                BindingFlags.Instance | BindingFlags.Public);
            _removeDependenciesMethod = _nodeType.GetMethod("RemoveDependencies",
                BindingFlags.Instance | BindingFlags.Public);
            _getDependenciesMethod = _nodeType.GetMethod("GetDependencies",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.NotNull(_nodeCtor);
            Assert.NotNull(_addDependenciesMethod);
            Assert.NotNull(_removeDependenciesMethod);
            Assert.NotNull(_getDependenciesMethod);
        }

        [Test]
        public void Build_ShouldRespectDependencies_FromFieldPropertyAndMethodAttributes()
        {
            var initOrder = new List<Type>();
            var builder = new InitializationContextBuilder();

            var log = new List<string>();

            var simple = new SimpleSystem(log);
            var fieldDependent = new FieldDependentSystem(log);
            var propertyDependent = new PropertyDependentSystem(log);
            var methodDependent = new MethodDependentSystem(log);

            builder.AddSystem(simple)
                   .OnStartInitialize(t => initOrder.Add(t));

            builder.AddSystem(fieldDependent)
                   .OnStartInitialize(t => initOrder.Add(t));

            builder.AddSystem(propertyDependent)
                   .OnStartInitialize(t => initOrder.Add(t));

            builder.AddSystem(methodDependent)
                   .OnStartInitialize(t => initOrder.Add(t));

            var context = builder.Build();

            context.InitializationAsync(CancellationToken.None)
                   .GetAwaiter()
                   .GetResult();

            int simpleIndex = initOrder.IndexOf(typeof(SimpleSystem));
            Assert.That(simpleIndex, Is.GreaterThanOrEqualTo(0), "SimpleSystem must be present in the initialization order.");

            int fieldIndex = initOrder.IndexOf(typeof(FieldDependentSystem));
            int propertyIndex = initOrder.IndexOf(typeof(PropertyDependentSystem));
            int methodIndex = initOrder.IndexOf(typeof(MethodDependentSystem));

            Assert.That(fieldIndex, Is.GreaterThan(simpleIndex));
            Assert.That(propertyIndex, Is.GreaterThan(simpleIndex));
            Assert.That(methodIndex, Is.GreaterThan(simpleIndex));
        }

        [Test]
        public void Build_ShouldThrow_OnCyclicDependencies()
        {
            var builder = new InitializationContextBuilder();
            var a = new CyclicSystemA();
            var b = new CyclicSystemB();

            builder.AddSystem(a);
            builder.AddSystem(b);

            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
            StringAssert.Contains("Cyclic dependencies detected", exception!.Message);
        }

        [Test]
        public void Initialization_ShouldInvokeCriticalEvent_WhenAllCriticalSystemsInitialized()
        {
            var builder = new InitializationContextBuilder();

            var critical1 = new CriticalSystem();
            var critical2 = new CriticalSystemB();
            var nonCritical = new SimpleSystem(null);

            builder.AddSystem(critical1).SetAsCritical();
            builder.AddSystem(critical2).SetAsCritical();
            builder.AddSystem(nonCritical);

            var context = builder.Build();

            bool eventInvoked = false;
            context.OnCriticalSystemsInitialized += () => eventInvoked = true;

            context.InitializationAsync(CancellationToken.None)
                   .GetAwaiter()
                   .GetResult();

            Assert.IsTrue(critical1.Initialized, "Critical System 1 must be initialized.");
            Assert.IsTrue(critical2.Initialized, "Critical System 2 must be initialized.");
            Assert.IsTrue(eventInvoked, "Event OnCriticalSystemsInitialized must be invoked after initialization all critical systems.");
        }

        [Test]
        public void NodeHandle_Callbacks_ShouldBeInvoked_OnStartAndOnComplete()
        {
            var builder = new InitializationContextBuilder();
            var system = new SimpleSystem(null);

            bool startCalled = false;
            bool completeCalled = false;

            builder.AddSystem(system)
                   .OnStartInitialize(type =>
                   {
                       startCalled = true;
                       Assert.AreEqual(typeof(SimpleSystem), type);
                   })
                   .OnCompleteInitialize(type =>
                   {
                       completeCalled = true;
                       Assert.AreEqual(typeof(SimpleSystem), type);
                   });

            var context = builder.Build();

            context.InitializationAsync(CancellationToken.None)
                   .GetAwaiter()
                   .GetResult();

            Assert.IsTrue(startCalled, "Callback OnStartInitialize must be invoked.");
            Assert.IsTrue(completeCalled, "Callback OnCompleteInitialize must be invoked.");
        }

        [Test]
        public void CriticalSystemInitialization_Precedes_OtherSystems()
        {
            var builder = new InitializationContextBuilder();
            var criticalSystem = new CriticalSystem();
            var a = new DepA();
            bool aInitialized = false;
            bool criticalInitializedBeforeA = false;

            builder.AddSystem(criticalSystem).SetAsCritical().OnStartInitialize(type =>
            {
                criticalInitializedBeforeA = !aInitialized;
            });

            builder.AddSystem(a).OnStartInitialize(type => aInitialized = true);
            InitializationContext context = builder.Build();

            context.InitializationAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(criticalInitializedBeforeA, "Critical system must be initialized before A.");
        }

        [Test]
        public void Build_ShouldThrow_WhenDependencyIsMissing()
        {
            var builder = new InitializationContextBuilder();
            var criticalSystem = new CriticalSystem();
            var dummy = new DummySystem();

            builder.AddSystem(criticalSystem).SetAsCritical();
            builder.AddSystem(dummy).AddDependency<DepA>();

            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
            StringAssert.Contains("which was not added", exception!.Message);
        }

        [Test]
        public void AddSystem_ShouldThrow_WhenRuntimeTypeAlreadyRegistered()
        {
            var builder = new InitializationContextBuilder();

            builder.AddSystem(new SimpleSystem(null));

            var exception = Assert.Throws<InvalidOperationException>(() => builder.AddSystem(new SimpleSystem(null)));
            StringAssert.Contains("already registered", exception!.Message);
        }

        [Test]
        public void NodeHandle_ShouldThrow_WhenMutatedAfterBuild()
        {
            var builder = new InitializationContextBuilder();
            IInitializationNodeHandle handle = builder.AddSystem(new SimpleSystem(null));

            builder.Build();

            Assert.Throws<InvalidOperationException>(() => handle.AddDependency<DepA>());
            Assert.Throws<InvalidOperationException>(() => handle.RemoveDependency<DepA>());
            Assert.Throws<InvalidOperationException>(() => handle.SetAsCritical());
        }

        [Test]
        public void Build_ShouldThrow_WhenInterfaceDependencyHasMultipleCandidates()
        {
            var builder = new InitializationContextBuilder();

            builder.AddSystem(new InterfaceDependencyA());
            builder.AddSystem(new InterfaceDependencyB());
            builder.AddSystem(new AmbiguousInterfaceDependentSystem());

            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
            StringAssert.Contains("matches multiple registered systems", exception!.Message);
        }

        [Test]
        public void Build_ShouldPreferExactDependency_WhenAssignableCandidatesExist()
        {
            var builder = new InitializationContextBuilder();

            builder.AddSystem(new BaseDependencySystem());
            builder.AddSystem(new DerivedDependencySystem());
            builder.AddSystem(new ExactBaseDependentSystem());

            Assert.DoesNotThrow(() => builder.Build());
        }

        [Test]
        public void AddSystem_ShouldThrow_WhenMultipleConstructorsHaveNoDependencyAttribute()
        {
            var builder = new InitializationContextBuilder();

            var exception = Assert.Throws<InvalidOperationException>(
                () => builder.AddSystem(new MultipleConstructorsWithoutAttributeSystem()));
            StringAssert.Contains("Mark one constructor", exception!.Message);
        }

        [Test]
        public void AddSystem_ShouldThrow_WhenMultipleConstructorsHaveDependencyAttribute()
        {
            var builder = new InitializationContextBuilder();

            var exception = Assert.Throws<InvalidOperationException>(
                () => builder.AddSystem(new MultipleMarkedConstructorsSystem()));
            StringAssert.Contains("multiple constructors marked", exception!.Message);
        }

        [Test]
        public async Task Initialization_ShouldInvokeCriticalEvent_BeforeSlowNonCriticalInSameBatchCompletes()
        {
            var builder = new InitializationContextBuilder();
            var slowCompletion = new TaskCompletionSource<bool>();

            builder.AddSystem(new CriticalSystem()).SetAsCritical();
            builder.AddSystem(new SlowSystem(slowCompletion.Task));

            InitializationContext context = builder.Build();
            bool eventInvoked = false;
            context.OnCriticalSystemsInitialized += () => eventInvoked = true;

            Task initialization = context.InitializationAsync(CancellationToken.None);

            Assert.IsTrue(eventInvoked, "Critical event must be invoked as soon as all critical systems are done.");
            Assert.IsFalse(initialization.IsCompleted, "Initialization must still wait for the slow non-critical system.");

            slowCompletion.SetResult(true);
            await initialization;
        }

        [Test]
        public void Initialization_ShouldThrow_WhenContextRunTwice()
        {
            var builder = new InitializationContextBuilder();
            builder.AddSystem(new SimpleSystem(null));

            InitializationContext context = builder.Build();
            context.InitializationAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() =>
                context.InitializationAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void RemoveDependencies_ByExactType_RemovesThatType()
        {
            var node = CreateNode();

            _addDependenciesMethod.Invoke(node, new object[] { new[] { typeof(DepA), typeof(DepB) } });

            _removeDependenciesMethod.Invoke(node, new object[] { new[] { typeof(DepA) } });

            List<Type> deps = GetDependencies(node);

            Assert.That(deps, !Does.Contain(typeof(DepA)));
            Assert.That(deps, Does.Contain(typeof(DepB)));
        }

        [Test]
        public void RemoveDependencies_ByBaseType_RemovesAllDerivedDependencies()
        {
            var node = CreateNode();

            _addDependenciesMethod.Invoke(node, new object[] { new[] { typeof(DepA), typeof(DepB) } });

            _removeDependenciesMethod.Invoke(node, new object[] { new[] { typeof(IBaseDep) } });

            List<Type> deps = GetDependencies(node);

            Assert.That(deps, Is.Empty);
        }

        [Test]
        public void RemoveDependencies_CanBeCalledMultipleTimes()
        {
            var node = CreateNode();

            _addDependenciesMethod.Invoke(node, new object[] { new[] { typeof(DepA), typeof(DepB) } });

            _removeDependenciesMethod.Invoke(node, new object[] { new[] { typeof(DepA) } });
            _removeDependenciesMethod.Invoke(node, new object[] { new[] { typeof(DepB) } });

            List<Type> deps = GetDependencies(node);

            Assert.That(deps, Is.Empty);
        }

        private object CreateNode()
        {
            var dummy = new DummySystem();
            return _nodeCtor.Invoke(new object[] { dummy });
        }

        private List<Type> GetDependencies(object node)
        {
            return (List<Type>)_getDependenciesMethod.Invoke(node, Array.Empty<object>());
        }

        private interface IInterfaceDependency : IInitializable { }

        private sealed class InterfaceDependencyA : IInterfaceDependency
        {
            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private sealed class InterfaceDependencyB : IInterfaceDependency
        {
            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private sealed class AmbiguousInterfaceDependentSystem : IInitializable
        {
            [InitDependency]
            private IInterfaceDependency _dependency;

            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private class BaseDependencySystem : IInitializable
        {
            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private sealed class DerivedDependencySystem : BaseDependencySystem { }

        private sealed class ExactBaseDependentSystem : IInitializable
        {
            [InitDependency]
            private BaseDependencySystem _dependency;

            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private sealed class MultipleConstructorsWithoutAttributeSystem : IInitializable
        {
            public MultipleConstructorsWithoutAttributeSystem() { }

            public MultipleConstructorsWithoutAttributeSystem(SimpleSystem simpleSystem) { }

            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private sealed class MultipleMarkedConstructorsSystem : IInitializable
        {
            [InitDependency]
            public MultipleMarkedConstructorsSystem() { }

            [InitDependency]
            public MultipleMarkedConstructorsSystem(SimpleSystem simpleSystem) { }

            public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        }

        private sealed class SlowSystem : IInitializable
        {
            private readonly Task _task;

            public SlowSystem(Task task)
            {
                _task = task;
            }

            public Task InitializeAsync(CancellationToken token) => _task;
        }
    }
}
