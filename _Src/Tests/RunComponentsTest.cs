using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Hosting;
using SimpleContainer.Infection;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class RunComponentsTest : SimpleContainerTestBase
	{
		public class Simple : RunComponentsTest
		{
			public class ComponentWrap
			{
				public readonly Component2 component2;

				public ComponentWrap(Component2 component2)
				{
					this.component2 = component2;
				}
			}

			public class Component2 : IComponent
			{
				public readonly IntermediateService intermediateService;

				public Component2(IntermediateService intermediateService)
				{
					this.intermediateService = intermediateService;
					LogBuilder.Append("Component2.ctor ");
				}

				public void Run()
				{
					LogBuilder.Append("Component2.Run ");
				}
			}

			public class IntermediateService
			{
				public Component1 component1;

				public IntermediateService(Component1 component1)
				{
					this.component1 = component1;
					LogBuilder.Append("IntermediateService.ctor ");
				}
			}

			public class Component1 : IComponent
			{
				public Component0 component0;

				public Component1(Component0 component0)
				{
					this.component0 = component0;
					LogBuilder.Append("Component1.ctor ");
				}

				public void Run()
				{
					LogBuilder.Append("Component1.Run ");
				}
			}

			public class Component0 : IComponent
			{
				public Component0()
				{
					LogBuilder.Append("Component0.ctor ");
				}

				public void Run()
				{
					LogBuilder.Append("Component0.Run ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Run();
				Assert.That(LogBuilder.ToString(), Is.EqualTo(""));
				container.Get<ComponentWrap>();
				Assert.That(LogBuilder.ToString(),
					Is.EqualTo("Component0.ctor Component1.ctor IntermediateService.ctor Component2.ctor "));
				LogBuilder.Clear();
				container.Run();
				Assert.That(LogBuilder.ToString(), Is.EqualTo("Component0.Run Component1.Run Component2.Run "));
			}
		}

		public class CorrectOrderingWhenContractsUsed : RunComponentsTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([RequireContract("x")] B b, [RequireContract("x")] C c)
				{
					this.b = b;
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

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(x => x.Contract("x"));
				container.Get<A>();
				var instanceCache = container.GetInstanceCache<object>();
				Assert.That(instanceCache.Select(x => x.Instance.GetType()).ToArray(),
					Is.EqualTo(new[] {typeof (C), typeof (B), typeof (A)}));
			}
		}

		public class DoNotRunNotUsedComponents : RunComponentsTest
		{
			private static readonly StringBuilder logBuilder = new StringBuilder();

			public class A
			{
				public readonly BWrap b;

				public A([Optional] BWrap b)
				{
					this.b = b;
				}
			}

			public class BWrap
			{
				public readonly B b;
				public readonly FilteredService filteredService;

				public BWrap(B b, FilteredService filteredService)
				{
					this.b = b;
					this.filteredService = filteredService;
				}
			}

			public class FilteredService
			{
			}

			public class B : IComponent
			{
				public void Run()
				{
					logBuilder.Append("Run ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.WithInstanceFilter<FilteredService>(x => false));
				var a = container.Run<A>();
				Assert.That(a.b, Is.Null);
				Assert.That(logBuilder.ToString(), Is.Empty);
			}
		}

		public class RunUsesInstancesReachableFromAllResolutionContexts : RunComponentsTest
		{
			private static readonly StringBuilder logBuilder = new StringBuilder();

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
				public readonly C c;

				public B(C c)
				{
					this.c = c;
				}
			}

			public class C : IComponent
			{
				public void Run()
				{
					logBuilder.Append("Run ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<B>();
				container.Run<A>();
				Assert.That(logBuilder.ToString(), Is.EqualTo("Run "));
			}
		}

		public class RunUsesInstancesCreatedByFactories : RunComponentsTest
		{
			private static readonly StringBuilder logBuilder = new StringBuilder();

			public class A
			{
				public readonly B b;

				public A(Func<B> createB)
				{
					b = createB();
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

			public class C : IComponent
			{
				public void Run()
				{
					logBuilder.Append("Run ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Run<A>();
				Assert.That(logBuilder.ToString(), Is.EqualTo("Run "));
			}
		}

		public class RunWithRunLogger : RunComponentsTest
		{
			private static StringBuilder log;

			protected override void SetUp()
			{
				base.SetUp();
				log = new StringBuilder();
			}

			public class SimpleComponentLogger : IComponentLogger
			{
				public IDisposable OnRunComponent(ServiceInstance<IComponent> component)
				{
					log.Append(component.FormatName() + ".start\r\n");
					return new ActionDisposable(() => log.Append(component.FormatName() + ".finish\r\n"));
				}

				public void TRASH_DumpConstructionLog(string constructionLog)
				{
				}
			}

			public class ComponentA : IComponent
			{
				public readonly ComponentB componentB;

				public ComponentA(ComponentB componentB)
				{
					this.componentB = componentB;
				}

				public void Run()
				{
					log.Append("ComponentA.Run\r\n");
				}
			}

			public class ComponentB : IComponent
			{
				public void Run()
				{
					log.Append("ComponentB.Run\r\n");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Run<ComponentA>();
				const string componentALog = "ComponentA.start\r\nComponentA.Run\r\nComponentA.finish\r\n";
				const string componentBLog = "ComponentB.start\r\nComponentB.Run\r\nComponentB.finish\r\n";
				Assert.That(log.ToString(), Is.EqualTo(componentBLog + componentALog));
			}
		}
	}
}