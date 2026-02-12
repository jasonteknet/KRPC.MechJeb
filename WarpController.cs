using System;
using System.Reflection;

using KRPC.MechJeb.ExtensionMethods;
using KRPC.Service.Attributes;

namespace KRPC.MechJeb {
	[KRPCClass(Service = "MechJeb")]
	public class WarpController : ComputerModule {
		internal new const string MechJebType = "MuMech.MechJebModuleWarpController";

		private static PropertyInfo warpPaused;
		private static FieldInfo activateSASOnWarp;
		private static FieldInfo useQuickWarp;

		private static MethodInfo warpToUTMethod;

		internal static new void InitType(Type type) {
			warpPaused = type.GetCheckedProperty("WarpPaused");
			activateSASOnWarp = type.GetCheckedField("activateSASOnWarp");
			useQuickWarp = type.GetCheckedField("useQuickWarp");

			warpToUTMethod = type.GetCheckedMethod("WarpToUT", new Type[] { typeof(double), typeof(double) });
		}

		[KRPCProperty]
		public override bool Enabled => base.Enabled;

		[KRPCProperty]
		public bool WarpPaused => (bool)warpPaused.GetValue(this.instance, null);

		[KRPCProperty]
		public bool ActivateSASOnWarp {
			get => (bool)activateSASOnWarp.GetValue(this.instance);
			set => activateSASOnWarp.SetValue(this.instance, value);
		}

		[KRPCProperty]
		public bool UseQuickWarp {
			get => (bool)useQuickWarp.GetValue(this.instance);
			set => useQuickWarp.SetValue(this.instance, value);
		}

		[KRPCMethod]
		public void WarpToUT(double ut) {
			warpToUTMethod.Invoke(this.instance, new object[] { ut, -1.0 });
		}

		[KRPCMethod]
		public void WarpToUTWithMaxRate(double ut, double maxRate) {
			warpToUTMethod.Invoke(this.instance, new object[] { ut, maxRate });
		}
	}
}
