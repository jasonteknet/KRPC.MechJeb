using System;
using System.Reflection;

using KRPC.MechJeb.ExtensionMethods;
using KRPC.MechJeb.Util;
using KRPC.Service.Attributes;

namespace KRPC.MechJeb {
	using BoolReason = Tuple<bool, string>;
	using BoolValueReason = Tuple<bool, bool, string>;
	using DoubleValueReason = Tuple<bool, double, string>;

	internal static class AscentGuidance {
		internal new const string MechJebType = "MuMech.MechJebModuleAscentGuidance";
		internal static readonly string[] MechJebTypes = {
			MechJebType,
			"MuMech.MechJebModuleAscentSettings"
		};

		// Fields and methods
		internal static FieldInfo desiredInclination;
		internal static FieldInfo launchingToPlane;
		internal static FieldInfo launchingToRendezvous;

		internal static void InitType(Type type) {
			desiredInclination = type.GetCheckedField("desiredInclination");
			launchingToPlane = type.GetCheckedField("launchingToPlane");
			launchingToRendezvous = type.GetCheckedField("launchingToRendezvous");
		}
	}

	/// <summary>
	/// This module controls the Ascent Guidance in MechJeb 2.
	/// </summary>
	/// <remarks>
	/// See <a href="https://github.com/MuMech/MechJeb2/wiki/Ascent-Guidance#initial-pitch-over-issues">MechJeb2 wiki</a> for more guidance on how to optimally set up this autopilot.
	/// </remarks>
	[KRPCClass(Service = "MechJeb")]
	public class AscentAutopilot : KRPCComputerModule {
		internal new const string MechJebType = "MuMech.MechJebModuleAscentAutopilot";
		internal static readonly string[] MechJebTypes = {
			MechJebType,
			"MuMech.MechJebModuleAscentBaseAutopilot"
		};

		// Fields and methods
		private static FieldInfo status;
		private static PropertyInfo ascentPathIdx;
		private static FieldInfo ascentTypeInteger;
		private static FieldInfo desiredOrbitAltitudeField;
		private static FieldInfo autoThrottle;
		private static FieldInfo correctiveSteering;
		private static FieldInfo correctiveSteeringGainField;
		private static FieldInfo forceRoll;
		private static FieldInfo verticalRollField;
		private static FieldInfo turnRollField;
		private static FieldInfo autodeploySolarPanels;
		private static FieldInfo autoDeployAntennas;
		private static FieldInfo skipCircularization;
		private static PropertyInfo autostage;
		private static FieldInfo autostageField;
		private static FieldInfo limitAoA;
		private static FieldInfo maxAoAField;
		private static FieldInfo aoALimitFadeoutPressureField;
		private static FieldInfo launchPhaseAngleField;
		private static FieldInfo launchLANDifferenceField;
		private static FieldInfo warpCountDownField;

		private static FieldInfo timedLaunch;
		private static MethodInfo startCountdown;

		// Instance objects
		private object guiInstance;

		private object desiredOrbitAltitude;
		private object correctiveSteeringGain;
		private object verticalRoll;
		private object turnRoll;
		private object maxAoA;
		private object aoALimitFadeoutPressure;
		private object launchPhaseAngle;
		private object launchLANDifference;
		private object warpCountDown;

		internal static new void InitType(Type type) {
			status = type.GetCheckedField("status");
			ascentPathIdx = type.GetCheckedProperty("ascentPathIdxPublic");
			ascentTypeInteger = type.GetCheckedField("AscentTypeInteger");
			desiredOrbitAltitudeField = type.GetCheckedField("desiredOrbitAltitude");
			autoThrottle = type.GetCheckedField("autoThrottle");
			correctiveSteering = type.GetCheckedField("correctiveSteering");
			correctiveSteeringGainField = type.GetCheckedField("correctiveSteeringGain");
			forceRoll = type.GetCheckedField("forceRoll");
			verticalRollField = type.GetCheckedField("verticalRoll");
			turnRollField = type.GetCheckedField("turnRoll");
			autodeploySolarPanels = type.GetCheckedField("autodeploySolarPanels");
			autoDeployAntennas = type.GetCheckedField("autoDeployAntennas");
			skipCircularization = type.GetCheckedField("skipCircularization");
			autostage = type.GetCheckedProperty("autostage");
			autostageField = type.GetCheckedField("_autostage");
			limitAoA = type.GetCheckedField("limitAoA");
			maxAoAField = type.GetCheckedField("maxAoA");
			aoALimitFadeoutPressureField = type.GetCheckedField("aoALimitFadeoutPressure");
			launchPhaseAngleField = type.GetCheckedField("launchPhaseAngle");
			launchLANDifferenceField = type.GetCheckedField("launchLANDifference");
			warpCountDownField = type.GetCheckedField("warpCountDown");

			timedLaunch = type.GetCheckedField("timedLaunch");
			startCountdown = type.GetCheckedMethod("StartCountdown");
		}

