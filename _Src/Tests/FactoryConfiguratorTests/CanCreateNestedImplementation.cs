﻿using System;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public class CanCreateNestedImplementation : SimpleContainerTestBase
	{
		public interface IServiceB
		{
		}

		public class ServiceA
		{
			public readonly Func<Type, object, IServiceB> func;

			public ServiceA(Func<Type, object, IServiceB> func)
			{
				this.func = func;
			}

			public class ServiceB<T> : IServiceB
			{
				public readonly int parameter;

				public ServiceB(int parameter)
				{
					this.parameter = parameter;
				}
			}
		}

		[Test]
		public void Test()
		{
			var serviceA = Container().Get<ServiceA>();
			var serviceB = serviceA.func(typeof (int), new {parameter = 42});
			Assert.That(serviceB, Is.InstanceOf<ServiceA.ServiceB<int>>());
			Assert.That(((ServiceA.ServiceB<int>) serviceB).parameter, Is.EqualTo(42));
		}
	}
}