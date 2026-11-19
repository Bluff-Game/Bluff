"""Check the public Unity project link and real Authentication/Relay availability."""

from __future__ import annotations

import argparse
import json
import re
import sys
import uuid
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

AUTH_URL = "https://player-auth.services.api.unity.com"
RELAY_URL = "https://relay-allocations.services.api.unity.com"


def validate_project_id(value: str) -> str:
    try:
        project_id = uuid.UUID(value)
    except (ValueError, AttributeError) as error:
        raise ValueError("Unity Cloud project ID must be a non-empty UUID") from error
    if not project_id.int:
        raise ValueError("Unity Cloud project ID cannot be the zero UUID")
    return str(project_id)


def read_project_id(settings: Path) -> str:
    source = settings.read_text(encoding="utf-8-sig")
    match = re.search(r"^  cloudProjectId:[ \t]*([^\r\n]*)$", source, re.MULTILINE)
    if not match:
        raise ValueError("ProjectSettings is missing cloudProjectId")
    return validate_project_id(match[1].strip())


def request_json(url: str, headers: dict, body: dict | None = None, method: str = "POST") -> dict:
    payload = None if body is None else json.dumps(body).encode("utf-8")
    request = Request(url, data=payload, headers={"Content-Type": "application/json", **headers}, method=method)
    try:
        with urlopen(request, timeout=25) as response:
            raw = response.read()
            return json.loads(raw) if raw else {}
    except HTTPError as error:
        # Never print response bodies: successful service responses contain player credentials.
        raise RuntimeError(f"{method} {url.split('/v1/')[0]} failed (HTTP {error.code}); check Authentication and Relay configuration") from None
    except (URLError, TimeoutError) as error:
        raise RuntimeError("Unity Services could not be reached within the request timeout") from error


def smoke_check(project_id: str) -> None:
    project_headers = {"ProjectId": project_id, "UnityEnvironment": "production"}
    players = []
    try:
        for _ in range(2):
            players.append(request_json(f"{AUTH_URL}/v1/authentication/anonymous", project_headers, {}))
        if players[0]["userId"] == players[1]["userId"]:
            raise ValueError("Smoke test requires two independent anonymous players")
        host_headers = {"Authorization": f"Bearer {players[0]['idToken']}"}
        client_headers = {"Authorization": f"Bearer {players[1]['idToken']}"}
        allocation = request_json(f"{RELAY_URL}/v1/allocate", host_headers, {"maxConnections": 3})["data"]["allocation"]
        code = request_json(f"{RELAY_URL}/v1/joincode", host_headers, {"allocationId": allocation["allocationId"]})["data"]["joinCode"]
        joined = request_json(f"{RELAY_URL}/v1/join", client_headers, {"joinCode": code})["data"]["allocation"]
        # Relay can re-encrypt connection blobs for the joining player; their bytes need not match.
        if not joined.get("hostConnectionData") or joined["relayServer"] != allocation["relayServer"]:
            raise ValueError("Relay join did not resolve to the host server")
        for result in (allocation, joined):
            if not any(endpoint["connectionType"] == "dtls" for endpoint in result["serverEndpoints"]):
                raise ValueError("Relay allocation has no DTLS endpoint for the Windows player")
        print("PASS: two anonymous players, Relay allocation, join code, host server and DTLS endpoints.")
    finally:
        # Only delete the disposable players created by this invocation. Unbound Relay allocations expire.
        for player in players:
            try:
                request_json(f"{AUTH_URL}/v1/users/{player['userId']}",
                             {**project_headers, "Authorization": f"Bearer {player['idToken']}"}, method="DELETE")
            except RuntimeError:
                print("Warning: a temporary smoke-test player could not be removed.", file=sys.stderr)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--settings", type=Path, default=Path("ProjectSettings/ProjectSettings.asset"))
    parser.add_argument("--smoke", action="store_true", help="Exercise live Authentication and Relay APIs (no gameplay transport)")
    args = parser.parse_args()
    project_id = read_project_id(args.settings)
    print(f"Unity Cloud project: {project_id}")
    if args.smoke:
        smoke_check(project_id)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)
