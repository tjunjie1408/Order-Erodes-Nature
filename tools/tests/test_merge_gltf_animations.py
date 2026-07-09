import json
import struct
import tempfile
import unittest
from pathlib import Path

from tools.merge_gltf_animations import merge_gltf_animations


def write_glb(path: Path, document: dict, binary: bytes) -> None:
    json_bytes = json.dumps(document, separators=(",", ":")).encode("utf-8")
    json_bytes += b" " * (-len(json_bytes) % 4)
    binary += b"\0" * (-len(binary) % 4)
    total_length = 12 + 8 + len(json_bytes) + 8 + len(binary)
    with path.open("wb") as stream:
        stream.write(struct.pack("<4sII", b"glTF", 2, total_length))
        stream.write(struct.pack("<II", len(json_bytes), 0x4E4F534A))
        stream.write(json_bytes)
        stream.write(struct.pack("<II", len(binary), 0x004E4942))
        stream.write(binary)


class MergeGltfAnimationsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.model_path = self.root / "model.gltf"
        self.model_bin_path = self.root / "model.bin"
        self.animation_path = self.root / "animations.glb"
        self.output_path = self.root / "merged.gltf"

        model = {
            "asset": {"version": "2.0"},
            "scene": 0,
            "scenes": [{"nodes": [0]}],
            "nodes": [{"name": "Root", "children": [1]}, {"name": "Bone"}],
            "buffers": [{"uri": "model.bin", "byteLength": 4}],
            "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 4}],
            "accessors": [{"bufferView": 0, "componentType": 5126, "count": 1, "type": "SCALAR"}],
        }
        self.model_path.write_text(json.dumps(model), encoding="utf-8")
        self.model_bin_path.write_bytes(b"MODL")

        donor = {
            "asset": {"version": "2.0"},
            "nodes": [{"name": "Bone"}],
            "buffers": [{"byteLength": 4}],
            "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 4}],
            "accessors": [{"bufferView": 0, "componentType": 5126, "count": 1, "type": "SCALAR"}],
            "animations": [
                {
                    "name": "Idle_Loop",
                    "samplers": [{"input": 0, "output": 0, "interpolation": "LINEAR"}],
                    "channels": [{"sampler": 0, "target": {"node": 0, "path": "rotation"}}],
                }
            ],
        }
        write_glb(self.animation_path, donor, b"ANIM")

    def tearDown(self) -> None:
        self.temp_dir.cleanup()

    def test_merge_maps_tracks_offsets_binary_and_is_deterministic(self) -> None:
        names = merge_gltf_animations(self.model_path, self.animation_path, self.output_path)

        merged = json.loads(self.output_path.read_text(encoding="utf-8"))
        self.assertEqual(["Idle_Loop"], names)
        self.assertEqual(1, merged["animations"][0]["channels"][0]["target"]["node"])
        self.assertEqual(4, merged["bufferViews"][1]["byteOffset"])
        self.assertEqual(1, merged["animations"][0]["samplers"][0]["input"])
        self.assertEqual("merged.bin", merged["buffers"][0]["uri"])
        self.assertEqual(b"MODLANIM", self.output_path.with_suffix(".bin").read_bytes())

        first_json = self.output_path.read_bytes()
        first_binary = self.output_path.with_suffix(".bin").read_bytes()
        merge_gltf_animations(self.model_path, self.animation_path, self.output_path)
        self.assertEqual(first_json, self.output_path.read_bytes())
        self.assertEqual(first_binary, self.output_path.with_suffix(".bin").read_bytes())

    def test_merge_rejects_animation_target_missing_from_model(self) -> None:
        donor = {
            "asset": {"version": "2.0"},
            "nodes": [{"name": "MissingBone"}],
            "buffers": [{"byteLength": 4}],
            "bufferViews": [{"buffer": 0, "byteLength": 4}],
            "accessors": [{"bufferView": 0, "componentType": 5126, "count": 1, "type": "SCALAR"}],
            "animations": [
                {
                    "name": "Idle_Loop",
                    "samplers": [{"input": 0, "output": 0}],
                    "channels": [{"sampler": 0, "target": {"node": 0, "path": "rotation"}}],
                }
            ],
        }
        write_glb(self.animation_path, donor, b"ANIM")

        with self.assertRaisesRegex(ValueError, "MissingBone"):
            merge_gltf_animations(self.model_path, self.animation_path, self.output_path)


if __name__ == "__main__":
    unittest.main()
