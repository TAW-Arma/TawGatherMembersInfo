﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neitri
{
	public interface IDependencyManager
	{
		/// <summary>
		/// Get instance for type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		object Resolve(Type type);
		bool CanResolve(Type type);
		IDependencyManager Register(object instance);
		IDependencyManager RegisterType(Type type);
		/// <summary>
		/// Finds instances for members that are null and marked with DependencyAttribute.
		/// </summary>
		/// <param name="instance"></param>
		IDependencyManager BuildUp(object instance);
		/// <summary>
		/// Creates new instance and then resolves it's dependencies.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		object Create(Type type, params object[] args);
		object CreateAndRegister(Type type, params object[] args);
	}

	/// <summary>
	/// Mark field or property to be automatically resolved during either object creation (<see cref="IDependencyManager.Create(Type, object[])"/>) or build up (<see cref="IDependencyManager.BuildUp(object)"/>).
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class DependencyAttribute : Attribute
	{
		/// <summary>
		/// Automatically register type if type is not registered.
		/// </summary>
		public bool RegisterType { get; set; }
	}


	public static class IDependencyManagerExtensions
	{
		public static IDependencyManager Register(this IDependencyManager me, params object[] instances)
		{
			foreach (var instance in instances)
			{
				me.Register(instance);
			}
			return me;
		}

		public static IDependencyManager Register<TManager>(this IDependencyManager me) where TManager : new()
		{
			return me.RegisterType(typeof(TManager));
		}
		/// <summary>
		/// Creates new instance and then resolves it's dependencies.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static T Create<T>(this IDependencyManager me, params object[] args)
		{
			return (T)me.Create(typeof(T), args);
		}
		public static T CreateAndRegister<T>(this IDependencyManager me, params object[] args)
		{
			return (T)me.CreateAndRegister(typeof(T), args);
		}

	}
}
