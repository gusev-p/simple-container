using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	public class StaticContainer : SimpleContainer, IStaticContainer
	{
		private readonly Func<AssemblyName, bool> assemblyFilter;
		private IEnumerable<Type> staticServices;

		public StaticContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			Func<AssemblyName, bool> assemblyFilter) : base(configuration, inheritors, null)
		{
			this.assemblyFilter = assemblyFilter;
		}

		public IContainer CreateLocalContainer(Assembly primaryAssembly, Action<ContainerConfigurationBuilder> configure)
		{
			var targetAssemblies = Utils.Closure(primaryAssembly, ReferencedAssemblies).ToSet();
			var restrictedHierarchy = new AssembliesRestrictedInheritanceHierarchy(targetAssemblies, inheritors);
			var configurationBuilder = new ContainerConfigurationBuilder();
			using (var configurationContainer = new SimpleContainer(configuration, restrictedHierarchy, this))
				foreach (var configurator in configurationContainer.GetAll<IServiceConfiguratorInvoker>())
					configurator.Configure(configurationBuilder);
			if (configure != null)
				configure(configurationBuilder);
			var newStaticServices = configurationBuilder.GetStaticServices().ToArray();
			if (staticServices == null)
				staticServices = newStaticServices;
			else
			{
				var diff = staticServices.Except(newStaticServices).ToArray();
				if (diff.Any())
					throw new SimpleContainerException(string.Format("inconsistent static configuration, [{0}] were static, now local",
						diff.Select(x => x.FormatName()).JoinStrings(",")));
				diff = newStaticServices.Except(staticServices).ToArray();
				if (diff.Any())
					throw new SimpleContainerException(string.Format("inconsistent static configuration, [{0}] were local, now static",
						diff.Select(x => x.FormatName()).JoinStrings(",")));
			}
			var containerConfiguration = new MergedConfiguration(configuration, configurationBuilder.Build());
			return new SimpleContainer(containerConfiguration, restrictedHierarchy, this);
		}

		public interface IServiceConfiguratorInvoker
		{
			void Configure(ContainerConfigurationBuilder builder);
		}

		public class ServiceConfiguratorInvoker<T> : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IServiceConfigurator<T>> configurators;

			public ServiceConfiguratorInvoker(IEnumerable<IServiceConfigurator<T>> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ContainerConfigurationBuilder builder)
			{
				var serviceConfigurationBuilder = new ServiceConfigurationBuilder<T>(builder);
				foreach (var configurator in configurators)
					configurator.Configure(serviceConfigurationBuilder);
			}
		}

		private IEnumerable<Assembly> ReferencedAssemblies(Assembly assembly)
		{
			var referencedByAttribute = assembly.GetCustomAttributes<ContainerReferenceAttribute>()
				.Select(x => new AssemblyName(x.AssemblyName));
			return assembly.GetReferencedAssemblies()
				.Concat(referencedByAttribute)
				.Where(assemblyFilter)
				.Select(Assembly.Load);
		}
	}
}