from __future__ import annotations

import json
import struct
import sys
import tempfile
import unittest
import unittest.mock
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
PROJECT_ROOT = TOOLS_DIR.parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from comfy_batch import (
    DEFAULT_LEASE_CHUNK,
    CheckpointLease,
    ComfyError,
    RunObserver,
    _binary_preview,
    execute_prompt,
    hold_checkpoint_lease,
    lease_chunk,
    paired_node_contract,
    prompt_checkpoint,
    publish_workflows,
    ui_workflow_path,
    validate_workflow_pair,
    websocket_url,
)


def prompt_document() -> dict:
    return {
        "1": {
            "class_type": "EmptyLatentImage",
            "inputs": {"width": 512, "height": 512, "batch_size": 1},
            "_meta": {"title": "Latent"},
        },
        "2": {
            "class_type": "SaveImage",
            "inputs": {"images": ["1", 0], "filename_prefix": "test"},
            "_meta": {"title": "Save"},
        },
    }


def workflow_document() -> dict:
    return {
        "last_node_id": 2,
        "last_link_id": 1,
        "nodes": [
            {
                "id": 1,
                "type": "EmptyLatentImage",
                "inputs": [],
                "outputs": [
                    {"name": "LATENT", "type": "LATENT", "links": [1]}
                ],
            },
            {
                "id": 2,
                "type": "SaveImage",
                "inputs": [{"name": "images", "type": "IMAGE", "link": 1}],
                "outputs": [],
            },
        ],
        "links": [[1, 1, 0, 2, 0, "LATENT"]],
        "version": 0.4,
    }


