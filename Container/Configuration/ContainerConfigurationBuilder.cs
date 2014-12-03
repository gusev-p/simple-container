using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ContainerConfigurationBuilder
	{
		private readonly bool isStaticConfiguration;
		protected readonly IDictionary<Type, object> configurations = new Dictionary<Type, object>();
		private readonly ISet<Type> staticServices;

		private readonly IDictionary<string, ContractConfigurationBuilder> contractConfigurators =
			new Dictionary<string, ContractConfigurationBuilder>();

		public ContainerConfigurationBuilder(ISet<Type> staticServices, bool isStaticConfiguration)
		{
			this.staticServices = staticServices;
			this.isStaticConfiguration = isStaticConfiguration;
		}

		public ContainerConfigurationBuilder Bind<TInterface, TImplementation>(bool clearOld = false)
			where TImplementation : TInterface
		{
			return Bind(typeof (TInterface), typeof (TImplementation), clearOld);
		}

		public ContainerConfigurationBuilder Bind(Type interfaceType, Type implementationType, bool clearOld = false)
		{
			if (!interfaceType.IsAssignableFrom(implementationType))
				throw new SimpleContainerException(string.Format("[{0}] is not assignable from [{1}]", interfaceType.FormatName(),
					implementationType.FormatName()));
			GetOrCreate<InterfaceConfiguration>(interfaceType).AddImplementation(implementationType, clearOld);
			return this;
		}

		public ContainerConfigurationBuilder Bind<T>(object value)
		{
			return Bind(typeof (T), value);
		}

		public ContainerConfigurationBuilder Bind(Type interfaceType, object value)
		{
			if (value != null && interfaceType.IsInstanceOfType(value) == false)
				throw new SimpleContainerException(string.Format("value {0} can't be casted to required type [{1}]",
					DumpValue(value),
					interfaceType.FormatName()));
			GetOrCreate<InterfaceConfiguration>(interfaceType).UseInstance(value);
			return this;
		}

		public ContainerConfigurationBuilder WithInstanceFilter<T>(Func<T, bool> filter)
		{
			GetOrCreate<ImplementationConfiguration>(typeof (T)).InstanceFilter = o => filter((T) o);
			return this;
		}

		public ContainerConfigurationBuilder Bind<T>(Func<FactoryContext, object> creator)
		{
			GetOrCreate<InterfaceConfiguration>(typeof (T)).Factory = creator;
			return this;
		}

		public ContainerConfigurationBuilder BindDependency<T>(string dependencyName, object value)
		{
			ConfigureDependency(typeof (T), dependencyName).UseValue(value);
			return this;
		}

		public ContainerConfigurationBuilder BindDependency(Type type, string dependencyName, object value)
		{
			ConfigureDependency(type, dependencyName).UseValue(value);
			return this;
		}

		public ContainerConfigurationBuilder BindDependency<T, TDependency>(TDependency value)
		{
			BindDependency<T, TDependency>((object) value);
			return this;
		}

		public ContainerConfigurationBuilder BindDependency<T, TDependency>(object value)
		{
			if (value != null && value is TDependency == false)
				throw new SimpleContainerException(
					string.Format("dependency {0} for service [{1}] can't be casted to required type [{2}]",
						DumpValue(value),
						typeof (T).FormatName(),
						typeof (TDependency).FormatName()));
			ConfigureDependency(typeof (T), typeof (TDependency)).UseValue(value);
			return this;
		}

		public ContainerConfigurationBuilder BindDependency<T, TDependency, TDependencyValue>()
			where TDependencyValue : TDependency
		{
			ConfigureDependency(typeof (T), typeof (TDependency)).ImplementationType = typeof (TDependencyValue);
			return this;
		}

		public ContainerConfigurationBuilder BindDependency(Type type, Type dependencyType, Func<IContainer, object> creator)
		{
			ConfigureDependency(type, dependencyType).Factory = creator;
			return this;
		}

		public void BindDependencyFactory<T>(string dependencyName, Func<IContainer, object> creator)
		{
			ConfigureDependency(typeof (T), dependencyName).Factory = creator;
		}

		public ContainerConfigurationBuilder BindDependencyImplementation<T, TDependencyValue>(string dependencyName)
		{
			ConfigureDependency(typeof (T), dependencyName).ImplementationType = typeof (TDependencyValue);
			return this;
		}

		public ContainerConfigurationBuilder BindDependencies<T>(object dependencies)
		{
			foreach (var property in dependencies.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
				BindDependency<T>(property.Name, property.GetValue(dependencies, null));
			return this;
		}

		public ContainerConfigurationBuilder BindDependencyValue(Type type, Type dependencyType, object value)
		{
			ConfigureDependency(type, dependencyType).UseValue(value);
			return this;
		}

		public ContainerConfigurationBuilder MakeStatic(Type type)
		{
			if (!isStaticConfiguration)
			{
				const string messageFormat = "can't make type [{0}] static using non static configurator";
				throw new SimpleContainerException(string.Format(messageFormat, type.FormatName()));
			}
			staticServices.Add(type);
			return this;
		}

		public ContractConfigurationBuilder Contract<T>()
			where T : RequireContractAttribute, new()
		{
			return Contract(new T().ContractName);
		}

		public ContractConfigurationBuilder Contract(string contract)
		{
			ContractConfigurationBuilder result;
			if (!contractConfigurators.TryGetValue(contract, out result))
				contractConfigurators.Add(contract, result = new ContractConfigurationBuilder(staticServices, isStaticConfiguration));
			return result;
		}

		public ContainerConfigurationBuilder UseAutosearch(Type interfaceType, bool useAutosearch)
		{
			GetOrCreate<InterfaceConfiguration>(interfaceType).UseAutosearch = useAutosearch;
			return this;
		}

		public ContainerConfigurationBuilder DontUse(Type pluggableType)
		{
			GetOrCreate<ImplementationConfiguration>(pluggableType).DontUseIt = true;
			return this;
		}

		public ContainerConfigurationBuilder DontUse<T>()
		{
			return DontUse(typeof (T));
		}

		public IContainerConfiguration Build()
		{
			return new ContainerConfiguration(configurations,
				contractConfigurators.ToDictionary(x => x.Key, x => x.Value.Build()));
		}

		private ImplentationDependencyConfiguration ConfigureDependency(Type implementationType, Type dependencyType)
		{
			return GetDependencyConfigurator(implementationType, dependencyType.FormatName() + " type");
		}

		private ImplentationDependencyConfiguration ConfigureDependency(Type implementationType, string dependencyName)
		{
			return GetDependencyConfigurator(implementationType, dependencyName + " name");
		}

		private ImplentationDependencyConfiguration GetDependencyConfigurator(Type pluggable, string key)
		{
			return GetOrCreate<ImplementationConfiguration>(pluggable).GetOrCreateByKey(key);
		}

		private T GetOrCreate<T>(Type type) where T : class, new()
		{
			object result;

			var isStatic = staticServices.Contains(type) || type.IsDefined<StaticAttribute>();
			if (isStatic && !isStaticConfiguration)
			{
				const string messageFormat = "can't configure static service [{0}] using non static configurator";
				throw new SimpleContainerException(string.Format(messageFormat, type.FormatName()));
			}
			if (!isStatic && isStaticConfiguration)
			{
				const string messageFormat = "can't configure non static service [{0}] using static configurator";
				throw new SimpleContainerException(string.Format(messageFormat, type.FormatName()));
			}
			if (!configurations.TryGetValue(type, out result))
				configurations.Add(type, result = new T());
			try
			{
				return (T) result;
			}
			catch (InvalidCastException e)
			{
				throw new InvalidOperationException(string.Format("type {0}, existent {1}, required {2}",
					type.FormatName(), result.GetType().FormatName(), typeof (T).FormatName()), e);
			}
		}

		public static string DumpValue(object value)
		{
			if (value == null)
				return "[<null>]";
			var type = value.GetType();
			return ReflectionHelpers.simpleTypes.Contains(type)
				? string.Format("[{0}] of type [{1}]", value, type.FormatName())
				: string.Format("of type [{0}]", type.FormatName());
		}
	}
}