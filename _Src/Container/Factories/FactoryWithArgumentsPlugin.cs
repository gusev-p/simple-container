using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class FactoryWithArgumentsPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(Implementation.SimpleContainer container, ContainerService containerService)
		{
			var funcType = containerService.Type;
			if (!funcType.IsGenericType)
				return false;
			if (funcType.GetGenericTypeDefinition() != typeof (Func<,>))
				return false;
			var typeArguments = funcType.GetGenericArguments();
			if (typeArguments[0] != typeof (object))
				return false;
			var type = typeArguments[1];
			var factory = FactoryCreator.CreateFactory(typeArguments[1], container, containerService);
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			return true;
		}
	}
}