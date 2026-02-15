#!/usr/bin/env python3
"""
Smoke test for kRPC MechJeb service compatibility.

Focus:
- Catch reflection/signature drift that manifests as runtime crashes.
- Differentiate expected operation precondition errors from hard failures.
"""

from __future__ import annotations

import argparse
import sys
import traceback
from dataclasses import dataclass
from typing import Any, Callable, List

import krpc


CRASH_MARKERS = (
    "NullReferenceException",
    "TargetInvocationException",
    "MissingFieldException",
    "MissingMethodException",
    "TypeLoadException",
)

EXPECTED_OPERATION_MARKERS = (
    "target",
    "inclination",
    "intercept",
    "cannot",
    "not allowed",
    "no transfer",
    "no maneuver",
    "no maneuver parameters",
)


@dataclass
class CheckResult:
    name: str
    status: str
    detail: str = ""


def safe_read(results: List[CheckResult], name: str, fn: Callable[[], Any]) -> Any:
    try:
        value = fn()
        results.append(CheckResult(name, "PASS", repr(value)))
        return value
    except Exception as ex:  # pylint: disable=broad-except
        msg = str(ex)
        status = "FAIL"
        if any(marker in msg for marker in CRASH_MARKERS):
            status = "FAIL_CRASH"
        results.append(CheckResult(name, status, msg))
        return None


def safe_attr(results: List[CheckResult], owner_name: str, obj: Any, attr: str, aliases: List[str] | None = None) -> Any:
    names = [attr] + (aliases or [])
    try:
        exposed = set(dir(obj))
    except Exception as ex:  # pylint: disable=broad-except
        results.append(CheckResult(f"{owner_name}.{attr}", "FAIL", f"cannot inspect attributes: {ex}"))
        return None
    for name in names:
        if name in exposed:
            return safe_read(results, f"{owner_name}.{name}", lambda o=obj, n=name: getattr(o, n))
    results.append(CheckResult(f"{owner_name}.{attr}", "SKIP", "attribute not exposed by this client/service build"))
    return None


def is_expected_operation_error(message: str) -> bool:
    lower = message.lower()
    return any(marker in lower for marker in EXPECTED_OPERATION_MARKERS)


def probe_basic_service(conn: krpc.Client, results: List[CheckResult]) -> Any:
    safe_read(results, "krpc.version", lambda: conn.krpc.get_status().version)
    mj = safe_read(results, "service.mech_jeb", lambda: conn.mech_jeb)
    if mj is None:
        return None
    ready = safe_read(results, "mech_jeb.api_ready", lambda: mj.api_ready)
    if ready is False:
        results.append(CheckResult("mech_jeb.api_ready", "FAIL", "API is not ready"))
    return mj


def probe_modules(mj: Any, results: List[CheckResult]) -> None:
    modules = {
        "airplane_autopilot": ["enabled"],
        "ascent_autopilot": ["enabled", "ascent_path_index", "autostage"],
        "docking_autopilot": ["enabled", "speed_limit"],
        "landing_autopilot": ["enabled", "touchdown_speed"],
        "rendezvous_autopilot": ["enabled", "desired_distance", "max_phasing_orbits"],
        "maneuver_planner": [],
        "smart_ass": ["autopilot_mode", "force_roll", "force_pitch", "force_yaw"],
        "smart_rcs": ["auto_disable_smart_rcs"],
        "translatron": ["trans_spd"],
        "antenna_controller": ["auto_deploy"],
        "node_executor": ["enabled", "autowarp", "lead_time", "tolerance"],
        "rcs_controller": ["rcs_for_rotation", "rcs_throttle"],
        "staging_controller": ["enabled", "autostage_limit"],
        "solar_panel_controller": ["auto_deploy"],
        "target_controller": ["normal_target_exists", "position_target_exists", "can_align"],
        "thrust_controller": ["limit_dynamic_pressure", "limit_acceleration", "limit_throttle"],
        "warp_controller": ["enabled", "warp_paused", "activate_sas_on_warp", "use_quick_warp"],
    }

    for module_name, props in modules.items():
        module = safe_read(results, f"module.{module_name}", lambda n=module_name: getattr(mj, n))
        if module is None:
            continue
        for prop in props:
            safe_attr(results, module_name, module, prop)