		protected internal override void InitInstance(object instance) {
			object resolvedInstance = instance;
			if(instance != null) {
				Type runtimeType = instance.GetType();
				string runtimeTypeName = runtimeType.FullName ?? runtimeType.Name;
				if(runtimeTypeName.Contains("AscentGuidance") || runtimeTypeName.Contains("AscentSettings")) {
					object autopilotInstance = ResolveAutopilotInstance(instance, runtimeType);
					if(autopilotInstance != null) {
						Logger.Info(string.Format(
							"AscentAutopilot.InitInstance resolved autopilot from guidance/settings ({0} -> {1})",
							runtimeTypeName,
							autopilotInstance.GetType().FullName
						));
						resolvedInstance = autopilotInstance;
					}
					else {
						Logger.Warning("AscentAutopilot.InitInstance could not resolve autopilot from " + runtimeTypeName);
					}
				}
			}

			base.InitInstance(resolvedInstance);
			this.guiInstance = MechJeb.GetComputerModule("AscentGuidance");

			this.desiredOrbitAltitude = this.ResolveSettingObject(desiredOrbitAltitudeField.GetInstanceValue(this.instance), "desiredOrbitAltitude", "DesiredOrbitAltitude");
			this.correctiveSteeringGain = this.ResolveSettingObject(correctiveSteeringGainField.GetInstanceValue(this.instance), "correctiveSteeringGain", "CorrectiveSteeringGain");
			this.verticalRoll = this.ResolveSettingObject(verticalRollField.GetInstanceValue(this.instance), "verticalRoll", "VerticalRoll");
			this.turnRoll = this.ResolveSettingObject(turnRollField.GetInstanceValue(this.instance), "turnRoll", "TurnRoll");
			this.maxAoA = this.ResolveSettingObject(maxAoAField.GetInstanceValue(this.instance), "maxAoA", "MaxAoA");
			this.aoALimitFadeoutPressure = this.ResolveSettingObject(aoALimitFadeoutPressureField.GetInstanceValue(this.instance), "aoALimitFadeoutPressure", "AOALimitFadeoutPressure");
			this.launchPhaseAngle = this.ResolveSettingObject(launchPhaseAngleField.GetInstanceValue(this.instance), "launchPhaseAngle", "LaunchPhaseAngle");
			this.launchLANDifference = this.ResolveSettingObject(launchLANDifferenceField.GetInstanceValue(this.instance), "launchLANDifference", "LaunchLANDifference");
			this.warpCountDown = this.ResolveSettingObject(warpCountDownField.GetInstanceValue(this.instance), "warpCountDown", "WarpCountDown");

			this.AscentPathClassic.InitInstance(MechJeb.GetComputerModule("AscentClassic"));
			this.AscentPathGT.InitInstance(MechJeb.GetComputerModule("AscentGT", false));
			this.AscentPathPVG.InitInstance(MechJeb.GetComputerModule("AscentPVG", false));

			// Retrieve the current path index set in mechjeb and enable the path representing that index.
			// It fixes the issue with AscentAutopilot reporting empty status due to a disabled path.
			if(this.instance != null)
				this.AscentPathIndex = this.AscentPathIndex;
		}

		private object ResolveSettingObject(object currentValue, params string[] fallbackFieldNames) {
			if(currentValue != null)
				return currentValue;
			return GetFieldValue(this.guiInstance, fallbackFieldNames);
		}

		private static object GetFieldValue(object target, params string[] fieldNames) {
			if(target == null || fieldNames == null)
				return null;

			Type type = target.GetType();
			foreach(string name in fieldNames) {
				if(string.IsNullOrEmpty(name))
					continue;
				FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if(field != null) {
					object value = field.GetValue(target);
					if(value != null)
						return value;
				}
			}

			return null;
		}

		private bool GetBoolSetting(FieldInfo field, params string[] fallbackFieldNames) {
			object value = field.GetInstanceValue(this.instance);
			if(value == null)
				value = GetFieldValue(this.guiInstance, fallbackFieldNames);
			if(value == null)
				throw new MJServiceException("Boolean setting is unavailable for this MechJeb build.");
			return (bool)value;
		}

		private void SetBoolSetting(bool value, FieldInfo field, params string[] fallbackFieldNames) {
			if(field != null && this.instance != null) {
				field.SetValue(this.instance, value);
				return;
			}

			if(this.guiInstance != null && fallbackFieldNames != null) {
				Type type = this.guiInstance.GetType();
				foreach(string name in fallbackFieldNames) {
					if(string.IsNullOrEmpty(name))
						continue;
					FieldInfo fallbackField = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if(fallbackField == null)
						continue;
					fallbackField.SetValue(this.guiInstance, value);
					return;
				}
			}

			throw new MJServiceException("Boolean setting is unavailable for this MechJeb build.");
		}

