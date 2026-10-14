"""Prepare supplied Meshy GLBs without touching source files.

Python 3.12: numpy, scipy, Pillow, trimesh, fast-simplification.
Example: python Scripts/prepare_meshy_assets.py --source-root E:/
Dependencies can be installed outside the repository or under ignored Temp;
set PYTHONPATH to that directory. All images remain embedded in the GLB.
"""
from __future__ import annotations

import argparse
import copy
import io
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.spatial import cKDTree
from scipy.spatial.transform import Rotation, Slerp
import fast_simplification
import trimesh

DTYPES = {5120: np.int8, 5121: np.uint8, 5122: np.int16,
          5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTHS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class GLB:
    def __init__(self, path):
        raw = Path(path).read_bytes()
        magic, version, size = struct.unpack_from("<III", raw)
        assert magic == 0x46546C67 and version == 2 and size == len(raw)
        length, kind = struct.unpack_from("<II", raw, 12)
        assert kind == 0x4E4F534A
        self.doc = json.loads(raw[20:20 + length])
        offset = 20 + length
        blen, bkind = struct.unpack_from("<II", raw, offset)
        assert bkind == 0x004E4942
        self.bin = bytearray(raw[offset + 8:offset + 8 + blen])

    def array(self, index):
        acc = self.doc["accessors"][index]
        view = self.doc["bufferViews"][acc["bufferView"]]
        dtype = np.dtype(DTYPES[acc["componentType"]]).newbyteorder("<")
        width = WIDTHS[acc["type"]]
        stride = view.get("byteStride", width * dtype.itemsize)
        start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
        return np.ndarray((acc["count"], width), dtype=dtype, buffer=self.bin,
                          offset=start, strides=(stride, dtype.itemsize)).copy()

    def bytes_view(self, index):
        view = self.doc["bufferViews"][index]
        start = view.get("byteOffset", 0)
        return bytes(self.bin[start:start + view["byteLength"]])

    def add_view(self, blob):
        self.bin.extend(b"\0" * (-len(self.bin) % 4))
        index = len(self.doc.setdefault("bufferViews", []))
        self.doc["bufferViews"].append({"buffer": 0, "byteOffset": len(self.bin),
                                        "byteLength": len(blob)})
        self.bin.extend(blob)
        return index

    def set_array(self, index, values, kind, component=5126):
        values = np.ascontiguousarray(values, dtype=DTYPES[component])
        item = {"bufferView": self.add_view(values.tobytes()), "componentType": component,
                "count": len(values), "type": kind}
        if kind != "MAT4":
            item["min"] = values.reshape(len(values), -1).min(axis=0).tolist()
            item["max"] = values.reshape(len(values), -1).max(axis=0).tolist()
        if index is None:
            index = len(self.doc.setdefault("accessors", []))
            self.doc["accessors"].append(item)
        else:
            self.doc["accessors"][index] = item
        return index

    def resize_images(self, max_size=2048):
        report = []
        for image in self.doc.get("images", []):
            source = Image.open(io.BytesIO(self.bytes_view(image["bufferView"])))
            before = source.size
            source.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
            output = io.BytesIO()
            if image["mimeType"] == "image/jpeg":
                source.convert("RGB").save(output, "JPEG", quality=94, subsampling=0)
            else:
                source.save(output, "PNG", optimize=True)
            image["bufferView"] = self.add_view(output.getvalue())
            report.append({"before": before, "after": source.size})
        return report

    def save(self, path):
        # Repack only referenced views; discarded high-poly arrays are removed.
        references = [a for a in self.doc.get("accessors", []) if "bufferView" in a]
        references += self.doc.get("images", [])
        old_views = self.doc["bufferViews"]
        mapping, packed, views = {}, bytearray(), []
        for ref in references:
            old = ref["bufferView"]
            if old not in mapping:
                packed.extend(b"\0" * (-len(packed) % 4))
                view = copy.deepcopy(old_views[old])
                data = self.bytes_view(old)
                view.update(buffer=0, byteOffset=len(packed))
                mapping[old] = len(views)
                views.append(view)
                packed.extend(data)
            ref["bufferView"] = mapping[old]
        self.doc["bufferViews"] = views
        self.doc["buffers"] = [{"byteLength": len(packed)}]
        self.doc["asset"]["generator"] = "Thanh Giong Meshy preparation pipeline"
        encoded = json.dumps(self.doc, separators=(",", ":")).encode()
        encoded += b" " * (-len(encoded) % 4)
        packed.extend(b"\0" * (-len(packed) % 4))
        total = 12 + 8 + len(encoded) + 8 + len(packed)
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        Path(path).write_bytes(struct.pack("<III", 0x46546C67, 2, total) +
                              struct.pack("<II", len(encoded), 0x4E4F534A) + encoded +
                              struct.pack("<II", len(packed), 0x004E4942) + packed)


def local_matrix(node):
    if "matrix" in node:
        return np.array(node["matrix"]).reshape(4, 4).T
    result = np.eye(4)
    result[:3, :3] = Rotation.from_quat(node.get("rotation", [0, 0, 0, 1])).as_matrix() @ np.diag(node.get("scale", [1, 1, 1]))
    result[:3, 3] = node.get("translation", [0, 0, 0])
    return result


def world_matrices(nodes):
    parents = {child: i for i, node in enumerate(nodes) for child in node.get("children", [])}
    result = {}
    def get(i):
        if i not in result:
            result[i] = (get(parents[i]) if i in parents else np.eye(4)) @ local_matrix(nodes[i])
        return result[i]
    return [get(i) for i in range(len(nodes))], parents


def prepare_hero(source, output):
    glb = GLB(source)
    doc = glb.doc
    skin = doc["skins"][0]
    primitive = doc["meshes"][0]["primitives"][0]
    attrs = primitive["attributes"]
    positions = glb.array(attrs["POSITION"])
    inverse_bind = glb.array(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
    nodes = doc["nodes"]
    worlds, parents = world_matrices(nodes)
    bind_world = {n: np.linalg.inv(inverse_bind[i]) for i, n in enumerate(skin["joints"])}
    original_rest_error = max(float(np.max(np.abs(worlds[n] @ inverse_bind[i] - np.eye(4))))
                              for i, n in enumerate(skin["joints"]))
    # Normalize source cm joints + .01 root into meter joints + unit root.
    # The source inverse binds carry a compensating 100x scale; remove it while
    # retaining each joint's world position and rotation.
    for matrix in bind_world.values():
        matrix[:3, :3] /= np.linalg.norm(matrix[:3, :3], axis=0)[None, :]
    joint_set = set(skin["joints"])
    for i, node in enumerate(nodes):
        if i not in joint_set and any(c in joint_set for c in node.get("children", [])):
            node["scale"] = [1, 1, 1]
    normalized_worlds, _ = world_matrices(nodes)
    # Reconstruct the source bind pose as TRS, preserving animated node indices.
    for n in skin["joints"]:
        parent = parents.get(n)
        parent_world = bind_world.get(parent, normalized_worlds[parent] if parent is not None else np.eye(4))
        local = np.linalg.inv(parent_world) @ bind_world[n]
        scale = np.linalg.norm(local[:3, :3], axis=0)
        rot = local[:3, :3] / scale[None, :]
        nodes[n].pop("matrix", None)
        nodes[n]["translation"] = local[:3, 3].tolist()
        nodes[n]["rotation"] = Rotation.from_matrix(rot).as_quat().tolist()
        nodes[n]["scale"] = scale.tolist()
    names = {node.get("name"): i for i, node in enumerate(nodes)}
    forearm = names["mixamorig:RightForeArm"]
    left_hand = names["mixamorig:LeftHand"]
    length = float(np.linalg.norm(nodes[left_hand]["translation"]))
    right_hand = len(nodes)
    nodes.append({"name": "mixamorig:RightHand", "translation": [0, length, 0],
                  "rotation": [0, 0, 0, 1], "scale": [1, 1, 1]})
    nodes[forearm].setdefault("children", []).append(right_hand)
    hand_world = bind_world[forearm] @ local_matrix(nodes[right_hand])
    new_index = len(skin["joints"])
    skin["joints"].append(right_hand)
    normalized_inverse_bind = np.stack([np.linalg.inv(bind_world[n]) for n in skin["joints"][:-1]])
    expanded = np.concatenate([normalized_inverse_bind, np.linalg.inv(hand_world)[None]], axis=0)
    glb.set_array(skin["inverseBindMatrices"], expanded.transpose(0, 2, 1).reshape(-1, 16), "MAT4")
    joints = glb.array(attrs["JOINTS_0"])
    weights = glb.array(attrs["WEIGHTS_0"])
    forearm_index = skin["joints"].index(forearm)
    bone_local = (inverse_bind[forearm_index] @ np.column_stack([positions, np.ones(len(positions))]).T).T
    # Transfer distal hand skin, with a 3cm wrist blend. Existing hand animation
    # is preserved by inheriting the forearm; future clips may animate this joint.
    fraction = np.clip((bone_local[:, 1] - (length * 100 - 2.5)) / 3.0, 0, 1)
    affected = 0
    for v in np.flatnonzero((fraction > 0) & np.any((joints == forearm_index) & (weights > 0), axis=1)):
        slot = int(np.argmax((joints[v] == forearm_index) * weights[v]))
        amount = float(weights[v, slot] * fraction[v])
        if fraction[v] >= .999:
            joints[v, slot] = new_index
        else:
            free = int(np.argmin(weights[v]))
            if free == slot:
                joints[v, slot] = new_index
            else:
                weights[v, slot] -= amount
                joints[v, free] = new_index
                weights[v, free] = amount
                weights[v] /= weights[v].sum()
        affected += 1
    glb.set_array(attrs["JOINTS_0"], joints, "VEC4", 5123)
    glb.set_array(attrs["WEIGHTS_0"], weights, "VEC4")
    scaled_accessors = set()
    for animation in doc.get("animations", []):
        for channel in animation["channels"]:
            if channel["target"]["path"] == "translation" and channel["target"]["node"] in joint_set:
                index = animation["samplers"][channel["sampler"]]["output"]
                if index not in scaled_accessors:
                    glb.set_array(index, glb.array(index) * .01, "VEC3")
                    scaled_accessors.add(index)
    textures = glb.resize_images()
    glb.save(output)
    return {"source": str(source), "output": str(output), "vertices": len(positions),
            "triangles": doc["accessors"][primitive["indices"]]["count"] // 3,
            "mesh_bounds_m": [positions.min(axis=0).tolist(), positions.max(axis=0).tolist()],
            "height_m": float(np.ptp(positions[:, 1])), "joints": len(skin["joints"]),
            "added_joint": "mixamorig:RightHand", "wrist_blend_vertices": affected,
            "wrist_world_m": hand_world[:3, 3].tolist(), "original_rest_bind_error": original_rest_error,
            "skeleton_units": "meters; target_character rootScale1; animation translations multiplied by0.01",
            "animations": [a.get("name") for a in doc.get("animations", [])],
            "images": textures, "bytes": Path(output).stat().st_size}


def project_attributes(vertices, source_vertices, source_faces, uv, normals):
    # Find original surface triangles, then interpolate UVs/normals on their
    # closest point. This retains texture islands better than nearest-vertex UV.
    centroids = source_vertices[source_faces].mean(axis=1)
    tree = cKDTree(centroids)
    out_uv, out_normals, errors = [], [], []
    for start in range(0, len(vertices), 2048):
        points = vertices[start:start + 2048]
        _, candidates = tree.query(points, k=min(16, len(source_faces)), workers=-1)
        triangles = source_vertices[source_faces[candidates]]
        repeated = np.repeat(points, candidates.shape[1], axis=0)
        nearest = trimesh.triangles.closest_point(triangles.reshape(-1, 3, 3), repeated)
        distances = np.linalg.norm(nearest - repeated, axis=1).reshape(len(points), -1)
        best = distances.argmin(axis=1)
        faces = source_faces[candidates[np.arange(len(points)), best]]
        chosen = nearest.reshape(len(points), -1, 3)[np.arange(len(points)), best]
        bary = trimesh.triangles.points_to_barycentric(source_vertices[faces], chosen)
        out_uv.append((uv[faces] * bary[:, :, None]).sum(axis=1))
        out_normals.append((normals[faces] * bary[:, :, None]).sum(axis=1))
        errors.extend(distances[np.arange(len(points)), best].tolist())
    result_normals = np.concatenate(out_normals)
    result_normals /= np.maximum(np.linalg.norm(result_normals, axis=1)[:, None], 1e-12)
    return np.concatenate(out_uv), result_normals, float(max(errors)), float(np.mean(errors))


def horse_limb_estimates(vertices):
    high = vertices[vertices[:, 1] > .35]
    head_z_sign = 1 if np.median(high[:, 2]) > 0 else -1
    anchors = {}
    for xsign in [-1, 1]:
        for zsign in [-1, 1]:
            leg = vertices[(vertices[:, 1] < -.35) & (vertices[:, 0] * xsign > 0)
                           & (vertices[:, 2] * zsign > .12)]
            cutoff = np.quantile(leg[:, 1], .1)
            lowest = leg[leg[:, 1] <= cutoff]
            hoof = lowest.mean(axis=0)
            slices = {}
            for height in [-.5, -.35, -.2, -.05]:
                # Exclude the low tail and belly by following each hoof's Z.
                region = vertices[(abs(vertices[:, 1] - height) < .035)
                                  & (vertices[:, 0] * xsign > .03)
                                  & (abs(vertices[:, 2] - hoof[2]) < .25)]
                slices[str(height)] = np.median(region, axis=0).tolist() if len(region) else None
            name = ("front" if zsign == head_z_sign else "hind") + ("_negative_x" if xsign < 0 else "_positive_x")
            anchors[name] = {"low_vertex_count": len(leg), "lowest10pct_center": hoof.tolist(),
                             "min_y": float(leg[:, 1].min()), "y_slices": slices}
    return {"head_mass_z_sign": head_z_sign, "high_head_region_median": np.median(high, axis=0).tolist(),
            "limbs": anchors, "note": "Source has an asymmetrical posed foreleg. These geometry estimates are rig starting points, not authored skeleton joints."}


def prepare_static(source, output, target):
    glb = GLB(source)
    primitive = glb.doc["meshes"][0]["primitives"][0]
    attrs = primitive["attributes"]
    vertices = glb.array(attrs["POSITION"]).astype(np.float64)
    faces = glb.array(primitive["indices"]).reshape(-1, 3).astype(np.int32)
    uv = glb.array(attrs["TEXCOORD_0"])
    normals = glb.array(attrs["NORMAL"])
    print(f"Simplifying {Path(source).name}: {len(faces):,} -> {target:,} triangles", flush=True)
    reduced_vertices, reduced_faces = fast_simplification.simplify(vertices, faces, target_count=target, agg=7)
    reduced_uv, reduced_normals, max_error, mean_error = project_attributes(reduced_vertices, vertices, faces, uv, normals)
    glb.set_array(attrs["POSITION"], reduced_vertices, "VEC3")
    glb.set_array(attrs["NORMAL"], reduced_normals, "VEC3")
    glb.set_array(attrs["TEXCOORD_0"], reduced_uv, "VEC2")
    glb.set_array(primitive["indices"], reduced_faces.reshape(-1, 1), "SCALAR", 5125)
    textures = glb.resize_images()
    glb.save(output)
    report = {"source": str(source), "output": str(output), "source_triangles": len(faces),
            "triangles": len(reduced_faces), "vertices": len(reduced_vertices),
            "bounds": [reduced_vertices.min(axis=0).tolist(), reduced_vertices.max(axis=0).tolist()],
            "surface_projection_error_max": max_error, "surface_projection_error_mean": mean_error,
            "images": textures, "bytes": Path(output).stat().st_size,
            "rigged": False, "uv_method": "nearest original surface triangle barycentric interpolation"}
    if "iron_horse" in str(source):
        report["horse_limb_estimates_source_coordinates"] = horse_limb_estimates(vertices)
    return report


def validate(path):
    glb = GLB(path)
    for index, acc in enumerate(glb.doc.get("accessors", [])):
        values = glb.array(index)
        assert np.isfinite(values).all(), (path, index)
        assert len(values) == acc["count"]
    for mesh in glb.doc["meshes"]:
        for primitive in mesh["primitives"]:
            count = glb.doc["accessors"][primitive["attributes"]["POSITION"]]["count"]
            assert glb.array(primitive["indices"]).max() < count
    for skin in glb.doc.get("skins", []):
        assert len(skin["joints"]) == glb.doc["accessors"][skin["inverseBindMatrices"]]["count"]
    for node in glb.doc.get("nodes", []):
        assert all(i < len(glb.doc["nodes"]) for i in node.get("children", []))
    return "GLB binary/accessors/finite values/indices/skin hierarchy verified"


def skinned_positions(glb, animation_index=None, time=0):
    nodes = copy.deepcopy(glb.doc["nodes"])
    if animation_index is not None:
        animation = glb.doc["animations"][animation_index]
        for channel in animation["channels"]:
            sampler = animation["samplers"][channel["sampler"]]
            assert sampler.get("interpolation", "LINEAR") == "LINEAR"
            times = glb.array(sampler["input"]).ravel()
            values = glb.array(sampler["output"])
            frame_time = np.clip(time, times[0], times[-1])
            target = channel["target"]
            if len(times) == 1:
                value = values[0]
            elif target["path"] == "rotation":
                value = Slerp(times, Rotation.from_quat(values))(frame_time).as_quat()
            else:
                value = [np.interp(frame_time, times, values[:, c]) for c in range(values.shape[1])]
            nodes[target["node"]][target["path"]] = list(value)
    worlds, _ = world_matrices(nodes)
    skin = glb.doc["skins"][0]
    ibm = glb.array(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
    matrices = np.stack([worlds[n] @ ibm[i] for i, n in enumerate(skin["joints"])])
    attrs = glb.doc["meshes"][0]["primitives"][0]["attributes"]
    positions = glb.array(attrs["POSITION"])
    joints, weights = glb.array(attrs["JOINTS_0"]), glb.array(attrs["WEIGHTS_0"])
    homogeneous = np.column_stack([positions, np.ones(len(positions))])
    result = np.zeros((len(positions), 4))
    for i in range(4):
        result += np.einsum("nij,nj->ni", matrices[joints[:, i]], homogeneous) * weights[:, i, None]
    return result[:, :3]


def compare_hero_animation(source_path, output_path):
    source, output = GLB(source_path), GLB(output_path)
    comparisons = {}
    rest_diff = np.linalg.norm(skinned_positions(source) - skinned_positions(output), axis=1)
    comparisons["rest_max_vertex_delta_m"] = float(rest_diff.max())
    for index, animation in enumerate(source.doc["animations"]):
        duration = max(float(source.array(s["input"]).max()) for s in animation["samplers"])
        deltas = []
        for t in np.linspace(0, duration, 5):
            diff = np.linalg.norm(skinned_positions(source, index, t) - skinned_positions(output, index, t), axis=1)
            deltas.append(float(diff.max()))
        comparisons[animation.get("name", str(index))] = {"duration_seconds": duration,
            "sample_count": 5, "max_vertex_delta_m": max(deltas)}
        assert max(deltas) < .0001, (animation.get("name"), deltas)
    assert rest_diff.max() < .0001
    return comparisons


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", default="E:/")
    parser.add_argument("--project-root", default=str(Path(__file__).resolve().parents[1]))
    args = parser.parse_args()
    source_root, project = Path(args.source_root), Path(args.project_root)
    output = project / "Assets/Art/Meshy"
    report = {
        "hero": prepare_hero(source_root / "Meshy_AI_thanh_giong_rigged_All_Animations.glb", output / "ThanhGiong_Meshy.glb"),
        "horse": prepare_static(source_root / "Meshy_AI_iron_horse_1013164946_image-to-3d-texture.glb", output / "IronHorse_Meshy.glb", 55000),
        "spear": prepare_static(source_root / "Meshy_AI_bamboo_spear_1013165004_image-to-3d-texture.glb", output / "BambooSpear_Meshy.glb", 6000),
    }
    for item in report.values():
        item["validation"] = validate(item["output"])
    report["hero"]["animation_equivalence"] = compare_hero_animation(report["hero"]["source"], report["hero"]["output"])
    report["constraints"] = ["Horse and spear are static Meshy models; neither contains bones or animation.",
        "Hero original Walking and Running timings/rotations are retained; translations are converted from centimeters to meters. Added RightHand inherits forearm movement until an imported clip targets it.",
        "UVs are reprojected to the dense source surface after decimation; inspect tiny decorative edges in Unity.",
        "No source files were modified. Textures remain embedded, capped at 2048px."]
    report_path = project / "MeshyVerification/asset-preparation.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2), flush=True)


if __name__ == "__main__":
    main()
