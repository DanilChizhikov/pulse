using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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
            // Arrange
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

            // Act
            context.InitializationAsync(CancellationToken.None)
                   .GetAwaiter()
                   .GetResult();

            // Assert:
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
            // Arrange
            var builder = new InitializationContextBuilder();
            var a = new CyclicSystemA();
            var b = new CyclicSystemB();

            builder.AddSystem(a);
            builder.AddSystem(b);

            // Act & Assert
            var exception = Assert.Throws<Exception>(() => builder.Build());
            StringAssert.Contains("Cyclic dependencies detected", exception!.Message);
        }

        [Test]
        public void Initialization_ShouldInvokeCriticalEvent_WhenAllCriticalSystemsInitialized()
        {
            // Arrange
            var builder = new InitializationContextBuilder();

            var critical1 = new CriticalSystem();
            var critical2 = new CriticalSystemB();
            var nonCritical = new SimpleSystem(null);

            builder.AddSystem(critical1).SetCritical();
            builder.AddSystem(critical2).SetCritical();
            builder.AddSystem(nonCritical);

            var context = builder.Build();

            bool eventInvoked = false;
            context.OnCriticalSystemsInitialized += () => eventInvoked = true;

            // Act
            context.InitializationAsync(CancellationToken.None)
                   .GetAwaiter()
                   .GetResult();

            // Assert
            Assert.IsTrue(critical1.Initialized, "Critical System 1 must be initialized.");
            Assert.IsTrue(critical2.Initialized, "Critical System 2 must be initialized.");
            Assert.IsTrue(eventInvoked, "Event OnCriticalSystemsInitialized must be invoked after initialization all critical systems.");
        }

        [Test]
        public void NodeHandle_Callbacks_ShouldBeInvoked_OnStartAndOnComplete()
        {
            // Arrange
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

            // Act
            context.InitializationAsync(CancellationToken.None)
                   .GetAwaiter()
                   .GetResult();

            // Assert
            Assert.IsTrue(startCalled, "Callback OnStartInitialize must be invoked.");
            Assert.IsTrue(completeCalled, "Callback OnCompleteInitialize must be invoked.");
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
    }
}