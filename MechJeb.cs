using System;
using System.Collections.Generic;
using System.Reflection;

using KRPC.MechJeb.ExtensionMethods;
using KRPC.Service.Attributes;

using UnityEngine;

namespace KRPC.MechJeb {
	/// <summary>
	/// This service provides functionality to interact with <a href="https://github.com/MuMech/MechJeb2">MechJeb 2</a>.
	/// </summary>
	[KRPCService(GameScene = Service.GameScene.Flight)]
	public static class MechJeb {
		internal const string MechJebType = "MuMech.MechJebCore";

		internal static List<string> errors = new List<string>();
		private static readonly HashSet<string> optionalClassNames = new HashSet<string> {
			"AscentGT",
			"LaunchTiming",
			"MathFunctions"
		};
		private static readonly HashSet<string> optionalModuleKeys = new HashSet<string> {
			"AscentGT",
			"AscentPVG"
		};

		private static Type type;
		private static FieldInfo vesselStateField;
		private static MethodInfo getComputerModule;

		internal static readonly VesselState vesselState = new VesselState();
		private static readonly Dictionary<string, Module> modules = new Dictionary<string, Module>();

		internal static bool InitTypes() {
			try {
				// Scan the project assembly for wrapper classes and preferred runtime aliases.
				Dictionary<Type, List<string>> preferredTypeNames = new Dictionary<Type, List<string>>();
				foreach(Type t in Assembly.GetExecutingAssembly().GetTypes()) {
					FieldInfo mechjebTypeField = t.GetField("MechJebType", BindingFlags.NonPublic | BindingFlags.Static);
					if(mechjebTypeField != null) {
						string mechjebType = (string)mechjebTypeField.GetValue(null);
						Logger.Debug("Found class " + t.Name + " wanting to use " + mechjebType);
						if(!preferredTypeNames.ContainsKey(t))
							preferredTypeNames.Add(t, new List<string>());
						if(!string.IsNullOrEmpty(mechjebType) && !preferredTypeNames[t].Contains(mechjebType))
							preferredTypeNames[t].Add(mechjebType);
					}

					FieldInfo mechjebTypesField = t.GetField("MechJebTypes", BindingFlags.NonPublic | BindingFlags.Static);
					if(mechjebTypesField != null) {
						string[] aliases = (string[])mechjebTypesField.GetValue(null);
						if(aliases == null)
							continue;
						foreach(string alias in aliases) {
							if(string.IsNullOrEmpty(alias))
								continue;
							Logger.Debug("Found class " + t.Name + " wanting to use " + alias);
							if(!preferredTypeNames.ContainsKey(t))
								preferredTypeNames.Add(t, new List<string>());
							if(!preferredTypeNames[t].Contains(alias))
								preferredTypeNames[t].Add(alias);
						}
					}
				}

				// Scan loaded assemblies once and index available runtime types.
				Dictionary<string, Type> runtimeTypes = new Dictionary<string, Type>();
				AssemblyLoader.loadedAssemblies.TypeOperation(mechjebType => {
					if(mechjebType != null && !string.IsNullOrEmpty(mechjebType.FullName) && !runtimeTypes.ContainsKey(mechjebType.FullName))
						runtimeTypes.Add(mechjebType.FullName, mechjebType);
				});

				// Initialize each wrapper exactly once using its highest-priority alias that exists at runtime.
				HashSet<Type> loadedInternalTypes = new HashSet<Type>();
				foreach(KeyValuePair<Type, List<string>> p in preferredTypeNames) {
					Type internalType = p.Key;
					Type runtimeType = null;
					string matchedAlias = null;
					foreach(string alias in p.Value) {
						if(runtimeTypes.TryGetValue(alias, out runtimeType)) {
							matchedAlias = alias;
							break;
						}
					}

					if(runtimeType != null) {
						try {
							Logger.Debug("Loading class " + internalType.Name + " using " + matchedAlias);
							internalType.GetMethod("InitType", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { runtimeType });
							loadedInternalTypes.Add(internalType);
						}
						catch(Exception ex) {
							string error = "Cannot load class " + internalType.Name;
							Logger.Severe(error, ex);
							errors.Add(error);
						}
					}
				}

				// Check if all classes have been initialized
				foreach(KeyValuePair<Type, List<string>> p in preferredTypeNames) {
					if(loadedInternalTypes.Contains(p.Key))
						continue;
					if(optionalClassNames.Contains(p.Key.Name)) {
						Logger.Debug("Optional class " + p.Key.Name + " not found for " + string.Join(", ", p.Value.ToArray()));
						continue;
					}
					string error = "Cannot load class " + p.Key.Name;
					Logger.Severe(error + " because none of these aliases were found: " + string.Join(", ", p.Value.ToArray()));
					errors.Add(error);
				}
			}
			catch(Exception ex) {
				Logger.Severe("InitTypes() failed", ex);
				errors.Clear();
				errors.Add("kRPC.MechJeb failed to initialize: " + ex.Message);
				type = null;
			}

			return type != null;
		}

