import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import online
import release

PROJECT = "d778da03-d0cb-4da6-a7a0-9ccc24865d1d"
COMMIT = "a" * 40


class OnlineReleaseTests(unittest.TestCase):
    def test_unlinked_projects_fail_before_network_requests(self):
        with tempfile.TemporaryDirectory() as directory:
            settings = Path(directory) / "ProjectSettings.asset"
            for value in ("", "garbage", "00000000-0000-0000-0000-000000000000"):
                with self.subTest(value=value):
                    settings.write_text(f"  cloudProjectId: {value}\n  cloudEnabled: 0\n")
                    with self.assertRaises(ValueError):
                        online.read_project_id(settings)

    def test_link_survives_unity_crlf_serialization(self):
        with tempfile.TemporaryDirectory() as directory:
            settings = Path(directory) / "ProjectSettings.asset"
            settings.write_bytes(f"  cloudProjectId: {PROJECT}\r\n  cloudEnabled: 1\r\n".encode())
            self.assertEqual(PROJECT, online.read_project_id(settings))

    def test_successful_join_accepts_reencrypted_host_blob_and_cleans_players(self):
        endpoint = {"connectionType": "dtls"}
        server = {"ipV4": "127.0.0.1", "port": 1234}
        responses = [
            {"userId": "test-host", "idToken": "host-token"},
            {"userId": "test-client", "idToken": "client-token"},
            {"data": {"allocation": {"allocationId": "test-allocation", "connectionData": "original",
                                    "relayServer": server, "serverEndpoints": [endpoint]}}},
            {"data": {"joinCode": "TESTCODE"}},
            {"data": {"allocation": {"hostConnectionData": "reencrypted", "relayServer": server,
                                     "serverEndpoints": [endpoint]}}},
            {}, {},
        ]
        with patch.object(online, "request_json", side_effect=responses) as request:
            with contextlib.redirect_stdout(io.StringIO()):
                online.smoke_check(PROJECT)
            self.assertEqual(7, request.call_count)
            self.assertEqual("DELETE", request.call_args_list[-1].kwargs["method"])
            self.assertEqual("DELETE", request.call_args_list[-2].kwargs["method"])

    def test_relay_failure_blocks_release_and_cleans_players(self):
        responses = [
            {"userId": "test-host", "idToken": "host-token"},
            {"userId": "test-client", "idToken": "client-token"},
            RuntimeError("Relay disabled"), {}, {},
        ]
        with patch.object(online, "request_json", side_effect=responses) as request:
            with self.assertRaisesRegex(RuntimeError, "Relay disabled"):
                online.smoke_check(PROJECT)
            self.assertEqual(5, request.call_count)

    def test_packaging_rejects_missing_or_wrong_cloud_project(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            build = root / "player"
            settings = root / "ProjectSettings.asset"
            settings.write_text(f"  cloudProjectId: {PROJECT}\n")
            for name in ("Bluff.exe", "UnityPlayer.dll", "Bluff_Data/globalgamemanagers",
                         "Bluff_Data/Managed/Bluff.Game.dll", "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll"):
                path = build / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"test player fixture")
            info = {"version": "0.1.1", "commit": COMMIT, "target": "StandaloneWindows64", "unityVersion": "6000.3.25f1"}
            for project in ("", "01234567-89ab-cdef-0123-456789abcdef"):
                info["cloudProjectId"] = project
                (build / "build-info.json").write_text(json.dumps(info))
                with self.assertRaises(ValueError):
                    release.package_command(build, root / "release", "0.1.1", COMMIT, settings)
                self.assertFalse((root / "release").exists())
            info["cloudProjectId"] = PROJECT
            (build / "build-info.json").write_text(json.dumps(info))
            with self.assertRaisesRegex(ValueError, "player data"):
                release.package_command(build, root / "release", "0.1.1", COMMIT, settings)
            (build / "Bluff_Data/globalgamemanagers").write_bytes(b"fixture\x00" + PROJECT.encode("ascii") + b"\x00")
            with contextlib.redirect_stdout(io.StringIO()):
                release.package_command(build, root / "release", "0.1.1", COMMIT, settings)
            manifest = json.loads((root / "release/release-manifest.json").read_text())
            self.assertEqual(PROJECT, manifest["cloudProjectId"])


if __name__ == "__main__":
    unittest.main()
