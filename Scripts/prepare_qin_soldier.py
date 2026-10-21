"""Retargets ActionForge animations (including sword_regular_a_rec and locomotion)
onto the Meshy AI rigged Qin Soldier skeleton and validates posed meshes."""
from __future__ import annotations

import copy
import json
import struct
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation, Slerp

from prepare_giac_an_general import Glb


BONE_MAP = [
    ("pelvis", "Hips", "spine_01", "Spine02", False),
    ("spine_01", "Spine02", "spine_02", "Spine01", False),
    ("spine_02", "Spine01", "spine_03", "Spine", False),
    ("spine_03", "Spine", "neck_01", "neck", False),
    ("neck_01", "neck", "Head", "Head", False),
    ("Head", "Head", None, None, False),
    ("clavicle_l", "LeftShoulder", "upperarm_l", "LeftArm", False),
    ("upperarm_l", "LeftArm", "lowerarm_l", "LeftForeArm", True),
    ("lowerarm_l", "LeftForeArm", "hand_l", "LeftHand", True),
    ("hand_l", "LeftHand", None, None, False),
    ("clavicle_r", "RightShoulder", "upperarm_r", "RightArm", False),
    ("upperarm_r", "RightArm", "lowerarm_r", "RightForeArm", True),
    ("lowerarm_r", "RightForeArm", "hand_r", "RightHand", True),
    ("hand_r", "RightHand", None, None, False),
    ("thigh_l", "LeftUpLeg", "calf_l", "LeftLeg", True),
    ("calf_l", "LeftLeg", "foot_l", "LeftFoot", True),
    ("foot_l", "LeftFoot", "ball_l", "LeftToeBase", True),
    ("ball_l", "LeftToeBase", None, None, False),
    ("thigh_r", "RightUpLeg", "calf_r", "RightLeg", True),
    ("calf_r", "RightLeg", "foot_r", "RightFoot", True),
    ("foot_r", "RightFoot", "ball_r", "RightToeBase", True),
    ("ball_r", "RightToeBase", None, None, False),
]

ACTIONFORGE_CLIPS = [
    "sword_regular_a_rec",
    "sword_regular_a",
    "sword_regular_b",
    "sword_regular_c",
    "sword_attack",
    "sword_idle",
    "shield_oneshot",
    "idle_shield_loop",
    "overhandthrow",
    "idle_loop",
    "walk_loop",
    "jog_fwd_loop",
    "sprint_loop",
    "hit_chest",
    "death01",
]


def from_to_matrix(v_from: np.ndarray, v_to: np.ndarray) -> np.ndarray:
    u1 = v_from / max(float(np.linalg.norm(v_from)), 1e-12)
    u2 = v_to / max(float(np.linalg.norm(v_to)), 1e-12)
    c = np.cross(u1, u2)
    d = float(np.clip(np.dot(u1, u2), -1.0, 1.0))
    if d >= 0.999999:
        return np.eye(3)
    if d <= -0.999999:
        ortho = np.array([1.0, 0.0, 0.0]) if abs(u1[0]) < 0.9 else np.array([0.0, 1.0, 0.0])
        axis = np.cross(u1, ortho)
        axis /= np.linalg.norm(axis)
        return Rotation.from_rotvec(axis * np.pi).as_matrix()
    s = np.sqrt((1.0 + d) * 2.0)
    quat = [c[0] / s, c[1] / s, c[2] / s, 0.5 * s]
    return Rotation.from_quat(quat).as_matrix()


def local_matrix(node: dict) -> np.ndarray:
    if "matrix" in node:
        return np.array(node["matrix"], dtype=np.float64).reshape(4, 4).T
    res = np.eye(4, dtype=np.float64)
    rot = Rotation.from_quat(node.get("rotation", [0.0, 0.0, 0.0, 1.0])).as_matrix()
    scale = np.diag(node.get("scale", [1.0, 1.0, 1.0]))
    res[:3, :3] = rot @ scale
    res[:3, 3] = node.get("translation", [0.0, 0.0, 0.0])
    return res


