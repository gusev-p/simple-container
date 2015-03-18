﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class BasicTest : SimpleContainerTestBase
	{
		public class BindInvalidDependency_CorrectException : BasicTest
		{
			[Test]
			public void Test()
			{
				
				var container = Container(x => x.BindDependency<A>("x", 42));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				Assert.That(error.Message,
					Is.StringContaining("can't cast value [42] from [Int32] to [String] for dependency [x]\r\nWrap!\r\n\tA!"));
			}

			public class A
			{
				public readonly string x;

				public A(string x)
				{
					this.x = x;
				}
			}

			public class Wrap
			{
				public Wrap(A a)
				{
				}
			}
		}

		public class CanConfigureGenericDefinition : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependencyValue(typeof (GenericDefinition<>), typeof (int), 42));
				var outerServices = container.GetAll<IOuterService>().ToArray();
				Assert.That(outerServices.Length, Is.EqualTo(2));
				Assert.That(outerServices.Single(x => x.ServiceType == typeof (ServiceA)).Argument, Is.EqualTo(42));
				Assert.That(outerServices.Single(x => x.ServiceType == typeof (ServiceB)).Argument, Is.EqualTo(42));
			}

			public class GenericDefinition<T> : IOuterService
				where T : IRestriction
			{
				public GenericDefinition(int argument, T service)
				{
					Argument = argument;
					Service = service;
				}

				public T Service { get; private set; }

				public int Argument { get; private set; }

				public Type ServiceType
				{
					get { return typeof (T); }
				}
			}

			public interface IOuterService
			{
				int Argument { get; }
				Type ServiceType { get; }
			}

			public interface IRestriction
			{
			}

			public class ServiceA : IRestriction
			{
			}

			public class ServiceB : IRestriction
			{
			}
		}

		public class CanInjectBaseNotAbstractClass : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(typeof (BaseClass), Is.EqualTo(container.Get<Service>().TypeOfMember));
			}

			public class BaseClass
			{
			}

			public class DerivedClass : BaseClass
			{
			}

			public class Service
			{
				private readonly BaseClass member;

				public Service(BaseClass member)
				{
					this.member = member;
				}

				public Type TypeOfMember
				{
					get { return member.GetType(); }
				}
			}
		}

		public class CanInjectEnumerableByCtor : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<OuterService>().Interfaces,
					Is.EquivalentTo(new IInterface[] {container.Get<Impl1>(), container.Get<Impl2>()}));
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			public class Impl2 : IInterface
			{
			}

			public class OuterService
			{
				public OuterService(IEnumerable<IInterface> interfaces)
				{
					Interfaces = interfaces;
				}

				public IEnumerable<IInterface> Interfaces { get; private set; }
			}
		}

		public class CanInjectArray : BasicTest
		{
			public class A
			{
				public readonly IB[] instances;

				public A(IB[] instances)
				{
					this.instances = instances;
				}
			}

			public interface IB
			{
			}

			public class B1 : IB
			{
			}

			public class B2 : IB
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				Assert.That(a.instances.Select(x => x.GetType()), Is.EquivalentTo(new[] {typeof (B1), typeof (B2)}));
			}
		}

		public class CanInjectFactoriesOfGenerics : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var result = container.Get<SomeService<int>>().Factory();
				Assert.That(result, Is.Not.Null);
				Assert.That(result.GetType(), Is.EqualTo(typeof (ClassA<int>)));
			}

			public class ClassA<T>
			{
			}

			public class SomeService<T>
			{
				public SomeService(Func<ClassA<T>> factory)
				{
					Factory = factory;
				}

				public Func<ClassA<T>> Factory { get; set; }
			}
		}

		public class CanInjectGenericFactories : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var instance = container.Get<SomeService>().Factory(typeof (int), new {argument = 42});
				Assert.That(instance.Type, Is.EqualTo(typeof (int)));
				Assert.That(instance.Argument, Is.EqualTo(42));
			}

			public interface ISomeInterface
			{
				int Argument { get; }
				Type Type { get; }
			}

			public class SomeService
			{
				public SomeService(Func<Type, object, ISomeInterface> factory)
				{
					Factory = factory;
				}

				public Func<Type, object, ISomeInterface> Factory { get; private set; }

				private class SomeGenericService<T> : ISomeInterface
				{
					public SomeGenericService(int argument)
					{
						Argument = argument;
					}

					public int Argument { get; private set; }

					public Type Type
					{
						get { return typeof (T); }
					}
				}
			}
		}

		public class CanInjectGenericService : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var result = container.Get<SomeService<int>>();
				Assert.That(result.otherService, Is.SameAs(container.Get<OtherService<int>>()));
			}

			public class OtherService<T>
			{
			}

			public class SomeService<T>
			{
				public SomeService(OtherService<T> otherService)
				{
					this.otherService = otherService;
				}

				public OtherService<T> otherService { get; private set; }
			}
		}

		public class CanInjectResource : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var service = container.Get<ServiceWithResource>();
				Assert.That(service.GetContent(), Is.EqualTo("the resource"));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<ServiceA>());
				Assert.That(error.Message,
					Is.StringContaining(
						"can't find resource [inexistent.txt] in namespace of [SimpleContainer.Tests.BasicTest+CanInjectResource+ServiceWithInexistentResource], assembly [SimpleContainer.Tests]\r\nServiceA!\r\n\tServiceWithInexistentResource!"));
			}

			public class ServiceA
			{
				public ServiceA(ServiceWithInexistentResource serviceWithInexistentResource)
				{
				}
			}

			public class ServiceWithInexistentResource
			{
				public ServiceWithInexistentResource([FromResource("inexistent.txt")] Stream stream)
				{
				}
			}

			public class ServiceWithResource
			{
				public Stream stream;

				public ServiceWithResource([FromResource("testResource.txt")] Stream stream)
				{
					this.stream = stream;
				}

				public string GetContent()
				{
					return stream.ReadString(new UTF8Encoding(false));
				}
			}
		}

		public class CanInjectSimpleFactories : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var result = container.Get<SomeService>().Factory();
				Assert.That(result, Is.Not.Null);
				Assert.That(result.GetType(), Is.EqualTo(typeof (ClassA)));
			}

			public class ClassA
			{
			}

			public class SomeService
			{
				public SomeService(Func<ClassA> factory)
				{
					Factory = factory;
				}

				public Func<ClassA> Factory { get; set; }
			}
		}
		
		public class CanResolveSimpleFactories : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var factory = container.Get<Func<ClassA>>();
				Assert.That(factory(), Is.Not.SameAs(factory()));
			}

			public class ClassA
			{
			}
		}

		public class CanInjectLazy : BasicTest
		{
			public class A
			{
				public readonly Lazy<B> getB;

				public A(Lazy<B> getB)
				{
					this.getB = getB;
				}
			}

			public class B
			{
				public static int ctorCallCount;

				public B()
				{
					ctorCallCount++;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var instance = container.Get<A>();
				Assert.That(B.ctorCallCount, Is.EqualTo(0));
				Assert.That(instance.getB.Value, Is.Not.Null);
				Assert.That(B.ctorCallCount, Is.EqualTo(1));
			}
		}
		
		public class CanResolveLazy : BasicTest
		{
			public class B
			{
				public static int ctorCallCount;

				public B()
				{
					ctorCallCount++;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var lazy = container.Get<Lazy<B>>();
				Assert.That(B.ctorCallCount, Is.EqualTo(0));
				Assert.That(lazy.Value, Is.Not.Null);
				Assert.That(B.ctorCallCount, Is.EqualTo(1));
			}
		}

		public class CanResolveContainer : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IContainer>(), Is.SameAs(container));
				Assert.That(container.Get<SomeService>(), Is.SameAs(container.Get<IContainer>().Get<SomeService>()));
			}

			public class SomeService
			{
			}
		}

		public class CannotCreateOpenGenerics : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get(typeof(GenericClass<>)));
				Assert.That(error.Message, Is.StringContaining("can't create open generic\r\nGenericClass<T>! <---------------"));
			}

			public class GenericClass<T>
			{
			}
		}

		public class CannotResolveServiceFactory_NoFallback : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.EqualTo("parameter [unresolved] of service [Factory] is not configured\r\nA!\r\n\tFactory!\r\n\t\tunresolved <---------------"));
			}

			private class A
			{
				public class Factory
				{
					public Factory(string unresolved)
					{
					}

					public A Create()
					{
						return new A();
					}
				}
			}
		}

		public class CanChooseConstructorViaInfection : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Service>().CtorName, Is.EqualTo("second"));
			}

			public class Service
			{
				public Service(string someValue)
				{
					CtorName = "first";
				}

				[Infection.ContainerConstructor]
				public Service()
				{
					CtorName = "second";
				}

				public string CtorName { get; private set; }
			}
		}

		public class AnyContainerConstructorAttribute : BasicTest
		{
			public class ContainerConstructorAttribute : Attribute
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Service>().CtorName, Is.EqualTo("second"));
			}

			public class Service
			{
				public Service(string someValue)
				{
					CtorName = "first";
				}

				[ContainerConstructor]
				public Service()
				{
					CtorName = "second";
				}

				public string CtorName { get; private set; }
			}
		}

		public class CrashDuringConstruction_WrapWithContainerException : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				var messageText = exception.ToString();
				Assert.That(messageText, Is.StringContaining("construction exception\r\nOuterService!\r\n\tInnerService!\r\n"));
				Assert.That(messageText, Is.StringContaining("test crash"));
				Assert.That(messageText, Is.StringContaining("InvalidOperationException"));
			}

			public class InnerService
			{
				public InnerService()
				{
					throw new InvalidOperationException("test crash");
				}
			}

			public class OuterService
			{
				public OuterService(InnerService innerService)
				{
					InnerService = innerService;
				}

				public InnerService InnerService { get; private set; }
			}
		}

		public class CyclicDependency : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				Assert.That(error.Message,
					Is.EqualTo(
						"cyclic dependency ClassA ...-> ClassB -> ClassA\r\nOuterService!\r\n\tClassA!\r\n\t\tIntermediate!\r\n\t\t\tClassB!\r\n\t\t\t\tClassA!"));
			}

			public class ClassA
			{
				public ClassA(Intermediate intermediate)
				{
				}
			}

			public class Intermediate
			{
				public Intermediate(ClassB classB)
				{
				}
			}

			public class ClassB
			{
				public ClassB(ClassA classA)
				{
				}
			}

			public class OuterService
			{
				public OuterService(ClassA classA)
				{
				}
			}
		}

		public class DependencyConfiguredWithNull : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(x => x.BindDependency<Impl>("someInterface", null));
				Assert.That(container.Get<Impl>().SomeInterface, Is.Null);
			}

			public interface ISomeInterface
			{
			}

			public class Impl
			{
				public Impl(ISomeInterface someInterface)
				{
					SomeInterface = someInterface;
				}

				public ISomeInterface SomeInterface { get; private set; }
			}
		}

		public class DependencyWithOptionalValue : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Impl>().Parameter, Is.EqualTo(42));
			}

			public class Impl
			{
				public Impl(int parameter = 42)
				{
					Parameter = parameter;
				}

				public int Parameter { get; private set; }
			}
		}

		public class ParameterDefaultValuesAreIndependentServices : BasicTest
		{
			public class A
			{
				public readonly B b;

				public A(B b = null)
				{
					this.b = b;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<B>());
				Assert.That(container.Get<A>().b, Is.Null);
				Assert.That(container.GetAll<B>(), Is.Empty);
			}
		}

		public class DontUse : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.DontUse(typeof (Impl1)));
				Assert.That(container.Get<IInterface>(), Is.SameAs(container.Get<Impl2>()));
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			public class Impl2 : IInterface
			{
			}
		}

		public class EnumerableDependenciesAreRequired : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.StringContaining("A!\r\n\tB!\r\n\t\tunresolved!"));
			}

			public class A
			{
				public A(IEnumerable<B> dependency)
				{
				}
			}

			public class B
			{
				public B(string unresolved)
				{
				}
			}
		}

		public class ErrorMessageForGenerics : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c =>
				{
					c.Bind(typeof (IInterface), typeof (SomeImpl<int>));
					c.Bind(typeof (IInterface), typeof (SomeImpl<string>));
				});
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.EqualTo(
						"many implementations for IInterface\r\n\tSomeImpl<Int32>\r\n\tSomeImpl<String>\r\nIInterface++\r\n\tSomeImpl<Int32>\r\n\tSomeImpl<String>"));
			}

			public interface IInterface
			{
			}

			public class SomeImpl<T> : IInterface
			{
			}
		}

		public class FactoryDependantOnServiceType : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(builder => builder.Bind(c => new ChildService(c.target)));
				Assert.That(container.Get<SomeService>().ChildService.ParentService, Is.EqualTo(typeof (SomeService)));
				Assert.That(container.Get<ChildService>().ParentService, Is.Null);
			}

			public class ChildService
			{
				public ChildService(Type parentService)
				{
					ParentService = parentService;
				}

				public Type ParentService { get; private set; }
			}

			public class SomeService
			{
				public SomeService(ChildService childService)
				{
					ChildService = childService;
				}

				public ChildService ChildService { get; private set; }
			}
		}

		public class GetAfterGetAll_CorrectErrorMessage : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetAll<IInterface>().Count(), Is.EqualTo(2));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.EqualTo("many implementations for IInterface\r\n\tImpl1\r\n\tImpl2\r\nIInterface++\r\n\tImpl1\r\n\tImpl2"));
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			public class Impl2 : IInterface
			{
			}
		}

		public class GracefullErrorMessageForUncreatableTypes : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				const string message = "can't create simple type\r\nSomeClass!\r\n\targument! - <---------------";
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<SomeClass>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public class SomeClass
			{
				public SomeClass(int argument)
				{
					Argument = argument;
				}

				public int Argument { get; private set; }
			}
		}

		public class GracefullErrorMessageWhenNoImplementationFound : BasicTest
		{
			[Test]
			public void Test()
			{
				const string message =
					"no implementations for OuterOuterService\r\nOuterOuterService!\r\n\tOuterService!\r\n\t\tIInterface! - has no implementations";
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterOuterService>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public interface IInterface
			{
			}

			public class OuterOuterService
			{
				public OuterOuterService(OuterService outerService)
				{
					OuterService = outerService;
				}

				public OuterService OuterService { get; private set; }
			}

			public class OuterService
			{
				public OuterService(IInterface @interface)
				{
					Interface = @interface;
				}

				public IInterface Interface { get; private set; }
			}
		}

		public class IgnoreImplementation : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IInterface>(), Is.InstanceOf<Impl1>());
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			[Infection.IgnoredImplementation]
			public class Impl2 : IInterface
			{
			}

			public class IgnoredImplementationAttribute : Attribute
			{
			}

			[IgnoredImplementation]
			public class Impl3 : IInterface
			{
			}
		}

		public class ImplementationWithDependencyConfiguredByType : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependency<Impl, int>(42));
				Assert.That(container.Get<Impl>().Parameter, Is.EqualTo(42));
			}

			public class Impl
			{
				public Impl(int parameter)
				{
					Parameter = parameter;
				}

				public int Parameter { get; private set; }
			}
		}

		public class ImplementationWithDependencyImplementationConfig : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependencyImplementation<Impl, Impl2>("someInterface"));
				Assert.That(container.Get<Impl>().SomeInterface, Is.SameAs(container.Get<Impl2>()));
			}

			public interface ISomeInterface
			{
			}

			public class Impl
			{
				public Impl(ISomeInterface someInterface)
				{
					SomeInterface = someInterface;
				}

				public ISomeInterface SomeInterface { get; private set; }
			}

			public class Impl1 : ISomeInterface
			{
			}

			public class Impl2 : ISomeInterface
			{
			}
		}

		public class ImplementationWithExplicitDelegateFactory : BasicTest
		{
			[Test]
			public void Test()
			{
				var impl = new Impl();
				var container = Container(x => x.Bind<IInterface>(c => impl));
				Assert.That(container.Get<IInterface>(), Is.SameAs(impl));
			}

			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
			}
		}

		public class ExplicitDelegateFactoryWithEnumerableInjection : BasicTest
		{
			public class A
			{
				public readonly IEnumerable<IX> instances;

				public A(IEnumerable<IX> instances)
				{
					this.instances = instances;
				}
			}

			public interface IX
			{
			}

			public class X : IX
			{
				public readonly Type target;

				public X(Type target)
				{
					this.target = target;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<IX>(c => new X(c.target)));
				Assert.That(container.Get<A>().instances.Cast<X>().Single().target, Is.EqualTo(typeof (A)));
			}
		}

		public class ImplementationWithValueConfig : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependency<Impl>("someValue", 42));
				Assert.That(container.Get<Impl>().SomeValue, Is.EqualTo(42));
			}

			public class Impl
			{
				public Impl(int someValue)
				{
					SomeValue = someValue;
				}

				public int SomeValue { get; private set; }
			}
		}

		public class InterfaceWithConstantImplementation : BasicTest
		{
			[Test]
			public void Test()
			{
				var impl = new Impl();
				var container = Container(x => x.Bind<ISomeInterface>(impl));
				Assert.That(container.Get<ISomeInterface>(), Is.SameAs(impl));
			}

			public interface ISomeInterface
			{
			}

			public class Impl : ISomeInterface
			{
			}
		}

		public class MultipleImplementations_CorrectError : BasicTest
		{
			[Test]
			public void Test()
			{
				const string message =
					"many implementations for IInterface\r\n\tImpl1\r\n\tImpl2\r\nWrap!\r\n\tIInterface++\r\n\t\tImpl1\r\n\t\tImpl2";
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public class Wrap
			{
				public readonly IInterface s;

				public Wrap(IInterface s)
				{
					this.s = s;
				}
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			public class Impl2 : IInterface
			{
			}
		}

		public class NotCreatableService_Throw : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				const string message =
					"can't create simple type\r\nOuterService!\r\n\tIInterface!\r\n\t\tImpl1!\r\n\t\t\tparameter! - <---------------";
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
				public Impl1(int parameter)
				{
					Parameter = parameter;
				}

				public int Parameter { get; private set; }
			}

			public class Impl2 : IInterface
			{
			}

			public class OuterService
			{
				public OuterService(IInterface iInterface)
				{
					Interface = iInterface;
				}

				public IInterface Interface { get; set; }
			}
		}

		public class CommentManyCtors : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.StringContaining(
						"many public ctors, maybe some of them should be made private?\r\nIInterface!\r\n\tTestService! - <---------------"));
			}

			public interface IInterface
			{
			}

			public class SomeClass
			{
			}

			public class TestService : IInterface
			{
				public TestService()
					: this(null)
				{
				}

				public TestService(SomeClass someClass)
				{
				}
			}
		}

		public class CommentSkipMessageForPrivateCtors : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.StringContaining("no public ctors, maybe ctor is private?\r\nIInterface!\r\n\tTestService! - <---------------"));
			}

			public interface IInterface
			{
			}

			public class TestService : IInterface
			{
				private TestService()
				{
				}
			}
		}

		public class ServiceFactory : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<SomeService>().otherService, Is.SameAs(container.Get<SomeOtherService>()));
			}

			public class SomeOtherService
			{
			}

			public class SomeService
			{
				public readonly SomeOtherService otherService;

				private SomeService(SomeOtherService otherService)
				{
					this.otherService = otherService;
				}

				public class Factory
				{
					private readonly SomeOtherService otherService;

					public Factory(SomeOtherService otherService)
					{
						this.otherService = otherService;
					}

					public SomeService Create()
					{
						return new SomeService(otherService);
					}
				}
			}
		}

		public class ServiceWithConfig : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.Bind<ISomeInterface, Impl1>());
				Assert.That(container.Get<ISomeInterface>(), Is.SameAs(container.Get<Impl1>()));
			}

			public interface ISomeInterface
			{
			}

			public class Impl1 : ISomeInterface
			{
			}

			public class Impl2 : ISomeInterface
			{
			}
		}

		public class ServiceWithDependency : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<SomeService>().SomeOtherService, Is.SameAs(container.Get<SomeOtherService>()));
			}

			public class SomeOtherService
			{
			}

			public class SomeService
			{
				public SomeService(SomeOtherService someOtherService)
				{
					SomeOtherService = someOtherService;
				}

				public SomeOtherService SomeOtherService { get; private set; }
			}
		}

		public class ServiceWithMultipleImplementations : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetAll<AbstractParent>(),
					Is.EquivalentTo(new AbstractParent[] {container.Get<Child1>(), container.Get<Child2>()}));
			}

			public abstract class AbstractParent
			{
			}

			public class Child1 : AbstractParent
			{
			}

			public class Child2 : AbstractParent
			{
			}
		}

		public class Simple : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<SomeService>(), Is.SameAs(container.Get<SomeService>()));
			}

			public class SomeService
			{
			}
		}

		public class SimpleCreate : BasicTest
		{
			public class ClassA
			{
			}

			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Create<ClassA>(), Is.Not.SameAs(container.Create<ClassA>()));
				Assert.That(container.Create<ClassA>(), Is.Not.SameAs(container.Get<ClassA>()));
				Assert.That(container.Create<IInterface>(), Is.Not.SameAs(container.Get<IInterface>()));
				Assert.That(container.Create<IInterface>(), Is.Not.SameAs(container.Create<IInterface>()));
				Assert.That(container.Get<Impl>(), Is.SameAs(container.Get<Impl>()));
			}
		}

		public class SimpleWithInterface : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<ISomeServiceInterface>(), Is.SameAs(container.Get<ISomeServiceInterface>()));
			}

			public interface ISomeServiceInterface
			{
			}

			public class SomeService : ISomeServiceInterface
			{
			}
		}

		public class SimpleWithParent : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<AbstractParent>(), Is.SameAs(container.Get<AbstractParent>()));
				Assert.That(container.Get<AbstractParent>(), Is.SameAs(container.Get<Child>()));
			}

			public abstract class AbstractParent
			{
			}

			public class Child : AbstractParent
			{
			}
		}

		public class SkipNestedPrivate : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var outerService = container.Get<OuterService>().Interfaces.ToArray();
				Assert.That(outerService.Single(), Is.InstanceOf<OuterPublicService>());
			}

			public interface IInterface
			{
			}

			private class InnerPrivateService : IInterface
			{
			}

			public class OuterPublicService : IInterface
			{
			}

			public class OuterService
			{
				public OuterService(IEnumerable<IInterface> interfaces)
				{
					Interfaces = interfaces;
				}

				public IEnumerable<IInterface> Interfaces { get; private set; }
			}
		}

		public class StructsCannotBeCreated : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<StructConsumer>());
				Assert.That(error.Message,
					Is.StringContaining(string.Format(
						"can't create value type\r\nStructConsumer!\r\n\tSomeStruct! - <---------------")));
				error = Assert.Throws<SimpleContainerException>(() => container.Get<ITestInterface>());
				Assert.That(error.Message,
					Is.StringContaining(string.Format("ITestInterface!\r\n\tSomeStruct! - <---------------")));
			}

			public interface ITestInterface
			{
			}

			public struct SomeStruct : ITestInterface
			{
			}

			public class SomeClass : ITestInterface
			{
			}

			public class StructConsumer
			{
				public readonly SomeStruct someStruct;

				public StructConsumer(SomeStruct someStruct)
				{
					this.someStruct = someStruct;
				}
			}
		}

		public class TrackDependenciesForUnresolvedServices : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.Throws<SimpleContainerException>(() => container.GetAll<ClassA>());
				var childContainer = Container(c => c.BindDependency<ClassB>("argument", "42"));
				Assert.That(childContainer.Get<ClassA>().ClassB.Argument, Is.EqualTo("42"));
			}

			public class ClassA
			{
				public ClassA(ClassB classB)
				{
					ClassB = classB;
				}

				public ClassB ClassB { get; set; }
			}

			public class ClassB
			{
				public ClassB(string argument)
				{
					Argument = argument;
				}

				public string Argument { get; set; }
			}
		}

		public class UseConstructorArgumentNamesInErrorMessageForSimpleTypesOnly : BasicTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				const string message =
					"can't create simple type\r\nOuterService!\r\n\tIInterface!\r\n\t\tChild1!\r\n\t\t\targument! - <---------------";
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				Assert.That(error.Message, Is.EqualTo(message));

				const string message2 = "no implementations for Child2\r\nChild2!\r\n\tIOtherService! - has no implementations";
				error = Assert.Throws<SimpleContainerException>(() => container.Get<Child2>());
				Assert.That(error.Message, Is.EqualTo(message2));
			}

			public class Child1 : IInterface
			{
				public Child1(int argument)
				{
					Argument = argument;
				}

				public int Argument { get; private set; }
			}

			public class Child2 : IInterface
			{
				public Child2(IOtherService otherService)
				{
					OtherService = otherService;
				}

				public IOtherService OtherService { get; private set; }
			}

			public interface IInterface
			{
			}

			public interface IOtherService
			{
			}

			public class OuterService
			{
				public OuterService(IInterface someService)
				{
					SomeService = someService;
				}

				public IInterface SomeService { get; private set; }
			}
		}

		public class DependencyConfigurationNotUsed_FrienldyCrash : BasicTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependency<A>("inexistent", 42));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.EqualTo("unused dependency configurations [name=inexistent]\r\nA! - <---------------"));
			}
		}

		public class UseValueCreatedBy : BasicTest
		{
			[Test]
			public void Test()
			{
				var container =
					Container(c => c.BindDependencyFactory<SomeService>("serviceA", x => new ServiceA(42, x.Get<ServiceB>())));
				var serviceA = container.Get<SomeService>().ServiceA;
				Assert.That(serviceA.Argument, Is.EqualTo(42));
				Assert.That(serviceA.ServiceB, Is.Not.Null);
			}

			public class ServiceA
			{
				public ServiceA(int argument, ServiceB serviceB)
				{
					Argument = argument;
					ServiceB = serviceB;
				}

				public int Argument { get; private set; }
				public ServiceB ServiceB { get; private set; }
			}

			public class ServiceB
			{
			}

			public class SomeService
			{
				public SomeService(ServiceA serviceA)
				{
					ServiceA = serviceA;
				}

				public ServiceA ServiceA { get; private set; }
			}
		}

		public class NoImplementations_Crash : BasicTest
		{
			public interface IInterface
			{
			}

			public class Wrap
			{
				public readonly IEnumerable<IInterface> instances;

				public Wrap(IEnumerable<IInterface> instances)
				{
					this.instances = instances;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Wrap>().instances.Count(), Is.EqualTo(0));
				Assert.That(container.Resolve<Wrap>().GetConstructionLog(),
					Is.EqualTo("Wrap\r\n\tIInterface! - has no implementations"));
			}
		}

		public class CanExplicitlyBindIterfaceToNull : BasicTest
		{
			public class A
			{
				public readonly B b;

				public A(B b)
				{
					this.b = b;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<B>((object) null));
				var instance = container.Get<A>();
				Assert.That(instance.b, Is.Null);
			}
		}

		public class OptionalAttributeTest : BasicTest
		{
			public class WrapWithOptionalDependency
			{
				public readonly A a;

				public WrapWithOptionalDependency([Optional] A a)
				{
					this.a = a;
				}
			}

			public class WrapWithRequiredDependency
			{
				public readonly A a;

				public WrapWithRequiredDependency(A a)
				{
					this.a = a;
				}
			}

			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<A>());
				Assert.That(container.Get<WrapWithOptionalDependency>().a, Is.Null);
				Assert.That(container.Resolve<WrapWithOptionalDependency>().GetConstructionLog(),
					Is.EqualTo("WrapWithOptionalDependency\r\n\tA! - DontUse"));
				Assert.Throws<SimpleContainerException>(() => container.Get<WrapWithRequiredDependency>());
			}
		}

		public class AnyCanBeNullAttributeIsEquivalentToOptional : BasicTest
		{
			public sealed class CanBeNullAttribute : Attribute
			{
			}

			public class A
			{
				public readonly C c;

				public A([CanBeNull] C c)
				{
					this.c = c;
				}
			}

			public class B
			{
				public readonly C c;

				public B(C c)
				{
					this.c = c;
				}
			}

			[IgnoredImplementation]
			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.Throws<SimpleContainerException>(() => container.Get<B>());
				Assert.That(container.Get<A>().c, Is.Null);
			}
		}

		public class ServiceCouldNotBeCreatedException : BasicTest
		{
			public class A
			{
				public readonly IEnumerable<IInterface> enumerable;

				public A(IEnumerable<IInterface> enumerable)
				{
					this.enumerable = enumerable;
				}
			}

			public interface IInterface
			{
			}

			public class B1 : IInterface
			{
				public B1()
				{
					throw new Interface.ServiceCouldNotBeCreatedException("invalid test condition");
				}
			}

			public class B2 : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>().enumerable.Single(), Is.InstanceOf<B2>());
				Assert.That(container.Resolve<A>().GetConstructionLog(),
					Is.EqualTo("A\r\n\tIInterface\r\n\t\tB1! - invalid test condition\r\n\t\tB2"));
			}
		}

		public class LastConfigurationWins : BasicTest
		{
			public class A
			{
				public readonly B parameter;

				public A(B parameter)
				{
					this.parameter = parameter;
				}
			}

			public class B
			{
				public int value;
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Bind(c => new B {value = 1});
					b.Bind<B>(new B {value = 2});
				});
				Assert.That(container.Get<A>().parameter.value, Is.EqualTo(2));
			}
		}

		public class OptionalFunc : BasicTest
		{
			public class Wrap
			{
				public readonly IEnumerable<A> listOfA;

				public Wrap([TestContract("unioned")] IEnumerable<A> listOfA)
				{
					this.listOfA = listOfA;
				}
			}

			public class A
			{
				public B b;

				public A([Optional] Func<B> createB)
				{
					b = createB();
				}
			}

			public class B
			{
				public readonly int parameter;

				public B(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("unioned").UnionOf("c1", "c2");
					builder.Contract("c1").DontUse<B>();
					builder.Contract("c2").BindDependency<B>("parameter", 54);
				});
				Assert.That(container.Get<Wrap>().listOfA.Single().b.parameter, Is.EqualTo(54));
			}
		}
	}
}