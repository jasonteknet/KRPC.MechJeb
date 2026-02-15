using System;
using System.Reflection;

using KRPC.MechJeb.ExtensionMethods;
using KRPC.Service.Attributes;

namespace KRPC.MechJeb.Maneuver {
	/// <summary>
	/// Bi-impulsive (Hohmann) transfer to target.
	/// 
	/// This option is used to plan transfer to target in single sphere of influence. It is suitable for rendezvous with other vessels or moons.
	/// Contrary to the name, the transfer is often uni-impulsive. You can select when you want the manevuer to happen or select optimum time.
	/// </summary>
	[KRPCClass(Service = "MechJeb")]
	public class OperationTransfer : TimedOperation {
		internal new const string MechJebType = "MuMech.OperationGeneric";

		// Fields and methods
		private static FieldInfo interceptOnly;
		private static FieldInfo periodOffsetField;
		private static FieldInfo simpleTransfer;
		private static FieldInfo timeSelector;

		// Instance objects
		private object periodOffset;

		internal static new void InitType(Type type) {
			interceptOnly = type.GetCheckedField("intercept_only");
			periodOffsetField = type.GetCheckedField("periodOffset");
			simpleTransfer = type.GetCheckedField("simpleTransfer");
			timeSelector = GetTimeSelectorField(type);
		}

		protected internal override void InitInstance(object instance) {
			base.InitInstance(instance);

			this.periodOffset = periodOffsetField.GetInstanceValue(instance);
			this.InitTimeSelector(timeSelector);
		}

		private bool IsTransferOptionAvailable(FieldInfo option) {
			if(option == null || this.instance == null)
				return false;

			try {
				option.GetValue(this.instance);
				return true;
			}
			catch(NullReferenceException) {
				return false;
			}
			catch(TargetInvocationException ex) when(ex.InnerException is NullReferenceException) {
				return false;
			}
		}

		private bool GetTransferOption(FieldInfo option) {
			if(!this.IsTransferOptionAvailable(option))
				return false;

			try {
				return (bool)option.GetValue(this.instance);
			}
			catch(NullReferenceException) {
				return false;
			}
			catch(TargetInvocationException ex) when(ex.InnerException is NullReferenceException) {
				return false;
			}
		}

		private void SetTransferOption(FieldInfo option, string optionName, bool value) {
			if(!this.IsTransferOptionAvailable(option)) {
				Logger.Warning("OperationTransfer." + optionName + " is unavailable in current transfer context. Ignoring requested value.");
				return;
			}

			try {
				option.SetValue(this.instance, value);
			}
			catch(NullReferenceException) {
				Logger.Warning("OperationTransfer." + optionName + " setter was unavailable in current transfer context. Ignoring requested value.");
			}
			catch(TargetInvocationException ex) when(ex.InnerException is NullReferenceException) {
				Logger.Warning("OperationTransfer." + optionName + " setter was unavailable in current transfer context. Ignoring requested value.");
			}
		}

		[KRPCProperty]
		public bool InterceptOnlyAvailable => this.IsTransferOptionAvailable(interceptOnly);

		/// <summary>
		/// Intercept only, no capture burn (impact/flyby)
		/// </summary>
		[KRPCProperty]
		public bool InterceptOnly {
			get => this.GetTransferOption(interceptOnly);
			set => this.SetTransferOption(interceptOnly, "InterceptOnly", value);
		}

		/// <summary>
		/// Fractional target period offset
		/// </summary>
		[KRPCProperty]
		public double PeriodOffset {
			get => EditableDouble.Get(this.periodOffset);
			set => EditableDouble.Set(this.periodOffset, value);
		}

		/// <summary>
		/// Simple coplanar Hohmann transfer.
		/// Set it to true if you are used to the old version of transfer maneuver.
		/// </summary>
		/// <remarks>If set to true, TimeSelector property is ignored.</remarks>
		[KRPCProperty]
		public bool SimpleTransferAvailable => this.IsTransferOptionAvailable(simpleTransfer);

		/// <summary>
		/// Simple coplanar Hohmann transfer.
		/// Set it to true if you are used to the old version of transfer maneuver.
		/// </summary>
		/// <remarks>If set to true, TimeSelector property is ignored.</remarks>
		[KRPCProperty]
		public bool SimpleTransfer {
			get => this.GetTransferOption(simpleTransfer);
			set => this.SetTransferOption(simpleTransfer, "SimpleTransfer", value);
		}
	}
}
