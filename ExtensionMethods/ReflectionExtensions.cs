using System;
using System.Reflection;

namespace KRPC.MechJeb.ExtensionMethods {
	public static class ReflectionExtensions {
		private const BindingFlags AnyMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

		public static T CreateInstance<T>(this Type type, object[] args) {
			try {
				Type[] types = Type.EmptyTypes;
				if(args != null) {
					types = new Type[args.Length];
					for(int i = 0; i < args.Length; i++)
						types[i] = args[i].GetType();
				}

				return (T)type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, types, null).Invoke(args);
			}
			catch(Exception ex) {
				Logger.Severe("Coudn't create an instance of " + type, ex);
				throw new MJServiceException(ex.ToString());
			}
		}

		public static FieldInfo GetCheckedField(this Type type, string name) {
			return type.GetField(name, AnyMemberFlags) ??
				type.GetField(name, AnyMemberFlags | BindingFlags.IgnoreCase)
					.CheckIfExists(type, name);
		}

		public static FieldInfo GetCheckedField(this Type type, string name, BindingFlags bindingAttr) {
			return type.GetField(name, bindingAttr) ??
				type.GetField(name, bindingAttr | BindingFlags.IgnoreCase)
					.CheckIfExists(type, name);
		}

		public static PropertyInfo GetCheckedProperty(this Type type, string name) {
			return type.GetProperty(name, AnyMemberFlags) ??
				type.GetProperty(name, AnyMemberFlags | BindingFlags.IgnoreCase)
					.CheckIfExists(type, name);
		}

		public static MethodInfo GetCheckedMethod(this Type type, string name) {
			return type.GetMethod(name, AnyMemberFlags) ??
				type.GetMethod(name, AnyMemberFlags | BindingFlags.IgnoreCase)
					.CheckIfExists(type, name + "()");
		}

		public static MethodInfo GetCheckedMethod(this Type type, string name, Type[] types) {
			return type.GetMethod(name, AnyMemberFlags, null, types, null) ??
				type.GetMethod(name, AnyMemberFlags | BindingFlags.IgnoreCase, null, types, null)
					.CheckIfExists(type, name + "()");
		}

		public static MethodInfo GetCheckedMethod(this Type type, string name, BindingFlags bindingAttr) {
			return type.GetMethod(name, bindingAttr) ??
				type.GetMethod(name, bindingAttr | BindingFlags.IgnoreCase)
					.CheckIfExists(type, name + "()");
		}

		private static T CheckIfExists<T>(this T obj, Type type, string name) {
			if(obj == null) {
				string error = type + "." + name + " not found";
				Logger.Severe(error);
				MechJeb.errors.Add(error);
			}
			else
				Logger.Debug(type + "." + name + " found");

			return obj;
		}

		public static object GetInstanceValue(this FieldInfo field, object instance) {
			return field != null && instance != null ? field.GetValue(instance) : null;
		}
	}
}
