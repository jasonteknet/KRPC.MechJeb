using System;
using System.Reflection;

using KRPC.MechJeb.ExtensionMethods;
using KRPC.MechJeb.Util;
using KRPC.Service.Attributes;

namespace KRPC.MechJeb {
	internal static class AscentGuidance {
		internal new const string MechJebType = "MuMech.MechJebModuleAscentGuidance";

		// Fields and methods
		internal static FieldInfo desiredInclination;
		internal static FieldInfo launchingToPlane;
		internal static FieldInfo launchingToRendezvous;

		internal static void InitType(Type type) {
			desiredInclination = type.GetField("desiredInclination");
			launchingToPlane = type.GetField("launchingToPlane");
			launchingToRendezvous = type.GetField("launchingToRendezvous");
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

		// Fields and methods
		private static FieldInfo status;
		private static PropertyInfo ascentPathIdx;
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
			base.InitInstance(instance);
			this.guiInstance = MechJeb.GetComputerModule("AscentGuidance");

			this.desiredOrbitAltitude = desiredOrbitAltitudeField.GetInstanceValue(instance);
			this.correctiveSteeringGain = correctiveSteeringGainField.GetInstanceValue(instance);
			this.verticalRoll = verticalRollField.GetInstanceValue(instance);
			this.turnRoll = turnRollField.GetInstanceValue(instance);
			this.maxAoA = maxAoAField.GetInstanceValue(instance);
			this.aoALimitFadeoutPressure = aoALimitFadeoutPressureField.GetInstanceValue(instance);
			this.launchPhaseAngle = launchPhaseAngleField.GetInstanceValue(instance);
			this.launchLANDifference = launchLANDifferenceField.GetInstanceValue(instance);
			this.warpCountDown = warpCountDownField.GetInstanceValue(instance);

			this.AscentPathClassic.InitInstance(MechJeb.GetComputerModule("AscentClassic"));
			this.AscentPathGT.InitInstance(MechJeb.GetComputerModule("AscentGT"));
			this.AscentPathPVG.InitInstance(MechJeb.GetComputerModule("AscentPVG"));

			// Retrieve the current path index set in mechjeb and enable the path representing that index.
			// It fixes the issue with AscentAutopilot reporting empty status due to a disabled path.
			if(instance != null)
				this.AscentPathIndex = this.AscentPathIndex;
		}

		public AscentAutopilot() {
			this.AscentPathClassic = new AscentClassic();
			this.AscentPathGT = new AscentGT();
			this.AscentPathPVG = new AscentPVG();
		}

		/// <summary>
		/// The autopilot status; it depends on the selected ascent path.
		/// </summary>
		[KRPCProperty]
		public string Status => status.GetValue(this.instance).ToString();

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
			get => (int)ascentPathIdx.GetValue(this.instance, null);
			set {
				if(value < 0 || value > 2)
					return;

				ascentPathIdx.SetValue(this.instance, value, null);
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
			get => (bool)forceRoll.GetValue(this.instance);
			set => forceRoll.SetValue(this.instance, value);
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
			get => (bool)skipCircularization.GetValue(this.instance);
			set => skipCircularization.SetValue(this.instance, value);
		}

		/// <summary>
		/// The autopilot will automatically stage when the current stage has run out of fuel.
		/// Paramethers can be set in <see cref="KRPC.MechJeb.StagingController" />.
		/// </summary>
		[KRPCProperty]
		public bool Autostage {
			get => (bool)autostage.GetValue(this.instance, null);
			set => autostage.SetValue(this.instance, value, null);
		}

		/// <remarks>Equivalent to <see cref="MechJeb.StagingController" />.</remarks>
		[KRPCProperty]
		public StagingController StagingController => MechJeb.StagingController;

		/// <summary>
		/// Whether to limit angle of attack.
		/// </summary>
		[KRPCProperty]
		public bool LimitAoA {
			get => (bool)limitAoA.GetValue(this.instance);
			set => limitAoA.SetValue(this.instance, value);
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
				if(!(bool)timedLaunch.GetValue(this.instance))
					return AscentLaunchMode.Normal;
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
