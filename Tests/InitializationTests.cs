using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace DTech.Pulse.Tests
{
	[TestFixture]
    internal sealed class InitializationContextBuilderTests
    {
        [Test]
        public void AddSystem_ShouldThrow_WhenSystemTypeAlreadyAdded()
        {
            // Arrange
            var builder = new InitializationContextBuilder();
            var system1 = new SimpleSystem(null);
            var system2 = new SimpleSystem(null);

            builder.AddSystem(system1);

            // Act & Assert
            var exception = Assert.Throws<Exception>(() => builder.AddSystem(system2));
            StringAssert.Contains(typeof(SimpleSystem).FullName, exception!.Message);
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
    }
}