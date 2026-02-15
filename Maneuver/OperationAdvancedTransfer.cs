using System;
using System.Reflection;

using KRPC.MechJeb.ExtensionMethods;
using KRPC.Service.Attributes;

namespace KRPC.MechJeb.Maneuver {
	/// <summary>
	/// Create an advanced transfer maneuver to another planet.
	/// </summary>
	[KRPCClass(Service = "MechJeb")]
	public class OperationAdvancedTransfer : Operation {
		internal new const string MechJebType = "MuMech.OperationAdvancedTransfer";

		private static FieldInfo maxArrivalTimeField;
		private static FieldInfo includeCaptureBurnField;
		private static FieldInfo periapsisHeightField;

		private object maxArrivalTime;
		private object periapsisHeight;

		internal static new void InitType(Type type) {
			maxArrivalTimeField = type.GetCheckedField("maxArrivalTime", BindingFlags.NonPublic | BindingFlags.Instance);
			includeCaptureBurnField = type.GetCheckedField("includeCaptureBurn", BindingFlags.NonPublic | BindingFlags.Instance);
			periapsisHeightField = type.GetCheckedField("periapsisHeight", BindingFlags.NonPublic | BindingFlags.Instance);
		}

		protected internal override void InitInstance(object instance) {
			base.InitInstance(instance);
			this.maxArrivalTime = maxArrivalTimeField.GetInstanceValue(instance);
			this.periapsisHeight = periapsisHeightField.GetInstanceValue(instance);
		}

		/// <summary>
		/// Maximum arrival time used by the limited-time solver mode.
		/// </summary>
		[KRPCProperty]
		public double MaxArrivalTime {
			get => EditableDouble.Get(this.maxArrivalTime);
			set => EditableDouble.Set(this.maxArrivalTime, value);
		}

		/// <summary>
		/// Include capture burn at the destination body.
		/// </summary>
		[KRPCProperty]
		public bool IncludeCaptureBurn {
			get => includeCaptureBurnField != null && this.instance != null && (bool)includeCaptureBurnField.GetValue(this.instance);
			set {
				if(includeCaptureBurnField == null || this.instance == null)
					throw new MJServiceException("Advanced transfer include-capture option is unavailable for this MechJeb build.");
				includeCaptureBurnField.SetValue(this.instance, value);
			}
		}

		/// <summary>
		/// Target periapsis height at destination in kilometers.
		/// </summary>
		[KRPCProperty]
		public double PeriapsisHeight {
			get => EditableDouble.Get(this.periapsisHeight);
			set => EditableDouble.Set(this.periapsisHeight, value);
		}
	}
}