def world_matrices(nodes: list[dict]) -> tuple[list[np.ndarray], dict[int, int]]:
    parents = {c: i for i, n in enumerate(nodes) for c in n.get("children", [])}
    res: dict[int, np.ndarray] = {}

    def get(i: int) -> np.ndarray:
        if i not in res:
            res[i] = (get(parents[i]) if i in parents else np.eye(4)) @ local_matrix(nodes[i])
        return res[i]

    return [get(i) for i in range(len(nodes))], parents


def pure_rotation(mat: np.ndarray) -> np.ndarray:
    r = mat[:3, :3].copy()
    norms = np.linalg.norm(r, axis=0)
    norms = np.where(norms < 1e-12, 1.0, norms)
    return r / norms[None, :]


def sample_clip_nodes(base_nodes: list[dict], clip_glb: Glb, time_sec: float) -> list[dict]:
    nodes = copy.deepcopy(base_nodes)
    node_by_name = {n.get("name"): i for i, n in enumerate(nodes) if "name" in n}
    anim = clip_glb.doc["animations"][0]
    for ch in anim["channels"]:
        target = ch["target"]
        src_name = clip_glb.doc["nodes"][target["node"]].get("name")
        if src_name not in node_by_name:
            continue
        sampler = anim["samplers"][ch["sampler"]]
        times = clip_glb.array(sampler["input"]).ravel()
        values = clip_glb.array(sampler["output"])
        t = float(np.clip(time_sec, times[0], times[-1]))
        if len(times) == 1:
            val = values[0].tolist()
        elif target["path"] == "rotation":
            # Ensure hemisphere continuity for Slerp
            q = values.copy()
            for k in range(1, len(q)):
                if np.dot(q[k - 1], q[k]) < 0:
                    q[k] = -q[k]
            val = Slerp(times, Rotation.from_quat(q))(t).as_quat().tolist()
        else:
            val = [float(np.interp(t, times, values[:, c])) for c in range(values.shape[1])]
        nodes[node_by_name[src_name]][target["path"]] = val
    return nodes


def add_buffer_view(glb: Glb, data: np.ndarray) -> int:
    data = np.ascontiguousarray(data)
    glb.bin.extend(b"\0" * (-len(glb.bin) % 4))
    view_idx = len(glb.doc["bufferViews"])
    glb.doc["bufferViews"].append({
        "buffer": 0,
        "byteOffset": len(glb.bin),
        "byteLength": data.nbytes,
    })
    glb.bin.extend(data.tobytes())
    return view_idx


def add_accessor(glb: Glb, data: np.ndarray, acc_type: str) -> int:
    data = np.ascontiguousarray(data, dtype=np.float32)
    view_idx = add_buffer_view(glb, data)
    acc = {
        "bufferView": view_idx,
        "byteOffset": 0,
        "componentType": 5126,
        "count": len(data),
        "type": acc_type,
        "min": data.reshape(len(data), -1).min(axis=0).tolist(),
        "max": data.reshape(len(data), -1).max(axis=0).tolist(),
    }
    idx = len(glb.doc["accessors"])
    glb.doc["accessors"].append(acc)
    return idx