class WorkflowPairTests(unittest.TestCase):
    def test_every_project_api_workflow_has_a_valid_canvas_pair(self) -> None:
        directory = PROJECT_ROOT / "docs/art-direction/comfyui"
        api_paths = sorted(directory.glob("*.api.json"))
        self.assertGreaterEqual(len(api_paths), 5)
        for api_path in api_paths:
            with self.subTest(workflow=api_path.name):
                validate_workflow_pair(api_path)

    def test_canvas_path_is_derived_from_api_suffix(self) -> None:
        self.assertEqual(
            Path("example.workflow.json"),
            ui_workflow_path(Path("example.api.json")),
        )
        with self.assertRaisesRegex(ComfyError, "must end with"):
            ui_workflow_path(Path("example.json"))

    def test_pair_contract_checks_types_and_links(self) -> None:
        self.assertEqual(
            [],
            paired_node_contract(prompt_document(), workflow_document()),
        )
        broken = workflow_document()
        broken["links"][0][1] = 99
        self.assertIn(
            "node 2.images link differs",
            paired_node_contract(prompt_document(), broken)[0],
        )

    def test_validate_requires_sibling_canvas(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            api_path = Path(directory) / "example.api.json"
            api_path.write_text(json.dumps(prompt_document()), encoding="utf-8")
            with self.assertRaisesRegex(ComfyError, "canvas workflow is missing"):
                validate_workflow_pair(api_path)
            ui_workflow_path(api_path).write_text(
                json.dumps(workflow_document()),
                encoding="utf-8",
            )
            prompt, canvas_path, workflow = validate_workflow_pair(api_path)
            self.assertEqual(prompt_document(), prompt)
            self.assertEqual(ui_workflow_path(api_path), canvas_path)
            self.assertEqual(workflow_document(), workflow)

    def test_publish_workflows_writes_comfyui_userdata(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            api_path = root / "example.api.json"
            api_path.write_text(json.dumps(prompt_document()), encoding="utf-8")
            ui_workflow_path(api_path).write_text(
                json.dumps(workflow_document()),
                encoding="utf-8",
            )

            with unittest.mock.patch(
                "comfy_batch.request_bytes",
                return_value=b'"workflows/Project-C/example.json"',
            ) as request:
                published = publish_workflows(
                    "http://comfy.test",
                    root,
                )

            self.assertEqual(
                ["workflows/Project-C/example.json"],
                published,
            )
            _, request_path = request.call_args.args
            self.assertEqual(
                "/userdata/workflows%2FProject-C%2Fexample.json"
                "?overwrite=true",
                request_path,
            )
            self.assertEqual("POST", request.call_args.kwargs["method"])
            self.assertEqual(
                {"Content-Type": "application/json"},
                request.call_args.kwargs["headers"],
            )
            self.assertEqual(
                workflow_document(),
                json.loads(request.call_args.kwargs["body"]),
            )


class ProgressTests(unittest.TestCase):
    def test_execute_embeds_canvas_and_writes_exact_run_snapshot(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            api_path = root / "example.api.json"
            api_path.write_text(json.dumps(prompt_document()), encoding="utf-8")
            ui_workflow_path(api_path).write_text(
                json.dumps(workflow_document()),
                encoding="utf-8",
            )
            submitted: dict = {}

            def fake_request_json(
                _base_url: str,
                path: str,
                *,
                method: str = "GET",
                payload=None,
                timeout: float = 30.0,
            ):
                del timeout
                if path == "/prompt" and method == "POST":
                    submitted.update(payload)
                    return {"prompt_id": payload["prompt_id"]}
                if path.startswith("/history/"):
                    prompt_id = path.rsplit("/", 1)[-1]
                    return {
                        prompt_id: {
                            "status": {"completed": True},
                            "outputs": {
                                "2": {
                                    "images": [
                                        {
                                            "filename": "result.png",
                                            "subfolder": "",
                                            "type": "output",
                                        }
                                    ]
                                }
                            },
                        }
                    }
                raise AssertionError(path)

            with (
                unittest.mock.patch(
                    "comfy_batch.open_progress_socket",
                    return_value=None,
                ),
                unittest.mock.patch(
                    "comfy_batch.request_json",
                    side_effect=fake_request_json,
                ),
                unittest.mock.patch(
                    "comfy_batch.request_bytes",
                    return_value=b"png",
                ),
            ):
                prompt_id, outputs = execute_prompt(
                    "http://comfy.test",
                    prompt_document(),
                    root / "output",
                    timeout=1.0,
                    poll_interval=0.01,
                    workflow_path=api_path,
                    prefer_frontend=False,
                    progress=None,
                )

            self.assertEqual([root / "output/result.png"], outputs)
            self.assertEqual(
                workflow_document(),
                submitted["extra_data"]["extra_pnginfo"]["workflow"],
            )
            manifest = json.loads(
                (
                    root
                    / "output/_runs"
                    / prompt_id
                    / "run-manifest.json"
                ).read_text(encoding="utf-8")
            )
            self.assertEqual("completed", manifest["status"])
            self.assertEqual("history-poll", manifest["observation_mode"])

    def test_websocket_url_preserves_base_path(self) -> None:
        self.assertEqual(
            "ws://127.0.0.1:8188/ws?clientId=client-1",
            websocket_url("http://127.0.0.1:8188", "client-1"),
        )
        self.assertEqual(
            "wss://example.com/comfy/ws?clientId=c",
            websocket_url("https://example.com/comfy", "c"),
        )

    def test_binary_preview_decodes_png_frames(self) -> None:
        png = b"\x89PNG\r\n\x1a\ncontent"
        frame = struct.pack(">II", 1, 2) + png
        self.assertEqual((png, ".png"), _binary_preview(frame))

    def test_observer_persists_live_state_and_outputs(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            observer = RunObserver(
                root,
                prompt_id="prompt-1",
                client_id="client-1",
                prompt=prompt_document(),
                workflow=workflow_document(),
                api_path=Path("example.api.json"),
                canvas_path=Path("example.workflow.json"),
                mode="websocket",
                callback=None,
            )
            observer.submitted()
            observer.emit(
                {
                    "type": "executing",
                    "data": {"prompt_id": "prompt-1", "node": "2"},
                }
            )
            observer.emit(
                {
                    "type": "progress",
                    "data": {
                        "prompt_id": "prompt-1",
                        "node": "2",
                        "value": 3,
                        "max": 10,
                    },
                }
            )
            output = root / "result.png"
            observer.complete([output])
            manifest = json.loads(
                observer.manifest_path.read_text(encoding="utf-8")
            )
            self.assertEqual("completed", manifest["status"])
            self.assertEqual("Save", manifest["current_node_title"])
            self.assertEqual(0.3, manifest["progress"]["fraction"])
            self.assertEqual([str(output)], manifest["outputs"])
            self.assertEqual(
                2,
                len(observer.events_path.read_text(encoding="utf-8").splitlines()),
            )


class CheckpointLeaseTests(unittest.TestCase):
    def test_prompt_checkpoint_reads_loader_node(self) -> None:
        prompt = {
            "1": {
                "class_type": "CheckpointLoaderSimple",
                "inputs": {"ckpt_name": "zavychromaxl_v100.safetensors"},
            },
            "2": {"class_type": "SaveImage", "inputs": {}},
        }
        self.assertEqual(
            "zavychromaxl_v100.safetensors", prompt_checkpoint(prompt)
        )

    def test_prompt_checkpoint_returns_none_without_loader(self) -> None:
        self.assertIsNone(prompt_checkpoint({"1": {"class_type": "SaveImage"}}))

    def test_lease_holds_lock_across_chunk_then_yields(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "lease"
            lease = CheckpointLease("a.safetensors", chunk=2, path=path)
            with lease:
                lease.tick()
                first = lease._handle
                self.assertIsNotNone(first)
                lease.tick()
                # chunk 를 아직 다 쓰지 않았으면 같은 핸들을 유지한다.
                self.assertIs(first, lease._handle)
                lease.tick()
                # chunk 소진 후에는 락을 놓았다가 다시 잡는다.
                self.assertIsNotNone(lease._handle)
                self.assertIsNot(first, lease._handle)
            self.assertIsNone(lease._handle)

    def test_lease_without_checkpoint_never_locks(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            lease = CheckpointLease(None, path=Path(tmp) / "lease")
            with lease:
                lease.tick()
                self.assertIsNone(lease._handle)

    def test_lease_degrades_when_lock_unavailable(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "missing-dir" / "lease"
            lease = CheckpointLease("a.safetensors", path=path)
            with lease:
                lease.tick()
                # 락을 못 잡아도 예외 없이 진행한다.
                self.assertIsNone(lease._handle)
                self.assertIsNone(lease.checkpoint)

    def test_lease_rejects_zero_chunk(self) -> None:
        with self.assertRaises(ValueError):
            CheckpointLease("a.safetensors", chunk=0)

    def test_chunk_env_can_disable_leasing(self) -> None:
        with unittest.mock.patch.dict("os.environ", {"COMFY_LEASE_CHUNK": "0"}):
            self.assertEqual(0, lease_chunk())
            # 비활성일 때는 실제 락 파일을 건드리지 않는다 —
            # 돌고 있는 배치가 쥔 락에 테스트가 걸리면 안 된다.
            hold_checkpoint_lease("a.safetensors")

    def test_chunk_env_falls_back_when_unparsable(self) -> None:
        with unittest.mock.patch.dict("os.environ", {"COMFY_LEASE_CHUNK": "nope"}):
            self.assertEqual(DEFAULT_LEASE_CHUNK, lease_chunk())


if __name__ == "__main__":
    unittest.main()
