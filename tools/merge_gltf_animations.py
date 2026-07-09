"""Merge animations from a GLB into a glTF model with an identical named rig."""

from __future__ import annotations

import argparse
import copy
import json
import struct
from pathlib import Path


JSON_CHUNK_TYPE = 0x4E4F534A
BIN_CHUNK_TYPE = 0x004E4942


def _read_glb(path: Path) -> tuple[dict, bytes]:
    with path.open("rb") as stream:
        magic, version, total_length = struct.unpack("<4sII", stream.read(12))
        if magic != b"glTF" or version != 2:
            raise ValueError(f"{path} is not a glTF 2.0 GLB")

        json_length, json_type = struct.unpack("<II", stream.read(8))
        if json_type != JSON_CHUNK_TYPE:
            raise ValueError(f"{path} does not start with a JSON chunk")
        document = json.loads(stream.read(json_length).decode("utf-8").rstrip("\x00 "))

        binary_length, binary_type = struct.unpack("<II", stream.read(8))
        if binary_type != BIN_CHUNK_TYPE:
            raise ValueError(f"{path} does not contain a BIN chunk")
        binary = stream.read(binary_length)
        if stream.tell() != total_length:
            raise ValueError(f"{path} has an unexpected GLB length")
        return document, binary


def _named_nodes(document: dict, source: str) -> dict[str, int]:
    result: dict[str, int] = {}
    for index, node in enumerate(document.get("nodes", [])):
        name = node.get("name")
        if not name:
            continue
        if name in result:
            raise ValueError(f"Duplicate node name {name!r} in {source}")
        result[name] = index
    return result


def _offset_accessor(accessor: dict, buffer_view_offset: int) -> dict:
    shifted = copy.deepcopy(accessor)
    if "bufferView" in shifted:
        shifted["bufferView"] += buffer_view_offset
    sparse = shifted.get("sparse")
    if sparse:
        sparse["indices"]["bufferView"] += buffer_view_offset
        sparse["values"]["bufferView"] += buffer_view_offset
    return shifted


def merge_gltf_animations(
    model_path: Path | str,
    animations_path: Path | str,
    output_path: Path | str,
    required_animations: tuple[str, ...] = (),
) -> list[str]:
    model_path = Path(model_path)
    animations_path = Path(animations_path)
    output_path = Path(output_path)

    model = json.loads(model_path.read_text(encoding="utf-8"))
    donor, donor_binary = _read_glb(animations_path)

    model_buffers = model.get("buffers", [])
    donor_buffers = donor.get("buffers", [])
    if len(model_buffers) != 1 or len(donor_buffers) != 1:
        raise ValueError("The model and animation donor must each use exactly one buffer")
    model_uri = model_buffers[0].get("uri")
    if not model_uri or model_uri.startswith("data:"):
        raise ValueError("The model must use one external binary buffer")

    model_binary = (model_path.parent / model_uri).read_bytes()
    model_binary += b"\0" * (-len(model_binary) % 4)
    donor_offset = len(model_binary)
    merged_binary = model_binary + donor_binary

    model_nodes = _named_nodes(model, str(model_path))
    donor_nodes = donor.get("nodes", [])
    buffer_view_offset = len(model.get("bufferViews", []))
    accessor_offset = len(model.get("accessors", []))

    merged_views = model.setdefault("bufferViews", [])
    for view in donor.get("bufferViews", []):
        shifted = copy.deepcopy(view)
        if shifted.get("buffer", 0) != 0:
            raise ValueError("Animation donor buffer views must reference buffer 0")
        shifted["buffer"] = 0
        shifted["byteOffset"] = donor_offset + shifted.get("byteOffset", 0)
        merged_views.append(shifted)

    merged_accessors = model.setdefault("accessors", [])
    merged_accessors.extend(
        _offset_accessor(accessor, buffer_view_offset)
        for accessor in donor.get("accessors", [])
    )

    merged_animations = model.setdefault("animations", [])
    animation_names: list[str] = []
    for donor_animation in donor.get("animations", []):
        animation = copy.deepcopy(donor_animation)
        name = animation.get("name", "")
        animation_names.append(name)
        for sampler in animation.get("samplers", []):
            sampler["input"] += accessor_offset
            sampler["output"] += accessor_offset
        for channel in animation.get("channels", []):
            donor_node_index = channel["target"].get("node")
            if donor_node_index is None:
                continue
            donor_name = donor_nodes[donor_node_index].get("name")
            if donor_name not in model_nodes:
                raise ValueError(f"Animation target {donor_name!r} is missing from the model")
            channel["target"]["node"] = model_nodes[donor_name]
        merged_animations.append(animation)

    missing_animations = sorted(set(required_animations) - set(animation_names))
    if missing_animations:
        raise ValueError(f"Required animations are missing: {', '.join(missing_animations)}")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_binary_path = output_path.with_suffix(".bin")
    model["buffers"] = [{"byteLength": len(merged_binary), "uri": output_binary_path.name}]
    output_path.write_text(
        json.dumps(model, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    output_binary_path.write_bytes(merged_binary)
    return animation_names


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("model", type=Path)
    parser.add_argument("animations", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--require-animation", action="append", default=[])
    args = parser.parse_args()
    names = merge_gltf_animations(
        args.model,
        args.animations,
        args.output,
        tuple(args.require_animation),
    )
    print(f"Merged {len(names)} animations into {args.output}")


if __name__ == "__main__":
    main()