def probe_ascent_compat(mj: Any, results: List[CheckResult]) -> None:
    ascent = safe_read(results, "module.ascent_autopilot", lambda: mj.ascent_autopilot)
    if ascent is None:
        return

    checks = [
        ("desired_orbit_altitude", "try_set_desired_orbit_altitude", [100.0]),
        ("limit_ao_a", "try_set_limit_ao_a", [True]),
        ("limit_ao_a", "try_get_limit_ao_a", []),
        ("max_ao_a", "try_set_max_ao_a", [5.0]),
        ("max_ao_a", "try_get_max_ao_a", []),
        ("corrective_steering", "try_set_corrective_steering", [True]),
        ("corrective_steering", "try_get_corrective_steering", []),
        ("corrective_steering_gain", "try_set_corrective_steering_gain", [0.6]),
        ("corrective_steering_gain", "try_get_corrective_steering_gain", []),
        ("force_roll", "try_set_force_roll", [False]),
        ("skip_circularization", "try_set_skip_circularization", [False]),
        ("skip_circularization", "try_get_skip_circularization", []),
    ]

    for prop_name, method_name, args in checks:
        safe_read(
            results,
            f"ascent.is_property_available({prop_name})",
            lambda n=prop_name: ascent.is_property_available(n),
        )
        safe_read(
            results,
            f"ascent.get_unavailable_reason({prop_name})",
            lambda n=prop_name: ascent.get_unavailable_reason(n),
        )
        safe_read(
            results,
            f"ascent.{method_name}",
            lambda m=method_name, a=args: getattr(ascent, m)(*a),
        )


def clear_nodes(conn: krpc.Client, results: List[CheckResult]) -> None:
    vessel = conn.space_center.active_vessel
    removed = 0
    for node in list(vessel.control.nodes):
        node.remove()
        removed += 1
    results.append(CheckResult("space_center.clear_nodes", "PASS", f"removed={removed}"))


def probe_operations(conn: krpc.Client, mj: Any, results: List[CheckResult], make_nodes: bool) -> None:
    planner = safe_read(results, "maneuver_planner", lambda: mj.maneuver_planner)
    if planner is None:
        return

    operations = [
        "operation_apoapsis",
        "operation_circularize",
        "operation_course_correction",
        "operation_ellipticize",
        "operation_inclination",
        "operation_interplanetary_transfer",
        "operation_kill_rel_vel",
        "operation_lambert",
        "operation_lan",
        "operation_longitude",
        "operation_moon_return",
        "operation_periapsis",
        "operation_plane",
        "operation_resonant_orbit",
        "operation_semi_major",
        "operation_transfer",
    ]

    for op_name in operations:
        op = safe_read(results, f"op.{op_name}", lambda n=op_name: getattr(planner, n))
        if op is None:
            continue
        safe_read(results, f"op.{op_name}.error_message", lambda o=op: o.error_message)

        ts = safe_attr(results, f"op.{op_name}", op, "time_selector")
        if ts is not None:
            safe_attr(results, f"op.{op_name}.time_selector", ts, "time_reference", aliases=["current_time_reference"])
            safe_attr(results, f"op.{op_name}.time_selector", ts, "allowed_time_references")

        if not make_nodes:
            continue

        try:
            nodes = op.make_nodes()
            count = len(nodes)
            results.append(CheckResult(f"op.{op_name}.make_nodes", "PASS", f"nodes={count}"))
            clear_nodes(conn, results)
        except Exception as ex:  # pylint: disable=broad-except
            msg = str(ex)
            if any(marker in msg for marker in CRASH_MARKERS):
                results.append(CheckResult(f"op.{op_name}.make_nodes", "FAIL_CRASH", msg))
            elif is_expected_operation_error(msg):
                results.append(CheckResult(f"op.{op_name}.make_nodes", "EXPECTED_FAIL", msg))
            else:
                results.append(CheckResult(f"op.{op_name}.make_nodes", "FAIL", msg))


def summarize(results: List[CheckResult]) -> int:
    fail = [r for r in results if r.status.startswith("FAIL")]
    expected = [r for r in results if r.status == "EXPECTED_FAIL"]
    skipped = [r for r in results if r.status == "SKIP"]

    for r in results:
        print(f"[{r.status}] {r.name}: {r.detail}")

    print("")
    print(f"Summary: total={len(results)} fail={len(fail)} expected_fail={len(expected)} skipped={len(skipped)}")
    return 1 if fail else 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Smoke-test KRPC.MechJeb service against a live KSP+kRPC session.")
    parser.add_argument("--name", default="mechjeb-smoke", help="kRPC client name")
    parser.add_argument("--address", default="127.0.0.1", help="kRPC RPC server address")
    parser.add_argument("--rpc-port", type=int, default=50000, help="kRPC RPC port")
    parser.add_argument("--stream-port", type=int, default=50001, help="kRPC stream port")
    parser.add_argument("--make-nodes", action="store_true", help="Call operation.make_nodes() for all maneuver operations")
    args = parser.parse_args()

    results: List[CheckResult] = []

    try:
        conn = krpc.connect(
            name=args.name,
            address=args.address,
            rpc_port=args.rpc_port,
            stream_port=args.stream_port,
        )
    except Exception as ex:  # pylint: disable=broad-except
        print(f"[FAIL] connect: {ex}")
        return 2

    try:
        mj = probe_basic_service(conn, results)
        if mj is not None:
            probe_modules(mj, results)
            probe_ascent_compat(mj, results)
            probe_operations(conn, mj, results, args.make_nodes)
    except Exception:  # pylint: disable=broad-except
        print("[FAIL] unhandled exception during smoke test")
        traceback.print_exc()
        return 3
    finally:
        conn.close()

    return summarize(results)


if __name__ == "__main__":
    sys.exit(main())