		private static object ResolveAutopilotInstance(object moduleInstance, Type runtimeType) {
			PropertyInfo autopilotProperty = runtimeType.GetProperty("autopilot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?? runtimeType.GetProperty("Autopilot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?? runtimeType.GetProperty("AscentAutopilot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object autopilotInstance = autopilotProperty?.GetValue(moduleInstance, null);
			if(autopilotInstance != null)
				return autopilotInstance;

			FieldInfo autopilotField = runtimeType.GetField("autopilot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?? runtimeType.GetField("Autopilot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?? runtimeType.GetField("AscentAutopilot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			autopilotInstance = autopilotField?.GetValue(moduleInstance);
			if(autopilotInstance != null)
				return autopilotInstance;

			MethodInfo autopilotMethod = runtimeType.GetMethod("get_AscentAutopilot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			if(autopilotMethod != null) {
				autopilotInstance = autopilotMethod.Invoke(moduleInstance, null);
				if(autopilotInstance != null)
					return autopilotInstance;
			}

			MethodInfo getAscentModuleMethod = runtimeType.GetMethod("GetAscentModule", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			PropertyInfo ascentTypeProperty = runtimeType.GetProperty("AscentType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if(getAscentModuleMethod != null && ascentTypeProperty != null) {
				object ascentTypeValue = ascentTypeProperty.GetValue(moduleInstance, null);
				if(ascentTypeValue != null) {
					autopilotInstance = getAscentModuleMethod.Invoke(moduleInstance, new[] { ascentTypeValue });
					if(autopilotInstance != null)
						return autopilotInstance;
				}
			}

			object coreInstance = null;
			PropertyInfo coreProperty = runtimeType.GetProperty("core", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?? runtimeType.GetProperty("Core", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			coreInstance = coreProperty?.GetValue(moduleInstance, null);
			if(coreInstance == null) {
				FieldInfo coreField = runtimeType.GetField("core", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					?? runtimeType.GetField("Core", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				coreInstance = coreField?.GetValue(moduleInstance);
			}

			if(coreInstance != null) {
				MethodInfo getComputerModuleMethod = coreInstance.GetType().GetMethod("GetComputerModule", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
				if(getComputerModuleMethod != null) {
					string[] candidates = {
						"MechJebModuleAscentAutopilot",
						"MechJebModuleAscentBaseAutopilot",
						"MechJebModuleAscent"
					};
					foreach(string candidate in candidates) {
						object result = getComputerModuleMethod.Invoke(coreInstance, new object[] { candidate });
						if(result != null)
							return result;
					}
				}
			}

			return null;
		}

		public AscentAutopilot() {
			this.AscentPathClassic = new AscentClassic();
			this.AscentPathGT = new AscentGT();
			this.AscentPathPVG = new AscentPVG();
		}

		/// <summary>
		/// Engage/disengage ascent autopilot using the same user-pool object as the Ascent Guidance UI.
		/// </summary>
		[KRPCProperty]
		public override bool Enabled {
			get => base.Enabled;
			set {
				// MechJeb's Ascent Guidance window toggles autopilot via autopilot.users.Add/Remove(thisGuidanceModule).
				// Use that same guidance-module user when available so remote and UI engagement stay consistent.
				object user = this.guiInstance ?? this.instance;
				Logger.Info(string.Format(
					"Ascent.Enabled <- {0} (user={1}, autopilot={2}, guidance={3})",
					value,
					user?.GetType().FullName ?? "null",
					this.instance?.GetType().FullName ?? "null",
					this.guiInstance?.GetType().FullName ?? "null"
				));
				this.SetEnabledWithUser(user, value);
			}
		}

		/// <summary>
		/// The autopilot status; it depends on the selected ascent path.
		/// </summary>
		[KRPCProperty]
		public string Status {
			get {
				object value = status.GetValue(this.instance);
				return value?.ToString() ?? "";
			}
		}

		/// <summary>
		/// Diagnostic helper returning the runtime type names bound by this wrapper.
		/// </summary>
		[KRPCMethod]
		public string DebugBindingSummary() {
			string instanceType = this.instance?.GetType().FullName ?? "null";
			string guidanceType = this.guiInstance?.GetType().FullName ?? "null";
			string userPoolType = this.users?.GetType().FullName ?? "null";
			string guidanceUserPoolType = "null";
			string guidanceUserPoolReadError = "";
			try {
				if(this.guiInstance != null) {
					FieldInfo usersField = this.guiInstance.GetType().GetField("users", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					object guidanceUsers = usersField?.GetValue(this.guiInstance);
					guidanceUserPoolType = guidanceUsers?.GetType().FullName ?? "null";
				}
			}
			catch(Exception ex) {
				guidanceUserPoolReadError = ex.Message;
			}
			string launchMode;
			try {
				launchMode = this.LaunchMode.ToString();
			}
			catch(Exception ex) {
				launchMode = "ERR:" + ex.Message;
			}

			bool? timedLaunchValue = null;
			string timedLaunchError = "";
			try {
				if(timedLaunch != null && this.instance != null)
					timedLaunchValue = (bool)timedLaunch.GetValue(this.instance);
			}
			catch(Exception ex) {
				timedLaunchError = ex.Message;
			}

			bool? enabledValue = null;
			string enabledError = "";
			try {
				enabledValue = this.Enabled;
			}
			catch(Exception ex) {
				enabledError = ex.Message;
			}

			return string.Format(
				"instance={0};guidance={1};user_pool={2};guidance_user_pool={3};guidance_user_pool_error={4};enabled={5};launch_mode={6};timed_launch={7};timed_launch_error={8};enabled_error={9}",
				instanceType,
				guidanceType,
				userPoolType,
				guidanceUserPoolType,
				guidanceUserPoolReadError ?? "",
				enabledValue.HasValue ? enabledValue.Value.ToString() : "null",
				launchMode,
				timedLaunchValue.HasValue ? timedLaunchValue.Value.ToString() : "null",
				timedLaunchError ?? "",
				enabledError ?? ""
			);
		}

		/// <summary>
		/// The selected ascent path.
		/// 
		/// 0 = <see cref="AscentClassic" /> (Classic Ascent Profile)
		/// 
		/// 1 = <see cref="AscentGT" /> (Stock-style GravityTurn)
		/// 
		/// 2 = <see cref="AscentPVG" /> (Primer Vector Guidance (RSS/RO))
		/// </summary>
		[KRPCProperty]
		public int AscentPathIndex {
			get {
				if(ascentPathIdx != null)
					return (int)ascentPathIdx.GetValue(this.instance, null);
				if(ascentTypeInteger != null)
					return (int)ascentTypeInteger.GetValue(this.instance);
				return 0;
			}
			set {
				if(value < 0 || value > 2)
					return;

				if(ascentPathIdx != null)
					ascentPathIdx.SetValue(this.instance, value, null);
				else if(ascentTypeInteger != null)
					ascentTypeInteger.SetValue(this.instance, value);
			}
		}

		/// <summary>
		/// Get Classic Ascent Profile settings.
		/// </summary>
		[KRPCProperty]
		public AscentClassic AscentPathClassic { get; }

		/// <summary>
		/// Get Stock-style GravityTurn profile settings.
		/// </summary>
		[KRPCProperty]
		public AscentGT AscentPathGT { get; }

		/// <summary>
		/// Get Powered Explicit Guidance (RSS/RO) profile settings.
		/// </summary>
		[KRPCProperty]
		public AscentPVG AscentPathPVG { get; }

		/// <summary>
		/// The desired altitude in kilometres for the final circular orbit.
		/// </summary>
		[KRPCProperty]
		public double DesiredOrbitAltitude {
			get => EditableDouble.Get(this.desiredOrbitAltitude);
			set => EditableDouble.Set(this.desiredOrbitAltitude, value);
		}

		private static string NormalizePropertyName(string propertyName) {
			if(propertyName == null)
				return "";
			return propertyName.Replace("_", "").Replace("-", "").Trim().ToLowerInvariant();
		}

		private static string BuildUnavailableReason(string propertyName, string detail) {
			return propertyName + " is unavailable for this MechJeb build. " + detail;
		}

		private bool TryGetBoolField(FieldInfo field, string propertyName, out bool value, out string reason) {
			value = false;
			if(field == null) {
				reason = BuildUnavailableReason(propertyName, "Field was not found.");
				return false;
			}
			if(this.instance == null) {
				reason = BuildUnavailableReason(propertyName, "Ascent autopilot instance is not initialized.");
				return false;
			}

			try {
				value = (bool)field.GetValue(this.instance);
				reason = null;
				return true;
			}
			catch(Exception ex) {
				reason = BuildUnavailableReason(propertyName, ex.Message);
				return false;
			}
		}

		private bool TrySetBoolField(FieldInfo field, string propertyName, bool value, out string reason) {
			if(field == null) {
				reason = BuildUnavailableReason(propertyName, "Field was not found.");
				return false;
			}
			if(this.instance == null) {
				reason = BuildUnavailableReason(propertyName, "Ascent autopilot instance is not initialized.");
				return false;
			}

			try {
				field.SetValue(this.instance, value);
				reason = null;
				return true;
			}
			catch(Exception ex) {
				reason = BuildUnavailableReason(propertyName, ex.Message);
				return false;
			}
		}

		private bool TryGetEditableDouble(object editable, string propertyName, out double value, out string reason) {
			value = 0.0;
			if(editable == null) {
				reason = BuildUnavailableReason(propertyName, "Editable value wrapper is not initialized.");
				return false;
			}

			try {
				value = EditableDouble.Get(editable);
				reason = null;
				return true;
			}
			catch(Exception ex) {
				reason = BuildUnavailableReason(propertyName, ex.Message);
				return false;
			}
		}

		private bool TrySetEditableDouble(object editable, string propertyName, double value, out string reason) {
			if(editable == null) {
				reason = BuildUnavailableReason(propertyName, "Editable value wrapper is not initialized.");
				return false;
			}

			try {
				EditableDouble.Set(editable, value);
				reason = null;
				return true;
			}
			catch(Exception ex) {
				reason = BuildUnavailableReason(propertyName, ex.Message);
				return false;
			}
		}

		private bool TryGetPropertyAvailability(string propertyName, out string reason) {
			reason = null;
			switch(NormalizePropertyName(propertyName)) {
			case "desiredorbitaltitude":
				double desiredOrbitAltitudeValue;
				return this.TryGetEditableDouble(this.desiredOrbitAltitude, "DesiredOrbitAltitude", out desiredOrbitAltitudeValue, out reason);
			case "limitaoa":
				bool limitAoAValue;
				return this.TryGetBoolField(limitAoA, "LimitAoA", out limitAoAValue, out reason);
			case "maxaoa":
				double maxAoAValue;
				return this.TryGetEditableDouble(this.maxAoA, "MaxAoA", out maxAoAValue, out reason);
			case "correctivesteering":
				bool correctiveSteeringValue;
				return this.TryGetBoolField(correctiveSteering, "CorrectiveSteering", out correctiveSteeringValue, out reason);
			case "correctivesteeringgain":
				double correctiveSteeringGainValue;
				return this.TryGetEditableDouble(this.correctiveSteeringGain, "CorrectiveSteeringGain", out correctiveSteeringGainValue, out reason);
			case "forceroll":
				bool forceRollValue;
				return this.TryGetBoolField(forceRoll, "ForceRoll", out forceRollValue, out reason);
			case "skipcircularization":
				bool skipCircularizationValue;
				return this.TryGetBoolField(skipCircularization, "SkipCircularization", out skipCircularizationValue, out reason);
			default:
				reason = BuildUnavailableReason(propertyName, "Unknown property name.");
				return false;
			}
		}

		/// <summary>
		/// Checks whether an ascent property is currently available in this runtime/build combination.
		/// </summary>
		[KRPCMethod]
		public bool IsPropertyAvailable(string propertyName) {
			string reason;
			return this.TryGetPropertyAvailability(propertyName, out reason);
		}

		/// <summary>
		/// Returns an unavailability reason for a requested ascent property, or empty string if it is available.
		/// </summary>
		[KRPCMethod]
		public string GetUnavailableReason(string propertyName) {
			string reason;
			bool available = this.TryGetPropertyAvailability(propertyName, out reason);
			return available ? "" : reason;
		}

		[KRPCMethod]
		public BoolReason TrySetDesiredOrbitAltitude(double altitude) {
			try {
				this.DesiredOrbitAltitude = altitude;
				return Tuple.Create(true, "");
			}
			catch(Exception ex) {
				return Tuple.Create(false, ex.Message ?? "");
			}
		}

		[KRPCMethod]
		public BoolReason TrySetLimitAoA(bool enabled) {
			try {
				this.LimitAoA = enabled;
				return Tuple.Create(true, "");
			}
			catch(Exception ex) {
				return Tuple.Create(false, ex.Message ?? "");
			}
		}

		[KRPCMethod]
		public BoolValueReason TryGetLimitAoA() {
			try {
				return Tuple.Create(true, this.LimitAoA, "");
			}
			catch(Exception ex) {
				return Tuple.Create(false, false, ex.Message ?? "");
			}
		}

		[KRPCMethod]
		public BoolReason TrySetMaxAoA(double degrees) {
			string reason;
			return Tuple.Create(this.TrySetEditableDouble(this.maxAoA, "MaxAoA", degrees, out reason), reason ?? "");
		}

		[KRPCMethod]
		public DoubleValueReason TryGetMaxAoA() {
			double value;
			string reason;
			bool success = this.TryGetEditableDouble(this.maxAoA, "MaxAoA", out value, out reason);
			return Tuple.Create(success, value, reason ?? "");
		}

		[KRPCMethod]
		public BoolReason TrySetCorrectiveSteering(bool enabled) {
			string reason;
			return Tuple.Create(this.TrySetBoolField(correctiveSteering, "CorrectiveSteering", enabled, out reason), reason ?? "");
		}

		[KRPCMethod]
		public BoolValueReason TryGetCorrectiveSteering() {
			bool value;
			string reason;
			bool success = this.TryGetBoolField(correctiveSteering, "CorrectiveSteering", out value, out reason);
			return Tuple.Create(success, value, reason ?? "");
		}

		[KRPCMethod]
		public BoolReason TrySetCorrectiveSteeringGain(double gain) {
			string reason;
			return Tuple.Create(this.TrySetEditableDouble(this.correctiveSteeringGain, "CorrectiveSteeringGain", gain, out reason), reason ?? "");
		}

		[KRPCMethod]
		public DoubleValueReason TryGetCorrectiveSteeringGain() {
			double value;
			string reason;
			bool success = this.TryGetEditableDouble(this.correctiveSteeringGain, "CorrectiveSteeringGain", out value, out reason);
			return Tuple.Create(success, value, reason ?? "");
		}

		[KRPCMethod]
		public BoolReason TrySetForceRoll(bool enabled) {
			try {
				this.ForceRoll = enabled;
				return Tuple.Create(true, "");
			}
			catch(Exception ex) {
				return Tuple.Create(false, ex.Message ?? "");
			}
		}

		[KRPCMethod]
		public BoolReason TrySetSkipCircularization(bool enabled) {
			try {
				this.SkipCircularization = enabled;
				return Tuple.Create(true, "");
			}
			catch(Exception ex) {
				return Tuple.Create(false, ex.Message ?? "");
			}
		}

		[KRPCMethod]
		public BoolValueReason TryGetSkipCircularization() {
			try {
				return Tuple.Create(true, this.SkipCircularization, "");
			}
			catch(Exception ex) {
				return Tuple.Create(false, false, ex.Message ?? "");
			}
		}

		/// <summary>
		/// The desired inclination in degrees for the final circular orbit.
		/// </summary>
		[KRPCProperty]
		public double DesiredInclination {
			// We need to get desiredInclinationGUI value here because it may change over time.
			get => EditableDouble.Get(AscentGuidance.desiredInclination, this.guiInstance);
			set => EditableDouble.Set(AscentGuidance.desiredInclination, this.guiInstance, value);
		}

		/// <remarks>Equivalent to <see cref="MechJeb.ThrustController" />.</remarks>
		[KRPCProperty]
		public ThrustController ThrustController => MechJeb.ThrustController;

		/// <summary>
		/// Will cause the craft to steer based on the more accurate velocity vector rather than positional vector (large craft may actually perform better with this box unchecked).
		/// </summary>
		[KRPCProperty]
		public bool CorrectiveSteering {
			get => (bool)correctiveSteering.GetValue(this.instance);
			set => correctiveSteering.SetValue(this.instance, value);
		}

		/// <summary>
		/// The gain of corrective steering used by the autopilot.
		/// </summary>
		/// <remarks><see cref="CorrectiveSteering" /> needs to be enabled.</remarks>
		[KRPCProperty]
		public double CorrectiveSteeringGain {
			get => EditableDouble.Get(this.correctiveSteeringGain);
			set => EditableDouble.Set(this.correctiveSteeringGain, value);
		}

		/// <summary>
		/// The state of force roll.
		/// </summary>
		[KRPCProperty]
		public bool ForceRoll {
			get => this.GetBoolSetting(forceRoll, "ForceRoll", "forceRoll");
			set => this.SetBoolSetting(value, forceRoll, "ForceRoll", "forceRoll");
		}

		/// <summary>
		/// The vertical/climb roll used by the autopilot.
		/// </summary>
		/// <remarks><see cref="ForceRoll" /> needs to be enabled.</remarks>
		[KRPCProperty]
		public double VerticalRoll {
			get => EditableDouble.Get(this.verticalRoll);
			set => EditableDouble.Set(this.verticalRoll, value);
		}

		/// <summary>
		/// The turn roll used by the autopilot.
		/// </summary>
		/// <remarks><see cref="ForceRoll" /> needs to be enabled.</remarks>
		[KRPCProperty]
		public double TurnRoll {
			get => EditableDouble.Get(this.turnRoll);
			set => EditableDouble.Set(this.turnRoll, value);
		}

		/// <summary>
		/// Whether to deploy solar panels automatically when the ascent finishes.
		/// </summary>
		[KRPCProperty]
		public bool AutodeploySolarPanels {
			get => (bool)autodeploySolarPanels.GetValue(this.instance);
			set => autodeploySolarPanels.SetValue(this.instance, value);
		}

		/// <summary>
		/// Whether to deploy antennas automatically when the ascent finishes.
		/// </summary>
		[KRPCProperty]
		public bool AutoDeployAntennas {
			get => (bool)autoDeployAntennas.GetValue(this.instance);
			set => autoDeployAntennas.SetValue(this.instance, value);
		}

		/// <summary>
		/// Whether to skip circularization burn and do only the ascent.
		/// </summary>
		[KRPCProperty]
		public bool SkipCircularization {
			get => this.GetBoolSetting(skipCircularization, "SkipCircularization", "skipCircularization");
			set => this.SetBoolSetting(value, skipCircularization, "SkipCircularization", "skipCircularization");
		}

		/// <summary>
		/// The autopilot will automatically stage when the current stage has run out of fuel.
		/// Paramethers can be set in <see cref="KRPC.MechJeb.StagingController" />.
		/// </summary>
		[KRPCProperty]
		public bool Autostage {
			get {
				if(autostage != null)
					return (bool)autostage.GetValue(this.instance, null);
				if(autostageField != null)
					return (bool)autostageField.GetValue(this.instance);
				object fallback = GetFieldValue(this.guiInstance, "_autostage", "Autostage");
				if(fallback != null)
					return (bool)fallback;
				return false;
			}
			set {
				if(autostage != null)
					autostage.SetValue(this.instance, value, null);
				else if(autostageField != null)
					autostageField.SetValue(this.instance, value);
				else
					this.SetBoolSetting(value, null, "_autostage", "Autostage");
			}
		}

		/// <remarks>Equivalent to <see cref="MechJeb.StagingController" />.</remarks>
		[KRPCProperty]
		public StagingController StagingController => MechJeb.StagingController;

		/// <summary>
		/// Whether to limit angle of attack.
		/// </summary>
		[KRPCProperty]
		public bool LimitAoA {
			get => this.GetBoolSetting(limitAoA, "LimitAoA", "limitAoA");
			set => this.SetBoolSetting(value, limitAoA, "LimitAoA", "limitAoA");
		}

		/// <summary>
		/// The maximal angle of attack used by the autopilot.
		/// </summary>
		/// <remarks><see cref="LimitAoA" /> needs to be enabled</remarks>
		[KRPCProperty]
		public double MaxAoA {
			get => EditableDouble.Get(this.maxAoA);
			set => EditableDouble.Set(this.maxAoA, value);
		}

		/// <summary>
		/// The pressure value when AoA limit is automatically deactivated.
		/// </summary>
		/// <remarks><see cref="LimitAoA" /> needs to be enabled</remarks>
		[KRPCProperty]
		public double AoALimitFadeoutPressure {
			get => EditableDouble.Get(this.aoALimitFadeoutPressure);
			set => EditableDouble.Set(this.aoALimitFadeoutPressure, value);
		}

		[KRPCProperty]
		public double LaunchPhaseAngle {
			get => EditableDouble.Get(this.launchPhaseAngle);
			set => EditableDouble.Set(this.launchPhaseAngle, value);
		}

		[KRPCProperty]
		public double LaunchLANDifference {
			get => EditableDouble.Get(this.launchLANDifference);
			set => EditableDouble.Set(this.launchLANDifference, value);
		}

		[KRPCProperty]
		public int WarpCountDown {
			get => EditableInt.Get(this.warpCountDown);
			set => EditableInt.Set(this.warpCountDown, value);
		}

		/// <summary>
		/// Current autopilot mode. Useful for determining whether the autopilot is performing a timed launch or not.
		/// </summary>
		[KRPCProperty]
		public AscentLaunchMode LaunchMode {
			get {
				if(timedLaunch == null)
					return AscentLaunchMode.Unknown;
				if(!(bool)timedLaunch.GetValue(this.instance))
					return AscentLaunchMode.Normal;
				if(this.guiInstance == null || AscentGuidance.launchingToRendezvous == null || AscentGuidance.launchingToPlane == null)
					return AscentLaunchMode.Unknown;
				if((bool)AscentGuidance.launchingToRendezvous.GetValue(this.guiInstance))
					return AscentLaunchMode.Rendezvous;
				if((bool)AscentGuidance.launchingToPlane.GetValue(this.guiInstance))
					return AscentLaunchMode.TargetPlane;
				return AscentLaunchMode.Unknown;
			}
		}

		/// <summary>
		/// Whether launch-to-rendezvous can be started immediately with the current vessel and target state.
		/// </summary>
		[KRPCProperty]
		public bool CanLaunchToRendezvous => this.RendezvousLaunchStatus == AscentTimedLaunchStatus.Ready;

		/// <summary>
		/// Whether launch-to-target-plane can be started immediately with the current vessel and target state.
		/// </summary>
		[KRPCProperty]
		public bool CanLaunchToTargetPlane => this.TargetPlaneLaunchStatus == AscentTimedLaunchStatus.Ready;

		/// <summary>
		/// Readiness state for <see cref="LaunchToRendezvous" />.
		/// </summary>
		[KRPCProperty]
		public AscentTimedLaunchStatus RendezvousLaunchStatus => this.GetRendezvousLaunchStatus();

		/// <summary>
		/// Readiness state for <see cref="LaunchToTargetPlane" />.
		/// </summary>
		[KRPCProperty]
		public AscentTimedLaunchStatus TargetPlaneLaunchStatus => this.GetTargetPlaneLaunchStatus();

		private AscentTimedLaunchStatus GetRendezvousLaunchStatus() {
			if(this.LaunchMode == AscentLaunchMode.Unknown)
				return AscentTimedLaunchStatus.UnknownTimedLaunch;
			if(!MechJeb.TargetController.NormalTargetExists)
				return AscentTimedLaunchStatus.MissingTarget;
			if(MechJeb.TargetController.InternalTargetOrbit == null)
				return AscentTimedLaunchStatus.InvalidTargetOrbit;
			if(this.AscentPathIndex == 2)
				return AscentTimedLaunchStatus.UnsupportedAscentPath;

			return AscentTimedLaunchStatus.Ready;
		}

		private AscentTimedLaunchStatus GetTargetPlaneLaunchStatus() {
			if(this.LaunchMode == AscentLaunchMode.Unknown)
				return AscentTimedLaunchStatus.UnknownTimedLaunch;
			if(!MechJeb.TargetController.NormalTargetExists)
				return AscentTimedLaunchStatus.MissingTarget;
			if(MechJeb.TargetController.InternalTargetOrbit == null)
				return AscentTimedLaunchStatus.InvalidTargetOrbit;

			return AscentTimedLaunchStatus.Ready;
		}

		private static string GetTimedLaunchStatusError(AscentTimedLaunchStatus status, string action) {
			switch(status) {
			case AscentTimedLaunchStatus.Ready:
				return null;
			case AscentTimedLaunchStatus.MissingTarget:
				return string.Format("Cannot {0}: no valid target is selected", action);
			case AscentTimedLaunchStatus.InvalidTargetOrbit:
				return string.Format("Cannot {0}: selected target does not have a valid orbit", action);
			case AscentTimedLaunchStatus.UnsupportedAscentPath:
				return string.Format("Cannot {0}: this action can't be performed in PVG path mode", action);
			case AscentTimedLaunchStatus.UnknownTimedLaunch:
				return string.Format("Cannot {0}: there is an unknown timed launch ongoing", action);
			default:
				return string.Format("Cannot {0}: timed launch is not ready", action);
			}
		}

		/// <summary>
		/// Abort a known timed launch when it has not started yet
		/// </summary>
		[KRPCMethod]
		public void AbortTimedLaunch() {
			if(this.LaunchMode == AscentLaunchMode.Unknown)
				throw new InvalidOperationException("There is an unknown timed launch ongoing which can't be aborted");
			if(this.guiInstance == null || AscentGuidance.launchingToPlane == null || AscentGuidance.launchingToRendezvous == null)
				throw new MJServiceException("Timed launch controls are unavailable for this MechJeb build.");

			AscentGuidance.launchingToPlane.SetValue(this.guiInstance, false);
			AscentGuidance.launchingToRendezvous.SetValue(this.guiInstance, false);
			timedLaunch.SetValue(this.instance, false);
		}

		private void StartCountdown(double timeOffset) {
			startCountdown.Invoke(this.instance, new object[] { MechJeb.vesselState.Time + timeOffset });
		}

		/// <summary>
		/// Launch to rendezvous with the selected target.
		/// </summary>
		[KRPCMethod]
		public void LaunchToRendezvous() {
			AscentTimedLaunchStatus status = this.GetRendezvousLaunchStatus();
			if(status != AscentTimedLaunchStatus.Ready)
				throw new InvalidOperationException(GetTimedLaunchStatusError(status, "start launch-to-rendezvous"));

			this.AbortTimedLaunch();
			try {
				AscentGuidance.launchingToRendezvous.SetValue(this.guiInstance, true);
				this.Enabled = true;
				this.StartCountdown(LaunchTiming.TimeToPhaseAngle(this.LaunchPhaseAngle));
			}
			catch(Exception) {
				this.AbortTimedLaunch();
				this.Enabled = false;
				throw;
			}
		}

		/// <summary>
		/// Launch into the plane of the selected target.
		/// </summary>
		[KRPCMethod]
		public void LaunchToTargetPlane() {
			AscentTimedLaunchStatus status = this.GetTargetPlaneLaunchStatus();
			if(status != AscentTimedLaunchStatus.Ready)
				throw new InvalidOperationException(GetTimedLaunchStatusError(status, "start launch-to-target-plane"));

			this.AbortTimedLaunch();
			try {
				Orbit target = MechJeb.TargetController.InternalTargetOrbit;
				AscentGuidance.launchingToPlane.SetValue(this.guiInstance, true);
				this.Enabled = true;

				Tuple<double, double> item = MathFunctions.MinimumTimeToPlane(target.LAN - this.LaunchLANDifference, target.inclination);
				this.StartCountdown(item.Item1);
				this.DesiredInclination = item.Item2;
			}
			catch(Exception) {
				this.AbortTimedLaunch();
				this.Enabled = false;
				throw;
			}
		}

		[KRPCEnum(Service = "MechJeb")]
		public enum AscentLaunchMode {
			/// <summary>
			/// The autopilot is not performing a timed launch.
			/// </summary>
			Normal,

			/// <summary>
			/// The autopilot is performing a timed launch to rendezvous with the target vessel.
			/// </summary>
			Rendezvous,

			/// <summary>
			/// The autopilot is performing a timed launch to target plane.
			/// </summary>
			TargetPlane,

			/// <summary>
			/// The autopilot is performing an unknown timed launch.
			/// </summary>
			Unknown = 99
		}

		/// <summary>
		/// Readiness state for timed-launch operations.
		/// </summary>
		[KRPCEnum(Service = "MechJeb")]
		public enum AscentTimedLaunchStatus {
			/// <summary>
			/// Timed launch can be started.
			/// </summary>
			Ready,

			/// <summary>
			/// No normal target is selected.
			/// </summary>
			MissingTarget,

			/// <summary>
			/// The selected target does not expose a valid orbit.
			/// </summary>
			InvalidTargetOrbit,

			/// <summary>
			/// The currently selected ascent path does not support this launch mode.
			/// </summary>
			UnsupportedAscentPath,

			/// <summary>
			/// A timed launch is active but its mode cannot be identified.
			/// </summary>
			UnknownTimedLaunch = 99
		}
	}

	public abstract class AscentBase : ComputerModule { }
}