		internal static void InitType(Type t) {
			type = t;
			vesselStateField = t.GetCheckedField("vesselState");
			getComputerModule = t.GetCheckedMethod("GetComputerModule", new Type[] { typeof(string) });

			// MechJeb found, create module instances
			modules.Add("AirplaneAutopilot", new AirplaneAutopilot());
			modules.Add("AscentAutopilot", new AscentAutopilot());
			modules.Add("DockingAutopilot", new DockingAutopilot());
			modules.Add("LandingAutopilot", new LandingAutopilot());
			modules.Add("RendezvousAutopilot", new RendezvousAutopilot());

			modules.Add("ManeuverPlanner", new ManeuverPlanner());
			modules.Add("SmartASS", new SmartASS());
			modules.Add("SmartRCS", new SmartRCS());
			modules.Add("Translatron", new Translatron());

			modules.Add("DeployableAntennaController", new DeployableController());
			modules.Add("NodeExecutor", new NodeExecutor());
			modules.Add("RCSController", new RCSController());
			modules.Add("StagingController", new StagingController());
			modules.Add("SolarPanelController", new DeployableController());
			modules.Add("TargetController", new TargetController());
			modules.Add("ThrustController", new ThrustController());
			modules.Add("WarpController", new WarpController());
		}

		internal static bool InitInstance() {
			//assume all MechJeb types are loaded

			APIReady = false;
			try {
				MasterInstance = FlightGlobals.ActiveVessel.GetMasterMechJeb();
				if(MasterInstance == null)
					return false;

				vesselState.InitInstance(vesselStateField.GetInstanceValue(MasterInstance));

				// Set module instances to MechJeb objects
				foreach(KeyValuePair<string, Module> p in modules) {
					string error = "Cannot initialize class " + p.Value.GetType().Name + " with " + p.Key;
					try {
						bool optionalModule = optionalModuleKeys.Contains(p.Key);
						object moduleInstance = GetComputerModule(p.Key, !optionalModule);
						if(moduleInstance != null)
							p.Value.InitInstance(moduleInstance);
						else if(!optionalModule)
							errors.Add(error);
						else
							Logger.Debug("Optional MechJeb module " + p.Key + " is unavailable in this build.");
					}
					catch(Exception ex) {
						Logger.Severe(error, ex);
						errors.Add(error);
					}
				}

				APIReady = true;
			}
			catch(Exception ex) {
				Logger.Severe("InitInstance() failed", ex);
				errors.Clear();
				errors.Add("kRPC.MechJeb failed to initialize: " + ex.Message);
			}

			return APIReady;
		}

		internal static void ShowErrors() {
			if(errors.Count != 0) {
				PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "MechJebChecker", "kRPC.MechJeb may not work properly", string.Join("\n", errors.ToArray()), "OK", false, HighLogic.UISkin);
				errors.Clear();
			}
		}