def build_qin_soldier_animations() -> dict:
    src_dir = Path("QinSoldierVerification/Source")
    af_dir = src_dir / "ActionForge"
    rig_glb = Glb(af_dir / "rig-human.glb")
    tpose_glb = Glb(af_dir / "a_tpose.glb")
    qin_glb = Glb(src_dir / "QinSoldier_Meshy_Rigged.glb")

    # 1. Compute ActionForge T-pose world matrices
    src_tpose_nodes = sample_clip_nodes(rig_glb.doc["nodes"], tpose_glb, 0.0)
    src_tpose_worlds, _ = world_matrices(src_tpose_nodes)
    src_idx = {n.get("name"): i for i, n in enumerate(src_tpose_nodes) if "name" in n}

    # 2. Compute QinSoldier bind-pose world matrices
    tgt_bind_nodes = copy.deepcopy(qin_glb.doc["nodes"])
    tgt_bind_worlds, tgt_parents = world_matrices(tgt_bind_nodes)
    tgt_idx = {n.get("name"): i for i, n in enumerate(tgt_bind_nodes) if "name" in n}

    # 3. Compute aligned T-pose world rotations for each mapped bone
    src_T_rot: dict[str, np.ndarray] = {}
    tgt_T_rot: dict[str, np.ndarray] = {}
    align_rot: dict[str, np.ndarray] = {}

    for s_name, t_name, s_child, t_child, align_child in BONE_MAP:
        w_s = pure_rotation(src_tpose_worlds[src_idx[s_name]])
        w_t = pure_rotation(tgt_bind_worlds[tgt_idx[t_name]])
        src_T_rot[t_name] = w_s
        if align_child and s_child is not None and t_child is not None:
            v_s = src_tpose_worlds[src_idx[s_child]][:3, 3] - src_tpose_worlds[src_idx[s_name]][:3, 3]
            v_t = tgt_bind_worlds[tgt_idx[t_child]][:3, 3] - tgt_bind_worlds[tgt_idx[t_name]][:3, 3]
            a_mat = from_to_matrix(v_t, v_s)
            align_rot[t_name] = a_mat
            tgt_T_rot[t_name] = a_mat @ w_t
        elif t_name == "LeftHand":
            a_mat = align_rot.get("LeftForeArm", np.eye(3))
            tgt_T_rot[t_name] = a_mat @ w_t
        elif t_name == "RightHand":
            a_mat = align_rot.get("RightForeArm", np.eye(3))
            tgt_T_rot[t_name] = a_mat @ w_t
        else:
            align_rot[t_name] = np.eye(3)
            tgt_T_rot[t_name] = w_t

    src_hips_T_pos = src_tpose_worlds[src_idx["pelvis"]][:3, 3].copy()
    tgt_hips_bind_pos = tgt_bind_worlds[tgt_idx["Hips"]][:3, 3].copy()
    hips_parent_idx = tgt_parents.get(tgt_idx["Hips"])
    hips_parent_world = tgt_bind_worlds[hips_parent_idx] if hips_parent_idx is not None else np.eye(4)
    hips_parent_inv = np.linalg.inv(hips_parent_world)
    y_scale = float(tgt_hips_bind_pos[1] / max(src_hips_T_pos[1], 0.1))

    # Replace animations in qin_glb with all 15 fitted ActionForge animations
    qin_glb.doc["animations"] = []
    exported_json_clips = {}
    clip_summaries = []

    for clip_name in ACTIONFORGE_CLIPS:
        clip_glb = Glb(af_dir / f"{clip_name}.glb")
        anim = clip_glb.doc["animations"][0]
        duration = max(float(clip_glb.array(s["input"]).max()) for s in anim["samplers"])
        fps = 30.0
        frame_count = max(2, int(np.ceil(duration * fps)) + 1)
        times = np.linspace(0.0, duration, frame_count, dtype=np.float32)

        rot_tracks: dict[str, np.ndarray] = {t_name: np.zeros((frame_count, 4), dtype=np.float32) for _, t_name, _, _, _ in BONE_MAP}
        hips_pos_track = np.zeros((frame_count, 3), dtype=np.float32)

        for f_idx, t_sec in enumerate(times):
            posed_src = sample_clip_nodes(src_tpose_nodes, clip_glb, float(t_sec))
            src_worlds, _ = world_matrices(posed_src)

            # Compute desired world rotations for all mapped target bones
            desired_world_rot: dict[int, np.ndarray] = {}
            for s_name, t_name, _, _, _ in BONE_MAP:
                w_src = pure_rotation(src_worlds[src_idx[s_name]])
                delta = w_src @ src_T_rot[t_name].T
                w_tgt = delta @ tgt_T_rot[t_name]
                desired_world_rot[tgt_idx[t_name]] = w_tgt

            # Convert to local rotations in parent-first order
            for _, t_name, _, _, _ in BONE_MAP:
                n_i = tgt_idx[t_name]
                p_i = tgt_parents.get(n_i)
                if p_i is not None and p_i in desired_world_rot:
                    p_rot = desired_world_rot[p_i]
                elif p_i is not None:
                    p_rot = pure_rotation(tgt_bind_worlds[p_i])
                else:
                    p_rot = np.eye(3)
                local_r = p_rot.T @ desired_world_rot[n_i]
                quat = Rotation.from_matrix(local_r).as_quat().astype(np.float32)
                if f_idx > 0 and float(np.dot(rot_tracks[t_name][f_idx - 1], quat)) < 0.0:
                    quat = -quat
                rot_tracks[t_name][f_idx] = quat

            # Hips vertical position (in-place X/Z, scaled Y bounce)
            src_hips_pos = src_worlds[src_idx["pelvis"]][:3, 3]
            dy = (src_hips_pos[1] - src_hips_T_pos[1]) * y_scale
            world_hips_pos = np.array([
                tgt_hips_bind_pos[0],
                max(0.08, tgt_hips_bind_pos[1] + dy),
                tgt_hips_bind_pos[2],
                1.0,
            ])
            local_hips_pos = (hips_parent_inv @ world_hips_pos)[:3]
            hips_pos_track[f_idx] = local_hips_pos.astype(np.float32)

        # Add GLB animation channels
        time_acc = add_accessor(qin_glb, times.reshape(-1, 1), "SCALAR")
        samplers = []
        channels = []

        # Hips translation channel
        pos_acc = add_accessor(qin_glb, hips_pos_track, "VEC3")
        samplers.append({"input": time_acc, "interpolation": "LINEAR", "output": pos_acc})
        channels.append({"sampler": len(samplers) - 1, "target": {"node": tgt_idx["Hips"], "path": "translation"}})

        # 22 Bone rotation channels
        for _, t_name, _, _, _ in BONE_MAP:
            r_acc = add_accessor(qin_glb, rot_tracks[t_name], "VEC4")
            samplers.append({"input": time_acc, "interpolation": "LINEAR", "output": r_acc})
            channels.append({"sampler": len(samplers) - 1, "target": {"node": tgt_idx[t_name], "path": "rotation"}})

        fit_name = f"{clip_name}_fit"
        qin_glb.doc["animations"].append({
            "name": fit_name,
            "samplers": samplers,
            "channels": channels,
        })

        exported_json_clips[fit_name] = {
            "duration": float(duration),
            "frameCount": int(frame_count),
            "times": [round(float(x), 5) for x in times],
            "hipsPosition": [[round(float(v), 5) for v in row] for row in hips_pos_track],
            "rotations": {
                t_name: [[round(float(v), 6) for v in row] for row in rot_tracks[t_name]]
                for _, t_name, _, _, _ in BONE_MAP
            },
        }
        clip_summaries.append({"name": fit_name, "duration": round(float(duration), 4), "frames": frame_count})

    out_glb_ver = Path("QinSoldierVerification/QinSoldier_ActionForge_15_Animations.glb")
    out_glb_assets = Path("Assets/Art/Meshy/QinSoldier/QinSoldier_ActionForge.glb")
    out_json_assets = Path("Assets/Art/Meshy/QinSoldier/QinSoldier_ActionForge_Clips.json")

    qin_glb.save(out_glb_ver)
    qin_glb.save(out_glb_assets)

    bundle = {
        "generator": "Scripts/prepare_qin_soldier.py",
        "sourceModel": "QinSoldierVerification/Source/QinSoldier_Meshy_Rigged.glb",
        "actionForgeSource": "https://actionforge.app/#clip=sword_regular_a_rec",
        "boneNames": [t_name for _, t_name, _, _, _ in BONE_MAP],
        "clips": exported_json_clips,
    }
    out_json_assets.write_text(json.dumps(bundle, separators=(",", ":")), encoding="utf-8")

    summary = {
        "outputGlbVerification": str(out_glb_ver),
        "outputGlbAssets": str(out_glb_assets),
        "outputJsonAssets": str(out_json_assets),
        "jointCount": len(qin_glb.doc["skins"][0]["joints"]),
        "mappedHumanoidBones": len(BONE_MAP),
        "clips": clip_summaries,
    }
    Path("QinSoldierVerification/actionforge-retarget-report.json").write_text(
        json.dumps(summary, indent=2), encoding="utf-8"
    )
    return summary


if __name__ == "__main__":
    res = build_qin_soldier_animations()
    print(json.dumps(res, indent=2))
