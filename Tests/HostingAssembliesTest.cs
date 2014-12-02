﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Hosting;
using SimpleContainer.Implementation;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class HostingAssembliesTest : UnitTestBase
	{
		public class ImplementationsFromIndependentPrimaryAssemblies : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class A1Component: IComponent
						{
							public void Run()
							{
							}
						}
					}
				";

			private const string a2Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A2
					{
						public class A2Component: IComponent
						{
							public void Run()
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(a1Code);
				var a2 = CompileAssembly(a2Code);
				using (var staticContainer = Factory().FromAssemblies(new[] {a1, a2}))
				{
					using (var localContainer = staticContainer.CreateLocalContainer(a1, null))
						Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("A1Component"));

					using (var localContainer = staticContainer.CreateLocalContainer(a2, null))
						Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("A2Component"));
				}
			}
		}

		public class AcceptImplementationFromReferencedAssembly : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class Component1: IComponent
						{
							public void Run()
							{
							}
						}
					}
				";

			private const string a2Code = @"
					namespace A2
					{
						public class Impl2
						{
							public Impl2(A1.Component1 doNotAllowsCompilerRemoveReferenceToA1)
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(a1Code);
				var a2 = CompileAssembly(a2Code, a1);
				using (var staticContainer = Factory().FromAssemblies(new[] {a1, a2}))
				using (var localContainer = staticContainer.CreateLocalContainer(a2, null))
					Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("Component1"));
			}
		}

		public class AcceptImplementationFromAssemblyReferencedViaAttribute : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class Component1: IComponent
						{
							public void Run()
							{
							}
						}
					}
				";

			private const string a2CodeFormat = @"
					using SimpleContainer.Infection;

					[assembly: ContainerReference(""{0}"")]

					namespace A2
					{{
						public class Impl2
						{{
						}}
					}}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(a1Code);
				var a2 = CompileAssembly(string.Format(a2CodeFormat, a1.GetName().Name), a1);
				using (var staticContainer = Factory().FromAssemblies(new[] {a1, a2}))
				using (var localContainer = staticContainer.CreateLocalContainer(a2, null))
					Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("Component1"));
			}
		}

		public class DoNotUseConfiguratorsFromUnreferencedAssemblies : HostingAssembliesTest
		{
			private const string mainAssemblyCode = @"
					using SimpleContainer.Hosting;
					using System;

					namespace A1
					{
						public class Impl1: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}

						public class Impl2: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}
					}
				";

			private const string unreferencedAssemblyCode = @"
					using SimpleContainer.Configuration;
					using SimpleContainer;
					using System;
					using A1;

					namespace A2
					{
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl1>();
							}
						}
					}
				";

			private const string entryAssemblyCode = @"
					using SimpleContainer.Hosting;
					using SimpleContainer.Infection;
					using System;

					[assembly: ContainerReference(""{0}"")]

					namespace A3
					{{
						public class Component: IComponent
						{{
							public Component(IServiceProvider serviceProvider)
							{{
							}}

							public void Run()
							{{
							}}
						}}
					}}
				";

			[Test]
			public void Test()
			{
				var mainAssembly = CompileAssembly(mainAssemblyCode);
				var unreferencedAssembly = CompileAssembly(unreferencedAssemblyCode, mainAssembly);
				var entryAssembly = CompileAssembly(string.Format(entryAssemblyCode, mainAssembly.GetName().Name), mainAssembly);

				using (var staticContainer = Factory().FromAssemblies(new[] {mainAssembly, unreferencedAssembly, entryAssembly}))
				using (var localContainer = staticContainer.CreateLocalContainer(entryAssembly, null))
				{
					var error = Assert.Throws<SimpleContainerException>(() => localContainer.Get<IComponent>());
					Assert.That(error.Message, Is.StringContaining("many implementations for IServiceProvider\r\n\tImpl1\r\n\tImpl2"));
				}
			}
		}

		public class LocalConfiguratorsExecuteLast : HostingAssembliesTest
		{
			private const string referencedAssemblycode = @"
					using SimpleContainer.Configuration;
					using SimpleContainer;
					using System;

					namespace A1
					{
						public class Impl1: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}

						public class Impl2: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}
				
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl1>();
							}
						}
					}
				";

			private const string primaryAssemblyCode = @"
					using SimpleContainer.Configuration;
					using System;
					using A1;

					namespace A2
					{
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl2>(true);
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(referencedAssemblycode);
				var a2 = CompileAssembly(primaryAssemblyCode, a1);
				using (var staticContainer = Factory().FromAssemblies(new[] {a2, a1}))
				using (var localContainer = staticContainer.CreateLocalContainer(a2, null))
					Assert.That(localContainer.Get<IServiceProvider>().GetType().Name, Is.EqualTo("Impl2"));
			}
		}

		protected Assembly CompileAssembly(string source, params Assembly[] references)
		{
			var testAssemblyName = "tmp_" + Guid.NewGuid().ToString("N");
			var tempAssemblyFileName = testAssemblyName + ".dll";
			var compilationParameters = new CompilerParameters
			{
				OutputAssembly = tempAssemblyFileName,
				GenerateExecutable = false
			};
			var defaultAssemblies = new[]
			{
				Assembly.GetExecutingAssembly(),
				typeof (IComponent).Assembly,
				typeof (NameValueCollection).Assembly
			};
			foreach (var reference in references.Concat(defaultAssemblies).Select(x => x.GetName().Name + ".dll"))
				compilationParameters.ReferencedAssemblies.Add(reference);
			var compilationResult = CodeDomProvider.CreateProvider("C#").CompileAssemblyFromSource(compilationParameters, source);
			if (compilationResult.Errors.HasErrors || compilationResult.Errors.HasWarnings)
			{
				var message = compilationResult.Errors
					.Cast<CompilerError>()
					.Select(x => string.Format("{0}:{1} {2}", x.Line, x.Column, x.ErrorText))
					.JoinStrings("\r\n");
				Assert.Fail(message);
			}
			return compilationResult.CompiledAssembly;
		}

		protected static ContainerFactory Factory()
		{
			return new ContainerFactory(x => x.Name.StartsWith("tmp_"));
		}
	}
}