		internal static object GetComputerModule(string moduleType) {
			return GetComputerModule(moduleType, true);
		}

		internal static object GetComputerModule(string moduleType, bool logMissing) {
			string[] moduleTypeCandidates = GetModuleTypeCandidates(moduleType);
			object module = null;
			foreach(string candidate in moduleTypeCandidates) {
				module = getComputerModule.Invoke(MasterInstance, new object[] { "MechJebModule" + candidate });
				if(module != null)
					return module;
			}

			if(logMissing && !optionalModuleKeys.Contains(moduleType))
				Logger.Severe("MechJeb module " + moduleType + " not found");

			return module;
		}

		private static string[] GetModuleTypeCandidates(string moduleType) {
			switch(moduleType) {
			case "AscentAutopilot":
				return new[] { "AscentAutopilot", "AscentBaseAutopilot", "Ascent", "AscentGuidance", "AscentSettings" };
			case "AscentGuidance":
				return new[] { "AscentGuidance", "AscentSettings" };
			case "AscentClassic":
				return new[] { "AscentClassic", "AscentClassicAutopilot" };
			case "AscentGT":
				return new[] { "AscentGT" };
			case "AscentPVG":
				return new[] { "AscentPVG", "AscentPVGAutopilot" };
			default:
				return new[] { moduleType };
			}
		}

		internal static PartModule MasterInstance { get; private set; }

		public static bool TypesLoaded => type != null;

		/// <summary>
		/// A value indicating whether the service is available.
		/// </summary>
		[KRPCProperty]
		public static bool APIReady { get; private set; }

		// AUTOPILOTS

		[KRPCProperty]
		public static AirplaneAutopilot AirplaneAutopilot => (AirplaneAutopilot)modules["AirplaneAutopilot"];

		[KRPCProperty]
		public static AscentAutopilot AscentAutopilot => (AscentAutopilot)modules["AscentAutopilot"];

		[KRPCProperty]
		public static DockingAutopilot DockingAutopilot => (DockingAutopilot)modules["DockingAutopilot"];

		[KRPCProperty]
		public static LandingAutopilot LandingAutopilot => (LandingAutopilot)modules["LandingAutopilot"];

		[KRPCProperty]
		public static RendezvousAutopilot RendezvousAutopilot => (RendezvousAutopilot)modules["RendezvousAutopilot"];

		// WINDOWS

		[KRPCProperty]
		public static ManeuverPlanner ManeuverPlanner => (ManeuverPlanner)modules["ManeuverPlanner"];

		[KRPCProperty]
		public static SmartASS SmartASS => (SmartASS)modules["SmartASS"];

		[KRPCProperty]
		public static SmartRCS SmartRCS => (SmartRCS)modules["SmartRCS"];

		[KRPCProperty]
		public static Translatron Translatron => (Translatron)modules["Translatron"];

		// CONTROLLERS

		[KRPCProperty]
		public static DeployableController AntennaController => (DeployableController)modules["DeployableAntennaController"];

		[KRPCProperty]
		public static NodeExecutor NodeExecutor => (NodeExecutor)modules["NodeExecutor"];

		[KRPCProperty]
		public static RCSController RCSController => (RCSController)modules["RCSController"];

		[KRPCProperty]
		public static StagingController StagingController => (StagingController)modules["StagingController"];

		[KRPCProperty]
		public static DeployableController SolarPanelController => (DeployableController)modules["SolarPanelController"];

		[KRPCProperty]
		public static TargetController TargetController => (TargetController)modules["TargetController"];

		[KRPCProperty]
		public static ThrustController ThrustController => (ThrustController)modules["ThrustController"];

		[KRPCProperty]
		public static WarpController WarpController => (WarpController)modules["WarpController"];
	}

	/// <summary>
	/// General exception for errors in the service.
	/// </summary>
	[KRPCException(Service = "MechJeb")]
	public class MJServiceException : Exception {
		public MJServiceException(string message) : base(message) { }
	}
}
