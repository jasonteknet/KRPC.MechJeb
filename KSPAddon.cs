using UnityEngine;

namespace KRPC.MechJeb {
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	internal class InitTypes : MonoBehaviour {
		public void Start() {
			Logger.Info("Loading MechJeb types...");
			Logger.Info(MechJeb.InitTypes() ? "MechJeb found!" : "MechJeb is not available.");
			MechJeb.ShowErrors();
		}
	}

	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class InitInstance : MonoBehaviour {
		private Vessel activeVessel;
		private float nextRetryAt;

		public void LateUpdate() {
			if(!MechJeb.TypesLoaded)
				return;

			// Refresh MechJeb instance when focus changes or a flight is reverted to launch.
			// Also retry periodically when initialization failed, because the first frame after a vessel switch
			// can happen before MechJeb modules are fully ready.
			bool vesselChanged = this.activeVessel != FlightGlobals.ActiveVessel;
			bool shouldRetry = !MechJeb.APIReady && Time.unscaledTime >= this.nextRetryAt;
			if(!vesselChanged && !shouldRetry)
				return;

			this.activeVessel = FlightGlobals.ActiveVessel;

			if(vesselChanged || shouldRetry)
				Logger.Info("Initializing MechJeb instance...");

			if(MechJeb.InitInstance()) {
				Logger.Info("KRPC.MechJeb is ready!");
				this.nextRetryAt = 0f;
			}
			else {
				Logger.Info("MechJeb found but the instance initialization wasn't successful. Maybe you don't have any MechJeb part attached to the vessel?");
				this.nextRetryAt = Time.unscaledTime + 1f;
			}

			MechJeb.ShowErrors();
		}
	}
}
