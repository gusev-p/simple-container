using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Services;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public static class ContainerExtensions
	{
		public static T Get<T>(this IContainer container, string contract = null, bool dumpConstructionLog = false)
		{
			return (T)container.Get(typeof(T), contract, dumpConstructionLog);
		}

		public static ResolvedService<T> Resolve<T>(this IContainer container, params string[] contracts)
		{
			return new ResolvedService<T>(container.Resolve(typeof (T), contracts));
		}

		public static object Get(this IContainer container, Type type, string contract = null, bool dumpConstructionLog = false)
		{
			var resolvedService = container.Resolve(type, string.IsNullOrEmpty(contract) ? new string[0] : new[] {contract});
			resolvedService.Run(dumpConstructionLog);
			return resolvedService.Single();
		}

		public static ResolvedService Create(this IContainer container, Type type, string contract = null, object arguments = null)
		{
			var result = container.Create(type, string.IsNullOrEmpty(contract) ? new string[0] : new[] {contract}, arguments);
			result.Run();
			return result;
		}

		public static T Create<T>(this IContainer container, string contract = null, object arguments = null)
		{
			var result = container.Create(typeof (T), contract, arguments);
			return (T) result.Single();
		}

		public static IEnumerable<Type> GetImplementationsOf<T>(this IContainer container)
		{
			return container.GetImplementationsOf(typeof (T));
		}

		public static IEnumerable<object> GetAll(this IContainer container, Type type)
		{
			return container.Resolve(type, new string[0]).All();
		}

		public static T[] GetAll<T>(this IContainer container)
		{
			return container.GetAll(typeof (T)).Cast<T>().ToArray();
		}

		public static bool TryGet<T>(this IContainer container, out T result)
		{
			return container.GetAll<T>().TrySingle(out result);
		}

		public static IEnumerable<object> GetDependencyValues(this IContainer container, Type type)
		{
			return container.GetDependencies(type).SelectMany(container.GetAll);
		}

		public static IEnumerable<TDependency> GetDependencyValuesOfType<TDependency>(this IContainer container, Type type)
		{
			return container.GetDependencies(type)
				.Where(x => typeof (TDependency).IsAssignableFrom(x))
				.SelectMany(container.GetAll)
				.Cast<TDependency>();
		}

		public static IEnumerable<object> GetDependencyValuesRecursive(this IContainer container, Type type)
		{
			return container.GetDependenciesRecursive(type).SelectMany(container.GetAll);
		}

		public static IEnumerable<Type> GetDependenciesRecursive(this IContainer container, Type type)
		{
			return Utils.Closure(type, container.GetDependencies).Skip(1);
		}
	}
}