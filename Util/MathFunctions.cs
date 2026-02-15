using System;
using System.Linq;
using System.Reflection;

using KRPC.MechJeb.ExtensionMethods;

namespace KRPC.MechJeb.Util {
	internal static class LaunchTiming {
		internal const string MechJebType = "MuMech.LaunchTiming";
		internal static readonly string[] MechJebTypes = {
			MechJebType
		};

		// Fields and methods
		private static MethodInfo timeToPhaseAngle;

		internal static void InitType(Type type) {
			timeToPhaseAngle = type.GetCheckedMethod("TimeToPhaseAngle");
		}

		public static double TimeToPhaseAngle(double launchPhaseAngle) {
			if(timeToPhaseAngle == null)
				ResolveTimeToPhaseAngle();
			return (double)timeToPhaseAngle.Invoke(null, new object[] { launchPhaseAngle, FlightGlobals.ActiveVessel.mainBody, MechJeb.vesselState.Longitude, MechJeb.TargetController.TargetOrbit.InternalOrbit });
		}

		private static void ResolveTimeToPhaseAngle() {
			foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				foreach(Type type in assembly.GetTypes()) {
					MethodInfo candidate = type.GetMethod("TimeToPhaseAngle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					if(candidate == null || candidate.GetParameters().Length != 4)
						continue;
					timeToPhaseAngle = candidate;
					Logger.Debug("Resolved TimeToPhaseAngle using " + type.FullName);
					return;
				}
			}
			throw new MJServiceException("TimeToPhaseAngle() method not found in loaded MechJeb assemblies.");
		}
	}

	internal static class MathFunctions {
		internal const string MechJebType = "MechJebLib.Maths.Functions";
		internal static readonly string[] MechJebTypes = {
			MechJebType
		};

		// Fields and methods
		private static MethodInfo timeToPlane;

		internal static void InitType(Type type) {
			timeToPlane = type.GetCheckedMethod("TimeToPlane");
		}

		public static Tuple<double, double> MinimumTimeToPlane(double lan, double inclination) {
			double normal = TimeToPlane(lan, inclination);
			double inverted = TimeToPlane(lan, -inclination);
			return normal < inverted ? Tuple.Create(normal, inclination) : Tuple.Create(inverted, -inclination);
		}

		public static double TimeToPlane(double lan, double inclination) {
			if(timeToPlane == null)
				ResolveTimeToPlane();
			return (double)timeToPlane.Invoke(null, new object[] { FlightGlobals.ActiveVessel.mainBody.rotationPeriod, MechJeb.vesselState.Latitude, MechJeb.vesselState.CelestialLongitude, lan, inclination });
		}

		private static void ResolveTimeToPlane() {
			foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				foreach(Type type in assembly.GetTypes().Where(t => t.FullName != null && t.FullName.Contains("MuMech"))) {
					MethodInfo candidate = type.GetMethod("TimeToPlane", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					if(candidate == null || candidate.GetParameters().Length != 5)
						continue;
					timeToPlane = candidate;
					Logger.Debug("Resolved TimeToPlane using " + type.FullName);
					return;
				}
			}
			throw new MJServiceException("TimeToPlane() method not found in loaded MechJeb assemblies.");
		}
	}
}
