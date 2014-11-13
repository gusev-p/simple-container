﻿using System;

namespace SimpleContainer.Hosting
{
	[AttributeUsage(AttributeTargets.Assembly)]
	public class ContainerReferenceAttribute : Attribute
	{
		public string AssemblyName { get; private set; }

		public ContainerReferenceAttribute(string assemblyName)
		{
			AssemblyName = assemblyName;
		}
	}
}